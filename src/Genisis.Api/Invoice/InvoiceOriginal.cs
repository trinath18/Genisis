using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Invoice;

/// <summary>One invoice of a case and worksheet version as the Original Invoice Update form shows it.</summary>
public record OriginalInvoice
{
    /// <summary>The invoice's key (<c>INVIndex</c>).</summary>
    public int Id { get; set; }

    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Worksheet version the invoice belongs to.</summary>
    public string Version { get; set; } = "";

    /// <summary>Invoice number.</summary>
    public string? InvoiceNo { get; set; }

    /// <summary>The date on the invoice.</summary>
    public DateTime? InvoiceDate { get; set; }

    /// <summary>"OIR Date": the date the original invoice was received.</summary>
    public DateTime? OriginalReceivedDate { get; set; }

    /// <summary>"Y" once the original invoice has been received.</summary>
    public string? OriginalStatus { get; set; }

    /// <summary>Who is paid: H hospital, M member.</summary>
    public string? PayTo { get; set; }

    /// <summary>The hospital's code when the invoice is paid to one that exists.</summary>
    public string? HospitalCode { get; set; }

    /// <summary>The hospital's accounting code.</summary>
    public string? HospitalAccCode { get; set; }

    /// <summary>The hospital's name.</summary>
    public string? HospitalName { get; set; }
}

/// <summary>What <c>cmdUpdate_Click</c> saves for one invoice.</summary>
public class OriginalInvoiceUpdate
{
    /// <summary>The invoice number.</summary>
    public string? InvoiceNo { get; set; }

    /// <summary>The date on the invoice; left as it was when empty.</summary>
    public DateTime? InvoiceDate { get; set; }

    /// <summary>"OIR Date": the date the original invoice was received (the desktop's "Please Enter Invoice Date!" is about this one).</summary>
    public DateTime? OriginalReceivedDate { get; set; }

    /// <summary>The hospital's code; unchanged when empty or not a hospital.</summary>
    public string? HospitalCode { get; set; }
}

/// <summary><c>cmdUpdate_Click</c>'s checks, in the desktop's order (the case number is the route's).</summary>
public sealed class OriginalInvoiceUpdateValidator : RequestValidator<OriginalInvoiceUpdate>
{
    /// <summary>Declares the rules.</summary>
    public OriginalInvoiceUpdateValidator()
    {
        RuleFor(u => u.InvoiceNo).Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Please Enter Invoice No!");
        RuleFor(u => u.OriginalReceivedDate).NotNull().WithMessage("Please Enter Invoice Date!");
    }
}

/// <summary>Reads and updates the invoices the Original Invoice Update form works on.</summary>
public interface IInvoiceOriginalRepository
{
    /// <summary>The invoices of a case and worksheet version, oldest first.</summary>
    Task<List<OriginalInvoice>> FindAsync(IDbSession session, string caseNo, string version);

    /// <summary>Updates one invoice; returns how many were changed.</summary>
    Task<int> UpdateAsync(IDbSession session, int id, OriginalInvoiceUpdate update);

    /// <summary>The hospitals an invoice can be paid to.</summary>
    Task<List<InvoiceOption>> HospitalsAsync(IDbSession session);
}

/// <inheritdoc />
public sealed class InvoiceOriginalRepository : IInvoiceOriginalRepository
{
    /// <inheritdoc />
    public Task<List<OriginalInvoice>> FindAsync(IDbSession session, string caseNo, string version) =>
        session.Connection.ProcQueryAsync<OriginalInvoice>("genisis.InvoiceOriginal_Find", new { cas = caseNo, version }, session.Transaction);

    /// <inheritdoc />
    public async Task<int> UpdateAsync(IDbSession session, int id, OriginalInvoiceUpdate u) =>
        await session.Connection.ProcScalarAsync<int>("genisis.InvoiceOriginal_Update", new
        {
            id, invoiceNo = u.InvoiceNo!.Trim(), invoiceDate = u.InvoiceDate?.Date, originalDate = u.OriginalReceivedDate!.Value.Date,
            hospital = string.IsNullOrWhiteSpace(u.HospitalCode) ? null : u.HospitalCode.Trim(),
        }, session.Transaction);

    /// <inheritdoc />
    public Task<List<InvoiceOption>> HospitalsAsync(IDbSession session) =>
        session.Connection.ProcQueryAsync<InvoiceOption>("genisis.Invoice_Hospitals", null, session.Transaction);
}

/// <summary>Invoice > Original Invoice Update (legacy frmUpdateInvoice).</summary>
public class InvoiceOriginalService(IDbSessions sessions, IInvoiceOriginalRepository repository, ILogger<InvoiceOriginalService> log)
{
    /// <summary>The message the desktop shows when nothing matches.</summary>
    public const string NotFound = "No record found!";

    /// <summary>cmdShow_Click: the invoices of a case and worksheet version. Throws <see cref="BusinessRuleException"/> for a missing case or version, <see cref="NotFoundException"/> when there is none.</summary>
    public async Task<List<OriginalInvoice>> FindAsync(string? caseNo, string? version)
    {
        if (string.IsNullOrWhiteSpace(caseNo)) throw new BusinessRuleException("Please Enter Case No");
        if (string.IsNullOrWhiteSpace(version)) throw new BusinessRuleException("Please Enter Case Version");
        await using var session = await sessions.OpenAsync();
        var invoices = await repository.FindAsync(session, caseNo.Trim(), version.Trim());
        return invoices.Count == 0 ? throw new NotFoundException(NotFound) : invoices;
    }

    /// <summary>cmdUpdate_Click: saves the invoice number, dates and hospital of one invoice and marks the original as received, in one transaction.</summary>
    public async Task UpdateAsync(int id, OriginalInvoiceUpdate update, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        if (await repository.UpdateAsync(session, id, update) == 0) throw new NotFoundException(NotFound);
        await session.CommitAsync();
        log.LogInformation("Invoice {Id} original receipt updated by {User}", id, userCode);
    }

    /// <summary>Form_Load: the hospitals the drop-down offers.</summary>
    public async Task<List<InvoiceOption>> HospitalsAsync()
    {
        await using var session = await sessions.OpenAsync();
        return await repository.HospitalsAsync(session);
    }
}
