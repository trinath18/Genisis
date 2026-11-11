using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Invoice;

/// <summary>The case an invoice belongs to, with the member, plan and hospital the Invoice Processing form shows above its invoice list.</summary>
public record InvoiceCaseHeader
{
    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Membership number.</summary>
    public string? MemberNo { get; set; }

    /// <summary>Cover id of the patient (00 is the member).</summary>
    public string? CoverId { get; set; }

    /// <summary>Member name.</summary>
    public string? MemberName { get; set; }

    /// <summary>Patient name when the patient is a covered person.</summary>
    public string? PatientName { get; set; }

    /// <summary>Payor code.</summary>
    public string? Payor { get; set; }

    /// <summary>Plan code.</summary>
    public string? PlanCode { get; set; }

    /// <summary>Insured type.</summary>
    public string? InsuredType { get; set; }

    /// <summary>Health type.</summary>
    public string? HealthType { get; set; }

    /// <summary>Hospital code of the case.</summary>
    public string? HospitalCode { get; set; }

    /// <summary>Hospital name.</summary>
    public string? Hospital { get; set; }

    /// <summary>Date of admission.</summary>
    public DateTime? Admitted { get; set; }

    /// <summary>Date of discharge.</summary>
    public DateTime? Discharged { get; set; }

    /// <summary>Stay type of the case.</summary>
    public string? StayType { get; set; }

    /// <summary>Pay type of the case.</summary>
    public string? PayType { get; set; }

    /// <summary>Claimability code.</summary>
    public string? Claimability { get; set; }

    /// <summary>The last worksheet version the case has; <c>null</c> when it has no worksheet yet.</summary>
    public int? LastWorksheetVersion { get; set; }

    /// <summary>The worksheet version a new invoice is attached to: the last completed version plus one, or 1.</summary>
    public int NextWorksheetVersion { get; set; }

    /// <summary>The case's modality: <c>S</c> surgical, <c>N</c> non-surgical; the form enables the matching frame.</summary>
    public string? Modality { get; set; }

    /// <summary>The remark kept for the case (one per case); a new invoice's form starts from it so saving does not wipe it.</summary>
    public string? Remarks { get; set; }
}

/// <summary>One invoice of a case, as the form's invoice list shows it.</summary>
public record InvoiceSummary
{
    /// <summary>The invoice's index (its key).</summary>
    public int Index { get; set; }

    /// <summary>Invoice number.</summary>
    public string? InvoiceNo { get; set; }

    /// <summary>Invoice date.</summary>
    public DateTime? InvoiceDate { get; set; }

    /// <summary>Who is paid: H hospital, M member (and others).</summary>
    public string? PayTo { get; set; }

    /// <summary>The hospital code, or the payee as typed.</summary>
    public string? Payee { get; set; }

    /// <summary>The payee's name: the hospital's name when a hospital is paid.</summary>
    public string? PayeeName { get; set; }

    /// <summary>Invoice amount.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Whether a worksheet has used the invoice (it cannot then be removed).</summary>
    public bool InWorksheet { get; set; }

    /// <summary>The worksheet version the invoice belongs to.</summary>
    public string? WorksheetVersion { get; set; }

    /// <summary>Bordx reference number once bordxed.</summary>
    public string? BordxNo { get; set; }

    /// <summary>Stay type.</summary>
    public string? StayType { get; set; }

    /// <summary>Pay type.</summary>
    public string? PayType { get; set; }

    /// <summary>Receipt mode.</summary>
    public string? ReceiptMode { get; set; }

    /// <summary>Invoice receipt date.</summary>
    public DateTime? ReceiptDate { get; set; }

    /// <summary>Follow-up date.</summary>
    public DateTime? FollowUpDate { get; set; }

    /// <summary>Room type.</summary>
    public string? RoomType { get; set; }

    /// <summary>Excess amount.</summary>
    public decimal? ExcessAmount { get; set; }

    /// <summary>Amount to pay.</summary>
    public decimal? PayAmount { get; set; }

    /// <summary>The initial invoice amount.</summary>
    public decimal? InitialAmount { get; set; }

    /// <summary>UHC number.</summary>
    public string? UhcNo { get; set; }

    /// <summary>Who entered the invoice.</summary>
    public string? EnteredBy { get; set; }

    /// <summary>When it was entered.</summary>
    public DateTime? EnteredDate { get; set; }
}

/// <summary>The case's header and invoices.</summary>
/// <param name="Header">The case.</param>
/// <param name="Invoices">Its invoices, oldest first.</param>
public record InvoiceCaseResult(InvoiceCaseHeader Header, List<InvoiceSummary> Invoices);

/// <summary>One worksheet row of an invoice.</summary>
public record InvoiceRowAmount
{
    /// <summary>Grid row, 1 to 29.</summary>
    public int Slot { get; set; }

    /// <summary>Benefit category.</summary>
    public string? Category { get; set; }

    /// <summary>Benefit name.</summary>
    public string Benefit { get; set; } = "";

    /// <summary>The invoice's amount in the row.</summary>
    public decimal Amount { get; set; }
}

/// <summary>An invoice with its amounts by worksheet row.</summary>
public record InvoiceDetail
{
    /// <summary>The invoice.</summary>
    public InvoiceSummary Invoice { get; set; } = new();

    /// <summary>The 29 rows.</summary>
    public List<InvoiceRowAmount> Rows { get; set; } = [];

    /// <summary>The rows added up.</summary>
    public decimal RowsTotal { get; set; }

    /// <summary>Whether the rows add up to the invoice amount that was saved (the form's <c>getTotal</c>); false for an invoice that was changed after it was saved.</summary>
    public bool RowsMatchAmount { get; set; }
}

/// <summary>Row captions as the worksheet shows them (the result of <c>WorksheetCalc_Captions</c>).</summary>
public record InvoiceRowCaption(int Slot, string? BenefitCode, string? Category, string Benefit);

/// <summary>Reads the invoices of a case.</summary>
public interface IInvoiceRepository
{
    /// <summary>The case header; <c>null</c> when the case is not there.</summary>
    Task<InvoiceCaseHeader?> HeaderAsync(IDbSession session, string caseNo);

    /// <summary>The case's invoices, oldest first.</summary>
    Task<List<InvoiceSummary>> InvoicesAsync(IDbSession session, string caseNo);

    /// <summary>An invoice's amounts per worksheet row (slot 1 to 29).</summary>
    Task<List<(int Slot, decimal Amount)>> RowsAsync(IDbSession session, int invoiceIndex);

    /// <summary>The captions of the 29 rows.</summary>
    Task<List<InvoiceRowCaption>> CaptionsAsync(IDbSession session);
}

/// <inheritdoc />
public sealed class InvoiceRepository : IInvoiceRepository
{
    private sealed record RowDto(int Slot, decimal Amount);

    /// <inheritdoc />
    public Task<InvoiceCaseHeader?> HeaderAsync(IDbSession session, string caseNo) =>
        session.Connection.ProcFirstOrDefaultAsync<InvoiceCaseHeader>("genisis.Invoice_Case", new { cas = caseNo }, session.Transaction);

    /// <inheritdoc />
    public Task<List<InvoiceSummary>> InvoicesAsync(IDbSession session, string caseNo) =>
        session.Connection.ProcQueryAsync<InvoiceSummary>("genisis.Invoice_List", new { cas = caseNo }, session.Transaction);

    /// <inheritdoc />
    public async Task<List<(int Slot, decimal Amount)>> RowsAsync(IDbSession session, int invoiceIndex) =>
        (await session.Connection.ProcQueryAsync<RowDto>("genisis.Invoice_Rows", new { invoice = invoiceIndex }, session.Transaction)).Select(r => (r.Slot, r.Amount)).ToList();

    /// <inheritdoc />
    public Task<List<InvoiceRowCaption>> CaptionsAsync(IDbSession session) =>
        session.Connection.ProcQueryAsync<InvoiceRowCaption>("genisis.WorksheetCalc_Captions", null, session.Transaction);
}

/// <summary>Invoice Processing, read side (legacy <c>frmInvoice</c>): a case's invoices and one invoice's amounts. Nothing is written.</summary>
public class InvoiceService(IDbSessions sessions, IInvoiceRepository repository)
{
    private const string NotFound = "Record not found!";

    /// <summary>The case and its invoices. Throws <see cref="NotFoundException"/> ("Record not found!") when the case is not there.</summary>
    public async Task<InvoiceCaseResult> GetCaseAsync(string caseNo)
    {
        var cas = caseNo.Trim();
        await using var session = await sessions.OpenAsync();
        var header = await repository.HeaderAsync(session, cas) ?? throw new NotFoundException(NotFound);
        return new InvoiceCaseResult(header, await repository.InvoicesAsync(session, cas));
    }

    /// <summary>One invoice of the case with its amounts by row. Throws <see cref="NotFoundException"/> when the case has no such invoice.</summary>
    public async Task<InvoiceDetail> GetInvoiceAsync(string caseNo, int invoiceIndex)
    {
        var cas = caseNo.Trim();
        await using var session = await sessions.OpenAsync();
        var invoice = (await repository.InvoicesAsync(session, cas)).FirstOrDefault(i => i.Index == invoiceIndex) ?? throw new NotFoundException(NotFound);
        var captions = (await repository.CaptionsAsync(session)).ToDictionary(c => c.Slot);
        var rows = (await repository.RowsAsync(session, invoiceIndex)).Select(r =>
        {
            captions.TryGetValue(r.Slot, out var caption);
            return new InvoiceRowAmount { Slot = r.Slot, Category = caption?.Category, Benefit = caption?.Benefit ?? $"Row {r.Slot}", Amount = r.Amount };
        }).ToList();
        var total = rows.Sum(r => r.Amount);
        return new InvoiceDetail { Invoice = invoice, Rows = rows, RowsTotal = total, RowsMatchAmount = Math.Abs(total - (invoice.Amount ?? 0m)) < 0.011m };
    }
}
