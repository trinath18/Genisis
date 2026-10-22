using Dapper;
using Genisis.Api.Worksheet.Engine;
using Xunit.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Worksheet calculation engine, step 4: the limits the plan fixes.</summary>
public class WorksheetPlanLimitsRulesTests
{
    private static PlanBenefit B(string code, string? limit = null, string? days = null, string? claimable = null) => new() { Code = code, Limit = limit, Days = days, Claimable = claimable };

    [Fact]
    public void Payor_TKs_non_panel_hospital_has_a_full_room_and_board_limit_whatever_the_plans_rate()
    {
        var plan = new[] { B("101", "0", "120", "250"), B("103", "FULL") };
        var amounts = new HashSet<int> { 1 };
        Assert.Equal("FULL", PlanLimitRules.Derive(plan, 3, 0, amounts, "TK", "NP")[1]);
        Assert.Equal("FULL", PlanLimitRules.Derive(plan, 3, 0, amounts, " tk ", " np ")[1]);
        Assert.Equal("750.00", PlanLimitRules.Derive(plan, 3, 0, amounts, "TK", "ISLAM")[1]);
        Assert.Equal("750.00", PlanLimitRules.Derive(plan, 3, 0, amounts, "XL", "NP")[1]);
        Assert.Equal("750.00", PlanLimitRules.Derive(plan, 3, 0, amounts)[1]);
        Assert.False(PlanLimitRules.Derive(plan, 3, 0, new HashSet<int>(), "TK", "NP").TryGetValue(1, out var none) && none == "FULL");
    }

    [Fact]
    public void A_full_room_and_board_makes_its_service_tax_full_and_a_full_icu_its_tax()
    {
        var tk = PlanLimitRules.Derive([B("101", "0", "120", "250")], 3, 0, new HashSet<int> { 1 }, "TK", "NP");
        Assert.Equal(("FULL", "FULL"), (tk[1], tk[27]));
        var icu = PlanLimitRules.Derive([B("102", "FULL", "20", "FULL")], 0, 2, new HashSet<int> { 2 });
        Assert.False(icu.ContainsKey(28) && icu[28] != "FULL");
        Assert.Equal("45.00", PlanLimitRules.Derive([B("101", "0", "120", "250")], 3, 0, new HashSet<int> { 1 })[27]);
    }

    [Fact]
    public void The_convention_rows_are_reported_so_the_calculation_can_say_where_it_is_not_the_plan()
    {
        var withoutRow = new[] { B("103", "FULL") };
        Assert.Equal([10, 7], PlanLimitRules.ConventionSlots(withoutRow, new HashSet<int> { 10, 7, 24 }, "XL", "ISLAM"));
        Assert.Equal([1], PlanLimitRules.ConventionSlots([B("101", "0", "120", "250")], new HashSet<int> { 1 }, "TK", "NP"));
        Assert.Empty(PlanLimitRules.ConventionSlots(withoutRow, new HashSet<int> { 10 }, "AY", "NP"));
        Assert.Empty(PlanLimitRules.ConventionSlots([B("301", "80.00")], new HashSet<int> { 10 }, "TK", null));
        Assert.Empty(PlanLimitRules.ConventionSlots(withoutRow, null, "TK", "NP"));
    }

    [Fact]
    public void A_payors_convention_fills_a_benefit_the_plan_has_no_row_for_and_only_then()
    {
        var withoutRow = new[] { B("103", "FULL") };
        var amounts = new HashSet<int> { 10, 7, 24, 23 };
        var xl = PlanLimitRules.Derive(withoutRow, 0, 0, amounts, "XL", "ISLAM");
        Assert.Equal(("FULL", "FULL"), (xl[10], xl[7]));
        Assert.False(xl.ContainsKey(24) || xl.ContainsKey(23));
        Assert.Equal("FULL", PlanLimitRules.Derive(withoutRow, 0, 0, amounts, "TK", null)[10]);
        Assert.Equal("FULL", PlanLimitRules.Derive(withoutRow, 0, 0, amounts, "AX", null)[24]);
        Assert.Equal("FULL", PlanLimitRules.Derive(withoutRow, 0, 0, amounts, "SE", null)[23]);
        Assert.False(PlanLimitRules.Derive(withoutRow, 0, 0, amounts, "AY", null).ContainsKey(10));
        // the plan's own row wins, and with no benefit rows at all or no amount in the row nothing is filled in
        Assert.Equal("80.00", PlanLimitRules.Derive([B("301", "80.00")], 0, 0, amounts, "TK", null)[10]);
        Assert.False(PlanLimitRules.Derive([], 0, 0, amounts, "TK", null).ContainsKey(10));
        Assert.False(PlanLimitRules.Derive(withoutRow, 0, 0, new HashSet<int>(), "TK", null).ContainsKey(10));
    }

    [Fact]
    public void A_plain_benefit_gives_the_plans_FULL_or_amount_to_its_row()
    {
        var limits = PlanLimitRules.Derive([B("103", "FULL"), B("302", " 120.00 "), B("402", "FULL"), B("999", "FULL")], 0, 0);
        Assert.Equal(new Dictionary<int, string> { [3] = "FULL", [11] = "120.00", [15] = "FULL" }, limits);
    }

    [Theory]
    [InlineData("100", "120", 2, "200.00")]
    [InlineData("100", "120", 150, "12,000.00")]
    [InlineData("100.0000", null, 3, "300.00")]
    public void Room_and_board_is_the_rate_times_the_days_held_to_the_days_the_plan_allows(string rate, string? planDays, int days, string expected) =>
        Assert.Equal(expected, PlanLimitRules.Derive([B("101", "0", planDays, rate)], days, 0)[1]);

    [Fact]
    public void A_stay_with_no_room_and_board_days_has_no_limit_but_ICU_with_no_days_has_zero()
    {
        var limits = PlanLimitRules.Derive([B("101", "0", "120", "100"), B("102", "7000", "20", "350")], 0, 0);
        Assert.False(limits.ContainsKey(1));
        Assert.False(limits.ContainsKey(27));
        Assert.Equal("0.00", limits[2]);
    }

    [Fact]
    public void ICU_uses_its_own_days()
    {
        var limits = PlanLimitRules.Derive([B("101", "0", "120", "100"), B("102", "7000", "20", "350")], 2, 3);
        Assert.Equal(("200.00", "1,050.00"), (limits[1], limits[2]));
    }

    [Fact]
    public void The_case_dependent_rows_get_the_plans_limit_only_when_the_row_has_an_amount()
    {
        var benefits = new[] { B("303", "FULL"), B("801", "80"), B("404", "FULL"), B("701", "FULL") };
        Assert.Empty(PlanLimitRules.Derive(benefits, 0, 0));
        Assert.Equal(new Dictionary<int, string> { [12] = "FULL", [21] = "80" }, PlanLimitRules.Derive(benefits, 0, 0, new HashSet<int> { 12, 21 }));
    }

    [Fact]
    public void A_row_with_an_amount_whose_benefit_the_plan_does_not_list_gets_the_desktops_default()
    {
        var plan = new[] { B("101", "0", "120", "100"), B("104", "FULL") };
        var limits = PlanLimitRules.Derive(plan, 2, 0, new HashSet<int> { 1, 3, 9, 11, 14, 21, 23, 15 });
        Assert.Equal(("FULL", "FULL", "FULL", "FULL", "50.00"), (limits[3], limits[9], limits[11], limits[14], limits[21]));
        Assert.False(limits.ContainsKey(23)); // admin and ambulance show "-" when the plan has no row
        Assert.False(limits.ContainsKey(15));
    }

    [Fact]
    public void The_default_applies_only_to_rows_with_an_amount_and_only_when_the_plan_has_rows()
    {
        Assert.False(PlanLimitRules.Derive([B("104", "FULL")], 0, 0, new HashSet<int>()).ContainsKey(3));
        Assert.False(PlanLimitRules.Derive([], 0, 0, new HashSet<int> { 3 }).ContainsKey(3));
        Assert.Equal("80.00", PlanLimitRules.Derive([B("103", "80.00")], 0, 0, new HashSet<int> { 3 })[3]);
    }

    [Fact]
    public void Others_is_never_covered_whatever_the_plan_says_and_only_when_it_has_an_amount()
    {
        Assert.False(PlanLimitRules.Derive([B("901", "100")], 0, 0).ContainsKey(25));
        Assert.Equal("0.00", PlanLimitRules.Derive([B("901", "100")], 0, 0, new HashSet<int> { 25 })[25]);
        Assert.False(PlanLimitRules.Derive([], 0, 0, new HashSet<int> { 25 }).ContainsKey(25));
    }

    [Fact]
    public void A_stay_benefit_without_a_rate_gives_no_limit() => Assert.Empty(PlanLimitRules.Derive([B("101", "0", "120"), B("102", "7000", "20")], 5, 5));

    [Theory]
    [InlineData("200.00", "12.00")]
    [InlineData("123.45", "7.41")]
    [InlineData("12,000.00", "720.00")]
    public void The_service_tax_limit_is_six_per_cent_of_the_room_and_board_limit(string roomLimit, string tax)
    {
        var rate = decimal.Parse(roomLimit.Replace(",", ""), System.Globalization.CultureInfo.InvariantCulture);
        var limits = PlanLimitRules.Derive([B("101", "0", null, "1")], (int)rate, 0);
        if (rate == Math.Floor(rate)) Assert.Equal(tax, limits[27]);
        else Assert.Equal(tax, PlanLimitRules.Money(Math.Round(rate * PlanLimitRules.TaxRate, 2, MidpointRounding.AwayFromZero)));
    }

    [Fact]
    public void There_is_no_tax_limit_without_a_room_and_board_limit() => Assert.False(PlanLimitRules.Derive([B("101", "0", "5", "0")], 4, 0).ContainsKey(27));

    [Theory]
    [InlineData("0.00", "-", true)]
    [InlineData("0.00", null, true)]
    [InlineData("0.00", "0", true)]
    [InlineData("FULL", "-", false)]
    [InlineData("FULL", "full", true)]
    [InlineData("100.00", "FULL", false)]
    [InlineData("1,000.00", "1000", true)]
    public void Two_limits_have_the_same_effect_when_both_approve_the_same(string? derived, string? saved, bool same) => Assert.Equal(same, PlanLimitRules.SameEffect(derived, saved));

    [Theory]
    [InlineData("FULL", "full", true)]
    [InlineData("1,000.00", "1000", true)]
    [InlineData("80", "80.00", true)]
    [InlineData("80", "-", false)]
    [InlineData("FULL", "80.00", false)]
    [InlineData(null, null, true)]
    public void A_derived_limit_equals_a_saved_one_when_the_word_or_the_amount_is_the_same(string? derived, string? saved, bool same) =>
        Assert.Equal(same, PlanLimitRules.SameLimit(derived, saved));
}

/// <summary>The limits the plan fixes, derived from the benefit table, against the limit text the desktop saved with the worksheets.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class WorksheetPlanLimitsOracleTests(ITestOutputHelper output)
{
    /// <summary>
    /// Over about one saved version in a hundred, each kind of row is derived from the plan's benefit rows and compared with the saved limit text, counting only the rows that have an amount (a row
    /// without one shows no limit in the desktop and nothing can be approved on it): the plain rows (FULL or an amount), the case dependent rows, Others, Room &amp; Board and ICU (rate times the saved
    /// days) and the service tax.
    /// </summary>
    [Fact]
    public async Task The_limits_the_plan_fixes_reproduce_the_text_the_desktop_saved_on_the_rows_that_have_an_amount()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var sample = (await session.Connection.QueryAsync<(string Cas, int Version, string? RoomDays, string? IcuDays)>(
            """
            SELECT RTRIM(l.CASNumber), l.CLMVersion, RTRIM(l.CLMBoardDays), RTRIM(l.CLMICUDays) FROM genisis.WS_ClaimLimit l
            WHERE l.CASNumber NOT LIKE 'ZZ%' AND ABS(CHECKSUM(l.CASNumber, l.CLMVersion)) % 100 = 0
              AND EXISTS (SELECT 1 FROM dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v WHERE v.CASNumber = l.CASNumber AND v.ClaimVersion = l.CLMVersion AND v.MBMNumber IS NOT NULL)
            """, transaction: session.Transaction)).ToList();
        Assert.True(sample.Count >= 150, $"only {sample.Count} versions in the sample");

        static decimal Days(string? text) => decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0m;
        var service = new PlanLimitService(TestDb.Sessions(), new PlanBenefitRepository());
        var engine = new WorksheetEngineRepository();
        var groups = new[] { "plain", "case dependent", "Others", "Room & Board", "ICU", "tax" }.ToDictionary(g => g, _ => (N: 0, Ok: 0));
        foreach (var v in sample)
        {
            var withAmount = (await engine.ActualAsync(session, v.Cas, v.Version)).Where(a => a.Actual > 0).Select(a => a.Slot).ToHashSet();
            var derived = await service.LimitsAsync(v.Cas, v.Version, Days(v.RoomDays), Days(v.IcuDays), withAmount);
            if (derived.Count == 0) continue;
            var saved = (await engine.LimitsAsync(session, v.Cas, v.Version)).ToDictionary(l => l.Slot, l => l.LimitText);
            foreach (var (slot, text) in derived)
            {
                if (!withAmount.Contains(slot) && slot != PlanLimitRules.RoomAndBoardTax) continue;
                if (!saved.TryGetValue(slot, out var s) || s is null) continue;
                var group = PlanLimitRules.Slots.Values.Contains(slot) ? "plain" : PlanLimitRules.CaseSlots.Values.Contains(slot) ? "case dependent" : slot == PlanLimitRules.Others ? "Others"
                    : slot == PlanLimitRules.RoomAndBoard ? "Room & Board" : slot == PlanLimitRules.Icu ? "ICU" : "tax";
                var (n, ok) = groups[group];
                groups[group] = (n + 1, ok + ((group == "Others" ? PlanLimitRules.SameEffect(text, s) : PlanLimitRules.SameLimit(text, s)) ? 1 : 0));
            }
        }
        output.WriteLine($"{sample.Count} versions: " + string.Join(", ", groups.Select(g => $"{g.Key} {g.Value.Ok}/{g.Value.N}")));
        void Check(string group, int minimum, double rate) => Assert.True(groups[group].N >= minimum && groups[group].Ok >= groups[group].N * rate, $"{group}: {groups[group].Ok} of {groups[group].N}");
        Check("plain", 120, 0.90);
        Check("case dependent", 25, 0.80);
        Check("Others", 40, 0.90);
        Check("Room & Board", 20, 0.88);
        if (groups["ICU"].N > 0) Check("ICU", 1, 0.80); // rows with an ICU amount are rare: none in a small sample is normal (96 per cent in the larger one)
        Check("tax", 20, 0.88);
    }
}
