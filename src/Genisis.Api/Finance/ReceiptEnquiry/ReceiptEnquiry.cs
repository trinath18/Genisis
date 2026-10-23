using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Finance.ReceiptEnquiry;

/// <summary>One receipt (a ReceiptSummary row).</summary>
public record ReceiptRow
{
    /// <summary>ReceiptSummary.ReceiptIndex.</summary>
    public int Id { get; set; }

    /// <summary>Receipt number (RecSeq prefix RA / RB / RC / RG + sequence).</summary>
    public string? ReceiptNo { get; set; }

    /// <summary>Receipt date.</summary>
    public DateTime? ReceiptDate { get; set; }

    /// <summary>Receipt amount.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Receipt type: IPCLM, OPCLM, IPFEE, OPFEE.</summary>
    public string? ReceiptType { get; set; }

    /// <summary>Description (for a fee receipt, the member MCO invoice number).</summary>
    public string? Description { get; set; }

    /// <summary>Cheque number (ONLINE for transfers).</summary>
    public string? ChequeNo { get; set; }

    /// <summary>Payor code.</summary>
    public string? Payor { get; set; }

    /// <summary>SUN posting batch number.</summary>
    public string? BatchNo { get; set; }

    /// <summary>True when posted to SUN (RecPayStatus 1).</summary>
    public bool Posted { get; set; }

    /// <summary>SUN posting date.</summary>
    public DateTime? PostDate { get; set; }

    /// <summary>User who posted it.</summary>
    public string? PostUser { get; set; }

    /// <summary>Number of claim allocation lines (ReceiptfrPAY).</summary>
    public int AllocationLines { get; set; }

    /// <summary>Total allocated to claims.</summary>
    public decimal? Allocated { get; set; }

    /// <summary>The receipt that reversed this one (Reverse Receipt), or null.</summary>
    public string? ReversedBy { get; set; }

    /// <summary>True when this receipt is itself a reversal (description "REV &lt;receipt no&gt;-...").</summary>
    public bool IsReversal => Description?.StartsWith("REV ", StringComparison.Ordinal) == true;
}

/// <summary>One claim allocation line of a receipt (a ReceiptfrPAY row).</summary>
public record ReceiptAllocation
{
    /// <summary>ReceiptfrPAY.RecPAYIndex.</summary>
    public int Id { get; set; }

    /// <summary>Case number.</summary>
    public string? CaseNo { get; set; }

    /// <summary>Claim version.</summary>
    public string? ClaimVersion { get; set; }

    /// <summary>Bordx number.</summary>
    public string? BordxNo { get; set; }

    /// <summary>Amount allocated to this claim (CheckAmt).</summary>
    public decimal? Allocated { get; set; }

    /// <summary>The receipt amount recorded on the line.</summary>
    public decimal? ReceiptAmount { get; set; }

    /// <summary>Bank charge.</summary>
    public decimal? BankCharge { get; set; }

    /// <summary>Cheque number.</summary>
    public string? ChequeNo { get; set; }

    /// <summary>Accounting period "MM/YYYY".</summary>
    public string? AccPeriod { get; set; }

    /// <summary>Remark.</summary>
    public string? Remark { get; set; }

    /// <summary>Last updated by.</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>Last updated on.</summary>
    public DateTime? UpdatedOn { get; set; }
}

/// <summary>The member MCO invoice a fee receipt names.</summary>
public record ReceiptInvoice
{
    /// <summary>Invoice number (MBMMCOInvoice.MCOInvNo).</summary>
    public string? InvoiceNo { get; set; }

    /// <summary>Invoice date.</summary>
    public DateTime? InvoiceDate { get; set; }

    /// <summary>Number of member lines on the invoice.</summary>
    public int Members { get; set; }

    /// <summary>Total billed (BillMCOAmt).</summary>
    public decimal? BillAmount { get; set; }

    /// <summary>Total GST column (as stored).</summary>
    public decimal? GstAmount { get; set; }
}

/// <summary>A SUN posting of the receipt (SUNPosting rows with TransRef = receipt number, grouped).</summary>
public record ReceiptPosting
{
    /// <summary>Post type (RecPayor ...).</summary>
    public string? PostType { get; set; }

    /// <summary>Batch number.</summary>
    public string? BatchNo { get; set; }

    /// <summary>Post date.</summary>
    public DateTime? PostDate { get; set; }

    /// <summary>Posted by.</summary>
    public string? PostBy { get; set; }

    /// <summary>Accounting period "MM/YYYY".</summary>
    public string? AccPeriod { get; set; }

    /// <summary>Number of SUNPosting rows.</summary>
    public int Rows { get; set; }
}

/// <summary>A receipt with everything behind it.</summary>
/// <param name="Receipt">The header.</param>
/// <param name="Allocations">Claim allocation lines.</param>
/// <param name="Invoices">The fee invoice(s) the description names.</param>
/// <param name="Postings">SUN postings.</param>
/// <param name="Unallocated">Receipt amount minus the allocated total (claim receipts with lines only).</param>
public record ReceiptDetail(ReceiptRow Receipt, List<ReceiptAllocation> Allocations, List<ReceiptInvoice> Invoices, List<ReceiptPosting> Postings, decimal? Unallocated);

/// <summary>The drop-down lists.</summary>
/// <param name="ReceiptTypes">Receipt types in use.</param>
/// <param name="Payors">Payors.</param>
/// <param name="PostedStatuses">Posted to SUN: 0 no, 1 yes.</param>
public record ReceiptEnquiryLookups(List<CodeName> ReceiptTypes, List<CodeName> Payors, IReadOnlyList<CodeName> PostedStatuses);

/// <summary>A search result.</summary>
/// <param name="Rows">Receipts (at most <see cref="FinanceLimits.MaxRows"/>).</param>
/// <param name="Total">Sum of the listed receipt amounts.</param>
/// <param name="Truncated">True when more receipts matched.</param>
public record ReceiptSearchResult(List<ReceiptRow> Rows, decimal Total, bool Truncated);

/// <summary>The search criteria; ranges are inclusive and blank means no criterion.</summary>
public class ReceiptEnquiryFilter
{
    /// <summary>Receipt number from.</summary>
    public string? ReceiptNoFrom { get; set; }

    /// <summary>Receipt number to.</summary>
    public string? ReceiptNoTo { get; set; }

    /// <summary>Receipt date from.</summary>
    public DateTime? ReceiptDateFrom { get; set; }

    /// <summary>Receipt date to.</summary>
    public DateTime? ReceiptDateTo { get; set; }

    /// <summary>Receipt type.</summary>
    public string? ReceiptType { get; set; }

    /// <summary>Payor code.</summary>
    public string? Payor { get; set; }

    /// <summary>Cheque number (exact).</summary>
    public string? ChequeNo { get; set; }

    /// <summary>SUN batch number (exact).</summary>
    public string? BatchNo { get; set; }

    /// <summary>Posted to SUN: 0 no, 1 yes, blank both.</summary>
    public string? Posted { get; set; }

    /// <summary>Text the description contains.</summary>
    public string? Description { get; set; }

    /// <summary>Receipts with an allocation line for this case number.</summary>
    public string? CaseNo { get; set; }
}

/// <summary>The search checks: at least one criterion, valid dates and codes.</summary>
public sealed class ReceiptEnquiryFilterValidator : RequestValidator<ReceiptEnquiryFilter>
{
    /// <summary>Declares the rules.</summary>
    public ReceiptEnquiryFilterValidator()
    {
        RuleFor(f => f).Must(HasCriterion).WithMessage("Please enter at least one search criterion.");
        RuleFor(f => f).Must(f => f.ReceiptDateFrom is null || f.ReceiptDateTo is null || f.ReceiptDateFrom <= f.ReceiptDateTo)
            .WithMessage("Receipt Date from cannot be after Receipt Date to.");
        RuleFor(f => f.Posted).Must(p => FinanceLimits.IsBlankOr(p, "0", "1")).WithMessage("Select a valid posted status.");
    }

    private static bool HasCriterion(ReceiptEnquiryFilter f) =>
        new[] { f.ReceiptNoFrom, f.ReceiptNoTo, f.ReceiptType, f.Payor, f.ChequeNo, f.BatchNo, f.Posted, f.Description, f.CaseNo }.Any(v => !string.IsNullOrWhiteSpace(v))
        || f.ReceiptDateFrom is not null || f.ReceiptDateTo is not null;
}

/// <summary>Reads receipts (no writes).</summary>
public interface IReceiptEnquiryRepository
{
    /// <summary>Receipt types and payors.</summary>
    Task<(List<CodeName> Types, List<CodeName> Payors)> LookupsAsync(IDbSession session);

    /// <summary>The receipts that match (at most <see cref="FinanceLimits.FetchRows"/>).</summary>
    Task<List<ReceiptRow>> SearchAsync(IDbSession session, ReceiptEnquiryFilter filter);

    /// <summary>One receipt with its lines, invoices and postings; null when it does not exist.</summary>
    Task<(ReceiptRow Receipt, List<ReceiptAllocation> Allocations, List<ReceiptInvoice> Invoices, List<ReceiptPosting> Postings)?> GetAsync(IDbSession session, int id);
}

/// <inheritdoc />
public sealed class ReceiptEnquiryRepository : IReceiptEnquiryRepository
{
    /// <inheritdoc />
    public async Task<(List<CodeName> Types, List<CodeName> Payors)> LookupsAsync(IDbSession session)
    {
        using var grid = await session.Connection.ProcMultipleAsync("genisis.ReceiptEnquiry_Lookups", null, session.Transaction);
        return ((await grid.ReadAsync<CodeName>()).ToList(), (await grid.ReadAsync<CodeName>()).ToList());
    }

    /// <inheritdoc />
    public Task<List<ReceiptRow>> SearchAsync(IDbSession session, ReceiptEnquiryFilter f) =>
        session.Connection.ProcQueryAsync<ReceiptRow>("genisis.ReceiptEnquiry_Search", new
        {
            receiptFrom = FinanceLimits.Text(f.ReceiptNoFrom), receiptTo = FinanceLimits.Text(f.ReceiptNoTo),
            dateFrom = FinanceLimits.Day(f.ReceiptDateFrom), dateTo = FinanceLimits.Day(f.ReceiptDateTo),
            type = FinanceLimits.Text(f.ReceiptType), payor = FinanceLimits.Text(f.Payor), chequeNo = FinanceLimits.Text(f.ChequeNo),
            batchNo = FinanceLimits.Text(f.BatchNo), posted = FinanceLimits.Text(f.Posted), description = FinanceLimits.Text(f.Description),
            caseNo = FinanceLimits.Text(f.CaseNo), max = FinanceLimits.FetchRows,
        }, session.Transaction);

    /// <inheritdoc />
    public async Task<(ReceiptRow Receipt, List<ReceiptAllocation> Allocations, List<ReceiptInvoice> Invoices, List<ReceiptPosting> Postings)?> GetAsync(IDbSession session, int id)
    {
        using var grid = await session.Connection.ProcMultipleAsync("genisis.ReceiptEnquiry_Get", new { id }, session.Transaction);
        var receipt = (await grid.ReadAsync<ReceiptRow>()).FirstOrDefault();
        var allocations = (await grid.ReadAsync<ReceiptAllocation>()).ToList();
        var invoices = (await grid.ReadAsync<ReceiptInvoice>()).ToList();
        var postings = (await grid.ReadAsync<ReceiptPosting>()).ToList();
        return receipt is null ? null : (receipt, allocations, invoices, postings);
    }
}

/// <summary>Finance &gt; Receipt Enquiry (legacy frmReceiptEnquiry, source not available; rebuilt from ReceiptSummary / ReceiptfrPAY).</summary>
public class ReceiptEnquiryService(IDbSessions sessions, IReceiptEnquiryRepository repository)
{
    /// <summary>Posted-to-SUN list.</summary>
    public static readonly CodeName[] PostedStatuses = [new("0", "0 - No"), new("1", "1 - Yes")];

    /// <summary>The drop-down lists.</summary>
    public async Task<ReceiptEnquiryLookups> LookupsAsync()
    {
        await using var session = await sessions.OpenAsync();
        var (types, payors) = await repository.LookupsAsync(session);
        return new ReceiptEnquiryLookups(types, payors, PostedStatuses);
    }

    /// <summary>The receipts that match, newest first, with their total.</summary>
    public async Task<ReceiptSearchResult> SearchAsync(ReceiptEnquiryFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        var rows = await repository.SearchAsync(session, filter);
        var truncated = rows.Count > FinanceLimits.MaxRows;
        if (truncated) rows = rows.Take(FinanceLimits.MaxRows).ToList();
        return new ReceiptSearchResult(rows, rows.Sum(r => r.Amount ?? 0m), truncated);
    }

    /// <summary>One receipt with its allocation lines, fee invoice and SUN postings. Throws <see cref="NotFoundException"/> when it does not exist.</summary>
    public async Task<ReceiptDetail> GetAsync(int id)
    {
        await using var session = await sessions.OpenAsync();
        var found = await repository.GetAsync(session, id) ?? throw new NotFoundException("No record found!");
        var (receipt, allocations, invoices, postings) = found;
        var allocated = allocations.Sum(a => a.Allocated ?? 0m);
        var header = receipt with { AllocationLines = allocations.Count, Allocated = allocations.Count == 0 ? null : allocated };
        return new ReceiptDetail(header, allocations, invoices, postings, allocations.Count == 0 ? null : (receipt.Amount ?? 0m) - allocated);
    }
}
