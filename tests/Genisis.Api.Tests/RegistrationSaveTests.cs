using System.Globalization;
using Dapper;
using Genisis.Api.Worksheet;
using Genisis.Api.Worksheet.Calculation;
using Genisis.Api.Worksheet.Engine;
using Xunit.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>The small rows of the save as pure rules: the GST copy and the remarks row.</summary>
public class RegistrationRowsRulesTests
{
    [Fact]
    public void The_GST_copy_is_a_dash_in_every_row_the_layout_writes()
    {
        var itemised = WorksheetRows.ActualGst(flat: false);
        Assert.Equal(WorksheetRows.Columns(false).Count, itemised.Count);
        Assert.All(itemised.Values, (v) => Assert.Equal("-", v));
        Assert.Contains("CLMPreHSAmt", itemised.Keys);
        var flat = WorksheetRows.ActualGst(flat: true);
        Assert.Equal(WorksheetRows.Columns(true).Count, flat.Count);
        Assert.Contains("CLMCOPreHSSum", flat.Keys);
        Assert.DoesNotContain("CLMPreHSAmt", flat.Keys);
    }

    [Fact]
    public void The_two_plan_layouts_together_have_31_columns_and_the_limit_table_six_more()
    {
        Assert.Equal(31, WorksheetRows.AllColumns.Count);
        Assert.Equal(31, WorksheetRows.AllColumns.Distinct().Count());
        Assert.All(WorksheetRows.Columns(false).Concat(WorksheetRows.Columns(true)), (c) => Assert.Contains(c.Column, WorksheetRows.AllColumns));
        Assert.Equal(6, WorksheetRows.DayColumns.Count);
        Assert.DoesNotContain(WorksheetRows.DayColumns, (d) => WorksheetRows.AllColumns.Contains(d));
    }

    [Theory]
    [InlineData("TEST", 0, "TEST")]
    [InlineData("TEST", 150, "TEST GROSS RB RATE : RM150")]
    [InlineData("TEST", 150.5, "TEST GROSS RB RATE : RM150.5")]
    [InlineData(null, 0, "")]
    [InlineData("", 99.99, " GROSS RB RATE : RM99.99")]
    public void A_gross_room_rate_is_added_to_the_worksheet_remarks(string? remarks, double rate, string expected) => Assert.Equal(expected, ClaimOthersRow.Remarks(remarks, (decimal)rate));

    private static WorksheetLine Line(int slot, decimal actual, string? limit, decimal approved) => new(slot, actual, limit, approved, approved, 0m);

    [Theory]
    [InlineData("5,000.00", 0, 100, true)]
    [InlineData("5,000.00", 10, 100, false)]
    [InlineData("5,000.00", 0, 0, false)]
    [InlineData("FULL", 0, 100, false)]
    [InlineData("-", 0, 100, false)]
    [InlineData(null, 0, 100, false)]
    [InlineData("0.00", 0, 100, false)]
    public void A_row_is_flagged_when_there_is_a_limit_nothing_was_invoiced_and_something_is_approved(string? limit, double actual, double approved, bool expected) =>
        Assert.Equal(expected, PayMbmStatusRow.Flag(Line(1, (decimal)actual, limit, (decimal)approved)));

    [Fact]
    public void A_missing_row_is_not_flagged_and_every_row_of_the_layout_gets_a_Y_or_N()
    {
        Assert.False(PayMbmStatusRow.Flag(null));
        var grid = new WorksheetEvaluation([Line(1, 0m, "100", 50m), Line(3, 5m, "100", 50m)], new WorksheetTotals(5m, 100m, 100m, 0m));
        var itemised = PayMbmStatusRow.Build(grid, flat: false);
        Assert.Equal(PayMbmStatusRow.Map(false).Count, itemised.Count);
        Assert.Equal("Y", itemised["CLMBoardAmt"]);
        Assert.Equal("N", itemised["CLMServices"]);
        Assert.Equal("N", itemised["CLMICUAmt"]);
        Assert.All(itemised.Values, (v) => Assert.Contains(v, new[] { "Y", "N" }));
    }

    [Fact]
    public void From_row_19_on_the_columns_are_one_row_behind_the_grid_as_in_the_desktops_routine_and_the_layouts_differ_in_rows_8_and_13()
    {
        var itemised = PayMbmStatusRow.Map(false).ToDictionary((c) => c.Slot, (c) => c.Column);
        Assert.Equal(("CLMCashDays", "CLMGuardianAmt", "CLMOthers1", "CLMLodgerTaxAmt"), (itemised[19], itemised[20], itemised[24], itemised[28]));
        Assert.False(itemised.ContainsKey(29));
        Assert.Equal(("CLMSurgicalAmt", "CLMMPreHSAmt"), (itemised[8], itemised[13]));
        var flat = PayMbmStatusRow.Map(true).ToDictionary((c) => c.Slot, (c) => c.Column);
        Assert.Equal(("CLMCOPreHSSum", "CLMCOMPreHSSum"), (flat[8], flat[13]));
        Assert.False(flat.ContainsKey(6));
        Assert.Equal(30, PayMbmStatusRow.AllColumns.Count);
        Assert.Equal(30, PayMbmStatusRow.AllColumns.Distinct().Count());
        Assert.DoesNotContain("CLMTax", PayMbmStatusRow.AllColumns);
    }

    [Fact]
    public void The_remarks_row_has_the_procedures_parameters_and_empty_text_for_what_is_not_given()
    {
        var row = ClaimOthersRow.Build(new ClaimOthersInput { CaseNo = " AB1 ", Version = 4, WorksheetRemarks = "W", Address1 = "A1", PostCode = "50000" });
        Assert.Equal(ClaimOthersRow.Parameters.OrderBy((n) => n, StringComparer.Ordinal), row.Keys.OrderBy((n) => n, StringComparer.Ordinal));
        Assert.Equal(("AB1", 4, "W", "", "A1", "50000", ""), (row["CasNumber"], row["claimversion"], row["CLMWSRemarks"], row["CLMDCNoteRemarks"], row["MBMAdd1"], row["MBMPostCode"], row["CLMAttention"]));
    }
}

/// <summary>
/// The writer of a version against the database. Everything that writes does so inside a transaction that is rolled back; the version numbers used (9001 and up) belong to no case.
/// </summary>
[Collection(DatabaseWriteCollection.Name)]
public class RegistrationSaveDatabaseTests(ITestOutputHelper output)
{
    private const int FirstTestVersion = 9001;

    /// <summary>Parameter names compared as T-SQL compares them, without regard to case (the desktop's procedure says <c>CLMOthersNotCOPay</c>, the column is <c>CLMOthersNotCoPay</c>).</summary>
    private static List<string> Names(IEnumerable<string> names) => names.Select((n) => n.ToLowerInvariant()).OrderBy((n) => n, StringComparer.Ordinal).ToList();

    [Fact]
    public async Task The_procedures_take_the_parameters_the_rows_carry()
    {
        var worksheetDb = TestDb.WorkSheet;
        if (worksheetDb is null) return;
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(worksheetDb);
        await connection.OpenAsync();
        async Task<List<string>> Parameters(string procedure) =>
            (await connection.QueryAsync<string>("SELECT REPLACE(name, '@', '') FROM sys.parameters WHERE object_id = OBJECT_ID(@procedure) ORDER BY parameter_id", new { procedure = $"dbo.{procedure}" })).ToList();

        string[] key = ["CasNumber", "CLMversion"];
        foreach (var procedure in new[] { "Insert_WS_ClaimActual", "Insert_WS_ClaimActualGST", "Insert_WS_ClaimApprove", "Insert_WS_ClaimHOS", "Insert_WS_ClaimMBM" })
            Assert.Equal(Names(key.Concat(WorksheetRows.AllColumns)), Names(await Parameters(procedure)));
        Assert.Equal(Names(key.Concat(WorksheetRows.AllColumns).Concat(WorksheetRows.DayColumns)), Names(await Parameters("Insert_WS_ClaimLimit")));
        Assert.Equal(Names(ClaimOthersRow.Parameters), Names(await Parameters("Insert_WS_ClaimOthers_Jul08")));
        Assert.Equal(Names(ClaimVersionRow.Parameters), Names(await Parameters("Insert_WS_Claim_2012")));

        await using var main = new Microsoft.Data.SqlClient.SqlConnection(TestDb.MainDb);
        await main.OpenAsync();
        var own = (await main.QueryAsync<string>("SELECT REPLACE(name, '@', '') FROM sys.parameters WHERE object_id = OBJECT_ID('genisis.WorksheetSave_PayMbmStatus') ORDER BY parameter_id")).ToList();
        Assert.Equal(Names(new[] { "cas", "version" }.Concat(PayMbmStatusRow.AllColumns)), Names(own));

        var letterParameters = (await main.QueryAsync<string>("SELECT REPLACE(name, '@', '') FROM sys.parameters WHERE object_id = OBJECT_ID('genisis.WorksheetSave_ExcessLetter')")).Select((n) => n.ToLowerInvariant()).ToHashSet();
        var renamed = new Dictionary<string, string> { ["CASNumber"] = "cas", ["INVNo"] = "invoiceNo", ["INVAmt"] = "invoiceAmount", ["EXCDate"] = "date", ["ExcessAmt"] = "excessAmount", ["EXCIssuedBy"] = "issuedBy", ["EXCOthersDesc"] = "othersDesc" };
        var grid = new WorksheetEvaluation([new WorksheetLine(25, 0m, null, 0m, 0m, -10m)], new WorksheetTotals(0m, 0m, 0m, -10m));
        var final = new WorksheetFinal(0m, 0m, 0m, 0m, 0m, -10m, 0m);
        foreach (var btbg in new[] { false, true })
        {
            var row = ExcessLetterRow.Build(new ExcessLetterInput { CaseNo = "X", UserCode = "U", Today = DateTime.Today, HospitalInvoiceAmount = 1m, MemberBtbg = btbg, GuardianAllowed = true, MedicalReportAllowed = true, TelephoneAllowed = true, AdminAllowed = true, OthersAllowed = true }, grid, final)!;
            foreach (var column in row.Keys.Where((k) => k != "EXCIssuedDate")) Assert.Contains((renamed.GetValueOrDefault(column) ?? column).ToLowerInvariant(), letterParameters);
        }
    }

    /// <summary>
    /// The pay-to-member flags worked out from a saved grid (its limit texts, actual and approved cells) against the saved <c>ClaimPayMBMStatus</c> row, on the columns the plan layout writes, for about one saved
    /// version in twenty since July 2020.
    /// </summary>
    [Fact]
    public async Task The_pay_status_worked_out_from_a_saved_grid_equals_the_saved_row()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var sample = await SavedVersion.SampleAsync(session, 20);
        int done = 0, cells = 0, same = 0, rowsSame = 0, yes = 0;
        var misses = new List<string>();
        foreach (var (cas, version, family, payor, btbg) in sample)
        {
            var saved = await SavedVersion.ReadAsync(session, cas, version, family, payor, btbg);
            if (saved is null || await session.Connection.QueryFirstOrDefaultAsync("SELECT * FROM genisis.WS_ClaimPayMBMStatus WHERE CASNumber = @cas AND CLMVersion = @version", new { cas, version }, session.Transaction) is not IDictionary<string, object?> row) continue;
            done++;
            var built = PayMbmStatusRow.Build(saved.Grid, saved.Flat);
            var all = true;
            foreach (var (column, flag) in built)
            {
                cells++;
                if (flag == "Y") yes++;
                if (SavedVersion.Text(row[column]) == flag) same++;
                else
                {
                    all = false;
                    if (misses.Count < 6) misses.Add($"{cas} v{version} {column}: built {flag} saved '{SavedVersion.Text(row[column])}'");
                }
            }
            if (all) rowsSame++;
        }
        output.WriteLine($"{done} versions: {same} of {cells} flags equal, {rowsSame} rows entirely equal, {yes} Y built");
        foreach (var line in misses) output.WriteLine("  " + line);
        Assert.True(done >= 300, $"only {done} versions");
        Assert.True(same >= cells * 0.99, $"{same} of {cells}");
    }

    private static bool Same(object? built, object? saved) => built switch
    {
        null => saved is null,
        decimal d => Math.Abs(d - SavedVersion.Number(saved)) < 0.011m,
        DateTime dt => saved is DateTime s && dt.Date == s.Date,
        int i => i == (int)SavedVersion.Number(saved),
        bool b => saved is bool s && b == s,
        string flag when saved is bool bit && flag is "0" or "1" => (flag == "1") == bit,
        _ => SavedVersion.Text(built) == SavedVersion.Text(saved),
    };

    /// <summary>
    /// Each version of a sample (about one in sixty of those saved since July 2020) is built from its saved tables, written as version 9001 and up of the same case in a transaction, read back, and every cell of the
    /// eight tables compared with what was meant to be written. The version row of Claim is compared column by column; the five grid tables on the cells of the plan layout. Then the transaction is rolled back and
    /// nothing is left.
    /// </summary>
    [Fact]
    public async Task A_saved_version_written_again_as_a_new_version_reads_back_as_it_was_built_and_a_rollback_leaves_nothing()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        var repository = new RegistrationRepository();
        await using var read = await TestDb.Sessions().OpenAsync();
        var sample = await SavedVersion.SampleAsync(read, 60);
        Assert.True(sample.Count >= 100, $"only {sample.Count} versions in the sample");
        var casesBefore = await read.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.WS_Claim WHERE ClaimVersion >= @first", new { first = FirstTestVersion }, read.Transaction);

        int done = 0, cells = 0, cellsSame = 0, claimColumns = 0, claimSame = 0, others = 0, othersSame = 0;
        var misses = new List<string>();
        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var next = FirstTestVersion;
            foreach (var (cas, version, family, payor, btbg) in sample)
            {
                var saved = await SavedVersion.ReadAsync(write, cas, version, family, payor, btbg);
                if (saved is null || saved.Others is null) continue;
                var newVersion = next++;
                var flat = saved.Flat;
                var rows = saved.Rows(newVersion);
                var claimRow = rows.Claim;
                var result = await repository.WriteVersionAsync(write, rows);
                Assert.Equal(0, result.InvoicesMarked);
                done++;

                async Task<IDictionary<string, object?>> Row(string table, string versionColumn) =>
                    (IDictionary<string, object?>)(await write.Connection.QueryFirstAsync($"SELECT * FROM genisis.WS_{table} WHERE CASNumber = @cas AND {versionColumn} = @newVersion", new { cas, newVersion }, write.Transaction));

                foreach (var (table, cellsBuilt) in new (string, Dictionary<string, string>)[] { ("ClaimLimit", rows.Limit), ("ClaimActual", rows.Actual), ("ClaimActualGST", rows.ActualGst), ("ClaimApprove", rows.Approve), ("ClaimHos", rows.Hospital), ("ClaimMember", rows.Member) })
                {
                    var back = await Row(table, "CLMVersion");
                    foreach (var (column, text) in cellsBuilt)
                    {
                        cells++;
                        if (SavedVersion.Text(back[column]) == text.Trim()) cellsSame++;
                        else if (misses.Count < 6) misses.Add($"{cas} v{version} {table}.{column}: built '{text}' read '{SavedVersion.Text(back[column])}'");
                    }
                }

                var statusBack = await Row("ClaimPayMBMStatus", "CLMVersion");
                foreach (var (column, flag) in rows.PayMbmStatus)
                {
                    cells++;
                    if (SavedVersion.Text(statusBack[column]) == flag) cellsSame++;
                    else if (misses.Count < 6) misses.Add($"{cas} v{version} ClaimPayMBMStatus.{column}: built '{flag}' read '{SavedVersion.Text(statusBack[column])}'");
                }
                var claimBack = new Dictionary<string, object?>(await Row("Claim", "ClaimVersion"), StringComparer.OrdinalIgnoreCase);
                foreach (var (parameter, value) in claimRow)
                {
                    claimColumns++;
                    var column = parameter switch { "claimversion" => "ClaimVersion", "CaseMgmtFees" => "CLMCaseMgmtFees", _ => parameter };
                    if (Same(value, claimBack[column])) claimSame++;
                    else if (misses.Count < 6) misses.Add($"{cas} v{version} Claim.{column}: built '{value}' read '{SavedVersion.Text(claimBack[column])}'");
                }
                var othersBack = new Dictionary<string, object?>(await Row("ClaimOthers", "ClaimVersion"), StringComparer.OrdinalIgnoreCase);
                foreach (var (parameter, value) in rows.Others)
                {
                    others++;
                    if (Same(value, othersBack[parameter == "claimversion" ? "ClaimVersion" : parameter])) othersSame++;
                    else if (misses.Count < 6) misses.Add($"{cas} v{version} ClaimOthers.{parameter}: built '{value}' read '{SavedVersion.Text(othersBack[parameter == "claimversion" ? "ClaimVersion" : parameter])}'");
                }

                var pending = await write.Connection.ExecuteScalarAsync<string>("SELECT RTRIM(CASPending) FROM dbo.CAS WHERE CASNumber = @cas", new { cas }, write.Transaction);
                var reserve = await write.Connection.ExecuteScalarAsync<decimal?>("SELECT CASReserveAmount FROM dbo.CAS WHERE CASNumber = @cas", new { cas }, write.Transaction);
                Assert.Equal("N", pending);
                Assert.Equal(0m, reserve ?? 0m);
            }
        }

        output.WriteLine($"{done} versions written: {cellsSame} of {cells} grid cells, {claimSame} of {claimColumns} Claim columns and {othersSame} of {others} ClaimOthers columns read back as built");
        foreach (var line in misses) output.WriteLine("  " + line);
        Assert.True(done >= 100, $"only {done} versions could be written");
        Assert.Equal(cells, cellsSame);
        Assert.Equal(claimColumns, claimSame);
        Assert.Equal(others, othersSame);

        Assert.Equal(casesBefore, await read.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.WS_Claim WHERE ClaimVersion >= @first", new { first = FirstTestVersion }, read.Transaction));
        foreach (var table in new[] { "ClaimActual", "ClaimActualGST", "ClaimApprove", "ClaimHos", "ClaimMember", "ClaimLimit", "ClaimPayMBMStatus" })
            Assert.Equal(0, await read.Connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM genisis.WS_{table} WHERE CLMVersion >= @first", new { first = FirstTestVersion }, read.Transaction));
        Assert.Equal(0, await read.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.WS_ClaimOthers WHERE ClaimVersion >= @first", new { first = FirstTestVersion }, read.Transaction));
    }

    /// <summary>
    /// A case that has an excess version and no letter: the first save adds the letter with the number E, the case, a dash and 1 (or the sequence after the one it has); the next save that leaves an excess moves
    /// the sequence on and leaves the letter as it was; a save without an excess touches neither. All in a transaction that is rolled back.
    /// </summary>
    [Fact]
    public async Task An_excess_letter_is_added_once_and_the_sequence_moves_on_at_every_save_that_leaves_an_excess()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        var repository = new RegistrationRepository();
        await using var read = await TestDb.Sessions().OpenAsync();
        var candidate = await read.Connection.QueryFirstOrDefaultAsync<(string Cas, int Version, string? Family, string? Payor, string? Btbg, string? InvoiceNo, decimal InvoiceAmount)?>(
            """
            SELECT TOP (1) RTRIM(c.CASNumber), c.ClaimVersion, LEFT(RTRIM(CASE WHEN v.MBMPatientCovID <> N'00' AND ISNULL(RTRIM(v.MBMCPLNCode), N'') <> N'' THEN v.MBMCPLNCode ELSE v.PLNCode END), 1), LEFT(RTRIM(v.PAYCode), 2), RTRIM(v.MBMBTBG),
                   (SELECT TOP (1) RTRIM(i.INVNo) FROM genisis.WS_ClaimInvoice i WHERE i.CASNumber = c.CASNumber AND i.INVPayTo = 'H' ORDER BY i.INVIndex),
                   (SELECT TOP (1) i.INVAmount FROM genisis.WS_ClaimInvoice i WHERE i.CASNumber = c.CASNumber AND i.INVPayTo = 'H' ORDER BY i.INVIndex)
            FROM genisis.WS_Claim c JOIN dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v ON v.CASNumber = c.CASNumber AND v.ClaimVersion = c.ClaimVersion
            WHERE c.ClaimVersion <> 0 AND c.ClaimStatus = 'C' AND c.ClaimOpenDate >= '2020-07-01' AND (c.CLMExcess < 0 OR c.CLMLimitExceeded < 0) AND c.CASNumber NOT LIKE 'ZZ%'
              AND NOT EXISTS (SELECT 1 FROM genisis.WS_ExcessLetter e WHERE e.CASNumber = c.CASNumber)
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimInvoice i WHERE i.CASNumber = c.CASNumber AND i.INVPayTo = 'H')
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimApprove p WHERE p.CASNumber = c.CASNumber AND p.CLMVersion = c.ClaimVersion)
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimHos h WHERE h.CASNumber = c.CASNumber AND h.CLMVersion = c.ClaimVersion)
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimMember m WHERE m.CASNumber = c.CASNumber AND m.CLMVersion = c.ClaimVersion)
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimLimit l WHERE l.CASNumber = c.CASNumber AND l.CLMVersion = c.ClaimVersion)
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimActual a WHERE a.CASNumber = c.CASNumber AND a.CLMVersion = c.ClaimVersion)
            ORDER BY c.CASNumber DESC
            """, transaction: read.Transaction, commandTimeout: 120);
        Assert.NotNull(candidate);
        var (cas, version, family, payor, btbg, invoiceNo, invoiceAmount) = candidate.Value;
        var saved = await SavedVersion.ReadAsync(read, cas, version, family, payor, btbg);
        Assert.NotNull(saved);
        var letter = new ExcessLetterInput { CaseNo = cas, UserCode = "E2E", Today = new DateTime(2026, 10, 24), InvoiceNo = invoiceNo, HospitalInvoiceAmount = invoiceAmount, MemberBtbg = btbg == "Y" };
        var sequenceBefore = await read.Connection.ExecuteScalarAsync<int?>("SELECT EXCSequence FROM genisis.WS_ExcessSequence WHERE CASNumber = @cas", new { cas }, read.Transaction);

        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var first = await repository.WriteVersionAsync(write, saved.Rows(9001, letter));
            Assert.True(first.ExcessLetterAdded);
            Assert.Equal($"E{cas}-{(sequenceBefore ?? 0) + 1}", first.ExcessNumber);
            var row = new Dictionary<string, object?>((IDictionary<string, object?>)await write.Connection.QueryFirstAsync("SELECT * FROM genisis.WS_ExcessLetter WHERE CASNumber = @cas", new { cas }, write.Transaction), StringComparer.OrdinalIgnoreCase);
            var built = saved.Rows(9001, letter).ExcessLetter!;
            Assert.Equal(first.ExcessNumber, SavedVersion.Text(row["EXCNumber"]));
            foreach (var (column, value) in built)
            {
                var same = value switch
                {
                    null => row[column] is null,
                    decimal d => Math.Abs(d - SavedVersion.Number(row[column])) < 0.011m,
                    DateTime dt => row[column] is DateTime s && dt.Date == s.Date,
                    _ => SavedVersion.Text(value) == SavedVersion.Text(row[column]),
                };
                Assert.True(same, $"{column}: built '{value}' read '{SavedVersion.Text(row[column])}'");
            }

            var second = await repository.WriteVersionAsync(write, saved.Rows(9002, letter));
            Assert.False(second.ExcessLetterAdded);
            Assert.Equal($"E{cas}-{(sequenceBefore ?? 0) + 2}", second.ExcessNumber);
            Assert.Equal(first.ExcessNumber, await write.Connection.ExecuteScalarAsync<string>("SELECT RTRIM(EXCNumber) FROM genisis.WS_ExcessLetter WHERE CASNumber = @cas", new { cas }, write.Transaction));
            Assert.Equal(1, await write.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.WS_ExcessLetter WHERE CASNumber = @cas", new { cas }, write.Transaction));

            var none = await repository.WriteVersionAsync(write, saved.Rows(9003));
            Assert.Null(none.ExcessNumber);
            Assert.False(none.ExcessLetterAdded);
            Assert.Equal((sequenceBefore ?? 0) + 2, await write.Connection.ExecuteScalarAsync<int>("SELECT EXCSequence FROM genisis.WS_ExcessSequence WHERE CASNumber = @cas", new { cas }, write.Transaction));
        }

        Assert.Equal(0, await read.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.WS_ExcessLetter WHERE CASNumber = @cas", new { cas }, read.Transaction));
        Assert.Equal(sequenceBefore, await read.Connection.ExecuteScalarAsync<int?>("SELECT EXCSequence FROM genisis.WS_ExcessSequence WHERE CASNumber = @cas", new { cas }, read.Transaction));
        Assert.Equal(0, await read.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion >= @first", new { cas, first = FirstTestVersion }, read.Transaction));
    }

    /// <summary>The routines that update rows other than the version's own: the invoices of a version, the case's pending flag and its reserve.</summary>
    [Fact]
    public async Task The_invoices_of_the_version_are_marked_and_a_void_worksheet_bordxes_them_under_VOID()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var invoice = await read.Connection.QueryFirstAsync<(string Cas, string Version, int Count)>(
            "SELECT TOP (1) RTRIM(CASNumber), RTRIM(INVWSversion), COUNT(*) FROM genisis.WS_ClaimInvoice WHERE CASNumber NOT LIKE 'ZZ%' AND INVWSversion NOT LIKE '%[^0-9]%' AND INVWSversion <> '' GROUP BY CASNumber, INVWSversion HAVING COUNT(*) >= 2 ORDER BY CASNumber DESC",
            transaction: read.Transaction);
        var version = int.Parse(invoice.Version, CultureInfo.InvariantCulture);
        var before = (await read.Connection.QueryAsync<(bool Wsed, string? Ref, bool? Bordxed)>(
            "SELECT INVWSed, INVBordxRefNo, INVBordxed FROM genisis.WS_ClaimInvoice WHERE CASNumber = @Cas AND INVWSversion = @Version", new { invoice.Cas, invoice.Version }, read.Transaction)).ToList();

        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var marked = await write.Connection.ExecuteScalarAsync<int>("genisis.WorksheetSave_MarkInvoices", new { cas = invoice.Cas, version, @void = false }, write.Transaction, commandType: System.Data.CommandType.StoredProcedure);
            Assert.Equal(invoice.Count, marked);
            var plain = (await write.Connection.QueryAsync<(bool Wsed, string? Ref, bool? Bordxed)>(
                "SELECT INVWSed, INVBordxRefNo, INVBordxed FROM genisis.WS_ClaimInvoice WHERE CASNumber = @Cas AND INVWSversion = @Version", new { invoice.Cas, invoice.Version }, write.Transaction)).ToList();
            Assert.All(plain, (r) => Assert.True(r.Wsed));
            Assert.Equal(before.Select((r) => (r.Ref, r.Bordxed)), plain.Select((r) => (r.Ref, r.Bordxed)));

            await write.Connection.ExecuteScalarAsync<int>("genisis.WorksheetSave_MarkInvoices", new { cas = invoice.Cas, version, @void = true }, write.Transaction, commandType: System.Data.CommandType.StoredProcedure);
            var voided = (await write.Connection.QueryAsync<(bool Wsed, string? Ref, bool? Bordxed)>(
                "SELECT INVWSed, INVBordxRefNo, INVBordxed FROM genisis.WS_ClaimInvoice WHERE CASNumber = @Cas AND INVWSversion = @Version", new { invoice.Cas, invoice.Version }, write.Transaction)).ToList();
            Assert.All(voided, (r) => Assert.Equal((true, "VOID", true), (r.Wsed, r.Ref?.Trim(), r.Bordxed)));
            Assert.Equal(0, await write.Connection.ExecuteScalarAsync<int>("genisis.WorksheetSave_MarkInvoices", new { cas = "NO SUCH CASE", version, @void = false }, write.Transaction, commandType: System.Data.CommandType.StoredProcedure));
        }

        var after = (await read.Connection.QueryAsync<(bool Wsed, string? Ref, bool? Bordxed)>(
            "SELECT INVWSed, INVBordxRefNo, INVBordxed FROM genisis.WS_ClaimInvoice WHERE CASNumber = @Cas AND INVWSversion = @Version", new { invoice.Cas, invoice.Version }, read.Transaction)).ToList();
        Assert.Equal(before, after);
    }
}
