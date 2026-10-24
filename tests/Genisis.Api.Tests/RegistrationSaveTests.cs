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

    /// <summary>Parameter names compared as T-SQL compares them: without regard to case (the desktop's procedure says <c>CLMOthersNotCOPay</c>, the column is <c>CLMOthersNotCoPay</c>).</summary>
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
        const string casChecksum = "SELECT CHECKSUM_AGG(CHECKSUM(CASNumber, CASPending, CASReserveAmount)) FROM dbo.CAS";
        var checksumBefore = await read.Connection.ExecuteScalarAsync<int?>(casChecksum, transaction: read.Transaction, commandTimeout: 300);

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
                var claimRow = ClaimVersionRow.Build(saved.Input with { Version = newVersion }, saved.Grid, saved.Final);
                var rows = new WorksheetVersionRows(
                    claimRow,
                    ClaimOthersRow.Build(saved.Others with { Version = newVersion }),
                    WorksheetRows.Limits(saved.Grid, flat, new WorksheetRows.LimitDays(
                        SavedVersion.Number(saved.Tables["ClaimLimit"]["CLMBoardDays"]), SavedVersion.Number(saved.Tables["ClaimLimit"]["CLMICUDays"]), SavedVersion.Number(saved.Tables["ClaimLimit"]["CLMINHOSPhyDays"]),
                        SavedVersion.Number(saved.Tables["ClaimLimit"]["CLMKIDDCDays"]), SavedVersion.Number(saved.Tables["ClaimLimit"]["CLMCashDays"]), SavedVersion.Number(saved.Tables["ClaimLimit"]["CLMGurdianDays"]))),
                    WorksheetRows.Build(saved.Grid, flat, WorksheetRows.Kind.Actual),
                    WorksheetRows.ActualGst(flat),
                    WorksheetRows.Build(saved.Grid, flat, WorksheetRows.Kind.Approved),
                    WorksheetRows.Build(saved.Grid, flat, WorksheetRows.Kind.Hospital),
                    WorksheetRows.Build(saved.Grid, flat, WorksheetRows.Kind.Member));
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
        foreach (var table in new[] { "ClaimActual", "ClaimActualGST", "ClaimApprove", "ClaimHos", "ClaimMember", "ClaimLimit" })
            Assert.Equal(0, await read.Connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM genisis.WS_{table} WHERE CLMVersion >= @first", new { first = FirstTestVersion }, read.Transaction));
        Assert.Equal(0, await read.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.WS_ClaimOthers WHERE ClaimVersion >= @first", new { first = FirstTestVersion }, read.Transaction));
        Assert.Equal(checksumBefore, await read.Connection.ExecuteScalarAsync<int?>(casChecksum, transaction: read.Transaction, commandTimeout: 300));
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
