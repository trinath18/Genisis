using System.Globalization;
using Dapper;
using Genisis.Api.Worksheet.Engine;
using Xunit.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>The rows the Registration save writes to the four per-row tables, without a database.</summary>
public class WorksheetRowsRulesTests
{
    [Theory]
    [InlineData(214.4, "214.40")]
    [InlineData(1036.51, "1,036.51")]
    [InlineData(1234567.8, "1,234,567.80")]
    [InlineData(0.5, ".50")]
    [InlineData(-12.5, "-12.50")]
    [InlineData(-0.25, "-.25")]
    public void The_grid_shows_money_with_thousands_two_decimals_and_no_digit_before_the_point_under_one(double value, string expected) => Assert.Equal(expected, WorksheetRows.Grid((decimal)value));

    [Theory]
    [InlineData(-94.4, "-94.4")]
    [InlineData(0.5, "0.5")]
    [InlineData(-5.66, "-5.66")]
    [InlineData(0.3333, "0.3333")]
    public void VB_makes_a_number_of_an_amount_without_separators_or_trailing_zeros(double value, string expected) => Assert.Equal(expected, WorksheetRows.Number((decimal)value));

    [Fact]
    public void An_empty_cell_is_a_dash_and_the_members_column_saves_what_is_under_one_as_a_number()
    {
        Assert.Equal("-", WorksheetRows.Cell(0m, WorksheetRows.Kind.Actual));
        Assert.Equal("-", WorksheetRows.Cell(0m, WorksheetRows.Kind.Member));
        Assert.Equal("214.40", WorksheetRows.Cell(214.4m, WorksheetRows.Kind.Hospital));
        Assert.Equal("-94.4", WorksheetRows.Cell(-94.4m, WorksheetRows.Kind.Member));
        Assert.Equal("0.5", WorksheetRows.Cell(0.5m, WorksheetRows.Kind.Member));
        Assert.Equal("1,500.00", WorksheetRows.Cell(1500m, WorksheetRows.Kind.Member));
        Assert.Equal("1.00", WorksheetRows.Cell(1m, WorksheetRows.Kind.Member));
        Assert.Equal(".50", WorksheetRows.Cell(0.5m, WorksheetRows.Kind.Actual));
        Assert.Equal("-94.40", WorksheetRows.Cell(-94.4m, WorksheetRows.Kind.Actual));
    }

    [Fact]
    public void The_approved_cell_of_the_monthly_and_organ_rows_is_a_zero_when_empty_and_nothing_else_is()
    {
        Assert.Equal("0", WorksheetRows.Cell(0m, WorksheetRows.Kind.Approved, 17));
        Assert.Equal("0", WorksheetRows.Cell(0m, WorksheetRows.Kind.Approved, 18));
        Assert.Equal("-", WorksheetRows.Cell(0m, WorksheetRows.Kind.Approved, 16));
        Assert.Equal("-", WorksheetRows.Cell(0m, WorksheetRows.Kind.Actual, 17));
        Assert.Equal("-", WorksheetRows.Cell(0m, WorksheetRows.Kind.Hospital, 18));
        Assert.Equal("5.00", WorksheetRows.Cell(5m, WorksheetRows.Kind.Approved, 17));
    }

    [Fact]
    public void Plan_families_2_to_5_and_payor_IB_are_written_flat()
    {
        Assert.All(new[] { "2", "3", "4", "5", " 3 " }, (f) => Assert.True(WorksheetRows.Flat(f, "XX")));
        Assert.True(WorksheetRows.Flat("A", "IB"));
        Assert.False(WorksheetRows.Flat("A", "XL"));
        Assert.False(WorksheetRows.Flat(null, null));
        Assert.False(WorksheetRows.Flat("1", "TK"));
    }

    [Fact]
    public void The_itemised_plans_write_29_columns_and_the_flat_ones_the_two_sums_instead_of_eight()
    {
        var itemised = WorksheetRows.Columns(false);
        var flat = WorksheetRows.Columns(true);
        Assert.Equal((29, 23), (itemised.Count, flat.Count));
        Assert.Equal(itemised.Count, itemised.Select((c) => c.Column).Distinct().Count());
        Assert.Equal("CLMSurgicalAmt", itemised.Single((c) => c.Slot == 8).Column);
        Assert.Equal("CLMCOPreHSSum", flat.Single((c) => c.Slot == 8).Column);
        Assert.Equal("CLMCOMPreHSSum", flat.Single((c) => c.Slot == 13).Column);
        Assert.DoesNotContain(flat, (c) => c.Slot is 6 or 7 or 9 or 10 or 11 or 12);
        Assert.Equal("CLMTax", itemised.Single((c) => c.Slot == 19).Column);
        Assert.Equal(Enumerable.Range(1, 29), itemised.Select((c) => c.Slot).OrderBy((s) => s));
    }

    [Fact]
    public void The_limit_row_has_the_limit_text_of_each_row_and_the_days_they_were_worked_out_with()
    {
        var grid = new WorksheetEvaluation([new WorksheetLine(1, 0, "1,500.00", 0, 0, 0), new WorksheetLine(2, 0, "0", 0, 0, 0), new WorksheetLine(3, 0, " FULL ", 0, 0, 0), new WorksheetLine(8, 0, "300.00", 0, 0, 0)], new WorksheetTotals(0, 0, 0, 0));
        var itemised = WorksheetRows.Limits(grid, false, new WorksheetRows.LimitDays(Board: 6m, Icu: 0m, PhysicianVisits: 2.5m, Cash: 3m));
        Assert.Equal(("1,500.00", "0", "FULL", "-", "300.00"), (itemised["CLMBoardAmt"], itemised["CLMICUAmt"], itemised["CLMServices"], itemised["CLMTheatre"], itemised["CLMSurgicalAmt"]));
        Assert.Equal(("6", "0", "2.5", "0", "3", "0"), (itemised["CLMBoardDays"], itemised["CLMICUDays"], itemised["CLMINHOSPhyDays"], itemised["CLMKIDDCDays"], itemised["CLMCashDays"], itemised["CLMGurdianDays"]));
        var flat = WorksheetRows.Limits(grid, true, new WorksheetRows.LimitDays(Board: 6m));
        Assert.Equal("300.00", flat["CLMCOPreHSSum"]);
        Assert.False(flat.ContainsKey("CLMSurgicalAmt") || flat.ContainsKey("CLMINHOSPhyDays"));
        Assert.Equal(WorksheetRows.Columns(true).Count + 5, flat.Count);
        Assert.Equal(WorksheetRows.Columns(false).Count + 6, itemised.Count);
    }

    [Fact]
    public void The_days_a_limit_is_worked_out_with_are_the_stay_held_to_the_days_the_plan_allows()
    {
        PlanBenefit Plan(string code, string? days) => new() { Code = code, Days = days };
        Assert.Equal(7m, PlanLimitRules.LimitedDays([Plan("101", "120")], "101", 7m));
        Assert.Equal(120m, PlanLimitRules.LimitedDays([Plan("101", "120")], "101", 200m));
        Assert.Equal(7.5m, PlanLimitRules.LimitedDays([Plan("101", "0")], "101", 7.5m));
        Assert.Equal(7.5m, PlanLimitRules.LimitedDays([Plan("101", null)], "101", 7.5m));
        Assert.Equal(3m, PlanLimitRules.LimitedDays([Plan("102", "20")], "101", 3m));
        Assert.Equal(2m, PlanLimitRules.LimitedDays([], "102", 2m));
    }

    [Fact]
    public void A_table_is_the_grid_column_it_holds_in_the_columns_of_the_tables_rows()
    {
        var grid = new WorksheetEvaluation(
            [new WorksheetLine(1, 214.4m, "120.00", 120m, 214.4m, -94.4m), new WorksheetLine(3, 1036.51m, "FULL", 1036.51m, 1036.51m, 0m), new WorksheetLine(27, 12.86m, "7.20", 7.2m, 12.86m, -5.66m)],
            new WorksheetTotals(1263.77m, 1163.71m, 1263.77m, -100.06m));
        var actual = WorksheetRows.Build(grid, false, WorksheetRows.Kind.Actual);
        var member = WorksheetRows.Build(grid, false, WorksheetRows.Kind.Member);
        Assert.Equal(("214.40", "1,036.51", "12.86", "-"), (actual["CLMBoardAmt"], actual["CLMServices"], actual["CLMBoardTaxAmt"], actual["CLMICUAmt"]));
        Assert.Equal(("-94.4", "-", "-5.66"), (member["CLMBoardAmt"], member["CLMServices"], member["CLMBoardTaxAmt"]));
        Assert.Equal("120.00", WorksheetRows.Build(grid, false, WorksheetRows.Kind.Approved)["CLMBoardAmt"]);
        Assert.Equal("214.40", WorksheetRows.Build(grid, false, WorksheetRows.Kind.Hospital)["CLMBoardAmt"]);
        Assert.Equal(29, actual.Count);
    }
}

/// <summary>The row builders against the rows the desktop saved: each saved row is read as numbers and written again, and the text is compared.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class WorksheetRowsReferenceTests(ITestOutputHelper output)
{
    private static decimal Parse(object? value)
    {
        var text = Convert.ToString(value)?.Trim();
        if (string.IsNullOrEmpty(text) || text == "-") return 0m;
        var negative = text.StartsWith('(') && text.EndsWith(')');
        text = text.Trim('(', ')').Replace(",", "");
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? (negative ? -n : n) : 0m;
    }

    /// <summary>
    /// Over about one saved version in twenty of those opened since July 2020 (the desktop now in use), each of the four tables is read back into numbers per worksheet row and written again by <see cref="WorksheetRows"/>, and every cell of the table is compared with
    /// the text the desktop saved. This checks the row-to-column map of both plan layouts and the formats, with the engine out of it. The classes of difference are printed.
    /// </summary>
    [Fact]
    public async Task Rows_written_from_a_saved_rows_own_numbers_equal_the_text_the_desktop_saved()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var sample = (await session.Connection.QueryAsync<(string Cas, int Version, string? Family, string? Payor)>(
            """
            SELECT RTRIM(a.CASNumber), a.CLMVersion, LEFT(RTRIM(CASE WHEN v.MBMPatientCovID <> N'00' AND ISNULL(RTRIM(v.MBMCPLNCode), N'') <> N'' THEN v.MBMCPLNCode ELSE v.PLNCode END), 1), LEFT(RTRIM(v.PAYCode), 2)
            FROM genisis.WS_ClaimActual a
            JOIN dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v ON v.CASNumber = a.CASNumber AND v.ClaimVersion = a.CLMVersion
            JOIN genisis.WS_Claim w ON w.CASNumber = a.CASNumber AND w.ClaimVersion = a.CLMVersion
            WHERE a.CASNumber NOT LIKE 'ZZ%' AND v.MBMNumber IS NOT NULL AND w.ClaimOpenDate >= '2020-07-01' AND ABS(CHECKSUM(a.CASNumber, a.CLMVersion)) % 20 = 0
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimApprove p WHERE p.CASNumber = a.CASNumber AND p.CLMVersion = a.CLMVersion)
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimHos h WHERE h.CASNumber = a.CASNumber AND h.CLMVersion = a.CLMVersion)
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimMember m WHERE m.CASNumber = a.CASNumber AND m.CLMVersion = a.CLMVersion)
            """, transaction: session.Transaction, commandTimeout: 300)).ToList();
        Assert.True(sample.Count >= 300, $"only {sample.Count} versions in the sample");

        var tables = new (string Table, WorksheetRows.Kind Kind)[] { ("ClaimActual", WorksheetRows.Kind.Actual), ("ClaimApprove", WorksheetRows.Kind.Approved), ("ClaimHos", WorksheetRows.Kind.Hospital), ("ClaimMember", WorksheetRows.Kind.Member) };
        var cells = tables.ToDictionary((t) => t.Table, (_) => (N: 0, Same: 0));
        var versionsSame = tables.ToDictionary((t) => t.Table, (_) => 0);
        var classes = new Dictionary<string, int>();
        int done = 0;
        foreach (var (cas, version, family, payor) in sample)
        {
            var flat = WorksheetRows.Flat(family, payor);
            var columns = WorksheetRows.Columns(flat);
            var saved = new Dictionary<string, IDictionary<string, object?>>();
            foreach (var (table, _) in tables)
            {
                var row = await session.Connection.QueryFirstOrDefaultAsync($"SELECT * FROM genisis.WS_{table} WHERE CASNumber = @cas AND CLMVersion = @version", new { cas, version }, session.Transaction);
                if (row is IDictionary<string, object?> r) saved[table] = r;
            }
            if (saved.Count < 4) continue;
            done++;
            var lines = columns.Select((c) => new WorksheetLine(
                c.Slot, Parse(saved["ClaimActual"][c.Column]), null, Parse(saved["ClaimApprove"][c.Column]), Parse(saved["ClaimHos"][c.Column]), Parse(saved["ClaimMember"][c.Column]))).ToList();
            var grid = new WorksheetEvaluation(lines, new WorksheetTotals(0, 0, 0, 0));
            foreach (var (table, kind) in tables)
            {
                var built = WorksheetRows.Build(grid, flat, kind);
                var allSame = true;
                foreach (var (column, text) in built)
                {
                    var savedText = Convert.ToString(saved[table][column])?.Trim();
                    var same = string.Equals(savedText, text, StringComparison.Ordinal);
                    cells[table] = (cells[table].N + 1, cells[table].Same + (same ? 1 : 0));
                    if (same) continue;
                    allSame = false;
                    var shape = $"{table} {(table == "ClaimApprove" ? column : "")}: saved [{Shape(savedText)}] -> built [{Shape(text)}]{(flat ? " (flat)" : "")}";
                    classes[shape] = classes.GetValueOrDefault(shape) + 1;
                }
                versionsSame[table] += allSame ? 1 : 0;
            }
        }
        foreach (var (table, (n, same)) in cells) output.WriteLine($"{table}: {same} of {n} cells equal ({100.0 * same / n:F2}%), {versionsSame[table]} of {done} rows entirely equal");
        foreach (var (shape, n) in classes.OrderByDescending((c) => c.Value).Take(20)) output.WriteLine($"  {n,6}x {shape}");
        Assert.True(done >= 300);
        foreach (var (table, (n, same)) in cells) Assert.True(same >= n * 0.90, $"{table}: {same} of {n} cells equal");
    }

    /// <summary>
    /// The limit table the same way: each saved <c>ClaimLimit</c> row of the sample is read back (limit texts and days) and written again by <see cref="WorksheetRows.Limits"/>, and every cell it writes is compared with the
    /// saved text. Then the days the engine can work out alone, Room &amp; Board and ICU (the stay's days held to the plan's), are compared with the saved ones.
    /// </summary>
    [Fact]
    public async Task The_limit_row_written_from_a_saved_rows_own_texts_equals_it_and_the_engine_finds_the_board_and_icu_days()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var sample = (await session.Connection.QueryAsync<(string Cas, int Version, string? Family, string? Payor)>(
            """
            SELECT RTRIM(a.CASNumber), a.CLMVersion, LEFT(RTRIM(CASE WHEN v.MBMPatientCovID <> N'00' AND ISNULL(RTRIM(v.MBMCPLNCode), N'') <> N'' THEN v.MBMCPLNCode ELSE v.PLNCode END), 1), LEFT(RTRIM(v.PAYCode), 2)
            FROM genisis.WS_ClaimLimit a
            JOIN dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v ON v.CASNumber = a.CASNumber AND v.ClaimVersion = a.CLMVersion
            JOIN genisis.WS_Claim w ON w.CASNumber = a.CASNumber AND w.ClaimVersion = a.CLMVersion
            WHERE a.CASNumber NOT LIKE 'ZZ%' AND v.MBMNumber IS NOT NULL AND w.ClaimOpenDate >= '2020-07-01' AND ABS(CHECKSUM(a.CASNumber, a.CLMVersion)) % 20 = 0
            """, transaction: session.Transaction, commandTimeout: 300)).ToList();
        Assert.True(sample.Count >= 300, $"only {sample.Count} versions in the sample");

        var classes = new Dictionary<string, int>();
        int cellsN = 0, cellsSame = 0, rowsSame = 0, done = 0, boardN = 0, boardSame = 0, icuN = 0, icuSame = 0;
        var stay = new StayDaysRepository();
        var plan = new PlanBenefitRepository();
        string? Text(IDictionary<string, object?> row, string column) => Convert.ToString(row[column])?.Trim();
        foreach (var (cas, version, family, payor) in sample)
        {
            if (await session.Connection.QueryFirstOrDefaultAsync("SELECT * FROM genisis.WS_ClaimLimit WHERE CASNumber = @cas AND CLMVersion = @version", new { cas, version }, session.Transaction) is not IDictionary<string, object?> saved) continue;
            done++;
            var flat = WorksheetRows.Flat(family, payor);
            var lines = WorksheetRows.Columns(flat).Select((c) => new WorksheetLine(c.Slot, 0, Text(saved, c.Column), 0, 0, 0)).ToList();
            decimal D(string column) => Parse(saved[column]);
            var built = WorksheetRows.Limits(new WorksheetEvaluation(lines, new WorksheetTotals(0, 0, 0, 0)), flat, new WorksheetRows.LimitDays(D("CLMBoardDays"), D("CLMICUDays"), D("CLMINHOSPhyDays"), D("CLMKIDDCDays"), D("CLMCashDays"), D("CLMGurdianDays")));
            var allSame = true;
            foreach (var (column, text) in built)
            {
                var savedText = Text(saved, column);
                var same = string.Equals(savedText, text, StringComparison.Ordinal);
                cellsN++;
                cellsSame += same ? 1 : 0;
                if (same) continue;
                allSame = false;
                var shape = $"{(column.EndsWith("Days", StringComparison.Ordinal) ? column : "limit")}: saved [{Shape(savedText)}] -> built [{Shape(text)}]{(flat ? " (flat)" : "")}";
                classes[shape] = classes.GetValueOrDefault(shape) + 1;
            }
            rowsSame += allSame ? 1 : 0;

            var benefits = await plan.BenefitsAsync(session, cas, version);
            var days = await stay.DaysAsync(session, cas, version);
            if (benefits.Count > 0)
            {
                boardN++;
                boardSame += Math.Abs(PlanLimitRules.LimitedDays(benefits, "101", days.RoomDays) - D("CLMBoardDays")) < 0.005m ? 1 : 0;
                icuN++;
                icuSame += Math.Abs(PlanLimitRules.LimitedDays(benefits, "102", days.IcuDays) - D("CLMICUDays")) < 0.005m ? 1 : 0;
            }
        }
        output.WriteLine($"ClaimLimit: {cellsSame} of {cellsN} cells equal ({100.0 * cellsSame / cellsN:F2}%), {rowsSame} of {done} rows entirely equal");
        foreach (var (shape, n) in classes.OrderByDescending((c) => c.Value).Take(14)) output.WriteLine($"  {n,6}x {shape}");
        output.WriteLine($"days worked out by the engine: Room & Board {boardSame} of {boardN}, ICU {icuSame} of {icuN}");
        Assert.True(done >= 300);
        Assert.True(cellsSame >= cellsN * 0.90, $"{cellsSame} of {cellsN} cells equal");
        Assert.True(boardN >= 200 && boardSame >= boardN * 0.80, $"Room & Board days: {boardSame} of {boardN}");
    }

    private static string Shape(string? text) => text switch
    {
        null => "NULL",
        "" => "empty",
        "-" => "dash",
        _ when text.Equals("FULL", StringComparison.OrdinalIgnoreCase) => "FULL",
        _ when text.Any((c) => !(char.IsDigit(c) || c is '.' or ',' or '-' or '(' or ')')) => "text",
        _ when text.StartsWith('(') => "(negative)",
        _ when text.Contains(',') => "thousands.dd",
        _ when text.StartsWith('.') => ".dd",
        _ when text.StartsWith("-.", StringComparison.Ordinal) => "-.dd",
        _ when text.StartsWith('-') && text.Contains('.') && text.Length - text.IndexOf('.') == 3 => "-n.dd",
        _ when text.StartsWith('-') => "-number",
        _ when text.Contains('.') && text.Length - text.IndexOf('.') == 3 => "n.dd",
        _ when text.Contains('.') => "n.d..",
        _ => "whole",
    };
}
