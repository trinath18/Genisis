using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Finance;
using Genisis.Api.Payment.ToHospital;

namespace Genisis.Api.Payment.PvUpdate;

/// <summary>The criteria of the PVs listed.</summary>
public class PvUpdateFilter
{
    /// <summary>The sources: hospital (Payment2HOS) and member (Payment2MBM).</summary>
    public static readonly string[] Sources = ["hospital", "member"];

    /// <summary>hospital or member.</summary>
    public string? Source { get; set; } = "hospital";

    /// <summary>PV number (exact).</summary>
    public string? PvNo { get; set; }

    /// <summary>A case on the PV.</summary>
    public string? CaseNo { get; set; }

    /// <summary>PV date from.</summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>PV date to.</summary>
    public DateTime? DateTo { get; set; }
}

/// <summary>The source has to be one the screen offers and the dates in order.</summary>
public sealed class PvUpdateFilterValidator : RequestValidator<PvUpdateFilter>
{
    /// <summary>Declares the rules.</summary>
    public PvUpdateFilterValidator()
    {
        RuleFor(f => f.Source).Must(s => s is not null && PvUpdateFilter.Sources.Contains(s)).WithMessage("Select To Hospital or To Member.");
        RuleFor(f => f).Must(f => f.DateFrom is null || f.DateTo is null || f.DateFrom <= f.DateTo).WithMessage("PV Date From cannot be later than PV Date To.");
    }
}

/// <summary>A PV found.</summary>
public record PvUpdateListRow
{
    /// <summary>PV number.</summary>
    public string PvNo { get; set; } = "";

    /// <summary>PV date.</summary>
    public DateTime? PvDate { get; set; }

    /// <summary>Payee.</summary>
    public string? PayTo { get; set; }

    /// <summary>PV total.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Number of claims on the PV.</summary>
    public int Claims { get; set; }

    /// <summary>Cheque number.</summary>
    public string? ChequeNo { get; set; }

    /// <summary>Payment mode.</summary>
    public string? PaymentMode { get; set; }

    /// <summary>PV status.</summary>
    public string? Status { get; set; }

    /// <summary>SUN PV number, once posted.</summary>
    public string? SunNo { get; set; }
}

/// <summary>The PVs found; <see cref="Truncated"/> when more than 5,000 matched.</summary>
public record PvUpdateListResult(List<PvUpdateListRow> Rows, bool Truncated);

/// <summary>A row of a PV, with the PV's details.</summary>
public record PvUpdateRow
{
    /// <summary>PVHOSIndex / PVMBMIndex.</summary>
    public int Id { get; set; }

    /// <summary>PV number.</summary>
    public string? PvNo { get; set; }

    /// <summary>PV date.</summary>
    public DateTime? PvDate { get; set; }

    /// <summary>Case number.</summary>
    public string? CaseNo { get; set; }

    /// <summary>Claim version.</summary>
    public string? ClaimVersion { get; set; }

    /// <summary>The claim's amount on the PV.</summary>
    public decimal? Paid { get; set; }

    /// <summary>PV total.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Cheque number.</summary>
    public string? ChequeNo { get; set; }

    /// <summary>Cheque date.</summary>
    public DateTime? ChequeDate { get; set; }

    /// <summary>Payment mode.</summary>
    public string? PaymentMode { get; set; }

    /// <summary>Payee.</summary>
    public string? PayTo { get; set; }

    /// <summary>Bank.</summary>
    public string? Bank { get; set; }

    /// <summary>Account number.</summary>
    public string? Account { get; set; }

    /// <summary>Remark.</summary>
    public string? Remark { get; set; }

    /// <summary>PV status.</summary>
    public string? Status { get; set; }

    /// <summary>SUN PV number, once posted.</summary>
    public string? SunNo { get; set; }

    /// <summary>Accounting period month.</summary>
    public string? AccMonth { get; set; }

    /// <summary>Accounting period year.</summary>
    public string? AccYear { get; set; }
}

/// <summary>A PV's rows.</summary>
public record PvUpdateDetail(string Source, string PvNo, List<PvUpdateRow> Rows);

/// <summary>The details to save on every row of a PV.</summary>
public class PvUpdateRequest
{
    /// <summary>hospital or member.</summary>
    public string? Source { get; set; }

    /// <summary>PV number.</summary>
    public string? PvNo { get; set; }

    /// <summary>Cheque number (the date as ddMMyyyy for an online payment).</summary>
    public string? ChequeNo { get; set; }

    /// <summary>Cheque date.</summary>
    public DateTime? ChequeDate { get; set; }

    /// <summary>ONLINE or CHEQUE.</summary>
    public string? PaymentMode { get; set; }

    /// <summary>Payee (member PVs only).</summary>
    public string? PayTo { get; set; }

    /// <summary>Bank.</summary>
    public string? Bank { get; set; }

    /// <summary>Account number.</summary>
    public string? Account { get; set; }

    /// <summary>Remark.</summary>
    public string? Remark { get; set; }
}

/// <summary>Save's checks.</summary>
public sealed class PvUpdateRequestValidator : RequestValidator<PvUpdateRequest>
{
    /// <summary>Declares the rules.</summary>
    public PvUpdateRequestValidator()
    {
        RuleFor(r => r.Source).Must(s => s is not null && PvUpdateFilter.Sources.Contains(s)).WithMessage("Select To Hospital or To Member.");
        RuleFor(r => r.PvNo).Must(p => !string.IsNullOrWhiteSpace(p)).WithMessage("Please select the PV.");
        RuleFor(r => r.PaymentMode).Must(m => m is not null && PaymentToHospitalRequest.Modes.Contains(m)).WithMessage("Payment Mode is ONLINE or CHEQUE.");
        RuleFor(r => r.ChequeNo).Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Please enter the Cheque No.").MaximumLength(40).WithMessage("The Cheque No is too long.");
        RuleFor(r => r.ChequeDate).NotNull().WithMessage("Please enter the Cheque Date.");
        RuleFor(r => r.PayTo).MaximumLength(120).WithMessage("Pay To is too long (120 characters at most).");
        RuleFor(r => r.Bank).MaximumLength(100).WithMessage("The bank is too long (100 characters at most).");
        RuleFor(r => r.Account).MaximumLength(60).WithMessage("The account number is too long (60 characters at most).");
        RuleFor(r => r.Remark).MaximumLength(400).WithMessage("The remark is too long (400 characters at most).");
    }
}

/// <summary>How many rows Save changed.</summary>
public record PvUpdateDone(int Rows);

/// <summary>Reads and updates the PVs.</summary>
public interface IPvUpdateRepository
{
    /// <summary>The PVs that match, at most <paramref name="max"/>.</summary>
    Task<List<PvUpdateListRow>> FindAsync(IDbSession session, PvUpdateFilter filter, int max);

    /// <summary>The rows of a PV.</summary>
    Task<List<PvUpdateRow>> RowsAsync(IDbSession session, string source, string pvNo);

    /// <summary>Saves the details on every row: (0 saved, 1 no PV, 2 posted to SUN, 3 stopped cheque; rows changed).</summary>
    Task<(int Result, int Rows)> SaveAsync(IDbSession session, PvUpdateRequest request, string userCode);
}

/// <inheritdoc />
public sealed class PvUpdateRepository : IPvUpdateRepository
{
    private sealed record Saved(int Result, int Rows);

    private static string? Upper(string? value) => FinanceLimits.Text(value)?.ToUpperInvariant();

    /// <inheritdoc />
    public Task<List<PvUpdateListRow>> FindAsync(IDbSession session, PvUpdateFilter f, int max) =>
        session.Connection.ProcQueryAsync<PvUpdateListRow>("genisis.PvUpdate_Find", new
        {
            source = f.Source, pvNo = Upper(f.PvNo), caseNo = Upper(f.CaseNo), dateFrom = FinanceLimits.Day(f.DateFrom), dateTo = FinanceLimits.Day(f.DateTo), max,
        }, session.Transaction);

    /// <inheritdoc />
    public Task<List<PvUpdateRow>> RowsAsync(IDbSession session, string source, string pvNo) =>
        session.Connection.ProcQueryAsync<PvUpdateRow>("genisis.PvUpdate_Rows", new { source, pvNo }, session.Transaction);

    /// <inheritdoc />
    public async Task<(int Result, int Rows)> SaveAsync(IDbSession session, PvUpdateRequest r, string userCode)
    {
        var saved = await session.Connection.ProcFirstOrDefaultAsync<Saved>("genisis.PvUpdate_Save", new
        {
            source = r.Source, pvNo = Upper(r.PvNo), chequeNo = Upper(r.ChequeNo), chequeDate = FinanceLimits.Day(r.ChequeDate), paymentMode = r.PaymentMode,
            payTo = r.Source == "member" ? Upper(r.PayTo) : null, bank = Upper(r.Bank), account = FinanceLimits.Text(r.Account), remark = FinanceLimits.Text(r.Remark), userCode,
        }, session.Transaction);
        return (saved?.Result ?? 1, saved?.Rows ?? 0);
    }
}

/// <summary>Payment > PV Update (Manual) (legacy <c>FrmPaymentManual</c>; rebuilt: its source is not in the source tree).</summary>
public class PvUpdateService(IDbSessions sessions, IPvUpdateRepository repository, ILogger<PvUpdateService> log)
{
    /// <summary>The PVs that match. Nothing found is an empty list.</summary>
    public async Task<PvUpdateListResult> FindAsync(PvUpdateFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        var rows = await repository.FindAsync(session, filter, FinanceLimits.FetchRows);
        return new PvUpdateListResult(rows.Take(FinanceLimits.MaxRows).ToList(), rows.Count > FinanceLimits.MaxRows);
    }

    /// <summary>The rows of a PV; an unknown PV is a <see cref="NotFoundException"/>.</summary>
    public async Task<PvUpdateDetail> RowsAsync(string source, string pvNo)
    {
        if (!PvUpdateFilter.Sources.Contains(source)) throw new BusinessRuleException("Select To Hospital or To Member.");
        var pv = pvNo.Trim().ToUpperInvariant();
        await using var session = await sessions.OpenAsync();
        var rows = await repository.RowsAsync(session, source, pv);
        if (rows.Count == 0) throw new NotFoundException($"PV {pv} not found.");
        return new PvUpdateDetail(source, pv, rows);
    }

    /// <summary>Save: the details on every row of the PV, in one transaction.</summary>
    public async Task<PvUpdateDone> SaveAsync(PvUpdateRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        var (result, rows) = await repository.SaveAsync(session, request, userCode);
        switch (result)
        {
            case 1: throw new NotFoundException($"PV {request.PvNo} not found.");
            case 2: throw new BusinessRuleException($"PV {request.PvNo} has been posted to SUN and cannot be changed.");
            case 3: throw new BusinessRuleException($"PV {request.PvNo} has been stopped (cancelled or expired) and cannot be changed.");
        }

        await session.CommitAsync();
        log.LogInformation("PV update {PvNo} ({Source}): {Rows} row(s) by {User}", request.PvNo, request.Source, rows, userCode);
        return new PvUpdateDone(rows);
    }
}
