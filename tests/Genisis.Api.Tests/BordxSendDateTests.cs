using Dapper;
using Genisis.Api.ClaimBordx.SendDate;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Claim Bordx > Bor Delivery > Bordx Send Date (legacy frmUploadApproval): the file layouts, the checks and the service, against a fake repository.</summary>
public class BordxSendDateRulesTests
{
    private sealed class FakeClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class FakeRepository(params string[] known) : IBordxSendDateRepository
    {
        public List<string> Calls { get; } = [];

        public Task<HashSet<string>> KnownAsync(IDbSession session, IEnumerable<string> bordxNos) =>
            Task.FromResult(new HashSet<string>(bordxNos.Where(known.Contains), StringComparer.OrdinalIgnoreCase));

        public Task<int> SendAsync(IDbSession session, string bordxNo, DateTime date, string userCode)
        {
            Calls.Add($"send {bordxNo} {date:yyyy-MM-dd} {userCode}");
            return Task.FromResult(StoredProcedures.Ok);
        }

        public Task<int> ApproveAsync(IDbSession session, string bordxNo, string appCode, DateTime appDate)
        {
            Calls.Add($"approve {bordxNo} {appCode} {appDate:yyyy-MM-dd}");
            return Task.FromResult(StoredProcedures.Ok);
        }

        public Task<int> ReturnAsync(IDbSession session, string bordxNo, DateTime date, string userCode, DateTime today)
        {
            Calls.Add($"return {bordxNo} {date:yyyy-MM-dd} {userCode} {today:yyyy-MM-dd}");
            return Task.FromResult(StoredProcedures.Ok);
        }
    }

    private static BordxSendDateService Service(FakeSessions sessions, FakeRepository repository) =>
        new(sessions, repository, new FakeClock(new DateTime(2026, 11, 14, 9, 0, 0)), NullLogger<BordxSendDateService>.Instance);

    /// <summary>A sheet with a header row and the given cells (column number → value) on each data row.</summary>
    private static List<object?[]> Sheet(params Dictionary<int, object?>[] rows)
    {
        var sheet = new List<object?[]> { new object?[20] };
        foreach (var row in rows)
        {
            var cells = new object?[20];
            foreach (var (column, value) in row) cells[column - 1] = value;
            sheet.Add(cells);
        }
        return sheet;
    }

    private static Dictionary<int, object?> SendRow(string bordx, object? amount, object? date) => new() { [1] = 1d, [2] = bordx, [3] = amount, [20] = date };

    private static Dictionary<int, object?> ApprovalRow(string bordx, string code, object? date, object? amount) => new() { [1] = bordx, [8] = code, [9] = date, [10] = amount };

    [Fact]
    public void Each_upload_reads_the_columns_of_its_desktop_loop()
    {
        Assert.Equal((2, 3, 0, 0, 20), BordxSendDateLayout.Columns(BordxSendDateKinds.Send));
        Assert.Equal((1, 10, 8, 9, 0), BordxSendDateLayout.Columns(BordxSendDateKinds.Approval));
        Assert.Equal((2, 3, 18, 19, 20), BordxSendDateLayout.Columns(BordxSendDateKinds.Return));
    }

    [Fact]
    public void The_rows_start_at_row_two_and_stop_at_the_first_empty_column_a()
    {
        var sheet = Sheet(SendRow("NAC0001926", 100d, new DateTime(2026, 11, 2)), SendRow("NAC0001927", "5", "03/11/2026"), new Dictionary<int, object?> { [2] = "NAC0001928" }, SendRow("NAC0001929", 1d, 46000d));
        var lines = BordxSendDateReader.Read(BordxSendDateKinds.Send, sheet);
        Assert.Equal([2, 3], lines.Select(l => l.Line));
        Assert.Equal(("NAC0001926", "100", "02/11/2026"), (lines[0].BordxNo, lines[0].Amount, lines[0].Date));
        Assert.All(lines, l => Assert.Empty(l.Problems));
        Assert.Equal(new DateTime(2026, 11, 3), lines[1].ParsedDate);
    }

    [Fact]
    public void The_checks_are_the_desktops_verify_checks()
    {
        var bad = BordxSendDateReader.Read(BordxSendDateKinds.Send, Sheet(SendRow("XAC000192", "abc", "not a date")))[0];
        Assert.Equal(["Bordx No must have 8 or 10 characters.", "Bordx No must start with N or S.", "Bordx Amount is not a number.", "Send Date is not a date."], bad.Problems);
        Assert.Empty(BordxSendDateReader.Read(BordxSendDateKinds.Send, Sheet(SendRow("SAC00019", 1d, new DateTime(2026, 1, 1))))[0].Problems);

        var approval = BordxSendDateReader.Read(BordxSendDateKinds.Approval, Sheet(ApprovalRow("NAC0001926", "12345678901", "x", 10d)))[0];
        Assert.Equal(["Approval Code must have 12 characters.", "Approval Date is not a date."], approval.Problems);
        Assert.Empty(BordxSendDateReader.Read(BordxSendDateKinds.Approval, Sheet(ApprovalRow("NAC0001926", "123456789012", new DateTime(2026, 1, 1), 10d)))[0].Problems);

        var returned = BordxSendDateReader.Read(BordxSendDateKinds.Return, Sheet(new Dictionary<int, object?> { [1] = 1d, [2] = "NAC0001926", [3] = 1d, [20] = "" }))[0];
        Assert.Equal(["Return Date is not a date."], returned.Problems);
    }

    [Fact]
    public async Task Verify_lists_every_row_and_a_bordx_that_does_not_exist_is_an_error()
    {
        var sessions = new FakeSessions();
        var repository = new FakeRepository("NAC0001926");
        var result = await Service(sessions, repository).VerifyAsync(BordxSendDateKinds.Send, Sheet(SendRow("NAC0001926", 1d, new DateTime(2026, 1, 1)), SendRow("NAC0009999", 1d, new DateTime(2026, 1, 1))));
        Assert.Equal((2, 1, 0, BordxSendDateMessages.Errors), (result.Rows.Count, result.Errors, result.Updated, result.Message));
        Assert.Equal(["Bordx No was not found."], result.Rows[1].Problems);
        Assert.Empty(repository.Calls);
        Assert.False(sessions.Last.Committed);
    }

    [Fact]
    public async Task An_upload_with_an_error_or_no_rows_writes_nothing()
    {
        var sessions = new FakeSessions();
        var repository = new FakeRepository("NAC0001926");
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Service(sessions, repository).UploadAsync(BordxSendDateKinds.Send, Sheet(SendRow("NAC0001926", "x", new DateTime(2026, 1, 1))), "ZZTEST"));
        Assert.Equal(BordxSendDateMessages.Errors, error.Message);
        var empty = await Assert.ThrowsAsync<BusinessRuleException>(() => Service(sessions, repository).UploadAsync(BordxSendDateKinds.Send, Sheet(), "ZZTEST"));
        Assert.Equal(BordxSendDateMessages.NoRows, empty.Message);
        Assert.Empty(repository.Calls);
        Assert.All(sessions.Sessions, s => Assert.False(s.Committed));
    }

    [Fact]
    public async Task Each_upload_writes_every_row_in_one_committed_transaction()
    {
        var repository = new FakeRepository("NAC0001926", "NAC0001927");
        var sessions = new FakeSessions();
        var sent = await Service(sessions, repository).UploadAsync(BordxSendDateKinds.Send, Sheet(SendRow("NAC0001926", 1d, new DateTime(2026, 11, 2)), SendRow("NAC0001927", 1d, "03/11/2026")), "ZZTEST");
        Assert.Equal((2, BordxSendDateMessages.Uploaded), (sent.Updated, sent.Message));
        Assert.True(Assert.Single(sessions.Sessions).Committed);

        await Service(new FakeSessions(), repository).UploadAsync(BordxSendDateKinds.Approval, Sheet(ApprovalRow("NAC0001926", "123456789012", new DateTime(2026, 11, 4), 5d)), "ZZTEST");
        await Service(new FakeSessions(), repository).UploadAsync(BordxSendDateKinds.Return, Sheet(new Dictionary<int, object?> { [1] = 1d, [2] = "NAC0001927", [3] = 1d, [20] = new DateTime(2026, 11, 5) }), "ZZTEST");
        Assert.Equal(["send NAC0001926 2026-11-02 ZZTEST", "send NAC0001927 2026-11-03 ZZTEST", "approve NAC0001926 123456789012 2026-11-04", "return NAC0001927 2026-11-05 ZZTEST 2026-11-14"], repository.Calls);
    }
}

/// <summary>Bordx Send Date against the database: every write runs in a transaction that is rolled back.</summary>
public class BordxSendDateDatabaseTests
{
    private static BordxSendDateRepository Repository => new();

    private static async Task<(string Bordx, List<string> Cases)> ABordxWithCasesAsync(IDbSession session)
    {
        var bordx = await session.Connection.QueryFirstAsync<string>(
            "SELECT TOP (1) RTRIM(b.BordxRefNo) FROM genisis.WS_Bordx b WHERE b.BordxRefNo LIKE 'N%' AND EXISTS (SELECT 1 FROM genisis.WS_Claim c JOIN dbo.CASRemarks r ON r.CASNumber = c.CASNumber WHERE c.BordxRefNo = b.BordxRefNo) ORDER BY b.BordxRefNo DESC",
            transaction: session.Transaction, commandTimeout: 120);
        var cases = (await session.Connection.QueryAsync<string>("SELECT DISTINCT RTRIM(CASNumber) FROM genisis.WS_Claim WHERE BordxRefNo = @bordx AND NULLIF(CASNumber, N'') IS NOT NULL", new { bordx }, session.Transaction)).ToList();
        return (bordx, cases);
    }

    [Fact]
    public async Task Known_gives_only_the_numbers_that_exist()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var (bordx, _) = await ABordxWithCasesAsync(session);
        var known = await Repository.KnownAsync(session, [bordx, "NZZ9999999", "bad'number"]);
        Assert.Equal([bordx], known);
    }

    [Fact]
    public async Task A_send_date_sets_the_payor_and_accounts_dates_and_the_web_flag_of_the_cases_and_leaves_mnrb_alone()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var write = await TestDb.Sessions().BeginAsync();
        var (bordx, cases) = await ABordxWithCasesAsync(write);
        var mnrbBefore = await write.Connection.QueryFirstAsync<DateTime?>("SELECT BordxMNRBSentDate FROM genisis.WS_Bordx WHERE BordxRefNo = @bordx", new { bordx }, write.Transaction);
        await write.Connection.ExecuteAsync("UPDATE dbo.CASRemarks SET CASWebStatus = 'N' WHERE CASNumber IN @cases", new { cases }, write.Transaction);

        var date = new DateTime(2026, 11, 2);
        Assert.Equal(StoredProcedures.Ok, await Repository.SendAsync(write, bordx, date, "ZZTEST"));
        var row = await write.Connection.QueryFirstAsync("SELECT BordxPAYSentDate, BordxPAYToStatus, BordxPAYSentBy, BordxSendACDate, BordxSendtoACStatus, BordxSendACBy, BordxMNRBSentDate FROM genisis.WS_Bordx WHERE BordxRefNo = @bordx", new { bordx }, write.Transaction);
        Assert.Equal((date, "1", "ZZTEST", date, true, "ZZTEST", mnrbBefore),
            ((DateTime)row.BordxPAYSentDate, ((string)row.BordxPAYToStatus).Trim(), ((string)row.BordxPAYSentBy).Trim(), (DateTime)row.BordxSendACDate, (bool)row.BordxSendtoACStatus, ((string)row.BordxSendACBy).Trim(), (DateTime?)row.BordxMNRBSentDate));
        Assert.Equal(0, await write.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.CASRemarks WHERE CASNumber IN @cases AND ISNULL(CASWebStatus, '') <> 'Y'", new { cases }, write.Transaction));
        Assert.Equal(StoredProcedures.NotFound, await Repository.SendAsync(write, "NZZ9999999", date, "ZZTEST"));
    }

    [Fact]
    public async Task An_approval_sets_the_payor_code_and_date_on_every_claim_of_the_bordx()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var write = await TestDb.Sessions().BeginAsync();
        var (bordx, _) = await ABordxWithCasesAsync(write);
        var date = new DateTime(2026, 11, 4);
        Assert.Equal(StoredProcedures.Ok, await Repository.ApproveAsync(write, bordx, "ZZ0000000001", date));
        var claims = (await write.Connection.QueryAsync("SELECT CLMPayorAppCode, CLMPayorAppDate, CLMPayorAppStatus FROM genisis.WS_Claim WHERE BordxRefNo = @bordx", new { bordx }, write.Transaction)).ToList();
        Assert.NotEmpty(claims);
        Assert.All(claims, c => Assert.Equal(("ZZ0000000001", date, "Y"), (((string)c.CLMPayorAppCode).Trim(), (DateTime)c.CLMPayorAppDate, ((string)c.CLMPayorAppStatus).Trim())));
    }

    [Fact]
    public async Task A_return_date_sets_the_claims_and_writes_the_remark_on_every_case_of_the_bordx()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var write = await TestDb.Sessions().BeginAsync();
        var (bordx, cases) = await ABordxWithCasesAsync(write);
        var date = new DateTime(2026, 11, 5);
        Assert.Equal(StoredProcedures.Ok, await Repository.ReturnAsync(write, bordx, date, "ZZTEST", new DateTime(2026, 11, 14)));
        var claims = (await write.Connection.QueryAsync("SELECT BordxPAYRecDocDate, CLMAppStatus FROM genisis.WS_Claim WHERE BordxRefNo = @bordx", new { bordx }, write.Transaction)).ToList();
        Assert.All(claims, c => Assert.Equal((date, "A"), ((DateTime)c.BordxPAYRecDocDate, ((string)c.CLMAppStatus).Trim())));
        var line = $"14/11/2026   Received Returned Bordx  -  {bordx} on 05/11/2026";
        foreach (var cas in cases)
        {
            var remarks = await write.Connection.ExecuteScalarAsync<string>(
                "SELECT ISNULL(r.CASORemarks, N'') + ISNULL(r.CASORemarksTwo, N'') + ISNULL(t.CASORemarks, N'') + ISNULL(t.CASORemarksTwo, N'') + ISNULL(h.CASRemarks, N'') + ISNULL(h.CASRemarksFive, N'') FROM dbo.CASRemarks r LEFT JOIN dbo.CASRemarksTwo t ON t.CASNumber = r.CASNumber LEFT JOIN dbo.CASRemarkThree h ON h.CASNumber = r.CASNumber WHERE r.CASNumber = @cas",
                new { cas }, write.Transaction);
            Assert.Contains(line, remarks);
        }
    }
}
