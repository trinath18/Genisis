using Dapper;
using Genisis.Api.Worksheet.Engine;
using Genisis.Api.Worksheet.Enquiry;

namespace Genisis.Api.Tests;

/// <summary>The grid rules of the worksheet engine: approved (GetMin / GetMax), hospital and member amounts.</summary>
public class WorksheetEngineRulesTests
{
    [Theory]
    [InlineData("200.00", 200)]
    [InlineData("1,000.00", 1000)]
    [InlineData(".00", 0)]
    [InlineData("0", 0)]
    [InlineData("(50.00)", -50)]
    [InlineData("-50", -50)]
    [InlineData("50-", -50)]
    [InlineData("+7.5", 7.5)]
    [InlineData("5.", 5)]
    [InlineData("  12  ", 12)]
    public void A_limit_that_VB6_would_call_numeric_is_read_as_a_number(string text, double expected)
    {
        Assert.True(WorksheetRules.TryParseLimit(text, out var value));
        Assert.Equal((decimal)expected, value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-")]
    [InlineData("FULL")]
    [InlineData("50%")]
    [InlineData("abc")]
    [InlineData("1,2,x")]
    public void Words_dashes_blanks_and_percentages_are_not_numbers(string? text) => Assert.False(WorksheetRules.TryParseLimit(text, out _));

    [Theory]
    [InlineData(150, "200.00", 150)]
    [InlineData(250, "200.00", 200)]
    [InlineData(200, "200.00", 200)]
    [InlineData(80, "1,000.00", 80)]
    [InlineData(50, ".00", 0)]
    [InlineData(50, "FULL", 50)]
    [InlineData(50, " full ", 50)]
    [InlineData(50, "-", 0)]
    [InlineData(50, "", 0)]
    [InlineData(50, null, 0)]
    [InlineData(50, "50%", 0)]
    [InlineData(0, "200.00", 0)]
    public void Approved_is_the_actual_amount_held_to_the_limit(double actual, string? limit, double expected) =>
        Assert.Equal((decimal)expected, WorksheetRules.Approve((decimal)actual, limit));

    [Theory]
    [InlineData(-10, "-5", -5)]
    [InlineData(-10, "-20", -10)]
    [InlineData(-10, "100", 100)]
    [InlineData(-10, "FULL", -10)]
    public void A_negative_actual_amount_is_ported_as_the_desktop_wrote_it(double actual, string limit, double expected) =>
        Assert.Equal((decimal)expected, WorksheetRules.Approve((decimal)actual, limit));

    [Theory]
    [InlineData(150, "200.00", 200)]
    [InlineData(250, "200.00", 250)]
    [InlineData(50, "FULL", 50)]
    [InlineData(50, "-", 0)]
    [InlineData(50, "-20", -20)]
    [InlineData(-50, "-20", -50)]
    public void A_row_flagged_pay_member_uses_the_limit_as_a_floor(double actual, string limit, double expected) =>
        Assert.Equal((decimal)expected, WorksheetRules.Approve((decimal)actual, limit, payMember: true));

    [Fact]
    public void The_member_amount_is_approved_minus_what_is_payable_to_the_hospital_and_the_totals_add_up()
    {
        var amounts = new[]
        {
            new SlotAmount { Slot = 2, Actual = 300, ToHospital = 300 },
            new SlotAmount { Slot = 1, Actual = 120, ToHospital = 0 },
            new SlotAmount { Slot = 3, Actual = 40, ToHospital = 0 },
        };
        var limits = new Dictionary<int, string?> { [1] = "FULL", [2] = "200.00", [3] = "-" };

        var result = WorksheetRules.Evaluate(amounts, limits);

        Assert.Equal([1, 2, 3], result.Lines.Select(l => l.Slot));
        Assert.Equal((120m, 120m, 0m, 120m), (result.Lines[0].Actual, result.Lines[0].Approved, result.Lines[0].ToHospital, result.Lines[0].ToMember));
        Assert.Equal((300m, 200m, 300m, -100m), (result.Lines[1].Actual, result.Lines[1].Approved, result.Lines[1].ToHospital, result.Lines[1].ToMember));
        Assert.Equal((40m, 0m, 0m, 0m), (result.Lines[2].Actual, result.Lines[2].Approved, result.Lines[2].ToHospital, result.Lines[2].ToMember));
        Assert.Equal(new WorksheetTotals(460m, 320m, 300m, 20m), result.Totals);
    }

    [Fact]
    public void A_row_with_no_saved_limit_text_approves_nothing()
    {
        var result = WorksheetRules.Evaluate([new SlotAmount { Slot = 8, Actual = 75 }], new Dictionary<int, string?>());
        Assert.Equal(0m, result.Lines[0].Approved);
    }

    [Fact]
    public void Rows_can_be_flagged_to_pay_the_member()
    {
        var result = WorksheetRules.Evaluate([new SlotAmount { Slot = 1, Actual = 100 }], new Dictionary<int, string?> { [1] = "150.00" }, new HashSet<int> { 1 });
        Assert.Equal(150m, result.Lines[0].Approved);
    }
}

/// <summary>The whole evaluation, checked against the worksheets the desktop saved.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class WorksheetEngineOracleTests
{
    /// <summary>
    /// Evaluating a saved version from its invoices and its saved limit text must give the approved, hospital and member amounts the desktop saved. Over all 35,592 saved
    /// versions the rule agrees in 99.9 per cent of rows or better (approved), 99.97 (hospital) and 99.8 (member); what remains are hand-edited worksheets.
    /// </summary>
    [Fact]
    public async Task Evaluating_a_saved_version_reproduces_what_the_desktop_saved()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var sample = (await session.Connection.QueryAsync<(string Cas, int Version)>(
            "SELECT TOP (400) RTRIM(CASNumber), CLMVersion FROM genisis.WS_ClaimActual ORDER BY CASNumber DESC, CLMVersion", transaction: session.Transaction)).ToList();
        Assert.True(sample.Count >= 200);

        var service = new WorksheetEngineService(TestDb.Sessions(), new WorksheetEngineRepository());
        var saved = new EnquiryRepository();
        int rows = 0, approvedOk = 0, hospitalOk = 0, memberOk = 0;
        foreach (var (cas, version) in sample)
        {
            var evaluated = await service.EvaluateSavedAsync(cas, version);
            var stored = await saved.BenefitsAsync(session, cas, version, version);
            foreach (var line in evaluated.Lines)
            {
                var s = stored.First(b => b.Slot == line.Slot);
                rows++;
                if (Math.Abs(line.Approved - (s.Approved ?? 0)) < 0.02m) approvedOk++;
                if (Math.Abs(line.ToHospital - (s.Hospital ?? 0)) < 0.02m) hospitalOk++;
                if (Math.Abs(line.ToMember - (s.Member ?? 0)) < 0.02m) memberOk++;
            }
        }
        Assert.True(approvedOk >= rows * 0.998, $"approved: {approvedOk} of {rows} rows agree");
        Assert.True(hospitalOk >= rows * 0.999, $"hospital: {hospitalOk} of {rows} rows agree");
        Assert.True(memberOk >= rows * 0.995, $"member: {memberOk} of {rows} rows agree");
    }
}
