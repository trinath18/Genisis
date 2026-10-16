using Genisis.Api.Tracking;
using Microsoft.Extensions.Configuration;

namespace Genisis.Api.Tests;

public class TrackingTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");

    [Fact]
    public void Catalog_matches_the_desktop_report_lists()
    {
        Assert.Equal(192, TrackingCaseKeys.All.Count);
        foreach (var report in TrackingCatalog.Reports)
        {
            Assert.True(TrackingCaseKeys.All.ContainsKey(report.PrincipalCase), report.Key);
            Assert.True(TrackingCaseKeys.All.ContainsKey(report.SupplementaryCase), report.Key);
        }
        // 22 report types; the selections listed by CboReportType_Click add up to 83 reports.
        Assert.Equal(83, TrackingCatalog.Reports.Count);
        Assert.Equal(22, TrackingCatalog.Reports.Select(r => r.TypeIndex).Distinct().Count());
        Assert.Equal(TrackingCatalog.Reports.Count, TrackingCatalog.Reports.Select(r => r.Key).Distinct().Count());
    }

    [Fact]
    public void Validator_types_the_filters_and_rejects_bad_input()
    {
        var error = TrackingValidator.Validate(new TrackingRequest
        {
            Report = "ins.payor",
            MemberType = "Both",
            PrincipalType = "C - Combined Limit",
            Values = { ["payor"] = " XI ", ["insuredGroup"] = "6", ["sex"] = "" },
            Ranges = { ["effDate"] = new() { From = "2021-01-01", To = "31/12/2021" }, ["premium"] = new() { From = "10.5" } },
            Blanks = ["groupCompany", "groupCompany"],
        }, out var query);

        Assert.Null(error);
        Assert.Equal((MemberTypeSelection.Both, PrincipalTypeSelection.CombinedLimit), (query!.MemberType, query.PrincipalType));
        Assert.Equal(new Dictionary<string, string> { ["payor"] = "XI", ["insuredGroup"] = "6" }, query.Values);
        Assert.Equal((new DateTime(2021, 1, 1), new DateTime(2021, 12, 31)), ((DateTime)query.Ranges["effDate"].From!, (DateTime)query.Ranges["effDate"].To!));
        Assert.Equal(10.5m, query.Ranges["premium"].From);
        Assert.Equal(["groupCompany"], query.Blanks);

        string? Message(TrackingRequest r) => TrackingValidator.Validate(r, out _);
        Assert.Equal("Please select a report type.", Message(new TrackingRequest { Report = "nope" }));
        Assert.Equal("MBM Type must be Both, P or S.", Message(new TrackingRequest { Report = "payor", MemberType = "X" }));
        Assert.Equal("'yesterday' is not a valid value for Date of Birth.",
            Message(new TrackingRequest { Report = "payor", Ranges = { ["dob"] = new() { From = "yesterday" } } }));
        Assert.Equal("'9' is not a valid Insured Type.", Message(new TrackingRequest { Report = "payor", Values = { ["insuredGroup"] = "9" } }));
        Assert.Equal("Unknown filter 'drop table'.", Message(new TrackingRequest { Report = "payor", Blanks = ["drop table"] }));
        Assert.Equal("Unknown filter 'memberTypeBlank'.", Message(new TrackingRequest { Report = "payor", Values = { ["memberTypeBlank"] = "P" } }));
        Assert.Equal("Unknown filter 'policyNo'.", Message(new TrackingRequest { Report = "payor", Values = { ["policyNo"] = "1" } }));
    }

    /// <summary>In-memory repository: returns canned rows per case and records the options of each call.</summary>
    private sealed class FakeRepository(Dictionary<int, List<Dictionary<string, object?>>> rows) : ITrackingRepository
    {
        public List<(int Case, TrackingCaseOptions Options)> Calls { get; } = [];

        public Task<TrackingLookups> GetLookupsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new TrackingLookups([new("XI", "XI INSURANCE", "N")], [new("SELANGOR", "SELANGOR")], [new("ML", "MEDICALIFE")]));

        public Task<List<Dictionary<string, object?>>> RunCaseAsync(TrackingQuery query, int caseNumber, TrackingCaseOptions options, CancellationToken cancellationToken)
        {
            Calls.Add((caseNumber, options));
            var copy = rows.GetValueOrDefault(caseNumber, []).Select(r => new Dictionary<string, object?>(r, StringComparer.OrdinalIgnoreCase)).ToList();
            return Task.FromResult(copy);
        }
    }

    private static Dictionary<string, object?> Row(params (string Key, object? Value)[] values) =>
        values.ToDictionary(v => v.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);

    private static TrackingQuery Query(string report, string memberType, string principalType = PrincipalTypeSelection.Both)
    {
        Assert.Null(TrackingValidator.Validate(new TrackingRequest { Report = report, MemberType = memberType, PrincipalType = principalType }, out var query));
        return query!;
    }

    [Fact]
    public async Task Joined_report_adds_supplementary_totals_by_matching_keys()
    {
        // Ageband by payor (Prin.): principal case 11 groups by AGECode, supplementary case 12 by the principal's PrinAGECode.
        var repository = new FakeRepository(new()
        {
            [11] = [Row(("PAYCode", "XI"), ("HLTCode", "N"), ("INSCode", "F"), ("AGECode", "02"), ("TotalP", 3), ("MCOGross", 30m), ("PremiumGross", 300m), ("EAFee", 1m)),
                    Row(("PAYCode", "XI"), ("HLTCode", "N"), ("INSCode", "F"), ("AGECode", null), ("TotalP", 1), ("MCOGross", 10m), ("PremiumGross", 100m))],
            [12] = [Row(("PAYCode", "XI"), ("HLTCode", "N"), ("INSCode", "F"), ("PrinAGECode", "02"), ("TotalS", 2), ("MCOGross", 4m), ("PremiumGross", 40m)),
                    Row(("PAYCode", "XI"), ("HLTCode", "N"), ("INSCode", "F"), ("PrinAGECode", ""), ("TotalS", 5), ("MCOGross", 5m), ("PremiumGross", 50m))],
        });
        var result = await new TrackingService(repository).RunAsync(Query("ageband.payor-prin", MemberTypeSelection.Both), 100, default);

        Assert.Equal(2m, result.Rows[0][TrackingColumns.SupplementaryCount]);
        Assert.Equal(40m, result.Rows[0][TrackingColumns.SupplementaryPremium]);
        Assert.Equal(5m, result.Rows[1][TrackingColumns.SupplementaryCount]);
        Assert.Equal(new TrackingTotals(4, 7, 11, 49, 490, 1), result.Totals);
        Assert.Equal([11, 12], repository.Calls.Select(c => c.Case));

        var principalsOnly = await new TrackingService(repository).RunAsync(Query("ageband.payor-prin", MemberTypeSelection.Principal), 100, default);
        Assert.DoesNotContain(TrackingColumns.SupplementaryCount, principalsOnly.Columns);
    }

    [Fact]
    public async Task Detail_listing_applies_member_type_filters_and_truncates_rows_not_totals()
    {
        var repository = new FakeRepository(new()
        {
            [88] = Enumerable.Range(1, 3).Select(i => Row(("MBMNumber", $"XI{i}"))).ToList(),
            [89] = [Row(("MBMNumber", "XI1"))],
        });
        var result = await new TrackingService(repository).RunAsync(
            Query("general.listing", MemberTypeSelection.Both, PrincipalTypeSelection.SeparateLimit), rowLimit: 2, default);

        Assert.True(result.Truncated);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal((3m, 1m), (result.Totals.Principals, result.Totals.Supplementaries));
        Assert.Equal([(88, new TrackingCaseOptions(MemberTypeFilters.DetailSeparate)),
            (89, new TrackingCaseOptions(MemberTypeFilters.DetailSeparate, WithSupplementaryRegistrationDate: true))], repository.Calls);
        Assert.Equal(TrackingRowTypes.Principal, result.Rows[0][TrackingColumns.RowType]);

        repository.Calls.Clear();
        await new TrackingService(repository).RunAsync(Query("general.prin-name", MemberTypeSelection.Principal, PrincipalTypeSelection.CombinedLimit), 10, default);
        Assert.Equal(MemberTypeFilters.PrincipalName("P", "C"), repository.Calls.Single().Options.MemberTypeFilter);
    }

    [Fact]
    public async Task Count_columns_use_the_insured_type_filters()
    {
        var repository = new FakeRepository(new()
        {
            [37] = [Row(("PAYCode", "XI"), ("MBMBordxDate", new DateTime(2021, 5, 1)), ("TotalP", 4))],
            [39] = [Row(("PAYCode", "XI"), ("MBMBordxDate", new DateTime(2021, 5, 1)), ("TotalIC", 2), ("MCOFEE", 20m))],
        });
        var result = await new TrackingService(repository).RunAsync(Query(TrackingReportKeys.BordxMco, MemberTypeSelection.Principal), 10, default);

        Assert.Equal((2m, 20m), (result.Rows[0]["Ind Child Count"], result.Rows[0]["Ind Child Amount"]));
        Assert.Equal(0m, result.Rows[0]["Family Supp Count"]);
        Assert.DoesNotContain(repository.Calls, c => c.Case is 45 or 46);
        Assert.Contains((39, new TrackingCaseOptions(CountFilter: TrackingCountFilters.IndividualChild)), repository.Calls);
    }

    [Fact]
    public void Csv_export_quotes_cells_and_neutralises_formulas()
    {
        var result = new TrackingResult(["Name", "Amount"],
            [Row(("Name", "=HYPERLINK(\"x\")"), ("Amount", 12.5m)), Row(("Name", "A, B"), ("Amount", null))], new TrackingTotals(0, 0, 0, 0, 0, 0), false);
        var text = System.Text.Encoding.UTF8.GetString(TrackingCsvWriter.Write(result)).TrimStart('\uFEFF');
        Assert.Equal("Name,Amount\r\n\"'=HYPERLINK(\"\"x\"\")\",12.5\r\n\"A, B\",\r\n", text.Replace(Environment.NewLine, "\r\n"));
        Assert.Equal("Tracking-INS-Type-MBM-Type-By-Payor.csv", TrackingCsvWriter.FileName(TrackingCatalog.Find("ins.payor")!));
    }

    /// <summary>Runs every report in all three member types through genisis.Tracking_Run (narrowed to one payor and year).</summary>
    [Fact]
    public async Task Every_report_runs_against_the_database()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var db = new Data.DbConnections(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:MainDb"] = MainDb }).Build());
        var service = new TrackingService(new TrackingRepository(db));
        var failures = new List<string>();
        var withRows = 0;
        foreach (var report in TrackingCatalog.Reports)
            foreach (var memberType in new[] { MemberTypeSelection.Principal, MemberTypeSelection.Both, MemberTypeSelection.Supplementary })
            {
                var request = new TrackingRequest
                {
                    Report = report.Key, MemberType = memberType,
                    Values = { ["payor"] = "XI", ["health"] = "N" },
                    Ranges = { ["bordxDate"] = new() { From = "2021-01-01", To = "2021-12-31" } },
                };
                try
                {
                    Assert.Null(TrackingValidator.Validate(request, out var query));
                    if ((await service.RunAsync(query!, 50, default)).Rows.Count > 0) withRows++;
                }
                catch (Exception ex)
                {
                    failures.Add($"{report.Key} [{memberType}]: {ex.Message}");
                }
            }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
        Assert.True(withRows > 100, $"only {withRows} report runs returned rows");
    }
}
