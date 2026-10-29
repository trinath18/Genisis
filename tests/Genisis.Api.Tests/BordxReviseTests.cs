using Dapper;
using Genisis.Api.ClaimBordx.Revise;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Claim Bordx > Worksheet > Remove Worksheet: the validators and the service, against a fake repository.</summary>
public class BordxReviseRulesTests
{
    private sealed class FakeRepository(Dictionary<string, int>? results = null) : IBordxReviseRepository
    {
        public List<(string Bordx, string Wks, DateTime Today, string User)> Removed { get; } = [];

        public Task<List<BordxReviseRow>> SearchAsync(IDbSession session, string bordxNo) =>
            Task.FromResult(bordxNo == "EMPTY" ? [] : new List<BordxReviseRow>
            {
                new() { CaseNo = "AB1 ", ClaimVersion = 1, BordxDate = new DateTime(2021, 8, 6), BordxPrintedDate = new DateTime(2021, 8, 7), SentOut = true, TotalApproved = 100.5m },
                new() { CaseNo = "AB2", ClaimVersion = 2, BordxDate = new DateTime(2021, 8, 6), SentOut = true, TotalApproved = null },
                new() { CaseNo = "AB3", ClaimVersion = 1, BordxDate = new DateTime(2021, 8, 6), SentOut = true, TotalApproved = 20m },
            });

        public Task<int> RemoveAsync(IDbSession session, string bordxNo, WorksheetPick pick, string userCode, DateTime today)
        {
            Removed.Add((bordxNo, pick.WksNo, today, userCode));
            return Task.FromResult(results?.GetValueOrDefault(pick.WksNo) ?? StoredProcedures.Ok);
        }
    }

    private static BordxReviseService Service(FakeSessions sessions, FakeRepository repository) => new(sessions, repository, NullLogger<BordxReviseService>.Instance);

    [Fact]
    public void A_bordx_number_is_needed_to_look_up_and_to_remove_and_something_has_to_be_ticked()
    {
        Assert.Equal("Please select a Bordx No", new BordxReviseFilterValidator().FirstError(new BordxReviseFilter { BordxNo = " " }));
        Assert.Null(new BordxReviseFilterValidator().FirstError(new BordxReviseFilter { BordxNo = "NUB0000371" }));
        var validator = new BordxReviseRequestValidator();
        Assert.Equal("Please select a Bordx No", validator.FirstError(new BordxReviseRequest { Items = [new("AB1", 1)] }));
        Assert.Equal("Please select at least one record to be removed!", validator.FirstError(new BordxReviseRequest { BordxNo = "N1" }));
        Assert.Null(validator.FirstError(new BordxReviseRequest { BordxNo = "N1", Items = [new("AB1", 1)] }));
    }

    [Fact]
    public async Task The_result_carries_the_dates_the_count_and_the_approved_total_of_the_listed_worksheets()
    {
        var result = await Service(new FakeSessions(), new FakeRepository()).SearchAsync(new BordxReviseFilter { BordxNo = " N1 " });
        Assert.Equal(("N1", new DateTime(2021, 8, 6), new DateTime(2021, 8, 7), 3, 120.5m, true), (result.BordxNo, result.BordxDate, result.PrintedDate, result.Items, result.TotalApproved, result.SentOut));
        Assert.Equal("AB1 - 1", result.Rows[0].ClaimNo);
    }

    [Fact]
    public async Task A_bordx_without_worksheets_gives_an_empty_result_the_page_reports_as_no_record_found()
    {
        var result = await Service(new FakeSessions(), new FakeRepository()).SearchAsync(new BordxReviseFilter { BordxNo = "EMPTY" });
        Assert.Equal((0, 0m, false), (result.Items, result.TotalApproved, result.SentOut));
        Assert.Empty(result.Rows);
    }

    [Fact]
    public async Task The_ticked_worksheets_are_removed_once_each_in_one_transaction_that_is_committed()
    {
        var sessions = new FakeSessions();
        var repository = new FakeRepository();
        var removed = await Service(sessions, repository).RemoveAsync(new BordxReviseRequest { BordxNo = " N1 ", Items = [new("AB1", 1), new("AB1 ", 1), new("AB2", 2)] }, "USER1");
        Assert.Equal(2, removed);
        Assert.Equal(["AB1-1", "AB2-2"], repository.Removed.Select(r => r.Wks));
        Assert.All(repository.Removed, r => Assert.Equal(("N1", DateTime.Today, "USER1"), (r.Bordx, r.Today, r.User)));
        Assert.True(sessions.Last.Committed);
    }

    [Theory]
    [InlineData(StoredProcedures.NotFound, "Worksheet AB2-2 or bordx N1 was not found. Search again.")]
    [InlineData(StoredProcedures.Conflict, "Worksheet AB2-2 is not in bordx N1. Search again.")]
    [InlineData(BordxReviseCodes.SentOut, "This Bordx has been sent out.")]
    public async Task A_worksheet_that_cannot_be_removed_stops_the_whole_removal_and_nothing_is_saved(int result, string message)
    {
        var sessions = new FakeSessions();
        var failure = await Assert.ThrowsAsync<BusinessRuleException>(() => Service(sessions, new FakeRepository(new() { ["AB2-2"] = result }))
            .RemoveAsync(new BordxReviseRequest { BordxNo = "N1", Items = [new("AB1", 1), new("AB2", 2), new("AB3", 1)] }, "USER1"));
        Assert.Equal(message, failure.Message);
        Assert.False(sessions.Last.Committed);
        Assert.True(sessions.Last.RolledBack);
    }
}

/// <summary>Claim Bordx > Worksheet > Remove Worksheet against the database. Every write is made in a session that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class BordxReviseDatabaseTests
{
    private static BordxReviseRepository Repository => new();

    private const string Checksum = """
        SELECT (SELECT CHECKSUM_AGG(CHECKSUM(BordxRefNo, BordxNoClaims, BordxClaimAmt)) FROM genisis.WS_Bordx)
             + (SELECT CHECKSUM_AGG(CHECKSUM(CASNumber, ClaimVersion, BordxRefNo, ClaimBordxStatus, CLMBordxWSStatus, CLMBordxWSASCII)) FROM genisis.WS_Claim)
             + (SELECT CHECKSUM_AGG(CHECKSUM(CASNumber, INVWSversion, INVBordxRefNo, INVBordxed)) FROM genisis.WS_ClaimInvoice)
             + (SELECT COUNT(*) FROM genisis.WS_BordxWSArchive)
        """;

    private static async Task<(string Bordx, int Claims)> AnUnsentBordxAsync(IDbSession session) =>
        await session.Connection.QueryFirstAsync<(string, int)>("""
            SELECT TOP (1) RTRIM(b.BordxRefNo), b.BordxNoClaims FROM genisis.WS_Bordx b
            WHERE b.BordxNoClaims >= 3 AND b.BordxPAYToStatus = '0' AND b.BordxMNRBToStatus = '0'
              AND (SELECT COUNT(*) FROM genisis.WS_Claim c WHERE c.BordxRefNo = b.BordxRefNo) = b.BordxNoClaims
            ORDER BY b.BordxIndex DESC
            """, transaction: session.Transaction);

    [Fact]
    public async Task The_search_lists_every_worksheet_of_a_bordx_with_its_member_patient_and_dates()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var (bordx, claims) = await AnUnsentBordxAsync(session);

        var rows = await Repository.SearchAsync(session, bordx);
        Assert.Equal(claims, rows.Count);
        Assert.Equal(rows.OrderBy(r => r.CaseNo, StringComparer.Ordinal).ThenBy(r => r.ClaimVersion).Select(r => r.ClaimNo), rows.Select(r => r.ClaimNo));
        Assert.All(rows, r => Assert.False(string.IsNullOrWhiteSpace(r.MbmNumber)));
        Assert.All(rows, r => Assert.False(string.IsNullOrWhiteSpace(r.PatientName)));
        Assert.All(rows, r => Assert.False(r.SentOut));
        var expected = await session.Connection.ExecuteScalarAsync<decimal>("SELECT SUM(CLMTotalApproved) FROM genisis.WS_Claim WHERE BordxRefNo = @bordx", new { bordx }, session.Transaction);
        Assert.Equal(expected, rows.Sum(r => r.TotalApproved ?? 0m));
        Assert.Empty(await Repository.SearchAsync(session, "NO SUCH BORDX"));
    }

    [Fact]
    public async Task Removing_a_worksheet_frees_it_and_its_invoices_takes_it_off_the_bordx_count_and_amount_and_archives_it_and_nothing_survives_the_rollback()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var (bordx, claims) = await AnUnsentBordxAsync(read);
        var before = await read.Connection.ExecuteScalarAsync<int>(Checksum, transaction: read.Transaction, commandTimeout: 300);

        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var rows = await Repository.SearchAsync(write, bordx);
            var pick = new WorksheetPick(rows[0].CaseNo.Trim(), rows[0].ClaimVersion);
            var other = new WorksheetPick(rows[1].CaseNo.Trim(), rows[1].ClaimVersion);
            var bordxBefore = await write.Connection.QueryFirstAsync("SELECT BordxNoClaims, BordxClaimAmt, BordxDate FROM genisis.WS_Bordx WHERE BordxRefNo = @bordx", new { bordx }, write.Transaction);
            await write.Connection.ExecuteAsync("UPDATE genisis.WS_Claim SET CLMBordxWSASCII = 'Y', CLMBordxWSStatus = 'A' WHERE CASNumber = @cas AND ClaimVersion = @v", new { cas = pick.CaseNo, v = pick.ClaimVersion }, write.Transaction);
            await write.Connection.ExecuteAsync("UPDATE genisis.WS_ClaimInvoice SET INVBordxRefNo = @bordx, INVBordxed = 1 WHERE CASNumber = @cas AND INVWSversion = @v", new { bordx, cas = pick.CaseNo, v = pick.ClaimVersion.ToString() }, write.Transaction);

            Assert.Equal(StoredProcedures.Ok, await Repository.RemoveAsync(write, bordx, pick, "ZZTEST", new DateTime(2026, 10, 29, 14, 0, 0)));

            var claim = await write.Connection.QueryFirstAsync("SELECT RTRIM(BordxRefNo) AS BordxRefNo, ClaimBordxStatus, RTRIM(CLMBordxWSASCII) AS Ascii, RTRIM(CLMBordxWSStatus) AS WsStatus FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @v", new { cas = pick.CaseNo, v = pick.ClaimVersion }, write.Transaction);
            Assert.Equal(("", false, "N", "D"), ((string)claim.BordxRefNo, (bool)claim.ClaimBordxStatus, (string)claim.Ascii, (string)claim.WsStatus));
            var invoices = (await write.Connection.QueryAsync("SELECT RTRIM(INVBordxRefNo) AS Ref, INVBordxed AS Bordxed FROM genisis.WS_ClaimInvoice WHERE CASNumber = @cas AND INVWSversion = @v", new { cas = pick.CaseNo, v = pick.ClaimVersion.ToString() }, write.Transaction)).ToList();
            Assert.NotEmpty(invoices);
            Assert.All(invoices, i => Assert.Equal(("", false), ((string)i.Ref, (bool)i.Bordxed)));
            var bordxAfter = await write.Connection.QueryFirstAsync("SELECT BordxNoClaims, BordxClaimAmt FROM genisis.WS_Bordx WHERE BordxRefNo = @bordx", new { bordx }, write.Transaction);
            Assert.Equal((claims - 1, (decimal)bordxBefore.BordxClaimAmt - (rows[0].TotalApproved ?? 0m)), ((int)bordxAfter.BordxNoClaims, (decimal)bordxAfter.BordxClaimAmt));
            var archive = await write.Connection.QueryFirstAsync("SELECT RTRIM(BordxRefNo) AS BordxRefNo, BordxDate, RTRIM(LastUpdateUser) AS UserCode, LastUpdateDate FROM genisis.WS_BordxWSArchive WHERE RTRIM(CASNumber) = @cas AND Claimversion = @v AND LastUpdateUser = 'ZZTEST'", new { cas = pick.CaseNo, v = pick.ClaimVersion.ToString() }, write.Transaction);
            Assert.Equal((bordx, (DateTime)bordxBefore.BordxDate, "ZZTEST", new DateTime(2026, 10, 29)), ((string)archive.BordxRefNo, (DateTime)archive.BordxDate, (string)archive.UserCode, (DateTime)archive.LastUpdateDate));

            Assert.Equal(claims - 1, (await Repository.SearchAsync(write, bordx)).Count);
            Assert.Equal(StoredProcedures.Conflict, await Repository.RemoveAsync(write, bordx, pick, "ZZTEST", DateTime.Today));
            Assert.Equal(StoredProcedures.Ok, await Repository.RemoveAsync(write, bordx, other, "ZZTEST", DateTime.Today));
            Assert.Equal(StoredProcedures.NotFound, await Repository.RemoveAsync(write, bordx, new WorksheetPick("NO SUCH CASE", 1), "ZZTEST", DateTime.Today));
            Assert.Equal(StoredProcedures.NotFound, await Repository.RemoveAsync(write, "NO SUCH BORDX", other, "ZZTEST", DateTime.Today));
        }

        Assert.Equal(before, await read.Connection.ExecuteScalarAsync<int>(Checksum, transaction: read.Transaction, commandTimeout: 300));
    }

    [Fact]
    public async Task A_bordx_that_has_been_sent_to_the_payor_or_to_mnrb_refuses_the_removal_and_changes_nothing()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var write = await TestDb.Sessions().BeginAsync();
        var (bordx, _) = await AnUnsentBordxAsync(write);
        var rows = await Repository.SearchAsync(write, bordx);
        var pick = new WorksheetPick(rows[0].CaseNo.Trim(), rows[0].ClaimVersion);
        foreach (var column in new[] { "BordxPAYToStatus", "BordxMNRBToStatus" })
        {
            await write.Connection.ExecuteAsync($"UPDATE genisis.WS_Bordx SET {column} = '1' WHERE BordxRefNo = @bordx", new { bordx }, write.Transaction);
            Assert.True((await Repository.SearchAsync(write, bordx)).All(r => r.SentOut));
            Assert.Equal(BordxReviseCodes.SentOut, await Repository.RemoveAsync(write, bordx, pick, "ZZTEST", DateTime.Today));
            Assert.Equal(rows.Count, await write.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.WS_Claim WHERE BordxRefNo = @bordx", new { bordx }, write.Transaction));
            await write.Connection.ExecuteAsync($"UPDATE genisis.WS_Bordx SET {column} = '0' WHERE BordxRefNo = @bordx", new { bordx }, write.Transaction);
        }
    }

    [Fact]
    public async Task A_worksheet_of_another_bordx_is_not_removed_from_this_one()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var write = await TestDb.Sessions().BeginAsync();
        var (bordx, _) = await AnUnsentBordxAsync(write);
        var elsewhere = await write.Connection.QueryFirstAsync<(string Cas, int Version)>("SELECT TOP (1) RTRIM(CASNumber), ClaimVersion FROM genisis.WS_Claim WHERE BordxRefNo <> @bordx AND BordxRefNo <> '' ORDER BY CASNumber", new { bordx }, write.Transaction);
        Assert.Equal(StoredProcedures.Conflict, await Repository.RemoveAsync(write, bordx, new WorksheetPick(elsewhere.Cas, elsewhere.Version), "ZZTEST", DateTime.Today));
    }
}
