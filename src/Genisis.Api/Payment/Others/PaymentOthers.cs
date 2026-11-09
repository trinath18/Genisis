using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Finance;
using Genisis.Api.Payment.ToHospital;

namespace Genisis.Api.Payment.Others;

/// <summary>The criteria of the vouchers listed.</summary>
public class PaymentOthersFilter
{
    /// <summary>PV number from.</summary>
    public string? PvFrom { get; set; }

    /// <summary>PV number to.</summary>
    public string? PvTo { get; set; }

    /// <summary>PV date from.</summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>PV date to.</summary>
    public DateTime? DateTo { get; set; }

    /// <summary>Payee contains.</summary>
    public string? PayTo { get; set; }

    /// <summary>Case number (exact).</summary>
    public string? CaseNo { get; set; }
}

/// <summary>The PV dates have to be in order.</summary>
public sealed class PaymentOthersFilterValidator : RequestValidator<PaymentOthersFilter>
{
    /// <summary>Declares the rules.</summary>
    public PaymentOthersFilterValidator() =>
        RuleFor(f => f).Must(f => f.DateFrom is null || f.DateTo is null || f.DateFrom <= f.DateTo).WithMessage("PV Date From cannot be later than PV Date To.");
}

/// <summary>A voucher of PaymentOthers.</summary>
public record PaymentOthersRow
{
    /// <summary>PVOthIndex.</summary>
    public int Id { get; set; }

    /// <summary>PV number.</summary>
    public string? PvNo { get; set; }

    /// <summary>PV date.</summary>
    public DateTime? PvDate { get; set; }

    /// <summary>Payee.</summary>
    public string? PayTo { get; set; }

    /// <summary>Cheque amount.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Cheque number.</summary>
    public string? ChequeNo { get; set; }

    /// <summary>Cheque date.</summary>
    public DateTime? ChequeDate { get; set; }

    /// <summary>The payee's bank.</summary>
    public string? PayeeBank { get; set; }

    /// <summary>The payee's account number.</summary>
    public string? PayeeAccount { get; set; }

    /// <summary>Case number, when the payment is for a claim.</summary>
    public string? CaseNo { get; set; }

    /// <summary>Remark.</summary>
    public string? Remark { get; set; }

    /// <summary>Bank charges.</summary>
    public decimal? BankCharges { get; set; }

    /// <summary>Accounting period month.</summary>
    public string? AccMonth { get; set; }

    /// <summary>Accounting period year.</summary>
    public string? AccYear { get; set; }

    /// <summary>PV status (A).</summary>
    public string? Status { get; set; }

    /// <summary>User who wrote it.</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>When it was written.</summary>
    public DateTime? UpdatedOn { get; set; }
}

/// <summary>The vouchers found, their number and total; <see cref="Truncated"/> when more than 5,000 matched.</summary>
public record PaymentOthersResult(List<PaymentOthersRow> Rows, int Count, decimal TotalAmount, bool Truncated);

/// <summary>A new voucher.</summary>
public class PaymentOthersRequest
{
    /// <summary>PV prefix: CB, CC or CG.</summary>
    public string? Prefix { get; set; } = "CB";

    /// <summary>PV date (also the accounting period).</summary>
    public DateTime? PvDate { get; set; }

    /// <summary>Payee.</summary>
    public string? PayTo { get; set; }

    /// <summary>Amount.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Cheque number.</summary>
    public string? ChequeNo { get; set; }

    /// <summary>Cheque date (the PV date when empty).</summary>
    public DateTime? ChequeDate { get; set; }

    /// <summary>The payee's bank.</summary>
    public string? PayeeBank { get; set; }

    /// <summary>The payee's account number.</summary>
    public string? PayeeAccount { get; set; }

    /// <summary>Case number, when the payment is for a claim.</summary>
    public string? CaseNo { get; set; }

    /// <summary>Remark.</summary>
    public string? Remark { get; set; }

    /// <summary>Bank charges.</summary>
    public decimal? BankCharges { get; set; }
}

/// <summary>Save's checks, in the order of the screen.</summary>
public sealed class PaymentOthersRequestValidator : RequestValidator<PaymentOthersRequest>
{
    /// <summary>Declares the rules.</summary>
    public PaymentOthersRequestValidator()
    {
        RuleFor(r => r.Prefix).Must(p => p is not null && PaymentToHospitalService.Prefixes.Contains(p)).WithMessage("PV Type is CB, CC or CG.");
        RuleFor(r => r.PvDate).NotNull().WithMessage("Please enter the PV Date.");
        RuleFor(r => r.PayTo).Must(p => !string.IsNullOrWhiteSpace(p)).WithMessage("Please enter the Pay To.").MaximumLength(120).WithMessage("Pay To is too long (120 characters at most).");
        RuleFor(r => r.Amount).Must(a => a is > 0 and < 100_000_000m).WithMessage("Please enter the Amount.");
        RuleFor(r => r.ChequeNo).Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Please enter the Cheque No.").MaximumLength(40).WithMessage("The Cheque No is too long.");
        RuleFor(r => r.BankCharges).Must(b => b is null or >= 0).WithMessage("The bank charges cannot be negative.");
        RuleFor(r => r.PayeeBank).MaximumLength(200).WithMessage("The bank is too long (200 characters at most).");
        RuleFor(r => r.PayeeAccount).MaximumLength(60).WithMessage("The account number is too long (60 characters at most).");
        RuleFor(r => r.CaseNo).MaximumLength(30).WithMessage("The case number is too long.");
        RuleFor(r => r.Remark).MaximumLength(400).WithMessage("The remark is too long (400 characters at most).");
    }
}

/// <summary>The voucher written.</summary>
public record PaymentOthersDone(int Id, string PvNo);

/// <summary>Reads and writes PaymentOthers.</summary>
public interface IPaymentOthersRepository
{
    /// <summary>The vouchers that match, at most <paramref name="max"/>.</summary>
    Task<List<PaymentOthersRow>> SearchAsync(IDbSession session, PaymentOthersFilter filter, int max);

    /// <summary>The next PV number of the prefix; null when the prefix has no counter.</summary>
    Task<string?> NextPvAsync(IDbSession session, string prefix);

    /// <summary>Writes the voucher; its id.</summary>
    Task<int> InsertAsync(IDbSession session, string pvNo, PaymentOthersRequest request, string userCode);
}

/// <inheritdoc />
public sealed class PaymentOthersRepository : IPaymentOthersRepository
{
    private static string? Text(string? value) => FinanceLimits.Text(value);

    /// <inheritdoc />
    public Task<List<PaymentOthersRow>> SearchAsync(IDbSession session, PaymentOthersFilter f, int max) =>
        session.Connection.ProcQueryAsync<PaymentOthersRow>("genisis.PaymentOthers_Search", new
        {
            pvFrom = Text(f.PvFrom)?.ToUpperInvariant(), pvTo = Text(f.PvTo)?.ToUpperInvariant(), dateFrom = FinanceLimits.Day(f.DateFrom), dateTo = FinanceLimits.Day(f.DateTo),
            payTo = Text(f.PayTo), caseNo = Text(f.CaseNo)?.ToUpperInvariant(), max,
        }, session.Transaction);

    /// <inheritdoc />
    public Task<string?> NextPvAsync(IDbSession session, string prefix) => new PaymentToHospitalRepository().NextPvAsync(session, prefix);

    /// <inheritdoc />
    public Task<int> InsertAsync(IDbSession session, string pvNo, PaymentOthersRequest r, string userCode) =>
        session.Connection.ProcScalarAsync<int>("genisis.PaymentOthers_Insert", new
        {
            pvNo, pvDate = r.PvDate!.Value.Date, payTo = Text(r.PayTo)!.ToUpperInvariant(), amount = r.Amount, chequeNo = Text(r.ChequeNo)!.ToUpperInvariant(), chequeDate = FinanceLimits.Day(r.ChequeDate),
            payeeBank = Text(r.PayeeBank), payeeAccount = Text(r.PayeeAccount), caseNo = Text(r.CaseNo)?.ToUpperInvariant(), remark = Text(r.Remark), bankCharges = r.BankCharges ?? 0, userCode,
        }, session.Transaction);
}

/// <summary>Payment > Others (legacy <c>FrmPaymentOthers</c>; rebuilt: its source is not in the source tree).</summary>
public class PaymentOthersService(IDbSessions sessions, IPaymentOthersRepository repository, ILogger<PaymentOthersService> log)
{
    /// <summary>The vouchers that match. Nothing found is an empty list.</summary>
    public async Task<PaymentOthersResult> SearchAsync(PaymentOthersFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        var rows = await repository.SearchAsync(session, filter, FinanceLimits.FetchRows);
        var shown = rows.Take(FinanceLimits.MaxRows).ToList();
        return new PaymentOthersResult(shown, shown.Count, shown.Sum(r => r.Amount ?? 0), rows.Count > FinanceLimits.MaxRows);
    }

    /// <summary>Save: the next PV number of the prefix and the voucher, in one transaction.</summary>
    public async Task<PaymentOthersDone> CreateAsync(PaymentOthersRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        var pv = await repository.NextPvAsync(session, request.Prefix!) ?? throw new BusinessRuleException($"PV prefix {request.Prefix} has no counter.");
        var id = await repository.InsertAsync(session, pv, request, userCode);
        await session.CommitAsync();
        log.LogInformation("Payment (others) {PvNo} of {Amount} by {User}", pv, request.Amount, userCode);
        return new PaymentOthersDone(id, pv);
    }
}
