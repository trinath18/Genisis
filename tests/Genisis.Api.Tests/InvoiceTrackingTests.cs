using Dapper;
using Genisis.Api.Data;
using Genisis.Api.Invoice;

namespace Genisis.Api.Tests;

/// <summary>Invoice Tracking's rules and service, without a database.</summary>
public class InvoiceTrackingRulesTests
{
    private sealed class FakeRepository : IInvoiceTrackingRepository
    {
        public int Rows { get; set; } = 4;
        public string? Called { get; private set; }

        public Task<List<InvoiceTrackingDetailRow>> DetailAsync(IDbSession session, InvoiceTrackingFilter filter, int max)
        {
            Called = "detail";
            return Task.FromResult(Enumerable.Range(1, Math.Min(Rows, max)).Select(i => new InvoiceTrackingDetailRow { CaseNo = $"AB{i}" }).ToList());
        }

        public Task<List<InvoiceTrackingSummaryRow>> SummaryAsync(IDbSession session, InvoiceTrackingFilter filter, int report, int max)
        {
            Called = $"summary {report}";
            return Task.FromResult(Enumerable.Range(1, Math.Min(Rows, max)).Select(i => new InvoiceTrackingSummaryRow { TotalCases = i }).ToList());
        }

        public Task<List<string>> GroupCompaniesAsync(IDbSession session, string payor) => Task.FromResult(new List<string> { payor + " GROUP" });

        public Task<List<InvoiceOption>> HospitalsAsync(IDbSession session) => Task.FromResult(new List<InvoiceOption>());
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData(" 3 ", true)]
    [InlineData("4", true)]
    [InlineData("5", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void A_report_must_be_chosen_with_the_desktop_message(string? report, bool valid)
    {
        var error = new InvoiceTrackingFilterValidator().FirstError(new InvoiceTrackingFilter { Report = report });
        Assert.Equal(valid ? null : "Please select a Report.", error);
    }

    [Fact]
    public void The_drop_downs_hold_only_their_own_values()
    {
        var validator = new InvoiceTrackingFilterValidator();
        Assert.Equal("Select a valid stay type.", validator.FirstError(new InvoiceTrackingFilter { Report = "0", StayType = "9" }));
        Assert.Equal("Select a valid pay type.", validator.FirstError(new InvoiceTrackingFilter { Report = "0", PayType = "X" }));
        Assert.Equal("Select a valid pay to.", validator.FirstError(new InvoiceTrackingFilter { Report = "0", PayTo = "Z" }));
        Assert.Null(validator.FirstError(new InvoiceTrackingFilter { Report = "0", StayType = "4", PayType = "R", PayTo = "H" }));
    }

    [Theory]
    [InlineData("0", "I")]
    [InlineData("1", "O")]
    [InlineData("2", "F")]
    [InlineData("3", "P")]
    [InlineData("4", "PF")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void The_desktops_stay_type_numbers_become_the_codes_the_procedure_takes(string? number, string? code) => Assert.Equal(code, InvoiceTrackingRules.StayCode(number));

    [Fact]
    public async Task Report_3_reads_the_detail_and_the_others_the_summary_and_a_long_list_is_cut_off()
    {
        var repository = new FakeRepository();
        var service = new InvoiceTrackingService(new FakeSessions(), repository);
        var detail = await service.SearchAsync(new InvoiceTrackingFilter { Report = "3" });
        Assert.Equal(("detail", 4, 0, false), (repository.Called, detail.Detail.Count, detail.Summary.Count, detail.Truncated));
        var summary = await service.SearchAsync(new InvoiceTrackingFilter { Report = " 2 " });
        Assert.Equal(("summary 2", "2", 0, 4), (repository.Called, summary.Report, summary.Detail.Count, summary.Summary.Count));

        repository.Rows = InvoiceTrackingService.MaxRows + 1;
        var cut = await service.SearchAsync(new InvoiceTrackingFilter { Report = "3" });
        Assert.True(cut.Truncated);
        Assert.Equal(InvoiceTrackingService.MaxRows, cut.Detail.Count);
        Assert.True((await service.SearchAsync(new InvoiceTrackingFilter { Report = "0" })).Truncated);
    }

    [Fact]
    public async Task Group_companies_need_a_payor()
    {
        var service = new InvoiceTrackingService(new FakeSessions(), new FakeRepository());
        Assert.Empty(await service.GroupCompaniesAsync(" "));
        Assert.Equal("AB GROUP", Assert.Single(await service.GroupCompaniesAsync(" AB ")));
    }
}

/// <summary>Invoice Tracking against the database: the summaries must agree with the invoices and with each other.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class InvoiceTrackingDatabaseTests
{
    private static readonly InvoiceTrackingRepository Repository = new();
    private const int Max = 100000;

    private static InvoiceTrackingFilter January() => new() { Report = "0", InvoiceDateFrom = new DateTime(2021, 1, 1), InvoiceDateTo = new DateTime(2021, 1, 31) };

    [Fact]
    public async Task The_month_summary_counts_each_invoice_once_and_each_worksheets_payable_once()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var rows = await Repository.SummaryAsync(session, January(), 0, Max);
        var month = Assert.Single(rows);
        Assert.Equal((2021, 1), (month.InvYear, month.InvMonth));
        var expected = await session.Connection.QueryFirstAsync<(decimal Amount, int Worksheets, decimal Hospital, decimal Member)>(
            "SELECT SUM(INVAmount), COUNT(DISTINCT CASNumber + '-' + INVWSversion), " +
            "(SELECT SUM(x.H) FROM (SELECT DISTINCT t.CASNumber, t.INVWSversion, c.CLMPayableByMedix2H AS H FROM genisis.WS_InvoiceTracking t JOIN genisis.WS_Claim c ON c.CASNumber = t.CASNumber AND CAST(c.ClaimVersion AS nvarchar(10)) = t.INVWSversion WHERE t.InvNo IS NOT NULL AND t.CASNumber IS NOT NULL AND t.InvDate >= '20210101' AND t.InvDate < '20210201') x), " +
            "(SELECT SUM(x.M) FROM (SELECT DISTINCT t.CASNumber, t.INVWSversion, c.CLMPayableByMedix2M AS M FROM genisis.WS_InvoiceTracking t JOIN genisis.WS_Claim c ON c.CASNumber = t.CASNumber AND CAST(c.ClaimVersion AS nvarchar(10)) = t.INVWSversion WHERE t.InvNo IS NOT NULL AND t.CASNumber IS NOT NULL AND t.InvDate >= '20210101' AND t.InvDate < '20210201') x) " +
            "FROM genisis.WS_InvoiceTracking WHERE InvNo IS NOT NULL AND CASNumber IS NOT NULL AND InvDate >= '20210101' AND InvDate < '20210201'", transaction: session.Transaction);
        Assert.Equal(expected.Amount, month.InvoiceAmount);
        Assert.Equal(expected.Worksheets, month.TotalCases);
        Assert.Equal(expected.Hospital, month.PayableToHospital);
        Assert.Equal(expected.Member, month.PayableToMember);
        // the desktop's join by case number alone gave about 27 million for the same invoices
        Assert.True(month.InvoiceAmount < 15_000_000m);
    }

    [Fact]
    public async Task The_by_hospital_summaries_add_up_to_the_month_summary()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var total = Assert.Single(await Repository.SummaryAsync(session, January(), 0, Max));
        foreach (var report in new[] { 1, 2, 4 })
        {
            var rows = await Repository.SummaryAsync(session, January(), report, Max);
            Assert.True(rows.Count > 1, $"report {report}");
            Assert.Equal(total.InvoiceAmount, rows.Sum(r => r.InvoiceAmount));
            Assert.All(rows, r => Assert.False(string.IsNullOrWhiteSpace(r.HospitalCode) && r.Hospital is not null));
        }
        var byHospital = await Repository.SummaryAsync(session, January(), 4, Max);
        Assert.Equal(byHospital.Count, byHospital.Select(r => r.HospitalCode).Distinct().Count());
        Assert.All(byHospital, r => Assert.True(r.InvMonth is null && r.InvoiceNo is null));
        var listing = await Repository.SummaryAsync(session, January(), 2, Max);
        Assert.All(listing, r => Assert.NotNull(r.InvoiceDate));
        var byMonthAndHospital = await Repository.SummaryAsync(session, January(), 1, Max);
        Assert.All(byMonthAndHospital, r => Assert.Equal((2021, 1), (r.InvYear, r.InvMonth)));
    }

    [Fact]
    public async Task The_detail_lists_the_invoices_and_the_criteria_narrow_it()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var january = await Repository.DetailAsync(session, January(), Max);
        Assert.InRange(january.Count, 3000, 5000);
        Assert.All(january, r => Assert.InRange(r.InvoiceDate!.Value, new DateTime(2021, 1, 1), new DateTime(2021, 1, 31, 23, 59, 59)));

        var member = await Repository.DetailAsync(session, new InvoiceTrackingFilter { Report = "3", InvoiceDateFrom = new DateTime(2021, 1, 1), InvoiceDateTo = new DateTime(2021, 1, 31), PayTo = "M" }, Max);
        Assert.InRange(member.Count, 1, january.Count - 1);
        var cashless = await Repository.DetailAsync(session, new InvoiceTrackingFilter { Report = "3", InvoiceDateFrom = new DateTime(2021, 1, 1), InvoiceDateTo = new DateTime(2021, 1, 31), PayType = "C" }, Max);
        Assert.All(cashless, r => Assert.Equal("C", r.PayType));
        var followUp = await Repository.DetailAsync(session, new InvoiceTrackingFilter { Report = "3", InvoiceDateFrom = new DateTime(2021, 1, 1), InvoiceDateTo = new DateTime(2021, 1, 31), StayType = "4" }, Max);
        Assert.All(followUp, r => Assert.Contains(r.StayType, new[] { "P", "F" }));
        var inPatient = await Repository.DetailAsync(session, new InvoiceTrackingFilter { Report = "3", InvoiceDateFrom = new DateTime(2021, 1, 1), InvoiceDateTo = new DateTime(2021, 1, 31), StayType = "0" }, Max);
        Assert.All(inPatient, r => Assert.Equal("I", r.StayType));

        var one = january[january.Count / 2];
        var byWorksheet = await Repository.DetailAsync(session, new InvoiceTrackingFilter { Report = "3", WorksheetFrom = one.CaseNo, WorksheetTo = one.CaseNo, VersionFrom = int.Parse(one.Version!), VersionTo = int.Parse(one.Version!) }, Max);
        Assert.All(byWorksheet, r => Assert.Equal((one.CaseNo, one.Version), (r.CaseNo, r.Version)));
        Assert.Contains(byWorksheet, r => r.InvoiceNo == one.InvoiceNo);
        var byAmount = await Repository.DetailAsync(session, new InvoiceTrackingFilter { Report = "3", InvoiceDateFrom = new DateTime(2021, 1, 1), InvoiceDateTo = new DateTime(2021, 1, 31), AmountFrom = 50000 }, Max);
        Assert.All(byAmount, r => Assert.True(r.InvoiceAmount >= 50000));
    }

    [Fact]
    public async Task The_empty_ticks_find_the_rows_that_lack_the_value_including_cases_without_an_invoice()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var noInvoice = await Repository.DetailAsync(session, new InvoiceTrackingFilter { Report = "3", InvoiceNoEmpty = true }, Max);
        Assert.NotEmpty(noInvoice);
        Assert.All(noInvoice, r => Assert.True(string.IsNullOrEmpty(r.InvoiceNo)));
        Assert.Contains(noInvoice, r => r.InvoiceDate is null);
        var noBordx = await Repository.DetailAsync(session, new InvoiceTrackingFilter { Report = "3", BordxEmpty = true, InvoiceDateFrom = new DateTime(2021, 1, 1), InvoiceDateTo = new DateTime(2021, 1, 31) }, Max);
        Assert.All(noBordx, r => Assert.True(string.IsNullOrEmpty(r.BordxNo)));
        var noAmount = await Repository.DetailAsync(session, new InvoiceTrackingFilter { Report = "3", AmountEmpty = true }, Max);
        Assert.All(noAmount, r => Assert.Null(r.InvoiceAmount));
        // the summaries count invoices only, so a case without one is not in them
        var summary = await Repository.SummaryAsync(session, new InvoiceTrackingFilter { Report = "4", InvoiceNoEmpty = true }, 4, Max);
        Assert.All(summary, r => Assert.True(r.TotalCases >= 0));
    }

    [Fact]
    public async Task The_hospital_payor_and_group_company_lists_and_filters_work()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var hospitals = await Repository.HospitalsAsync(session);
        Assert.True(hospitals.Count > 100);
        var some = (await Repository.SummaryAsync(session, January(), 4, Max)).First(r => r.TotalCases >= 3);
        var forHospital = await Repository.SummaryAsync(session, new InvoiceTrackingFilter { Report = "4", InvoiceDateFrom = new DateTime(2021, 1, 1), InvoiceDateTo = new DateTime(2021, 1, 31), HospitalCode = some.HospitalCode }, 4, Max);
        Assert.Equal(some.HospitalCode, Assert.Single(forHospital).HospitalCode);
        Assert.Equal(some.InvoiceAmount, forHospital[0].InvoiceAmount);

        var payors = await Repository.GroupCompaniesAsync(session, "AY");
        Assert.NotEmpty(payors);
        var forGroup = await Repository.DetailAsync(session, new InvoiceTrackingFilter { Report = "3", GroupCompany = payors[0], InvoiceDateFrom = new DateTime(2021, 1, 1), InvoiceDateTo = new DateTime(2021, 1, 31) }, Max);
        Assert.All(forGroup, r => Assert.Equal(payors[0], r.GroupCompany));
        var forPayor = await Repository.DetailAsync(session, new InvoiceTrackingFilter { Report = "3", Payor = "AY", InvoiceDateFrom = new DateTime(2021, 1, 1), InvoiceDateTo = new DateTime(2021, 1, 31) }, Max);
        Assert.NotEmpty(forPayor);
        Assert.All(forPayor, r => Assert.StartsWith("AY", r.CaseNo));
    }
}
