using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Invoice;
using Xunit.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Invoice Processing's save against the database. Every write is made in a session that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class InvoiceEntryDatabaseTests(ITestOutputHelper output)
{
    private static InvoiceEntryService Service() => new(TestDb.Sessions(), new InvoiceRepository(), new InvoiceEntryRepository(), TimeProvider.System);

    private static async Task<(string Cas, string Hospital)> ACaseAsync(Data.IDbSession session) =>
        await session.Connection.QueryFirstAsync<(string, string)>(
            "SELECT TOP (1) RTRIM(c.CASNumber), RTRIM(c.CASHOSCode) FROM dbo.CAS c JOIN dbo.MBM m ON m.MBMNumber = c.MBMNumber WHERE c.CASNumber NOT LIKE 'ZZ%' AND c.CASDischargeDate IS NOT NULL " +
            "AND c.CASHOSCode IS NOT NULL AND EXISTS (SELECT 1 FROM genisis.WS_ClaimInvoice i WHERE i.CASNumber = c.CASNumber) AND EXISTS (SELECT 1 FROM dbo.HOS h WHERE h.HOSCode = c.CASHOSCode) ORDER BY c.CASNumber DESC",
            transaction: session.Transaction);

    private static InvoiceForm Form(string cas, string payTo, string payee) => new()
    {
        CaseNo = cas, InvoiceNo = "ZZ-" + Guid.NewGuid().ToString("N")[..10], InvoiceDate = new DateTime(2021, 1, 5), PayTo = payTo, Payee = payee, StayType = "I", RoomType = "", ReceiptMode = payTo == "M" ? "EF" : "",
        ReceiptDate = payTo == "M" ? new DateTime(2021, 1, 6) : null, ExcessAmount = payTo == "H" ? 20 : 0, InitialAmount = 100, UhcNo = "U1",
        Amounts = new() { ["CLMBoardDay"] = 2, ["CLMRB"] = 100, ["CLMBoardAmt"] = 200, ["CLMSurgicalFee"] = 300, ["CLMSurgicalWard"] = 50, ["CLMServices"] = 40.5m, ["CLMPreXRay"] = 10 },
        Texts = new() { ["CLMOther1Desc"] = "a note", ["CLMMedRptMBM"] = "Y" }, Remarks = "a remark for the case",
    };

    [Fact]
    public async Task An_invoice_is_added_with_its_sums_and_loaded_again_as_it_was_saved()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var (cas, hospital) = await ACaseAsync(session);
        var service = Service();
        var form = Form(cas, "H", hospital);

        var saved = await service.SaveAsync(session, form, "ZZTEST");
        Assert.NotNull(saved.Index);
        Assert.Equal(200m + 40.5m + 350m + 10m, saved.Calculation!.InvoiceAmount);
        Assert.Equal(saved.Calculation.InvoiceAmount - 20m, saved.Calculation.PayAmount);
        Assert.Equal(350m, saved.Calculation.Amounts["CLMSurgicalAmt"]);

        var loaded = await service.FormAsync(session, cas, saved.Index!.Value);
        Assert.Equal((form.InvoiceNo, "H", hospital, "I", 20m, 100m, "U1"), (loaded.Form.InvoiceNo, loaded.Form.PayTo, loaded.Form.Payee, loaded.Form.StayType, loaded.Form.ExcessAmount, loaded.Form.InitialAmount, loaded.Form.UhcNo));
        Assert.Equal((350m, 10m, 2m), (loaded.Form.Amounts["CLMSurgicalAmt"], loaded.Form.Amounts["CLMPreHSAmt"], loaded.Form.Amounts["CLMBoardDay"]));
        Assert.Equal(("a note", "Y", "N"), (loaded.Form.Texts["CLMOther1Desc"], loaded.Form.Texts["CLMMedRptMBM"], loaded.Form.Texts["CLMTelChargesMBM"]));
        Assert.Equal("a remark for the case", loaded.Form.Remarks);
        Assert.False(loaded.InWorksheet);
        Assert.False(loaded.Bordxed);

        var header = (await new InvoiceRepository().InvoicesAsync(session, cas)).Single((i) => i.Index == saved.Index);
        Assert.Equal((saved.Calculation.InvoiceAmount, saved.Calculation.PayAmount, "ZZTEST"), (header.Amount, header.PayAmount, header.EnteredBy));
        var next = await session.Connection.ExecuteScalarAsync<int>("EXEC genisis.Invoice_NextVersion @cas", new { cas }, session.Transaction);
        Assert.Equal(next.ToString(), header.WorksheetVersion);
        Assert.Equal(1, await session.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.WS_ClaimInvoiceActualGST WHERE INVIndex = @i AND CLMBoardAmt = 0 AND CLMServices = 0", new { i = saved.Index }, session.Transaction));
        Assert.Equal(1, await session.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.WS_ClaimInvoiceNotCover WHERE INVIndex = @i", new { i = saved.Index }, session.Transaction));
    }

    [Fact]
    public async Task A_paid_member_gets_a_pay_amount_of_zero_and_the_other_header_variants_are_used_by_their_dates()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var (cas, _) = await ACaseAsync(session);
        foreach (var (receipt, followUp) in new[] { (true, true), (true, false), (false, true), (false, false) })
        {
            var form = Form(cas, "M", "THE MEMBER");
            form.ReceiptDate = receipt ? new DateTime(2021, 1, 6) : null;
            form.ReceiptMode = receipt ? "EF" : "PM";
            form.FollowUpDate = followUp ? new DateTime(2021, 2, 1) : null;
            if (!receipt) { form.PayTo = "H"; form.Payee = (await ACaseAsync(session)).Item2; form.ExcessAmount = 0; }
            var saved = await Service().SaveAsync(session, form, "ZZTEST");
            var loaded = await Service().FormAsync(session, cas, saved.Index!.Value);
            Assert.Equal((receipt ? form.ReceiptDate : null, followUp ? form.FollowUpDate : null), (loaded.Form.ReceiptDate, loaded.Form.FollowUpDate));
            if (form.PayTo == "M") Assert.Equal(0m, saved.Calculation!.PayAmount);
        }
    }

    [Fact]
    public async Task Another_hospital_is_asked_about_first_and_nothing_is_saved_until_it_is_confirmed()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var (cas, hospital) = await ACaseAsync(session);
        var other = await session.Connection.ExecuteScalarAsync<string>("SELECT TOP (1) RTRIM(HOSCode) FROM dbo.HOS WHERE RTRIM(HOSCode) <> @hospital ORDER BY HOSCode", new { hospital }, session.Transaction);
        var form = Form(cas, "H", other!);
        var before = (await new InvoiceRepository().InvoicesAsync(session, cas)).Count;

        var asked = await Service().SaveAsync(session, form, "ZZTEST");
        Assert.Equal((null, InvoiceMessages.DifferentHospital), (asked.Index, asked.Confirm));
        Assert.Equal(before, (await new InvoiceRepository().InvoicesAsync(session, cas)).Count);

        form.ConfirmDifferentHospital = true;
        Assert.NotNull((await Service().SaveAsync(session, form, "ZZTEST")).Index);
        Assert.Equal(before + 1, (await new InvoiceRepository().InvoicesAsync(session, cas)).Count);
    }

    [Fact]
    public async Task The_same_invoice_number_cannot_be_added_twice_and_a_missing_entry_stops_the_save_before_anything_is_written()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var (cas, hospital) = await ACaseAsync(session);
        var form = Form(cas, "H", hospital);
        await Service().SaveAsync(session, form, "ZZTEST");
        var twice = await Assert.ThrowsAsync<ConflictException>(() => Service().SaveAsync(session, Form(cas, "H", hospital) with { InvoiceNo = form.InvoiceNo }, "ZZTEST"));
        Assert.Equal(InvoiceMessages.DuplicateInvoice, twice.Message);

        var count = (await new InvoiceRepository().InvoicesAsync(session, cas)).Count;
        var blank = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().SaveAsync(session, Form(cas, "H", hospital) with { InitialAmount = null }, "ZZTEST"));
        Assert.Equal(InvoiceMessages.NoInitialAmount, blank.Message);
        Assert.Equal(count, (await new InvoiceRepository().InvoicesAsync(session, cas)).Count);
        var unknown = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().SaveAsync(session, Form("NO SUCH CASE", "H", hospital), "ZZTEST"));
        Assert.Equal(InvoiceMessages.NoCase, unknown.Message);
    }

    [Fact]
    public async Task An_invoice_is_changed_and_removed_with_all_of_its_rows_and_one_that_is_used_cannot_be()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var (cas, hospital) = await ACaseAsync(session);
        var service = Service();
        var saved = await service.SaveAsync(session, Form(cas, "H", hospital), "ZZTEST");
        var index = saved.Index!.Value;

        var loaded = await service.FormAsync(session, cas, index);
        loaded.Form.Amounts["CLMServices"] = 99m;
        loaded.Form.ExcessAmount = 5;
        var changed = await service.SaveAsync(session, loaded.Form, "SOMEONE ELSE");
        Assert.Equal(index, changed.Index);
        var again = await service.FormAsync(session, cas, index);
        Assert.Equal((99m, 5m, changed.Calculation!.InvoiceAmount), (again.Form.Amounts["CLMServices"], again.Form.ExcessAmount, again.Calculation.InvoiceAmount));
        Assert.Equal("ZZTEST", (await new InvoiceRepository().InvoicesAsync(session, cas)).Single((i) => i.Index == index).EnteredBy);

        await service.RemoveAsync(session, cas, index);
        foreach (var table in new[] { "ClaimInvoice", "ClaimInvoiceActual", "ClaimInvoiceActualGST", "ClaimInvoiceActualOthers", "ClaimInvoiceNotCover" })
            Assert.Equal(0, await session.Connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM genisis.WS_{table} WHERE INVIndex = @index", new { index }, session.Transaction));
        await Assert.ThrowsAsync<NotFoundException>(() => service.FormAsync(session, cas, index));
    }

    [Fact]
    public async Task An_invoice_a_worksheet_has_used_or_that_is_bordxed_is_refused()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var used = await session.Connection.QueryFirstAsync<(string Cas, int Index)>(
            "SELECT TOP (1) RTRIM(i.CASNumber), i.INVIndex FROM genisis.WS_ClaimInvoice i JOIN dbo.CAS c ON c.CASNumber = i.CASNumber JOIN dbo.MBM m ON m.MBMNumber = c.MBMNumber WHERE i.INVWSed = 1 AND i.CASNumber NOT LIKE 'ZZ%' " +
            "AND (i.INVBordxRefNo IS NULL OR RTRIM(i.INVBordxRefNo) = '') AND c.CASDischargeDate IS NOT NULL ORDER BY i.INVIndex", transaction: session.Transaction);
        var bordxed = await session.Connection.QueryFirstAsync<(string Cas, int Index)>(
            "SELECT TOP (1) RTRIM(i.CASNumber), i.INVIndex FROM genisis.WS_ClaimInvoice i JOIN dbo.CAS c ON c.CASNumber = i.CASNumber JOIN dbo.MBM m ON m.MBMNumber = c.MBMNumber WHERE RTRIM(i.INVBordxRefNo) <> '' AND i.INVBordxRefNo IS NOT NULL " +
            "AND i.CASNumber NOT LIKE 'ZZ%' AND c.CASDischargeDate IS NOT NULL ORDER BY i.INVIndex", transaction: session.Transaction);
        var service = Service();

        var form = (await service.FormAsync(session, used.Cas, used.Index)).Form;
        Assert.Equal(InvoiceMessages.ChangeInWorksheet, (await Assert.ThrowsAsync<BusinessRuleException>(() => service.SaveAsync(session, form, "ZZTEST"))).Message);
        Assert.Equal(InvoiceMessages.UsedInWorksheet, (await Assert.ThrowsAsync<BusinessRuleException>(() => service.RemoveAsync(session, used.Cas, used.Index))).Message);
        var bordxForm = (await service.FormAsync(session, bordxed.Cas, bordxed.Index)).Form;
        Assert.Equal(InvoiceMessages.Bordxed, (await Assert.ThrowsAsync<BusinessRuleException>(() => service.SaveAsync(session, bordxForm, "ZZTEST"))).Message);
        Assert.True((await service.FormAsync(session, bordxed.Cas, bordxed.Index)).Bordxed);
    }

    [Fact]
    public async Task The_parameter_lists_in_the_code_are_those_of_the_live_procedures()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        foreach (var (synonym, expected) in InvoiceProcedures.Parameters)
        {
            var live = (await session.Connection.QueryAsync<string>(
                "SELECT REPLACE(p.name, '@', '') FROM sys.synonyms s JOIN HISWorkSheet.sys.procedures pr ON pr.name = PARSENAME(s.base_object_name, 1) " +
                "JOIN HISWorkSheet.sys.parameters p ON p.object_id = pr.object_id WHERE s.name = @synonym AND s.schema_id = SCHEMA_ID('genisis') ORDER BY p.parameter_id",
                new { synonym }, session.Transaction)).ToList();
            Assert.Equal(expected, live);
        }
        Assert.Equal(16, InvoiceProcedures.Parameters.Count);
    }

    [Fact]
    public async Task Every_column_the_form_writes_is_a_column_of_its_table_in_any_capitals()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        foreach (var (table, columns) in new[] { ("ClaimInvoiceActual", InvoiceFields.ActualColumns), ("ClaimInvoiceActualGST", InvoiceFields.ActualColumns), ("ClaimInvoiceActualOthers", InvoiceFields.OthersColumns), ("ClaimInvoiceNotCover", InvoiceFields.NotCoverColumns) })
        {
            var live = (await session.Connection.QueryAsync<string>("SELECT c.name FROM HISWorkSheet.sys.columns c WHERE c.object_id = OBJECT_ID(@name)", new { name = "HISWorkSheet.dbo." + table }, session.Transaction)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.All(columns, (c) => Assert.Contains(c, live));
        }
    }

    /// <summary>
    /// Saving a saved invoice with its own values must leave it as it was: the mapping of every form field to its column, the sums, the hospital sums and the GST row are all exercised at once. Of the
    /// invoices no worksheet has used and that are not bordxed, a sample is loaded, saved again and compared column by column with what was there (the invoice amount and the amount to pay are worked
    /// out again, so they are compared separately).
    /// </summary>
    [Fact]
    public async Task Saving_a_saved_invoice_again_leaves_every_column_as_it_was()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var sample = (await session.Connection.QueryAsync<(string Cas, int Index)>(
            "SELECT TOP (80) RTRIM(i.CASNumber), i.INVIndex FROM genisis.WS_ClaimInvoice i JOIN dbo.CAS c ON c.CASNumber = i.CASNumber JOIN dbo.MBM m ON m.MBMNumber = c.MBMNumber " +
            "WHERE ISNULL(i.INVWSed, 0) = 0 AND (i.INVBordxRefNo IS NULL OR RTRIM(i.INVBordxRefNo) = '') AND i.CASNumber NOT LIKE 'ZZ%' AND c.CASDischargeDate IS NOT NULL AND i.InvNo IS NOT NULL AND RTRIM(i.InvNo) <> '' " +
            "AND i.INVPayTo IS NOT NULL AND RTRIM(i.INVPayee) <> '' AND EXISTS (SELECT 1 FROM genisis.WS_ClaimInvoiceActual a WHERE a.INVIndex = i.INVIndex) AND EXISTS (SELECT 1 FROM genisis.WS_ClaimInvoiceActualOthers o WHERE o.INVIndex = i.INVIndex) " +
            "AND EXISTS (SELECT 1 FROM genisis.WS_ClaimInvoiceNotCover n WHERE n.INVIndex = i.INVIndex) AND ABS(CHECKSUM(i.INVIndex)) % 10 = 0 ORDER BY i.INVIndex DESC", transaction: session.Transaction, commandTimeout: 120)).ToList();
        Assert.True(sample.Count >= 20, $"only {sample.Count} invoices could be re-saved");

        var service = Service();
        var repository = new InvoiceEntryRepository();
        var differences = new Dictionary<string, int>();
        int same = 0, refused = 0;
        foreach (var (cas, index) in sample)
        {
            var before = await repository.LoadAsync(session, index);
            var form = (await service.FormAsync(session, cas, index)).Form;
            try { await service.SaveAsync(session, form, "ZZTEST"); }
            catch (BusinessRuleException ex) { refused++; output.WriteLine($"{cas} {index}: {ex.Message}"); continue; }
            var after = await repository.LoadAsync(session, index);
            var bad = new List<string>();
            foreach (var (name, b, a) in new[] { ("ClaimInvoice", before.Header, after.Header), ("ClaimInvoiceActual", before.Actual, after.Actual), ("ClaimInvoiceActualOthers", before.Others, after.Others), ("ClaimInvoiceNotCover", before.NotCover, after.NotCover) })
                foreach (var (column, value) in b!)
                {
                    if (name == "ClaimInvoice" && column is "INVAmount" or "InvPayAmt" or "InvPaidByMem" or "IsUpdate") continue;
                    var left = value is null or DBNull ? "" : Convert.ToString(value)!.TrimEnd();
                    var right = a![column] is null or DBNull ? "" : Convert.ToString(a[column])!.TrimEnd();
                    // an amount that was empty is saved as 0, as the desktop's own call does
                    if (left == "") left = right == "" ? "" : decimal.TryParse(right, out var zero) && zero == 0 ? "" : left;
                    if (left != right && !(decimal.TryParse(left, out var l) && decimal.TryParse(right, out var r) && Math.Abs(l - r) < 0.0051m) && !(left == "" && decimal.TryParse(right, out var z) && z == 0)) bad.Add($"{name}.{column}");
                }
            if (bad.Count == 0) same++;
            foreach (var column in bad) differences[column] = differences.GetValueOrDefault(column) + 1;
        }
        output.WriteLine($"{sample.Count} invoices re-saved: {same} unchanged, {refused} refused; columns that changed: {string.Join(", ", differences.OrderByDescending((d) => d.Value).Select((d) => $"{d.Key} x{d.Value}"))}");
        Assert.Equal(0, refused);
        Assert.True(same >= (sample.Count - refused) * 0.97, $"{same} of {sample.Count - refused} unchanged: {string.Join(", ", differences.Select((d) => $"{d.Key} x{d.Value}"))}");
    }
}
