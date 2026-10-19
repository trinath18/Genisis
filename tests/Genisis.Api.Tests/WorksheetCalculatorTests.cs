using Dapper;
using Genisis.Api.Worksheet.Engine;
using Xunit.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Worksheet calculation engine, step 6: a worksheet worked out from its invoices and the plan alone, against what the desktop saved.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class WorksheetCalculatorOracleTests(ITestOutputHelper output)
{
    private sealed record Saved(string Cas, int Version, decimal TotalApproved, decimal LimitExceeded, decimal CoPayment);

    private static WorksheetCalculator Calculator() => new(TestDb.Sessions(), new WorksheetEngineRepository(), new PlanBenefitRepository(), new PlanTermsRepository(), new BalanceLimitRepository(), new StayDaysRepository());

    /// <summary>
    /// For the latest version of the latest case of each patient (so the available limit is still the one the claim drew on), about one in thirty-six: the worksheet is worked out from the invoices,
    /// the plan's benefit rows, its co-insurance and the available limit, with nothing from the saved worksheet (the days of the stay come from the invoices). Two figures are compared with the saved ones to the cent:
    /// the approved total before the available limit comes into it (the saved total approved less the limit exceeded), which does not depend on the limit, and the total approved itself. Measured
    /// over 1,130 versions: 68.8 and 70.5 per cent. The breakdown the test prints shows why: the co-payment is right for 99.5 per cent, the approved total follows when the limits are right (778 of 781),
    /// and the limits are the whole gap: 69 per cent of the versions get the same approved sum from the plan as from the saved limits, the rest mostly where the plan has no row for a benefit the
    /// desktop shows as FULL (payor AY's M plans).
    /// </summary>
    [Fact]
    public async Task A_worksheet_worked_out_from_the_invoices_and_the_plan_reproduces_the_saved_totals()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var sample = (await session.Connection.QueryAsync<Saved>(
            """
            SELECT RTRIM(w.CASNumber) AS Cas, w.ClaimVersion AS Version, ISNULL(w.CLMTotalApproved, 0) AS TotalApproved, ISNULL(w.CLMLimitExceeded, 0) AS LimitExceeded, ISNULL(w.CLMCoPayRB, 0) AS CoPayment
            FROM genisis.WS_Claim w
            JOIN dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v ON v.CASNumber = w.CASNumber AND v.ClaimVersion = w.ClaimVersion
            WHERE w.CASNumber NOT LIKE 'ZZ%' AND v.MBMNumber IS NOT NULL AND w.CLMPreviousBalance IS NOT NULL AND v.CASDateAdmitted IS NOT NULL
              AND ABS(CHECKSUM(w.CASNumber, w.ClaimVersion)) % 36 = 0
              AND w.ClaimVersion = (SELECT MAX(x.ClaimVersion) FROM genisis.WS_Claim x WHERE x.CASNumber = w.CASNumber)
              AND NOT EXISTS (SELECT 1 FROM dbo.CAS c2 WHERE c2.MBMNumber = v.MBMNumber AND c2.MBMPatientCovID = v.MBMPatientCovID AND c2.CASDateAdmitted > v.CASDateAdmitted)
            """, transaction: session.Transaction, commandTimeout: 300)).ToList();
        Assert.True(sample.Count >= 200, $"only {sample.Count} versions in the sample");

        var calculator = Calculator();
        var perSlot = new SortedDictionary<int, int>();
        var examples = new Dictionary<string, int>();
        var engineService = new WorksheetEngineService(TestDb.Sessions(), new WorksheetEngineRepository());
        int done = 0, beforeLimitSame = 0, totalSame = 0, workedHard = 0, limitsOk = 0, limitsOkCoPayOk = 0, limitsOkCoPayOkTotalOk = 0, coPayOk = 0;
        foreach (var v in sample)
        {
            var result = await calculator.CalculateAsync(v.Cas, v.Version, v.TotalApproved);
            if (result is null) continue;
            done++;
            var derivedBeforeLimit = result.Grid.Totals.Approved - result.CoPayment;
            var savedBeforeLimit = v.TotalApproved - v.LimitExceeded;
            if (Math.Abs(derivedBeforeLimit - savedBeforeLimit) < 0.015m) beforeLimitSame++;
            if (Math.Abs(result.Final.TotalApproved - v.TotalApproved) < 0.015m) totalSame++;
            if (v.TotalApproved != 0) workedHard++;
            var withSavedLimits = await engineService.EvaluateSavedAsync(v.Cas, v.Version);
            var sameLimits = Math.Abs(withSavedLimits.Totals.Approved - result.Grid.Totals.Approved) < 0.015m;
            var sameCoPay = Math.Abs(result.CoPayment - v.CoPayment) < 0.015m;
            if (!sameLimits)
                foreach (var line in result.Grid.Lines)
                {
                    var savedLine = withSavedLimits.Lines.First(l => l.Slot == line.Slot);
                    if (Math.Abs(savedLine.Approved - line.Approved) < 0.015m) continue;
                    perSlot[line.Slot] = perSlot.GetValueOrDefault(line.Slot) + 1;
                    var key = $"slot {line.Slot}: plan gave [{line.Limit ?? "none"}], saved [{savedLine.Limit ?? "none"}], actual {(line.Actual > 0 ? ">0" : line.Actual < 0 ? "<0" : "0")}";
                    examples[key] = examples.GetValueOrDefault(key) + 1;
                }
            limitsOk += sameLimits ? 1 : 0;
            coPayOk += sameCoPay ? 1 : 0;
            limitsOkCoPayOk += sameLimits && sameCoPay ? 1 : 0;
            limitsOkCoPayOkTotalOk += sameLimits && sameCoPay && Math.Abs(derivedBeforeLimit - savedBeforeLimit) < 0.015m ? 1 : 0;
        }
        output.WriteLine($"{sample.Count} versions, {done} calculated: approved total before the limit reproduced {beforeLimitSame} ({100.0 * beforeLimitSame / Math.Max(done, 1):0.0}%), total approved {totalSame} ({100.0 * totalSame / Math.Max(done, 1):0.0}%); {workedHard} with a non-zero saved total");
        output.WriteLine($"of {done}: the plan limits give the same approved sum as the saved limits for {limitsOk}; the co-payment equals the saved one for {coPayOk}; both for {limitsOkCoPayOk}, and of those the approved total before the limit equals the saved one for {limitsOkCoPayOkTotalOk}");
        output.WriteLine("rows that differ, by slot: " + string.Join(", ", perSlot.OrderByDescending(p => p.Value).Take(10).Select(p => $"{p.Key}={p.Value}")));
        foreach (var e in examples.OrderByDescending(e => e.Value).Take(14)) output.WriteLine($"  {e.Value,4}x {e.Key}");
        Assert.True(done >= sample.Count * 0.9, $"calculated {done} of {sample.Count}");
        Assert.True(beforeLimitSame >= done * 0.62, $"approved total before the limit: {beforeLimitSame} of {done}");
        Assert.True(totalSame >= done * 0.62, $"total approved: {totalSame} of {done}");
    }
}
