using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.ContributionBilling.McoReinstatement;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;

namespace Genisis.Api.ContributionBilling.GenerateInvoice;

/// <summary>A member or covered person with MCO fees (a line of <c>lvwMCO</c>).</summary>
public record McoInvoiceRow
{
    /// <summary>Payor code.</summary>
    public string? PayorCode { get; set; }

    /// <summary>Member number.</summary>
    public string MemberNo { get; set; } = "";

    /// <summary>Cover id (00 for the principal).</summary>
    public string? CoverId { get; set; }

    /// <summary>Relation (P principal).</summary>
    public string? Relation { get; set; }

    /// <summary>Bordx date.</summary>
    public DateTime? BordxDate { get; set; }

    /// <summary>Policy number.</summary>
    public string? PolicyNo { get; set; }

    /// <summary>Health type.</summary>
    public string? HealthType { get; set; }

    /// <summary>Bordx batch.</summary>
    public int? BatchNo { get; set; }

    /// <summary>Member name.</summary>
    public string? MemberName { get; set; }

    /// <summary>Covered person's name.</summary>
    public string? CoveredName { get; set; }

    /// <summary>Group company.</summary>
    public string? GroupCompany { get; set; }

    /// <summary>Department.</summary>
    public string? Department { get; set; }

    /// <summary>Invoice number (empty until the invoice is printed).</summary>
    public string? InvoiceNo { get; set; }

    /// <summary>Invoice date.</summary>
    public DateTime? InvoiceDate { get; set; }

    /// <summary>Plan code.</summary>
    public string? PlanCode { get; set; }

    /// <summary>Insurance code.</summary>
    public string? InsCode { get; set; }

    /// <summary>Policy effective date.</summary>
    public DateTime? EffDate { get; set; }

    /// <summary>Policy expiry date.</summary>
    public DateTime? ExpDate { get; set; }

    /// <summary>Adult or Child.</summary>
    public string? AdultChild { get; set; }

    /// <summary>Basic MCO.</summary>
    public decimal? BasicMco { get; set; }

    /// <summary>MCO fee.</summary>
    public decimal? McoFee { get; set; }

    /// <summary>Premium.</summary>
    public decimal? Premium { get; set; }

    /// <summary>IC of the member.</summary>
    public string? IcNo { get; set; }

    /// <summary>True for a principal.</summary>
    public bool IsPrincipal => (Relation ?? "").Trim() == "P";
}

/// <summary>The list, its counts and what the desktop's search noted about it.</summary>
public record McoInvoiceSearchResult(List<McoInvoiceRow> Rows, int Principals, int Supplementary, List<string> Invoices, bool Billed, bool MultipleInvoices, bool FeeEmpty, bool Truncated);

/// <summary>Who the invoice is made out to.</summary>
public record McoInvoicePayor(string Code, string? Name, string? Address1, string? Address2, string? Address3, string? Contact, string? Telephone, string? Fax, string Prefix, bool ThirdParty);

/// <summary>A line of the invoice: members of one group, insurance code, plan and adult / child.</summary>
public record McoInvoiceLine(string? Group, string? InsCode, string? PlanCode, string? AdultChild, int Principals, int Members, decimal BasicMco, decimal McoFee);

/// <summary>What Print and Preview give back.</summary>
public record McoInvoiceDocument(string ReportType, string ListingVersion, bool Allocated, string? InvoiceNo, DateTime? InvoiceDate, McoInvoicePayor? Payor, string GroupBy, List<McoInvoiceLine> Lines, List<McoInvoiceRow> Rows);

/// <summary>The criteria of the search (and of Print and Preview).</summary>
public class McoInvoiceFilter
{
    /// <summary>"MCO Criteria": batch (Bordereaux Batch), range (Bordereaux Range) or invoice (Invoice Number).</summary>
    public string? Criteria { get; set; }

    /// <summary>Payor code (or "CODE - Name").</summary>
    public string? Payor { get; set; }

    /// <summary>Bordx date (batch).</summary>
    public DateTime? BordxDate { get; set; }

    /// <summary>Batch number (batch).</summary>
    public string? BatchNo { get; set; }

    /// <summary>Bordx date from (range).</summary>
    public DateTime? BordxFrom { get; set; }

    /// <summary>Bordx date to (range).</summary>
    public DateTime? BordxTo { get; set; }

    /// <summary>Invoice number (invoice).</summary>
    public string? InvoiceNo { get; set; }

    /// <summary>By Payor, By Department or By Group Company: payor, department or group.</summary>
    public string? GroupBy { get; set; } = "payor";

    /// <summary>Group company (By Group Company).</summary>
    public string? GroupCompany { get; set; }

    /// <summary>The "- Principal" criteria: the report lists the principals only.</summary>
    public bool PrincipalsOnly { get; set; }
}

/// <summary>What Print and Preview ask for.</summary>
public class McoInvoicePrintRequest : McoInvoiceFilter
{
    /// <summary>"Report Type": invoice or listing (Fee Listing).</summary>
    public string? ReportType { get; set; }

    /// <summary>"Listing Versions": plain (MCO Listing) or premium (MCO Listing With Premium).</summary>
    public string? ListingVersion { get; set; } = "plain";

    /// <summary>True: Print (gives the members a new invoice number); false: Preview (writes nothing).</summary>
    public bool Allocate { get; set; }
}

/// <summary>cmdMCOSearch_Click's checks, in the desktop's order.</summary>
public sealed class McoInvoiceFilterValidator : RequestValidator<McoInvoiceFilter>
{
    /// <summary>Declares the rules.</summary>
    public McoInvoiceFilterValidator()
    {
        RuleFor(f => f.Criteria).Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Please Enter MCO Criteria!");
        RuleFor(f => f.Criteria).Must(c => c is "batch" or "range" or "invoice").WithMessage("Select Bordereaux Batch, Bordereaux Range or Invoice Number.");
        RuleFor(f => f.GroupBy).Must(g => g is "payor" or "department" or "group").WithMessage("Select By Payor, By Department or By Group Company.");
        RuleFor(f => f).Must(f => f.Criteria == "invoice" || !string.IsNullOrWhiteSpace(McoRules.PayorCode(f.Payor))).WithMessage("Please Enter Payor!");
        RuleFor(f => f).Must(f => f.Criteria != "batch" || f.BordxDate is not null).WithMessage("Please Enter Bordereaux Date!");
        RuleFor(f => f).Must(f => f.Criteria != "batch" || !string.IsNullOrWhiteSpace(f.BatchNo)).WithMessage("Please Enter Batch No!");
        RuleFor(f => f).Must(f => f.Criteria != "batch" || McoRules.Batch(f.BatchNo) is not null).WithMessage("Batch No must be a number.");
        RuleFor(f => f).Must(f => f.Criteria != "range" || f.BordxFrom is not null).WithMessage("Please Enter Bordereaux Date From!");
        RuleFor(f => f).Must(f => f.Criteria != "range" || f.BordxTo is not null).WithMessage("Please Enter Bordereaux Date To!");
        RuleFor(f => f).Must(f => f.Criteria != "invoice" || !string.IsNullOrWhiteSpace(f.InvoiceNo)).WithMessage("Please Enter Invoice No!");
        RuleFor(f => f).Must(f => f.GroupBy != "group" || f.Criteria == "invoice" || !string.IsNullOrWhiteSpace(f.GroupCompany)).WithMessage("Please Enter Group Company!");
    }
}

/// <summary>Print and Preview's checks: the report type, then the search's.</summary>
public sealed class McoInvoicePrintRequestValidator : RequestValidator<McoInvoicePrintRequest>
{
    /// <summary>Declares the rules.</summary>
    public McoInvoicePrintRequestValidator()
    {
        RuleFor(r => r.ReportType).Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("Please Select Report Type !");
        RuleFor(r => r.ReportType).Must(t => t is "invoice" or "listing").WithMessage("Select Invoice or Fee Listing.");
        RuleFor(r => r.ListingVersion).Must(v => v is "plain" or "premium").WithMessage("Please Enter Leaflet Version !");
        Include(new McoInvoiceFilterValidator());
    }
}

/// <summary>Reads the members and writes the invoice.</summary>
public interface IMcoInvoiceRepository
{
    /// <summary>The members that match (at most <see cref="WorksheetLimits.FetchRows"/>).</summary>
    Task<List<McoInvoiceRow>> SearchAsync(IDbSession session, McoInvoiceFilter filter);

    /// <summary>The group companies of a payor.</summary>
    Task<List<string>> GroupCompaniesAsync(IDbSession session, string payor);

    /// <summary>Who the invoice is made out to (null when there is no such payor).</summary>
    Task<McoInvoicePayor?> PayorAsync(IDbSession session, string payor);

    /// <summary>The next MCO invoice number (the counters MCO Reinstatement uses, locked until the transaction ends).</summary>
    Task<string> NextInvoiceAsync(IDbSession session, string payor, string prefix);

    /// <summary>Stamps the invoice on a member or covered person, sets the member's MCOPending to B and adds the MBMMCOInvoice row.</summary>
    Task StampAsync(IDbSession session, McoInvoiceRow row, string invoiceNo, DateTime invoiceDate, string userCode);
}

/// <inheritdoc />
public sealed class McoInvoiceRepository : IMcoInvoiceRepository
{
    private static string? Text(string? value) => WorksheetLimits.Text(value);

    /// <inheritdoc />
    public Task<List<McoInvoiceRow>> SearchAsync(IDbSession session, McoInvoiceFilter f)
    {
        var invoice = f.Criteria == "invoice";
        return session.Connection.ProcQueryAsync<McoInvoiceRow>("genisis.McoInvoice_Search", new
        {
            payor = invoice ? null : McoRules.PayorCode(f.Payor), bordxDate = f.Criteria == "batch" ? f.BordxDate?.Date : null, batch = f.Criteria == "batch" && int.TryParse(McoRules.Batch(f.BatchNo), out var b) ? b : (int?)null,
            bordxFrom = f.Criteria == "range" ? f.BordxFrom?.Date : null, bordxTo = f.Criteria == "range" ? f.BordxTo?.Date : null, invoiceNo = invoice ? Text(f.InvoiceNo) : null,
            groupCompany = f.GroupBy == "group" && !invoice ? Text(f.GroupCompany) : null, max = WorksheetLimits.FetchRows,
        }, session.Transaction);
    }

    /// <inheritdoc />
    public async Task<List<string>> GroupCompaniesAsync(IDbSession session, string payor) =>
        (await session.Connection.ProcQueryAsync<string>("genisis.McoInvoice_GroupCompanies", new { payor }, session.Transaction));

    /// <inheritdoc />
    public async Task<McoInvoicePayor?> PayorAsync(IDbSession session, string payor) =>
        await session.Connection.ProcFirstOrDefaultAsync<McoInvoicePayor>("genisis.McoInvoice_PayorInfo", new { payor }, session.Transaction);

    /// <inheritdoc />
    public async Task<string> NextInvoiceAsync(IDbSession session, string payor, string prefix) =>
        (await session.Connection.ProcScalarAsync<string>("genisis.McoReinstatement_NextInvoice", new { payor, prefix }, session.Transaction))!;

    /// <inheritdoc />
    public Task StampAsync(IDbSession session, McoInvoiceRow r, string invoiceNo, DateTime invoiceDate, string userCode) =>
        session.Connection.ProcExecAsync("genisis.McoInvoice_Stamp", new
        {
            memberNo = r.MemberNo, coverId = (r.CoverId ?? "00").Trim(), invoiceNo, invoiceDate = invoiceDate.Date, userCode, planCode = r.PlanCode, insCode = r.InsCode, effDate = r.EffDate, expDate = r.ExpDate,
            adultChild = (r.AdultChild ?? "").Trim() is { Length: > 0 } a ? a[..1] : null, basicMco = r.BasicMco ?? 0m, mcoFee = r.McoFee ?? 0m,
        }, session.Transaction);
}

/// <summary>Contribution Billing > Generate Invoice (legacy <c>frmMCOFeesListingNew</c>, "Member MCO Fees").</summary>
public class McoInvoiceService(IDbSessions sessions, IMcoInvoiceRepository repository, ILogger<McoInvoiceService> log)
{
    private static McoInvoiceSearchResult Summarise(List<McoInvoiceRow> found)
    {
        var capped = WorksheetLimits.Cap(found);
        var invoices = capped.Rows.Select(r => (r.InvoiceNo ?? "").Trim()).Where(n => n != "").Distinct().ToList();
        return new McoInvoiceSearchResult(capped.Rows, capped.Rows.Count(r => r.IsPrincipal), capped.Rows.Count(r => !r.IsPrincipal), invoices, invoices.Count > 0, invoices.Count > 1, capped.Rows.Any(r => r.McoFee is null), capped.Truncated);
    }

    /// <summary>MCOListing: the members of the batch, range or invoice with the counts, whether they are billed, whether more than one invoice was found and whether a fee is empty. Nothing found is a <see cref="NotFoundException"/> ("Record not found !").</summary>
    public async Task<McoInvoiceSearchResult> SearchAsync(McoInvoiceFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        var result = Summarise(await repository.SearchAsync(session, filter));
        if (result.Rows.Count == 0) throw new NotFoundException("Record not found !");
        return result;
    }

    /// <summary>cboMCOPayor_Click: the group companies of the payor, for By Department and By Group Company.</summary>
    public async Task<List<string>> GroupCompaniesAsync(string? payor)
    {
        await using var session = await sessions.OpenAsync();
        return await repository.GroupCompaniesAsync(session, McoRules.PayorCode(payor));
    }

    private static List<McoInvoiceLine> Lines(IEnumerable<McoInvoiceRow> rows, string groupBy) =>
        rows.GroupBy(r => (Group: groupBy == "department" ? (r.Department ?? "").Trim() : groupBy == "group" ? (r.GroupCompany ?? "").Trim() : "", Ins: (r.InsCode ?? "").Trim(), Plan: (r.PlanCode ?? "").Trim(), Type: (r.AdultChild ?? "").Trim()))
            .OrderBy(g => g.Key.Group, StringComparer.Ordinal).ThenBy(g => g.Key.Ins, StringComparer.Ordinal).ThenBy(g => g.Key.Plan, StringComparer.Ordinal).ThenBy(g => g.Key.Type, StringComparer.Ordinal)
            .Select(g => new McoInvoiceLine(g.Key.Group == "" ? null : g.Key.Group, g.Key.Ins, g.Key.Plan, g.Key.Type, g.Count(r => r.IsPrincipal), g.Count(), g.Sum(r => r.BasicMco ?? 0), g.Sum(r => r.McoFee ?? 0))).ToList();

    /// <summary>
    /// Print and Preview. Print (Allocate) gives the members of a batch or range one new invoice number, stamps each member and covered person, sets the members' MCOPending to B and adds the MBMMCOInvoice rows, in
    /// one transaction; it is refused for members that are billed already (the desktop disabled the button). More than one invoice in the list is refused ("Different Invoice Found in Same Batch or Range, Please
    /// Search by Invoice No !"). Preview writes nothing. The document is the invoice (lines by group, insurance code, plan and adult / child) or the fee listing, of the principals only for the "- Principal" criteria.
    /// </summary>
    public async Task<McoInvoiceDocument> PrintAsync(McoInvoicePrintRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        var found = Summarise(await repository.SearchAsync(session, request));
        if (found.Rows.Count == 0) throw new NotFoundException("Record not found !");
        if (found.MultipleInvoices) throw new BusinessRuleException("Different Invoice Found in Same Batch or Range, Please Search by Invoice No ! ");
        if (request.Allocate && found.Billed) throw new BusinessRuleException("These members are billed already. Use Preview to print the invoice again.");

        var payorCode = McoRules.PayorCode(found.Rows[0].PayorCode);
        var payor = await repository.PayorAsync(session, payorCode);
        var (allocated, invoiceNo, invoiceDate, rows) = (false, found.Invoices.FirstOrDefault(), found.Rows[0].InvoiceDate, found.Rows);
        if (request.Allocate)
        {
            invoiceNo = await repository.NextInvoiceAsync(session, payorCode, payor?.Prefix ?? "S");
            invoiceDate = DateTime.Today;
            foreach (var row in found.Rows) await repository.StampAsync(session, row, invoiceNo, invoiceDate.Value, userCode);
            allocated = true;
            rows = Summarise(await repository.SearchAsync(session, new McoInvoiceFilter { Criteria = "invoice", InvoiceNo = invoiceNo })).Rows;
            await session.CommitAsync();
            log.LogInformation("MCO invoice {Invoice} given to {Count} line(s) of payor {Payor} by {User}", invoiceNo, found.Rows.Count, payorCode, userCode);
        }

        var shown = request.PrincipalsOnly ? rows.Where(r => r.IsPrincipal).ToList() : rows;
        return new McoInvoiceDocument(request.ReportType!, request.ListingVersion!, allocated, invoiceNo, invoiceDate, payor, request.GroupBy ?? "payor", request.ReportType == "invoice" ? Lines(shown, request.GroupBy ?? "payor") : [], shown);
    }
}
