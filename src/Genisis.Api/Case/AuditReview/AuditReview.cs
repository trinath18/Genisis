using System.Text.RegularExpressions;
using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Case.AuditReview;

/// <summary>The case's audit review record (CASAuditReview).</summary>
public record AuditRecord
{
    /// <summary>Invoice number.</summary>
    public string? InvoiceNo { get; set; }

    /// <summary>Invoice date.</summary>
    public DateTime? InvoiceDate { get; set; }

    /// <summary>Invoice amount.</summary>
    public decimal? InvoiceAmount { get; set; }

    /// <summary>Discount received.</summary>
    public decimal? DiscountReceived { get; set; }

    /// <summary>Discount type: A in main bordx, B in reverse bordx.</summary>
    public string? DiscountType { get; set; }

    /// <summary>Date the letter was sent.</summary>
    public DateTime? LetterSent { get; set; }

    /// <summary>Date the hospital replied.</summary>
    public DateTime? LetterReply { get; set; }

    /// <summary>Audit review status: C close, O open.</summary>
    public string? Status { get; set; }

    /// <summary>Date the review was opened.</summary>
    public DateTime? DateOpen { get; set; }

    /// <summary>Date sent to claims.</summary>
    public DateTime? DateToClaims { get; set; }

    /// <summary>The audit query.</summary>
    public string? Query { get; set; }

    /// <summary>The answer.</summary>
    public string? Answer { get; set; }

    /// <summary>Remarks.</summary>
    public string? Remarks { get; set; }

    /// <summary>Date of the hospital visit.</summary>
    public DateTime? VisitDate { get; set; }

    /// <summary>Time of the hospital visit (hh:mm).</summary>
    public string? VisitTime { get; set; }

    /// <summary>Action taken at the visit.</summary>
    public string? VisitAction { get; set; }

    /// <summary>Officer in charge.</summary>
    public string? Officer { get; set; }
}

/// <summary>A hospital payment of the case (HISPayment.Payment2HOS).</summary>
public record AuditPayment
{
    /// <summary>Payment voucher number.</summary>
    public string? PvNo { get; set; }

    /// <summary>Payment voucher date.</summary>
    public DateTime? PvDate { get; set; }

    /// <summary>Amount paid to the hospital.</summary>
    public decimal? AmountPaid { get; set; }
}

/// <summary>The case's member read straight from MBM (SearchMBMCas returns nothing for members without MBMOthers / MBMTwo rows).</summary>
public record AuditMember
{
    /// <summary>Membership number.</summary>
    public string MemberNo { get; set; } = "";

    /// <summary>Member name.</summary>
    public string? Name { get; set; }

    /// <summary>IC / passport number.</summary>
    public string? Ic { get; set; }

    /// <summary>Payor code.</summary>
    public string? Payor { get; set; }

    /// <summary>Policy number.</summary>
    public string? PolicyNo { get; set; }

    /// <summary>Renewal flag.</summary>
    public string? Renewal { get; set; }

    /// <summary>Take-over flag.</summary>
    public string? TakeOver { get; set; }

    /// <summary>Policy effective date.</summary>
    public DateTime? EffectiveDate { get; set; }

    /// <summary>Policy expiry date.</summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>Address line 1.</summary>
    public string? Address1 { get; set; }

    /// <summary>Address line 2.</summary>
    public string? Address2 { get; set; }

    /// <summary>Address line 3.</summary>
    public string? Address3 { get; set; }

    /// <summary>Post code.</summary>
    public string? PostCode { get; set; }

    /// <summary>Town.</summary>
    public string? Town { get; set; }

    /// <summary>State.</summary>
    public string? State { get; set; }

    /// <summary>Date of birth.</summary>
    public DateTime? Dob { get; set; }

    /// <summary>Available limit.</summary>
    public decimal? AvailableLimit { get; set; }

    /// <summary>Policy status.</summary>
    public string? Status { get; set; }

    /// <summary>Instalment.</summary>
    public string? Installment { get; set; }
}

/// <summary>What the audit record, payments and member reads return together.</summary>
/// <param name="Audit">The case's audit record, or null when it has none yet.</param>
/// <param name="Payments">The hospital payments.</param>
/// <param name="Member">The member from MBM.</param>
public record AuditExtras(AuditRecord? Audit, List<AuditPayment> Payments, AuditMember? Member);

/// <summary>The case as Case Adjustment loads it (case, member, diagnoses, repudiation) plus the audit record, the payments and the member read directly from MBM.</summary>
/// <param name="Case">Case Adjustment's view of the case.</param>
/// <param name="Audit">The case's audit record, or null.</param>
/// <param name="Payments">The hospital payments.</param>
/// <param name="Member">The member from MBM.</param>
public record AuditReviewView(CaseAdjustmentView Case, AuditRecord? Audit, List<AuditPayment> Payments, AuditMember? Member);

/// <summary>The values Update sends.</summary>
public class AuditReviewRequest
{
    /// <summary>Invoice number.</summary>
    public string? InvoiceNo { get; set; }

    /// <summary>Invoice date.</summary>
    public DateTime? InvoiceDate { get; set; }

    /// <summary>Invoice amount (blank is stored as 0).</summary>
    public decimal? InvoiceAmount { get; set; }

    /// <summary>Discount received (blank is stored as 0).</summary>
    public decimal? DiscountReceived { get; set; }

    /// <summary>Discount type (required): A or B.</summary>
    public string? DiscountType { get; set; }

    /// <summary>Date the letter was sent.</summary>
    public DateTime? LetterSent { get; set; }

    /// <summary>Date the hospital replied.</summary>
    public DateTime? LetterReply { get; set; }

    /// <summary>Audit review status: C or O.</summary>
    public string? Status { get; set; }

    /// <summary>Date the review was opened.</summary>
    public DateTime? DateOpen { get; set; }

    /// <summary>Date sent to claims.</summary>
    public DateTime? DateToClaims { get; set; }

    /// <summary>The audit query.</summary>
    public string? Query { get; set; }

    /// <summary>The answer.</summary>
    public string? Answer { get; set; }

    /// <summary>Remarks.</summary>
    public string? Remarks { get; set; }

    /// <summary>Date of the hospital visit.</summary>
    public DateTime? VisitDate { get; set; }

    /// <summary>Time of the hospital visit (hh:mm).</summary>
    public string? VisitTime { get; set; }

    /// <summary>Action taken at the visit.</summary>
    public string? VisitAction { get; set; }

    /// <summary>Officer in charge.</summary>
    public string? Officer { get; set; }
}

/// <summary>frmCaseAuditReview.cmdUpdate1_Click's checks plus the CASAuditReview column lengths.</summary>
public sealed partial class AuditReviewRequestValidator : RequestValidator<AuditReviewRequest>
{
    /// <summary>Valid audit review statuses.</summary>
    public static readonly string[] Statuses = ["C", "O"];

    /// <summary>Valid discount types.</summary>
    public static readonly string[] DiscountTypes = ["A", "B"];

    [GeneratedRegex(@"^([01]\d|2[0-3]):[0-5]\d$")]
    private static partial Regex Time();

    private static string Text(string? value) => (value ?? "").Trim();

    /// <summary>Declares the rules.</summary>
    public AuditReviewRequestValidator()
    {
        RuleFor(r => r.DiscountType).Must(v => Text(v).Length > 0).WithMessage("Please Enter Discount Type!");
        RuleFor(r => r.DiscountType).Must(v => DiscountTypes.Contains(Text(v))).WithMessage("Select a valid Discount Type.");
        RuleFor(r => r.Status).Must(v => Text(v).Length == 0 || Statuses.Contains(Text(v))).WithMessage("Select a valid Audit Review Status.");
        RuleFor(r => r.VisitTime).Must(v => Text(v).Length == 0 || Time().IsMatch(Text(v))).WithMessage("Invalid Time Format!");
        RuleFor(r => r).Must(r => r.InvoiceAmount is null or >= 0 && r.DiscountReceived is null or >= 0).WithMessage("Amounts cannot be negative.");
        RuleFor(r => r.InvoiceNo).Must(v => Text(v).Length <= 50).WithMessage("Invoice Number cannot be longer than 50 characters.");
        RuleFor(r => r).Must(r => Text(r.Query).Length <= 500 && Text(r.Answer).Length <= 500).WithMessage("Query and Answer cannot be longer than 500 characters.");
        RuleFor(r => r.Remarks).Must(v => Text(v).Length <= 200).WithMessage("Remarks cannot be longer than 200 characters.");
        RuleFor(r => r.VisitAction).Must(v => Text(v).Length <= 200).WithMessage("Action cannot be longer than 200 characters.");
        RuleFor(r => r.Officer).Must(v => Text(v).Length <= 50).WithMessage("Officer-in-Charge cannot be longer than 50 characters.");
    }
}

/// <summary>Reads and writes the audit review record.</summary>
public interface IAuditReviewRepository
{
    /// <summary>The audit record, payments and member of a case.</summary>
    Task<AuditExtras> GetExtrasAsync(IDbSession session, string cas);

    /// <summary>Inserts or updates the record and the cross-reference audit status; returns <see cref="StoredProcedures.NotFound"/> when the case does not exist.</summary>
    Task<int> SaveAsync(IDbSession session, string cas, AuditReviewRequest request, string userCode);
}

/// <inheritdoc />
public sealed class AuditReviewRepository : IAuditReviewRepository
{
    private static string Trim(string? value) => (value ?? "").Trim();

    /// <inheritdoc />
    public async Task<AuditExtras> GetExtrasAsync(IDbSession session, string cas)
    {
        using var grid = await session.Connection.ProcMultipleAsync("genisis.CaseAudit_Get", new { cas }, session.Transaction);
        var audit = await grid.ReadFirstOrDefaultAsync<AuditRecord>();
        var payments = (await grid.ReadAsync<AuditPayment>()).ToList();
        return new(audit, payments, await grid.ReadFirstOrDefaultAsync<AuditMember>());
    }

    /// <inheritdoc />
    public Task<int> SaveAsync(IDbSession session, string cas, AuditReviewRequest r, string userCode) =>
        session.Connection.ProcSingleAsync<int>("genisis.CaseAudit_Save", new
        {
            cas, invoiceNo = Trim(r.InvoiceNo), invoiceDate = r.InvoiceDate, invoiceAmount = r.InvoiceAmount,
            discountReceived = r.DiscountReceived, discountType = Trim(r.DiscountType), letterSent = r.LetterSent,
            letterReply = r.LetterReply, status = Trim(r.Status), dateOpen = r.DateOpen, dateToClaims = r.DateToClaims,
            query = Trim(r.Query), answer = Trim(r.Answer), remarks = Trim(r.Remarks),
            visitDate = r.VisitDate, visitTime = Trim(r.VisitTime), visitAction = Trim(r.VisitAction),
            officer = Trim(r.Officer), userCode,
        }, session.Transaction);
}

/// <summary>Case > Case Audit Review (legacy frmCaseAuditReview).</summary>
public class AuditReviewService(IDbSessions sessions, IAuditReviewRepository repository, CaseAdjustmentService adjustment, ILogger<AuditReviewService> log)
{
    private const string NoRecord = "No record found!";

    /// <summary>cmdShow_Click. Throws <see cref="NotFoundException"/> when the case does not exist.</summary>
    public async Task<AuditReviewView> LoadAsync(string cas, string userCode)
    {
        var view = await adjustment.LoadAsync(cas, userCode) ?? throw new NotFoundException(NoRecord);
        await using var session = await sessions.OpenAsync();
        var extras = await repository.GetExtrasAsync(session, cas.Trim());
        return new(view, extras.Audit, extras.Payments, extras.Member);
    }

    /// <summary>cmdUpdate1_Click. Throws <see cref="NotFoundException"/> when the case does not exist.</summary>
    public async Task SaveAsync(string cas, AuditReviewRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        await SaveAsync(session, cas, request, userCode);
        await session.CommitAsync();
        log.LogInformation("Audit review of case {CaseNumber} updated by {User}", cas, userCode);
    }

    /// <summary>Saves inside the caller's unit of work (nothing is committed here).</summary>
    public async Task SaveAsync(IDbSession session, string cas, AuditReviewRequest request, string userCode)
    {
        if (await repository.SaveAsync(session, cas.Trim(), request, userCode) != StoredProcedures.Ok) throw new NotFoundException(NoRecord);
    }
}
