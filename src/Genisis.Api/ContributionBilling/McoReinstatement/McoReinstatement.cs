using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;

namespace Genisis.Api.ContributionBilling.McoReinstatement;

/// <summary>A line of the list: a principal or a covered person whose cover was reinstated.</summary>
public record McoRow
{
    /// <summary>Member number.</summary>
    public string MemberNo { get; set; } = "";

    /// <summary>Cover id (00 for the principal).</summary>
    public string? CoverId { get; set; }

    /// <summary>Name of the principal or of the covered person.</summary>
    public string? MemberName { get; set; }

    /// <summary>Relation (P for the principal).</summary>
    public string? Relation { get; set; }

    /// <summary>Adult or Child.</summary>
    public string? AdultChild { get; set; }

    /// <summary>Endorsement bordx date.</summary>
    public DateTime? BordxDate { get; set; }

    /// <summary>Endorsement bordx batch.</summary>
    public string? BatchNo { get; set; }

    /// <summary>Policy number.</summary>
    public string? PolicyNo { get; set; }

    /// <summary>Payor effective date.</summary>
    public DateTime? EffDate { get; set; }

    /// <summary>Payor expiry date.</summary>
    public DateTime? ExpDate { get; set; }

    /// <summary>Invoice number (empty until the invoice is printed).</summary>
    public string? InvoiceNo { get; set; }

    /// <summary>Invoice date.</summary>
    public DateTime? InvoiceDate { get; set; }

    /// <summary>MCO fee.</summary>
    public decimal? McoFee { get; set; }

    /// <summary>The principal's IC / BC / passport.</summary>
    public string? IcNo { get; set; }

    /// <summary>The IC of the person on the line.</summary>
    public string? PersonIc { get; set; }

    /// <summary>Group company.</summary>
    public string? GroupCompany { get; set; }

    /// <summary>Days insured.</summary>
    public int? DaysInsured { get; set; }

    /// <summary>True for a covered person (a supplementary line), false for a principal.</summary>
    public bool IsCovered { get; set; }

    /// <summary>Payor code.</summary>
    public string? PayorCode { get; set; }
}

/// <summary>A line of the invoice: a bordx batch, insurance code and adult / child with the principals, the covered persons and the fee.</summary>
public record McoInvoiceLine
{
    /// <summary>Endorsement bordx date.</summary>
    public DateTime? BordxDate { get; set; }

    /// <summary>Batch number.</summary>
    public int? BatchNo { get; set; }

    /// <summary>Insurance code.</summary>
    public string? InsCode { get; set; }

    /// <summary>Adult or Child.</summary>
    public string? AdultChild { get; set; }

    /// <summary>Principals.</summary>
    public int Principals { get; set; }

    /// <summary>Covered persons.</summary>
    public int? CoveredPersons { get; set; }

    /// <summary>MCO fee.</summary>
    public decimal? McoFee { get; set; }

    /// <summary>Invoice number.</summary>
    public string? InvoiceNo { get; set; }

    /// <summary>Invoice date.</summary>
    public DateTime? InvoiceDate { get; set; }
}

/// <summary>The payor the invoice is made out to.</summary>
public record McoPayor(string Code, string? Name, string? Address1, string? Address2, string? Address3, string? Contact, string? Telephone, string? Fax, string Prefix);

/// <summary>The list and its counts.</summary>
public record McoSearchResult(List<McoRow> Rows, int Principals, int Supplementary, List<string> Invoices, bool Truncated);

/// <summary>What Print or Preview gives back: the payor, the invoice number and date, the invoice lines and the listed members.</summary>
public record McoDocument(string ReportType, bool Allocated, string? InvoiceNo, DateTime? InvoiceDate, McoPayor? Payor, List<McoInvoiceLine> Lines, List<McoRow> Rows);

/// <summary>The criteria of the search (and of Print and Preview).</summary>
public class McoFilter
{
    /// <summary>"MCO Criteria": batch (Bordx Date and Batch No, with the payor) or invoice (Invoice No).</summary>
    public string? Criteria { get; set; }

    /// <summary>Payor code (or "CODE - Name").</summary>
    public string? Payor { get; set; }

    /// <summary>Endorsement bordx date.</summary>
    public DateTime? BordxDate { get; set; }

    /// <summary>Batch number.</summary>
    public string? BatchNo { get; set; }

    /// <summary>Invoice number.</summary>
    public string? InvoiceNo { get; set; }
}

/// <summary>What Print and Preview ask for: the criteria, the report type and whether an invoice number may be given out.</summary>
public class McoPrintRequest : McoFilter
{
    /// <summary>"Report Type": invoice (the invoice) or listing (the listing of the members).</summary>
    public string? ReportType { get; set; }

    /// <summary>True: Print (gives the invoice number to members that have none); false: Preview (writes nothing).</summary>
    public bool Allocate { get; set; }
}

/// <summary>cmdMCOSearch_Click's checks, in the desktop's order.</summary>
public sealed class McoFilterValidator : RequestValidator<McoFilter>
{
    /// <summary>Declares the rules.</summary>
    public McoFilterValidator()
    {
        RuleFor(f => f.Criteria).Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Please Enter MCO Criteria!");
        RuleFor(f => f.Criteria).Must(c => c is "batch" or "invoice").WithMessage("Select Bordx Date / Batch No or Invoice No.");
        RuleFor(f => f).Must(f => f.Criteria != "batch" || !string.IsNullOrWhiteSpace(McoRules.PayorCode(f.Payor))).WithMessage("Please Enter Payor!");
        RuleFor(f => f).Must(f => f.Criteria != "batch" || f.BordxDate is not null).WithMessage("Please Enter Bordereaux Date!");
        RuleFor(f => f).Must(f => f.Criteria != "batch" || !string.IsNullOrWhiteSpace(f.BatchNo)).WithMessage("Please Enter Batch No!");
        RuleFor(f => f).Must(f => f.Criteria != "batch" || McoRules.Batch(f.BatchNo) is not null).WithMessage("Batch No must be a number.");
        RuleFor(f => f).Must(f => f.Criteria != "invoice" || !string.IsNullOrWhiteSpace(f.InvoiceNo)).WithMessage("Please Enter Invoice No!");
    }
}

/// <summary>Print and Preview's check on top of the search's: the report type.</summary>
public sealed class McoPrintRequestValidator : RequestValidator<McoPrintRequest>
{
    /// <summary>Declares the rules.</summary>
    public McoPrintRequestValidator()
    {
        RuleFor(r => r.ReportType).Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Please Select Report Type !");
        RuleFor(r => r.ReportType).Must(t => t is "invoice" or "listing").WithMessage("Select Invoice or Listing.");
        Include(new McoFilterValidator());
    }
}

/// <summary>The rules of the screen.</summary>
public static class McoRules
{
    /// <summary>The payor code of "CODE - Name".</summary>
    public static string PayorCode(string? text) => (text ?? "").Trim().Split(" - ")[0].Trim().ToUpperInvariant();

    /// <summary>The batch number as the database holds it (digits without leading zeros), or null when it is not a number.</summary>
    public static string? Batch(string? text) => int.TryParse((text ?? "").Trim(), out var n) && n >= 0 ? n.ToString() : null;

    /// <summary>The invoice's amount for a line: the fee rounded to a whole number, as the desktop wrote it.</summary>
    public static decimal Rounded(decimal? fee) => Math.Round(fee ?? 0m, 0, MidpointRounding.AwayFromZero);
}

/// <summary>Reads and writes the reinstated members and their invoice numbers.</summary>
public interface IMcoReinstatementRepository
{
    /// <summary>The members of the batch or invoice (at most <see cref="WorksheetLimits.FetchRows"/>).</summary>
    Task<List<McoRow>> SearchAsync(IDbSession session, McoFilter filter);

    /// <summary>The payor the invoice is for (null when there is no such payor).</summary>
    Task<McoPayor?> PayorAsync(IDbSession session, string payor);

    /// <summary>The invoice lines of the batch or invoice.</summary>
    Task<List<McoInvoiceLine>> LinesAsync(IDbSession session, McoFilter filter);

    /// <summary>Takes the next MCO invoice number (and keeps its counter row locked until the transaction ends).</summary>
    Task<string> NextInvoiceAsync(IDbSession session, string payor, string prefix);

    /// <summary>Stamps the invoice on one member's or covered person's row; returns the rows changed.</summary>
    Task<int> StampAsync(IDbSession session, McoRow row, string invoiceNo, DateTime invoiceDate, string userCode);
}

/// <inheritdoc />
public sealed class McoReinstatementRepository : IMcoReinstatementRepository
{
    private static string? Payor(McoFilter f) => f.Criteria == "batch" ? McoRules.PayorCode(f.Payor) : null;

    private static DateTime? Day(McoFilter f) => f.Criteria == "batch" ? f.BordxDate?.Date : null;

    private static string? Batch(McoFilter f) => f.Criteria == "batch" ? McoRules.Batch(f.BatchNo) : null;

    private static string? Invoice(McoFilter f) => f.Criteria == "invoice" ? f.InvoiceNo!.Trim() : null;

    /// <inheritdoc />
    public Task<List<McoRow>> SearchAsync(IDbSession session, McoFilter f) =>
        session.Connection.ProcQueryAsync<McoRow>("genisis.McoReinstatement_Search", new { payor = Payor(f), bordxDate = Day(f), batch = Batch(f), invoiceNo = Invoice(f), max = WorksheetLimits.FetchRows }, session.Transaction);

    /// <inheritdoc />
    public async Task<McoPayor?> PayorAsync(IDbSession session, string payor) => await session.Connection.ProcFirstOrDefaultAsync<McoPayor>("genisis.McoReinstatement_PayorInfo", new { payor }, session.Transaction);

    /// <inheritdoc />
    public Task<List<McoInvoiceLine>> LinesAsync(IDbSession session, McoFilter f) =>
        session.Connection.ProcQueryAsync<McoInvoiceLine>("genisis.McoReinstatement_InvoiceLines", new { payor = Payor(f), bordxDate = Day(f), batch = int.TryParse(Batch(f), out var n) ? n : (int?)null, invoiceNo = Invoice(f) }, session.Transaction);

    /// <inheritdoc />
    public async Task<string> NextInvoiceAsync(IDbSession session, string payor, string prefix) =>
        (await session.Connection.ProcScalarAsync<string>("genisis.McoReinstatement_NextInvoice", new { payor, prefix }, session.Transaction))!;

    /// <inheritdoc />
    public async Task<int> StampAsync(IDbSession session, McoRow row, string invoiceNo, DateTime invoiceDate, string userCode) =>
        await session.Connection.ProcScalarAsync<int>("genisis.McoReinstatement_Stamp", new
        {
            memberNo = row.MemberNo, coverId = row.CoverId ?? "00", bordxDate = row.BordxDate?.Date, batch = (row.BatchNo ?? "").Trim(), invoiceNo, invoiceDate = invoiceDate.Date, userCode,
        }, session.Transaction);
}

/// <summary>Contribution Billing > MCO Reinstatement (legacy <c>frmMCOInvoiceReinstatement</c>, "MCO Reinstatement Invoice").</summary>
public class McoReinstatementService(IDbSessions sessions, IMcoReinstatementRepository repository, ILogger<McoReinstatementService> log)
{
    private static McoSearchResult Summarise(List<McoRow> found)
    {
        var capped = WorksheetLimits.Cap(found);
        var invoices = capped.Rows.Select(r => (r.InvoiceNo ?? "").Trim()).Where(n => n != "").Distinct().ToList();
        return new McoSearchResult(capped.Rows, capped.Rows.Count(r => !r.IsCovered), capped.Rows.Count(r => r.IsCovered), invoices, capped.Truncated);
    }

    /// <summary>MCOListing: the principals and covered persons of the batch or invoice, with the invoice numbers they have. Nothing found is a <see cref="NotFoundException"/> ("Record not found !").</summary>
    public async Task<McoSearchResult> SearchAsync(McoFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        var result = Summarise(await repository.SearchAsync(session, filter));
        if (result.Rows.Count == 0) throw new NotFoundException("Record not found !");
        return result;
    }

    /// <summary>
    /// Print and Preview. Print (Allocate) gives the members one new invoice number, in one transaction, when none of them has an invoice yet; when they all have the same one it is reprinted and nothing is
    /// written. A batch whose members have different invoice numbers, or only some of them an invoice, is refused ("Different Invoice Found in Same Batch or Range, Please Search by Invoice No !"; the
    /// desktop looked at the first member only). Preview writes nothing. The document is the invoice lines and payor (invoice) or the members (listing).
    /// </summary>
    public async Task<McoDocument> PrintAsync(McoPrintRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        var found = Summarise(await repository.SearchAsync(session, request));
        if (found.Rows.Count == 0) throw new NotFoundException("Record not found !");
        var invoiced = found.Rows.Count(r => !string.IsNullOrWhiteSpace(r.InvoiceNo));
        if (found.Invoices.Count > 1 || (invoiced > 0 && invoiced < found.Rows.Count))
            throw new BusinessRuleException("Different Invoice Found in Same Batch or Range, Please Search by Invoice No ! ");

        var payorCode = McoRules.PayorCode(found.Rows[0].PayorCode);
        var payor = await repository.PayorAsync(session, payorCode);
        var (allocated, invoiceNo, invoiceDate, rows) = (false, found.Invoices.FirstOrDefault(), found.Rows[0].InvoiceDate, found.Rows);
        var criteria = (McoFilter)request;
        if (request.Allocate && invoiced == 0)
        {
            invoiceNo = await repository.NextInvoiceAsync(session, payorCode, payor?.Prefix ?? "S");
            invoiceDate = DateTime.Today;
            foreach (var row in found.Rows) await repository.StampAsync(session, row, invoiceNo, invoiceDate.Value, userCode);
            allocated = true;
            criteria = new McoFilter { Criteria = "invoice", InvoiceNo = invoiceNo };
            rows = Summarise(await repository.SearchAsync(session, criteria)).Rows;
        }

        var lines = request.ReportType == "invoice" ? await repository.LinesAsync(session, criteria) : [];
        if (allocated)
        {
            await session.CommitAsync();
            log.LogInformation("MCO reinstatement invoice {Invoice} given to {Count} line(s) of payor {Payor} by {User}", invoiceNo, found.Rows.Count, payorCode, userCode);
        }

        return new McoDocument(request.ReportType!, allocated, invoiceNo, invoiceDate, payor, lines, rows);
    }
}
