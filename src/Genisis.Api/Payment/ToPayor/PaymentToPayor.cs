using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Finance;
using Genisis.Api.Payment.ToHospital;

namespace Genisis.Api.Payment.ToPayor;

/// <summary>The criteria of the reversed claims to pay back.</summary>
public class PaymentToPayorFilter
{
    /// <summary>Payor code (or "CODE - Name").</summary>
    public string? Payor { get; set; }

    /// <summary>Case number from.</summary>
    public string? CaseFrom { get; set; }

    /// <summary>Case number to.</summary>
    public string? CaseTo { get; set; }

    /// <summary>Bordx number from.</summary>
    public string? BordxFrom { get; set; }

    /// <summary>Bordx number to.</summary>
    public string? BordxTo { get; set; }

    /// <summary>Claim close date from.</summary>
    public DateTime? CloseFrom { get; set; }

    /// <summary>Claim close date to.</summary>
    public DateTime? CloseTo { get; set; }
}

/// <summary>The close dates have to be in order.</summary>
public sealed class PaymentToPayorFilterValidator : RequestValidator<PaymentToPayorFilter>
{
    /// <summary>Declares the rules.</summary>
    public PaymentToPayorFilterValidator() =>
        RuleFor(f => f).Must(f => f.CloseFrom is null || f.CloseTo is null || f.CloseFrom <= f.CloseTo).WithMessage("Close Date From cannot be later than Close Date To.");
}

/// <summary>A reversed claim to pay back to its payor.</summary>
public record PaymentToPayorRow
{
    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Claim version.</summary>
    public string ClaimVersion { get; set; } = "";

    /// <summary>Member number.</summary>
    public string? MemberNo { get; set; }

    /// <summary>Member name.</summary>
    public string? MemberName { get; set; }

    /// <summary>Payor code (the first two letters of the case number).</summary>
    public string? PayorCode { get; set; }

    /// <summary>Payor name.</summary>
    public string? PayorName { get; set; }

    /// <summary>The payor's PV prefix (PAY.PVInd).</summary>
    public string? PvInd { get; set; }

    /// <summary>Bordx number.</summary>
    public string? BordxNo { get; set; }

    /// <summary>Claim close date.</summary>
    public DateTime? CloseDate { get; set; }

    /// <summary>The amount to pay back (the reversed amount, -CLMTotalApproved).</summary>
    public decimal? Amount { get; set; }

    /// <summary>The PV the claim is on, once paid back.</summary>
    public string? PvNo { get; set; }
}

/// <summary>The claims found, their number and total; <see cref="Truncated"/> when more than 5,000 matched.</summary>
public record PaymentToPayorResult(List<PaymentToPayorRow> Rows, int Count, decimal TotalAmount, bool Truncated);

/// <summary>What Generate PV is asked for.</summary>
public class PaymentToPayorRequest
{
    /// <summary>PV date (also the cheque date and the accounting period).</summary>
    public DateTime? PvDate { get; set; }

    /// <summary>The first cheque's number; the next PVs take the next numbers.</summary>
    public string? ChequeNo { get; set; }

    /// <summary>Remark written on every row (PAYRemark).</summary>
    public string? Remark { get; set; }

    /// <summary>The claims ticked.</summary>
    public List<PaymentClaimKey>? Claims { get; set; }
}

/// <summary>Generate PV's checks.</summary>
public sealed class PaymentToPayorRequestValidator : RequestValidator<PaymentToPayorRequest>
{
    /// <summary>Declares the rules.</summary>
    public PaymentToPayorRequestValidator()
    {
        RuleFor(r => r.Claims).Must(c => c is { Count: > 0 } && c.All(k => !string.IsNullOrWhiteSpace(k.CaseNo) && !string.IsNullOrWhiteSpace(k.ClaimVersion)))
            .WithMessage("Please select the claims to pay.");
        RuleFor(r => r.PvDate).NotNull().WithMessage("Please enter the PV Date.");
        RuleFor(r => r.ChequeNo).Must(PaymentCheques.IsValid).WithMessage("Please enter the Cheque No (it has to end with a number, e.g. CB002704).");
        RuleFor(r => r.Remark).MaximumLength(400).WithMessage("The remark is too long (400 characters at most).");
    }
}

/// <summary>A voucher written.</summary>
public record PayorVoucher(string PvNo, string PayorCode, string? PayorName, int Claims, decimal Amount, string ChequeNo);

/// <summary>What Generate PV wrote.</summary>
public record PaymentToPayorDone(List<PayorVoucher> Vouchers);

/// <summary>Reads the reversed claims and writes the vouchers.</summary>
public interface IPaymentToPayorRepository
{
    /// <summary>The claims not yet paid back that match, at most <paramref name="max"/>.</summary>
    Task<List<PaymentToPayorRow>> SearchAsync(IDbSession session, PaymentToPayorFilter filter, int max);

    /// <summary>One claim, paid back or not; null when it is not a reversed claim.</summary>
    Task<PaymentToPayorRow?> ClaimAsync(IDbSession session, string caseNo, string version);

    /// <summary>Locks the claim's rows: true when it is paid back already.</summary>
    Task<bool> LockPaidAsync(IDbSession session, string caseNo, string version);

    /// <summary>The next PV number of the prefix; null when the prefix has no counter.</summary>
    Task<string?> NextPvAsync(IDbSession session, string prefix);

    /// <summary>Writes a claim's row of a voucher.</summary>
    Task InsertAsync(IDbSession session, PayorVoucher voucher, DateTime pvDate, PaymentToPayorRow claim, string? remark, string userCode);
}

/// <inheritdoc />
public sealed class PaymentToPayorRepository : IPaymentToPayorRepository
{
    private static string? Upper(string? value) => FinanceLimits.Text(value)?.ToUpperInvariant();

    /// <inheritdoc />
    public Task<List<PaymentToPayorRow>> SearchAsync(IDbSession session, PaymentToPayorFilter f, int max) =>
        session.Connection.ProcQueryAsync<PaymentToPayorRow>("genisis.PaymentToPayor_Search", new
        {
            payor = Upper((f.Payor ?? "").Split(" - ")[0]), caseFrom = Upper(f.CaseFrom), caseTo = Upper(f.CaseTo), bordxFrom = Upper(f.BordxFrom), bordxTo = Upper(f.BordxTo),
            closeFrom = FinanceLimits.Day(f.CloseFrom), closeTo = FinanceLimits.Day(f.CloseTo), max,
        }, session.Transaction);

    /// <inheritdoc />
    public Task<PaymentToPayorRow?> ClaimAsync(IDbSession session, string caseNo, string version) =>
        session.Connection.ProcFirstOrDefaultAsync<PaymentToPayorRow>("genisis.PaymentToPayor_Search", new { caseNo, version, max = 1 }, session.Transaction);

    /// <inheritdoc />
    public async Task<bool> LockPaidAsync(IDbSession session, string caseNo, string version) =>
        await session.Connection.ProcScalarAsync<int>("genisis.PaymentToPayor_Lock", new { caseNo, version }, session.Transaction) == 2;

    /// <inheritdoc />
    public Task<string?> NextPvAsync(IDbSession session, string prefix) => new PaymentToHospitalRepository().NextPvAsync(session, prefix);

    /// <inheritdoc />
    public Task InsertAsync(IDbSession session, PayorVoucher v, DateTime pvDate, PaymentToPayorRow claim, string? remark, string userCode) =>
        session.Connection.ProcExecAsync("genisis.PaymentToPayor_Insert", new
        {
            pvNo = v.PvNo, pvDate, payTo = v.PayorCode, total = v.Amount, chequeNo = v.ChequeNo, caseNo = claim.CaseNo, version = claim.ClaimVersion, amount = claim.Amount ?? 0, remark, userCode,
        }, session.Transaction);
}

/// <summary>Payment > Payor (legacy <c>FrmPaymentPayor</c>; rebuilt: its source is not in the source tree).</summary>
public class PaymentToPayorService(IDbSessions sessions, IPaymentToPayorRepository repository, ILogger<PaymentToPayorService> log)
{
    /// <summary>The reversed claims not yet paid back. Nothing found is an empty list.</summary>
    public async Task<PaymentToPayorResult> SearchAsync(PaymentToPayorFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        var rows = await repository.SearchAsync(session, filter, FinanceLimits.FetchRows);
        var shown = rows.Take(FinanceLimits.MaxRows).ToList();
        return new PaymentToPayorResult(shown, shown.Count, shown.Sum(r => r.Amount ?? 0), rows.Count > FinanceLimits.MaxRows);
    }

    /// <summary>Generate PV: one voucher per payor (and PV prefix), a row per claim ticked, the cheques numbered on from the first; all or nothing.</summary>
    public async Task<PaymentToPayorDone> CreateAsync(PaymentToPayorRequest request, string userCode)
    {
        var keys = request.Claims!.Select(k => (Case: k.CaseNo!.Trim().ToUpperInvariant(), Version: k.ClaimVersion!.Trim())).Distinct().ToList();
        var pvDate = request.PvDate!.Value.Date;
        await using var session = await sessions.BeginAsync();

        var claims = new List<PaymentToPayorRow>();
        foreach (var (caseNo, version) in keys)
        {
            var name = $"{caseNo}-{version}";
            if (await repository.LockPaidAsync(session, caseNo, version)) throw new ConflictException($"Claim {name} has been paid back.");
            var claim = await repository.ClaimAsync(session, caseNo, version) ?? throw new NotFoundException($"Claim {name} is not a reversed claim.");
            if (claim.PvNo is not null) throw new ConflictException($"Claim {name} has been paid back.");
            if ((claim.Amount ?? 0) <= 0) throw new BusinessRuleException($"Claim {name} has nothing to pay back.");
            if (claim.PvInd is null || !PaymentToHospitalService.Prefixes.Contains(claim.PvInd)) throw new BusinessRuleException($"Payor {claim.PayorCode} of claim {name} has no PV prefix (PAY.PVInd).");
            claims.Add(claim);
        }

        var cheque = request.ChequeNo!.Trim();
        var vouchers = new List<PayorVoucher>();
        foreach (var group in claims.GroupBy(c => (c.PayorCode, c.PvInd)).OrderBy(g => g.Key.PayorCode).ThenBy(g => g.Key.PvInd))
        {
            var pv = await repository.NextPvAsync(session, group.Key.PvInd!) ?? throw new BusinessRuleException($"PV prefix {group.Key.PvInd} has no counter.");
            var voucher = new PayorVoucher(pv, group.Key.PayorCode!, group.First().PayorName, group.Count(), group.Sum(c => c.Amount ?? 0), cheque);
            cheque = PaymentCheques.Next(cheque);
            foreach (var claim in group) await repository.InsertAsync(session, voucher, pvDate, claim, FinanceLimits.Text(request.Remark), userCode);
            vouchers.Add(voucher);
        }

        await session.CommitAsync();
        log.LogInformation("Payment to payor: {Count} PV(s), {Claims} claim(s) by {User}", vouchers.Count, claims.Count, userCode);
        return new PaymentToPayorDone(vouchers);
    }
}
