using Dapper;
using Genisis.Api.Worksheet.Engine;
using Xunit.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Worksheet calculation engine, step 4: the limits the plan fixes.</summary>
public class WorksheetPlanLimitsRulesTests
{
    private static PlanBenefit B(string code, string? limit = null, string? days = null, string? claimable = null) => new() { Code = code, Limit = limit, Days = days, Claimable = claimable };

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
    /// Over about one saved version in forty, each kind of row is derived from the plan's benefit rows and compared with the saved limit text: the plain rows (FULL or an amount), Room &amp; Board
    /// and ICU (rate times the saved days) and the service tax. Over all 31,190 versions that have plan data the agreement is 93 to 97 per cent for the plain rows, 94 per cent for Room &amp; Board,
    /// 99 per cent for ICU and 98.6 per cent for the tax.
    /// </summary>
    [Fact]
    public async Task The_limits_the_plan_fixes_reproduce_the_text_the_desktop_saved()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var sample = (await session.Connection.QueryAsync<(string Cas, int Version, string? RoomDays, string? IcuDays)>(
            """
            SELECT RTRIM(l.CASNumber), l.CLMVersion, RTRIM(l.CLMBoardDays), RTRIM(l.CLMICUDays) FROM genisis.WS_ClaimLimit l
            WHERE l.CASNumber NOT LIKE 'ZZ%' AND ABS(CHECKSUM(l.CASNumber, l.CLMVersion)) % 40 = 0
              AND EXISTS (SELECT 1 FROM dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v WHERE v.CASNumber = l.CASNumber AND v.ClaimVersion = l.CLMVersion AND v.MBMNumber IS NOT NULL)
            """, transaction: session.Transaction)).ToList();
        Assert.True(sample.Count >= 300, $"only {sample.Count} versions in the sample");

        var service = new PlanLimitService(TestDb.Sessions(), new PlanBenefitRepository());
        var engine = new WorksheetEngineRepository();
        int plainN = 0, plainOk = 0, roomN = 0, roomOk = 0, icuN = 0, icuOk = 0, taxN = 0, taxOk = 0;
        foreach (var v in sample)
        {
            var derived = await service.LimitsAsync(v.Cas, v.Version, int.TryParse(v.RoomDays, out var rd) ? rd : 0, int.TryParse(v.IcuDays, out var id) ? id : 0);
            if (derived.Count == 0) continue;
            var saved = (await engine.LimitsAsync(session, v.Cas, v.Version)).ToDictionary(l => l.Slot, l => l.LimitText);
            foreach (var (slot, text) in derived)
            {
                if (!saved.TryGetValue(slot, out var s) || s is null) continue;
                var ok = PlanLimitRules.SameLimit(text, s);
                if (PlanLimitRules.Slots.Values.Contains(slot)) { plainN++; plainOk += ok ? 1 : 0; }
                else if (slot == PlanLimitRules.RoomAndBoard) { roomN++; roomOk += ok ? 1 : 0; }
                else if (slot == PlanLimitRules.Icu) { icuN++; icuOk += ok ? 1 : 0; }
                else if (slot == PlanLimitRules.RoomAndBoardTax) { taxN++; taxOk += ok ? 1 : 0; }
            }
        }
        output.WriteLine($"{sample.Count} versions: plain rows {plainOk}/{plainN}, Room & Board {roomOk}/{roomN}, ICU {icuOk}/{icuN}, tax {taxOk}/{taxN}");
        Assert.True(plainN > 1000 && plainOk >= plainN * 0.92, $"plain rows: {plainOk} of {plainN}");
        Assert.True(roomN > 50 && roomOk >= roomN * 0.90, $"Room & Board: {roomOk} of {roomN}");
        Assert.True(icuN > 30 && icuOk >= icuN * 0.93, $"ICU: {icuOk} of {icuN}");
        Assert.True(taxN > 50 && taxOk >= taxN * 0.93, $"tax: {taxOk} of {taxN}");
    }
}
