using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Finance;

namespace Genisis.Api.Receipt.Others;

/// <summary>A hospital to choose (code and name).</summary>
public record ReceiptHospital(string Code, string? Name);

/// <summary>The boxes of the Receipt (Others) search. A blank box is no criterion; the "empty" boxes list the receipts with no value.</summary>
public class ReceiptOthersFilter
{
    /// <summary>The hospital's code (the desktop chose the name).</summary>
    public string? Hospital { get; set; }

    /// <summary>Receipt No from.</summary>
    public string? NoFrom { get; set; }

    /// <summary>Receipt No to.</summary>
    public string? NoTo { get; set; }

    /// <summary>Only receipts with no receipt number.</summary>
    public bool BlankNo { get; set; }

    /// <summary>Receipt date from.</summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>Receipt date to.</summary>
    public DateTime? DateTo { get; set; }

    /// <summary>Only receipts with no receipt date.</summary>
    public bool BlankDate { get; set; }

    /// <summary>A/C period month from.</summary>
    public int? MonthFrom { get; set; }

    /// <summary>A/C period month to.</summary>
    public int? MonthTo { get; set; }

    /// <summary>Only receipts with no A/C period month.</summary>
    public bool BlankMonth { get; set; }

    /// <summary>A/C period year from.</summary>
    public int? YearFrom { get; set; }

    /// <summary>A/C period year to.</summary>
    public int? YearTo { get; set; }

    /// <summary>Only receipts with no A/C period year.</summary>
    public bool BlankYear { get; set; }
}

/// <summary>The search's checks: the A/C period lists the desktop offered (months 1-12, years 1999-3000) and the box lengths.</summary>
public sealed class ReceiptOthersFilterValidator : RequestValidator<ReceiptOthersFilter>
{
    /// <summary>Declares the rules.</summary>
    public ReceiptOthersFilterValidator()
    {
        RuleFor(f => f).Must(f => new[] { f.MonthFrom, f.MonthTo }.All(m => m is null or >= 1 and <= 12)).WithMessage("A/C Period Mth must be 1 to 12.");
        RuleFor(f => f).Must(f => new[] { f.YearFrom, f.YearTo }.All(y => y is null or >= 1999 and <= 3000)).WithMessage("A/C Period Yr must be 1999 to 3000.");
        RuleFor(f => f.NoFrom).MaximumLength(30).WithMessage("Receipt No is too long.");
        RuleFor(f => f.NoTo).MaximumLength(30).WithMessage("Receipt No is too long.");
        RuleFor(f => f.Hospital).MaximumLength(10).WithMessage("Select a valid hospital.");
    }
}

/// <summary>One receipt.</summary>
public record ReceiptOthersRow
{
    /// <summary>ReceiptIndex.</summary>
    public int Id { get; set; }

    /// <summary>The hospital's code (HOSName of the table).</summary>
    public string? HospitalCode { get; set; }

    /// <summary>The hospital's name; none when the code is not a hospital ("OTHER").</summary>
    public string? HospitalName { get; set; }

    /// <summary>Receipt number.</summary>
    public string? ReceiptNo { get; set; }

    /// <summary>Receipt date.</summary>
    public DateTime? ReceiptDate { get; set; }

    /// <summary>Receipt amount.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Bank charge.</summary>
    public decimal? BankCharge { get; set; }

    /// <summary>Cheque number.</summary>
    public string? ChequeNo { get; set; }

    /// <summary>Remarks.</summary>
    public string? Remarks { get; set; }

    /// <summary>A/C period month.</summary>
    public int? AccMonth { get; set; }

    /// <summary>A/C period year.</summary>
    public int? AccYear { get; set; }

    /// <summary>User who last changed it.</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>Day it was last changed.</summary>
    public DateTime? UpdatedOn { get; set; }
}

/// <summary>The receipts found, with their total and whether more than 5,000 matched.</summary>
public record ReceiptOthersResult(List<ReceiptOthersRow> Rows, decimal TotalAmount, bool Truncated);

/// <summary>A receipt to add.</summary>
public class ReceiptOthersRequest
{
    /// <summary>The hospital's code.</summary>
    public string? Hospital { get; set; }

    /// <summary>Receipt number.</summary>
    public string? ReceiptNo { get; set; }

    /// <summary>Receipt date.</summary>
    public DateTime? ReceiptDate { get; set; }

    /// <summary>Receipt amount.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Bank charge.</summary>
    public decimal? BankCharge { get; set; }

    /// <summary>Cheque number.</summary>
    public string? ChequeNo { get; set; }

    /// <summary>A/C period month.</summary>
    public int? AccMonth { get; set; }

    /// <summary>A/C period year.</summary>
    public int? AccYear { get; set; }

    /// <summary>Remarks.</summary>
    public string? Remarks { get; set; }
}

/// <summary>A receipt to change (the desktop's Edit, then Update); the receipt is the one in the route.</summary>
public class ReceiptOthersUpdateRequest : ReceiptOthersRequest;

internal static class ReceiptOthersRules
{
    public static bool Blank(string? v) => string.IsNullOrWhiteSpace(v);

    /// <summary>The checks both Save and Update make after their own required boxes: amounts, the A/C period lists and the box lengths.</summary>
    public static void Common<T>(AbstractValidator<T> v) where T : ReceiptOthersRequest
    {
        v.RuleFor(r => r.Amount).Must(a => a is null or >= 0).WithMessage("Receipt Amount cannot be negative.");
        v.RuleFor(r => r.BankCharge).Must(a => a is null or >= 0).WithMessage("Bank Charge cannot be negative.");
        v.RuleFor(r => r.AccMonth).Must(m => m is null or >= 1 and <= 12).WithMessage("A/C Period Mth must be 1 to 12.");
        v.RuleFor(r => r.AccYear).Must(y => y is null or >= 1999 and <= 3000).WithMessage("A/C Period Yr must be 1999 to 3000.");
        v.RuleFor(r => r.Hospital).MaximumLength(10).WithMessage("Select a valid hospital.");
        v.RuleFor(r => r.ReceiptNo).MaximumLength(30).WithMessage("Receipt No is too long.");
        v.RuleFor(r => r.ChequeNo).MaximumLength(40).WithMessage("Cheque No is too long.");
        v.RuleFor(r => r.Remarks).MaximumLength(100).WithMessage("Remarks is too long.");
    }
}

/// <summary>cmdSave_Click's checks, in the desktop's order.</summary>
public sealed class ReceiptOthersRequestValidator : RequestValidator<ReceiptOthersRequest>
{
    /// <summary>Declares the rules.</summary>
    public ReceiptOthersRequestValidator()
    {
        RuleFor(r => r.Hospital).Must(v => !ReceiptOthersRules.Blank(v)).WithMessage("Please Enter Hospital Code");
        RuleFor(r => r.ReceiptNo).Must(v => !ReceiptOthersRules.Blank(v)).WithMessage("Please Enter Receipt No");
        RuleFor(r => r.ReceiptDate).NotNull().WithMessage("Please Enter Receipt Date");
        RuleFor(r => r.Amount).NotNull().WithMessage("Please Enter Receipt Amount");
        RuleFor(r => r.ChequeNo).Must(v => !ReceiptOthersRules.Blank(v)).WithMessage("Please Enter Cheque No");
        ReceiptOthersRules.Common(this);
    }
}

/// <summary>cmdUpdate_Click's checks: its own messages, and the cheque number is asked for before the amount.</summary>
public sealed class ReceiptOthersUpdateRequestValidator : RequestValidator<ReceiptOthersUpdateRequest>
{
    /// <summary>Declares the rules.</summary>
    public ReceiptOthersUpdateRequestValidator()
    {
        RuleFor(r => r.Hospital).Must(v => !ReceiptOthersRules.Blank(v)).WithMessage("Please Enter Hospital Name!");
        RuleFor(r => r.ReceiptNo).Must(v => !ReceiptOthersRules.Blank(v)).WithMessage("Please Enter Receipt No!");
        RuleFor(r => r.ReceiptDate).NotNull().WithMessage("Please Enter Receipt Date");
        RuleFor(r => r.ChequeNo).Must(v => !ReceiptOthersRules.Blank(v)).WithMessage("Please Enter Cheque No!");
        RuleFor(r => r.Amount).NotNull().WithMessage("Please Enter Cheque Amt!");
        ReceiptOthersRules.Common(this);
    }
}

/// <summary>Reads and writes the receipts from hospitals and others.</summary>
public interface IReceiptOthersRepository
{
    /// <summary>The hospitals.</summary>
    Task<List<ReceiptHospital>> HospitalsAsync(IDbSession session);

    /// <summary>The receipts that match, at most <paramref name="max"/>.</summary>
    Task<List<ReceiptOthersRow>> SearchAsync(IDbSession session, ReceiptOthersFilter filter, int max);

    /// <summary>Adds a receipt; its ReceiptIndex.</summary>
    Task<int> AddAsync(IDbSession session, ReceiptOthersRequest request, string userCode, DateTime today);

    /// <summary>Changes receipt <paramref name="id"/>; the rows changed.</summary>
    Task<int> UpdateAsync(IDbSession session, int id, ReceiptOthersRequest request, string userCode, DateTime today);

    /// <summary>Deletes a receipt; the rows removed.</summary>
    Task<int> DeleteAsync(IDbSession session, int id);
}

/// <inheritdoc />
public sealed class ReceiptOthersRepository : IReceiptOthersRepository
{
    /// <inheritdoc />
    public Task<List<ReceiptHospital>> HospitalsAsync(IDbSession session) =>
        session.Connection.ProcQueryAsync<ReceiptHospital>("genisis.ReceiptOthers_Hospitals", null, session.Transaction);

    /// <inheritdoc />
    public Task<List<ReceiptOthersRow>> SearchAsync(IDbSession session, ReceiptOthersFilter f, int max) =>
        session.Connection.ProcQueryAsync<ReceiptOthersRow>("genisis.ReceiptOthers_Search", new
        {
            hospital = FinanceLimits.Text(f.Hospital), noFrom = FinanceLimits.Text(f.NoFrom), noTo = FinanceLimits.Text(f.NoTo), blankNo = f.BlankNo, dateFrom = FinanceLimits.Day(f.DateFrom),
            dateTo = FinanceLimits.Day(f.DateTo), blankDate = f.BlankDate, monthFrom = f.MonthFrom, monthTo = f.MonthTo, blankMonth = f.BlankMonth, yearFrom = f.YearFrom, yearTo = f.YearTo,
            blankYear = f.BlankYear, max,
        }, session.Transaction);

    private static object Values(ReceiptOthersRequest r, string userCode, DateTime today) => new
    {
        hospital = r.Hospital!.Trim(), receiptNo = r.ReceiptNo!.Trim(), receiptDate = r.ReceiptDate!.Value.Date, amount = r.Amount!.Value, bankCharge = r.BankCharge, chequeNo = r.ChequeNo!.Trim(),
        month = r.AccMonth, year = r.AccYear, remarks = r.Remarks?.Trim() ?? "", userCode, today = today.Date,
    };

    /// <inheritdoc />
    public Task<int> AddAsync(IDbSession session, ReceiptOthersRequest r, string userCode, DateTime today) =>
        session.Connection.ProcScalarAsync<int>("genisis.ReceiptOthers_Add", Values(r, userCode, today), session.Transaction);

    /// <inheritdoc />
    public Task<int> UpdateAsync(IDbSession session, int id, ReceiptOthersRequest r, string userCode, DateTime today)
    {
        var p = new Dapper.DynamicParameters(Values(r, userCode, today));
        p.Add("id", id);
        return session.Connection.ProcScalarAsync<int>("genisis.ReceiptOthers_Update", p, session.Transaction);
    }

    /// <inheritdoc />
    public Task<int> DeleteAsync(IDbSession session, int id) => session.Connection.ProcScalarAsync<int>("genisis.ReceiptOthers_Delete", new { id }, session.Transaction);
}

/// <summary>Receipt > Others (legacy <c>FrmReceiptHOS</c>, "Receipt (Others)").</summary>
public class ReceiptOthersService(IDbSessions sessions, IReceiptOthersRepository repository, ILogger<ReceiptOthersService> log)
{
    /// <summary>ComboListing: the hospitals.</summary>
    public async Task<List<ReceiptHospital>> HospitalsAsync()
    {
        await using var session = await sessions.OpenAsync();
        return await repository.HospitalsAsync(session);
    }

    /// <summary>cmdSearch_Click: the receipts that match and their total; nothing found is a <see cref="NotFoundException"/> ("No record found!").</summary>
    public async Task<ReceiptOthersResult> SearchAsync(ReceiptOthersFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        var rows = await repository.SearchAsync(session, filter, FinanceLimits.FetchRows);
        if (rows.Count == 0) throw new NotFoundException("No record found!");
        var shown = rows.Take(FinanceLimits.MaxRows).ToList();
        return new ReceiptOthersResult(shown, shown.Sum(r => r.Amount ?? 0m), rows.Count > FinanceLimits.MaxRows);
    }

    /// <summary>cmdSave_Click: adds a receipt. Returns its ReceiptIndex.</summary>
    public async Task<int> AddAsync(ReceiptOthersRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        var id = await repository.AddAsync(session, request, userCode, DateTime.Today);
        await session.CommitAsync();
        log.LogInformation("Receipt (Others) {Id} added by {User}", id, userCode);
        return id;
    }

    /// <summary>cmdUpdate_Click: changes receipt <paramref name="id"/>.</summary>
    public async Task UpdateAsync(int id, ReceiptOthersRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        if (await repository.UpdateAsync(session, id, request, userCode, DateTime.Today) == 0) throw new NotFoundException("Record not found.");
        await session.CommitAsync();
        log.LogInformation("Receipt (Others) {Id} updated by {User}", id, userCode);
    }

    /// <summary>CmdDelete_Click: deletes a receipt.</summary>
    public async Task DeleteAsync(int id, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        if (await repository.DeleteAsync(session, id) == 0) throw new NotFoundException("Record not found.");
        await session.CommitAsync();
        log.LogInformation("Receipt (Others) {Id} deleted by {User}", id, userCode);
    }
}
