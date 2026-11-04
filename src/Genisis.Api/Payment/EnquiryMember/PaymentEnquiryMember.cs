using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Finance;
using Genisis.Api.Payment.EnquiryHospital;

namespace Genisis.Api.Payment.EnquiryMember;

/// <summary>The drop-downs of the enquiry: the payors and the health types.</summary>
public record PaymentMemberLookups(List<PaymentCode> Payors, List<PaymentHealthType> HealthTypes);

/// <summary>The boxes of the Payment To Member enquiry. A blank box is no criterion; the "empty" boxes list the rows with no value.</summary>
public class PaymentEnquiryMemberFilter
{
    /// <summary>Case No (the one claim scanned, or typed).</summary>
    public string? CaseNo { get; set; }

    /// <summary>Claim version.</summary>
    public string? Version { get; set; }

    /// <summary>WS Claimability (CLMWSStatus).</summary>
    public string? WsStatus { get; set; }

    /// <summary>Health Type code.</summary>
    public string? HealthType { get; set; }

    /// <summary>Member number.</summary>
    public string? MbmNo { get; set; }

    /// <summary>Member name (contains).</summary>
    public string? MbmName { get; set; }

    /// <summary>Payor code.</summary>
    public string? Payor { get; set; }

    /// <summary>Pymt Status: 0 all, 1 paid, 2 unpaid, 3 part paid, 4 paid and part paid, 5 unpaid and part paid.</summary>
    public int Payment { get; set; }

    /// <summary>PV No from.</summary>
    public string? PvNoFrom { get; set; }

    /// <summary>PV No to.</summary>
    public string? PvNoTo { get; set; }

    /// <summary>Only rows with no PV number.</summary>
    public bool BlankPvNo { get; set; }

    /// <summary>PV Date from.</summary>
    public DateTime? PvDateFrom { get; set; }

    /// <summary>PV Date to.</summary>
    public DateTime? PvDateTo { get; set; }

    /// <summary>Only rows with no PV date.</summary>
    public bool BlankPvDate { get; set; }

    /// <summary>CR No (debit / credit note) from.</summary>
    public string? CrNoFrom { get; set; }

    /// <summary>CR No to.</summary>
    public string? CrNoTo { get; set; }

    /// <summary>Only rows with no CR number.</summary>
    public bool BlankCrNo { get; set; }

    /// <summary>A/C period month from.</summary>
    public int? MonthFrom { get; set; }

    /// <summary>A/C period month to.</summary>
    public int? MonthTo { get; set; }

    /// <summary>Only rows with no A/C period month.</summary>
    public bool BlankMonth { get; set; }

    /// <summary>A/C period year from.</summary>
    public int? YearFrom { get; set; }

    /// <summary>A/C period year to.</summary>
    public int? YearTo { get; set; }

    /// <summary>Only rows with no A/C period year.</summary>
    public bool BlankYear { get; set; }

    /// <summary>SUN PV No from.</summary>
    public string? SunNoFrom { get; set; }

    /// <summary>SUN PV No to.</summary>
    public string? SunNoTo { get; set; }

    /// <summary>Only rows with no SUN PV number.</summary>
    public bool BlankSunNo { get; set; }

    /// <summary>SUN PV Date from.</summary>
    public DateTime? SunDateFrom { get; set; }

    /// <summary>SUN PV Date to.</summary>
    public DateTime? SunDateTo { get; set; }

    /// <summary>Only rows with no SUN PV date.</summary>
    public bool BlankSunDate { get; set; }
}

/// <summary>cmdSearch_Click's checks: the codes of the drop-downs, the A/C period lists the desktop offered and the box lengths.</summary>
public sealed class PaymentEnquiryMemberFilterValidator : RequestValidator<PaymentEnquiryMemberFilter>
{
    private static readonly string[] WsStatuses = ["AA", "RR", "CC", "SA", "PP", "NN", "NA", "NR", "PA", "PR", "AR", "RA", "BR"];

    /// <summary>Declares the rules.</summary>
    public PaymentEnquiryMemberFilterValidator()
    {
        RuleFor(f => f.Payment).InclusiveBetween(0, 5).WithMessage("Select a valid Pymt Status.");
        RuleFor(f => f.WsStatus).Must(v => FinanceLimits.IsBlankOr(v, WsStatuses)).WithMessage("Select a valid WS Claimability.");
        RuleFor(f => f).Must(f => new[] { f.MonthFrom, f.MonthTo }.All(m => m is null or >= 1 and <= 12)).WithMessage("A/C Period Mth must be 1 to 12.");
        RuleFor(f => f).Must(f => new[] { f.YearFrom, f.YearTo }.All(y => y is null or >= 1999 and <= 3000)).WithMessage("A/C Period Yr must be 1999 to 3000.");
        RuleFor(f => f).Must(f => new[] { f.CaseNo, f.MbmNo }.All(v => (v?.Length ?? 0) <= 30) && (f.Version?.Length ?? 0) <= 4).WithMessage("A number is too long.");
        RuleFor(f => f.MbmName).MaximumLength(100).WithMessage("Member Name is too long.");
        RuleFor(f => f).Must(f => new[] { f.PvNoFrom, f.PvNoTo, f.CrNoFrom, f.CrNoTo, f.SunNoFrom, f.SunNoTo }.All(v => (v?.Length ?? 0) <= 40)).WithMessage("A number is too long.");
        RuleFor(f => f).Must(f => (f.Payor?.Length ?? 0) <= 10 && (f.HealthType?.Length ?? 0) <= 2).WithMessage("A box is too long.");
    }
}

/// <summary>One payment to a member (or a claim with none yet), or one that was reissued.</summary>
public record PaymentEnquiryMemberRow
{
    /// <summary>The claim's debit / credit note number.</summary>
    public string? CrNo { get; set; }

    /// <summary>Case number.</summary>
    public string? CaseNo { get; set; }

    /// <summary>Claim version.</summary>
    public string? ClaimVersion { get; set; }

    /// <summary>What Medix pays the member for the claim.</summary>
    public decimal? Payable { get; set; }

    /// <summary>The member's name.</summary>
    public string? MemberName { get; set; }

    /// <summary>Who the claim is paid to.</summary>
    public string? PayTo { get; set; }

    /// <summary>Member number.</summary>
    public string? MbmNo { get; set; }

    /// <summary>PVMBMIndex.</summary>
    public int? PvIndex { get; set; }

    /// <summary>The amount of this payment.</summary>
    public decimal? PaidAmt { get; set; }

    /// <summary>Audit review status.</summary>
    public string? ArStatus { get; set; }

    /// <summary>PV number.</summary>
    public string? PvNo { get; set; }

    /// <summary>PV date.</summary>
    public DateTime? PvDate { get; set; }

    /// <summary>PV status (R reissued).</summary>
    public string? PvStatus { get; set; }

    /// <summary>F full, P partial, none unpaid.</summary>
    public string? PayStatus { get; set; }

    /// <summary>Cheque number.</summary>
    public string? ChequeNo { get; set; }

    /// <summary>Cheque date.</summary>
    public DateTime? ChequeDate { get; set; }

    /// <summary>Cheque amount.</summary>
    public decimal? ChequeAmt { get; set; }

    /// <summary>The payee of the PV.</summary>
    public string? PvPayTo { get; set; }

    /// <summary>The payee's bank.</summary>
    public string? PayeeBank { get; set; }

    /// <summary>The payee's account number.</summary>
    public string? PayeeAcNo { get; set; }

    /// <summary>Remark.</summary>
    public string? Remark { get; set; }

    /// <summary>The claim's WS status (claimability).</summary>
    public string? WsStatus { get; set; }

    /// <summary>SUN PV number.</summary>
    public string? SunPvNo { get; set; }

    /// <summary>SUN PV date.</summary>
    public DateTime? SunPvDate { get; set; }

    /// <summary>Bank charges.</summary>
    public decimal? BankCharges { get; set; }

    /// <summary>A/C period month.</summary>
    public string? AccMonth { get; set; }

    /// <summary>A/C period year.</summary>
    public string? AccYear { get; set; }

    /// <summary>User who last changed the payment.</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>For a part-paid claim, the sum of every payment of it.</summary>
    public decimal? PaidTotal { get; set; }

    /// <summary>What is still to pay: the amount less the payments for a part-paid claim, the whole amount for an unpaid one.</summary>
    public decimal? Os { get; set; }
}

/// <summary>One receipt of a claim (from MNRB, the payor or the member).</summary>
public record PaymentEnquiryMemberReceipt
{
    /// <summary>Case number.</summary>
    public string? CaseNo { get; set; }

    /// <summary>Claim version.</summary>
    public string? ClaimVersion { get; set; }

    /// <summary>Member number.</summary>
    public string? MbmNo { get; set; }

    /// <summary>MNRB, PAY or MBM.</summary>
    public string? Kind { get; set; }

    /// <summary>What Medix pays the member for the claim.</summary>
    public decimal? Payable { get; set; }

    /// <summary>The receipt's amount.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Receipt number.</summary>
    public string? ReceiptNo { get; set; }

    /// <summary>Receipt date.</summary>
    public DateTime? ReceiptDate { get; set; }

    /// <summary>A/C period month.</summary>
    public string? AccMonth { get; set; }

    /// <summary>A/C period year.</summary>
    public string? AccYear { get; set; }

    /// <summary>Remarks.</summary>
    public string? Remarks { get; set; }
}

/// <summary>The three lists of the screen, the totals of the payments and whether more than 5,000 payments matched.</summary>
public record PaymentEnquiryMemberResult(List<PaymentEnquiryMemberRow> Payments, List<PaymentEnquiryMemberRow> Reissues, List<PaymentEnquiryMemberReceipt> Receipts, decimal TotalPayable, decimal TotalPaid, bool Truncated);

/// <summary>Reads the Payment To Member enquiry.</summary>
public interface IPaymentEnquiryMemberRepository
{
    /// <summary>The payors and the health types.</summary>
    Task<PaymentMemberLookups> LookupsAsync(IDbSession session);

    /// <summary>The payments that match (the reissued ones when <paramref name="reissue"/>), at most <paramref name="max"/>.</summary>
    Task<List<PaymentEnquiryMemberRow>> SearchAsync(IDbSession session, PaymentEnquiryMemberFilter filter, bool reissue, int max);

    /// <summary>The receipts of the claims.</summary>
    Task<List<PaymentEnquiryMemberReceipt>> ReceiptsAsync(IDbSession session, IReadOnlyCollection<(string CaseNo, string Version)> claims);
}

/// <inheritdoc />
public sealed class PaymentEnquiryMemberRepository : IPaymentEnquiryMemberRepository
{
    /// <inheritdoc />
    public async Task<PaymentMemberLookups> LookupsAsync(IDbSession session)
    {
        using var grid = await session.Connection.ProcMultipleAsync("genisis.PaymentEnqMbm_Lookups", null, session.Transaction);
        return new PaymentMemberLookups((await grid.ReadAsync<PaymentCode>()).ToList(), (await grid.ReadAsync<PaymentHealthType>()).ToList());
    }

    /// <inheritdoc />
    public Task<List<PaymentEnquiryMemberRow>> SearchAsync(IDbSession session, PaymentEnquiryMemberFilter f, bool reissue, int max) =>
        session.Connection.ProcQueryAsync<PaymentEnquiryMemberRow>("genisis.PaymentEnqMbm_Search", new
        {
            caseNo = FinanceLimits.Text(f.CaseNo), version = FinanceLimits.Text(f.Version), wsStatus = FinanceLimits.Text(f.WsStatus), healthType = FinanceLimits.Text(f.HealthType), mbmNo = FinanceLimits.Text(f.MbmNo),
            mbmName = FinanceLimits.Text(f.MbmName), payor = FinanceLimits.Text(f.Payor), payment = f.Payment, pvNoFrom = FinanceLimits.Text(f.PvNoFrom), pvNoTo = FinanceLimits.Text(f.PvNoTo), blankPvNo = f.BlankPvNo,
            pvDateFrom = FinanceLimits.Day(f.PvDateFrom), pvDateTo = FinanceLimits.Day(f.PvDateTo), blankPvDate = f.BlankPvDate, crNoFrom = FinanceLimits.Text(f.CrNoFrom), crNoTo = FinanceLimits.Text(f.CrNoTo),
            blankCrNo = f.BlankCrNo, monthFrom = f.MonthFrom, monthTo = f.MonthTo, blankMonth = f.BlankMonth, yearFrom = f.YearFrom, yearTo = f.YearTo, blankYear = f.BlankYear,
            sunNoFrom = FinanceLimits.Text(f.SunNoFrom), sunNoTo = FinanceLimits.Text(f.SunNoTo), blankSunNo = f.BlankSunNo, sunDateFrom = FinanceLimits.Day(f.SunDateFrom), sunDateTo = FinanceLimits.Day(f.SunDateTo),
            blankSunDate = f.BlankSunDate, reissue, max,
        }, session.Transaction);

    /// <inheritdoc />
    public Task<List<PaymentEnquiryMemberReceipt>> ReceiptsAsync(IDbSession session, IReadOnlyCollection<(string CaseNo, string Version)> claims) =>
        claims.Count == 0
            ? Task.FromResult(new List<PaymentEnquiryMemberReceipt>())
            : session.Connection.ProcQueryAsync<PaymentEnquiryMemberReceipt>("genisis.PaymentEnqMbm_Receipts",
                new { claims = new System.Xml.Linq.XElement("l", claims.Select(c => new System.Xml.Linq.XElement("i", $"{c.CaseNo}|{c.Version}"))).ToString(System.Xml.Linq.SaveOptions.DisableFormatting) }, session.Transaction);
}

/// <summary>Payment > Enquiry > Payment To Member (legacy <c>FrmEnquiryPayMember</c>).</summary>
public class PaymentEnquiryMemberService(IDbSessions sessions, IPaymentEnquiryMemberRepository repository)
{
    /// <summary>ComboListing: the drop-downs.</summary>
    public async Task<PaymentMemberLookups> LookupsAsync()
    {
        await using var session = await sessions.OpenAsync();
        return await repository.LookupsAsync(session);
    }

    /// <summary>
    /// cmdSearch_Click: the payments that match, the payments that were reissued and the receipts of the claims listed, all at once (the desktop asked for the receipts of each row in turn). Nothing found among the
    /// payments is a <see cref="NotFoundException"/> ("No record found!").
    /// </summary>
    public async Task<PaymentEnquiryMemberResult> SearchAsync(PaymentEnquiryMemberFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        var found = await repository.SearchAsync(session, filter, false, FinanceLimits.FetchRows);
        if (found.Count == 0) throw new NotFoundException("No record found!");
        var payments = found.Take(FinanceLimits.MaxRows).ToList();
        var reissues = await repository.SearchAsync(session, filter, true, FinanceLimits.MaxRows);
        var claims = payments.Where(p => !string.IsNullOrEmpty(p.CaseNo)).Select(p => (p.CaseNo!, p.ClaimVersion ?? "")).Distinct().ToList();
        var receipts = await repository.ReceiptsAsync(session, claims);
        return new PaymentEnquiryMemberResult(payments, reissues, receipts, payments.Sum(p => p.Payable ?? 0m), payments.Sum(p => p.PaidAmt ?? 0m), found.Count > FinanceLimits.MaxRows);
    }
}
