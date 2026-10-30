using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;

namespace Genisis.Api.Receipt.Tracking;

/// <summary>One line of the detail ("By Claim") listing: a receipt line.</summary>
public record ReceiptTrackingDetailRow
{
    /// <summary>Receipt number.</summary>
    public string? ReceiptNo { get; set; }

    /// <summary>Receipt date.</summary>
    public DateTime? ReceiptDate { get; set; }

    /// <summary>Amount: the receipt amount for member receipts, the cheque amount for payor and MNRB receipts (what the desktop listed).</summary>
    public decimal? Amount { get; set; }

    /// <summary>Payor code.</summary>
    public string? PayorCode { get; set; }

    /// <summary>Member number.</summary>
    public string? MemberNo { get; set; }

    /// <summary>Member name.</summary>
    public string? MemberName { get; set; }

    /// <summary>Case number.</summary>
    public string? CaseNo { get; set; }

    /// <summary>Claim version.</summary>
    public int? ClaimVersion { get; set; }

    /// <summary>Bordx number.</summary>
    public string? BordxNo { get; set; }

    /// <summary>Debit note number.</summary>
    public string? DebitNoteNo { get; set; }

    /// <summary>Remarks.</summary>
    public string? Remarks { get; set; }

    /// <summary>Accounting period month.</summary>
    public string? AccMonth { get; set; }

    /// <summary>Accounting period year.</summary>
    public string? AccYear { get; set; }
}

/// <summary>One line of a summary: a bordx, a receipt or a payor with the number of cases and the amount.</summary>
public record ReceiptTrackingSummaryRow
{
    /// <summary>Bordx number, receipt number or payor code.</summary>
    public string? Key1 { get; set; }

    /// <summary>Bordx date or receipt date (empty for a payor).</summary>
    public DateTime? KeyDate { get; set; }

    /// <summary>Number of cases (receipts for a payor).</summary>
    public int Cases { get; set; }

    /// <summary>Amount received.</summary>
    public decimal? Amount { get; set; }
}

/// <summary>The lines of the layout asked for, the number of them and the total amount.</summary>
public record ReceiptTrackingResult(string Source, string Mode, List<ReceiptTrackingDetailRow> Detail, List<ReceiptTrackingSummaryRow> Summary, int Count, decimal TotalAmount, bool Truncated);

/// <summary>The criteria of the Receipt Tracking search. A summary only uses the health type, payor, receipt date, bordx and receipt number ranges.</summary>
public class ReceiptTrackingFilter
{
    /// <summary>Receipts from member, payor or mnrb.</summary>
    public string? Source { get; set; } = "member";

    /// <summary>claim (the detail, "By Claim"), bordx, receipt or payor (the summaries).</summary>
    public string? Mode { get; set; } = "claim";

    /// <summary>S or N.</summary>
    public string? HealthType { get; set; }

    /// <summary>Member number contains.</summary>
    public string? MemberNo { get; set; }

    /// <summary>Payor code (or "CODE - Name").</summary>
    public string? Payor { get; set; }

    /// <summary>Member name contains.</summary>
    public string? MemberName { get; set; }

    /// <summary>Claim (case) number from.</summary>
    public string? ClaimFrom { get; set; }

    /// <summary>Claim (case) number to.</summary>
    public string? ClaimTo { get; set; }

    /// <summary>Bordx number from.</summary>
    public string? BordxFrom { get; set; }

    /// <summary>Bordx number to.</summary>
    public string? BordxTo { get; set; }

    /// <summary>Receipt number from.</summary>
    public string? ReceiptFrom { get; set; }

    /// <summary>Receipt number to.</summary>
    public string? ReceiptTo { get; set; }

    /// <summary>Receipt date from.</summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>Receipt date to.</summary>
    public DateTime? DateTo { get; set; }

    /// <summary>Accounting period month from.</summary>
    public string? AcMonthFrom { get; set; }

    /// <summary>Accounting period month to.</summary>
    public string? AcMonthTo { get; set; }

    /// <summary>Accounting period year from.</summary>
    public string? AcYearFrom { get; set; }

    /// <summary>Accounting period year to.</summary>
    public string? AcYearTo { get; set; }

    /// <summary>Debit note number from.</summary>
    public string? DebitFrom { get; set; }

    /// <summary>Debit note number to.</summary>
    public string? DebitTo { get; set; }

    /// <summary>Amount from.</summary>
    public decimal? AmountFrom { get; set; }

    /// <summary>Amount to.</summary>
    public decimal? AmountTo { get; set; }
}

/// <summary>Source, layout and health type have to be the ones the option buttons and the drop-down offer.</summary>
public sealed class ReceiptTrackingFilterValidator : RequestValidator<ReceiptTrackingFilter>
{
    /// <summary>Declares the rules.</summary>
    public ReceiptTrackingFilterValidator()
    {
        RuleFor(f => f.Source).Must(s => s is "member" or "payor" or "mnrb").WithMessage("Select Payor, Member or MNRB.");
        RuleFor(f => f.Mode).Must(m => m is "claim" or "bordx" or "receipt" or "payor").WithMessage("Select By Bordx, By Claim, By Receipt No or By Payor.");
        RuleFor(f => f.HealthType).Must(h => string.IsNullOrWhiteSpace(h) || h.Trim().ToUpperInvariant()[..1] is "S" or "N").WithMessage("Health Type is S - Sihat or N - Non-Sihat.");
    }
}

/// <summary>Reads the receipts that came in.</summary>
public interface IReceiptTrackingRepository
{
    /// <summary>The detail listing (at most <see cref="WorksheetLimits.FetchRows"/> lines).</summary>
    Task<List<ReceiptTrackingDetailRow>> DetailAsync(IDbSession session, ReceiptTrackingFilter filter);

    /// <summary>A summary by bordx, receipt or payor (at most <see cref="WorksheetLimits.FetchRows"/> lines).</summary>
    Task<List<ReceiptTrackingSummaryRow>> SummaryAsync(IDbSession session, ReceiptTrackingFilter filter);
}

/// <inheritdoc />
public sealed class ReceiptTrackingRepository : IReceiptTrackingRepository
{
    private static string? Text(string? value) => WorksheetLimits.Text(value);

    private static string? Upper(string? value) => WorksheetLimits.Text(value)?.ToUpperInvariant();

    private static string? Health(string? value) => Text(value) is { } t ? t[..1].ToUpperInvariant() : null;

    private static string? PayorCode(string? value) => Upper((value ?? "").Split(" - ")[0]);

    /// <inheritdoc />
    public Task<List<ReceiptTrackingDetailRow>> DetailAsync(IDbSession session, ReceiptTrackingFilter f) =>
        session.Connection.ProcQueryAsync<ReceiptTrackingDetailRow>("genisis.ReceiptTracking_Detail", new
        {
            source = f.Source, health = Health(f.HealthType), memberNo = Upper(f.MemberNo), payor = PayorCode(f.Payor), memberName = Upper(f.MemberName), caseFrom = Text(f.ClaimFrom), caseTo = Text(f.ClaimTo),
            bordxFrom = Text(f.BordxFrom), bordxTo = Text(f.BordxTo), receiptFrom = Upper(f.ReceiptFrom), receiptTo = Upper(f.ReceiptTo), dateFrom = WorksheetLimits.Day(f.DateFrom), dateTo = WorksheetLimits.Day(f.DateTo),
            acMonthFrom = Text(f.AcMonthFrom), acMonthTo = Text(f.AcMonthTo), acYearFrom = Text(f.AcYearFrom), acYearTo = Text(f.AcYearTo), debitFrom = Upper(f.DebitFrom), debitTo = Upper(f.DebitTo),
            amountFrom = f.AmountFrom, amountTo = f.AmountTo, max = WorksheetLimits.FetchRows,
        }, session.Transaction);

    /// <inheritdoc />
    public Task<List<ReceiptTrackingSummaryRow>> SummaryAsync(IDbSession session, ReceiptTrackingFilter f) =>
        session.Connection.ProcQueryAsync<ReceiptTrackingSummaryRow>("genisis.ReceiptTracking_Summary", new
        {
            source = f.Source, by = f.Mode, health = Health(f.HealthType), payor = PayorCode(f.Payor), dateFrom = WorksheetLimits.Day(f.DateFrom), dateTo = WorksheetLimits.Day(f.DateTo),
            bordxFrom = Text(f.BordxFrom), bordxTo = Text(f.BordxTo), receiptFrom = Upper(f.ReceiptFrom), receiptTo = Upper(f.ReceiptTo), max = WorksheetLimits.FetchRows,
        }, session.Transaction);
}

/// <summary>Receipt > Receipt Tracking (legacy <c>FrmReceiptTracking</c>).</summary>
public class ReceiptTrackingService(IDbSessions sessions, IReceiptTrackingRepository repository)
{
    /// <summary>
    /// SearchRecord: the receipts that came in from members, payors or MNRB, listed by claim or summed by bordx, receipt number or payor. The count and the total add up the lines listed
    /// (the desktop added the amount column it showed).
    /// </summary>
    public async Task<ReceiptTrackingResult> SearchAsync(ReceiptTrackingFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        if (filter.Mode == "claim")
        {
            var capped = WorksheetLimits.Cap(await repository.DetailAsync(session, filter));
            return new ReceiptTrackingResult(filter.Source!, "claim", capped.Rows, [], capped.Rows.Count, capped.Rows.Sum(r => r.Amount ?? 0), capped.Truncated);
        }

        var summary = WorksheetLimits.Cap(await repository.SummaryAsync(session, filter));
        return new ReceiptTrackingResult(filter.Source!, filter.Mode!, [], summary.Rows, summary.Rows.Count, summary.Rows.Sum(r => r.Amount ?? 0), summary.Truncated);
    }
}
