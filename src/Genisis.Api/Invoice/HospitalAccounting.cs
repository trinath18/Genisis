using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;

namespace Genisis.Api.Invoice;

/// <summary>One invoice paid to a hospital, as a row of the Invoice HOS Accounting list.</summary>
public record HospitalAccountingRow
{
    /// <summary>The hospital's code.</summary>
    public string? HospitalCode { get; set; }

    /// <summary>The hospital's name.</summary>
    public string? Hospital { get; set; }

    /// <summary>Invoice number.</summary>
    public string? InvoiceNo { get; set; }

    /// <summary>Invoice date.</summary>
    public DateTime? InvoiceDate { get; set; }

    /// <summary>The date the invoice was registered.</summary>
    public DateTime? RegisteredDate { get; set; }

    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Worksheet version.</summary>
    public string Version { get; set; } = "";

    /// <summary>Bordx number.</summary>
    public string? BordxNo { get; set; }

    /// <summary>Member number.</summary>
    public string? MemberNo { get; set; }

    /// <summary>Member name.</summary>
    public string? MemberName { get; set; }

    /// <summary>Patient name.</summary>
    public string? PatientName { get; set; }

    /// <summary>Payable to the hospital (<c>CLMPayableByMedix2H</c>).</summary>
    public decimal? Payable { get; set; }

    /// <summary>The amount printed before (<c>INVPreviousAmt</c>).</summary>
    public decimal? PreviousAmount { get; set; }

    /// <summary>The debit/credit reference.</summary>
    public string? DebitCreditRefNo { get; set; }

    /// <summary>The batch the invoice was printed in: H, the day (ddmmyyyy).</summary>
    public string? BatchDate { get; set; }

    /// <summary>The number of the batch within its day.</summary>
    public int? BatchNo { get; set; }

    /// <summary>The DR column.</summary>
    public decimal? Debit { get; set; }

    /// <summary>The CR column.</summary>
    public decimal? Credit { get; set; }
}

/// <summary>The criteria of the Invoice HOS Accounting search: every "from"/"to" pair is inclusive and ignored when empty.</summary>
public class HospitalAccountingFilter
{
    /// <summary>Print Status: N new (not printed yet) or R reprint (printed). Required.</summary>
    public string? PrintStatus { get; set; } = "N";

    /// <summary>Invoice date from.</summary>
    public DateTime? InvoiceDateFrom { get; set; }

    /// <summary>Invoice date to.</summary>
    public DateTime? InvoiceDateTo { get; set; }

    /// <summary>Invoice registration date from.</summary>
    public DateTime? RegisteredFrom { get; set; }

    /// <summary>Invoice registration date to.</summary>
    public DateTime? RegisteredTo { get; set; }

    /// <summary>The hospital's code.</summary>
    public string? HospitalCode { get; set; }

    /// <summary>Batch day from.</summary>
    public DateTime? BatchDateFrom { get; set; }

    /// <summary>Batch day to.</summary>
    public DateTime? BatchDateTo { get; set; }

    /// <summary>Batch number from.</summary>
    public int? BatchNoFrom { get; set; }

    /// <summary>Batch number to.</summary>
    public int? BatchNoTo { get; set; }
}

/// <summary>What printing gave: the batch the invoices were marked with (none for a reprint) and how many invoices were marked.</summary>
/// <param name="Batch">The batch label (<c>H23102026-1</c>), or <c>null</c> for a reprint, which marks nothing.</param>
/// <param name="Updated">Invoices marked as printed.</param>
/// <param name="Rows">The invoices that were listed.</param>
public record AccountingPrintResult(string? Batch, int Updated, int Rows);

/// <summary>The checks of the Invoice HOS Accounting search and print (the print status is required).</summary>
public sealed class HospitalAccountingFilterValidator : RequestValidator<HospitalAccountingFilter>
{
    /// <summary>Declares the rules.</summary>
    public HospitalAccountingFilterValidator() =>
        RuleFor(f => f.PrintStatus).Must(InvoiceAccountingRules.IsPrintStatus).WithMessage(InvoiceAccountingRules.NoPrintStatus);
}

/// <summary>Reads the invoices paid to hospitals and marks them as printed.</summary>
public interface IHospitalAccountingRepository
{
    /// <summary>The invoices that match the filter (at most <paramref name="max"/>).</summary>
    Task<List<HospitalAccountingRow>> SearchAsync(IDbSession session, HospitalAccountingFilter filter, int max);

    /// <summary>The next batch label of the day.</summary>
    Task<string> NextBatchAsync(IDbSession session, DateOnly day);

    /// <summary>Marks the invoices of the worksheet versions the filter lists as printed in a batch; returns how many.</summary>
    Task<int> PrintAsync(IDbSession session, HospitalAccountingFilter filter, string batch, string userCode);

    /// <summary>Form_Load / ComboListing: the hospitals of the drop-down.</summary>
    Task<List<InvoiceOption>> HospitalsAsync(IDbSession session);
}

/// <inheritdoc />
public sealed class HospitalAccountingRepository : IHospitalAccountingRepository
{
    /// <inheritdoc />
    public async Task<List<HospitalAccountingRow>> SearchAsync(IDbSession session, HospitalAccountingFilter f, int max)
    {
        var rows = await session.Connection.ProcQueryAsync<HospitalAccountingRow>("genisis.InvoiceHospitalAccounting_Search", new
        {
            reprint = InvoiceAccountingRules.IsReprint(f.PrintStatus), invFrom = f.InvoiceDateFrom?.Date, invTo = f.InvoiceDateTo?.Date, regFrom = f.RegisteredFrom?.Date, regTo = f.RegisteredTo?.Date, hospital = WorksheetLimits.Text(f.HospitalCode),
            batchDateFrom = f.BatchDateFrom?.Date, batchDateTo = f.BatchDateTo?.Date, batchNoFrom = f.BatchNoFrom, batchNoTo = f.BatchNoTo, max,
        }, session.Transaction);
        foreach (var row in rows) (row.Debit, row.Credit) = InvoiceAccountingRules.DebitCredit(row.PreviousAmount, row.Payable);
        return rows;
    }

    /// <inheritdoc />
    public Task<List<InvoiceOption>> HospitalsAsync(IDbSession session) => session.Connection.ProcQueryAsync<InvoiceOption>("genisis.Invoice_Hospitals", null, session.Transaction);

    /// <inheritdoc />
    public async Task<string> NextBatchAsync(IDbSession session, DateOnly day) =>
        await session.Connection.ProcScalarAsync<string>("genisis.InvoiceAccounting_NextBatch", new { kind = "H", day = day.ToDateTime(TimeOnly.MinValue) }, session.Transaction) ?? "";

    /// <inheritdoc />
    public async Task<int> PrintAsync(IDbSession session, HospitalAccountingFilter f, string batch, string userCode) =>
        await session.Connection.ProcScalarAsync<int>("genisis.InvoiceHospitalAccounting_Print", new
        {
            batch, user = userCode, invFrom = f.InvoiceDateFrom?.Date, invTo = f.InvoiceDateTo?.Date, regFrom = f.RegisteredFrom?.Date, regTo = f.RegisteredTo?.Date, hospital = WorksheetLimits.Text(f.HospitalCode),
            batchDateFrom = f.BatchDateFrom?.Date, batchDateTo = f.BatchDateTo?.Date, batchNoFrom = f.BatchNoFrom, batchNoTo = f.BatchNoTo,
        }, session.Transaction);
}

/// <summary>Invoice > Invoice HOS Accounting (legacy FrmInvoiceHOSAC, "Invoice Hospital Accounting").</summary>
public class HospitalAccountingService(IDbSessions sessions, IHospitalAccountingRepository repository, TimeProvider clock, ILogger<HospitalAccountingService> log)
{
    /// <summary>Form_Load / ComboListing: the hospitals of the drop-down.</summary>
    public async Task<List<InvoiceOption>> HospitalsAsync()
    {
        await using var session = await sessions.OpenAsync();
        return await repository.HospitalsAsync(session);
    }

    /// <summary>CmdSearch_Click: the invoices that match the criteria, cut off at <see cref="InvoiceAccountingRules.MaxRows"/>.</summary>
    public async Task<SearchResult<HospitalAccountingRow>> SearchAsync(HospitalAccountingFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        var rows = await repository.SearchAsync(session, filter, InvoiceAccountingRules.MaxRows + 1);
        return rows.Count > InvoiceAccountingRules.MaxRows ? new SearchResult<HospitalAccountingRow>(rows.Take(InvoiceAccountingRules.MaxRows).ToList(), true) : new SearchResult<HospitalAccountingRow>(rows, false);
    }

    /// <summary>
    /// CmdPrintFile_Click: for "N - New" takes the next batch of the day and marks the invoices the criteria list as printed in it, all in one transaction; for "R - Reprint" marks
    /// nothing. The desktop marked every invoice the criteria matched, which was what its list showed; a search that matches more than the list can hold is refused here, so a
    /// print never marks invoices that were not listed. Throws <see cref="BusinessRuleException"/> when nothing is listed or too much is.
    /// </summary>
    public async Task<AccountingPrintResult> PrintAsync(HospitalAccountingFilter filter, string userCode)
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
        log.LogInformation("Hospital invoice batch {Batch}: {Updated} invoice(s) marked printed by {User}", batch, updated, userCode);
        return new AccountingPrintResult(batch, updated, rows.Count);
    }
}
