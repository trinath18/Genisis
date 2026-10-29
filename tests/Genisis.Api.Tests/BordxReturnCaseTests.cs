using Dapper;
using Genisis.Api.ClaimBordx.ReturnCase;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Claim Bordx > Bor Delivery > Returned Fr Payor (Not App): the validators and the service, against a fake repository.</summary>
public class BordxReturnCaseRulesTests
{
    private sealed class FakeRepository(BordxReturnCaseRow? row, int setResult = StoredProcedures.Ok) : IBordxReturnCaseRepository
    {
        public List<(string Case, int Version, string Status)> Set { get; } = [];

        public Task<List<int>> VersionsAsync(IDbSession session, string caseNo) => Task.FromResult(new List<int> { 1, 2 });

        public Task<BordxReturnCaseRow?> FindAsync(IDbSession session, string caseNo, int version) => Task.FromResult(row);

        public Task<int> SetAsync(IDbSession session, string caseNo, int version, string status)
        {
            Set.Add((caseNo, version, status));
            return Task.FromResult(setResult);
        }
    }

    private static BordxReturnCaseService Service(FakeSessions sessions, FakeRepository repository) => new(sessions, repository, NullLogger<BordxReturnCaseService>.Instance);

    [Fact]
    public void A_case_number_is_needed_to_look_up_and_the_status_has_to_be_n_or_y_to_update()
    {
        Assert.Equal("Please Enter Claim No", new BordxReturnCaseFilterValidator().FirstError(new BordxReturnCaseFilter { CaseNo = " " }));
        Assert.Null(new BordxReturnCaseFilterValidator().FirstError(new BordxReturnCaseFilter { CaseNo = "AB1" }));
        var validator = new BordxReturnCaseRequestValidator();
        Assert.Equal("Please Enter Claim No", validator.FirstError(new BordxReturnCaseRequest { Status = "Y" }));
        Assert.Equal("Please Enter Approved Status", validator.FirstError(new BordxReturnCaseRequest { CaseNo = "AB1" }));
        Assert.Equal("Approved Status must be N - No or Y - Yes.", validator.FirstError(new BordxReturnCaseRequest { CaseNo = "AB1", Status = "A" }));
        Assert.Null(validator.FirstError(new BordxReturnCaseRequest { CaseNo = "AB1", Status = " y " }));
    }

    [Fact]
    public async Task A_worksheet_whose_bordx_has_not_been_sent_to_the_payor_is_not_found_with_the_desktop_message()
    {
        var failure = await Assert.ThrowsAsync<NotFoundException>(() => Service(new FakeSessions(), new FakeRepository(null)).FindAsync(new BordxReturnCaseFilter { CaseNo = "AB1" }));
        Assert.Equal("Bordx not send to Payor!", failure.Message);
    }

    [Fact]
    public async Task The_worksheet_shows_as_case_dash_version()
    {
        var row = await Service(new FakeSessions(), new FakeRepository(new BordxReturnCaseRow { CaseNo = "AB1 ", ClaimVersion = 2 })).FindAsync(new BordxReturnCaseFilter { CaseNo = "AB1", Version = 2 });
        Assert.Equal("AB1-2", row.ClaimNo);
    }

    [Fact]
    public async Task Setting_the_status_trims_and_upper_cases_it_and_commits()
    {
        var sessions = new FakeSessions();
        var repository = new FakeRepository(null);
        await Service(sessions, repository).SetAsync(new BordxReturnCaseRequest { CaseNo = " AB1 ", Version = 2, Status = " y " }, "USER1");
        Assert.Equal([(" AB1 ", 2, " y ")], repository.Set);
        Assert.True(sessions.Last.Committed);
    }

    [Fact]
    public async Task Setting_the_status_of_a_worksheet_that_was_not_sent_to_the_payor_saves_nothing()
    {
        var sessions = new FakeSessions();
        var failure = await Assert.ThrowsAsync<NotFoundException>(() => Service(sessions, new FakeRepository(null, StoredProcedures.NotFound)).SetAsync(new BordxReturnCaseRequest { CaseNo = "AB1", Status = "N" }, "USER1"));
        Assert.Equal("Bordx not send to Payor!", failure.Message);
        Assert.False(sessions.Last.Committed);
    }
}

/// <summary>Returned Fr Payor (Not App) against the database. Every write is made in a session that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class BordxReturnCaseDatabaseTests
{
    private static BordxReturnCaseRepository Repository => new();

    private const string Checksum = "SELECT CHECKSUM_AGG(CHECKSUM(CASNumber, ClaimVersion, CLMAppStatus, BordxRefNo)) FROM genisis.WS_Claim";

    [Fact]
    public async Task The_versions_are_every_claim_version_of_the_case_and_an_unsent_bordx_is_not_found()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var (cas, version) = await session.Connection.QueryFirstAsync<(string, int)>("SELECT TOP (1) RTRIM(c.CASNumber), c.ClaimVersion FROM genisis.WS_Claim c WHERE c.BordxRefNo <> '' AND EXISTS (SELECT 1 FROM genisis.WS_Claim d WHERE d.CASNumber = c.CASNumber AND d.ClaimVersion <> c.ClaimVersion) ORDER BY c.CASNumber DESC", transaction: session.Transaction);

        var versions = await Repository.VersionsAsync(session, cas);
        Assert.Equal(await session.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.WS_Claim WHERE CASNumber = @cas", new { cas }, session.Transaction), versions.Count);
        Assert.Equal(versions.Order(), versions);
        Assert.Empty(await Repository.VersionsAsync(session, "NO SUCH CASE"));
        Assert.Null(await Repository.FindAsync(session, cas, version));
        Assert.Equal(StoredProcedures.NotFound, await Repository.SetAsync(session, cas, version, "Y"));
    }

    [Fact]
    public async Task Once_the_bordx_is_sent_to_the_payor_the_worksheet_is_found_with_its_bordx_and_its_status_can_be_set_and_nothing_survives_the_rollback()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var before = await read.Connection.ExecuteScalarAsync<int>(Checksum, transaction: read.Transaction, commandTimeout: 300);

        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var (cas, version, bordx) = await write.Connection.QueryFirstAsync<(string, int, string)>("SELECT TOP (1) RTRIM(c.CASNumber), c.ClaimVersion, RTRIM(c.BordxRefNo) FROM genisis.WS_Claim c JOIN genisis.WS_Bordx b ON b.BordxRefNo = c.BordxRefNo ORDER BY c.CASNumber DESC", transaction: write.Transaction);
            await write.Connection.ExecuteAsync("UPDATE genisis.WS_Bordx SET BordxPAYToStatus = '1' WHERE BordxRefNo = @bordx", new { bordx }, write.Transaction);

            var row = await Repository.FindAsync(write, cas, version);
            Assert.NotNull(row);
            Assert.Equal((cas, version, bordx), (row.CaseNo, row.ClaimVersion, row.BordxNo));
            var expected = await write.Connection.QueryFirstAsync("SELECT RTRIM(b.BordxPAYCode) AS Payor, b.BordxDate, b.BordxClaimAmt, RTRIM(b.BordxStatus) AS BordxStatus, c.CLMTotalApproved, RTRIM(c.CLMAppStatus) AS AppStatus FROM genisis.WS_Claim c JOIN genisis.WS_Bordx b ON b.BordxRefNo = c.BordxRefNo WHERE c.CASNumber = @cas AND c.ClaimVersion = @version", new { cas, version }, write.Transaction);
            Assert.Equal(((string?)expected.Payor, (DateTime?)expected.BordxDate, (decimal?)expected.BordxClaimAmt, (string?)expected.BordxStatus, (decimal?)expected.CLMTotalApproved, (string?)expected.AppStatus),
                (row.Payor, row.BordxDate, row.BordxAmount, row.BordxStatus, row.ApprovedAmount, row.AppStatus));

            Assert.Equal(StoredProcedures.Ok, await Repository.SetAsync(write, cas, version, "y"));
            Assert.Equal("Y", (await Repository.FindAsync(write, cas, version))!.AppStatus);
            Assert.Equal(StoredProcedures.Ok, await Repository.SetAsync(write, cas, version, "N"));
            Assert.Equal("N", (await Repository.FindAsync(write, cas, version))!.AppStatus);
            Assert.Equal(StoredProcedures.NotFound, await Repository.SetAsync(write, "NO SUCH CASE", 1, "Y"));
        }

        Assert.Equal(before, await read.Connection.ExecuteScalarAsync<int>(Checksum, transaction: read.Transaction, commandTimeout: 300));
    }
}
