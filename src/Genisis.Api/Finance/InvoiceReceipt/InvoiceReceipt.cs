using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Finance.ReceiptEnquiry;

namespace Genisis.Api.Finance.InvoiceReceipt;

/// <summary>Fixed codes of Finance &gt; Invoice Receipt.</summary>
public static class InvoiceReceiptCodes
{
    /// <summary>Fee receipt types (inpatient / outpatient fees).</summary>
    public static readonly CodeName[] ReceiptTypes = [new("IPFEE", "IPFEE - Inpatient Fee"), new("OPFEE", "OPFEE - Outpatient Fee")];

    /// <summary>The receipt number prefix the fee receipts use (RecSeq RB).</summary>
    public const string DefaultPrefix = "RB";

    /// <summary>The cheque number the transfers carry.</summary>
    public const string Online = "ONLINE";
}

/// <summary>The drop-down lists.</summary>
/// <param name="ReceiptTypes">IPFEE / OPFEE.</param>
/// <param name="Prefixes">Receipt number prefixes in RecSeq.</param>
/// <param name="Payors">Payors.</param>
/// <param name="DefaultPrefix">The prefix offered first (RB).</param>
public record InvoiceReceiptLookups(IReadOnlyList<CodeName> ReceiptTypes, List<CodeName> Prefixes, List<CodeName> Payors, string DefaultPrefix);

/// <summary>A member MCO invoice with what has been received against it.</summary>
public record OutstandingInvoice
{
    /// <summary>Invoice number (MBMMCOInvoice.MCOInvNo).</summary>
    public string InvoiceNo { get; set; } = "";

    /// <summary>Invoice date.</summary>
    public DateTime? InvoiceDate { get; set; }

    /// <summary>Number of member lines.</summary>
    public int Members { get; set; }

    /// <summary>Total billed (BillMCOAmt).</summary>
    public decimal BillAmount { get; set; }

    /// <summary>Payor code (prefix of the membership numbers).</summary>
    public string? Payor { get; set; }

    /// <summary>Number of fee receipts recorded against it.</summary>
    public int Receipts { get; set; }

    /// <summary>Total received.</summary>
    public decimal Received { get; set; }

    /// <summary>Billed minus received (negative when more was received).</summary>
    public decimal Outstanding { get; set; }
}

/// <summary>A search result.</summary>
/// <param name="Rows">Invoices (at most <see cref="FinanceLimits.MaxRows"/>).</param>
/// <param name="TotalOutstanding">Sum of the listed balances.</param>
/// <param name="Truncated">True when more invoices matched.</param>
public record InvoiceSearchResult(List<OutstandingInvoice> Rows, decimal TotalOutstanding, bool Truncated);

/// <summary>The invoice search criteria; ranges inclusive, blank = no criterion.</summary>
public class InvoiceReceiptFilter
{
    /// <summary>Invoice number from.</summary>
    public string? InvoiceNoFrom { get; set; }

    /// <summary>Invoice number to.</summary>
    public string? InvoiceNoTo { get; set; }

    /// <summary>Invoice date from.</summary>
    public DateTime? InvoiceDateFrom { get; set; }

    /// <summary>Invoice date to.</summary>
    public DateTime? InvoiceDateTo { get; set; }

    /// <summary>Invoices with a member of this payor.</summary>
    public string? Payor { get; set; }

    /// <summary>Only invoices with nothing received or a balance above zero.</summary>
    public bool OutstandingOnly { get; set; } = true;

    /// <summary>Short Payment Receipt: only invoices already part paid (a receipt recorded) with a balance still above zero.</summary>
    public bool PartPaidOnly { get; set; }
}

/// <summary>A fee receipt to record against an invoice, numbered from RecSeq.</summary>
public class InvoiceReceiptRequest
{
    /// <summary>The invoice paid.</summary>
    public string? InvoiceNo { get; set; }

    /// <summary>IPFEE or OPFEE.</summary>
    public string? ReceiptType { get; set; }

    /// <summary>Receipt date.</summary>
    public DateTime? ReceiptDate { get; set; }

    /// <summary>Receipt amount.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Cheque number (ONLINE for a transfer).</summary>
    public string? ChequeNo { get; set; }

    /// <summary>Payor code.</summary>
    public string? Payor { get; set; }

    /// <summary>Description; the invoice number when blank.</summary>
    public string? Description { get; set; }

    /// <summary>Receipt number prefix (RecSeq RCInd); RB when blank.</summary>
    public string? Prefix { get; set; }
}

/// <summary>A fee receipt with a receipt number typed by the user (Notification / Suspension, the desktop's manual form).</summary>
public class ManualInvoiceReceiptRequest : InvoiceReceiptRequest
{
    /// <summary>Receipt number.</summary>
    public string? ReceiptNo { get; set; }
}

/// <summary>A recorded receipt.</summary>
/// <param name="Id">ReceiptSummary.ReceiptIndex.</param>
/// <param name="ReceiptNo">Receipt number.</param>
/// <param name="Message">The desktop's confirmation.</param>
public record InvoiceReceiptResult(int Id, string ReceiptNo, string Message);

/// <summary>Search checks.</summary>
public sealed class InvoiceReceiptFilterValidator : RequestValidator<InvoiceReceiptFilter>
{
    /// <summary>Declares the rules.</summary>
    public InvoiceReceiptFilterValidator() =>
        RuleFor(f => f).Must(f => f.InvoiceDateFrom is null || f.InvoiceDateTo is null || f.InvoiceDateFrom <= f.InvoiceDateTo)
            .WithMessage("Invoice Date from cannot be after Invoice Date to.");
}

/// <summary>CmdSave_Click's checks, in the desktop's order and with its messages.</summary>
public static class InvoiceReceiptRules
{
    /// <summary>Adds the shared rules to a validator.</summary>
    public static void Add<T>(AbstractValidator<T> v) where T : InvoiceReceiptRequest
    {
        v.RuleFor(r => r.InvoiceNo).NotEmpty().WithMessage("Please select the invoice");
        v.RuleFor(r => r.ReceiptType).NotEmpty().WithMessage("Please Enter Rec. Type");
        v.RuleFor(r => r.ReceiptType).Must(t => InvoiceReceiptCodes.ReceiptTypes.Any(c => c.Code == t!.Trim())).WithMessage("Select a valid receipt type.");
        v.RuleFor(r => r.ReceiptDate).NotNull().WithMessage("Please Enter Receipt Date");
        v.RuleFor(r => r.Amount).NotNull().WithMessage("Please Enter Receipt Amount");
        v.RuleFor(r => r.Amount).Must(a => a != 0).WithMessage("Receipt Amount cannot be zero.");
        v.RuleFor(r => r.Description).MaximumLength(100).WithMessage("Description is longer than 100 characters.");
        v.RuleFor(r => r.ChequeNo).MaximumLength(40).WithMessage("Chq No is longer than 40 characters.");
        v.RuleFor(r => r.Payor).MaximumLength(4).WithMessage("Select a valid payor.");
    }
}

/// <summary>Checks of an auto-numbered receipt.</summary>
public sealed class InvoiceReceiptRequestValidator : RequestValidator<InvoiceReceiptRequest>
{
    /// <summary>Declares the rules.</summary>
    public InvoiceReceiptRequestValidator()
    {
        InvoiceReceiptRules.Add(this);
        RuleFor(r => r.Prefix).Must(p => string.IsNullOrWhiteSpace(p) || p.Trim().Length == 2).WithMessage("Select a valid receipt prefix.");
    }
}

/// <summary>Checks of a receipt with a typed number (the manual form also asked for the receipt number).</summary>
public sealed class ManualInvoiceReceiptRequestValidator : RequestValidator<ManualInvoiceReceiptRequest>
{
    /// <summary>Declares the rules.</summary>
    public ManualInvoiceReceiptRequestValidator()
    {
        RuleFor(r => r.ReceiptNo).NotEmpty().WithMessage("Please Enter Receipt No");
        RuleFor(r => r.ReceiptNo).MaximumLength(40).WithMessage("Receipt No is longer than 40 characters.");
        InvoiceReceiptRules.Add(this);
    }
}

/// <summary>Reads invoices / fee receipts and records receipts.</summary>
public interface IInvoiceReceiptRepository
{
    /// <summary>Receipt prefixes and payors.</summary>
    Task<(List<CodeName> Prefixes, List<CodeName> Payors)> LookupsAsync(IDbSession session);

    /// <summary>The invoices that match (at most <see cref="FinanceLimits.FetchRows"/>).</summary>
    Task<List<OutstandingInvoice>> SearchAsync(IDbSession session, InvoiceReceiptFilter filter);

    /// <summary>The fee receipts recorded against an invoice.</summary>
    Task<List<ReceiptRow>> ReceiptsAsync(IDbSession session, string invoiceNo);

    /// <summary>Takes the next receipt number for a prefix.</summary>
    Task<string> NextNumberAsync(IDbSession session, string prefix);

    /// <summary>Saves a receipt header; returns the result code and the new id.</summary>
    Task<(int Result, int? Id)> InsertAsync(IDbSession session, string receiptNo, InvoiceReceiptRequest request, string description);
}

/// <inheritdoc />
public sealed class InvoiceReceiptRepository : IInvoiceReceiptRepository
{
    /// <inheritdoc />
    public async Task<(List<CodeName> Prefixes, List<CodeName> Payors)> LookupsAsync(IDbSession session)
    {
        using var grid = await session.Connection.ProcMultipleAsync("genisis.InvoiceReceipt_Lookups", null, session.Transaction);
        return ((await grid.ReadAsync<CodeName>()).ToList(), (await grid.ReadAsync<CodeName>()).ToList());
    }

    /// <inheritdoc />
    public Task<List<OutstandingInvoice>> SearchAsync(IDbSession session, InvoiceReceiptFilter f) =>
        session.Connection.ProcQueryAsync<OutstandingInvoice>("genisis.InvoiceReceipt_SearchInvoices", new
        {
            invoiceFrom = FinanceLimits.Text(f.InvoiceNoFrom), invoiceTo = FinanceLimits.Text(f.InvoiceNoTo),
            dateFrom = FinanceLimits.Day(f.InvoiceDateFrom), dateTo = FinanceLimits.Day(f.InvoiceDateTo),
            payor = FinanceLimits.Text(f.Payor), outstandingOnly = f.OutstandingOnly, partPaidOnly = f.PartPaidOnly, max = FinanceLimits.FetchRows,
        }, session.Transaction);

    /// <inheritdoc />
    public Task<List<ReceiptRow>> ReceiptsAsync(IDbSession session, string invoiceNo) =>
        session.Connection.ProcQueryAsync<ReceiptRow>("genisis.InvoiceReceipt_Receipts", new { invoiceNo = invoiceNo.Trim() }, session.Transaction);

    /// <inheritdoc />
    public async Task<string> NextNumberAsync(IDbSession session, string prefix) =>
        await session.Connection.ProcScalarAsync<string>("genisis.FinanceReceipt_NextNumber", new { prefix }, session.Transaction)
            ?? throw new InvalidOperationException("genisis.FinanceReceipt_NextNumber returned no receipt number.");

    /// <inheritdoc />
    public async Task<(int Result, int? Id)> InsertAsync(IDbSession session, string receiptNo, InvoiceReceiptRequest r, string description)
    {
        var row = await session.Connection.ProcSingleAsync<(int Result, int? Id)>("genisis.FinanceReceipt_Insert", new
        {
            receiptNo, receiptDate = r.ReceiptDate!.Value.Date, amount = r.Amount, receiptType = r.ReceiptType!.Trim(), description,
            chequeNo = FinanceLimits.Text(r.ChequeNo) ?? InvoiceReceiptCodes.Online, payor = FinanceLimits.Text(r.Payor),
        }, session.Transaction);
        return row;
    }
}

/// <summary>
/// Finance &gt; Invoice Receipt (Outstanding) and Notification / Suspension (legacy FrmRecMCO / frmRecMCOManual): member MCO invoices with
/// their balance, and fee receipts recorded against them in ReceiptSummary (the table the receipts have been entered in since 2010).
/// </summary>
public class InvoiceReceiptService(IDbSessions sessions, IInvoiceReceiptRepository repository, ILogger<InvoiceReceiptService> log)
{
    /// <summary>The drop-down lists.</summary>
    public async Task<InvoiceReceiptLookups> LookupsAsync()
    {
        await using var session = await sessions.OpenAsync();
        var (prefixes, payors) = await repository.LookupsAsync(session);
        return new InvoiceReceiptLookups(InvoiceReceiptCodes.ReceiptTypes, prefixes, payors, InvoiceReceiptCodes.DefaultPrefix);
    }

    /// <summary>The invoices that match, newest first, with the total balance.</summary>
    public async Task<InvoiceSearchResult> SearchAsync(InvoiceReceiptFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        var rows = await repository.SearchAsync(session, filter);
        var truncated = rows.Count > FinanceLimits.MaxRows;
        if (truncated) rows = rows.Take(FinanceLimits.MaxRows).ToList();
        return new InvoiceSearchResult(rows, rows.Sum(r => r.Outstanding), truncated);
    }

    /// <summary>The fee receipts of one invoice.</summary>
    public async Task<List<ReceiptRow>> ReceiptsAsync(string invoiceNo)
    {
        await using var session = await sessions.OpenAsync();
        return await repository.ReceiptsAsync(session, invoiceNo);
    }

    /// <summary>CmdSave_Click: takes the next receipt number for the prefix and saves the receipt, in one transaction.</summary>
    public Task<InvoiceReceiptResult> RecordAsync(InvoiceReceiptRequest request, string userCode) =>
        SaveAsync(request, null, userCode);

    /// <summary>The manual form's save: the receipt number is the one typed; refused when it is already used.</summary>
    public Task<InvoiceReceiptResult> RecordManualAsync(ManualInvoiceReceiptRequest request, string userCode) =>
        SaveAsync(request, request.ReceiptNo!.Trim(), userCode);

    private async Task<InvoiceReceiptResult> SaveAsync(InvoiceReceiptRequest request, string? typedNo, string userCode)
    {
        var description = FinanceLimits.Text(request.Description) ?? request.InvoiceNo!.Trim();
        await using var session = await sessions.BeginAsync();
        var receiptNo = typedNo ?? await repository.NextNumberAsync(session, (FinanceLimits.Text(request.Prefix) ?? InvoiceReceiptCodes.DefaultPrefix).ToUpperInvariant());
        var (result, id) = await repository.InsertAsync(session, receiptNo, request, description);
        if (result == StoredProcedures.Conflict || id is null) throw new FinanceException($"Receipt No {receiptNo} already exists.");
        await session.CommitAsync();
        // ReceiptSummary has no user / update columns, so who recorded the receipt is kept in the log.
        log.LogInformation("Fee receipt {ReceiptNo} ({Type} {Amount}) recorded for invoice {Invoice} by {User}", receiptNo, request.ReceiptType, request.Amount,
            request.InvoiceNo, userCode);
        return new InvoiceReceiptResult(id.Value, receiptNo, "Record Saved...");
    }
}
