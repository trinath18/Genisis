using System.Globalization;
using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Finance;
using Genisis.Api.Payment.ToHospital;

namespace Genisis.Api.Payment.ToMember;

/// <summary>The criteria of the claims to pay to members.</summary>
public class PaymentToMemberFilter
{
    /// <summary>The sources: dn (Pay2MBM, the claims with a debit / credit note) and auto (Pay2MBM(Auto), the claims with an amount payable to the member).</summary>
    public static readonly string[] Sources = ["dn", "auto"];

    /// <summary>dn or auto.</summary>
    public string? Source { get; set; } = "dn";

    /// <summary>Payor code (or "CODE - Name").</summary>
    public string? Payor { get; set; }

    /// <summary>Member number contains.</summary>
    public string? MemberNo { get; set; }

    /// <summary>Member name contains.</summary>
    public string? MemberName { get; set; }

    /// <summary>Case number from.</summary>
    public string? CaseFrom { get; set; }

    /// <summary>Case number to.</summary>
    public string? CaseTo { get; set; }

    /// <summary>Debit / credit note number from.</summary>
    public string? NoteFrom { get; set; }

    /// <summary>Debit / credit note number to.</summary>
    public string? NoteTo { get; set; }

    /// <summary>Claim close date from.</summary>
    public DateTime? CloseFrom { get; set; }

    /// <summary>Claim close date to.</summary>
    public DateTime? CloseTo { get; set; }
}

/// <summary>The source has to be one the screens offer and the dates in order.</summary>
public sealed class PaymentToMemberFilterValidator : RequestValidator<PaymentToMemberFilter>
{
    /// <summary>Declares the rules.</summary>
    public PaymentToMemberFilterValidator()
    {
        RuleFor(f => f.Source).Must(s => s is not null && PaymentToMemberFilter.Sources.Contains(s)).WithMessage("Select Pay2MBM or Pay2MBM(Auto).");
        RuleFor(f => f).Must(f => f.CloseFrom is null || f.CloseTo is null || f.CloseFrom <= f.CloseTo).WithMessage("Close Date From cannot be later than Close Date To.");
    }
}

/// <summary>A claim to pay to the member.</summary>
public record PaymentToMemberRow
{
    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Claim version.</summary>
    public string ClaimVersion { get; set; } = "";

    /// <summary>Member number.</summary>
    public string? MemberNo { get; set; }

    /// <summary>Member name.</summary>
    public string? MemberName { get; set; }

    /// <summary>Patient (covered person) name.</summary>
    public string? PatientName { get; set; }

    /// <summary>Payor code.</summary>
    public string? PayorCode { get; set; }

    /// <summary>The payor's PV prefix (PAY.PVInd).</summary>
    public string? PvInd { get; set; }

    /// <summary>Debit / credit note number.</summary>
    public string? DebitNoteNo { get; set; }

    /// <summary>The amount payable to the member (CLMPayableByMedix2M).</summary>
    public decimal? Amount { get; set; }

    /// <summary>The claim's payee (CLMPayTo, or the member's name).</summary>
    public string? PayTo { get; set; }

    /// <summary>The payee's bank (CLMBank).</summary>
    public string? Bank { get; set; }

    /// <summary>The payee's account number (CLMAccountNo).</summary>
    public string? Account { get; set; }

    /// <summary>The claim's pay mode (ClaimOthers.CLMPayMode; Pay2MBM(Auto) only).</summary>
    public string? PayMode { get; set; }

    /// <summary>The payor's receipt for the claim (Pay2MBM(Auto) only).</summary>
    public string? ReceiptNo { get; set; }

    /// <summary>The payor's receipt date.</summary>
    public DateTime? ReceiptDate { get; set; }

    /// <summary>Y when the claim's payment is held.</summary>
    public string? PayPending { get; set; }

    /// <summary>Bordx number.</summary>
    public string? BordxNo { get; set; }

    /// <summary>Claim close date.</summary>
    public DateTime? CloseDate { get; set; }

    /// <summary>The PV (or VOID) the claim is on, once done.</summary>
    public string? PvNo { get; set; }
}

/// <summary>The claims found, their number and total; <see cref="Truncated"/> when more than 5,000 matched.</summary>
public record PaymentToMemberResult(List<PaymentToMemberRow> Rows, int Count, decimal TotalAmount, bool Truncated);

/// <summary>A claim ticked, with the payee, bank and account to write when they are changed on the screen.</summary>
public class PaymentMemberClaim
{
    /// <summary>Case number.</summary>
    public string? CaseNo { get; set; }

    /// <summary>Claim version.</summary>
    public string? ClaimVersion { get; set; }

    /// <summary>Payee (the claim's when empty).</summary>
    public string? PayTo { get; set; }

    /// <summary>Bank (the claim's when empty).</summary>
    public string? Bank { get; set; }

    /// <summary>Account number (the claim's when empty).</summary>
    public string? Account { get; set; }
}

/// <summary>What Generate PV is asked for.</summary>
public class PaymentToMemberRequest
{
    /// <summary>dn or auto: the list the claims come from.</summary>
    public string? Source { get; set; } = "dn";

    /// <summary>PV date (also the cheque date and the accounting period).</summary>
    public DateTime? PvDate { get; set; }

    /// <summary>ONLINE or CHEQUE.</summary>
    public string? PaymentMode { get; set; } = "ONLINE";

    /// <summary>CHEQUE: the first cheque's number; the next PVs take the next numbers.</summary>
    public string? ChequeNo { get; set; }

    /// <summary>Remark written on every row.</summary>
    public string? Remark { get; set; }

    /// <summary>The claims ticked.</summary>
    public List<PaymentMemberClaim>? Claims { get; set; }
}

/// <summary>Generate PV's checks.</summary>
public sealed class PaymentToMemberRequestValidator : RequestValidator<PaymentToMemberRequest>
{
    /// <summary>Declares the rules.</summary>
    public PaymentToMemberRequestValidator()
    {
        RuleFor(r => r.Source).Must(s => s is not null && PaymentToMemberFilter.Sources.Contains(s)).WithMessage("Select Pay2MBM or Pay2MBM(Auto).");
        RuleFor(r => r.Claims).Must(c => c is { Count: > 0 } && c.All(k => !string.IsNullOrWhiteSpace(k.CaseNo) && !string.IsNullOrWhiteSpace(k.ClaimVersion)))
            .WithMessage("Please select the claims to pay.");
        RuleFor(r => r.Claims).Must(c => c is null || c.All(k => (k.PayTo?.Length ?? 0) <= 120 && (k.Bank?.Length ?? 0) <= 200 && (k.Account?.Length ?? 0) <= 60))
            .WithMessage("A payee, bank or account number is too long.");
        RuleFor(r => r.PvDate).NotNull().WithMessage("Please enter the PV Date.");
        RuleFor(r => r.PaymentMode).Must(m => m is not null && PaymentToHospitalRequest.Modes.Contains(m)).WithMessage("Payment Mode is ONLINE or CHEQUE.");
        RuleFor(r => r.ChequeNo).Must(PaymentCheques.IsValid).When(r => r.PaymentMode == "CHEQUE")
            .WithMessage("Please enter the Cheque No (it has to end with a number, e.g. MBB026496).");
        RuleFor(r => r.Remark).MaximumLength(400).WithMessage("The remark is too long (400 characters at most).");
    }
}

/// <summary>What Void is asked for.</summary>
public class PaymentToMemberVoidRequest
{
    /// <summary>dn or auto.</summary>
    public string? Source { get; set; } = "dn";

    /// <summary>The date of the VOID rows.</summary>
    public DateTime? VoidDate { get; set; }

    /// <summary>Why the claims are not paid.</summary>
    public string? Remark { get; set; }

    /// <summary>The claims ticked.</summary>
    public List<PaymentClaimKey>? Claims { get; set; }
}

/// <summary>Void's checks.</summary>
public sealed class PaymentToMemberVoidRequestValidator : RequestValidator<PaymentToMemberVoidRequest>
{
    /// <summary>Declares the rules.</summary>
    public PaymentToMemberVoidRequestValidator()
    {
        RuleFor(r => r.Source).Must(s => s is not null && PaymentToMemberFilter.Sources.Contains(s)).WithMessage("Select Pay2MBM or Pay2MBM(Auto).");
        RuleFor(r => r.Claims).Must(c => c is { Count: > 0 } && c.All(k => !string.IsNullOrWhiteSpace(k.CaseNo) && !string.IsNullOrWhiteSpace(k.ClaimVersion)))
            .WithMessage("Please select the claims to void.");
        RuleFor(r => r.VoidDate).NotNull().WithMessage("Please enter the PV Date.");
        RuleFor(r => r.Remark).MaximumLength(400).WithMessage("The remark is too long (400 characters at most).");
    }
}

/// <summary>A member voucher written.</summary>
public record MemberVoucher(string PvNo, string CaseNo, string ClaimVersion, string? PayTo, string? Bank, string? Account, decimal Amount, string ChequeNo, string Wording, string WordingTwo);

/// <summary>What Generate PV wrote.</summary>
public record PaymentToMemberDone(List<MemberVoucher> Vouchers);

/// <summary>How many claims Void closed.</summary>
public record PaymentToMemberVoided(int Count);

/// <summary>Reads the claims payable to members and writes Payment2MBM.</summary>
public interface IPaymentToMemberRepository
{
    /// <summary>The claims not yet paid or voided that match, at most <paramref name="max"/>.</summary>
    Task<List<PaymentToMemberRow>> SearchAsync(IDbSession session, PaymentToMemberFilter filter, int max);

    /// <summary>One claim of the source, done or not; null when the source does not list it.</summary>
    Task<PaymentToMemberRow?> ClaimAsync(IDbSession session, string source, string caseNo, string version);

    /// <summary>Locks the claim's rows: true when it has a row already.</summary>
    Task<bool> LockDoneAsync(IDbSession session, string caseNo, string version);

    /// <summary>The next PV number of the prefix; null when the prefix has no counter.</summary>
    Task<string?> NextPvAsync(IDbSession session, string prefix);

    /// <summary>Writes a voucher's row.</summary>
    Task InsertAsync(IDbSession session, MemberVoucher voucher, DateTime pvDate, string paymentMode, string? remark, string userCode);

    /// <summary>Writes a claim's VOID row.</summary>
    Task VoidAsync(IDbSession session, string caseNo, string version, DateTime voidDate, string? remark, string userCode);
}

/// <inheritdoc />
public sealed class PaymentToMemberRepository : IPaymentToMemberRepository
{
    private static string? Upper(string? value) => FinanceLimits.Text(value)?.ToUpperInvariant();

    /// <inheritdoc />
    public Task<List<PaymentToMemberRow>> SearchAsync(IDbSession session, PaymentToMemberFilter f, int max) =>
        session.Connection.ProcQueryAsync<PaymentToMemberRow>("genisis.PaymentToMember_Search", new
        {
            source = f.Source, payor = Upper((f.Payor ?? "").Split(" - ")[0]), memberNo = Upper(f.MemberNo), memberName = Upper(f.MemberName), caseFrom = Upper(f.CaseFrom), caseTo = Upper(f.CaseTo),
            noteFrom = Upper(f.NoteFrom), noteTo = Upper(f.NoteTo), closeFrom = FinanceLimits.Day(f.CloseFrom), closeTo = FinanceLimits.Day(f.CloseTo), max,
        }, session.Transaction);

    /// <inheritdoc />
    public Task<PaymentToMemberRow?> ClaimAsync(IDbSession session, string source, string caseNo, string version) =>
        session.Connection.ProcFirstOrDefaultAsync<PaymentToMemberRow>("genisis.PaymentToMember_Search", new { source, caseNo, version, max = 1 }, session.Transaction);

    /// <inheritdoc />
    public async Task<bool> LockDoneAsync(IDbSession session, string caseNo, string version) =>
        await session.Connection.ProcScalarAsync<int>("genisis.PaymentToMember_Lock", new { caseNo, version }, session.Transaction) == 2;

    /// <inheritdoc />
    public Task<string?> NextPvAsync(IDbSession session, string prefix) => new PaymentToHospitalRepository().NextPvAsync(session, prefix);

    /// <inheritdoc />
    public Task InsertAsync(IDbSession session, MemberVoucher v, DateTime pvDate, string paymentMode, string? remark, string userCode) =>
        session.Connection.ProcExecAsync("genisis.PaymentToMember_Insert", new
        {
            pvNo = v.PvNo, pvDate, total = v.Amount, chequeNo = v.ChequeNo, payTo = v.PayTo, bank = v.Bank, account = v.Account, caseNo = v.CaseNo, version = v.ClaimVersion, amount = v.Amount,
            remark, wording = v.Wording, wordingTwo = v.WordingTwo, paymentMode, userCode,
        }, session.Transaction);

    /// <inheritdoc />
    public Task VoidAsync(IDbSession session, string caseNo, string version, DateTime voidDate, string? remark, string userCode) =>
        session.Connection.ProcExecAsync("genisis.PaymentToMember_Void", new { caseNo, version, voidDate, remark, userCode }, session.Transaction);
}

/// <summary>Payment > To Member > Pay2MBM and Pay2MBM(Auto) (legacy <c>FrmPayMember</c> and <c>frmPayMemberJul08</c>; rebuilt: their sources are not in the source tree).</summary>
public class PaymentToMemberService(IDbSessions sessions, IPaymentToMemberRepository repository, ILogger<PaymentToMemberService> log)
{
    /// <summary>The claims not yet paid or voided. Nothing found is an empty list.</summary>
    public async Task<PaymentToMemberResult> SearchAsync(PaymentToMemberFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        var rows = await repository.SearchAsync(session, filter, FinanceLimits.FetchRows);
        var shown = rows.Take(FinanceLimits.MaxRows).ToList();
        return new PaymentToMemberResult(shown, shown.Count, shown.Sum(r => r.Amount ?? 0), rows.Count > FinanceLimits.MaxRows);
    }

    private static string Name(string caseNo, string version) => $"{caseNo}-{version}";

    private async Task<PaymentToMemberRow> OpenClaimAsync(IDbSession session, string source, string caseNo, string version)
    {
        if (await repository.LockDoneAsync(session, caseNo, version)) throw new ConflictException($"Claim {Name(caseNo, version)} has been paid or voided.");
        var claim = await repository.ClaimAsync(session, source, caseNo, version) ?? throw new NotFoundException($"Claim {Name(caseNo, version)} is not in the list.");
        if (claim.PvNo is not null) throw new ConflictException($"Claim {Name(caseNo, version)} has been paid or voided.");
        return claim;
    }

    /// <summary>Generate PV: one voucher per claim ticked, all or nothing. A claim done meanwhile, held, with nothing payable or of a payor with no PV prefix stops the run.</summary>
    public async Task<PaymentToMemberDone> CreateAsync(PaymentToMemberRequest request, string userCode)
    {
        var pvDate = request.PvDate!.Value.Date;
        var mode = request.PaymentMode!;
        var cheque = request.ChequeNo?.Trim();
        var seen = new HashSet<(string, string)>();
        await using var session = await sessions.BeginAsync();

        var vouchers = new List<MemberVoucher>();
        foreach (var k in request.Claims!)
        {
            var (caseNo, version) = (k.CaseNo!.Trim().ToUpperInvariant(), k.ClaimVersion!.Trim());
            if (!seen.Add((caseNo, version))) continue;
            var claim = await OpenClaimAsync(session, request.Source!, caseNo, version);
            var name = Name(caseNo, version);
            if ((claim.Amount ?? 0) <= 0) throw new BusinessRuleException($"Claim {name} has nothing payable to the member: void it instead.");
            if (claim.PayPending == "Y") throw new BusinessRuleException($"The payment of claim {name} is pending.");
            if (claim.PvInd is null || !PaymentToHospitalService.Prefixes.Contains(claim.PvInd)) throw new BusinessRuleException($"Payor {claim.PayorCode} of claim {name} has no PV prefix (PAY.PVInd).");
            var payTo = FinanceLimits.Text(k.PayTo)?.ToUpperInvariant() ?? claim.PayTo;
            if (string.IsNullOrWhiteSpace(payTo)) throw new BusinessRuleException($"Please enter the payee of claim {name}.");

            var pv = await repository.NextPvAsync(session, claim.PvInd) ?? throw new BusinessRuleException($"PV prefix {claim.PvInd} has no counter.");
            string chequeNo;
            if (mode == "ONLINE") chequeNo = pvDate.ToString("ddMMyyyy", CultureInfo.InvariantCulture);
            else
            {
                chequeNo = cheque!;
                cheque = PaymentCheques.Next(cheque!);
            }

            var (wording, wordingTwo) = PvAmountWords.Lines(claim.Amount!.Value);
            var voucher = new MemberVoucher(pv, caseNo, version, payTo, FinanceLimits.Text(k.Bank)?.ToUpperInvariant() ?? claim.Bank, FinanceLimits.Text(k.Account) ?? claim.Account,
                claim.Amount.Value, chequeNo, wording, wordingTwo);
            await repository.InsertAsync(session, voucher, pvDate, mode, FinanceLimits.Text(request.Remark), userCode);
            vouchers.Add(voucher);
        }

        await session.CommitAsync();
        log.LogInformation("Payment to member ({Source}): {Count} PV(s) by {User}", request.Source, vouchers.Count, userCode);
        return new PaymentToMemberDone(vouchers);
    }

    /// <summary>Void: closes the claims ticked with a VOID row (nothing is paid to the member), all or nothing.</summary>
    public async Task<PaymentToMemberVoided> VoidAsync(PaymentToMemberVoidRequest request, string userCode)
    {
        var keys = request.Claims!.Select(k => (Case: k.CaseNo!.Trim().ToUpperInvariant(), Version: k.ClaimVersion!.Trim())).Distinct().ToList();
        await using var session = await sessions.BeginAsync();
        foreach (var (caseNo, version) in keys)
        {
            await OpenClaimAsync(session, request.Source!, caseNo, version);
            await repository.VoidAsync(session, caseNo, version, request.VoidDate!.Value.Date, FinanceLimits.Text(request.Remark)?.ToUpperInvariant(), userCode);
        }

        await session.CommitAsync();
        log.LogInformation("Payment to member ({Source}): {Count} claim(s) voided by {User}", request.Source, keys.Count, userCode);
        return new PaymentToMemberVoided(keys.Count);
    }
}
