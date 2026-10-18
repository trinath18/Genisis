using Dapper;
using Genisis.Api.Case.Lookups;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;
using Genisis.Api.Worksheet.Lookups;
using Genisis.Api.Worksheet.SendToAccounts;
using Genisis.Api.Worksheet.SendToAr;
using Genisis.Api.Worksheet.Tracking;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Runs the Worksheet screens' repositories and services against the development databases when GENISIS_TEST_MAINDB is set.
/// Every write happens in a session that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class WorksheetDatabaseTests
{
    private static readonly string? MainDb = TestDb.MainDb;
    private static readonly DateTime SentOn = new(2026, 10, 18);

    /// <summary>A worksheet that is neither bordxed nor sent, whose case has a remarks row, an invoice and a cross-reference row.</summary>
    private static async Task<WorksheetPick> FreshWorksheet(IDbSession session)
    {
        var (cas, version) = await session.Connection.QueryFirstAsync<(string, int)>(
            """
            SELECT TOP (1) RTRIM(c.CASNumber), c.ClaimVersion FROM genisis.WS_Claim c
            WHERE ISNULL(c.ClaimBordxStatus, 0) = 0 AND ISNULL(c.CLMTokenStatus, 0) = 0
              AND EXISTS (SELECT 1 FROM dbo.CASRemarks r WHERE r.CASNumber = c.CASNumber)
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimInvoice i WHERE i.CASNumber = c.CASNumber AND i.INVWSversion = CAST(c.ClaimVersion AS nvarchar(4)))
              AND EXISTS (SELECT 1 FROM genisis.Maint_CASCrossReference x WHERE x.CASNumber = c.CASNumber)
              AND EXISTS (SELECT 1 FROM dbo.CAS s WHERE s.CASNumber = c.CASNumber)
            ORDER BY c.CASNumber DESC, c.ClaimVersion
            """, transaction: session.Transaction);
        return new WorksheetPick(cas, version);
    }

    private static Task<T?> Scalar<T>(IDbSession session, string sql, object? args = null) =>
        session.Connection.ExecuteScalarAsync<T?>(sql, args, session.Transaction);

    // ---------------- Lookups ----------------

    [Fact]
    public async Task Lookups_list_the_insured_types_and_the_payors()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var types = await new WorksheetLookupService(TestDb.Sessions(), new WorksheetLookupRepository()).GetInsuredTypesAsync();
        Assert.Contains(types, t => t.Code == "G" && !string.IsNullOrWhiteSpace(t.Name));
        Assert.NotEmpty(await new PayorLookupService(TestDb.Sessions(), new PayorRepository()).GetPayorsAsync());
    }

    // ---------------- Worksheet Tracking ----------------

    private static TrackingService Tracking() => new(TestDb.Sessions(), new TrackingRepository(), NullLogger<TrackingService>.Instance);

    [Fact]
    public async Task Tracking_searches_by_scan_range_and_each_kind_of_criterion()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var service = Tracking();

        var sample = await service.SearchAsync(new TrackingFilter { UseScan = false, Payor = "AC", ApprovedFrom = 1 });
        Assert.NotEmpty(sample.Rows);
        Assert.False(sample.Truncated);
        var one = sample.Rows[0];

        var scan = await service.SearchAsync(new TrackingFilter { WksNoFrom = one.CaseNo });
        Assert.All(scan.Rows, r => Assert.Equal(one.CaseNo, r.CaseNo));
        Assert.Contains(scan.Rows, r => r.ClaimVersion == one.ClaimVersion);
        var version = await service.SearchAsync(new TrackingFilter { WksNoFrom = one.CaseNo, VersionFrom = one.ClaimVersion, VersionTo = one.ClaimVersion });
        Assert.Equal(one.ClaimVersion, Assert.Single(version.Rows).ClaimVersion);
        var range = await service.SearchAsync(new TrackingFilter { UseScan = false, WksNoFrom = one.CaseNo, WksNoTo = one.CaseNo });
        Assert.All(range.Rows, r => Assert.Equal(one.CaseNo, r.CaseNo));
        Assert.Empty((await service.SearchAsync(new TrackingFilter { WksNoFrom = "NO SUCH WORKSHEET" })).Rows);

        Assert.All((await service.SearchAsync(new TrackingFilter { UseScan = false, Payor = "AC", ApprovedFrom = 100, ApprovedTo = 200 })).Rows,
            r => Assert.InRange(r.ApprovedAmt!.Value, 100, 200));
        Assert.All((await service.SearchAsync(new TrackingFilter { UseScan = false, Payor = "AC", BordxEmpty = true })).Rows, r => Assert.True(string.IsNullOrEmpty(r.BordxNo)));
        Assert.All((await service.SearchAsync(new TrackingFilter { UseScan = false, Payor = "AC", DodEmpty = true })).Rows, r => Assert.True(r.Dod is null || r.Dod == new DateTime(1900, 1, 1)));
        Assert.All((await service.SearchAsync(new TrackingFilter { UseScan = false, Payor = "AC", PendingEmpty = true })).Rows, r => Assert.True(string.IsNullOrEmpty(r.Pending)));
        Assert.All((await service.SearchAsync(new TrackingFilter { UseScan = false, Pending = "B" })).Rows, r => Assert.Equal("B", r.Pending));
        var closed = await service.SearchAsync(new TrackingFilter { UseScan = false, Payor = "AC", DodFrom = new(2000, 1, 1) });
        Assert.All(closed.Rows, r => Assert.True(r.Dod >= new DateTime(2000, 1, 1)));
    }

    [Fact]
    public async Task Tracking_ins_type_filter_really_filters_and_group_companies_are_distinct()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var service = Tracking();

        var all = await service.SearchAsync(new TrackingFilter { UseScan = false, Payor = "AX" });
        Assert.NotEmpty(all.Rows);
        Assert.Equal(all.Rows.Count, (await service.SearchAsync(new TrackingFilter { UseScan = false, Payor = "AX", InsFrom = "H", InsTo = "H" })).Rows.Count);
        Assert.Empty((await service.SearchAsync(new TrackingFilter { UseScan = false, Payor = "AX", InsFrom = "G", InsTo = "G" })).Rows);

        var companies = await service.GroupCompaniesAsync("AX");
        Assert.NotEmpty(companies);
        Assert.Equal(companies.Count, companies.Distinct().Count());
        Assert.All(companies, c => Assert.False(string.IsNullOrWhiteSpace(c)));
        var byCompany = await service.SearchAsync(new TrackingFilter { UseScan = false, Payor = "AX", GroupCompany = companies[0] });
        Assert.NotEmpty(byCompany.Rows);
        Assert.True(byCompany.Rows.Count <= all.Rows.Count);
    }

    [Fact]
    public async Task Tracking_update_writes_the_four_places_and_refuses_bordxed_and_missing_worksheets()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var repository = new TrackingRepository();
        await using var session = await TestDb.Sessions().BeginAsync();
        var pick = await FreshWorksheet(session);
        var args = new { cas = pick.CaseNo, ver = pick.ClaimVersion };

        Assert.Equal(StoredProcedures.Ok, await repository.UpdateAsync(session, pick, SentOn.AddHours(15), "P", "UITEST"));
        Assert.Equal("P", (await Scalar<string>(session, "SELECT RTRIM(CLMPending) FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @ver", args)));
        Assert.Equal(SentOn, await Scalar<DateTime>(session, "SELECT CLMPendingDate FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @ver", args));
        Assert.Equal("UITEST", await Scalar<string>(session, "SELECT RTRIM(CLMPendingUser) FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @ver", args));
        Assert.Equal("P", await Scalar<string>(session, "SELECT TOP (1) RTRIM(CLMInvPending) FROM genisis.WS_ClaimInvoice WHERE CASNumber = @cas AND INVWSversion = CAST(@ver AS nvarchar(4))", args));
        Assert.Equal("N", await Scalar<string>(session, "SELECT RTRIM(CASPending) FROM dbo.CAS WHERE CASNumber = @cas", args));
        Assert.Equal("N", await Scalar<string>(session, "SELECT TOP (1) RTRIM(CASPending) FROM genisis.Maint_CASCrossReference WHERE CASNumber = @cas", args));

        await session.Connection.ExecuteAsync("UPDATE genisis.WS_Claim SET ClaimBordxStatus = 1 WHERE CASNumber = @cas AND ClaimVersion = @ver", args, session.Transaction);
        Assert.Equal(StoredProcedures.Conflict, await repository.UpdateAsync(session, pick, SentOn, "C", "UITEST"));
        Assert.Equal("P", await Scalar<string>(session, "SELECT RTRIM(CLMPending) FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @ver", args));
        Assert.Equal(StoredProcedures.NotFound, await repository.UpdateAsync(session, new WorksheetPick("NO SUCH CASE", 1), SentOn, "P", "UITEST"));
    }

    [Fact]
    public async Task Tracking_update_saves_nothing_when_one_worksheet_of_the_request_is_missing()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var sessions = TestDb.Sessions();
        await using var probe = await sessions.OpenAsync();
        var pick = await FreshWorksheet(probe);
        var sql = "SELECT ISNULL(RTRIM(CLMPending), '') FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @ver";
        var args = new { cas = pick.CaseNo, ver = pick.ClaimVersion };
        var before = await Scalar<string>(probe, sql, args);

        var request = new TrackingUpdateRequest { Date = SentOn, Status = "C", Items = [pick, new WorksheetPick("NO SUCH CASE", 1)] };
        var error = await Assert.ThrowsAsync<WorksheetException>(() => Tracking().UpdateAsync(request, "UITEST"));
        Assert.Equal("Worksheet NO SUCH CASE-1 was not found. Search again.", error.Message);
        Assert.Equal(before, await Scalar<string>(probe, sql, args));
    }

    // ---------------- Send to A/C ----------------

    private static SendToAccountsService Accounts() => new(TestDb.Sessions(), new SendToAccountsRepository(), NullLogger<SendToAccountsService>.Instance);

    [Fact]
    public async Task Send_to_accounts_searches_by_scan_range_and_token_status()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var service = Accounts();

        var both = await service.SearchAsync(new SendToAccountsFilter { UseScan = false, Payor = "AC", TokenStatus = "0" });
        Assert.NotEmpty(both.Rows);
        var one = both.Rows[0];
        Assert.Equal("AC", one.Payor);
        // The view repeats a worksheet once per payment row; the screen lists each worksheet once (payor AC has repeated ones).
        Assert.Equal(both.Rows.Count, both.Rows.Select(r => (r.CaseNo, r.ClaimVersion)).Distinct().Count());

        var scan = await service.SearchAsync(new SendToAccountsFilter { WksNoFrom = one.CaseNo });
        Assert.All(scan.Rows, r => Assert.Equal(one.CaseNo, r.CaseNo));
        var range = await service.SearchAsync(new SendToAccountsFilter { UseScan = false, WksNoFrom = one.CaseNo, WksNoTo = one.CaseNo });
        Assert.Equal(scan.Rows.Count, range.Rows.Count);
        Assert.Empty((await service.SearchAsync(new SendToAccountsFilter { WksNoFrom = "NO SUCH WORKSHEET" })).Rows);

        Assert.All((await service.SearchAsync(new SendToAccountsFilter { UseScan = false, Payor = "AC", TokenStatus = "2" })).Rows, r => Assert.False(r.Sent));
        Assert.All((await service.SearchAsync(new SendToAccountsFilter { UseScan = false, Payor = "AC", TokenStatus = "1" })).Rows, r => Assert.True(r.Sent));
        Assert.All((await service.SearchAsync(new SendToAccountsFilter { UseScan = false, Payor = "AC", AmountFrom = 100, AmountTo = 200 })).Rows,
            r => Assert.InRange(r.WksAmount!.Value, 100, 200));
        Assert.All((await service.SearchAsync(new SendToAccountsFilter { UseScan = false, Payor = "AC", WksDateFrom = new(2021, 4, 1) })).Rows,
            r => Assert.True(r.WksDate >= new DateTime(2021, 4, 1)));
    }

    [Fact]
    public async Task Send_to_accounts_marks_the_worksheet_adds_the_remark_and_refuses_a_second_send()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var repository = new SendToAccountsRepository();
        await using var session = await TestDb.Sessions().BeginAsync();
        var pick = await FreshWorksheet(session);
        var args = new { cas = pick.CaseNo, ver = pick.ClaimVersion };
        await session.Connection.ExecuteAsync(
            """
            UPDATE dbo.CASRemarks SET CASORemarks = N'old remark', CASORemarksTwo = NULL WHERE CASNumber = @cas;
            UPDATE dbo.CASRemarksTwo SET CASORemarks = NULL, CASORemarksTwo = NULL WHERE CASNumber = @cas;
            UPDATE dbo.CASRemarkThree SET CASRemarks = NULL, CASRemarksFour = NULL WHERE CASNumber = @cas;
            """, args, session.Transaction);
        var line = $"~\n{DateTime.Today:dd/MM/yyyy}         FILE  -  {pick.CaseNo}-{pick.ClaimVersion} SENT TO ACCOUNT ON {SentOn:dd/MM/yyyy}{new string(' ', 24)}UITEST";

        Assert.Equal(StoredProcedures.Ok, await repository.SendAsync(session, pick, SentOn.AddHours(9), "uitest"));
        Assert.True(await Scalar<bool>(session, "SELECT CLMTokenStatus FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @ver", args));
        Assert.Equal(SentOn, await Scalar<DateTime>(session, "SELECT CLMTokenDate FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @ver", args));
        Assert.Equal("uitest", await Scalar<string>(session, "SELECT RTRIM(CLMUserToken) FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @ver", args));
        Assert.Equal(("OLD REMARK" + line).ToUpperInvariant(), await Scalar<string>(session, "SELECT CASORemarks FROM dbo.CASRemarks WHERE CASNumber = @cas", args));

        Assert.Equal(StoredProcedures.Conflict, await repository.SendAsync(session, pick, SentOn, "UITEST"));
        Assert.Equal(StoredProcedures.NotFound, await repository.SendAsync(session, new WorksheetPick("NO SUCH CASE", 1), SentOn, "UITEST"));
        Assert.Equal(("OLD REMARK" + line).ToUpperInvariant(), await Scalar<string>(session, "SELECT CASORemarks FROM dbo.CASRemarks WHERE CASNumber = @cas", args));
    }

    [Fact]
    public async Task Send_to_accounts_adds_the_remark_to_the_page_the_desktop_chose()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var repository = new SendToAccountsRepository();
        await using var session = await TestDb.Sessions().BeginAsync();
        var (cas, version) = await session.Connection.QueryFirstAsync<(string, int)>(
            """
            SELECT TOP (1) RTRIM(c.CASNumber), c.ClaimVersion FROM genisis.WS_Claim c
            WHERE ISNULL(c.ClaimBordxStatus, 0) = 0 AND ISNULL(c.CLMTokenStatus, 0) = 0
              AND EXISTS (SELECT 1 FROM dbo.CASRemarks r WHERE r.CASNumber = c.CASNumber)
              AND EXISTS (SELECT 1 FROM dbo.CASRemarkThree r3 WHERE r3.CASNumber = c.CASNumber)
            ORDER BY c.CASNumber DESC, c.ClaimVersion
            """, transaction: session.Transaction);
        var pick = new WorksheetPick(cas, version);
        var args = new { cas, ver = version };
        string Add() => $"~\n{DateTime.Today:dd/MM/yyyy}         FILE  -  {cas}-{version} SENT TO ACCOUNT ON {SentOn:dd/MM/yyyy}{new string(' ', 24)}UITEST";
        async Task Reset(string page1, string pageThree) => await session.Connection.ExecuteAsync(
            """
            UPDATE genisis.WS_Claim SET CLMTokenStatus = 0 WHERE CASNumber = @cas AND ClaimVersion = @ver;
            UPDATE dbo.CASRemarks SET CASORemarks = @p1, CASORemarksTwo = NULL WHERE CASNumber = @cas;
            UPDATE dbo.CASRemarksTwo SET CASORemarks = NULL, CASORemarksTwo = NULL WHERE CASNumber = @cas;
            UPDATE dbo.CASRemarkThree SET CASRemarks = @p3, CASRemarksFour = NULL WHERE CASNumber = @cas;
            """, new { cas, ver = version, p1 = page1, p3 = pageThree }, session.Transaction);

        // The first page holds more than 1,800 characters and the others are empty: the remark goes to the second page.
        var full = new string('x', 1900);
        await Reset(full, null!);
        Assert.Equal(StoredProcedures.Ok, await repository.SendAsync(session, pick, SentOn, "UITEST"));
        Assert.Equal(full, await Scalar<string>(session, "SELECT CASORemarks FROM dbo.CASRemarks WHERE CASNumber = @cas", args));
        Assert.Equal(Add().ToUpperInvariant(), await Scalar<string>(session, "SELECT CASORemarksTwo FROM dbo.CASRemarks WHERE CASNumber = @cas", args));

        // The fifth page holds more than 1,800 characters: the remark goes to the sixth (CASRemarksFour).
        await Reset("short", new string('y', 1900));
        Assert.Equal(StoredProcedures.Ok, await repository.SendAsync(session, pick, SentOn, "UITEST"));
        Assert.Equal(Add().ToUpperInvariant(), await Scalar<string>(session, "SELECT CASRemarksFour FROM dbo.CASRemarkThree WHERE CASNumber = @cas", args));
        Assert.Equal("short", await Scalar<string>(session, "SELECT CASORemarks FROM dbo.CASRemarks WHERE CASNumber = @cas", args));

        // The fifth page holds a little: the remark is added to it (and the page is upper-cased, as the desktop's ChkStr did).
        await Reset("short", "some text");
        Assert.Equal(StoredProcedures.Ok, await repository.SendAsync(session, pick, SentOn, "UITEST"));
        Assert.Equal(("SOME TEXT" + Add()).ToUpperInvariant(), await Scalar<string>(session, "SELECT CASRemarks FROM dbo.CASRemarkThree WHERE CASNumber = @cas", args));
    }

    // ---------------- Send to A/R ----------------

    [Fact]
    public async Task Send_to_ar_searches_and_marks_a_case_as_sent()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var repository = new SendToArRepository();
        var service = new SendToArService(TestDb.Sessions(), repository, NullLogger<SendToArService>.Instance);

        var both = await service.SearchAsync(new SendToArFilter { UseScan = false, TokenStatus = "0", WksNoFrom = "AC", WksNoTo = "AC99999999" });
        Assert.NotEmpty(both.Rows);
        var cas = both.Rows[0].CaseNo;
        Assert.Equal(cas, Assert.Single((await service.SearchAsync(new SendToArFilter { WksNoFrom = cas })).Rows).CaseNo);
        Assert.Empty((await service.SearchAsync(new SendToArFilter { WksNoFrom = "NO SUCH CASE" })).Rows);

        await using var session = await TestDb.Sessions().BeginAsync();
        Assert.Equal(StoredProcedures.Ok, await repository.SendAsync(session, cas, "uitest"));
        var sent = Assert.Single(await repository.SearchAsync(session, new SendToArFilter { WksNoFrom = cas, TokenStatus = "1" }));
        Assert.Equal(DateTime.Today, sent.DateSent);
        Assert.Equal("uitest", sent.PassedBy);
        Assert.Equal("1", sent.Status);
        Assert.Empty(await repository.SearchAsync(session, new SendToArFilter { WksNoFrom = cas, TokenStatus = "2" }));
        Assert.Single(await repository.SearchAsync(session, new SendToArFilter { WksNoFrom = cas, TokenStatus = "0" }));
        Assert.Equal(StoredProcedures.NotFound, await repository.SendAsync(session, "NO SUCH CASE", "uitest"));
    }
}
