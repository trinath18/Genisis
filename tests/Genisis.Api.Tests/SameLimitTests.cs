using Dapper;
using Genisis.Api.Worksheet.Engine;
using Xunit.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>What is left of a case's limits after a worksheet (saveSameLimit) as pure rules.</summary>
public class SameLimitRulesTests
{
    [Theory]
    [InlineData("FULL", "500.00", "FULL")]
    [InlineData("750.00", "-", "750.00")]
    [InlineData("750", "-", "750.00")]
    [InlineData("750.00", "250.00", "500.00")]
    [InlineData("1,000.00", "250.50", "749.50")]
    [InlineData("100.00", "300.00", "-200.00")]
    [InlineData("-", "300.00", "0")]
    [InlineData("-", "-", "-")]
    [InlineData("-", "0", "0")]
    [InlineData("%", "300.00", "0")]
    [InlineData("50%", "300.00", "50%")]
    [InlineData("0", "30.00", "-30.00")]
    [InlineData("0", "-", "0.00")]
    [InlineData("0", "0", "0.00")]
    public void A_limit_that_is_used_is_reduced_by_what_was_approved_as_the_desktop_compares_the_two_cells(string limit, string approvedCell, string expected) => Assert.Equal(expected, SameLimitRow.Remaining(limit, approvedCell));

    [Fact]
    public void A_case_without_a_row_has_what_the_plan_allows_less_what_the_worksheet_used()
    {
        var left = SameLimitRow.NextDays(null, new WorksheetRows.LimitDays(180m, 30m, 180m, 12m, 150m, 90m), new WorksheetRows.LimitDays(3m, 0m, 0m, 0m, 0m, 0m));
        Assert.Equal(new WorksheetRows.LimitDays(177m, 30m, 180m, 12m, 150m, 90m), left);
    }

    [Fact]
    public void A_case_with_a_row_keeps_what_it_had_left_less_what_the_worksheet_used_and_never_goes_below_none()
    {
        var allowed = new WorksheetRows.LimitDays(180m, 30m, 180m, 12m, 150m, 90m);
        var previous = new WorksheetRows.LimitDays(177m, 5m, 10m, 0m, 150m, 90m);
        var left = SameLimitRow.NextDays(previous, allowed, new WorksheetRows.LimitDays(7m, 5m, 11m, 2m, 0m, 0m));
        Assert.Equal(new WorksheetRows.LimitDays(170m, 0m, 0m, 0m, 150m, 90m), left);
    }

    private static WorksheetLine Line(int slot, string? limit, decimal approved) => new(slot, approved, limit, approved, approved, 0m);

    [Fact]
    public void The_rows_that_are_used_up_hold_what_is_left_and_the_others_the_limit_as_shown()
    {
        var grid = new WorksheetEvaluation([Line(1, "750.00", 250m), Line(3, "FULL", 100m), Line(4, "800.00", 300m), Line(14, "500.00", 200m), Line(16, null, 0m)], new WorksheetTotals(0m, 0m, 0m, 0m));
        var rows = SameLimitRow.Limits(grid, flat: false);
        Assert.Equal(("750.00", "FULL", "500.00", "500.00", "-"), (rows["CLMBoardAmt"], rows["CLMServices"], rows["CLMTheatre"], rows["CLMAccPostAmt"], rows["CLMOutpatient"]));
        Assert.False(rows.ContainsKey("CLMHomeNCAmt"));
    }

    [Fact]
    public void The_post_hospitalisation_row_is_what_is_left_of_its_limit_or_zero_when_nothing_was_approved()
    {
        Assert.Equal("700.00", SameLimitRow.Limits(new WorksheetEvaluation([Line(12, "1,000.00", 300m)], new WorksheetTotals(0m, 0m, 0m, 0m)), false)["CLMPost"]);
        Assert.Equal("0.00", SameLimitRow.Limits(new WorksheetEvaluation([Line(12, "1,000.00", 0m)], new WorksheetTotals(0m, 0m, 0m, 0m)), false)["CLMPost"]);
        Assert.Equal("0.00", SameLimitRow.Limits(new WorksheetEvaluation([Line(12, "FULL", 300m)], new WorksheetTotals(0m, 0m, 0m, 0m)), false)["CLMPost"]);
    }
}

/// <summary>The limits of the row against the saved same-limit rows.</summary>
public class SameLimitReferenceTests(ITestOutputHelper output)
{
    /// <summary>
    /// The days that are left, worked out across the versions of each case of a sample (cases whose first closed version was opened since July 2020, one in ten, with at most eight closed versions): the first version
    /// starts from what the plan allows, each next one from what was left, using the days each version's limit row says it used; the result is compared with the saved row of the case.
    /// </summary>
    [Fact]
    public async Task The_days_left_worked_out_across_the_versions_of_a_case_equal_the_saved_same_limit_row()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var cases = (await session.Connection.QueryAsync<string>(
            """
            SELECT RTRIM(s.CASNumber) FROM genisis.WS_SameLimit s
            WHERE ABS(CHECKSUM(s.CASNumber)) % 10 = 0
              AND (SELECT MIN(c.ClaimOpenDate) FROM genisis.WS_Claim c WHERE c.CASNumber = s.CASNumber AND c.ClaimVersion <> 0 AND c.ClaimStatus = 'C') >= '2020-07-01'
              AND (SELECT COUNT(*) FROM genisis.WS_Claim c WHERE c.CASNumber = s.CASNumber AND c.ClaimVersion <> 0 AND c.ClaimStatus = 'C') BETWEEN 1 AND 8
              AND (SELECT MIN(c.ClaimVersion) FROM genisis.WS_Claim c WHERE c.CASNumber = s.CASNumber AND c.ClaimVersion <> 0 AND c.ClaimStatus = 'C') = 1
              AND NOT EXISTS (SELECT 1 FROM genisis.WS_Claim c WHERE c.CASNumber = s.CASNumber AND c.ClaimVersion <> 0 AND c.ClaimStatus = 'C' AND NOT EXISTS (SELECT 1 FROM genisis.WS_ClaimLimit l WHERE l.CASNumber = c.CASNumber AND l.CLMVersion = c.ClaimVersion))
            """, transaction: session.Transaction, commandTimeout: 300)).ToList();
        Assert.True(cases.Count >= 300, $"only {cases.Count} cases in the sample");

        var plans = new PlanBenefitRepository();
        string[] names = ["CLMBoardDays", "CLMICUDays", "CLMINHOSPhyDays", "CLMKIDDCDays", "CLMCashDays", "CLMGurdianDays"];
        var same = new int[6];
        int done = 0, allSame = 0;
        var misses = new List<string>();
        foreach (var cas in cases)
        {
            var versions = (await session.Connection.QueryAsync<int>("SELECT ClaimVersion FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion <> 0 AND ClaimStatus = 'C' ORDER BY ClaimVersion", new { cas }, session.Transaction)).ToList();
            var benefits = await plans.BenefitsAsync(session, cas, versions[0]);
            if (benefits.Count == 0) continue;
            var allowed = new WorksheetRows.LimitDays(PlanDays(benefits, "101"), PlanDays(benefits, "102"), PlanDays(benefits, "302"), PlanDays(benefits, "404"), PlanDays(benefits, "601"), PlanDays(benefits, "701"));
            WorksheetRows.LimitDays? left = null;
            foreach (var version in versions)
            {
                var limit = (IDictionary<string, object?>)await session.Connection.QueryFirstAsync("SELECT * FROM genisis.WS_ClaimLimit WHERE CASNumber = @cas AND CLMVersion = @version", new { cas, version }, session.Transaction);
                var used = new WorksheetRows.LimitDays(Days(limit, names[0]), Days(limit, names[1]), Days(limit, names[2]), Days(limit, names[3]), Days(limit, names[4]), Days(limit, names[5]));
                left = SameLimitRow.NextDays(left, allowed, used);
            }
            if (await session.Connection.QueryFirstOrDefaultAsync("SELECT * FROM genisis.WS_SameLimit WHERE CASNumber = @cas", new { cas }, session.Transaction) is not IDictionary<string, object?> saved) continue;
            done++;
            decimal[] built = [left!.Board, left.Icu, left.PhysicianVisits, left.KidneyMonths, left.Cash, left.Guardian];
            var all = true;
            for (var i = 0; i < 6; i++)
            {
                var savedDays = Days(saved, names[i]);
                if (savedDays == built[i]) same[i]++;
                else
                {
                    all = false;
                    if (misses.Count < 8) misses.Add($"{cas} {names[i]}: saved {savedDays} built {built[i]} ({versions.Count} versions)");
                }
            }
            if (all) allSame++;
        }
        for (var i = 0; i < 6; i++) output.WriteLine($"{names[i]}: {same[i]} of {done} equal ({100.0 * same[i] / done:F1}%)");
        foreach (var line in misses) output.WriteLine("  " + line);
        output.WriteLine($"cases: {done}; all six days equal: {allSame}");
        Assert.True(done >= 300, $"only {done} cases");
        for (var i = 0; i < 6; i++) Assert.True(same[i] >= done * 0.80, $"{names[i]}: {same[i]} of {done}");
    }

    private static decimal PlanDays(IEnumerable<PlanBenefit> benefits, string code) => SavedVersion.Number(benefits.FirstOrDefault((b) => b.Code.Trim() == code)?.Days);

    private static decimal Days(IDictionary<string, object?> row, string column) => SavedVersion.Number(new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase).GetValueOrDefault(column));

    /// <summary>
    /// For each case of a sample (about one in ten of those whose last closed version was opened since July 2020 and that has a same-limit row) the limits are worked out from the last version's saved grid (its limit
    /// texts and approved amounts) and compared with the saved row, column by column. The days are not here: they depend on every worksheet of the case.
    /// </summary>
    [Fact]
    public async Task The_limits_worked_out_from_the_last_versions_grid_equal_the_saved_same_limit_row()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var sample = (await session.Connection.QueryAsync<(string Cas, int Version, string? Family, string? Payor)>(
            """
            SELECT RTRIM(s.CASNumber), c.ClaimVersion, LEFT(RTRIM(CASE WHEN v.MBMPatientCovID <> N'00' AND ISNULL(RTRIM(v.MBMCPLNCode), N'') <> N'' THEN v.MBMCPLNCode ELSE v.PLNCode END), 1), LEFT(RTRIM(v.PAYCode), 2)
            FROM genisis.WS_SameLimit s
            CROSS APPLY (SELECT TOP (1) c.ClaimVersion, c.ClaimOpenDate FROM genisis.WS_Claim c WHERE c.CASNumber = s.CASNumber AND c.ClaimVersion <> 0 AND c.ClaimStatus = 'C' ORDER BY c.ClaimVersion DESC) c
            JOIN dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v ON v.CASNumber = s.CASNumber AND v.ClaimVersion = c.ClaimVersion
            WHERE c.ClaimOpenDate >= '2020-07-01' AND ABS(CHECKSUM(s.CASNumber)) % 10 = 0
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimApprove p WHERE p.CASNumber = s.CASNumber AND p.CLMVersion = c.ClaimVersion)
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimHos h WHERE h.CASNumber = s.CASNumber AND h.CLMVersion = c.ClaimVersion)
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimMember m WHERE m.CASNumber = s.CASNumber AND m.CLMVersion = c.ClaimVersion)
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimLimit l WHERE l.CASNumber = s.CASNumber AND l.CLMVersion = c.ClaimVersion)
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimActual a WHERE a.CASNumber = s.CASNumber AND a.CLMVersion = c.ClaimVersion)
            """, transaction: session.Transaction, commandTimeout: 300)).ToList();
        Assert.True(sample.Count >= 300, $"only {sample.Count} cases in the sample");

        var seen = new Dictionary<string, int>();
        var same = new Dictionary<string, int>();
        var exact = new Dictionary<string, int>();
        var misses = new Dictionary<string, List<string>>();
        int done = 0, rowsSame = 0;
        foreach (var (cas, version, family, payor) in sample)
        {
            var saved = await SavedVersion.ReadAsync(session, cas, version, family, payor, null);
            if (saved is null || await session.Connection.QueryFirstOrDefaultAsync("SELECT * FROM genisis.WS_SameLimit WHERE CASNumber = @cas", new { cas }, session.Transaction) is not IDictionary<string, object?> raw) continue;
            var row = new Dictionary<string, object?>(raw, StringComparer.OrdinalIgnoreCase);
            done++;
            var all = true;
            foreach (var (column, text) in SameLimitRow.Limits(saved.Grid, saved.Flat))
            {
                if (!row.ContainsKey(column)) continue;
                seen[column] = seen.GetValueOrDefault(column) + 1;
                var savedText = SavedVersion.Text(row[column]);
                if (savedText == text) exact[column] = exact.GetValueOrDefault(column) + 1;
                if (PlanLimitRules.SameEffect(text, savedText)) same[column] = same.GetValueOrDefault(column) + 1;
                else
                {
                    all = false;
                    if ((misses.TryGetValue(column, out var list) ? list : misses[column] = []).Count < 3) misses[column].Add($"{cas} v{version}: saved '{savedText}' built '{text}'");
                }
            }
            if (all) rowsSame++;
        }
        foreach (var (column, n) in seen.OrderBy((c) => same.GetValueOrDefault(c.Key) * 1.0 / c.Value)) output.WriteLine($"{column}: {same.GetValueOrDefault(column)} of {n} with the same effect ({100.0 * same.GetValueOrDefault(column) / n:F1}%), {exact.GetValueOrDefault(column)} the same text");
        foreach (var (column, list) in misses) foreach (var line in list) output.WriteLine($"  {column} {line}");
        output.WriteLine($"cases: {done}; rows with every limit equal: {rowsSame}");
        Assert.True(done >= 300, $"only {done} cases");
        foreach (var (column, n) in seen) Assert.True(same.GetValueOrDefault(column) >= n * 0.85, $"{column}: {same.GetValueOrDefault(column)} of {n}");
    }
}
