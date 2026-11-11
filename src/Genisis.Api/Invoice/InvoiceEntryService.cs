using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Invoice;

/// <summary>The rows of a saved invoice, as column names and values.</summary>
/// <param name="Header">The <c>ClaimInvoice</c> row.</param>
/// <param name="Actual">The <c>ClaimInvoiceActual</c> row.</param>
/// <param name="Others">The <c>ClaimInvoiceActualOthers</c> row.</param>
/// <param name="NotCover">The <c>ClaimInvoiceNotCover</c> row.</param>
public record InvoiceStored(IDictionary<string, object?>? Header, IDictionary<string, object?>? Actual, IDictionary<string, object?>? Others, IDictionary<string, object?>? NotCover);

/// <summary>A hospital the invoice can be paid to.</summary>
/// <param name="Code">Hospital code.</param>
/// <param name="Name">Hospital name.</param>
/// <param name="DiscountIndicator"><c>Y</c> when the hospital owes a discount (payor AX is reminded to give it).</param>
public record InvoiceHospital(string Code, string? Name, string? DiscountIndicator);

/// <summary>Writes the invoices of a case through the legacy procedures of the worksheet database.</summary>
public interface IInvoiceEntryRepository
{
    /// <summary>The worksheet version a new invoice is attached to.</summary>
    Task<int> NextVersionAsync(IDbSession session, string caseNo);

    /// <summary>The four rows of a saved invoice.</summary>
    Task<InvoiceStored> LoadAsync(IDbSession session, int invoiceIndex);

    /// <summary>The remark kept for the case.</summary>
    Task<string?> RemarksAsync(IDbSession session, string caseNo);

    /// <summary>Adds or replaces the case's remark.</summary>
    Task SaveRemarksAsync(IDbSession session, string caseNo, string? remarks);

    /// <summary>The key of the invoice with this number just added to the case.</summary>
    Task<int?> KeyAsync(IDbSession session, string caseNo, string invoiceNo);

    /// <summary>A hospital by its code.</summary>
    Task<InvoiceHospital?> HospitalAsync(IDbSession session, string code);

    /// <summary>Whether the plan is a new SM plan.</summary>
    Task<bool> PlanIsNewSmAsync(IDbSession session, string planCode);

    /// <summary>Runs a legacy procedure (by its <c>genisis.WS_*</c> synonym) with the values it has parameters for.</summary>
    Task CallAsync(IDbSession session, string procedure, IReadOnlyDictionary<string, object?> values);

    /// <summary>Deletes an invoice and its rows.</summary>
    Task RemoveAsync(IDbSession session, int invoiceIndex);

    /// <summary>The form's drop-down lists.</summary>
    Task<InvoiceLookups> LookupsAsync(IDbSession session);
}

/// <inheritdoc />
public sealed class InvoiceEntryRepository : IInvoiceEntryRepository
{
    /// <inheritdoc />
    public async Task<int> NextVersionAsync(IDbSession session, string caseNo) =>
        await session.Connection.ProcScalarAsync<int>("genisis.Invoice_NextVersion", new { cas = caseNo }, session.Transaction);

    /// <inheritdoc />
    public async Task<InvoiceStored> LoadAsync(IDbSession session, int invoiceIndex)
    {
        using var reader = await session.Connection.ProcMultipleAsync("genisis.Invoice_Form", new { invoice = invoiceIndex }, session.Transaction);
        IDictionary<string, object?>? Row() => reader.Read().FirstOrDefault() as IDictionary<string, object?>;
        return new InvoiceStored(Row(), Row(), Row(), Row());
    }

    /// <inheritdoc />
    public Task<string?> RemarksAsync(IDbSession session, string caseNo) => session.Connection.ProcScalarAsync<string>("genisis.Invoice_Remarks", new { cas = caseNo }, session.Transaction);

    /// <inheritdoc />
    public Task SaveRemarksAsync(IDbSession session, string caseNo, string? remarks) => session.Connection.ProcExecAsync("genisis.Invoice_SaveRemarks", new { cas = caseNo, remarks = remarks ?? "" }, session.Transaction);

    /// <inheritdoc />
    public Task<int?> KeyAsync(IDbSession session, string caseNo, string invoiceNo) => session.Connection.ProcScalarAsync<int?>("genisis.Invoice_Key", new { cas = caseNo, invoiceNo }, session.Transaction);

    /// <inheritdoc />
    public Task<InvoiceHospital?> HospitalAsync(IDbSession session, string code) => session.Connection.ProcFirstOrDefaultAsync<InvoiceHospital>("genisis.Invoice_Hospital", new { code }, session.Transaction);

    /// <inheritdoc />
    public async Task<bool> PlanIsNewSmAsync(IDbSession session, string planCode) => await session.Connection.ProcScalarAsync<bool>("genisis.Invoice_PlanIsNewSm", new { plan = planCode }, session.Transaction);

    /// <inheritdoc />
    public Task CallAsync(IDbSession session, string procedure, IReadOnlyDictionary<string, object?> values)
    {
        var known = InvoiceProcedures.Parameters[procedure];
        var parameters = new DynamicParameters();
        foreach (var name in known)
            if (values.TryGetValue(name, out var value)) parameters.Add(name, value);
        return session.Connection.ProcExecAsync($"genisis.{procedure}", parameters, session.Transaction);
    }

    /// <inheritdoc />
    public Task RemoveAsync(IDbSession session, int invoiceIndex) => session.Connection.ProcExecAsync("genisis.Invoice_Remove", new { invoice = invoiceIndex }, session.Transaction);

    /// <inheritdoc />
    public async Task<InvoiceLookups> LookupsAsync(IDbSession session)
    {
        var stay = await session.Connection.ProcQueryAsync<InvoiceOption>("genisis.Invoice_StayTypes", null, session.Transaction);
        var modes = await session.Connection.ProcQueryAsync<InvoiceOption>("genisis.Invoice_ReceiptModes", null, session.Transaction);
        var rooms = await session.Connection.ProcQueryAsync<string>("genisis.Invoice_RoomTypes", null, session.Transaction);
        var hospitals = await session.Connection.ProcQueryAsync<InvoiceOption>("genisis.Invoice_Hospitals", null, session.Transaction);
        return new InvoiceLookups(stay, modes, rooms, hospitals);
    }
}

/// <summary>
/// Invoice Processing (legacy <c>frmInvoice</c> / <c>frmInvoiceGST</c>), write side: adds, changes and removes the invoices of a case. The save checks what the desktop's <c>SaveClaim</c> checks, in its
/// order, then in one transaction writes the invoice, its amounts (and the GST row, every amount zero), its entries, what is not covered and the case's remark, as the desktop does.
/// <para>
/// Not done as the desktop does it: changing an invoice that a worksheet has used is refused (the desktop then also saves the worksheet's limits); the case's MMA entry is not rewritten (the desktop's
/// <c>SaveCASOthers</c> saves whatever its MMA dialog holds; the MMA / CPT screen owns that data); the hospital is compared with the case's by code, not by the length of its name; removing an invoice
/// deletes all of its rows, not three of the five.
/// </para>
/// </summary>
public class InvoiceEntryService(IDbSessions sessions, IInvoiceRepository read, IInvoiceEntryRepository repository, TimeProvider time)
{
    private static string Code(string? value, int length) => (value ?? "").Trim().PadRight(length)[..length].TrimEnd();

    /// <summary>The form's drop-down lists.</summary>
    public async Task<InvoiceLookups> LookupsAsync()
    {
        await using var session = await sessions.OpenAsync();
        return await repository.LookupsAsync(session);
    }

    /// <summary>Works an invoice out from what was typed, without saving it: the totals the form shows as it is filled in.</summary>
    public async Task<InvoiceCalculation> CalculateAsync(InvoiceForm form)
    {
        await using var session = await sessions.OpenAsync();
        return await CalculateAsync(session, form);
    }

    /// <summary>As <see cref="CalculateAsync(InvoiceForm)"/> on the caller's session.</summary>
    public async Task<InvoiceCalculation> CalculateAsync(IDbSession session, InvoiceForm form)
    {
        if (InvoiceChecks.Fields(form) is { } problem) throw new BusinessRuleException(problem);
        var header = await read.HeaderAsync(session, form.CaseNo.Trim()) ?? throw new NotFoundException("Record not found!");
        return Calculate(form, header, await HospitalSumsApplyAsync(session, header));
    }

    private async Task<bool> HospitalSumsApplyAsync(IDbSession session, InvoiceCaseHeader header)
    {
        var plan = (header.PlanCode ?? "").Trim();
        return (plan.Length > 0 && plan[0] is >= '2' and <= '5') || (plan.Length > 0 && await repository.PlanIsNewSmAsync(session, plan));
    }

    private static InvoiceCalculation Calculate(InvoiceForm form, InvoiceCaseHeader header, bool hospitalSums) =>
        InvoiceCalculator.Calculate(form.Amounts, form.PayTo ?? "", form.ExcessAmount ?? 0m, hospitalSums);

    /// <summary>An invoice of the case as the form holds it, to be changed. Throws <see cref="NotFoundException"/> when the case has no such invoice.</summary>
    public async Task<InvoiceFormData> FormAsync(string caseNo, int invoiceIndex)
    {
        await using var session = await sessions.OpenAsync();
        return await FormAsync(session, caseNo, invoiceIndex);
    }

    /// <summary>As <see cref="FormAsync(string, int)"/> on the caller's session.</summary>
    public async Task<InvoiceFormData> FormAsync(IDbSession session, string caseNo, int invoiceIndex)
    {
        var cas = caseNo.Trim();
        var header = await read.HeaderAsync(session, cas) ?? throw new NotFoundException("Record not found!");
        var summary = (await read.InvoicesAsync(session, cas)).FirstOrDefault((i) => i.Index == invoiceIndex) ?? throw new NotFoundException("Record not found!");
        var stored = await repository.LoadAsync(session, invoiceIndex);
        var form = new InvoiceForm
        {
            CaseNo = cas, Index = invoiceIndex, InvoiceNo = summary.InvoiceNo?.Trim(), InvoiceDate = summary.InvoiceDate, PayTo = Code(summary.PayTo, 1), Payee = (summary.Payee ?? "").Trim(),
            StayType = Code(summary.StayType, 1), RoomType = (summary.RoomType ?? "").Trim(), ReceiptMode = Code(summary.ReceiptMode, 2), ReceiptDate = summary.ReceiptDate, FollowUpDate = summary.FollowUpDate,
            ExcessAmount = summary.ExcessAmount, InitialAmount = summary.InitialAmount, UhcNo = (summary.UhcNo ?? "").Trim(), Remarks = await repository.RemarksAsync(session, cas),
        };
        foreach (var row in new[] { stored.Actual, stored.Others, stored.NotCover })
        {
            if (row is null) continue;
            foreach (var (liveColumn, value) in row)
            {
                // the tables spell five of the columns with other capitals than the procedures' parameters do (CLMPreXray, CLMPreOthers, CLMInHosPhyVisitDay, CLMLodgerDay, preMedic)
                if (!InvoiceFields.KeyOf.TryGetValue(liveColumn, out var column)) continue;
                if (InvoiceFields.FlagColumns.Contains(column) || InvoiceFields.TextColumns.Contains(column)) form.Texts[column] = value is null or DBNull ? null : Convert.ToString(value)?.TrimEnd();
                else if (InvoiceFields.AmountColumns.Contains(column)) form.Amounts[column] = value is null or DBNull ? null : Convert.ToDecimal(value);
            }
        }
        return new InvoiceFormData(form, Calculate(form, header, await HospitalSumsApplyAsync(session, header)), summary.InWorksheet, !string.IsNullOrWhiteSpace(summary.BordxNo));
    }

    /// <summary>Adds or changes an invoice. Returns a question to ask when one has to be answered first (nothing is saved then).</summary>
    /// <param name="form">What was typed; <see cref="InvoiceForm.Index"/> is set when an invoice is changed.</param>
    /// <param name="userCode">The user entering the invoice.</param>
    public async Task<InvoiceSaveResult> SaveAsync(InvoiceForm form, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        var result = await SaveAsync(session, form, userCode);
        if (result.Index is not null) await session.CommitAsync();
        return result;
    }

    /// <summary>As <see cref="SaveAsync(InvoiceForm, string)"/> on the caller's session (its transaction is the caller's to commit or roll back).</summary>
    public async Task<InvoiceSaveResult> SaveAsync(IDbSession session, InvoiceForm form, string userCode)
    {
        var cas = form.CaseNo.Trim();
        var isNew = form.Index is null;
        var existing = isNew ? null : (await read.InvoicesAsync(session, cas)).FirstOrDefault((i) => i.Index == form.Index) ?? throw new NotFoundException("Record not found!");
        if (existing is not null && !string.IsNullOrWhiteSpace(existing.BordxNo)) throw new BusinessRuleException(InvoiceMessages.Bordxed);
        if (existing is { InWorksheet: true }) throw new BusinessRuleException(InvoiceMessages.ChangeInWorksheet);
        if (InvoiceChecks.Fields(form) is { } problem) throw new BusinessRuleException(problem);

        var header = await read.HeaderAsync(session, cas);
        var hospital = form.PayTo == "H" && !string.IsNullOrWhiteSpace(form.Payee) ? await repository.HospitalAsync(session, form.Payee.Trim()) : null;
        if (InvoiceChecks.First(form, header, hospital?.Name) is { } message) throw new BusinessRuleException(message);
        if (form.PayTo == "H" && hospital is null) throw new BusinessRuleException(InvoiceMessages.NoPayee);
        if (InvoiceChecks.NeedsHospitalConfirmation(form, header!)) return new InvoiceSaveResult(null, InvoiceMessages.DifferentHospital, null, null);
        var invoiceNo = form.InvoiceNo!.Trim();
        if (isNew && (await read.InvoicesAsync(session, cas)).Any((i) => string.Equals((i.InvoiceNo ?? "").Trim(), invoiceNo, StringComparison.OrdinalIgnoreCase))) throw new ConflictException(InvoiceMessages.DuplicateInvoice);

        var calculation = Calculate(form, header!, await HospitalSumsApplyAsync(session, header!));
        var excess = form.ExcessAmount ?? 0m;
        var hasFollowUp = form.FollowUpDate is not null;
        var hasReceipt = form.ReceiptDate is not null;
        var values = new Dictionary<string, object?>
        {
            ["INVWSversion"] = isNew ? (await repository.NextVersionAsync(session, cas)).ToString() : null, ["INVBordxed"] = false, ["INVWSed"] = false, ["INVNo"] = invoiceNo, ["CasNumber"] = cas,
            ["InvDate"] = form.InvoiceDate!.Value.Date, ["INVEnteredDate"] = time.GetLocalNow().Date, ["INVEnteredBy"] = userCode, ["INVPayee"] = hospital?.Code ?? form.Payee!.Trim(),
            ["INVStayType"] = Code(form.StayType, 1), ["INVPAYType"] = Code(header!.PayType, 1), ["InvPayTo"] = form.PayTo, ["CLMINVRecMode"] = Code(form.ReceiptMode, 2),
            ["INVFolUpDate"] = form.FollowUpDate?.Date, ["CLMINVRecDate"] = form.ReceiptDate?.Date, ["INVAmount"] = calculation.InvoiceAmount, ["InvPaidByMem"] = excess,
            ["CLMUHCNo"] = (form.UhcNo ?? "").Trim(), ["InvExcessAmt"] = excess, ["InvPayAmt"] = calculation.PayAmount, ["INVInitialActAmt"] = form.InitialAmount ?? 0m,
            ["INVRoomType"] = (form.RoomType ?? "").Trim(),
        };
        // the four header procedures differ in the follow-up and the receipt date they take: both, the follow-up only (WODIR), the receipt only (WOFolDate), neither (WO2013)
        var (insertProc, updateProc) = (hasFollowUp, hasReceipt) switch
        {
            (true, true) => ("WS_Insert_ClaimInvoice_2013", "WS_Update_ClaimInvoice2013"),
            (true, false) => ("WS_Insert_ClaimInvoice_WODIR", "WS_Update_ClaimInvoice_WODIR2013"),
            (false, true) => ("WS_Insert_ClaimInvoice_WOFolDate", "WS_Update_ClaimInvoice_WOFolDate2013"),
            _ => ("WS_Insert_ClaimInvoice_WO2013", "WS_Update_ClaimInvoice_WO2013"),
        };
        int index;
        IDictionary<string, object?>? kept = null;
        if (!isNew)
        {
            // what the form does not show but the update procedures would reset: the invoice's own pay type, the upfront discount and the discount received
            kept = (await repository.LoadAsync(session, form.Index!.Value)).Header;
            foreach (var (liveColumn, value) in kept ?? new Dictionary<string, object?>())
                if (liveColumn.Equals("INVPAYType", StringComparison.OrdinalIgnoreCase) || liveColumn.Equals("UpfrontDisc", StringComparison.OrdinalIgnoreCase) || liveColumn.Equals("DiscReceived", StringComparison.OrdinalIgnoreCase))
                    values[liveColumn.Equals("DiscReceived", StringComparison.OrdinalIgnoreCase) ? "Discreceived" : liveColumn.Equals("UpfrontDisc", StringComparison.OrdinalIgnoreCase) ? "UpfrontDisc" : "INVPAYType"] = value is DBNull ? null : value;
        }
        if (isNew)
        {
            await repository.CallAsync(session, insertProc, values);
            index = await repository.KeyAsync(session, cas, invoiceNo) ?? throw new InvalidOperationException("The invoice was not found after it was added.");
        }
        else
        {
            index = form.Index!.Value;
            values["INVIndex"] = index;
            await repository.CallAsync(session, updateProc, values);
        }

        string Text(string column) => form.Texts.GetValueOrDefault(column) ?? "";
        object Amount(string column) => calculation.Amounts.GetValueOrDefault(column) ?? 0m;
        var actual = InvoiceFields.ActualColumns.ToDictionary((c) => c, (c) => InvoiceFields.TextColumns.Contains(c) ? (object)Text(c) : Amount(c));
        var gst = InvoiceFields.ActualColumns.ToDictionary((c) => c, (c) => InvoiceFields.GstPassThrough.Contains(c) ? actual[c] : InvoiceFields.TextColumns.Contains(c) ? "" : (object)0m);
        var others = InvoiceFields.OthersColumns.ToDictionary((c) => c, (c) => InvoiceFields.FlagColumns.Contains(c) ? (form.Texts.GetValueOrDefault(c) is "Y" ? "Y" : "N") : Amount(c));
        var notCover = InvoiceFields.NotCoverColumns.ToDictionary((c) => c, (c) => InvoiceFields.TextColumns.Contains(c) ? (object)Text(c) : Amount(c));
        foreach (var set in new[] { actual, gst, others, notCover })
        {
            set["INVIdex"] = index;
            set["INVIndex"] = index;
        }
        var prefix = isNew ? "Insert" : "Update";
        await repository.CallAsync(session, $"WS_{prefix}_ClaimInvActual2010", actual!);
        await repository.CallAsync(session, $"WS_{prefix}_ClaimInvActualGST", gst!);
        await repository.CallAsync(session, $"WS_{prefix}_ClaimInvoiceActOthers_New_2019", others!);
        await repository.CallAsync(session, $"WS_{prefix}_ClaimInvoiceNotCov08", notCover!);
        await repository.SaveRemarksAsync(session, cas, form.Remarks);

        var warning = header.Payor?.Trim() == "AX" && hospital?.DiscountIndicator?.Trim() == "Y" ? "Please ensure hospital discount is given!" : null;
        return new InvoiceSaveResult(index, null, warning, calculation);
    }

    /// <summary>Removes an invoice no worksheet has used. Throws <see cref="NotFoundException"/> when the case has no such invoice and <see cref="BusinessRuleException"/> when a worksheet has used it.</summary>
    public async Task RemoveAsync(string caseNo, int invoiceIndex)
    {
        await using var session = await sessions.BeginAsync();
        await RemoveAsync(session, caseNo, invoiceIndex);
        await session.CommitAsync();
    }

    /// <summary>As <see cref="RemoveAsync(string, int)"/> on the caller's session.</summary>
    public async Task RemoveAsync(IDbSession session, string caseNo, int invoiceIndex)
    {
        var invoice = (await read.InvoicesAsync(session, caseNo.Trim())).FirstOrDefault((i) => i.Index == invoiceIndex) ?? throw new NotFoundException("Record not found!");
        if (invoice.InWorksheet) throw new BusinessRuleException(InvoiceMessages.UsedInWorksheet);
        await repository.RemoveAsync(session, invoiceIndex);
    }
}
