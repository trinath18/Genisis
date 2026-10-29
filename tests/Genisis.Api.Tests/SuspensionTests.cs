using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Maintenance.Suspension;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Maintenance > Suspension: the validators and the service, against a fake repository.</summary>
public class SuspensionRulesTests
{
    private sealed class FakeRepository(int saveResult = 0, int categoryDelete = 0, int detailDelete = 0) : ISuspensionRepository
    {
        public List<string> Calls { get; } = [];

        public Task<List<SuspensionCategory>> SearchCategoriesAsync(IDbSession session, string? code) => Task.FromResult(new List<SuspensionCategory> { new() { Code = "01" } });

        public Task<int> SaveCategoryAsync(IDbSession session, string code, string description, bool edit, string userCode, DateTime today)
        {
            Calls.Add($"cat {code}|{description}|{edit}");
            return Task.FromResult(saveResult);
        }

        public Task<int> DeleteCategoryAsync(IDbSession session, string code) => Task.FromResult(categoryDelete);

        public Task<List<SuspensionDetailRow>> SearchDetailsAsync(IDbSession session, string? selection, string? key, string? payor) => Task.FromResult(new List<SuspensionDetailRow>());

        public Task<int> SaveDetailAsync(IDbSession session, string selection, string key, string remarks, string payor, bool edit, string userCode, DateTime today)
        {
            Calls.Add($"det {selection}|{key}|{remarks}|{payor}|{edit}");
            return Task.FromResult(saveResult);
        }

        public Task<int> DeleteDetailAsync(IDbSession session, string selection, string key) => Task.FromResult(detailDelete);
    }

    private static SuspensionService Service(FakeSessions sessions, FakeRepository repository) => new(sessions, repository, NullLogger<SuspensionService>.Instance);

    [Fact]
    public void A_category_needs_a_code_and_then_a_description()
    {
        var validator = new SuspensionCategoryRequestValidator();
        Assert.Equal("Please Enter Code", validator.FirstError(new SuspensionCategoryRequest { Description = "X" }));
        Assert.Equal("Please Enter Description", validator.FirstError(new SuspensionCategoryRequest { Code = "03" }));
        Assert.Equal("Code can be at most 4 characters.", validator.FirstError(new SuspensionCategoryRequest { Code = "12345", Description = "X" }));
        Assert.Null(validator.FirstError(new SuspensionCategoryRequest { Code = "03", Description = "X" }));
    }

    [Fact]
    public void A_suspension_is_checked_in_the_desktops_order_and_a_member_number_has_to_start_with_the_payor_code()
    {
        var validator = new SuspensionDetailRequestValidator();
        var r = new SuspensionDetailRequest();
        Assert.Equal("Please Enter Selection", validator.FirstError(r));
        r.Selection = "01 - Member";
        Assert.Equal("Please Enter Member Number/Agent Code", validator.FirstError(r));
        r.Key = "ab123";
        Assert.Equal("Please Enter Description", validator.FirstError(r));
        r.Description = "x";
        Assert.Equal("Please Enter Payor", validator.FirstError(r));
        r.Payor = "XL - A PAYOR";
        Assert.Equal("Payor Code Not Match", validator.FirstError(r));
        r.Payor = "ab - A PAYOR";
        Assert.Null(validator.FirstError(r));
        r.Selection = "02 - Agent";
        r.Payor = "XL - A PAYOR";
        Assert.Null(validator.FirstError(r));
        r.Selection = "03";
        Assert.Equal("Select 01 - Member or 02 - Agent.", validator.FirstError(r));
    }

    [Fact]
    public async Task Codes_and_descriptions_are_trimmed_and_upper_cased_like_ChkStr_and_the_save_commits()
    {
        var sessions = new FakeSessions();
        var repository = new FakeRepository();
        await Service(sessions, repository).SaveCategoryAsync(new SuspensionCategoryRequest { Code = " 03 ", Description = " blocked " }, false, "USER1");
        await Service(sessions, repository).SaveDetailAsync(new SuspensionDetailRequest { Selection = "01 - Member", Key = " ab12 ", Description = "late", Payor = "ab - X" }, true, "USER1");
        Assert.Equal(["cat 03|BLOCKED|False", "det 01|AB12|LATE|AB|True"], repository.Calls);
        Assert.True(sessions.Last.Committed);
    }

    [Theory]
    [InlineData(1, false, typeof(ConflictException), "Record Duplicated! Please Change The Corresponding Field")]
    [InlineData(2, true, typeof(NotFoundException), "Record not found")]
    public async Task A_save_that_did_not_happen_says_why_and_commits_nothing(int result, bool edit, Type exception, string message)
    {
        var sessions = new FakeSessions();
        var failure = await Assert.ThrowsAnyAsync<BusinessRuleException>(() => Service(sessions, new FakeRepository(result)).SaveCategoryAsync(new SuspensionCategoryRequest { Code = "03", Description = "X" }, edit, "USER1"));
        Assert.IsType(exception, failure);
        Assert.Equal(message, failure.Message);
        Assert.False(sessions.Last.Committed);
    }

    [Fact]
    public async Task A_category_a_suspension_uses_cannot_be_deleted()
    {
        var failure = await Assert.ThrowsAsync<ConflictException>(() => Service(new FakeSessions(), new FakeRepository(categoryDelete: StoredProcedures.Conflict)).DeleteCategoryAsync("01", "USER1"));
        Assert.Equal("This record cannot be deleted because it is used in Suspension Detail table!", failure.Message);
        await Assert.ThrowsAsync<NotFoundException>(() => Service(new FakeSessions(), new FakeRepository(categoryDelete: StoredProcedures.NotFound)).DeleteCategoryAsync("09", "USER1"));
        await Assert.ThrowsAsync<NotFoundException>(() => Service(new FakeSessions(), new FakeRepository(detailDelete: StoredProcedures.NotFound)).DeleteDetailAsync("01", "X", "USER1"));
    }

    [Fact]
    public void The_payor_shows_as_code_dash_name()
    {
        Assert.Equal("XL - A PAYOR", new SuspensionDetailRow { PayorCode = "XL", PayorName = "A PAYOR" }.Payor);
        Assert.Equal("XL", new SuspensionDetailRow { PayorCode = "XL" }.Payor);
    }
}

/// <summary>Maintenance > Suspension against the database. Every write is made in a session that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class SuspensionDatabaseTests
{
    private static SuspensionRepository Repository => new();

    private const string Checksum = """
        SELECT (SELECT CHECKSUM_AGG(CHECKSUM(SuspensionCode, SuspensionDesc, LastUpdateUser, LastUpdateDate)) FROM dbo.SuspensionCat)
             + (SELECT CHECKSUM_AGG(CHECKSUM(SUSIndex, AgentCode, MembershipNo, Remarks, PayorCode, SuspensionCode)) FROM dbo.SuspensionDetail)
             + (SELECT COUNT(*) FROM dbo.SuspensionDetail)
        """;

    [Fact]
    public async Task The_categories_are_searched_added_changed_and_deleted_unless_a_suspension_uses_them_and_nothing_survives_the_rollback()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var before = await read.Connection.ExecuteScalarAsync<int>(Checksum, transaction: read.Transaction);
        var all = await Repository.SearchCategoriesAsync(read, null);
        Assert.Equal(await read.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.SuspensionCat", transaction: read.Transaction), all.Count);
        Assert.Equal(["01"], (await Repository.SearchCategoriesAsync(read, "01 x")).Select(c => c.Code));

        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var day = new DateTime(2026, 10, 29, 9, 0, 0);
            Assert.Equal(0, await Repository.SaveCategoryAsync(write, "ZZ", "ZZ TEST", false, "ZZTEST", day));
            Assert.Equal(1, await Repository.SaveCategoryAsync(write, "ZZ", "AGAIN", false, "ZZTEST", day));
            Assert.Equal(2, await Repository.SaveCategoryAsync(write, "ZY", "NONE", true, "ZZTEST", day));
            Assert.Equal(0, await Repository.SaveCategoryAsync(write, "ZZ", "ZZ CHANGED", true, "ZZTEST", day));
            var row = await write.Connection.QueryFirstAsync("SELECT RTRIM(SuspensionDesc) AS Description, RTRIM(LastUpdateUser) AS UserCode, LastUpdateDate FROM dbo.SuspensionCat WHERE SuspensionCode = 'ZZ'", transaction: write.Transaction);
            Assert.Equal(("ZZ CHANGED", "ZZTEST", day.Date), ((string)row.Description, (string)row.UserCode, (DateTime)row.LastUpdateDate));

            Assert.Equal(0, await Repository.SaveDetailAsync(write, "ZZ", "ZZ-USES-IT", "x", "XL", false, "ZZTEST", day));
            Assert.Equal(StoredProcedures.Conflict, await Repository.DeleteCategoryAsync(write, "ZZ"));
            Assert.Equal(0, await Repository.DeleteDetailAsync(write, "ZZ", "ZZ-USES-IT"));
            Assert.Equal(StoredProcedures.Ok, await Repository.DeleteCategoryAsync(write, "ZZ"));
            Assert.Equal(StoredProcedures.NotFound, await Repository.DeleteCategoryAsync(write, "ZZ"));
        }

        Assert.Equal(before, await read.Connection.ExecuteScalarAsync<int>(Checksum, transaction: read.Transaction));
    }

    [Fact]
    public async Task A_member_and_an_agent_suspension_are_added_found_with_the_payor_name_changed_and_removed_and_a_duplicate_key_is_refused()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var write = await TestDb.Sessions().BeginAsync();
        var (payor, name) = await write.Connection.QueryFirstAsync<(string, string)>("SELECT TOP (1) RTRIM(PAYCode), RTRIM(PAYCompanyName) FROM dbo.PAY ORDER BY PAYCode", transaction: write.Transaction);
        var day = new DateTime(2026, 10, 29, 9, 0, 0);

        Assert.Equal(0, await Repository.SaveDetailAsync(write, "01", payor + "-ZZ00001*01", "member note", payor, false, "ZZTEST", day));
        Assert.Equal(0, await Repository.SaveDetailAsync(write, "02", "AGENT-ZZ1", "agent note", payor, false, "ZZTEST", day));
        Assert.Equal(1, await Repository.SaveDetailAsync(write, "01", payor + "-ZZ00001*01", "again", payor, false, "ZZTEST", day));
        Assert.Equal(1, await Repository.SaveDetailAsync(write, "02", "AGENT-ZZ1", "again", payor, false, "ZZTEST", day));

        var members = await Repository.SearchDetailsAsync(write, "01", payor + "-ZZ00001*01", payor);
        var member = Assert.Single(members);
        Assert.Equal(("01", payor + "-ZZ00001*01", "member note", payor, name, $"{payor} - {name}"), (member.Selection, member.KeyCode, member.Description, member.PayorCode, member.PayorName, member.Payor));
        var agent = Assert.Single(await Repository.SearchDetailsAsync(write, "02", "AGENT-ZZ1", null));
        Assert.Equal(("02", "AGENT-ZZ1", "agent note"), (agent.Selection, agent.KeyCode, agent.Description));
        Assert.Equal(2, (await Repository.SearchDetailsAsync(write, null, null, payor)).Count(d => d.KeyCode is "AGENT-ZZ1" || d.KeyCode == payor + "-ZZ00001*01"));

        Assert.Equal(0, await Repository.SaveDetailAsync(write, "02", "AGENT-ZZ1", "agent changed", payor, true, "ZZTEST2", day));
        Assert.Equal("agent changed", Assert.Single(await Repository.SearchDetailsAsync(write, "02", "AGENT-ZZ1", null)).Description);
        Assert.Equal(2, await Repository.SaveDetailAsync(write, "02", "NO-SUCH-AGENT", "x", payor, true, "ZZTEST", day));
        Assert.Equal(1, await write.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.SuspensionDetail WHERE AgentCode = 'AGENT-ZZ1' AND MembershipNo IS NULL AND LastUpdateUser = 'ZZTEST2'", transaction: write.Transaction));

        Assert.Equal(0, await Repository.DeleteDetailAsync(write, "01", payor + "-ZZ00001*01"));
        Assert.Equal(1, await Repository.DeleteDetailAsync(write, "01", payor + "-ZZ00001*01"));
        Assert.Equal(0, await Repository.DeleteDetailAsync(write, "02", "AGENT-ZZ1"));
        Assert.Empty(await Repository.SearchDetailsAsync(write, "02", "AGENT-ZZ1", null));
    }
}
