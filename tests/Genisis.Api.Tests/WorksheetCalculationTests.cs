using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Worksheet.Calculation;
using Genisis.Api.Worksheet.Engine;

namespace Genisis.Api.Tests;

/// <summary>The notes the calculation screen shows where its figures cannot be trusted.</summary>
public class WorksheetCalculationRulesTests
{
    private static CalculationLine Line(string benefit, decimal actual, string? limit) => new() { Benefit = benefit, Actual = actual, Limit = limit };

    [Fact]
    public void A_plan_without_rows_says_so_and_nothing_else()
    {
        var notes = CalculationRules.Notes([Line("Room & Board", 100, null)], planHasRows: false);
        Assert.Single(notes);
        Assert.Contains("no benefit rows", notes[0]);
    }

    [Fact]
    public void A_row_with_an_amount_and_no_limit_is_named()
    {
        var notes = CalculationRules.Notes([Line("Ambulance", 80, null), Line("Surgical", 500, "FULL"), Line("Medical Report", 0, null), Line("Others", 20, " ")], planHasRows: true);
        Assert.Single(notes);
        Assert.Contains("Ambulance, Others", notes[0]);
        Assert.DoesNotContain("Surgical", notes[0]);
        Assert.DoesNotContain("Medical Report", notes[0]);
    }

    [Fact]
    public void Nothing_to_say_when_every_row_with_an_amount_has_a_limit() => Assert.Empty(CalculationRules.Notes([Line("Surgical", 500, "FULL"), Line("Ambulance", 0, null)], planHasRows: true));
}

/// <summary>The calculation service against the database.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class WorksheetCalculationDatabaseTests
{
    private static CalculationService Service() => new(TestDb.Sessions(), new CalculationRepository(), new WorksheetEngineRepository(),
        new WorksheetCalculator(TestDb.Sessions(), new WorksheetEngineRepository(), new PlanBenefitRepository(), new PlanTermsRepository(), new BalanceLimitRepository(), new StayDaysRepository()));

    [Fact]
    public async Task A_saved_worksheet_is_calculated_with_captions_the_saved_limits_and_the_saved_summary()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var cas = await session.Connection.ExecuteScalarAsync<string>(
            """
            SELECT TOP (1) RTRIM(w.CASNumber) FROM genisis.WS_Claim w JOIN dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v ON v.CASNumber = w.CASNumber AND v.ClaimVersion = w.ClaimVersion
            WHERE w.CASNumber NOT LIKE 'ZZ%' AND v.MBMNumber IS NOT NULL AND ISNULL(w.CLMTotalApproved, 0) > 0 ORDER BY w.CASNumber
            """, transaction: session.Transaction);
        Assert.NotNull(cas);

        var result = await Service().GetAsync(cas!, null);
        Assert.Equal(cas, result.CaseNo);
        Assert.Contains(result.Version, result.Versions);
        Assert.Equal(result.Versions.Max(), result.Version);
        Assert.Equal(Enumerable.Range(1, 29), result.Lines.Select(l => l.Slot));
        Assert.All(result.Lines, l => Assert.False(string.IsNullOrWhiteSpace(l.Benefit)));
        Assert.Equal("Room & Board", result.Lines[0].Benefit, ignoreCase: true);
        Assert.NotNull(result.Calculated);
        Assert.NotNull(result.Saved);
        Assert.Equal(result.Lines.Sum(l => l.Approved), result.Calculated!.SumApproved);
        Assert.Contains(result.Lines, l => l.SavedLimit is not null);

        var first = await Service().GetAsync(cas!, result.Versions.Min());
        Assert.Equal(result.Versions.Min(), first.Version);
    }

    [Fact]
    public async Task An_unknown_case_or_version_is_record_not_found()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => Service().GetAsync("NO SUCH CASE", null));
        Assert.Equal("Record not found!", ex.Message);
        await using var session = await TestDb.Sessions().OpenAsync();
        var cas = await session.Connection.ExecuteScalarAsync<string>("SELECT TOP (1) RTRIM(CASNumber) FROM genisis.WS_Claim WHERE CASNumber NOT LIKE 'ZZ%'", transaction: session.Transaction);
        await Assert.ThrowsAsync<NotFoundException>(() => Service().GetAsync(cas!, 9999));
    }
}
