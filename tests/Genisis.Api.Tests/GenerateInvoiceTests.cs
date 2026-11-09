using Dapper;
using Genisis.Api.Common;
using Genisis.Api.ContributionBilling.GenerateInvoice;
using Genisis.Api.Data;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Contribution Billing > Generate Invoice: the validators and the service, against a fake repository.</summary>
public class GenerateInvoiceRulesTests
{
    private sealed class FakeRepository : IMcoInvoiceRepository
    {
        public List<McoInvoiceRow> Rows { get; set; } = [];

        public List<string> Calls { get; } = [];

        public Task<List<McoInvoiceRow>> SearchAsync(IDbSession session, McoInvoiceFilter filter)
        {
            Calls.Add("search " + (filter.Criteria == "invoice" ? filter.InvoiceNo : filter.Criteria));
            return Task.FromResult(filter.Criteria == "invoice" && Calls.Contains("stamped") ? Rows.Select(r => r with { InvoiceNo = filter.InvoiceNo, InvoiceDate = new DateTime(2026, 10, 30) }).ToList() : Rows);
        }

        public Task<List<string>> GroupCompaniesAsync(IDbSession session, string payor) => Task.FromResult(new List<string> { "ACME" });

        public Task<McoInvoicePayor?> PayorAsync(IDbSession session, string payor) => Task.FromResult<McoInvoicePayor?>(new McoInvoicePayor(payor, "A PAYOR", "1", "2", "3", "C", "T", "F", "P", false));

        public Task<string> NextInvoiceAsync(IDbSession session, string payor, string prefix)
        {
            Calls.Add($"next {payor} {prefix}");
            return Task.FromResult(prefix + "00042");
        }

        public Task StampAsync(IDbSession session, McoInvoiceRow row, string invoiceNo, DateTime invoiceDate, string userCode)
        {
            Calls.Add("stamped");
            return Task.CompletedTask;
        }
    }

    private static McoInvoiceRow Row(string member, string cover, string relation, string? invoice = null, string ins = "H", string type = "Adult", decimal fee = 10m, string? dept = "D1") =>
        new() { MemberNo = member, CoverId = cover, Relation = relation, InvoiceNo = invoice, PayorCode = "AB", InsCode = ins, PlanCode = "P1", AdultChild = type, McoFee = fee, BasicMco = fee - 1, GroupCompany = "ACME", Department = dept };

    private static McoInvoiceService Service(FakeRepository repository, FakeSessions sessions) => new(sessions, repository, NullLogger<McoInvoiceService>.Instance);

    private static McoInvoicePrintRequest Request(bool allocate, string type = "invoice", Action<McoInvoicePrintRequest>? tweak = null)
    {
        var r = new McoInvoicePrintRequest { Criteria = "batch", Payor = "AB - X", BordxDate = new DateTime(2021, 7, 30), BatchNo = "1", ReportType = type, Allocate = allocate };
        tweak?.Invoke(r);
        return r;
    }

    [Fact]
    public void The_checks_come_in_the_desktops_order()
    {
        var validator = new McoInvoiceFilterValidator();
        Assert.Equal("Please Enter MCO Criteria!", validator.FirstError(new McoInvoiceFilter()));
        Assert.Equal("Please Enter Payor!", validator.FirstError(new McoInvoiceFilter { Criteria = "batch" }));
        Assert.Equal("Please Enter Bordereaux Date!", validator.FirstError(new McoInvoiceFilter { Criteria = "batch", Payor = "AB" }));
        Assert.Equal("Please Enter Batch No!", validator.FirstError(new McoInvoiceFilter { Criteria = "batch", Payor = "AB", BordxDate = new DateTime(2021, 7, 30) }));
        Assert.Equal("Batch No must be a number.", validator.FirstError(new McoInvoiceFilter { Criteria = "batch", Payor = "AB", BordxDate = new DateTime(2021, 7, 30), BatchNo = "x" }));
        Assert.Equal("Please Enter Bordereaux Date From!", validator.FirstError(new McoInvoiceFilter { Criteria = "range", Payor = "AB" }));
        Assert.Equal("Please Enter Bordereaux Date To!", validator.FirstError(new McoInvoiceFilter { Criteria = "range", Payor = "AB", BordxFrom = new DateTime(2021, 7, 1) }));
        Assert.Equal("Please Enter Invoice No!", validator.FirstError(new McoInvoiceFilter { Criteria = "invoice" }));
        Assert.Null(validator.FirstError(new McoInvoiceFilter { Criteria = "invoice", InvoiceNo = "S1" }));
        Assert.Equal("Please Enter Group Company!", validator.FirstError(new McoInvoiceFilter { Criteria = "range", Payor = "AB", BordxFrom = new DateTime(2021, 7, 1), BordxTo = new DateTime(2021, 7, 31), GroupBy = "group" }));
        Assert.Null(validator.FirstError(new McoInvoiceFilter { Criteria = "invoice", InvoiceNo = "S1", GroupBy = "group" }));
        Assert.NotNull(validator.FirstError(new McoInvoiceFilter { Criteria = "invoice", InvoiceNo = "S1", GroupBy = "x" }));
    }

    [Fact]
    public void The_endorsement_range_needs_a_payor_and_both_endorsement_dates_in_order()
    {
        var validator = new McoInvoiceFilterValidator();
        Assert.Equal("Please Enter Payor!", validator.FirstError(new McoInvoiceFilter { Criteria = "endorsement" }));
        Assert.Equal("Please Enter Endorsement Date From!", validator.FirstError(new McoInvoiceFilter { Criteria = "endorsement", Payor = "BS" }));
        Assert.Equal("Please Enter Endorsement Date To!", validator.FirstError(new McoInvoiceFilter { Criteria = "endorsement", Payor = "BS", BordxFrom = new DateTime(2021, 8, 5) }));
        Assert.Equal("Date From cannot be later than Date To.", validator.FirstError(new McoInvoiceFilter { Criteria = "endorsement", Payor = "BS", BordxFrom = new DateTime(2021, 8, 6), BordxTo = new DateTime(2021, 8, 5) }));
        Assert.Null(validator.FirstError(new McoInvoiceFilter { Criteria = "endorsement", Payor = "BS", BordxFrom = new DateTime(2021, 8, 5), BordxTo = new DateTime(2021, 8, 5) }));
    }

    [Fact]
    public void Print_and_preview_need_a_report_type_and_a_listing_version()
    {
        var validator = new McoInvoicePrintRequestValidator();
        Assert.Equal("Please Select Report Type !", validator.FirstError(Request(true, "")));
        Assert.Equal("Select Invoice or Fee Listing.", validator.FirstError(Request(true, "pdf")));
        Assert.Equal("Please Enter Leaflet Version !", validator.FirstError(Request(true, tweak: r => r.ListingVersion = "")));
        Assert.Null(validator.FirstError(Request(true)));
        Assert.Equal("Please Enter Payor!", validator.FirstError(new McoInvoicePrintRequest { Criteria = "batch", ReportType = "invoice" }));
    }

    [Fact]
    public async Task Search_counts_principals_and_covered_persons_and_notes_billing_multiple_invoices_and_empty_fees()
    {
        var repository = new FakeRepository { Rows = [Row("M1", "00", "P"), Row("M1", "01", "H"), Row("M2", "00", "P") with { McoFee = null }] };
        var result = await Service(repository, new FakeSessions()).SearchAsync(new McoInvoiceFilter { Criteria = "batch", Payor = "AB" });
        Assert.Equal((2, 1, false, false, true), (result.Principals, result.Supplementary, result.Billed, result.MultipleInvoices, result.FeeEmpty));
        repository.Rows = [Row("M1", "00", "P", "S1"), Row("M2", "00", "P", "S2")];
        var billed = await Service(repository, new FakeSessions()).SearchAsync(new McoInvoiceFilter { Criteria = "invoice", InvoiceNo = "S1" });
        Assert.Equal((true, true), (billed.Billed, billed.MultipleInvoices));
        repository.Rows = [];
        Assert.Equal("Record not found !", (await Assert.ThrowsAsync<NotFoundException>(() => Service(repository, new FakeSessions()).SearchAsync(new McoInvoiceFilter { Criteria = "batch", Payor = "AB" }))).Message);
    }

    [Fact]
    public async Task Print_gives_one_new_invoice_number_to_every_member_in_one_transaction_and_groups_the_invoice_lines()
    {
        var repository = new FakeRepository { Rows = [Row("M1", "00", "P"), Row("M1", "01", "H", type: "Child", fee: 4m), Row("M2", "00", "P", ins: "F", dept: "D2")] };
        var sessions = new FakeSessions();
        var document = await Service(repository, sessions).PrintAsync(Request(true, tweak: r => r.GroupBy = "department"), "U1");
        Assert.Equal(["search batch", "next AB P", "stamped", "stamped", "stamped", "search P00042"], repository.Calls);
        Assert.True(sessions.Last.Committed);
        Assert.Equal((true, "P00042"), (document.Allocated, document.InvoiceNo));
        Assert.All(document.Rows, r => Assert.Equal("P00042", r.InvoiceNo));
        Assert.Equal([("D1", "H", "Adult", 1, 1, 10m), ("D1", "H", "Child", 0, 1, 4m), ("D2", "F", "Adult", 1, 1, 10m)], document.Lines.Select(l => (l.Group, l.InsCode, l.AdultChild, l.Principals, l.Members, l.McoFee)));
        var byPayor = await Service(new FakeRepository { Rows = repository.Rows }, new FakeSessions()).PrintAsync(Request(false), "U1");
        Assert.All(byPayor.Lines, l => Assert.Null(l.Group));
    }

    [Fact]
    public async Task Preview_writes_nothing_the_fee_listing_has_no_lines_and_the_principal_criteria_list_principals_only()
    {
        var repository = new FakeRepository { Rows = [Row("M1", "00", "P", "S1"), Row("M1", "01", "H", "S1")] };
        var sessions = new FakeSessions();
        var preview = await Service(repository, sessions).PrintAsync(Request(false, "listing", r => (r.Criteria, r.InvoiceNo, r.PrincipalsOnly) = ("invoice", "S1", true)), "U1");
        Assert.Equal((false, "S1"), (preview.Allocated, preview.InvoiceNo));
        Assert.Empty(preview.Lines);
        Assert.Single(preview.Rows);
        Assert.False(sessions.Last.Committed);
        Assert.DoesNotContain(repository.Calls, c => c.StartsWith("next") || c == "stamped");
    }

    [Fact]
    public async Task Print_is_refused_for_billed_members_and_more_than_one_invoice_is_refused_for_both()
    {
        var billed = new FakeRepository { Rows = [Row("M1", "00", "P", "S1")] };
        var failure = await Assert.ThrowsAsync<BusinessRuleException>(() => Service(billed, new FakeSessions()).PrintAsync(Request(true, tweak: r => (r.Criteria, r.InvoiceNo) = ("invoice", "S1")), "U1"));
        Assert.Equal("These members are billed already. Use Preview to print the invoice again.", failure.Message);
        Assert.DoesNotContain(billed.Calls, c => c == "stamped");
        var multiple = new FakeRepository { Rows = [Row("M1", "00", "P", "S1"), Row("M2", "00", "P", "S2")] };
        foreach (var allocate in new[] { true, false })
        {
            var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => Service(multiple, new FakeSessions()).PrintAsync(Request(allocate), "U1"));
            Assert.Equal("Different Invoice Found in Same Batch or Range, Please Search by Invoice No ! ", refused.Message);
        }
    }
}

/// <summary>Generate Invoice against the database. Every write is made in a session that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class GenerateInvoiceDatabaseTests
{
    private static McoInvoiceRepository Repository => new();

    private static async Task<int> Count(IDbSession s, string sql, object? args = null) => await s.Connection.ExecuteScalarAsync<int>(sql, args, s.Transaction);

    private const string Checksum = "SELECT (SELECT ISNULL(CHECKSUM_AGG(CHECKSUM(MBMNumber, MBMInvoiceNo, MBMInvoiceDate, MBMInvoiceBy)), 0) + COUNT(*) FROM dbo.MBMOthers) + (SELECT ISNULL(CHECKSUM_AGG(CHECKSUM(MBMCNumber, MBMCCoverID, MBMInvoiceNo, MBMInvoiceDate, MBMInvoiceBy)), 0) + COUNT(*) FROM dbo.MBMCoveredPersons) + (SELECT COUNT(*) FROM dbo.MBMMCOInvoice) + (SELECT ISNULL(CHECKSUM_AGG(CHECKSUM(MCOInvIndex, MCOInvoice_No)), 0) FROM dbo.MBMMCOINVQ) + (SELECT ISNULL(CHECKSUM_AGG(CHECKSUM(MBMNumber, Status, LastUpdateUser)), 0) FROM genisis.Maint_MCOPending)";

    private static readonly McoInvoiceFilter Batch = new() { Criteria = "batch", Payor = "wk - x", BordxDate = new DateTime(2021, 4, 30), BatchNo = "2" };

    [Fact]
    public async Task The_search_gives_the_unbilled_members_of_a_batch_a_range_and_the_billed_members_of_an_invoice()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var rows = await Repository.SearchAsync(session, Batch);
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM (SELECT DISTINCT MBMNumber, MBMCCoverID, MBMCRELCode, MBMBordxDate, MBMPolicyNo, HLTCode, MBMBatchNo, MBMName, MBMCName, MBMGroupCompany, MBMDepartment, MBMInvoiceNo, MBMInvoiceDate, PLNCode, INSCode, MBMPayorEffDate, MBMPayorExpDate, Type, MBMBasicMCO, MBMMCOFee, MBMPremium, MBMIcBcPp, PAYCode FROM dbo.VIEW_MCO_LISTING_All2 WHERE PAYCode = 'WK' AND MBMBordxDate = '2021-04-30' AND MBMBatchNo = 2 AND (MBMInvoiceNo IS NULL OR MBMInvoiceNo = '')) x"), rows.Count);
        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.True(string.IsNullOrEmpty(r.InvoiceNo) && r.PayorCode == "WK" && r.BatchNo == 2));
        var range = await Repository.SearchAsync(session, new McoInvoiceFilter { Criteria = "range", Payor = "WK", BordxFrom = new DateTime(2021, 4, 1), BordxTo = new DateTime(2021, 4, 30) });
        Assert.True(range.Count >= rows.Count);
        var company = rows[0].GroupCompany;
        Assert.All(await Repository.SearchAsync(session, new McoInvoiceFilter { Criteria = "batch", Payor = "WK", BordxDate = Batch.BordxDate, BatchNo = "2", GroupBy = "group", GroupCompany = company }), r => Assert.Equal(company, r.GroupCompany));
        Assert.Empty(await Repository.SearchAsync(session, new McoInvoiceFilter { Criteria = "batch", Payor = "WK", BordxDate = new DateTime(2099, 1, 1), BatchNo = "1" }));

        var billed = await session.Connection.QueryFirstAsync<string>("SELECT TOP (1) RTRIM(MBMInvoiceNo) FROM dbo.VIEW_MCO_LISTING_All2 WHERE MBMInvoiceNo <> '' AND PAYCode = 'AM'", transaction: session.Transaction);
        var byInvoice = await Repository.SearchAsync(session, new McoInvoiceFilter { Criteria = "invoice", InvoiceNo = billed });
        Assert.NotEmpty(byInvoice);
        Assert.All(byInvoice, r => Assert.Equal(billed, r.InvoiceNo));
    }

    [Fact]
    public async Task The_endorsement_range_gives_the_unbilled_members_endorsed_in_it_with_their_covered_persons()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var day = await session.Connection.QueryFirstOrDefaultAsync<DateTime?>(
            "SELECT TOP (1) t.MBMENDtBordxDate FROM dbo.MBMTwo t JOIN dbo.MBM m ON m.MBMNumber = t.MBMNumber JOIN dbo.MBMOthers o ON o.MBMNumber = t.MBMNumber WHERE m.PAYCode = 'BS' AND t.MBMENDtBordxDate IS NOT NULL AND ISNULL(o.MBMInvoiceNo, '') = '' AND m.MBMStatus <> 'D' ORDER BY t.MBMENDtBordxDate DESC",
            transaction: session.Transaction);
        if (day is null) return;
        var filter = new McoInvoiceFilter { Criteria = "endorsement", Payor = "BS", BordxFrom = day.Value.Date, BordxTo = day.Value.Date };
        var rows = await Repository.SearchAsync(session, filter);
        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.True(string.IsNullOrEmpty(r.InvoiceNo) && r.PayorCode == "BS"));
        var members = rows.Select(r => r.MemberNo).Distinct().ToList();
        Assert.Equal(members.Count, await Count(session, "SELECT COUNT(*) FROM dbo.MBMTwo WHERE MBMNumber IN @members AND MBMENDtBordxDate >= @day AND MBMENDtBordxDate < DATEADD(day, 1, @day)", new { members, day = day.Value.Date }));
        Assert.Contains(rows, r => r.IsPrincipal);

        var bordx = await Repository.SearchAsync(session, new McoInvoiceFilter { Criteria = "range", Payor = "BS", BordxFrom = day.Value.Date, BordxTo = day.Value.Date });
        Assert.All(bordx, r => Assert.Equal(day.Value.Date, r.BordxDate?.Date));
        Assert.Empty(await Repository.SearchAsync(session, new McoInvoiceFilter { Criteria = "endorsement", Payor = "BS", BordxFrom = new DateTime(2099, 1, 1), BordxTo = new DateTime(2099, 1, 2) }));
    }

    [Fact]
    public async Task The_payor_is_billed_directly_or_through_its_broker_and_the_group_companies_are_listed()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var third = await Repository.PayorAsync(session, "WK");
        Assert.True(third!.ThirdParty);
        Assert.Equal("S", third.Prefix);
        Assert.Equal("AAO PANA INSURANCE BROKERS SDN BHD", third.Name);
        var direct = await Repository.PayorAsync(session, "AH");
        Assert.False(direct!.ThirdParty);
        Assert.Equal("S", direct.Prefix);
        Assert.Equal("P", (await Repository.PayorAsync(session, "XP"))!.Prefix);
        Assert.Null(await Repository.PayorAsync(session, "QQ"));
        var companies = await Repository.GroupCompaniesAsync(session, "WK");
        Assert.NotEmpty(companies);
        Assert.Equal(companies.Order(StringComparer.Ordinal), companies);
    }

    [Fact]
    public async Task Stamping_sets_the_invoice_on_the_members_and_covered_persons_adds_the_invoice_rows_and_nothing_survives_the_rollback()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var before = await Count(read, Checksum);
        var members = await Repository.SearchAsync(read, Batch);
        var two = members.Take(2).ToList();
        Assert.Equal(2, two.Count);
        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var number = await Repository.NextInvoiceAsync(write, "WK", "S");
            foreach (var m in two) await Repository.StampAsync(write, m, number, new DateTime(2026, 10, 30), "E2E");
            Assert.Equal(2, await Count(write, "SELECT COUNT(*) FROM dbo.MBMMCOInvoice WHERE MCOInvNo = @n AND MCoInvBy = 'E2E'", new { n = number }));
            var covered = two.Count(m => (m.CoverId ?? "00").Trim() != "00");
            Assert.Equal(covered, await Count(write, "SELECT COUNT(*) FROM dbo.MBMCoveredPersons WHERE MBMInvoiceNo = @n AND MBMInvoiceBy = 'E2E'", new { n = number }));
            Assert.Equal(two.Count - covered, await Count(write, "SELECT COUNT(*) FROM dbo.MBMOthers WHERE MBMInvoiceNo = @n AND MBMInvoiceBy = 'E2E'", new { n = number }));
            Assert.Equal(0, await Count(write, "SELECT COUNT(*) FROM genisis.Maint_MCOPending WHERE MBMNumber IN @m AND (Status <> 'B' OR LastUpdateUser <> 'E2E')", new { m = two.Select(x => x.MemberNo).ToArray() }));
            var stamped = await Repository.SearchAsync(write, new McoInvoiceFilter { Criteria = "invoice", InvoiceNo = number });
            Assert.Equal(2, stamped.Count);
            Assert.Equal(members.Count - 2, (await Repository.SearchAsync(write, Batch)).Count);
        }

        Assert.Equal(before, await Count(read, Checksum));
    }
}
