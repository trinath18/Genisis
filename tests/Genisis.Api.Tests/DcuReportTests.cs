using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Dapper;
using Genisis.Api.Authorize.Dcu;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Authorize Claims &gt; DCU Tracking &gt; Claim Report: the catalog, the criteria, the cells of the lists and the service, against a fake repository.</summary>
public partial class DcuReportRulesTests
{
    private static DcuRow Row(params (string Key, object? Value)[] values) => new(values.ToDictionary(v => v.Key, v => v.Value));

    private static DcuReportDefinition Find(string mode, int type, int? selection) => DcuReportCatalog.Instance.Find(mode, type, selection)!;

    private static readonly DcuLookupData NoLookups = new(new Dictionary<string, decimal>(), new Dictionary<string, string>(), new Dictionary<string, (decimal?, decimal?)>());

    [GeneratedRegex(@"^\s*\((\d+), N'", RegexOptions.Multiline)]
    private static partial Regex CaseRow();

    [Fact]
    public void The_catalog_has_the_88_reports_of_the_form_49_for_Claim_and_39_for_Case()
    {
        var reports = DcuReportCatalog.Instance.Reports;
        Assert.Equal((88, 49, 39), (reports.Count, reports.Count(r => r.Mode == "claim"), reports.Count(r => r.Mode == "case")));
        Assert.Equal(reports.Count, reports.Select(r => r.Id).Distinct().Count());
        var types = DcuReportCatalog.Instance.Types();
        Assert.Equal(["General Listing", "Age Band", "Diagnosis", "Hospital", "Month", "Insured Type", "Payor", "Plan No", "State", "Efficiency"], types.Select(t => t.Name));
        Assert.Equal(["Without Remarks"], types[0].Claim);
        Assert.Equal(6, types[1].Claim.Count);
        Assert.Equal(5, types[1].Case!.Count);
        Assert.Equal(12, types[4].Claim.Count);
        Assert.Equal(10, types[4].Case!.Count);
        Assert.Equal([7, 6], [types[3].Claim.Count, types[3].Case!.Count]);
        Assert.Empty(types[6].Claim);
        Assert.Null(types[9].Case);
        Assert.Equal(["Registration", "Bordereaux (Claim)", "Invoice", "Bordereaux (Inv.)", "O/S Bordereaux"], types[9].Claim);
    }

    [Fact]
    public void Every_report_has_headed_columns_and_a_query_that_the_run_procedure_knows()
    {
        var sql = File.ReadAllText(Path.Combine(SqlScripts.Root, "sql", "main", "620_dcu_report.sql"));
        var known = CaseRow().Matches(sql).Select(m => int.Parse(m.Groups[1].Value)).ToHashSet();
        foreach (var report in DcuReportCatalog.Instance.Reports)
        {
            Assert.NotEmpty(report.Columns);
            Assert.All(report.Columns, c => Assert.False(string.IsNullOrWhiteSpace(c.Header), report.Id));
            Assert.All(report.Columns, c => Assert.Contains(c.Kind, new[] { "text", "money", "date", "claimNo", "yearMonth", "ratio", "duration", "icd", "notYetBordx", "bordxed", "efficiency" }));
            if (report.Efficiency is null || report.Efficiency == "OS") Assert.Contains(report.Case, known);
            if (report.DurationCase is { } d) Assert.Contains(d, known);
            if (report.DurationCase is not null) Assert.NotEmpty(report.DurationKeys);
        }
    }

    [Fact]
    public void The_general_listing_has_the_49_columns_of_the_claim_list_and_the_48_of_the_case_list()
    {
        var claim = Find("claim", 0, 0);
        var cases = Find("case", 0, 0);
        Assert.Equal((49, 48), (claim.Columns.Count, cases.Columns.Count));
        Assert.Equal(["Payor", "Claim No", "BordxNo", "Bordx Date", "Member No", "Policy No", "Covered ID", "Prin. Name", "Patient Name"], claim.Columns.Take(9).Select(c => c.Header));
        Assert.Equal("Duration", claim.Columns[35].Header);
        Assert.Equal(["Not Yet Bordx", "Bordx"], cases.Columns.Skip(24).Take(2).Select(c => c.Header));
        Assert.Equal((1, 2), (claim.Case, cases.Case));
        Assert.Null(cases.DurationCase);
    }

    [Fact]
    public void Find_ignores_the_selection_of_a_type_that_has_none_and_refuses_what_does_not_exist()
    {
        Assert.Equal("claim:6:0", DcuReportCatalog.Instance.Find("claim", 6, null)!.Id);
        Assert.Equal("case:8:0", DcuReportCatalog.Instance.Find("case", 8, 3)!.Id);
        Assert.Equal("claim:1:0", DcuReportCatalog.Instance.Find("claim", 1, null)!.Id);
        Assert.Null(DcuReportCatalog.Instance.Find("case", 9, 0));
        Assert.Null(DcuReportCatalog.Instance.Find("claim", 1, 6));
        Assert.Null(DcuReportCatalog.Instance.Find("both", 0, 0));
        Assert.Equal("TotalApproved", Find("claim", 3, 4).OrderBy);
        Assert.Equal("TotalApproved", Find("claim", 3, 5).OrderBy);
        Assert.Null(Find("claim", 3, 3).OrderBy);
    }

    [Fact]
    public void The_criteria_become_an_element_each_and_a_case_report_has_no_bordx_or_version_criteria()
    {
        var criteria = new DcuCriteria { HealthType = " N ", Payor = "AC", CaseStatus = 1, DoaFrom = new DateTime(2021, 1, 5), VersionFrom = 2, BordxNoFrom = "NAC1", BlankPlan = true, BlankBordxNo = true, AgentCode = "a'b<&>" };
        var claim = XElement.Parse(DcuCriteriaXml.Build(criteria, "claim"));
        Assert.Equal("N", (string?)claim.Element("healthType"));
        Assert.Equal(["N", "AC", "1", "2021-01-05", "2", "NAC1", "true", "true", "a'b<&>"], new[] { "healthType", "payor", "caseStatus", "doaFrom", "versionFrom", "bordxNoFrom", "blankPlan", "blankBordxNo", "agentCode" }.Select(n => (string?)claim.Element(n)));
        var caseXml = XElement.Parse(DcuCriteriaXml.Build(criteria, "case"));
        Assert.All(new[] { "versionFrom", "bordxNoFrom", "blankBordxNo" }, n => Assert.Null(caseXml.Element(n)));
        Assert.Equal("true", (string?)caseXml.Element("blankPlan"));
        Assert.Equal("<c><caseStatus>1</caseStatus></c>", DcuCriteriaXml.Build(new DcuCriteria(), "claim"));
        Assert.Equal("<c><caseStatus>3</caseStatus></c>", DcuCriteriaXml.Build(new DcuCriteria { CaseStatus = 3, Payor = " " }, "claim"));
        Assert.Equal("ab", (string?)XElement.Parse(DcuCriteriaXml.Build(new DcuCriteria { AgentCode = "a\u0001b" }, "claim")).Element("agentCode"));
        Assert.Equal("<l><i>A09</i><i>J18.9</i></l>", DcuCriteriaXml.List(["A09", "J18.9"]));
    }

    [Fact]
    public void Search_checks_the_report_and_the_codes()
    {
        var validator = new DcuReportRequestValidator();
        Assert.Equal("Please select a Claim or Case Report.", validator.FirstError(new DcuReportRequest { Mode = null }));
        Assert.Equal("Select a report type and selection.", validator.FirstError(new DcuReportRequest { Mode = "case", ReportType = 9 }));
        Assert.Equal("Select a report type and selection.", validator.FirstError(new DcuReportRequest { ReportType = 1, Selection = 9 }));
        Assert.Null(validator.FirstError(new DcuReportRequest()));
        Assert.Null(validator.FirstError(new DcuReportRequest { ReportType = 6 }));
        Assert.Equal("Select a valid HLT Type.", validator.FirstError(new DcuReportRequest { HealthType = "X" }));
        Assert.Equal("Select a valid Mem Type.", validator.FirstError(new DcuReportRequest { MemberType = "Q" }));
        Assert.Equal("Select a valid Case Status.", validator.FirstError(new DcuReportRequest { CaseStatus = 4 }));
        Assert.Equal("Select a valid Modality Code.", validator.FirstError(new DcuReportRequest { Modality = "Z" }));
        Assert.Equal("Agent Code is too long.", validator.FirstError(new DcuReportRequest { AgentCode = new string('x', 201) }));
        Assert.Null(validator.FirstError(new DcuReportRequest { HealthType = "S", MemberType = "P", SupplementaryType = "C", CaseStatus = 3, Modality = "B", StayType = "I", CashType = "R", DataStatus = "A", Claimability = "R", CasePending = "N", ClaimPending = "P" }));
    }

    [Fact]
    public void A_general_listing_row_has_the_cells_the_desktop_showed()
    {
        var row = Row(("PAYCode", "AC"), ("CASNumber", "AC00000644"), ("ClaimVersion", 2), ("BordxRefNo", null), ("BordxDate", new DateTime(2021, 2, 26)), ("MBMName", "  NAME  "), ("FirstDiag", " J18 "), ("CLMTotalActual", 1234.5m), ("CLMTotalApproved", -3m),
            ("CASReserveAmount", null), ("RecoveryAmt", 5m), ("NoofDay", 0), ("CASDateAdmitted", new DateTime(2021, 2, 1)), ("CASStatus", "C"));
        var report = Find("claim", 0, 0);
        var lookups = NoLookups with { IcdDescriptions = new Dictionary<string, string> { ["J18"] = "Pneumonia" } };
        var cells = DcuReportEngine.Build(report, [row], lookups).Single();
        Assert.Equal(report.Columns.Count, cells.Length);
        Assert.Equal(["AC", "AC00000644-2", "", "26/02/2021"], cells.Take(4));
        Assert.Equal("NAME", cells[7]);
        Assert.Equal(["J18", "Pneumonia"], [cells[20].Trim(), cells[21]]);
        Assert.Equal(["1,234.50", "(3.00)", "", "0.00"], cells.Skip(24).Take(4));
        Assert.Equal(["C", "01/02/2021", "1"], [cells[29], cells[32], cells[35]]);
        Assert.Equal("Empty", DcuReportEngine.Build(report, [Row(("CASNumber", "X"), ("ClaimVersion", 1))], NoLookups).Single()[0]);
        Assert.Equal("5.00", DcuReportEngine.Build(report, [Row(("RecoveryAmt", -5m))], NoLookups).Single()[27].Replace("(", "").Replace(")", ""));
    }

    [Fact]
    public void A_case_listing_shows_what_each_group_of_claims_is_worth_and_the_bordx_amounts_of_the_case()
    {
        var report = Find("case", 0, 0);
        var row = Row(("PAYCode", "AC"), ("CASNumber", "AC1"), ("CLMTotalApproved", 90m), ("NoofDay", 4));
        var lookups = NoLookups with { CaseBordx = new Dictionary<string, (decimal?, decimal?)> { ["AC1"] = (40m, 50m) } };
        var cells = DcuReportEngine.Build(report, [row], lookups).Single();
        Assert.Equal(["40.00", "50.00"], cells.Skip(24).Take(2));
        Assert.Equal("4", cells[35]);
        Assert.Equal(["", ""], DcuReportEngine.Build(report, [row], NoLookups).Single().Skip(24).Take(2));
        Assert.Equal(["AC1"], DcuReportEngine.CaseNumbers(report, [row, row]));
        Assert.Empty(DcuReportEngine.CaseNumbers(Find("claim", 0, 0), [row]));
    }

    [Fact]
    public void A_group_report_shows_the_empty_label_the_ratios_and_the_amounts()
    {
        var report = Find("claim", 3, 3);
        var row = Row(("PAYCode", null), ("HOSName", "  A HOSPITAL "), ("Total", 3), ("NoofDay", 7), ("TotalActual", 100m), ("TotalApproved", 90m));
        Assert.Equal(["Empty", "A HOSPITAL", "3", "7", "100.00", "90.00", "33.00", "30.00"], DcuReportEngine.Build(report, [row], NoLookups).Single());
        var none = Row(("PAYCode", "AC"), ("HOSName", null), ("Total", 0), ("NoofDay", 0), ("TotalActual", null), ("TotalApproved", null));
        Assert.Equal(["AC", "Empty", "0", "1", "", "", "0.00", "0.00"], DcuReportEngine.Build(report, [none], NoLookups).Single());
        Assert.Equal("2.00", DcuReportEngine.Build(report, [Row(("Total", 2), ("TotalActual", 5m), ("TotalApproved", 0m))], NoLookups).Single()[6]);
        Assert.Equal("4.00", DcuReportEngine.Build(report, [Row(("Total", 2), ("TotalActual", 9m), ("TotalApproved", 0m))], NoLookups).Single()[6]);
    }

    [Fact]
    public void A_month_report_labels_the_month_year_slash_month_and_zero_amounts_are_shown_as_0_00()
    {
        var report = Find("claim", 4, 0);
        var cells = DcuReportEngine.Build(report, [Row(("MONTH", 4), ("YEAR", 2021), ("TotalCases", 12), ("NoofDay", 30), ("TotalActual", null), ("TotalApproved", 5m)), Row(("MONTH", 11), ("YEAR", 2020), ("TotalCases", 1), ("NoofDay", 2), ("TotalActual", 1m), ("TotalApproved", 1m)),
            Row(("TotalCases", 1), ("NoofDay", 1), ("TotalActual", 1m), ("TotalApproved", 1m))], NoLookups);
        Assert.Equal(["2021/04", "12", "30", "0.00", "5.00"], cells[0]);
        Assert.Equal("2020/11", cells[1][0]);
        Assert.Equal("/", cells[2][0]);
    }

    [Fact]
    public void A_case_report_takes_the_duration_of_its_group_from_the_duration_case_with_empty_and_missing_keys_summed()
    {
        var report = Find("case", 1, 1);
        Assert.Equal((41, 42), (report.Case, report.DurationCase!.Value));
        Assert.Equal(["Sex", "Ageband"], report.DurationKeys);
        var durations = DcuReportEngine.SumDurations([Row(("Sex", "M"), ("Ageband", "01"), ("NoofDay", 5)), Row(("Sex", null), ("Ageband", "02"), ("NoofDay", 3)), Row(("Sex", ""), ("Ageband", "02"), ("NoofDay", 4)), Row(("Sex", "F"), ("Ageband", "01"), ("NoofDay", 0))], report.DurationKeys);
        var lookups = NoLookups with { Durations = durations };
        string Duration(string? sex, string? age) => DcuReportEngine.Build(report, [Row(("Sex", sex), ("Ageband", age), ("TotalCases", 1), ("TotalActualAmt", 1m), ("TotalApprovedAmt", 1m))], lookups).Single()[3];
        Assert.Equal(["5", "7", "7", "1", ""], [Duration("M", "01"), Duration(null, "02"), Duration("", "02"), Duration("F", "01"), Duration("X", "09")]);
        Assert.Equal(["Sex", "AgeBand"], report.Columns.Take(2).Select(c => c.Field));
    }

    [Fact]
    public void The_icd_columns_name_the_code_they_describe_and_the_codes_are_asked_for_once()
    {
        var report = Find("claim", 2, 0);
        Assert.Equal(("Diag. Desc", "icd", "FinalDiag"), (report.Columns[1].Header, report.Columns[1].Kind, report.Columns[1].Field));
        Assert.Equal(["A09", "B01"], DcuReportEngine.IcdCodes(report, [Row(("FinalDiag", " A09 ")), Row(("FinalDiag", "a09")), Row(("FinalDiag", "B01")), Row(("FinalDiag", null))]));
        Assert.Equal(["J1", "J2"], DcuReportEngine.IcdCodes(Find("claim", 0, 0), [Row(("FirstDiag", "J1"), ("FinalDiag", "J2"))]));
    }

    [Fact]
    public void The_ageing_rows_show_the_total_the_age_bands_and_what_is_outstanding_and_the_outstanding_report_carries_forward()
    {
        var ageing = DcuEfficiency.AgeingRows([Row(("MONTH", 2), ("Total", 84), ("D0to15", 53), ("D15to30", 14), ("D31to60", 8), ("D61to90", 2), ("D90", 7)), Row(("MONTH", null), ("Total", 3), ("D0to15", 0), ("D15to30", 0), ("D31to60", 0), ("D61to90", 0), ("D90", 0))]);
        Assert.Equal(["February", "84", "53", "14", "8", "2", "7", "0"], ageing[0]);
        Assert.Equal(["Empty", "3", "0", "0", "0", "0", "0", "3"], ageing[1]);
        var os = DcuEfficiency.OutstandingRows([Row(("YEAR", 2021), ("MONTH", 2), ("Total", 24)), Row(("YEAR", 2021), ("MONTH", 3), ("Total", 47)), Row(("YEAR", 2021), ("MONTH", 4), ("Total", 10))], [Row(("YEAR", 2021), ("MONTH", 3), ("Total", 40)), Row(("YEAR", 2021), ("MONTH", 2), ("Total", 5))]);
        Assert.Equal(["2021/02", "0", "24", "5", "19"], os[0]);
        Assert.Equal(["2021/03", "19", "47", "40", "26"], os[1]);
        Assert.Equal(["2021/04", "26", "10", "0", "36"], os[2]);
        Assert.Equal(["Registration", "O/S Bordereaux"], new[] { Find("claim", 9, 0).Type is "Efficiency" ? Find("claim", 9, 0).Selection! : "", Find("claim", 9, 4).Selection! });
        Assert.Equal(["DIR Month", "Total", "0-15", "15-30", "31-60", "61-90", ">90", "O/S"], Find("claim", 9, 0).Columns.Select(c => c.Header));
        Assert.Equal(["Reg. Month", "B/F", "Registered", "Bordx", "O/S"], Find("claim", 9, 4).Columns.Select(c => c.Header));
    }

    private sealed class FakeRepository : IDcuReportRepository
    {
        public Dictionary<int, List<DcuRow>> Cases { get; } = [];

        public List<DcuRow> Efficiency { get; set; } = [];

        public List<string> Calls { get; } = [];

        public Task<(List<DcuCode> Ins, List<DcuPayor> Payors, List<DcuCode> PolicyStatuses)> LookupsAsync(IDbSession session) =>
            Task.FromResult((new List<DcuCode> { new("I", "Individual") }, new List<DcuPayor> { new("AC", "ALLIANZ", "N") }, new List<DcuCode> { new("A", "Active") }));

        public Task<List<string>> GroupCompaniesAsync(IDbSession session, string payor) => Task.FromResult(new List<string> { "ACME" });

        public Task<List<DcuRow>> RunAsync(IDbSession session, int caseNo, string criteriaXml, string? orderBy, int max)
        {
            Calls.Add($"run {caseNo} {orderBy} {max} {criteriaXml}");
            return Task.FromResult(Cases.GetValueOrDefault(caseNo) ?? []);
        }

        public Task<List<DcuRow>> EfficiencyAsync(IDbSession session, string kind, string criteriaXml)
        {
            Calls.Add($"efficiency {kind}");
            return Task.FromResult(Efficiency);
        }

        public Task<Dictionary<string, string>> IcdDescriptionsAsync(IDbSession session, IReadOnlyCollection<string> codes)
        {
            Calls.Add("icd " + string.Join(',', codes));
            return Task.FromResult(codes.ToDictionary(c => c, c => "desc of " + c));
        }

        public Task<Dictionary<string, (decimal? NotYetBordx, decimal? Bordxed)>> CaseBordxAsync(IDbSession session, IReadOnlyCollection<string> cases)
        {
            Calls.Add("bordx " + string.Join(',', cases));
            return Task.FromResult(new Dictionary<string, (decimal?, decimal?)>());
        }
    }

    private static DcuReportService Service(FakeRepository repository) => new(new FakeSessions(), repository, NullLogger<DcuReportService>.Instance);

    [Fact]
    public async Task Search_runs_the_query_once_with_the_criteria_and_nothing_found_is_not_found()
    {
        var repository = new FakeRepository();
        var request = new DcuReportRequest { ReportType = 1, Selection = 0, Payor = "AC" };
        Assert.Equal("No record found!", (await Assert.ThrowsAsync<NotFoundException>(() => Service(repository).RunAsync(request, "U"))).Message);
        repository.Cases[37] = [Row(("AgeBand", "01"), ("TotalCases", 5), ("NoofDay", 9), ("TotalActualAmt", 10m), ("TotalApprovedAmt", 8m))];
        var result = await Service(repository).RunAsync(request, "U");
        Assert.Equal("Claim - Age Band - Summary", result.Title);
        Assert.Equal(["Ageband", "Total Claims", "Total Duration", "Total Actual Amt", "Total Approved Amt"], result.Columns.Select(c => c.Header));
        Assert.Equal(["01", "5", "9", "10.00", "8.00"], result.Rows.Single());
        Assert.False(result.Truncated);
        Assert.Contains("run 37  5001 <c><caseStatus>1</caseStatus><payor>AC</payor></c>", repository.Calls);
    }

    [Fact]
    public async Task A_case_report_asks_for_the_duration_case_once_and_a_ranked_list_says_how_it_is_ranked()
    {
        var repository = new FakeRepository();
        repository.Cases[56] = [Row(("PAYCode", "AC"), ("HOSName", "H1"), ("Total", 2), ("TotalActual", 10m), ("TotalApproved", 6m))];
        repository.Cases[62] = [Row(("PAYCode", "AC"), ("HOSName", "H1"), ("NoofDay", 8))];
        var result = await Service(repository).RunAsync(new DcuReportRequest { Mode = "case", ReportType = 3, Selection = 4 }, "U");
        Assert.Equal(["AC", "H1", "2", "8", "10.00", "6.00", "5.00", "3.00"], result.Rows.Single());
        Assert.Equal(["run 56 TotalApproved 5001", "run 62  2147483647"], repository.Calls.Select(c => c[..Math.Min(c.Length, c.IndexOf(" <c>", StringComparison.Ordinal) < 0 ? c.Length : c.IndexOf(" <c>", StringComparison.Ordinal))]).Take(2));
    }

    [Fact]
    public async Task More_than_5000_rows_are_cut_and_the_icd_descriptions_are_asked_for_the_rows_shown()
    {
        var repository = new FakeRepository();
        repository.Cases[71] = Enumerable.Range(0, 5001).Select(i => Row(("FinalDiag", "D" + i), ("Total", 1), ("NoofDay", 1), ("TotalActual", 1m), ("TotalApproved", 1m))).ToList();
        var result = await Service(repository).RunAsync(new DcuReportRequest { ReportType = 2, Selection = 0 }, "U");
        Assert.True(result.Truncated);
        Assert.Equal(5000, result.Rows.Count);
        Assert.Equal("desc of D0", result.Rows[0][1]);
        Assert.Equal(5000, repository.Calls.Single(c => c.StartsWith("icd ", StringComparison.Ordinal)).Split(',').Length);
    }

    [Fact]
    public async Task The_efficiency_reports_read_the_ageing_counts_and_the_outstanding_report_reads_the_registered_and_bordxed_months()
    {
        var repository = new FakeRepository { Efficiency = [Row(("MONTH", 1), ("Total", 10), ("D0to15", 4), ("D15to30", 0), ("D31to60", 0), ("D61to90", 0), ("D90", 1))] };
        var result = await Service(repository).RunAsync(new DcuReportRequest { ReportType = 9, Selection = 2 }, "U");
        Assert.Equal(["January", "10", "4", "0", "0", "0", "1", "5"], result.Rows.Single());
        Assert.Equal("efficiency INV", repository.Calls.Single());
        var os = new FakeRepository { Efficiency = [Row(("YEAR", 2021), ("MONTH", 3), ("Total", 4))] };
        os.Cases[120] = [Row(("YEAR", 2021), ("MONTH", 3), ("Total", 9))];
        Assert.Equal(["2021/03", "0", "9", "4", "5"], (await Service(os).RunAsync(new DcuReportRequest { ReportType = 9, Selection = 4 }, "U")).Rows.Single());
        Assert.Contains("efficiency OSBORDX", os.Calls);
    }

    [Fact]
    public async Task The_lookups_list_the_codes_and_the_report_types_and_the_group_companies_follow_the_payor()
    {
        var repository = new FakeRepository();
        var lookups = await Service(repository).LookupsAsync();
        Assert.Equal(("I", "AC", "N", 10), (lookups.InsTypes[0].Code, lookups.Payors[0].Code, lookups.Payors[0].HealthType, lookups.ReportTypes.Count));
        Assert.Equal(["ACME"], await Service(repository).GroupCompaniesAsync(" AC "));
        Assert.Empty(await Service(repository).GroupCompaniesAsync(" "));
    }
}

/// <summary>The DCU report against the database: every report runs in both modes, the copied queries give what the legacy procedure gives, and the criteria are quoted.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class DcuReportDatabaseTests
{
    private const string Criteria = "<c><payor>AC</payor><caseStatus>1</caseStatus><dorFrom>2021-02-01</dorFrom><dorTo>2021-03-31</dorTo></c>";

    /// <summary>Cases whose rows differ from the legacy procedure's on purpose: the ranked lists (the legacy Top N had no ORDER BY), the duration cases that did not select their payor, and case 7, which the legacy procedure ran without the criteria.</summary>
    private static readonly HashSet<int> Changed = [7, 25, 26, 28, 29, 55, 56, 57, 58, 71, 72, 73, 74, 75, 76, 89, 95, 101, 107, 113];

    [Fact]
    public async Task Every_report_of_the_catalog_runs_in_both_modes_and_fills_every_column()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        var service = new DcuReportService(TestDb.Sessions(), new DcuReportRepository(), NullLogger<DcuReportService>.Instance);
        var ran = 0;
        foreach (var report in DcuReportCatalog.Instance.Reports)
        {
            var request = new DcuReportRequest { Mode = report.Mode, ReportType = report.TypeIndex, Selection = report.SelectionIndex, Payor = "AC", DorFrom = new DateTime(2021, 2, 1), DorTo = new DateTime(2021, 3, 31) };
            try
            {
                var result = await service.RunAsync(request, "E2E");
                Assert.All(result.Rows, r => Assert.Equal(report.Columns.Count, r.Length));
                Assert.Equal(report.Columns.Select(c => c.Header), result.Columns.Select(c => c.Header));
                ran++;
            }
            catch (NotFoundException)
            {
                ran++;
            }
        }

        Assert.Equal(88, ran);
    }

    private static async Task<List<string>> Rows(IDbSession s, string sql, object? args = null)
    {
        await using var reader = await s.Connection.ExecuteReaderAsync(new CommandDefinition(sql, args, s.Transaction, commandTimeout: 300));
        var rows = new List<string>();
        while (await reader.ReadAsync())
            rows.Add(string.Join("|", Enumerable.Range(0, reader.FieldCount).OrderBy(i => reader.GetName(i), StringComparer.OrdinalIgnoreCase).Select(i => reader.GetName(i).ToLowerInvariant() + "=" + Convert.ToString(reader.IsDBNull(i) ? null : reader.GetValue(i), CultureInfo.InvariantCulture))));
        rows.Sort(StringComparer.Ordinal);
        return rows;
    }

    [Fact]
    public async Task The_copied_queries_give_the_rows_the_legacy_procedure_gives()
    {
        if (TestDb.MainDb is null || TestDb.WorkSheet is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var cases = DcuReportCatalog.Instance.Reports.Where(r => r.Efficiency is null || r.Efficiency == "OS").SelectMany(r => new[] { r.Case, r.DurationCase ?? 0 }).Where(c => c != 0 && !Changed.Contains(c) && c != 120).Distinct().Order().ToList();
        Assert.True(cases.Count > 60);
        var where = await session.Connection.ExecuteScalarAsync<string>("SELECT genisis.DcuReport_Where(@c)", new { c = Criteria }, session.Transaction);
        foreach (var caseNo in cases)
        {
            var legacy = await Rows(session, "EXEC HISWorkSheet.dbo.DCUReport @strAppend, @strAppendCase", new { strAppend = where, strAppendCase = caseNo });
            var mine = await Rows(session, "EXEC genisis.DcuReport_Run @case, @criteria, NULL, 2147483647", new { @case = caseNo, criteria = Criteria });
            Assert.True(legacy.SequenceEqual(mine), $"case {caseNo}: legacy {legacy.Count} rows, new {mine.Count} rows");
        }
    }

    [Fact]
    public async Task A_ranked_list_is_the_highest_of_its_measure_and_every_criterion_is_quoted()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var repository = new DcuReportRepository();
        var byClaims = await repository.RunAsync(session, 55, "<c/>", null, 100);
        Assert.Equal(20, byClaims.Count);
        Assert.Equal(byClaims.Select(r => r.Number("Total")).OrderByDescending(n => n), byClaims.Select(r => r.Number("Total")));
        var byAmount = await repository.RunAsync(session, 55, "<c/>", "TotalApproved", 100);
        Assert.Equal(byAmount.Select(r => r.Number("TotalApproved")).OrderByDescending(n => n), byAmount.Select(r => r.Number("TotalApproved")));

        var before = await session.Connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM genisis.WS_Claim", transaction: session.Transaction);
        foreach (var hostile in new[] { "x'; DROP TABLE dbo.PAY;--", "' OR 1=1 --", "a%' UNION SELECT 1,2,3 --" })
        {
            var xml = DcuCriteriaXml.Build(new DcuCriteria { AgentCode = hostile, PolicyNo = hostile, GroupCompany = hostile, FinalDiagFrom = hostile, CaseNoFrom = hostile }, "claim");
            Assert.Empty(await repository.RunAsync(session, 1, xml, null, 10));
        }

        Assert.Equal(before, await session.Connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM genisis.WS_Claim", transaction: session.Transaction));
        Assert.True(await session.Connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM dbo.PAY", transaction: session.Transaction) > 0);
    }

    [Fact]
    public async Task The_lookups_the_efficiency_counts_and_the_case_bordx_amounts_come_from_the_database()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var repository = new DcuReportRepository();
        var (ins, payors, statuses) = await repository.LookupsAsync(session);
        Assert.True(ins.Count > 0 && statuses.Count > 0);
        Assert.Contains(payors, p => p.Code == "AC" && !string.IsNullOrEmpty(p.HealthType));
        Assert.NotEmpty(await repository.GroupCompaniesAsync(session, "AC"));
        var icd = await repository.IcdDescriptionsAsync(session, ["A09", "NO-SUCH-ICD"]);
        Assert.Equal(["A09"], icd.Keys);
        foreach (var kind in new[] { "DIR", "BORDX", "INV", "BORDXINV" })
        {
            var rows = await repository.EfficiencyAsync(session, kind, "<c><payor>AC</payor></c>");
            Assert.NotEmpty(rows);
            Assert.All(rows, r => Assert.True(r.Number("Total") > 0 && r.Number("MONTH") is >= 1 and <= 12));
        }

        var bordx = await repository.CaseBordxAsync(session, ["AC00000644", "NO-SUCH-CASE"]);
        Assert.Equal(["AC00000644"], bordx.Keys);
        Assert.Equal(16246.3m, bordx["AC00000644"].Bordxed);
        Assert.Null(bordx["AC00000644"].NotYetBordx);
    }
}
