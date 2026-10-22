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

    private static PlanTerms RoomTerms(string family = "M", string payorRoomTax = "N", string? majorMed = null, DateTime? date = null) => new()
    {
        CoPaymentInd = "Y", RoomCoPayPercent = 20, PlanFamily = family, PayorRoomTax = payorRoomTax, MajorMedInd = majorMed, MriPercent = 50, LastInvoiceDate = date ?? new DateTime(2013, 11, 8),
    };

    /// <summary>Room and Board approved 600 of 900 charged, other rows 2,500 and a Room and Board tax row of 30: an approved total of 3,130 (plus any change).</summary>
    private static WorksheetEvaluation Room(decimal other = 2500m, decimal boardActual = 900m, string? limit = "600.00") =>
        new([new WorksheetLine(1, boardActual, limit, 600m, 0, 0), new WorksheetLine(3, other, null, other, 0, 0), new WorksheetLine(5, 400m, null, 400m, 0, 0), new WorksheetLine(27, 30m, null, 30m, 0, 0)],
            new WorksheetTotals(boardActual + other + 430m, 600m + other + 430m, 0, 0));

    [Fact]
    public void The_room_and_board_co_payment_of_a_payor_without_the_room_tax_leaves_the_tax_out_of_the_base()
    {
        // total 3,530 (600 + 2,500 + 400 MRI + 30 tax); base = total - tax - room and board = 2,900; 20 per cent = 580
        Assert.Equal(580.00m, CoPaymentRules.RoomAndBoard(Room(), RoomTerms()));
    }

    [Fact]
    public void The_branch_of_a_payor_with_the_room_tax_leaves_the_tax_in_unless_the_plan_is_major_medical()
    {
        Assert.Equal(586.00m, CoPaymentRules.RoomAndBoard(Room(), RoomTerms(payorRoomTax: "Y")));
        Assert.Equal(580.00m, CoPaymentRules.RoomAndBoard(Room(), RoomTerms(payorRoomTax: "Y", majorMed: "Y")));
    }

    [Fact]
    public void Families_2_and_3_and_new_sm_plans_take_the_mri_share_off_and_not_the_tax()
    {
        // base = 3,530 - 600 - 400 x 50 per cent = 2,730; 20 per cent = 546
        Assert.Equal(546.00m, CoPaymentRules.RoomAndBoard(Room(), RoomTerms(family: "2")));
        Assert.Equal(546.00m, CoPaymentRules.RoomAndBoard(Room(), RoomTerms() with { NewSmPlan = "Y" }));
    }

    [Fact]
    public void The_room_and_board_co_payment_is_rounded_to_5_cents_from_april_2008_only()
    {
        var grid = Room(other: 2503.33m);
        Assert.Equal(580.65m, CoPaymentRules.RoomAndBoard(grid, RoomTerms()));
        Assert.Equal(580.67m, CoPaymentRules.RoomAndBoard(grid, RoomTerms(date: new DateTime(2008, 3, 31))));
        Assert.Equal(580.65m, CoPaymentRules.RoomAndBoard(grid, RoomTerms(date: new DateTime(2008, 4, 1))));
    }

    [Fact]
    public void There_is_no_room_and_board_co_payment_when_the_room_is_within_the_limit_the_indicator_is_off_or_the_limit_is_full()
    {
        Assert.Equal(0m, CoPaymentRules.RoomAndBoard(Room(boardActual: 600m), RoomTerms()));
        Assert.Equal(0m, CoPaymentRules.RoomAndBoard(Room(), RoomTerms() with { CoPaymentInd = "N" }));
        Assert.Equal(0m, CoPaymentRules.RoomAndBoard(Room(), RoomTerms() with { RoomCoPayPercent = 0 }));
        Assert.Equal(0m, CoPaymentRules.RoomAndBoard(Room(limit: "FULL"), RoomTerms(payorRoomTax: "Y")));
        Assert.Equal(0m, CoPaymentRules.RoomAndBoard(Room(), null));
    }

    [Fact]
    public void The_members_share_beats_the_room_and_board_co_payment_which_beats_the_co_insurance()
    {
        var terms = RoomTerms() with { CoInsurancePercent = 10, CoInsuranceSign = "Y" };
        Assert.Equal(580.00m, CoPaymentRules.CoPayment(Room(), terms));
        Assert.Equal(353.00m, CoPaymentRules.CoPayment(Room(boardActual: 600m), terms));
        Assert.Equal(( 3530m - 600m - 30m) * 0.2m, CoPaymentRules.CoPayment(Room(boardActual: 600m), terms with { MemberCoPayPercent = 20, MemberCoPayInd = "Y" }));
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
    /// Two directions, over about a sixth of the versions that qualify: of the versions with a saved co-payment, how many does the plan's percentage reproduce (78 per cent in the larger data with the member's own share, the Room & Board co-payment and the co-insurance, 86 per cent accepting a payor's other Room & Board tax setting; the rest are mostly the ex-gratia row and approved totals the engine gets wrong);
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
        var bySource = new Dictionary<string, (int N, int Ok)>();
        int savedLenient = 0, derivedLenient = 0;
        var wrong = new List<string>();
        var terms = new PlanTermsRepository();
        foreach (var v in sample)
        {
            var grid = await engine.EvaluateSavedAsync(v.Cas, v.Version);
            if (grid is null) continue;
            var derived = await service.CoPaymentAsync(v.Cas, v.Version, grid);
            await using var termsRead = await TestDb.Sessions().OpenAsync();
            var planTerms = await new PlanTermsRepository().TermsAsync(termsRead, v.Cas, v.Version);
            // a payor's Room & Board tax setting has changed over the years (XL: Y until about 2012, N now) and only today's is stored, so a worksheet made under the other setting differs by the tax;
            // the lenient count accepts the other branch's amount too
            var other = planTerms is null ? 0m : CoPaymentRules.RoomAndBoard(grid, planTerms with { PayorRoomTax = string.Equals(planTerms.PayorRoomTax?.Trim(), "Y", StringComparison.OrdinalIgnoreCase) ? "N" : "Y" });
            bool Lenient() => Math.Abs(derived - v.Saved) < 0.015m || (other != 0m && !CoPaymentRules.HasMemberShare(planTerms) && Math.Abs(other - v.Saved) < 0.015m);
            if (v.Saved != 0) { savedN++; savedOk += Math.Abs(derived - v.Saved) < 0.015m ? 1 : 0; savedLenient += Lenient() ? 1 : 0; }
            if (derived != 0)
            {
                derivedN++;
                derivedOk += Math.Abs(derived - v.Saved) < 0.015m ? 1 : 0;
                derivedLenient += Lenient() ? 1 : 0;
                await using var read = await TestDb.Sessions().OpenAsync();
                var t = await terms.TermsAsync(read, v.Cas, v.Version);
                var source = CoPaymentRules.HasMemberShare(t) ? "member share" : CoPaymentRules.RoomAndBoard(grid, t) != 0 ? "room and board" : "co-insurance";
                var ok = Math.Abs(derived - v.Saved) < 0.015m;
                bySource[source] = (bySource.GetValueOrDefault(source).N + 1, bySource.GetValueOrDefault(source).Ok + (ok ? 1 : 0));
                if (!ok && source == "room and board" && wrong.Count < 12) wrong.Add($"{v.Cas} v{v.Version} {t?.PlanFamily} {t?.PayorRoomTax} {t?.RoomCoPayPercent}%: saved {v.Saved} derived {derived}");
            }
        }
        foreach (var (source, (n, ok)) in bySource) output.WriteLine($"  {source}: {ok} of {n} equal the saved amount");
        foreach (var line in wrong) output.WriteLine("  wrong room and board: " + line);
        output.WriteLine($"{sample.Count} versions: of {savedN} with a saved co-payment {savedOk} reproduced ({savedLenient} when the payor's other Room & Board tax setting is accepted); of {derivedN} the plan gives one for, {derivedOk} equal the saved amount ({derivedLenient} accepting it)");
        // with the member's own share (payor TK), the Room & Board co-payment and the plan's co-insurance: 80 per cent of those given are right and 86 per cent of the saved co-payments are reproduced
        // (78 per cent strictly); the rest are mostly the co-insurance that goes to the ex-gratia row, not ported, and approved totals the engine gets wrong
        Assert.True(derivedN >= 50 && derivedLenient >= derivedN * 0.75, $"derived co-payments that equal the saved one: {derivedLenient} of {derivedN}");
        Assert.True(savedN >= 50 && savedLenient >= savedN * 0.80, $"saved co-payments reproduced: {savedLenient} of {savedN}");
    }
}
