using Dapper;
using Genisis.Api.ClaimBordx.Print;
using Genisis.Api.ClaimBordx.PaymentAdvice;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Tests;

/// <summary>Claim Bordx > Payment Advice > Hospital Report: the validator, the formatting and the service, against a fake repository.</summary>
public class HospitalReportRulesTests
{
    private sealed class FakeRepository : IHospitalReportRepository
    {
        public List<PrintBordxRow> Head { get; set; } = [new() { CaseNo = "MH1", BordxNo = "NMH1", BordxDate = new DateTime(2026, 10, 1), Btbg = "N", TotalMgmtFee = 7 }];

        public Dictionary<string, List<HospitalPaymentRow>> Hospitals { get; } = [];

        public Dictionary<string, List<HospitalClaimRow>> Claims { get; } = [];

        public List<MemberPaymentRow> Members { get; set; } = [];

        public List<RecoveryRow> Recovery { get; set; } = [];

        public List<CostCentreRow> CostCentres { get; set; } = [];

        public List<GroupRow> Groups { get; set; } = [];

        public List<ExpenseRow> Others { get; set; } = [];

        public List<ExpenseRow> Wing { get; set; } = [];

        public Task<List<PrintBordxRow>> SearchAsync(IDbSession session, HospitalReportFilter filter) => Task.FromResult(Head);

        public Task<List<HospitalPaymentRow>> HospitalsAsync(IDbSession session, string bordxNo, string section) => Task.FromResult(Hospitals.GetValueOrDefault(section) ?? []);

        public Task<List<HospitalClaimRow>> ClaimsAsync(IDbSession session, string bordxNo, string section) => Task.FromResult(Claims.GetValueOrDefault(section) ?? []);

        public Task<List<MemberPaymentRow>> MembersAsync(IDbSession session, string bordxNo) => Task.FromResult(Members);

        public Task<List<RecoveryRow>> RecoveryAsync(IDbSession session, string bordxNo) => Task.FromResult(Recovery);

        public Task<List<CostCentreRow>> CostCentresAsync(IDbSession session, HospitalReportFilter filter) => Task.FromResult(CostCentres);

        public Task<List<GroupRow>> GroupsAsync(IDbSession session, HospitalReportFilter filter) => Task.FromResult(Groups);

        public Task<List<ExpenseRow>> ExpensesAsync(IDbSession session, HospitalReportFilter filter, bool maswing) => Task.FromResult(maswing ? Wing : Others);
    }

    private static HospitalReportService Service(FakeRepository repository) => new(new FakeSessions(), repository);

    [Fact]
    public void Both_ends_of_the_bordx_range_are_needed_and_values_are_formatted_as_the_desktop_did()
    {
        Assert.Equal("Please enter Bordx Reference No", new HospitalReportFilterValidator().FirstError(new HospitalReportFilter { BordxFrom = "B1" }));
        Assert.Null(new HospitalReportFilterValidator().FirstError(new HospitalReportFilter { BordxFrom = "B1", BordxTo = "B2" }));
        Assert.Equal(("1,234.50", "0.00", "07/09/2026", "007"), (ReportFormat.Money(1234.5m), ReportFormat.Money(null), ReportFormat.Day(new DateTime(2026, 9, 7)), ReportFormat.Sequence(7)));
        Assert.Equal(("200.00", "-", "", "x"), (ReportFormat.Tax("200"), ReportFormat.Tax(" - "), ReportFormat.Tax(null), ReportFormat.Tax("x")));
    }

    [Fact]
    public async Task Search_totals_with_the_lists_rule_and_nothing_found_is_not_found()
    {
        var repository = new FakeRepository();
        var result = await Service(repository).SearchAsync(new HospitalReportFilter { BordxFrom = "A", BordxTo = "Z" });
        Assert.Equal(7m, result.TotalAmount);
        repository.Head = [];
        var failure = await Assert.ThrowsAsync<NotFoundException>(() => Service(repository).SearchAsync(new HospitalReportFilter { BordxFrom = "A", BordxTo = "Z" }));
        Assert.Equal("No record found ", failure.Message);
    }

    [Fact]
    public async Task A_bordx_is_needed_and_must_exist()
    {
        var repository = new FakeRepository();
        Assert.Equal("Please Select Bordx", (await Assert.ThrowsAsync<BusinessRuleException>(() => Service(repository).PaymentAdviceAsync(" "))).Message);
        repository.Head = [];
        Assert.Equal("Bordx NMH9 not found", (await Assert.ThrowsAsync<NotFoundException>(() => Service(repository).PaymentAdviceAsync("nmh9"))).Message);
    }

    [Fact]
    public async Task The_payment_advice_has_a_summary_and_the_claims_for_each_hospital_set_that_has_some_and_the_member_sets()
    {
        var repository = new FakeRepository();
        repository.Hospitals["panel"] = [new("H1", "HOSPITAL ONE", 2, 300m, 250m), new("H2", "HOSPITAL TWO", 1, 50m, 50m)];
        repository.Claims["panel"] =
        [
            new("H1", "HOSPITAL ONE", "PAYEE ONE", "E1", "A", new DateTime(2026, 9, 1), "INV1", "MH1", 1, 100m, 80m),
            new("H1", "HOSPITAL ONE", "PAYEE ONE", "E2", "B", new DateTime(2026, 9, 2), "INV2", "MH2", 2, 200m, 170m),
            new("H2", "HOSPITAL TWO", "PAYEE TWO", "E3", "C", null, "INV3", "MH3", 1, 50m, 50m),
        ];
        repository.Hospitals["sabah"] = [new("S1", "SABAH HOSPITAL", 1, 10m, 10m)];
        repository.Claims["sabah"] = [new("S1", "SABAH HOSPITAL", null, "E4", "D", null, "INV4", "MH4", 1, 10m, 10m)];
        repository.Members =
        [
            new("E1", "A", "a@x", "IC1", "CC1", "MH1", 1, 40m, "40.00", 40m),
            new("E2", "B", "b@x", "IC2", "CC2", "MH2", 1, 25m, "-", 25m),
            new("E3", "C", "c@x", "IC3", "CC3", "MH3", 2, 10m, "5", 10m),
        ];
        repository.Recovery = [new("E1", "A", "A CHILD", "MH1", 1, "H1", new DateTime(2026, 9, 1), 0m, -50m, "CC1")];

        var document = await Service(repository).PaymentAdviceAsync(" nmh1 ");
        Assert.Equal(("NMH1", new DateTime(2026, 10, 1)), (document.Name, document.Date));
        Assert.Equal(["Payment Advice - Panel Hospitals", "Payment Advice - Panel Hospitals - claims", "Payment Advice - AWUS (Sabah)", "Payment Advice - AWUS (Sabah) - claims", "Payment to Members", "Recovery", "Staff Claim", "Daily Allowance"], document.Sections.Select(s => s.Title));
        var summary = document.Sections[0];
        Assert.Equal(["001", "HOSPITAL ONE", "2", "300.00", "50.00", "250.00"], summary.Rows[0]);
        Assert.Equal(["", "Total", "3", "350.00", "50.00", "300.00"], summary.Rows[2]);
        var claims = document.Sections[1];
        Assert.Equal(["HOSPITAL ONE", "E1", "A", "01/09/2026", "INV1", "MH1-1", "100.00", "20.00", "80.00"], claims.Rows[0]);
        Assert.Equal(["HOSPITAL ONE", "", "", "", "", "Sub Total", "300.00", "50.00", "250.00"], claims.Rows[2]);
        Assert.Equal(["HOSPITAL ONE", "Payable to", "PAYEE ONE", "", "", "", "", "", ""], claims.Rows[3]);
        Assert.Equal(7, claims.Rows.Count);
        Assert.Equal(["E1", "A", "CC1", "IC1", "MH1-1", "40.00", "40.00", "40.00"], document.Sections[4].Rows[0]);
        Assert.Equal(["E1", "A", "A CHILD", "MH1-1", "H1", "01/09/2026", "0.00", "-50.00", "CC1"], document.Sections[5].Rows[0]);
        Assert.Equal(["E2", "E3"], document.Sections[6].Rows.Select(r => r[0]));
        Assert.Equal(["E1", "A", "a@x", "40.00", "MH11", "01/10/2026"], document.Sections[7].Rows[0]);
    }

    [Fact]
    public async Task The_cost_centre_report_writes_the_allocation_with_the_desktops_account_codes_and_the_expenses_with_totals()
    {
        var repository = new FakeRepository
        {
            CostCentres = [new(3, "50HEQ6007", 1200.5m)],
            Groups = [new("MASWING SDN BHD", 99m)],
            Others = [new("CC1", "E1", "A", "IC1", 10m), new("CC1", "E1", "A", "IC1", 5m), new("CC2", "E2", "B", "IC2", 20m)],
            Wing = [new("MASWING SDN BHD", "E9", "W", "ICW", 7m)],
        };
        var document = await Service(repository).CostCentreReportAsync(new HospitalReportFilter { BordxFrom = "A", BordxTo = "Z" });
        var period = $"{DateTime.Today.Month}{DateTime.Today.Year}";
        Assert.Equal(["10HEQ2510", "95010000", "1,200.50", "MYR", "50HEQ6007", "3", "EA", $"Cost Allocation-GHSS {period}"], document.Sections[0].Rows[0]);
        Assert.Equal(["51702", "00", "MASWING", "99.00", $"JXXXXXX INS {period}", "5170200MASWING", "JXXXXXX", $"INS {period}"], document.Sections[1].Rows[0]);
        var expenses = document.Sections[2];
        Assert.Equal(["", "", "", "Sub Total", "15.00"], expenses.Rows[2]);
        Assert.Equal(["MASWING", "E9", "W", "ICW", "7.00"], document.Sections[3].Rows[0]);
        Assert.Equal(["Grand Total", "42.00"], document.Sections[4].Rows[0]);
        repository.CostCentres = [];
        repository.Groups = [];
        repository.Others = [];
        repository.Wing = [];
        await Assert.ThrowsAsync<NotFoundException>(() => Service(repository).CostCentreReportAsync(new HospitalReportFilter()));
    }
}

/// <summary>The report's sets against the database (read only).</summary>
public class HospitalReportDatabaseTests
{
    private static HospitalReportRepository Repository => new();

    private static async Task<int> Count(IDbSession s, string sql, object? args = null) => await s.Connection.ExecuteScalarAsync<int>(sql, args, s.Transaction);

    [Fact]
    public async Task The_search_and_the_hospital_sets_give_what_the_views_hold_for_the_bordx()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var rows = await Repository.SearchAsync(session, new HospitalReportFilter { BordxFrom = "nxl0117395", BordxTo = "NXL0117395" });
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM (SELECT DISTINCT PLNCode, INSCode, CASNumber, ClaimVersion, PatientName, MBMNumber, HOSName, CLMPayableByMedix2H, CLMTotalActual + COALESCE(TotGST, 0) a, CLMTotalApproved, CLMPayableByMedix2M, DebitCreditRefNo, BordxRefNo, BordxPrintedStatus, HLTCode, PAYCode, MBMBTBG, INVPAYTo, CLMCaseMgmtFees, TotalMgmtFee, BordxDate FROM genisis.WS_PrintBordx WHERE ClaimBordxStatus = 1 AND INVBordxed = 1 AND BordxRefNo = 'NXL0117395') x"), rows.Count);
        Assert.NotEmpty(rows);
        Assert.Empty(await Repository.SearchAsync(session, new HospitalReportFilter { BordxFrom = "NXL0117395", BordxTo = "NXL0117395", Hospital = "NO SUCH HOSPITAL" }));
        Assert.Empty(await Repository.SearchAsync(session, new HospitalReportFilter { BordxFrom = "NXL0117395", BordxTo = "NXL0117395", DodFrom = new DateTime(2099, 1, 1) }));

        var panel = await Repository.HospitalsAsync(session, "NMH00105", "panel");
        var sabah = await Repository.HospitalsAsync(session, "NMH00105", "sabah");
        var sarawak = await Repository.HospitalsAsync(session, "NMH00105", "sarawak");
        Assert.NotEmpty(panel);
        Assert.Single(sabah);
        Assert.Equal("SMCSA", sabah[0].HospitalCode);
        Assert.Empty(await Repository.HospitalsAsync(session, "NMH00105", "maswing"));
        Assert.Equal(panel.Sum(h => h.TotalCases), (await Repository.ClaimsAsync(session, "NMH00105", "panel")).Count);
        var sarawakClaims = await Repository.ClaimsAsync(session, "NMH00105", "sarawak");
        Assert.Equal(sarawak.Sum(h => h.TotalCases), sarawakClaims.Count);
        Assert.All(sarawakClaims, c => Assert.False(string.IsNullOrWhiteSpace(c.Payee)));
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM genisis.WS_PaymentAdviceHos WHERE BordxRefNo = 'NMH00105' AND INVPayTo = 'H' AND MBMGroupCategory <> 'MASWING SDN BHD' AND HOSState <> 'Sabah' AND HOSState <> 'Sarawak'"), panel.Sum(h => h.TotalCases));
        Assert.Equal(panel.Select(h => h.Hospital).Order(StringComparer.Ordinal), panel.Select(h => h.Hospital));
    }

    [Fact]
    public async Task The_member_recovery_and_cost_centre_sets_give_what_the_views_hold()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var recovery = await Repository.RecoveryAsync(session, "NMH00105");
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM genisis.WS_MasRecovery WHERE BordxRefNo = 'NMH00105'"), recovery.Count);
        Assert.All(recovery, r => Assert.False(string.IsNullOrWhiteSpace(r.PatientName)));
        var members = await Repository.MembersAsync(session, "NXI0003416");
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM genisis.WS_PaymentAdviceMbm WHERE BordxRefNo = 'NXI0003416'"), members.Count);
        Assert.All(members, m => Assert.True(m.PayableToMember > 0));

        var filter = new HospitalReportFilter { BordxFrom = "NMH00115", BordxTo = "NMH00115" };
        var centres = await Repository.CostCentresAsync(session, filter);
        Assert.NotEmpty(centres);
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM genisis.WS_MasRptOthers WHERE BordxRefNo = 'NMH00115'"), centres.Sum(c => c.TotalCases));
        var expenses = await Repository.ExpensesAsync(session, filter, false);
        Assert.Equal(centres.Sum(c => c.TotalAmount ?? 0), expenses.Sum(e => e.TotalAmount ?? 0));
        Assert.Equal(expenses.Select(e => e.CostCenter).Order(StringComparer.Ordinal), expenses.Select(e => e.CostCenter));
        _ = await Repository.GroupsAsync(session, filter);
        _ = await Repository.ExpensesAsync(session, filter, true);
        Assert.Empty(await Repository.CostCentresAsync(session, new HospitalReportFilter { BordxFrom = "ZZZ1", BordxTo = "ZZZ2" }));
    }
}
