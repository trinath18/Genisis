using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;

namespace Genisis.Api.Invoice;

/// <summary>One invoice paid to a member, as a row of the Invoice MBM Accounting list.</summary>
public record MemberAccountingRow
{
    /// <summary>Who is paid (the invoice's payee).</summary>
    public string? PayTo { get; set; }

    /// <summary>Who the invoice is paid to: M member, H hospital.</summary>
    public string? PayToCode { get; set; }

    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Worksheet version.</summary>
    public string Version { get; set; } = "";

    /// <summary>The date the worksheet was closed ("Claim Reg Date").</summary>
    public DateTime? ClaimCloseDate { get; set; }

    /// <summary>The debit/credit reference ("Main DCN").</summary>
    public string? DebitCreditRefNo { get; set; }

    /// <summary>Bordx number.</summary>
    public string? BordxNo { get; set; }

    /// <summary>Member number.</summary>
    public string? MemberNo { get; set; }

    /// <summary>Member name.</summary>
    public string? MemberName { get; set; }

    /// <summary>Patient name.</summary>
    public string? PatientName { get; set; }

    /// <summary>Payable to the member (<c>CLMPayableByMedix2M</c>).</summary>
    public decimal? Payable { get; set; }

    /// <summary>The amount printed before (<c>INVPreviousAmt</c>).</summary>
    public decimal? PreviousAmount { get; set; }

    /// <summary>The bordx date.</summary>
    public DateTime? BordxDate { get; set; }

    /// <summary>The batch the invoice was printed in: M, the day (ddmmyyyy).</summary>
    public string? BatchDate { get; set; }

    /// <summary>The number of the batch within its day.</summary>
    public int? BatchNo { get; set; }

    /// <summary>The plan code.</summary>
    public string? PlanCode { get; set; }

    /// <summary>What the worksheet's invoices add up to.</summary>
    public decimal? TotalActual { get; set; }

    /// <summary>The DR column.</summary>
    public decimal? Debit { get; set; }

    /// <summary>The CR column.</summary>
    public decimal? Credit { get; set; }
}

/// <summary>The criteria of the Invoice MBM Accounting search: every "from"/"to" pair is inclusive and ignored when empty.</summary>
public class MemberAccountingFilter
{
    /// <summary>Print Status: N new (not printed yet) or R reprint (printed). Required.</summary>
    public string? PrintStatus { get; set; } = "N";

    /// <summary>The worksheet's close date from.</summary>
    public DateTime? CloseDateFrom { get; set; }

    /// <summary>The worksheet's close date to.</summary>
    public DateTime? CloseDateTo { get; set; }

    /// <summary>Payor: the first two letters of the case number.</summary>
    public string? Payor { get; set; }

    /// <summary>Batch day from.</summary>
    public DateTime? BatchDateFrom { get; set; }

    /// <summary>Batch day to.</summary>
    public DateTime? BatchDateTo { get; set; }

    /// <summary>Batch number from.</summary>
    public int? BatchNoFrom { get; set; }

    /// <summary>Batch number to.</summary>
    public int? BatchNoTo { get; set; }

    /// <summary>Bordx date from.</summary>
    public DateTime? BordxDateFrom { get; set; }

    /// <summary>Bordx date to.</summary>
    public DateTime? BordxDateTo { get; set; }
}

/// <summary>One worksheet version whose member-paid invoices are to be given an accounting period.</summary>
/// <param name="CaseNo">Case number.</param>
/// <param name="Version">Worksheet version.</param>
public record AccountingPeriodItem(string CaseNo, string Version);

/// <summary>cmdUpdate_Click: the ticked worksheet versions and the accounting month and year to give their member-paid invoices.</summary>
public class AccountingPeriodRequest
{
    /// <summary>"A/C Period Mth", 1 to 12.</summary>
    public int? Month { get; set; }

    /// <summary>"A/C Period Yr".</summary>
    public int? Year { get; set; }

    /// <summary>The ticked worksheet versions.</summary>
    public List<AccountingPeriodItem> Items { get; set; } = [];
}

/// <summary>The checks of the Invoice MBM Accounting search and print (the print status is required).</summary>
public sealed class MemberAccountingFilterValidator : RequestValidator<MemberAccountingFilter>
{
    /// <summary>Declares the rules.</summary>
    public MemberAccountingFilterValidator() =>
        RuleFor(f => f.PrintStatus).Must(InvoiceAccountingRules.IsPrintStatus).WithMessage(InvoiceAccountingRules.NoPrintStatus);
}

/// <summary><c>cmdUpdate_Click</c>'s checks, in the desktop's order (the empty list of the desktop's first check is the caller's).</summary>
public sealed class AccountingPeriodRequestValidator : RequestValidator<AccountingPeriodRequest>
{
    /// <summary>Declares the rules.</summary>
    public AccountingPeriodRequestValidator()
    {
        RuleFor(r => r.Month).Must(m => m is >= 1 and <= 12).WithMessage("Month cannot be Empty.");
        RuleFor(r => r.Year).Must(y => y is >= 1900 and <= 2999).WithMessage("Year cannot be empty!");
        RuleFor(r => r.Items).Must(i => i.Count > 0).WithMessage("Please select at least one record to be updated! ");
    }
}

/// <summary>Reads the invoices paid to members, marks them as printed and sets their accounting period.</summary>
public interface IMemberAccountingRepository
{
    /// <summary>The invoices that match the filter (at most <paramref name="max"/>).</summary>
    Task<List<MemberAccountingRow>> SearchAsync(IDbSession session, MemberAccountingFilter filter, int max);

    /// <summary>The next batch label of the day.</summary>
    Task<string> NextBatchAsync(IDbSession session, DateOnly day);

    /// <summary>Marks the member's invoices of the worksheet versions the filter lists as printed in a batch; returns how many.</summary>
    Task<int> PrintAsync(IDbSession session, MemberAccountingFilter filter, string batch, string userCode);

    /// <summary>Gives the member-paid invoices of a worksheet version an accounting month and year; returns how many invoices were changed.</summary>
    Task<int> SetPeriodAsync(IDbSession session, AccountingPeriodItem item, int month, int year, string userCode);
}

/// <inheritdoc />
public sealed class MemberAccountingRepository : IMemberAccountingRepository
{
    private static string? Payor(MemberAccountingFilter f) => WorksheetLimits.Text(f.Payor) is { } p ? p[..Math.Min(2, p.Length)] : null;

    /// <inheritdoc />
    public async Task<List<MemberAccountingRow>> SearchAsync(IDbSession session, MemberAccountingFilter f, int max)
    {
        var rows = await session.Connection.ProcQueryAsync<MemberAccountingRow>("genisis.InvoiceMemberAccounting_Search", new
        {
            reprint = InvoiceAccountingRules.IsReprint(f.PrintStatus), closeFrom = f.CloseDateFrom?.Date, closeTo = f.CloseDateTo?.Date, payor = Payor(f),
            batchDateFrom = f.BatchDateFrom?.Date, batchDateTo = f.BatchDateTo?.Date, batchNoFrom = f.BatchNoFrom, batchNoTo = f.BatchNoTo,
            bordxFrom = f.BordxDateFrom?.Date, bordxTo = f.BordxDateTo?.Date, max,
        }, session.Transaction);
        foreach (var row in rows) (row.Debit, row.Credit) = InvoiceAccountingRules.DebitCredit(row.PreviousAmount, row.Payable);
        return rows;
    }

    /// <inheritdoc />
    public async Task<string> NextBatchAsync(IDbSession session, DateOnly day) =>
        await session.Connection.ProcScalarAsync<string>("genisis.InvoiceAccounting_NextBatch", new { kind = "M", day = day.ToDateTime(TimeOnly.MinValue) }, session.Transaction) ?? "";

    /// <inheritdoc />
    public async Task<int> PrintAsync(IDbSession session, MemberAccountingFilter f, string batch, string userCode) =>
        await session.Connection.ProcScalarAsync<int>("genisis.InvoiceMemberAccounting_Print", new
        {
            batch, user = userCode, closeFrom = f.CloseDateFrom?.Date, closeTo = f.CloseDateTo?.Date, payor = Payor(f), batchDateFrom = f.BatchDateFrom?.Date, batchDateTo = f.BatchDateTo?.Date,
            batchNoFrom = f.BatchNoFrom, batchNoTo = f.BatchNoTo, bordxFrom = f.BordxDateFrom?.Date, bordxTo = f.BordxDateTo?.Date,
        }, session.Transaction);

    /// <inheritdoc />
    public async Task<int> SetPeriodAsync(IDbSession session, AccountingPeriodItem item, int month, int year, string userCode) =>
        await session.Connection.ProcScalarAsync<int>("genisis.InvoiceMemberAccounting_SetPeriod",
            new { cas = item.CaseNo.Trim(), version = item.Version.Trim(), month, year, user = userCode }, session.Transaction);
}

/// <summary>Invoice > Invoice MBM Accounting (legacy FrmInvoiceMBMAC, "Member Invoice Accounting").</summary>
public class MemberAccountingService(IDbSessions sessions, IMemberAccountingRepository repository, TimeProvider clock, ILogger<MemberAccountingService> log)
{
    /// <summary>CmdSearch_Click: the invoices that match the criteria, cut off at <see cref="InvoiceAccountingRules.MaxRows"/>.</summary>
    public async Task<SearchResult<MemberAccountingRow>> SearchAsync(MemberAccountingFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        var rows = await repository.SearchAsync(session, filter, InvoiceAccountingRules.MaxRows + 1);
        return rows.Count > InvoiceAccountingRules.MaxRows ? new SearchResult<MemberAccountingRow>(rows.Take(InvoiceAccountingRules.MaxRows).ToList(), true) : new SearchResult<MemberAccountingRow>(rows, false);
    }

    /// <summary>
    /// CmdPrintFile_Click: for "N - New" takes the next batch of the day and marks the invoices the criteria list as printed in it, all in one transaction; for "R - Reprint" marks nothing.
    /// A search that matches more than the list can hold is refused, so a print never marks invoices that were not listed.
    /// </summary>
    public async Task<AccountingPrintResult> PrintAsync(MemberAccountingFilter filter, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        var rows = await repository.SearchAsync(session, filter, InvoiceAccountingRules.MaxRows + 1);
        if (rows.Count == 0) throw new BusinessRuleException(InvoiceAccountingRules.NothingListed);
        if (rows.Count > InvoiceAccountingRules.MaxRows)
            throw new BusinessRuleException($"More than {InvoiceAccountingRules.MaxRows:N0} invoices match: narrow the search, so that what is printed is what is listed.");
        if (InvoiceAccountingRules.IsReprint(filter.PrintStatus)) return new AccountingPrintResult(null, 0, rows.Count);

        var batch = await repository.NextBatchAsync(session, DateOnly.FromDateTime(clock.GetLocalNow().DateTime));
        var updated = await repository.PrintAsync(session, filter, batch, userCode);
        await session.CommitAsync();
        log.LogInformation("Member invoice batch {Batch}: {Updated} invoice(s) marked printed by {User}", batch, updated, userCode);
        return new AccountingPrintResult(batch, updated, rows.Count);
    }

    /// <summary>
    /// cmdUpdate_Click: gives the member-paid invoices of the ticked worksheet versions the accounting month and year, in one transaction. A version with none stops it with nothing saved.
    /// Returns how many invoices were changed.
    /// </summary>
    public async Task<int> SetPeriodAsync(AccountingPeriodRequest request, string userCode)
    {
        var items = request.Items.DistinctBy(i => (i.CaseNo.Trim(), i.Version.Trim())).ToList();
        await using var session = await sessions.BeginAsync();
        var changed = 0;
        foreach (var item in items)
        {
            var n = await repository.SetPeriodAsync(session, item, request.Month!.Value, request.Year!.Value, userCode);
            if (n == 0) throw new BusinessRuleException($"Worksheet {item.CaseNo.Trim()}-{item.Version.Trim()} has no invoice paid to the member. Search again.");
            changed += n;
        }
        await session.CommitAsync();
        log.LogInformation("{Count} invoice(s) given accounting period {Month}/{Year} by {User}", changed, request.Month, request.Year, userCode);
        return changed;
    }
}
