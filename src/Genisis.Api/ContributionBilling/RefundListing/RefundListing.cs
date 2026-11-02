using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;

namespace Genisis.Api.ContributionBilling.RefundListing;

/// <summary>A member of the refund view as the database holds it (a column of <c>VIEW_MBM_Refund_Search</c> or <c>View_MBM_Supplementary_Refund</c>).</summary>
public record RefundRaw
{
    /// <summary>Member number.</summary>
    public string MemberNo { get; set; } = "";

    /// <summary>Cover id.</summary>
    public string? CoverId { get; set; }

    /// <summary>Policy number.</summary>
    public string? PolicyNo { get; set; }

    /// <summary>Member name.</summary>
    public string? MemberName { get; set; }

    /// <summary>C cancelled, P pending.</summary>
    public string? Status { get; set; }

    /// <summary>IC / BC / passport.</summary>
    public string? IcNo { get; set; }

    /// <summary>Bordx date.</summary>
    public DateTime? BordxDate { get; set; }

    /// <summary>Payor effective date.</summary>
    public DateTime? PayorEffDate { get; set; }

    /// <summary>Payor expiry date.</summary>
    public DateTime? PayorExpDate { get; set; }

    /// <summary>Days insured.</summary>
    public int? DaysInsured { get; set; }

    /// <summary>Endorsement (cancellation) effective date.</summary>
    public DateTime? EndtEffDate { get; set; }

    /// <summary>Endorsement bordx date.</summary>
    public DateTime? EndtBordxDate { get; set; }

    /// <summary>Days utilised.</summary>
    public int? DaysUtilised { get; set; }

    /// <summary>Months utilised.</summary>
    public int? MonthsUtilised { get; set; }

    /// <summary>MCO fees charged.</summary>
    public decimal? McoFee { get; set; }

    /// <summary>MCO fees utilised.</summary>
    public decimal? McoUtilised { get; set; }

    /// <summary>MCO fees refund.</summary>
    public decimal? McoRefund { get; set; }

    /// <summary>Invoice number.</summary>
    public string? InvoiceNo { get; set; }

    /// <summary>Invoice date.</summary>
    public DateTime? InvoiceDate { get; set; }

    /// <summary>Number of cases the member had.</summary>
    public int? TotalCases { get; set; }

    /// <summary>Credit note number.</summary>
    public string? CreditNote { get; set; }

    /// <summary>Credit note date.</summary>
    public DateTime? CreditDate { get; set; }

    /// <summary>Insurance code (F family, H household for the principals).</summary>
    public string? InsCode { get; set; }
}

/// <summary>A line of the list: a principal, or one of the supplementary members that follow it.</summary>
public record RefundRow(
    string Kind, string MemberNo, string? CoverId, string? PolicyNo, string? MemberName, string? Status, string? IcNo, DateTime? BordxDate, DateTime? PayorEffDate, DateTime? PayorExpDate, int? DaysInsured,
    DateTime? EndtEffDate, DateTime? EndtBordxDate, int? DaysUtilised, int MonthsUtilised, decimal McoFee, decimal McoUtilised, decimal? McoRefund, decimal McoRefundMin, string? InvoiceNo, DateTime? InvoiceDate,
    string? Remarks, string? CreditNote, DateTime? CreditDate);

/// <summary>The lines, in the desktop's order, and the totals.</summary>
public record RefundResult(List<RefundRow> Rows, int Principals, int Supplementary, decimal TotalRefund, bool Truncated);

/// <summary>The criteria of the Refund Listing search.</summary>
public class RefundFilter
{
    /// <summary>"Date Selection": effective (Endt. Eff Date) or bordx (Endt. Bordx Date).</summary>
    public string? DateBy { get; set; }

    /// <summary>Payor code (or "CODE - Name").</summary>
    public string? Payor { get; set; }

    /// <summary>Date from.</summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>Date to.</summary>
    public DateTime? DateTo { get; set; }

    /// <summary>"Print Status": N (no credit note yet) or Y.</summary>
    public string? Printed { get; set; } = "N";
}

/// <summary>cmdSearch_Click's checks: the date selection first, then the payor.</summary>
public sealed class RefundFilterValidator : RequestValidator<RefundFilter>
{
    /// <summary>Declares the rules.</summary>
    public RefundFilterValidator()
    {
        RuleFor(f => f.DateBy).Must(d => !string.IsNullOrWhiteSpace(d)).WithMessage("Please Select Date Selection!");
        RuleFor(f => f.DateBy).Must(d => d is "effective" or "bordx").WithMessage("Select Endt. Eff Date or Endt. Bordx Date.");
        RuleFor(f => f.Payor).Must(p => !string.IsNullOrWhiteSpace(RefundRules.PayorCode(p))).WithMessage("Please Select Payor!");
        RuleFor(f => f.Printed).Must(p => string.IsNullOrWhiteSpace(p) || p.Trim().ToUpperInvariant()[..1] is "N" or "Y").WithMessage("Print Status is N - No or Y - Yes.");
    }
}

/// <summary>The listing's arithmetic, as the desktop did it.</summary>
public static class RefundRules
{
    /// <summary>The payor code of "CODE - Name" (the desktop took the first two letters).</summary>
    public static string PayorCode(string? text) => (text ?? "").Trim().Split(" - ")[0].Trim().ToUpperInvariant();

    /// <summary>The MCO fee refund (Min.): from a utilised fee at or below the minimum (50 for principals of the family and household codes, 25 for the others and for the supplementary members) the fee less
    /// the minimum, above it the utilised fee itself, and 0 without one (the desktop's code, kept as written).</summary>
    public static decimal RefundMin(decimal? fee, decimal? utilised, bool strongerMinimum)
    {
        if (utilised is null) return 0m;
        var minimum = strongerMinimum ? 50m : 25m;
        return utilised <= minimum ? (fee ?? 0m) - minimum : utilised.Value;
    }

    /// <summary>The days utilised: for a payor whose cancelled members are listed (MBMCancelStatus Y) below zero shows 0; for the others the value as it is.</summary>
    public static int? DaysUtilised(int? value, bool cancelStatus) => cancelStatus ? Math.Max(value ?? 0, 0) : value;

    /// <summary>Whether a principal is a family or household member, whose supplementary members follow it.</summary>
    public static bool IsFamily(string? insCode) => insCode?.Trim() is "F" or "H";
}

/// <summary>Reads the refund views.</summary>
public interface IRefundListingRepository
{
    /// <summary>The payor and whether its cancelled members are listed (null when there is no such payor).</summary>
    Task<(string Code, string? Name, bool CancelStatus)?> PayorAsync(IDbSession session, string payor);

    /// <summary>The principals (at most <see cref="WorksheetLimits.FetchRows"/>).</summary>
    Task<List<RefundRaw>> PrincipalsAsync(IDbSession session, string payor, RefundFilter filter);

    /// <summary>The supplementary members of the payor in the same date range.</summary>
    Task<List<RefundRaw>> SupplementaryAsync(IDbSession session, string payor, RefundFilter filter);
}

/// <inheritdoc />
public sealed class RefundListingRepository : IRefundListingRepository
{
    private sealed record PayorRow(string Code, string? Name, bool CancelStatus);

    /// <inheritdoc />
    public async Task<(string Code, string? Name, bool CancelStatus)?> PayorAsync(IDbSession session, string payor) =>
        await session.Connection.ProcFirstOrDefaultAsync<PayorRow>("genisis.RefundListing_PayorStatus", new { payor }, session.Transaction) is { } row ? (row.Code, row.Name, row.CancelStatus) : null;

    /// <inheritdoc />
    public Task<List<RefundRaw>> PrincipalsAsync(IDbSession session, string payor, RefundFilter f) =>
        session.Connection.ProcQueryAsync<RefundRaw>("genisis.RefundListing_Principals", new
        {
            payor, dateBy = f.DateBy, dateFrom = WorksheetLimits.Day(f.DateFrom), dateTo = WorksheetLimits.Day(f.DateTo), printed = (f.Printed ?? "N").Trim().ToUpperInvariant()[..1], max = WorksheetLimits.FetchRows,
        }, session.Transaction);

    /// <inheritdoc />
    public Task<List<RefundRaw>> SupplementaryAsync(IDbSession session, string payor, RefundFilter f) =>
        session.Connection.ProcQueryAsync<RefundRaw>("genisis.RefundListing_Supplementary", new
        {
            payor, dateBy = f.DateBy, dateFrom = WorksheetLimits.Day(f.DateFrom), dateTo = WorksheetLimits.Day(f.DateTo), max = WorksheetLimits.FetchRows,
        }, session.Transaction);
}

/// <summary>Contribution Billing > Refund Listing (legacy <c>frmMCORefundListing2</c>, "MCO Refund Listing").</summary>
public class RefundListingService(IDbSessions sessions, IRefundListingRepository repository)
{
    private static RefundRow ToRow(RefundRaw r, string kind, bool cancelStatus, bool strongerMinimum) => new(
        kind, r.MemberNo, r.CoverId, r.PolicyNo, r.MemberName, r.Status, r.IcNo, r.BordxDate, r.PayorEffDate, r.PayorExpDate, r.DaysInsured, r.EndtEffDate, r.EndtBordxDate,
        RefundRules.DaysUtilised(r.DaysUtilised, cancelStatus), r.MonthsUtilised ?? 0, r.McoFee ?? 0m, r.McoUtilised ?? 0m, r.McoRefund, RefundRules.RefundMin(r.McoFee, r.McoUtilised, strongerMinimum), r.InvoiceNo,
        r.InvoiceDate, r.TotalCases > 0 ? $"This member had {r.TotalCases} Case(s)" : null, r.CreditNote, r.CreditDate);

    /// <summary>
    /// cmdSearch_Click: the principals of the payor whose endorsement date is in the range and whose credit note has (or has not) been printed, each followed, when it is a family or household member, by its
    /// supplementary members. An unknown payor is a <see cref="NotFoundException"/>. The total refund adds the principals' MCO fees refund (the supplementary members are counted, not added).
    /// </summary>
    public async Task<RefundResult> SearchAsync(RefundFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        var code = RefundRules.PayorCode(filter.Payor);
        var payor = await repository.PayorAsync(session, code) ?? throw new NotFoundException("Payor not found");
        var capped = WorksheetLimits.Cap(await repository.PrincipalsAsync(session, payor.Code, filter));
        var supplementary = capped.Rows.Any(p => RefundRules.IsFamily(p.InsCode)) ? (await repository.SupplementaryAsync(session, payor.Code, filter)).ToLookup(s => s.MemberNo) : null;
        var rows = new List<RefundRow>();
        var (principals, supp) = (0, 0);
        foreach (var principal in capped.Rows)
        {
            var family = RefundRules.IsFamily(principal.InsCode);
            rows.Add(ToRow(principal, "principal", payor.CancelStatus, family));
            principals++;
            if (!family || supplementary is null) continue;
            foreach (var s in supplementary[principal.MemberNo])
            {
                rows.Add(ToRow(s, "supplementary", payor.CancelStatus, false));
                supp++;
            }
        }

        return new RefundResult(rows, principals, supp, capped.Rows.Sum(p => p.McoRefund ?? 0m), capped.Truncated);
    }
}
