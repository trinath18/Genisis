using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Finance.SuspendActive;

/// <summary>Fixed codes of Finance &gt; Suspend / Active (the PV statuses frmPostingPay2MBM lists).</summary>
public static class SuspendActiveCodes
{
    /// <summary>Active.</summary>
    public const string Active = "A";

    /// <summary>Cancelled (stopped cheque).</summary>
    public const string Cancel = "C";

    /// <summary>Expired cheque.</summary>
    public const string Expired = "E";

    /// <summary>Re-issued.</summary>
    public const string Reissue = "R";

    /// <summary>The status list.</summary>
    public static readonly CodeName[] Statuses = [new(Active, "A - Active"), new(Cancel, "C - Cancel"), new(Expired, "E - Expired"), new(Reissue, "R - Reissue")];

    /// <summary>How a stop can be recorded.</summary>
    public static readonly CodeName[] StopStatuses = [new(Cancel, "C - Cancel"), new(Expired, "E - Expired")];

    /// <summary>Payment modes of a re-issue.</summary>
    public static readonly CodeName[] PaymentModes = [new("ONLINE", "ONLINE"), new("CHEQUE", "CHEQUE")];
}

/// <summary>The drop-down lists.</summary>
/// <param name="Statuses">All PV statuses.</param>
/// <param name="StopStatuses">C / E.</param>
/// <param name="PaymentModes">ONLINE / CHEQUE.</param>
public record SuspendActiveLookups(IReadOnlyList<CodeName> Statuses, IReadOnlyList<CodeName> StopStatuses, IReadOnlyList<CodeName> PaymentModes);

/// <summary>One member payment voucher row (Payment2MBM).</summary>
public record VoucherRow
{
    /// <summary>Payment2MBM.PVMBMIndex.</summary>
    public int Id { get; set; }

    /// <summary>PV number.</summary>
    public string? PvNo { get; set; }

    /// <summary>PV date.</summary>
    public DateTime? PvDate { get; set; }

    /// <summary>Amount (negative on a C / E row).</summary>
    public decimal? Amount { get; set; }

    /// <summary>Cheque number or transfer reference.</summary>
    public string? ChequeNo { get; set; }

    /// <summary>Cheque date.</summary>
    public DateTime? ChequeDate { get; set; }

    /// <summary>PV status: A, C, E, R.</summary>
    public string? Status { get; set; }

    /// <summary>Cheque printed (Y).</summary>
    public string? ChequeStatus { get; set; }

    /// <summary>ONLINE / CHEQUE.</summary>
    public string? PaymentMode { get; set; }

    /// <summary>Payee.</summary>
    public string? PayTo { get; set; }

    /// <summary>Payee bank.</summary>
    public string? PayeeBank { get; set; }

    /// <summary>Payee account, masked to the last 4 digits.</summary>
    public string? PayeeAccount { get; set; }

    /// <summary>Case number.</summary>
    public string? CaseNo { get; set; }

    /// <summary>Claim version.</summary>
    public string? ClaimVersion { get; set; }

    /// <summary>Remark.</summary>
    public string? Remark { get; set; }

    /// <summary>Last updated by.</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>Last updated on.</summary>
    public DateTime? UpdatedOn { get; set; }

    /// <summary>C or E when this voucher has been stopped.</summary>
    public string? StoppedAs { get; set; }

    /// <summary>Date it was stopped.</summary>
    public DateTime? StoppedOn { get; set; }

    /// <summary>The PV number of the re-issue that replaced it.</summary>
    public string? ReissuedAs { get; set; }
}

/// <summary>A search result.</summary>
/// <param name="Rows">Vouchers (at most <see cref="FinanceLimits.MaxRows"/>).</param>
/// <param name="Truncated">True when more matched.</param>
public record VoucherSearchResult(List<VoucherRow> Rows, bool Truncated);

/// <summary>The search criteria; blank = no criterion.</summary>
public class VoucherFilter
{
    /// <summary>PV number from.</summary>
    public string? PvNoFrom { get; set; }

    /// <summary>PV number to.</summary>
    public string? PvNoTo { get; set; }

    /// <summary>PV date from.</summary>
    public DateTime? PvDateFrom { get; set; }

    /// <summary>PV date to.</summary>
    public DateTime? PvDateTo { get; set; }

    /// <summary>Case number.</summary>
    public string? CaseNo { get; set; }

    /// <summary>Cheque number / transfer reference.</summary>
    public string? ChequeNo { get; set; }

    /// <summary>Text in the payee name.</summary>
    public string? Payee { get; set; }

    /// <summary>PV status.</summary>
    public string? Status { get; set; }
}

/// <summary>Stop a voucher: cancel (stopped cheque) or expired.</summary>
public class StopVoucherRequest
{
    /// <summary>C or E.</summary>
    public string? Status { get; set; }

    /// <summary>Date of the action.</summary>
    public DateTime? ActionDate { get; set; }

    /// <summary>Remark (the voucher's remark is kept when blank).</summary>
    public string? Remark { get; set; }
}

/// <summary>Re-issue a stopped voucher.</summary>
public class ReissueVoucherRequest
{
    /// <summary>Date of the re-issue.</summary>
    public DateTime? ActionDate { get; set; }

    /// <summary>New cheque number or transfer reference.</summary>
    public string? ChequeNo { get; set; }

    /// <summary>ONLINE or CHEQUE.</summary>
    public string? PaymentMode { get; set; }

    /// <summary>Payee bank (the voucher's when blank).</summary>
    public string? PayeeBank { get; set; }

    /// <summary>Payee account (the voucher's when blank).</summary>
    public string? PayeeAccount { get; set; }

    /// <summary>Remark, e.g. "STOP CHQ AND RE-ISSUE AND TT TO ...".</summary>
    public string? Remark { get; set; }
}

/// <summary>Search checks.</summary>
public sealed class VoucherFilterValidator : RequestValidator<VoucherFilter>
{
    /// <summary>Declares the rules.</summary>
    public VoucherFilterValidator()
    {
        RuleFor(f => f).Must(f => new[] { f.PvNoFrom, f.PvNoTo, f.CaseNo, f.ChequeNo, f.Payee, f.Status }.Any(v => !string.IsNullOrWhiteSpace(v))
                                  || f.PvDateFrom is not null || f.PvDateTo is not null)
            .WithMessage("Please enter at least one search criterion.");
        RuleFor(f => f.Status).Must(s => FinanceLimits.IsBlankOr(s, "A", "C", "E", "R")).WithMessage("Select a valid PV status.");
    }
}

/// <summary>Stop checks.</summary>
public sealed class StopVoucherRequestValidator : RequestValidator<StopVoucherRequest>
{
    /// <summary>Declares the rules.</summary>
    public StopVoucherRequestValidator()
    {
        RuleFor(r => r.Status).Must(s => s?.Trim() is SuspendActiveCodes.Cancel or SuspendActiveCodes.Expired).WithMessage("Please select Cancel or Expired.");
        RuleFor(r => r.ActionDate).NotNull().WithMessage("Please Enter Date");
        RuleFor(r => r.Remark).MaximumLength(400).WithMessage("Remark is longer than 400 characters.");
    }
}

/// <summary>Re-issue checks.</summary>
public sealed class ReissueVoucherRequestValidator : RequestValidator<ReissueVoucherRequest>
{
    /// <summary>Declares the rules.</summary>
    public ReissueVoucherRequestValidator()
    {
        RuleFor(r => r.ActionDate).NotNull().WithMessage("Please Enter Date");
        RuleFor(r => r.ChequeNo).NotEmpty().WithMessage("Please Enter Cheque No / TT Reference");
        RuleFor(r => r.ChequeNo).MaximumLength(40).WithMessage("Cheque No is longer than 40 characters.");
        RuleFor(r => r.PaymentMode).Must(m => SuspendActiveCodes.PaymentModes.Any(p => p.Code == m?.Trim())).WithMessage("Select a valid payment mode.");
        RuleFor(r => r.Remark).NotEmpty().WithMessage("Please Enter Remark");
        RuleFor(r => r.Remark).MaximumLength(400).WithMessage("Remark is longer than 400 characters.");
        RuleFor(r => r.PayeeBank).MaximumLength(200).WithMessage("Bank is longer than 200 characters.");
        RuleFor(r => r.PayeeAccount).MaximumLength(60).WithMessage("Account No is longer than 60 characters.");
    }
}

/// <summary>Reads and writes member payment vouchers.</summary>
public interface ISuspendActiveRepository
{
    /// <summary>The vouchers that match (at most <see cref="FinanceLimits.FetchRows"/>).</summary>
    Task<List<VoucherRow>> SearchAsync(IDbSession session, VoucherFilter filter);

    /// <summary>Stops a voucher; returns a <see cref="StoredProcedures"/> result code.</summary>
    Task<int> StopAsync(IDbSession session, int id, StopVoucherRequest request, string userCode);

    /// <summary>Re-issues a stopped voucher; returns the result code and the new PV number.</summary>
    Task<(int Result, string? PvNo)> ReissueAsync(IDbSession session, int id, ReissueVoucherRequest request, string userCode);
}

/// <inheritdoc />
public sealed class SuspendActiveRepository : ISuspendActiveRepository
{
    /// <inheritdoc />
    public Task<List<VoucherRow>> SearchAsync(IDbSession session, VoucherFilter f) =>
        session.Connection.ProcQueryAsync<VoucherRow>("genisis.SuspendActive_Search", new
        {
            pvFrom = FinanceLimits.Text(f.PvNoFrom), pvTo = FinanceLimits.Text(f.PvNoTo), dateFrom = FinanceLimits.Day(f.PvDateFrom), dateTo = FinanceLimits.Day(f.PvDateTo),
            caseNo = FinanceLimits.Text(f.CaseNo), chequeNo = FinanceLimits.Text(f.ChequeNo), payee = FinanceLimits.Text(f.Payee), status = FinanceLimits.Text(f.Status),
            max = FinanceLimits.FetchRows,
        }, session.Transaction);

    /// <inheritdoc />
    public async Task<int> StopAsync(IDbSession session, int id, StopVoucherRequest r, string userCode) =>
        await session.Connection.ProcScalarAsync<int>("genisis.SuspendActive_Stop",
            new { id, status = r.Status!.Trim(), actionDate = r.ActionDate!.Value.Date, remark = FinanceLimits.Text(r.Remark), userCode }, session.Transaction);

    /// <inheritdoc />
    public Task<(int Result, string? PvNo)> ReissueAsync(IDbSession session, int id, ReissueVoucherRequest r, string userCode) =>
        session.Connection.ProcSingleAsync<(int Result, string? PvNo)>("genisis.SuspendActive_Reissue", new
        {
            id, actionDate = r.ActionDate!.Value.Date, chequeNo = r.ChequeNo!.Trim(), paymentMode = r.PaymentMode!.Trim(), payeeBank = FinanceLimits.Text(r.PayeeBank),
            payeeAccount = FinanceLimits.Text(r.PayeeAccount), remark = r.Remark!.Trim(), userCode,
        }, session.Transaction);
}

/// <summary>
/// Finance &gt; Suspend / Active (legacy frmPaymentMBMReissue, source not available): stop a member payment (cancel / expired cheque) and
/// re-issue it, as the rows in Payment2MBM show it was done.
/// </summary>
public class SuspendActiveService(IDbSessions sessions, ISuspendActiveRepository repository, ILogger<SuspendActiveService> log)
{
    /// <summary>The drop-down lists.</summary>
    public static SuspendActiveLookups Lookups() => new(SuspendActiveCodes.Statuses, SuspendActiveCodes.StopStatuses, SuspendActiveCodes.PaymentModes);

    /// <summary>Why a voucher cannot be stopped (null when it can).</summary>
    public static string? StopProblem(VoucherRow v) =>
        v.Status?.Trim() is not (SuspendActiveCodes.Active or SuspendActiveCodes.Reissue) || (v.Amount ?? 0m) <= 0
            ? $"PV {v.PvNo} is not an active payment, so it cannot be stopped."
            : !string.IsNullOrWhiteSpace(v.StoppedAs) ? $"PV {v.PvNo} has already been stopped ({v.StoppedAs})." : null;

    /// <summary>Why a voucher cannot be re-issued (null when it can).</summary>
    public static string? ReissueProblem(VoucherRow v) =>
        string.IsNullOrWhiteSpace(v.StoppedAs) ? $"PV {v.PvNo} has not been stopped. Cancel or expire it first."
        : !string.IsNullOrWhiteSpace(v.ReissuedAs) ? $"PV {v.PvNo} has already been re-issued as {v.ReissuedAs}." : null;

    /// <summary>The vouchers that match, newest first.</summary>
    public async Task<VoucherSearchResult> SearchAsync(VoucherFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        var rows = await repository.SearchAsync(session, filter);
        var truncated = rows.Count > FinanceLimits.MaxRows;
        return new VoucherSearchResult(truncated ? rows.Take(FinanceLimits.MaxRows).ToList() : rows, truncated);
    }

    /// <summary>Stops voucher <paramref name="id"/> (a C / E row with the negative amount). Throws when it cannot be stopped.</summary>
    public async Task<string> StopAsync(int id, StopVoucherRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        var result = await repository.StopAsync(session, id, request, userCode);
        if (result == StoredProcedures.NotFound) throw new NotFoundException("No record found!");
        if (result == StoredProcedures.Conflict) throw new FinanceException("This payment is not active or has already been stopped. Search again.");
        await session.CommitAsync();
        log.LogInformation("Member payment voucher {Id} stopped as {Status} by {User}", id, request.Status, userCode);
        return request.Status!.Trim() == SuspendActiveCodes.Cancel ? "Payment cancelled." : "Payment marked as expired.";
    }

    /// <summary>Re-issues stopped voucher <paramref name="id"/> under a new PV number. Throws when it cannot be re-issued.</summary>
    public async Task<(string PvNo, string Message)> ReissueAsync(int id, ReissueVoucherRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        var (result, pvNo) = await repository.ReissueAsync(session, id, request, userCode);
        if (result == StoredProcedures.NotFound) throw new NotFoundException("No record found!");
        if (result == StoredProcedures.Conflict || pvNo is null) throw new FinanceException("This payment has not been stopped or has already been re-issued. Search again.");
        await session.CommitAsync();
        log.LogInformation("Member payment voucher {Id} re-issued as {PvNo} by {User}", id, pvNo, userCode);
        return (pvNo, $"Payment re-issued as PV {pvNo}.");
    }
}
