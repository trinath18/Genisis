using System.Globalization;
using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Finance;

namespace Genisis.Api.Payment.ToHospital;

/// <summary>The criteria of the claims to pay.</summary>
public class PaymentToHospitalFilter
{
    /// <summary>Payor code (or "CODE - Name").</summary>
    public string? Payor { get; set; }

    /// <summary>Hospital code, or a part of its name.</summary>
    public string? Hospital { get; set; }

    /// <summary>Case number from.</summary>
    public string? CaseFrom { get; set; }

    /// <summary>Case number to.</summary>
    public string? CaseTo { get; set; }

    /// <summary>Bordx number from.</summary>
    public string? BordxFrom { get; set; }

    /// <summary>Bordx number to.</summary>
    public string? BordxTo { get; set; }

    /// <summary>Bordx date from.</summary>
    public DateTime? BordxDateFrom { get; set; }

    /// <summary>Bordx date to.</summary>
    public DateTime? BordxDateTo { get; set; }
}

/// <summary>The bordx dates have to be in order.</summary>
public sealed class PaymentToHospitalFilterValidator : RequestValidator<PaymentToHospitalFilter>
{
    /// <summary>Declares the rules.</summary>
    public PaymentToHospitalFilterValidator() =>
        RuleFor(f => f).Must(f => f.BordxDateFrom is null || f.BordxDateTo is null || f.BordxDateFrom <= f.BordxDateTo).WithMessage("Bordx Date From cannot be later than Bordx Date To.");
}

/// <summary>A claim the hospital is to be paid for.</summary>
public record PaymentToHospitalRow
{
    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Claim version.</summary>
    public string ClaimVersion { get; set; } = "";

    /// <summary>Member number.</summary>
    public string? MemberNo { get; set; }

    /// <summary>The invoices' payee (HOS.HOSCode).</summary>
    public string? HospitalCode { get; set; }

    /// <summary>Hospital name.</summary>
    public string? HospitalName { get; set; }

    /// <summary>The hospital's payee (HOS.HOSPayee): the PV is made out to it.</summary>
    public string? Payee { get; set; }

    /// <summary>Bordx number.</summary>
    public string? BordxNo { get; set; }

    /// <summary>Bordx date.</summary>
    public DateTime? BordxDate { get; set; }

    /// <summary>Payor code.</summary>
    public string? PayorCode { get; set; }

    /// <summary>The payor's PV prefix (PAY.PVInd: CB, CC or CG).</summary>
    public string? PvInd { get; set; }

    /// <summary>The amount payable to the hospital (CLMPayableByMedix2H).</summary>
    public decimal? Amount { get; set; }

    /// <summary>Number of invoices of the claim.</summary>
    public int Invoices { get; set; }

    /// <summary>Earliest invoice date.</summary>
    public DateTime? InvDate { get; set; }

    /// <summary>Y when the claim's payment is held (CASCrossReference.CASPayPending).</summary>
    public string? PayPending { get; set; }

    /// <summary>Why the payment is held.</summary>
    public string? PendingRemark { get; set; }

    /// <summary>The PV the claim is on, once paid.</summary>
    public string? PvNo { get; set; }
}

/// <summary>The claims found, their number and total; <see cref="Truncated"/> when more than 5,000 matched.</summary>
public record PaymentToHospitalResult(List<PaymentToHospitalRow> Rows, int Count, decimal TotalAmount, bool Truncated);

/// <summary>A claim ticked.</summary>
public class PaymentClaimKey
{
    /// <summary>Case number.</summary>
    public string? CaseNo { get; set; }

    /// <summary>Claim version.</summary>
    public string? ClaimVersion { get; set; }
}

/// <summary>What Generate PV is asked for: the claims ticked and how they are paid.</summary>
public class PaymentToHospitalRequest
{
    /// <summary>The payment modes.</summary>
    public static readonly string[] Modes = ["ONLINE", "CHEQUE"];

    /// <summary>PV date (also the cheque date and the accounting period).</summary>
    public DateTime? PvDate { get; set; }

    /// <summary>ONLINE or CHEQUE.</summary>
    public string? PaymentMode { get; set; } = "ONLINE";

    /// <summary>CHEQUE: the first cheque's number; the next PVs take the next numbers.</summary>
    public string? ChequeNo { get; set; }

    /// <summary>Remark written on every row (HOSRemark).</summary>
    public string? Remark { get; set; }

    /// <summary>The claims ticked.</summary>
    public List<PaymentClaimKey>? Claims { get; set; }
}

/// <summary>Generate PV's checks.</summary>
public sealed class PaymentToHospitalRequestValidator : RequestValidator<PaymentToHospitalRequest>
{
    /// <summary>Declares the rules.</summary>
    public PaymentToHospitalRequestValidator()
    {
        RuleFor(r => r.Claims).Must(c => c is { Count: > 0 } && c.All(k => !string.IsNullOrWhiteSpace(k.CaseNo) && !string.IsNullOrWhiteSpace(k.ClaimVersion)))
            .WithMessage("Please select the claims to pay.");
        RuleFor(r => r.PvDate).NotNull().WithMessage("Please enter the PV Date.");
        RuleFor(r => r.PaymentMode).Must(m => m is not null && PaymentToHospitalRequest.Modes.Contains(m)).WithMessage("Payment Mode is ONLINE or CHEQUE.");
        RuleFor(r => r.ChequeNo).Must(PaymentCheques.IsValid).When(r => r.PaymentMode == "CHEQUE")
            .WithMessage("Please enter the Cheque No (it has to end with a number, e.g. MBB026496).");
        RuleFor(r => r.Remark).MaximumLength(400).WithMessage("The remark is too long (400 characters at most).");
    }
}

/// <summary>A voucher written.</summary>
public record PaymentVoucher(string PvNo, string? HospitalName, string? Payee, int Claims, decimal Amount, string ChequeNo, string Wording, string WordingTwo);

/// <summary>What Generate PV wrote.</summary>
public record PaymentToHospitalDone(List<PaymentVoucher> Vouchers);

/// <summary>Cheque numbers: letters then a number; the next cheque is the number plus one, as wide.</summary>
public static class PaymentCheques
{
    /// <summary>True for a cheque number that ends with digits.</summary>
    public static bool IsValid(string? chequeNo) => chequeNo is { Length: > 0 and <= 40 } c && char.IsAsciiDigit(c.Trim()[^1]);

    /// <summary>The next cheque's number (MBB026496 → MBB026497).</summary>
    public static string Next(string chequeNo)
    {
        var c = chequeNo.Trim();
        var start = c.Length;
        while (start > 0 && char.IsAsciiDigit(c[start - 1])) start--;
        var digits = c[start..];
        var next = (decimal.Parse(digits, CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture);
        return c[..start] + next.PadLeft(digits.Length, '0');
    }
}

/// <summary>Reads the claims payable to hospitals and writes the vouchers.</summary>
public interface IPaymentToHospitalRepository
{
    /// <summary>The claims not yet paid that match, at most <paramref name="max"/>.</summary>
    Task<List<PaymentToHospitalRow>> SearchAsync(IDbSession session, PaymentToHospitalFilter filter, int max);

    /// <summary>One claim, paid or not; null when it is not payable to a hospital.</summary>
    Task<PaymentToHospitalRow?> ClaimAsync(IDbSession session, string caseNo, string version);

    /// <summary>Locks the claim's payment rows: true when it is paid already.</summary>
    Task<bool> LockPaidAsync(IDbSession session, string caseNo, string version);

    /// <summary>The next PV number of the prefix; null when the prefix has no counter.</summary>
    Task<string?> NextPvAsync(IDbSession session, string prefix);

    /// <summary>Writes a claim's row of a voucher.</summary>
    Task InsertAsync(IDbSession session, PaymentVoucher voucher, DateTime pvDate, PaymentToHospitalRow claim, string paymentMode, string? remark, string userCode);
}

/// <inheritdoc />
public sealed class PaymentToHospitalRepository : IPaymentToHospitalRepository
{
    private sealed record NextPv(int Result, string? PvNo);

    /// <inheritdoc />
    public Task<List<PaymentToHospitalRow>> SearchAsync(IDbSession session, PaymentToHospitalFilter f, int max) =>
        session.Connection.ProcQueryAsync<PaymentToHospitalRow>("genisis.PaymentToHospital_Search", new
        {
            payor = FinanceLimits.Text((f.Payor ?? "").Split(" - ")[0])?.ToUpperInvariant(), hospital = FinanceLimits.Text(f.Hospital)?.ToUpperInvariant(),
            caseFrom = FinanceLimits.Text(f.CaseFrom)?.ToUpperInvariant(), caseTo = FinanceLimits.Text(f.CaseTo)?.ToUpperInvariant(), bordxFrom = FinanceLimits.Text(f.BordxFrom)?.ToUpperInvariant(),
            bordxTo = FinanceLimits.Text(f.BordxTo)?.ToUpperInvariant(), bordxDateFrom = FinanceLimits.Day(f.BordxDateFrom), bordxDateTo = FinanceLimits.Day(f.BordxDateTo), max,
        }, session.Transaction);

    /// <inheritdoc />
    public Task<PaymentToHospitalRow?> ClaimAsync(IDbSession session, string caseNo, string version) =>
        session.Connection.ProcFirstOrDefaultAsync<PaymentToHospitalRow>("genisis.PaymentToHospital_Search", new { caseNo, version, max = 1 }, session.Transaction);

    /// <inheritdoc />
    public async Task<bool> LockPaidAsync(IDbSession session, string caseNo, string version) =>
        await session.Connection.ProcScalarAsync<int>("genisis.PaymentToHospital_Lock", new { caseNo, version }, session.Transaction) == 2;

    /// <inheritdoc />
    public async Task<string?> NextPvAsync(IDbSession session, string prefix)
    {
        var next = await session.Connection.ProcFirstOrDefaultAsync<NextPv>("genisis.Payment_NextPv", new { prefix }, session.Transaction);
        return next is { Result: 0 } ? next.PvNo : null;
    }

    /// <inheritdoc />
    public Task InsertAsync(IDbSession session, PaymentVoucher v, DateTime pvDate, PaymentToHospitalRow claim, string paymentMode, string? remark, string userCode) =>
        session.Connection.ProcExecAsync("genisis.PaymentToHospital_Insert", new
        {
            pvNo = v.PvNo, pvDate, total = v.Amount, chequeNo = v.ChequeNo, caseNo = claim.CaseNo, version = claim.ClaimVersion, amount = claim.Amount ?? 0, remark,
            payee = v.Payee, wording = v.Wording, wordingTwo = v.WordingTwo, paymentMode, userCode,
        }, session.Transaction);
}

/// <summary>Payment > To Hospital (legacy <c>FrmPaymentHospital</c>; rebuilt: its source is not in the source tree).</summary>
public class PaymentToHospitalService(IDbSessions sessions, IPaymentToHospitalRepository repository, ILogger<PaymentToHospitalService> log)
{
    /// <summary>The prefixes that have a PV counter.</summary>
    public static readonly string[] Prefixes = ["CB", "CC", "CG"];

    /// <summary>The claims not yet paid to their hospital. Nothing found is an empty list.</summary>
    public async Task<PaymentToHospitalResult> SearchAsync(PaymentToHospitalFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        var rows = await repository.SearchAsync(session, filter, FinanceLimits.FetchRows);
        var shown = rows.Take(FinanceLimits.MaxRows).ToList();
        return new PaymentToHospitalResult(shown, shown.Count, shown.Sum(r => r.Amount ?? 0), rows.Count > FinanceLimits.MaxRows);
    }

    /// <summary>Generate PV: one voucher per hospital and PV prefix, a row per claim ticked, all or nothing. A claim paid meanwhile, held, or of a payor with no PV prefix stops the run.</summary>
    public async Task<PaymentToHospitalDone> CreateAsync(PaymentToHospitalRequest request, string userCode)
    {
        var keys = request.Claims!.Select(k => (Case: k.CaseNo!.Trim().ToUpperInvariant(), Version: k.ClaimVersion!.Trim())).Distinct().ToList();
        var pvDate = request.PvDate!.Value.Date;
        var mode = request.PaymentMode!;
        await using var session = await sessions.BeginAsync();

        var claims = new List<PaymentToHospitalRow>();
        foreach (var (caseNo, version) in keys)
        {
            var name = $"{caseNo}-{version}";
            if (await repository.LockPaidAsync(session, caseNo, version)) throw new ConflictException($"Claim {name} has been paid.");
            var claim = await repository.ClaimAsync(session, caseNo, version) ?? throw new NotFoundException($"Claim {name} is not payable to a hospital.");
            if (claim.PvNo is not null) throw new ConflictException($"Claim {name} has been paid.");
            if ((claim.Amount ?? 0) == 0) throw new BusinessRuleException($"Claim {name} has nothing payable to the hospital.");
            if (claim.PayPending == "Y") throw new BusinessRuleException($"The payment of claim {name} is pending{(string.IsNullOrWhiteSpace(claim.PendingRemark) ? "" : ": " + claim.PendingRemark)}.");
            if (claim.PvInd is null || !Prefixes.Contains(claim.PvInd)) throw new BusinessRuleException($"Payor {claim.PayorCode} of claim {name} has no PV prefix (PAY.PVInd).");
            if (string.IsNullOrWhiteSpace(claim.HospitalCode)) throw new BusinessRuleException($"Claim {name} has no hospital to pay.");
            claims.Add(claim);
        }

        var cheque = request.ChequeNo?.Trim();
        var vouchers = new List<PaymentVoucher>();
        foreach (var group in claims.GroupBy(c => (c.HospitalCode, c.PvInd)).OrderBy(g => g.First().HospitalName).ThenBy(g => g.Key.PvInd))
        {
            var first = group.First();
            var total = group.Sum(c => c.Amount ?? 0);
            if (total <= 0) throw new BusinessRuleException($"The payment to {first.HospitalName} adds up to {total:N2}: a PV has to be more than 0.");
            var pv = await repository.NextPvAsync(session, group.Key.PvInd!) ?? throw new BusinessRuleException($"PV prefix {group.Key.PvInd} has no counter.");
            string chequeNo;
            if (mode == "ONLINE") chequeNo = pvDate.ToString("ddMMyyyy", CultureInfo.InvariantCulture);
            else
            {
                chequeNo = cheque!;
                cheque = PaymentCheques.Next(cheque!);
            }

            var (wording, wordingTwo) = PvAmountWords.Lines(total);
            var voucher = new PaymentVoucher(pv, first.HospitalName, string.IsNullOrWhiteSpace(first.Payee) ? first.HospitalName : first.Payee, group.Count(), total, chequeNo, wording, wordingTwo);
            foreach (var claim in group) await repository.InsertAsync(session, voucher, pvDate, claim, mode, FinanceLimits.Text(request.Remark), userCode);
            vouchers.Add(voucher);
        }

        await session.CommitAsync();
        log.LogInformation("Payment to hospital: {Count} PV(s), {Claims} claim(s) by {User}", vouchers.Count, claims.Count, userCode);
        return new PaymentToHospitalDone(vouchers);
    }
}
