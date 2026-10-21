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

    private static PlanTerms Member(decimal percent, string? ind = "Y") => new() { MemberCoPayPercent = percent, MemberCoPayInd = ind };

    [Theory]
    [InlineData(1000, 100, 20, 20, 176.00)]
    [InlineData(500, 0, 0, 20, 100.00)]
    [InlineData(333.33, 100.11, 5.5, 20, 45.54)]
    public void The_members_own_share_is_their_percentage_of_the_total_less_room_and_board_and_taxes(double total, double board, double taxes, double percent, double expected) =>
        Assert.Equal((decimal)expected, CoPaymentRules.MemberShare((decimal)total, (decimal)board, (decimal)taxes, Member((decimal)percent)));

    [Fact]
    public void The_members_share_needs_the_indicator_and_a_percentage()
    {
        Assert.Equal(0m, CoPaymentRules.MemberShare(1000, 0, 0, Member(20, "N")));
        Assert.Equal(0m, CoPaymentRules.MemberShare(1000, 0, 0, Member(0)));
        Assert.False(CoPaymentRules.HasMemberShare(null));
        Assert.True(CoPaymentRules.HasMemberShare(Member(20, " y ")));
    }

    [Fact]
    public void The_members_share_comes_before_the_plans_co_insurance_and_the_plans_stands_without_it()
    {
        var grid = new WorksheetEvaluation([new WorksheetLine(1, 100, null, 100, 0, 0), new WorksheetLine(3, 900, null, 900, 0, 0), new WorksheetLine(27, 10, null, 10, 0, 0)], new WorksheetTotals(1010, 1010, 0, 0));
        var both = new PlanTerms { CoInsurancePercent = 10, CoInsuranceSign = "Y", MemberCoPayPercent = 20, MemberCoPayInd = "Y" };
        Assert.Equal(180m, CoPaymentRules.CoPayment(grid, both));
        Assert.Equal(101m, CoPaymentRules.CoPayment(grid, both with { MemberCoPayInd = "N" }));
        Assert.Equal(0m, CoPaymentRules.CoPayment(grid, null));
    }

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
    /// Two directions, over about a sixth of the versions that qualify: of the versions with a saved co-payment, how many does the plan's percentage reproduce (71 per cent in the larger data with the member's own share and the co-insurance; the rest are the Room & Board co-payment and the ex-gratia row);
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
            WHERE w.CASNumber NOT LIKE 'ZZ%' AND ABS(CHECKSUM(w.CASNumber, w.ClaimVersion)) % 6 = 0
              AND (ISNULL(w.CLMCoPayRB, 0) <> 0 OR EXISTS (SELECT 1 FROM dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v2 JOIN dbo.MBM m2 ON m2.MBMNumber = v2.MBMNumber WHERE v2.CASNumber = w.CASNumber AND v2.ClaimVersion = w.ClaimVersion AND m2.MBMTopupCopay > 0) OR EXISTS (SELECT 1 FROM dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v JOIN dbo.PLN p ON p.PLNCode = RTRIM(CASE WHEN v.MBMPatientCovID <> N'00' AND ISNULL(RTRIM(v.MBMCPLNCode), N'') <> N'' THEN v.MBMCPLNCode ELSE v.PLNCode END) AND p.PAYCode = v.PAYCode AND p.HLTCode = v.HLTCode
                    WHERE v.CASNumber = w.CASNumber AND v.ClaimVersion = w.ClaimVersion AND v.MBMNumber IS NOT NULL AND p.PLNCoINSPerc > 0))
            """, transaction: session.Transaction, commandTimeout: 300)).ToList();

        var engine = new WorksheetEngineService(TestDb.Sessions(), new WorksheetEngineRepository());
        var service = new CoPaymentService(TestDb.Sessions(), new PlanTermsRepository());
        int savedN = 0, savedOk = 0, derivedN = 0, derivedOk = 0;
        foreach (var v in sample)
        {
            var grid = await engine.EvaluateSavedAsync(v.Cas, v.Version);
            if (grid is null) continue;
            var derived = await service.CoPaymentAsync(v.Cas, v.Version, grid);
            if (v.Saved != 0) { savedN++; savedOk += Math.Abs(derived - v.Saved) < 0.015m ? 1 : 0; }
            if (derived != 0) { derivedN++; derivedOk += Math.Abs(derived - v.Saved) < 0.015m ? 1 : 0; }
        }
        output.WriteLine($"{sample.Count} versions: of {savedN} with a saved co-payment {savedOk} reproduced; of {derivedN} the plan gives one for, {derivedOk} equal the saved amount");
        // with the member's own share (payor TK) and the plan's co-insurance: 80 per cent of those given are right and 71 per cent of the saved co-payments are reproduced; the rest are the Room & Board
        // co-payment and the co-insurance that goes to the ex-gratia row, not ported
        Assert.True(derivedN >= 50 && derivedOk >= derivedN * 0.75, $"derived co-payments that equal the saved one: {derivedOk} of {derivedN}");
        Assert.True(savedN >= 50 && savedOk >= savedN * 0.65, $"saved co-payments reproduced: {savedOk} of {savedN}");
    }
}
