using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;

namespace Genisis.Api.Payment.Tracking;

/// <summary>One line of the detail ("By Claim") listing: a payment voucher line.</summary>
public record PaymentTrackingDetailRow
{
    /// <summary>PV number.</summary>
    public string? PvNo { get; set; }

    /// <summary>PV date.</summary>
    public DateTime? PvDate { get; set; }

    /// <summary>Amount paid (HOSPaidAmt / MBMPaidAmt).</summary>
    public decimal? Amount { get; set; }

    /// <summary>Case number.</summary>
    public string? CaseNo { get; set; }

    /// <summary>Claim version.</summary>
    public string? ClaimVersion { get; set; }

    /// <summary>Member number.</summary>
    public string? MemberNo { get; set; }

    /// <summary>Member name.</summary>
    public string? MemberName { get; set; }

    /// <summary>Hospital name.</summary>
    public string? HospitalName { get; set; }

    /// <summary>Payor code.</summary>
    public string? PayorCode { get; set; }

    /// <summary>Bordx number.</summary>
    public string? BordxNo { get; set; }

    /// <summary>Debit note number (payments to members).</summary>
    public string? DebitNoteNo { get; set; }

    /// <summary>Payee (payments to members).</summary>
    public string? PayTo { get; set; }

    /// <summary>Payment status (PVPayHOSStatus / PVPayMBMStatus: F full, P part).</summary>
    public string? Status { get; set; }

    /// <summary>Remark.</summary>
    public string? Remark { get; set; }

    /// <summary>Accounting period month.</summary>
    public string? AccMonth { get; set; }

    /// <summary>Accounting period year.</summary>
    public string? AccYear { get; set; }
}

/// <summary>One line of a summary: the grouping key, its date when it has one, the number of cases and the amount paid.</summary>
public record PaymentTrackingSummaryRow
{
    /// <summary>Bordx, claim, hospital, member, payor or PV number.</summary>
    public string? Key1 { get; set; }

    /// <summary>Bordx date or PV date (empty for the layouts without one).</summary>
    public DateTime? KeyDate { get; set; }

    /// <summary>Number of cases.</summary>
    public int Cases { get; set; }

    /// <summary>Amount paid.</summary>
    public decimal? Amount { get; set; }
}

/// <summary>The lines of the layout asked for, the number of them and the total amount.</summary>
public record PaymentTrackingResult(string Source, string Mode, List<PaymentTrackingDetailRow> Detail, List<PaymentTrackingSummaryRow> Summary, int Count, decimal TotalAmount, bool Truncated);

/// <summary>The criteria of the Payment Tracking search. Every criterion applies to every layout.</summary>
public class PaymentTrackingFilter
{
    /// <summary>The layouts: claim (the detail) and the seven summaries.</summary>
    public static readonly string[] Modes = ["claim", "bordx", "case", "hospitalDate", "member", "payorDate", "pv", "hospital"];

    /// <summary>Payments to hospital or member.</summary>
    public string? Source { get; set; } = "hospital";

    /// <summary>One of <see cref="Modes"/>.</summary>
    public string? Mode { get; set; } = "claim";

    /// <summary>S or N.</summary>
    public string? HealthType { get; set; }

    /// <summary>Payor code (or "CODE - Name").</summary>
    public string? Payor { get; set; }

    /// <summary>Member number contains.</summary>
    public string? MemberNo { get; set; }

    /// <summary>Member name contains.</summary>
    public string? MemberName { get; set; }

    /// <summary>Hospital name contains.</summary>
    public string? Hospital { get; set; }

    /// <summary>Payment status: F (full) or P (part).</summary>
    public string? Status { get; set; }

    /// <summary>Claim (case) number from.</summary>
    public string? ClaimFrom { get; set; }

    /// <summary>Claim (case) number to.</summary>
    public string? ClaimTo { get; set; }

    /// <summary>Bordx number from.</summary>
    public string? BordxFrom { get; set; }

    /// <summary>Bordx number to.</summary>
    public string? BordxTo { get; set; }

    /// <summary>PV number from.</summary>
    public string? PvFrom { get; set; }

    /// <summary>PV number to.</summary>
    public string? PvTo { get; set; }

    /// <summary>PV date from.</summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>PV date to.</summary>
    public DateTime? DateTo { get; set; }

    /// <summary>Accounting period month from.</summary>
    public string? AcMonthFrom { get; set; }

    /// <summary>Accounting period month to.</summary>
    public string? AcMonthTo { get; set; }

    /// <summary>Accounting period year from.</summary>
    public string? AcYearFrom { get; set; }

    /// <summary>Accounting period year to.</summary>
    public string? AcYearTo { get; set; }

    /// <summary>Debit note number from (payments to members).</summary>
    public string? DebitFrom { get; set; }

    /// <summary>Debit note number to.</summary>
    public string? DebitTo { get; set; }
}

/// <summary>Source, layout, health type and status have to be ones the screen offers.</summary>
public sealed class PaymentTrackingFilterValidator : RequestValidator<PaymentTrackingFilter>
{
    /// <summary>Declares the rules.</summary>
    public PaymentTrackingFilterValidator()
    {
        RuleFor(f => f.Source).Must(s => s is "hospital" or "member").WithMessage("Select Payment To Hospital or To Member.");
        RuleFor(f => f.Mode).Must(m => m is not null && PaymentTrackingFilter.Modes.Contains(m)).WithMessage("Select a report type.");
        RuleFor(f => f.HealthType).Must(h => string.IsNullOrWhiteSpace(h) || h.Trim().ToUpperInvariant()[..1] is "S" or "N").WithMessage("Health Type is S - Sihat or N - Non-Sihat.");
        RuleFor(f => f.Status).Must(s => string.IsNullOrWhiteSpace(s) || s.Trim().ToUpperInvariant() is "F" or "P").WithMessage("Payment Status is F - Full or P - Part.");
        RuleFor(f => f).Must(f => f.DateFrom is null || f.DateTo is null || f.DateFrom <= f.DateTo).WithMessage("PV Date From cannot be later than PV Date To.");
    }
}

/// <summary>Reads the payment vouchers.</summary>
public interface IPaymentTrackingRepository
{
    /// <summary>The detail listing (at most <see cref="WorksheetLimits.FetchRows"/> lines).</summary>
    Task<List<PaymentTrackingDetailRow>> DetailAsync(IDbSession session, PaymentTrackingFilter filter);

    /// <summary>A summary (at most <see cref="WorksheetLimits.FetchRows"/> lines).</summary>
    Task<List<PaymentTrackingSummaryRow>> SummaryAsync(IDbSession session, PaymentTrackingFilter filter);
}

/// <inheritdoc />
public sealed class PaymentTrackingRepository : IPaymentTrackingRepository
{
    private const string Procedure = "genisis.PaymentTracking_Search";

    private static string? Text(string? value) => WorksheetLimits.Text(value);

    private static string? Upper(string? value) => WorksheetLimits.Text(value)?.ToUpperInvariant();

    private static object Parameters(PaymentTrackingFilter f) => new
    {
        source = f.Source, by = f.Mode, health = Text(f.HealthType) is { } h ? h[..1].ToUpperInvariant() : null, payor = Upper((f.Payor ?? "").Split(" - ")[0]),
        memberNo = Upper(f.MemberNo), memberName = Upper(f.MemberName), hospital = Upper(f.Hospital), status = Upper(f.Status), caseFrom = Upper(f.ClaimFrom), caseTo = Upper(f.ClaimTo),
        bordxFrom = Upper(f.BordxFrom), bordxTo = Upper(f.BordxTo), pvFrom = Upper(f.PvFrom), pvTo = Upper(f.PvTo), dateFrom = WorksheetLimits.Day(f.DateFrom), dateTo = WorksheetLimits.Day(f.DateTo),
        acMonthFrom = Text(f.AcMonthFrom), acMonthTo = Text(f.AcMonthTo), acYearFrom = Text(f.AcYearFrom), acYearTo = Text(f.AcYearTo), debitFrom = Upper(f.DebitFrom), debitTo = Upper(f.DebitTo),
        max = WorksheetLimits.FetchRows,
    };

    /// <inheritdoc />
    public Task<List<PaymentTrackingDetailRow>> DetailAsync(IDbSession session, PaymentTrackingFilter f) =>
        session.Connection.ProcQueryAsync<PaymentTrackingDetailRow>(Procedure, Parameters(f), session.Transaction);

    /// <inheritdoc />
    public Task<List<PaymentTrackingSummaryRow>> SummaryAsync(IDbSession session, PaymentTrackingFilter f) =>
        session.Connection.ProcQueryAsync<PaymentTrackingSummaryRow>(Procedure, Parameters(f), session.Transaction);
}

/// <summary>Payment > Payment Tracking (legacy <c>FrmPaymentTracking</c>; rebuilt on the <c>PaymentTracking</c> procedure it called).</summary>
public class PaymentTrackingService(IDbSessions sessions, IPaymentTrackingRepository repository)
{
    /// <summary>The payments to hospitals or members, listed by claim or summed by one of the seven keys. The count and the total add up the lines listed.</summary>
    public async Task<PaymentTrackingResult> SearchAsync(PaymentTrackingFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        if (filter.Mode == "claim")
        {
            var capped = WorksheetLimits.Cap(await repository.DetailAsync(session, filter));
            return new PaymentTrackingResult(filter.Source!, "claim", capped.Rows, [], capped.Rows.Count, capped.Rows.Sum(r => r.Amount ?? 0), capped.Truncated);
        }

        var summary = WorksheetLimits.Cap(await repository.SummaryAsync(session, filter));
        return new PaymentTrackingResult(filter.Source!, filter.Mode!, [], summary.Rows, summary.Rows.Count, summary.Rows.Sum(r => r.Amount ?? 0), summary.Truncated);
    }
}
