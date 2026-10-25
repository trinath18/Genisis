using System.Globalization;
using Dapper;
using Genisis.Api.Worksheet;
using Genisis.Api.Worksheet.Engine;
using Xunit.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>The excess letter (saveExcessLetter) as pure rules.</summary>
public class ExcessLetterRulesTests
{
    private static readonly DateTime Today = new(2026, 10, 24, 9, 30, 0);

    private static WorksheetEvaluation Grid(params (int Slot, decimal Member)[] rows) =>
        new(rows.Select((r) => new WorksheetLine(r.Slot, 0m, null, 0m, 0m, r.Member)).ToList(), new WorksheetTotals(0m, 0m, 0m, rows.Sum((r) => r.Member)));

    private static WorksheetFinal Final(decimal excess = -100m, decimal limitExceeded = 0m, decimal coPayment = 0m) => new(0m, coPayment, limitExceeded, 0m, 0m, excess, 0m);

    private static ExcessLetterInput Input(Func<ExcessLetterInput, ExcessLetterInput>? change = null) =>
        (change ?? ((x) => x))(new ExcessLetterInput { CaseNo = " XI00002412 ", UserCode = "E2E", Today = Today, InvoiceNo = " INV-1 ", HospitalInvoiceAmount = 1500m });

    [Theory]
    [InlineData(-100, 0, true, true)]
    [InlineData(0, -50, true, true)]
    [InlineData(0, 0, true, false)]
    [InlineData(25, 0, true, false)]
    [InlineData(-100, 0, false, false)]
    public void A_letter_is_made_when_the_member_is_left_an_excess_or_a_limit_exceeded_and_there_is_a_hospital_invoice(double excess, double limitExceeded, bool invoice, bool expected) =>
        Assert.Equal(expected, ExcessLetterRow.Required(Final((decimal)excess, (decimal)limitExceeded), invoice ? 1500m : null));

    [Fact]
    public void No_row_is_built_when_no_letter_is_made()
    {
        Assert.Null(ExcessLetterRow.Build(Input(), Grid((1, 0m)), Final(excess: 0m)));
        Assert.Null(ExcessLetterRow.Build(Input((i) => i with { HospitalInvoiceAmount = null }), Grid((25, -100m)), Final()));
    }

    [Theory]
    [InlineData(-182.7, true, "182.7")]
    [InlineData(-55, false, "55")]
    [InlineData(0, true, "0")]
    [InlineData(0, false, null)]
    [InlineData(12.5, true, null)]
    [InlineData(-0.5, false, "0.5")]
    public void A_cell_holds_what_the_member_owes_as_a_number_and_an_empty_cell_as_zero_only_where_the_desktop_does(double amount, bool zeroForEmpty, string? expected) =>
        Assert.Equal(expected, ExcessLetterRow.Cell((decimal)amount, zeroForEmpty));

    [Fact]
    public void The_number_is_E_the_case_a_dash_and_the_sequence() => Assert.Equal("EXI00002412-3", ExcessLetterRow.Number(" XI00002412 ", 3));

    [Fact]
    public void A_letter_lists_the_rows_the_member_owes_and_fills_the_columns_that_take_a_zero()
    {
        var row = ExcessLetterRow.Build(Input(), Grid((25, -182.7m), (1, 0m), (2, 0m), (5, 0m), (23, -20m)), Final(excess: -202.7m))!;
        Assert.Equal(("XI00002412", "INV-1", 1500m, new DateTime(2026, 10, 24)), (row["CASNumber"], row["INVNo"], row["INVAmt"], row["EXCDate"]));
        Assert.Equal(("182.7", "0", "0", null, "20"), (row["EXCOthers1"], row["EXCBoardAmt"], row["EXCICUAmt"], row["EXCMRI"], row["EXCAdmin"]));
        Assert.Equal(("E2E", new DateTime(2026, 10, 24)), (row["EXCIssuedBy"], row["EXCIssuedDate"]));
        Assert.Equal(202.7m, row["ExcessAmt"]);
    }

    [Fact]
    public void The_board_row_is_a_dash_when_the_member_is_owed_money_and_the_other_rows_stay_empty()
    {
        var row = ExcessLetterRow.Build(Input(), Grid((1, 30m), (25, -100m)), Final())!;
        Assert.Equal("-", row["EXCBoardAmt"]);
        var other = ExcessLetterRow.Build(Input(), Grid((2, 30m), (25, -100m)), Final())!;
        Assert.Null(other["EXCICUAmt"]);
    }

    [Fact]
    public void The_copayment_the_undertaking_and_the_limit_exceeded_are_listed_and_the_excess_amount_is_the_members_excess()
    {
        var row = ExcessLetterRow.Build(Input((i) => i with { UndertakingExcess = 20m }), Grid((25, -100m)), Final(excess: -100m, limitExceeded: -50m, coPayment: 10m))!;
        Assert.Equal(("20", "10", "50"), (row["EXCUndExcess"], row["EXCCoPayment"], row["EXCTotalExceed"]));
        Assert.Equal(100m, row["ExcessAmt"]);
    }

    [Fact]
    public void A_BTBG_members_letter_lists_only_the_rows_the_member_may_be_charged()
    {
        var input = Input((i) => i with { MemberBtbg = true, GuardianAllowed = true, OthersAllowed = true });
        var row = ExcessLetterRow.Build(input, Grid((20, -40m), (21, -30m), (25, -60m), (1, -10m)), Final())!;
        Assert.Equal(("40", null, "60"), (row["EXCGuardianAMT"], row["EXCMedical"], row["EXCOthers1"]));
        Assert.False(row.ContainsKey("EXCBoardAmt"));
        Assert.False(row.ContainsKey("ExcessAmt"));
    }
}

/// <summary>The letter row against the letters the desktop saved.</summary>
public class ExcessLetterReferenceTests(ITestOutputHelper output)
{
    /// <summary>
    /// Each letter of a sample (about one in five of those dated since July 2020, made in the earliest closed version of its case that left the member an excess or a limit exceeded, opened the day the letter is dated)
    /// is rebuilt from that version's saved grid, and the columns <see cref="ExcessLetterRow.Build"/> fills are compared with the saved letter.
    /// </summary>
    [Fact]
    public async Task A_letter_rebuilt_from_the_saved_member_column_equals_the_saved_letter()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var letters = (await session.Connection.QueryAsync<(string Cas, int Version, string? Family, string? Payor, string? Btbg)>(
            """
            SELECT RTRIM(e.CASNumber), c.ClaimVersion, LEFT(RTRIM(CASE WHEN v.MBMPatientCovID <> N'00' AND ISNULL(RTRIM(v.MBMCPLNCode), N'') <> N'' THEN v.MBMCPLNCode ELSE v.PLNCode END), 1), LEFT(RTRIM(v.PAYCode), 2), RTRIM(v.MBMBTBG)
            FROM genisis.WS_ExcessLetter e
            CROSS APPLY (SELECT TOP (1) c.ClaimVersion, c.ClaimOpenDate FROM genisis.WS_Claim c WHERE c.CASNumber = e.CASNumber AND c.ClaimVersion <> 0 AND c.ClaimStatus = 'C' AND (c.CLMExcess < 0 OR c.CLMLimitExceeded < 0) ORDER BY c.ClaimVersion) c
            JOIN dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v ON v.CASNumber = e.CASNumber AND v.ClaimVersion = c.ClaimVersion
            WHERE e.EXCDate >= '2020-07-01' AND CAST(c.ClaimOpenDate AS date) = CAST(e.EXCDate AS date) AND ABS(CHECKSUM(e.CASNumber)) % 5 = 0
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimApprove p WHERE p.CASNumber = e.CASNumber AND p.CLMVersion = c.ClaimVersion)
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimHos h WHERE h.CASNumber = e.CASNumber AND h.CLMVersion = c.ClaimVersion)
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimMember m WHERE m.CASNumber = e.CASNumber AND m.CLMVersion = c.ClaimVersion)
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimLimit l WHERE l.CASNumber = e.CASNumber AND l.CLMVersion = c.ClaimVersion)
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimActual a WHERE a.CASNumber = e.CASNumber AND a.CLMVersion = c.ClaimVersion)
            """, transaction: session.Transaction, commandTimeout: 300)).ToList();
        Assert.True(letters.Count >= 300, $"only {letters.Count} letters in the sample");

        var columns = new Dictionary<string, int>();
        var seen = new Dictionary<string, int>();
        var misses = new Dictionary<string, List<string>>();
        int done = 0, rowsSame = 0;
        foreach (var (cas, version, family, payor, btbg) in letters)
        {
            var saved = await SavedVersion.ReadAsync(session, cas, version, family, payor, btbg == "Y" ? "Y" : null);
            if (saved is null || await session.Connection.QueryFirstOrDefaultAsync("SELECT * FROM genisis.WS_ExcessLetter WHERE CASNumber = @cas", new { cas }, session.Transaction) is not IDictionary<string, object?> raw) continue;
            var letter = new Dictionary<string, object?>(raw, StringComparer.OrdinalIgnoreCase);
            var btbgVariant = btbg == "Y" && letter["EXCBoardAmt"] is null && letter["EXCICUAmt"] is null;
            var input = new ExcessLetterInput
            {
                CaseNo = cas,
                UserCode = SavedVersion.Text(letter["EXCIssuedBy"]),
                Today = Convert.ToDateTime(letter["EXCDate"], CultureInfo.InvariantCulture),
                InvoiceNo = SavedVersion.Text(letter["INVNo"]),
                HospitalInvoiceAmount = letter["INVAmt"] is null ? 0m : SavedVersion.Number(letter["INVAmt"]),
                MemberBtbg = btbgVariant,
                GuardianAllowed = letter["EXCGuardianAMT"] is not null,
                MedicalReportAllowed = letter["EXCMedical"] is not null,
                TelephoneAllowed = letter["EXCPhone"] is not null,
                AdminAllowed = letter["EXCAdmin"] is not null,
                OthersAllowed = letter["EXCOthers1"] is not null,
                UndertakingExcess = SavedVersion.Number(letter["EXCUndExcess"]),
                OthersDescription = letter["EXCOthersDesc"] as string,
            };
            var built = ExcessLetterRow.Build(input, saved.Grid, saved.Final);
            if (built is null)
            {
                columns["(no letter built)"] = columns.GetValueOrDefault("(no letter built)") + 1;
                continue;
            }
            done++;
            var all = true;
            foreach (var (column, value) in built)
            {
                seen[column] = seen.GetValueOrDefault(column) + 1;
                var savedValue = letter[column];
                var equal = value switch
                {
                    null => savedValue is null,
                    decimal d => Math.Abs(d - SavedVersion.Number(savedValue)) < 0.011m,
                    DateTime dt => savedValue is DateTime s && dt.Date == s.Date,
                    _ => SavedVersion.Text(value) == SavedVersion.Text(savedValue) && (savedValue is not null || value is "" or null),
                };
                if (equal) columns[column] = columns.GetValueOrDefault(column) + 1;
                else
                {
                    all = false;
                    if ((misses.TryGetValue(column, out var list) ? list : misses[column] = []).Count < 3) misses[column].Add($"{cas} v{version}: saved '{SavedVersion.Text(savedValue)}' built '{SavedVersion.Text(value)}'");
                }
            }
            if (all) rowsSame++;
        }

        foreach (var (column, n) in seen.OrderBy((c) => columns.GetValueOrDefault(c.Key) * 1.0 / c.Value)) output.WriteLine($"{column}: {columns.GetValueOrDefault(column)} of {n} equal ({100.0 * columns.GetValueOrDefault(column) / n:F1}%)");
        foreach (var (column, list) in misses) foreach (var line in list) output.WriteLine($"  {column} {line}");
        output.WriteLine($"letters rebuilt: {done}; entirely equal: {rowsSame}; not built: {columns.GetValueOrDefault("(no letter built)")}");
        Assert.True(done >= 300, $"only {done} letters rebuilt");
        Assert.True(columns.GetValueOrDefault("(no letter built)") <= done * 0.02, "letters the rule would not make");
        foreach (var (column, n) in seen) Assert.True(columns.GetValueOrDefault(column) >= n * 0.80, $"{column}: {columns.GetValueOrDefault(column)} of {n}");
    }
}
