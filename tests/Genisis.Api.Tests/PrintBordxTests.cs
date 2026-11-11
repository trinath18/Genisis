using Dapper;
using Genisis.Api.ClaimBordx.Print;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Claim Bordx > Print Bordx: the validators, the arithmetic and the service, against a fake repository.</summary>
public class PrintBordxRulesTests
{
    private sealed class FakeRepository : IPrintBordxRepository
    {
        public List<PrintBordxRow> Rows { get; set; } = [];

        public List<string> Calls { get; } = [];

        public Task<List<PrintBordxRow>> SearchAsync(IDbSession session, PrintBordxFilter filter) => Task.FromResult(Rows);

        public Task<List<PrintBordxReportRow>> ReportAsync(IDbSession session, PrintBordxFilter filter) =>
            Task.FromResult(new List<PrintBordxReportRow>
            {
                new() { BordxNo = "B1", CaseNo = "C1", TotalActual = 10, TotalApproved = 8, PayableToHospital = 7, PayableToMember = 1, TotalMgmtFee = 2, BordxAmount = 8 },
                new() { BordxNo = "B1", CaseNo = "C2", TotalActual = 20, TotalApproved = 16, PayableToHospital = 15, PayableToMember = 1, TotalMgmtFee = 3, BordxAmount = 16 },
                new() { BordxNo = "B2", CaseNo = "C3", TotalActual = 5, TotalApproved = 4, PayableToHospital = 4, PayableToMember = 0, TotalMgmtFee = 1, BordxAmount = 4 },
            });

        public Task<PrintBordxPayor?> PayorAsync(IDbSession session, string payor, string? plan, string? health)
        {
            Calls.Add($"payor {payor} {plan} {health}");
            return Task.FromResult<PrintBordxPayor?>(new PrintBordxPayor(payor, "A PAYOR", "1", "2", "3", "SIHAT"));
        }

        public Task<(int Bordx, int Claims, int Invoices)> MarkAsync(IDbSession session, PrintBordxFilter filter, string userCode)
        {
            Calls.Add($"mark {filter.BordxFrom}-{filter.BordxTo} {userCode}");
            return Task.FromResult((2, 3, 3));
        }
    }

    private static PrintBordxRow Row(bool printed, string btbg = "N", string payTo = "H") =>
        new() { CaseNo = "C1", ClaimVersion = 1, Printed = printed, Btbg = btbg, PayTo = payTo, PayorCode = "AH", PlanCode = "P1", HealthType = "S", TotalApproved = 8, PayableToHospital = 7, PayableToMember = 1, TotalMgmtFee = 2 };

    private static PrintBordxService Service(FakeRepository repository, FakeSessions sessions) => new(sessions, repository, NullLogger<PrintBordxService>.Instance);

    private static PrintBordxRequest Request(string kind = "detail", bool print = true, string option = "new") => new() { BordxFrom = "b1", BordxTo = "b2", Kind = kind, Print = print, PrintOption = option };

    [Fact]
    public void Both_ends_of_the_range_are_needed_and_the_choices_are_the_ones_offered()
    {
        Assert.Equal("Please enter Bordx Reference No", new PrintBordxFilterValidator().FirstError(new PrintBordxFilter { BordxFrom = "B1" }));
        var validator = new PrintBordxRequestValidator();
        Assert.Null(validator.FirstError(Request()));
        Assert.Equal("Please enter Bordx Reference No", validator.FirstError(new PrintBordxRequest { Kind = "detail" }));
        Assert.NotNull(validator.FirstError(Request("pdf")));
        Assert.NotNull(validator.FirstError(new PrintBordxRequest { BordxFrom = "B1", BordxTo = "B2", Kind = "detail", Btbg = "X" }));
        Assert.NotNull(validator.FirstError(new PrintBordxRequest { BordxFrom = "B1", BordxTo = "B2", Kind = "detail", ReportType = "x" }));
        Assert.NotNull(validator.FirstError(new PrintBordxRequest { BordxFrom = "B1", BordxTo = "B2", Kind = "detail", PrintOption = "x" }));
    }

    [Fact]
    public void The_total_follows_the_desktop_and_the_print_status_is_taken_from_the_last_claim()
    {
        Assert.Equal(7m, PrintBordxRules.Amount(Row(false, "Y", "H")));
        Assert.Equal(1m, PrintBordxRules.Amount(Row(false, "Y", "M")));
        Assert.Equal(2m, PrintBordxRules.Amount(Row(false, "N", "H")));
        Assert.Equal(7m, Row(false, "Y").BordxAmount);
        Assert.Equal(8m, Row(false, "N").BordxAmount);
        Assert.Equal("reprint", PrintBordxRules.SuggestedOption([Row(false), Row(true)]));
        Assert.Equal("new", PrintBordxRules.SuggestedOption([Row(true), Row(false)]));
    }

    [Fact]
    public async Task Search_totals_and_counts_what_is_printed_and_nothing_found_is_not_found()
    {
        var repository = new FakeRepository { Rows = [Row(true), Row(false, "Y", "M")] };
        var result = await Service(repository, new FakeSessions()).SearchAsync(new PrintBordxFilter { BordxFrom = "B1", BordxTo = "B2" });
        Assert.Equal((3m, 1, 1, "new"), (result.TotalAmount, result.Printed, result.NotPrinted, result.PrintOption));
        repository.Rows = [];
        var failure = await Assert.ThrowsAsync<NotFoundException>(() => Service(repository, new FakeSessions()).SearchAsync(new PrintBordxFilter { BordxFrom = "B1", BordxTo = "B2" }));
        Assert.Equal("No record found ", failure.Message);
    }

    [Fact]
    public async Task Printing_the_detail_report_marks_the_range_in_one_transaction_and_summarises_by_bordx()
    {
        var repository = new FakeRepository { Rows = [Row(false), Row(false)] };
        var sessions = new FakeSessions();
        var document = await Service(repository, sessions).PrintAsync(Request(), "U1");
        Assert.True(document.Marked);
        Assert.Equal(["payor AH P1 S", "mark b1-b2 U1"], repository.Calls);
        Assert.True(sessions.Last.Committed);
        Assert.Equal(("B1", "B2", "A PAYOR"), (document.BordxFrom, document.BordxTo, document.Payor!.Name));
        Assert.Equal([(2, 30m, 24m, 22m), (1, 5m, 4m, 4m)], document.Summary.Select(s => (s.Claims, s.TotalActual, s.TotalApproved, s.PayableToHospital)));
        Assert.Equal(2m + 3m, document.Summary[0].MgmtFee);
    }

    [Fact]
    public async Task Previews_and_the_summary_report_write_nothing()
    {
        foreach (var request in new[] { Request("detail", false), Request("summary", true) })
        {
            var repository = new FakeRepository { Rows = [Row(false)] };
            var sessions = new FakeSessions();
            var document = await Service(repository, sessions).PrintAsync(request, "U1");
            Assert.False(document.Marked);
            Assert.DoesNotContain(repository.Calls, c => c.StartsWith("mark"));
            Assert.False(sessions.Last.Committed);
        }
    }

    [Fact]
    public async Task The_print_status_is_checked_against_the_claims_before_the_detail_report_is_printed()
    {
        var neverPrinted = new FakeRepository { Rows = [Row(false)] };
        var sessions = new FakeSessions();
        var first = await Assert.ThrowsAsync<BusinessRuleException>(() => Service(neverPrinted, sessions).PrintAsync(Request(option: "reprint"), "U1"));
        Assert.Equal("Please select 'Print New' option for this report", first.Message);
        var allPrinted = new FakeRepository { Rows = [Row(true)] };
        var second = await Assert.ThrowsAsync<BusinessRuleException>(() => Service(allPrinted, new FakeSessions()).PrintAsync(Request(option: "new"), "U1"));
        Assert.Equal("This report already printed, please select 'Reprint' ", second.Message);
        Assert.DoesNotContain(allPrinted.Calls, c => c.StartsWith("mark"));
        var reprint = await Service(allPrinted, new FakeSessions()).PrintAsync(Request(option: "reprint"), "U1");
        Assert.True(reprint.Marked);
        var mixed = new FakeRepository { Rows = [Row(true), Row(false)] };
        Assert.True((await Service(mixed, new FakeSessions()).PrintAsync(Request(option: "new"), "U1")).Marked);
    }
}

/// <summary>Print Bordx against the database. Every write is made in a session that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class PrintBordxDatabaseTests
{
    private static PrintBordxRepository Repository => new();

    private static async Task<int> Count(IDbSession s, string sql, object? args = null) => await s.Connection.ExecuteScalarAsync<int>(sql, args, s.Transaction);

    private const string Checksum = "SELECT ISNULL(CHECKSUM_AGG(CHECKSUM(BordxRefNo, BordxPrintedStatus, BordxPrintedDate, BordxPrintedBy)), 0) + COUNT(*) FROM genisis.WS_Bordx";

    private const string ClaimChecksum = "SELECT ISNULL(CHECKSUM_AGG(CHECKSUM(CASNumber, ClaimVersion, CLMPending)), 0) + COUNT(*) FROM genisis.WS_Claim";

    private const string InvoiceChecksum = "SELECT ISNULL(CHECKSUM_AGG(CHECKSUM(CASNumber, INVWSversion, CLMInvPending)), 0) + COUNT(*) FROM genisis.WS_ClaimInvoice";

    private static readonly PrintBordxFilter Range = new() { BordxFrom = "nut0000072", BordxTo = "NUT0000074" };

    [Fact]
    public async Task The_search_and_the_report_give_the_claims_of_the_range_with_the_payor_and_the_product()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var rows = await Repository.SearchAsync(session, Range);
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM (SELECT DISTINCT PLNCode, INSCode, CASNumber, ClaimVersion, PatientName, MBMNumber, HOSName, CLMPayableByMedix2H, CLMTotalActual + COALESCE(TotGST, 0) a, CLMTotalApproved, CLMPayableByMedix2M, DebitCreditRefNo, BordxRefNo, BordxPrintedStatus, HLTCode, PAYCode, MBMBTBG, INVPAYTo, CLMCaseMgmtFees, TotalMgmtFee, BordxDate FROM genisis.WS_PrintBordx WHERE ClaimBordxStatus = 1 AND INVBordxed = 1 AND BordxRefNo >= 'NUT0000072' AND BordxRefNo <= 'NUT0000074') x"), rows.Count);
        Assert.True(rows.Count >= 15);
        Assert.All(rows, r => Assert.InRange(string.CompareOrdinal(r.BordxNo, "NUT0000072"), 0, 2));
        Assert.Equal(rows.OrderBy(r => r.BordxNo, StringComparer.Ordinal).ThenBy(r => r.CaseNo, StringComparer.Ordinal).ThenBy(r => r.ClaimVersion).Select(r => (r.BordxNo, r.CaseNo, r.ClaimVersion)), rows.Select(r => (r.BordxNo, r.CaseNo, r.ClaimVersion)));
        var report = await Repository.ReportAsync(session, Range);
        Assert.Equal(await Count(session, "SELECT COUNT(DISTINCT CONCAT(BordxRefNo, '|', CASNumber, '|', ClaimVersion)) FROM genisis.WS_PrintClaimBordx WHERE BordxRefNo >= 'NUT0000072' AND BordxRefNo <= 'NUT0000074'"), report.Select(r => (r.BordxNo, r.CaseNo, r.ClaimVersion)).Distinct().Count());
        Assert.All(report, r => Assert.False(string.IsNullOrWhiteSpace(r.PatientName)));
        var payor = await Repository.PayorAsync(session, "UT", rows[0].PlanCode, rows[0].HealthType);
        Assert.Equal("UT", payor!.Code);
        Assert.False(string.IsNullOrWhiteSpace(payor.Name));
        Assert.Null((await Repository.PayorAsync(session, "UT", null, "S"))!.Product);
        var plan = await session.Connection.QueryFirstAsync<(string Plan, string Payor)>("SELECT TOP (1) RTRIM(PLNCode), RTRIM(PAYCode) FROM dbo.PLN WHERE RTRIM(PLNProCat) = 'NS'", transaction: session.Transaction);
        Assert.Equal("GROUP H&S", (await Repository.PayorAsync(session, plan.Payor, plan.Plan, "N"))!.Product);
        Assert.Null(await Repository.PayorAsync(session, "QQ", null, null));
        Assert.Empty(await Repository.SearchAsync(session, new PrintBordxFilter { BordxFrom = "ZZZ1", BordxTo = "ZZZ2" }));
    }

    [Fact]
    public async Task Marking_sets_the_bordx_the_worksheets_and_the_invoices_as_printed_and_nothing_survives_the_rollback()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var (bordx, claims, invoices) = (await Count(read, Checksum), await Count(read, ClaimChecksum), await Count(read, InvoiceChecksum));
        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var (b, c, i) = await Repository.MarkAsync(write, Range, "E2E");
            Assert.Equal(3, b);
            Assert.True(c >= 15 && i >= 15);
            var marked = await Repository.SearchAsync(write, Range);
            Assert.All(marked, r => Assert.True(r.Printed));
            Assert.Equal(0, await Count(write, "SELECT COUNT(*) FROM genisis.WS_Bordx WHERE BordxRefNo >= 'NUT0000072' AND BordxRefNo <= 'NUT0000074' AND (BordxPrintedStatus <> 1 OR BordxPrintedBy <> 'E2E' OR BordxPrintedDate <> CAST(GETDATE() AS date))"));
            Assert.Equal(0, await Count(write, "SELECT COUNT(*) FROM genisis.WS_Claim WHERE BordxRefNo >= 'NUT0000072' AND BordxRefNo <= 'NUT0000074' AND ISNULL(CLMPending, '') <> 'B'"));
            Assert.Equal(0, await Count(write, "SELECT COUNT(*) FROM genisis.WS_ClaimInvoice WHERE INVBordxRefNo >= 'NUT0000072' AND INVBordxRefNo <= 'NUT0000074' AND ISNULL(CLMInvPending, '') <> 'B'"));
        }

        Assert.Equal(bordx, await Count(read, Checksum));
        Assert.Equal(claims, await Count(read, ClaimChecksum));
        Assert.Equal(invoices, await Count(read, InvoiceChecksum));
    }
}
