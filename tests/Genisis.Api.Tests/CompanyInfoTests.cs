using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Maintenance.CompanyInfo;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Maintenance &gt; Company Info: the validator and the service, against a fake repository.</summary>
public class CompanyInfoRulesTests
{
    private sealed class FakeRepository(int result = 0) : ICompanyInfoRepository
    {
        public List<(CompanyInfoRequest Request, string User, DateTime Today)> Saved { get; } = [];

        public Task<CompanyInfoRow> GetAsync(IDbSession session) => Task.FromResult(new CompanyInfoRow { TableExists = true, Found = true, Code = "MX", Name = "MediExpress" });

        public Task<int> SaveAsync(IDbSession session, CompanyInfoRequest request, string userCode, DateTime today)
        {
            Saved.Add((request, userCode, today));
            return Task.FromResult(result);
        }
    }

    private static CompanyInfoRequest Full() => new() { Code = "MX", Name = "MediExpress (M) Sdn Bhd", Address = "1 Jalan", Phone = "03-1234.5678", Fax = "0312345678", Email = "a@b.my", Homepage = "www.b.my", CurrentYear = "2026" };

    [Fact]
    public void The_code_the_name_and_the_year_are_asked_for_in_the_desktops_order_one_message_at_a_time()
    {
        var validator = new CompanyInfoRequestValidator();
        var request = new CompanyInfoRequest();
        Assert.Equal("Please Enter Company Code", validator.FirstError(request));
        request.Code = "MX";
        Assert.Equal("Please Enter Company Name", validator.FirstError(request));
        request.Name = "MediExpress";
        Assert.Equal("Please Enter Company Current Year", validator.FirstError(request));
        request.CurrentYear = "26";
        Assert.Null(validator.FirstError(request));
        request.CurrentYear = "abcd";
        Assert.Equal("Company Current Year must be a year of four digits.", validator.FirstError(request));
        request.CurrentYear = "2026";
        Assert.Null(validator.FirstError(request));
    }

    [Fact]
    public void The_phone_and_fax_take_what_the_number_boxes_took_and_the_box_lengths_hold()
    {
        var validator = new CompanyInfoRequestValidator();
        Assert.Null(validator.FirstError(Full()));
        Assert.Equal("Phone No must be a number.", validator.FirstError(Full().With(r => r.Phone = "03 1234")));
        Assert.Equal("Fax No must be a number.", validator.FirstError(Full().With(r => r.Fax = "x")));
        Assert.Equal("Phone No is too long.", validator.FirstError(Full().With(r => r.Phone = "1234567890123456")));
        Assert.Equal("Company Name is too long.", validator.FirstError(Full().With(r => r.Name = new string('n', 151))));
        Assert.Equal("Address is too long.", validator.FirstError(Full().With(r => r.Address = new string('a', 201))));
        Assert.Equal("Email is too long.", validator.FirstError(Full().With(r => r.Email = new string('e', 151))));
        Assert.Equal("Homepage is too long.", validator.FirstError(Full().With(r => r.Homepage = new string('h', 151))));
        Assert.Equal("Company Code is too long.", validator.FirstError(Full().With(r => r.Code = new string('c', 51))));
    }

    [Fact]
    public async Task Saving_commits_with_the_user_and_the_day()
    {
        var sessions = new FakeSessions();
        var repository = new FakeRepository();
        await new CompanyInfoService(sessions, repository, NullLogger<CompanyInfoService>.Instance).SaveAsync(Full(), "USER1");
        Assert.Equal(("USER1", DateTime.Today), (repository.Saved[0].User, repository.Saved[0].Today));
        Assert.True(sessions.Last.Committed);
    }

    [Fact]
    public async Task A_missing_table_is_said_so_and_nothing_is_committed()
    {
        var sessions = new FakeSessions();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => new CompanyInfoService(sessions, new FakeRepository(-1), NullLogger<CompanyInfoService>.Instance).SaveAsync(Full(), "USER1"));
        Assert.Equal("The Company table is not in this database.", error.Message);
        Assert.False(sessions.Last.Committed);
    }

    [Fact]
    public async Task Loading_returns_the_saved_row()
    {
        var row = await new CompanyInfoService(new FakeSessions(), new FakeRepository(), NullLogger<CompanyInfoService>.Instance).GetAsync();
        Assert.Equal((true, true, "MX"), (row.TableExists, row.Found, row.Code));
    }
}

internal static class CompanyInfoRequestExtensions
{
    /// <summary>The request with one box changed.</summary>
    public static CompanyInfoRequest With(this CompanyInfoRequest request, Action<CompanyInfoRequest> change)
    {
        change(request);
        return request;
    }
}

/// <summary>Maintenance &gt; Company Info against the database. dbo.Company is not in the development databases, so the writes run on a copy of the table made inside a session that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class CompanyInfoDatabaseTests
{
    private static CompanyInfoRepository Repository => new();

    private const string CreateTable = """
        CREATE TABLE dbo.Company (COMCode nvarchar(50) NOT NULL, COMName nvarchar(150) NULL, COMAddress nvarchar(200) NULL, COMPhone nvarchar(15) NULL, COMFax nvarchar(15) NULL,
            COMEmail nvarchar(150) NULL, COMHomepage nvarchar(150) NULL, COMCurrentYear int NULL, COMLastUpdateUser nvarchar(20) NULL, COMLastUpdateDate datetime NULL)
        """;

    private static CompanyInfoRequest Request(string code) => new() { Code = code, Name = "MediExpress (M) Sdn Bhd", Address = "1 Jalan Satu", Phone = "03-1234.5678", Fax = "0312345678", Email = "a@b.my", Homepage = "www.b.my", CurrentYear = "2026" };

    [Fact]
    public async Task Without_the_table_the_screen_says_so_and_a_save_changes_nothing()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().BeginAsync();
        if (await session.Connection.ExecuteScalarAsync<int?>("SELECT OBJECT_ID(N'dbo.Company', N'U')", transaction: session.Transaction) is not null) return;
        var row = await Repository.GetAsync(session);
        Assert.Equal((false, false), (row.TableExists, row.Found));
        Assert.Equal(-1, await Repository.SaveAsync(session, Request("MX"), "ZZTEST", DateTime.Today));
    }

    [Fact]
    public async Task A_save_adds_the_row_to_an_empty_table_then_updates_the_first_row_and_stamps_the_user_and_day_and_nothing_survives_the_rollback()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            if (await write.Connection.ExecuteScalarAsync<int?>("SELECT OBJECT_ID(N'dbo.Company', N'U')", transaction: write.Transaction) is null)
                await write.Connection.ExecuteAsync(CreateTable, transaction: write.Transaction);
            else
                await write.Connection.ExecuteAsync("DELETE FROM dbo.Company", transaction: write.Transaction);

            var empty = await Repository.GetAsync(write);
            Assert.Equal((true, false), (empty.TableExists, empty.Found));
            Assert.Equal(1, await Repository.SaveAsync(write, Request("MX"), "ZZTEST", new DateTime(2026, 10, 29, 12, 0, 0)));
            var added = await Repository.GetAsync(write);
            Assert.Equal((true, true, "MX", "MediExpress (M) Sdn Bhd", "1 Jalan Satu", "03-1234.5678", "0312345678", "a@b.my", "www.b.my", "2026"),
                (added.TableExists, added.Found, added.Code, added.Name, added.Address, added.Phone, added.Fax, added.Email, added.Homepage, added.CurrentYear));

            Assert.Equal(0, await Repository.SaveAsync(write, Request("MY").With(r => { r.Name = "Renamed"; r.CurrentYear = "2027"; }), "ZZTWO", new DateTime(2026, 10, 30)));
            var changed = await Repository.GetAsync(write);
            Assert.Equal(("MY", "Renamed", "2027"), (changed.Code, changed.Name, changed.CurrentYear));
            Assert.Equal(1, await write.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Company", transaction: write.Transaction));
            var stamp = await write.Connection.QuerySingleAsync("SELECT RTRIM(COMLastUpdateUser) AS UserCode, COMLastUpdateDate AS Day FROM dbo.Company", transaction: write.Transaction);
            Assert.Equal(("ZZTWO", new DateTime(2026, 10, 30)), ((string)stamp.UserCode, (DateTime)stamp.Day));
        }

        await using var read = await TestDb.Sessions().OpenAsync();
        Assert.NotEqual("MY", (await Repository.GetAsync(read)).Code);
    }
}
