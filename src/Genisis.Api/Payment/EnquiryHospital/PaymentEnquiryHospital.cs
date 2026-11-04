using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Finance;

namespace Genisis.Api.Payment.EnquiryHospital;

/// <summary>A code with its name or description.</summary>
public record PaymentCode(string Code, string? Name);

/// <summary>A health type (HLT).</summary>
public record PaymentHealthType(string Code, string? Description);

/// <summary>The drop-downs of the enquiry.</summary>
public record PaymentEnquiryLookups(List<PaymentCode> Hospitals, List<PaymentCode> Payors, List<PaymentHealthType> HealthTypes);

/// <summary>The boxes of the Payment To Hospital enquiry. A blank box is no criterion; the "empty" boxes list the rows with no value.</summary>
public class PaymentEnquiryHospitalFilter
{
    /// <summary>The hospital the invoice is paid to (its code).</summary>
    public string? Hospital { get; set; }

    /// <summary>Payor code.</summary>
    public string? Payor { get; set; }

    /// <summary>WS Claimability (CLMWSStatus: AA, RR, CC, SA, PP, NN, NA, NR, PA, PR, AR, RA or BR).</summary>
    public string? WsStatus { get; set; }

    /// <summary>Health Type code.</summary>
    public string? HealthType { get; set; }

    /// <summary>Pymt Status: 0 all, 1 paid, 2 unpaid, 3 part paid, 4 paid and part paid, 5 unpaid and part paid.</summary>
    public int Payment { get; set; }

    /// <summary>Use Scan: the one claim number scanned (exact).</summary>
    public string? ScanCase { get; set; }

    /// <summary>Clm No from.</summary>
    public string? CaseFrom { get; set; }

    /// <summary>Clm No to.</summary>
    public string? CaseTo { get; set; }

    /// <summary>Inv No.</summary>
    public string? InvNo { get; set; }

    /// <summary>Only rows with no invoice number.</summary>
    public bool BlankInvNo { get; set; }

    /// <summary>Inv Date from.</summary>
    public DateTime? InvDateFrom { get; set; }

    /// <summary>Inv Date to.</summary>
    public DateTime? InvDateTo { get; set; }

    /// <summary>Only rows with no invoice date.</summary>
    public bool BlankInvDate { get; set; }

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

/// <summary>cmdSearch_Click's checks: the codes of the drop-downs and the box lengths.</summary>
public sealed class PaymentEnquiryHospitalFilterValidator : RequestValidator<PaymentEnquiryHospitalFilter>
{
    private static readonly string[] WsStatuses = ["AA", "RR", "CC", "SA", "PP", "NN", "NA", "NR", "PA", "PR", "AR", "RA", "BR"];

    /// <summary>Declares the rules.</summary>
    public PaymentEnquiryHospitalFilterValidator()
    {
        RuleFor(f => f.Payment).InclusiveBetween(0, 5).WithMessage("Select a valid Pymt Status.");
        RuleFor(f => f.WsStatus).Must(v => FinanceLimits.IsBlankOr(v, WsStatuses)).WithMessage("Select a valid WS Claimability.");
        RuleFor(f => f).Must(f => new[] { f.ScanCase, f.CaseFrom, f.CaseTo }.All(v => (v?.Length ?? 0) <= 30)).WithMessage("Clm No is too long.");
        RuleFor(f => f).Must(f => new[] { f.InvNo, f.Hospital }.All(v => (v?.Length ?? 0) <= 100)).WithMessage("A box is too long.");
        RuleFor(f => f).Must(f => new[] { f.PvNoFrom, f.PvNoTo, f.SunNoFrom, f.SunNoTo }.All(v => (v?.Length ?? 0) <= 40)).WithMessage("A PV number is too long.");
        RuleFor(f => f).Must(f => (f.Payor?.Length ?? 0) <= 10 && (f.HealthType?.Length ?? 0) <= 2).WithMessage("A box is too long.");
    }
}

/// <summary>One payment of a claim payable to a hospital.</summary>
public record PaymentEnquiryHospitalRow
{
    /// <summary>F full, P partial, none unpaid.</summary>
    public string? PayStatus { get; set; }

    /// <summary>Hospital name.</summary>
    public string? HospitalName { get; set; }

    /// <summary>Invoice number.</summary>
    public string? InvNo { get; set; }

    /// <summary>Invoice date.</summary>
    public DateTime? InvDate { get; set; }

    /// <summary>The amount payable to the hospital (Pay2HOS).</summary>
    public decimal? Payable { get; set; }

    /// <summary>The amount of this payment.</summary>
    public decimal? PaidAmt { get; set; }

    /// <summary>The patient's name (the covered person's when it is not the principal).</summary>
    public string? PatientName { get; set; }

    /// <summary>Case number.</summary>
    public string? CaseNo { get; set; }

    /// <summary>Claim version.</summary>
    public string? ClaimVersion { get; set; }

    /// <summary>Bordx number.</summary>
    public string? BordxNo { get; set; }

    /// <summary>Audit review status.</summary>
    public string? ArStatus { get; set; }

    /// <summary>Date of admission.</summary>
    public DateTime? Doa { get; set; }

    /// <summary>Date of discharge.</summary>
    public DateTime? Dod { get; set; }

    /// <summary>Date of payment.</summary>
    public DateTime? PaymentDate { get; set; }

    /// <summary>Who entered the invoice.</summary>
    public string? EntryBy { get; set; }

    /// <summary>PV HOS index.</summary>
    public int? PvIndex { get; set; }

    /// <summary>PV number.</summary>
    public string? PvNo { get; set; }

    /// <summary>Cheque number.</summary>
    public string? ChequeNo { get; set; }

    /// <summary>Cheque date.</summary>
    public DateTime? ChequeDate { get; set; }

    /// <summary>A/C period month.</summary>
    public int? AccMonth { get; set; }

    /// <summary>A/C period year.</summary>
    public int? AccYear { get; set; }

    /// <summary>Bank charges.</summary>
    public decimal? BankCharges { get; set; }

    /// <summary>The claim's WS status (claimability).</summary>
    public string? WsStatus { get; set; }

    /// <summary>SUN PV number.</summary>
    public string? SunPvNo { get; set; }

    /// <summary>SUN PV date.</summary>
    public DateTime? SunPvDate { get; set; }

    /// <summary>For a part-paid claim, the sum of every payment of it.</summary>
    public decimal? PaidTotal { get; set; }

    /// <summary>What is still to pay: the amount less the payments for a part-paid claim, the whole amount for an unpaid one.</summary>
    public decimal? Os { get; set; }

    /// <summary>The receipt numbers of the claim (MNRB or payor, then the member's).</summary>
    public string? ReceiptNo { get; set; }

    /// <summary>The receipt amount of the claim.</summary>
    public decimal? ReceiptAmt { get; set; }
}

/// <summary>The rows found, the sum of what is payable and whether more than 5,000 matched.</summary>
public record PaymentEnquiryHospitalResult(List<PaymentEnquiryHospitalRow> Rows, decimal TotalPayable, bool Truncated);

/// <summary>Reads the Payment To Hospital enquiry.</summary>
public interface IPaymentEnquiryHospitalRepository
{
    /// <summary>The hospitals, the payors and the health types.</summary>
    Task<PaymentEnquiryLookups> LookupsAsync(IDbSession session);

    /// <summary>The rows that match, at most <paramref name="max"/>.</summary>
    Task<List<PaymentEnquiryHospitalRow>> SearchAsync(IDbSession session, PaymentEnquiryHospitalFilter filter, int max);
}

/// <inheritdoc />
public sealed class PaymentEnquiryHospitalRepository : IPaymentEnquiryHospitalRepository
{
    /// <inheritdoc />
    public async Task<PaymentEnquiryLookups> LookupsAsync(IDbSession session)
    {
        using var grid = await session.Connection.ProcMultipleAsync("genisis.PaymentEnqHos_Lookups", null, session.Transaction);
        return new PaymentEnquiryLookups((await grid.ReadAsync<PaymentCode>()).ToList(), (await grid.ReadAsync<PaymentCode>()).ToList(), (await grid.ReadAsync<PaymentHealthType>()).ToList());
    }

    /// <inheritdoc />
    public Task<List<PaymentEnquiryHospitalRow>> SearchAsync(IDbSession session, PaymentEnquiryHospitalFilter f, int max) =>
        session.Connection.ProcQueryAsync<PaymentEnquiryHospitalRow>("genisis.PaymentEnqHos_Search", new
        {
            hospital = FinanceLimits.Text(f.Hospital), payor = FinanceLimits.Text(f.Payor), wsStatus = FinanceLimits.Text(f.WsStatus), healthType = FinanceLimits.Text(f.HealthType), payment = f.Payment,
            scanCase = FinanceLimits.Text(f.ScanCase), caseFrom = FinanceLimits.Text(f.CaseFrom), caseTo = FinanceLimits.Text(f.CaseTo), invNo = FinanceLimits.Text(f.InvNo), blankInvNo = f.BlankInvNo,
            invDateFrom = FinanceLimits.Day(f.InvDateFrom), invDateTo = FinanceLimits.Day(f.InvDateTo), blankInvDate = f.BlankInvDate, pvNoFrom = FinanceLimits.Text(f.PvNoFrom), pvNoTo = FinanceLimits.Text(f.PvNoTo),
            blankPvNo = f.BlankPvNo, pvDateFrom = FinanceLimits.Day(f.PvDateFrom), pvDateTo = FinanceLimits.Day(f.PvDateTo), blankPvDate = f.BlankPvDate, sunNoFrom = FinanceLimits.Text(f.SunNoFrom),
            sunNoTo = FinanceLimits.Text(f.SunNoTo), blankSunNo = f.BlankSunNo, sunDateFrom = FinanceLimits.Day(f.SunDateFrom), sunDateTo = FinanceLimits.Day(f.SunDateTo), blankSunDate = f.BlankSunDate, max,
        }, session.Transaction);
}

/// <summary>Payment > Enquiry > Payment To Hospital (legacy <c>FrmEnquiryPaymentHospital</c>).</summary>
public class PaymentEnquiryHospitalService(IDbSessions sessions, IPaymentEnquiryHospitalRepository repository)
{
    /// <summary>ComboListing: the drop-downs.</summary>
    public async Task<PaymentEnquiryLookups> LookupsAsync()
    {
        await using var session = await sessions.OpenAsync();
        return await repository.LookupsAsync(session);
    }

    /// <summary>cmdSearch_Click: the rows that match and the sum payable; nothing found is a <see cref="NotFoundException"/> ("No record found!").</summary>
    public async Task<PaymentEnquiryHospitalResult> SearchAsync(PaymentEnquiryHospitalFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        var rows = await repository.SearchAsync(session, filter, FinanceLimits.FetchRows);
        if (rows.Count == 0) throw new NotFoundException("No record found!");
        var shown = rows.Take(FinanceLimits.MaxRows).ToList();
        return new PaymentEnquiryHospitalResult(shown, shown.Sum(r => r.Payable ?? 0m), rows.Count > FinanceLimits.MaxRows);
    }
}
