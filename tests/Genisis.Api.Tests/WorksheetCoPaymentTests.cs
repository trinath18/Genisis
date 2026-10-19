using Dapper;
using Genisis.Api.Worksheet.Engine;
using Xunit.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Worksheet calculation engine, step 5: the co-payment.</summary>
public class WorksheetCoPaymentRulesTests
{
    private static PlanTerms Terms(decimal percent, string? sign = "Y") => new() { CoInsurancePercent = percent, CoInsuranceSign = sign };

    [Theory]
    [InlineData(11385.10, 10, 1138.51)]
    [InlineData(299.35, 10, 29.94)]
    [InlineData(1000, 25, 250.00)]
    [InlineData(-299.35, 10, -29.94)]
    [InlineData(0, 10, 0)]
    public void The_co_insurance_is_the_plans_percentage_of_the_approved_total_to_the_cent(double total, double percent, double expected) =>
        Assert.Equal((decimal)expected, CoPaymentRules.CoInsurance((decimal)total, Terms((decimal)percent)));

    [Fact]
    public void A_half_cent_rounds_away_from_zero() => Assert.Equal(0.13m, CoPaymentRules.CoInsurance(2.5m, Terms(5)));

    [Theory]
    [InlineData(0, "Y")]
    [InlineData(10, "N")]
    [InlineData(10, null)]
    [InlineData(10, "")]
    public void There_is_no_co_insurance_without_a_percentage_or_with_the_sign_off(double percent, string? sign) =>
        Assert.Equal(0m, CoPaymentRules.CoInsurance(1000m, Terms((decimal)percent, sign)));

    [Fact]
    public void A_plan_without_a_row_has_none() => Assert.Equal(0m, CoPaymentRules.CoInsurance(1000m, null));

    [Fact]
    public void The_sign_is_read_without_regard_to_case_or_padding() => Assert.Equal(100m, CoPaymentRules.CoInsurance(1000m, Terms(10, " y ")));
}

/// <summary>The co-insurance derived from the plan, against the co-payment the desktop saved.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class WorksheetCoPaymentOracleTests(ITestOutputHelper output)
{
    /// <summary>
    /// Two directions, over about a quarter of the versions that qualify: of the versions with a saved co-payment, how many does the plan's percentage reproduce (about 93 per cent over all of them);
    /// and of the versions whose plan has a percentage and an approved total, how many have that saved (about 89 per cent). The approved total is the engine's own, from the invoices and the
    /// saved limit text.
    /// </summary>
    [Fact]
    public async Task The_plans_co_insurance_reproduces_the_co_payment_the_desktop_saved()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var sample = (await session.Connection.QueryAsync<(string Cas, int Version, decimal Saved)>(
            """
            SELECT RTRIM(w.CASNumber), w.ClaimVersion, ISNULL(w.CLMCoPayRB, 0) FROM genisis.WS_Claim w
            WHERE w.CASNumber NOT LIKE 'ZZ%' AND ABS(CHECKSUM(w.CASNumber, w.ClaimVersion)) % 4 = 0
              AND (ISNULL(w.CLMCoPayRB, 0) <> 0 OR EXISTS (SELECT 1 FROM dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v JOIN dbo.PLN p ON p.PLNCode = RTRIM(CASE WHEN v.MBMPatientCovID <> N'00' AND ISNULL(RTRIM(v.MBMCPLNCode), N'') <> N'' THEN v.MBMCPLNCode ELSE v.PLNCode END) AND p.PAYCode = v.PAYCode AND p.HLTCode = v.HLTCode
                    WHERE v.CASNumber = w.CASNumber AND v.ClaimVersion = w.ClaimVersion AND v.MBMNumber IS NOT NULL AND p.PLNCoINSPerc > 0))
            """, transaction: session.Transaction, commandTimeout: 300)).ToList();

        var engine = new WorksheetEngineService(TestDb.Sessions(), new WorksheetEngineRepository());
        var service = new CoPaymentService(TestDb.Sessions(), new PlanTermsRepository());
        int savedN = 0, savedOk = 0, derivedN = 0, derivedOk = 0;
        foreach (var v in sample)
        {
            var grid = await engine.EvaluateSavedAsync(v.Cas, v.Version);
            if (grid is null) continue;
            var derived = await service.CoInsuranceAsync(v.Cas, v.Version, grid.Totals.Approved);
            if (v.Saved != 0) { savedN++; savedOk += Math.Abs(derived - v.Saved) < 0.015m ? 1 : 0; }
            if (derived != 0) { derivedN++; derivedOk += Math.Abs(derived - v.Saved) < 0.015m ? 1 : 0; }
        }
        output.WriteLine($"{sample.Count} versions: of {savedN} with a saved co-payment {savedOk} reproduced; of {derivedN} the plan gives one for, {derivedOk} equal the saved amount");
        Assert.True(savedN >= 80 && savedOk >= savedN * 0.85, $"saved co-payments reproduced: {savedOk} of {savedN}");
        Assert.True(derivedN >= 80 && derivedOk >= derivedN * 0.80, $"derived co-payments that equal the saved one: {derivedOk} of {derivedN}");
    }
}
