using System.Text.RegularExpressions;
using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Finance.InvoiceReceipt;
using Genisis.Api.Finance.ReceiptEnquiry;

namespace Genisis.Api.Finance.ReverseReceipt;

/// <summary>The reversal details (the receipt is chosen through Receipt Enquiry's search).</summary>
public class ReverseReceiptRequest
{
    /// <summary>Date of the reversal receipt.</summary>
    public DateTime? ReversalDate { get; set; }

    /// <summary>Accounting period "MM/YYYY" of the reversed claim lines.</summary>
    public string? AccPeriod { get; set; }
}

/// <summary>The reversal created.</summary>
/// <param name="Id">ReceiptSummary.ReceiptIndex of the reversal.</param>
/// <param name="ReceiptNo">Its receipt number.</param>
/// <param name="Lines">Claim allocation lines reversed.</param>
/// <param name="Message">Confirmation.</param>
public record ReverseReceiptResult(int Id, string ReceiptNo, int Lines, string Message);

/// <summary>The reversal checks, in the order the user fixes them.</summary>
public sealed partial class ReverseReceiptRequestValidator : RequestValidator<ReverseReceiptRequest>
{
    /// <summary>Declares the rules.</summary>
    public ReverseReceiptRequestValidator()
    {
        RuleFor(r => r.ReversalDate).NotNull().WithMessage("Please Enter Reversal Date");
        RuleFor(r => r.AccPeriod).NotEmpty().WithMessage("Please Keyin for Accounting Period ! ");
        RuleFor(r => r.AccPeriod).Must(p => Period().IsMatch(p!.Trim())).WithMessage("Select a valid accounting period.");
    }

    [GeneratedRegex(@"^(0[1-9]|1[0-2])/\d{4}$")]
    private static partial Regex Period();
}

/// <summary>Writes a reversal.</summary>
public interface IReverseReceiptRepository
{
    /// <summary>Reverses receipt <paramref name="id"/> under <paramref name="newNo"/>; returns the result code, the new id and the lines copied.</summary>
    Task<(int Result, int? Id, int Lines)> ReverseAsync(IDbSession session, int id, string newNo, DateTime date, int accMonth, int accYear, string userCode);
}

/// <inheritdoc />
public sealed class ReverseReceiptRepository : IReverseReceiptRepository
{
    /// <inheritdoc />
    public Task<(int Result, int? Id, int Lines)> ReverseAsync(IDbSession session, int id, string newNo, DateTime date, int accMonth, int accYear, string userCode) =>
        session.Connection.ProcSingleAsync<(int Result, int? Id, int Lines)>("genisis.ReverseReceipt_Reverse",
            new { id, newNo, reversalDate = date.Date, accMonth, accYear, userCode }, session.Transaction);
}

/// <summary>
/// Finance &gt; Reverse Receipt (legacy frmReverseReceipt, source not available). A receipt is reversed by a new receipt with the negative
/// amount (the pattern in the data), numbered from RecSeq like the original; a claim receipt's allocation lines are copied negative.
/// </summary>
public class ReverseReceiptService(
    IDbSessions sessions, IReceiptEnquiryRepository receipts, IInvoiceReceiptRepository numbering, IReverseReceiptRepository repository, ILogger<ReverseReceiptService> log)
{
    /// <summary>The checks on the receipt itself, before anything is written; null when it can be reversed.</summary>
    public static string? Problem(ReceiptRow receipt)
    {
        var no = receipt.ReceiptNo?.Trim() ?? "";
        if (no.Length == 0) return "A receipt without a receipt number cannot be reversed.";
        if (receipt.IsReversal || (receipt.Amount ?? 0m) <= 0) return $"Receipt {no} is a reversal or has no positive amount, so it cannot be reversed.";
        if (!string.IsNullOrWhiteSpace(receipt.ReversedBy)) return $"Receipt {no} has already been reversed by {receipt.ReversedBy}.";
        return null;
    }

    /// <summary>The prefix of the reversal's number: the original's when it is a receipt sequence prefix (RA, RB, RC, RG ...), else RB.</summary>
    public static string Prefix(string receiptNo, IEnumerable<CodeName> prefixes)
    {
        var head = receiptNo.Trim().Length >= 2 ? receiptNo.Trim()[..2].ToUpperInvariant() : "";
        return prefixes.Any(p => p.Code == head) ? head : InvoiceReceiptCodes.DefaultPrefix;
    }

    /// <summary>Reverses receipt <paramref name="id"/> in one transaction. Throws <see cref="NotFoundException"/> / <see cref="FinanceException"/> when it cannot.</summary>
    public async Task<ReverseReceiptResult> ReverseAsync(int id, ReverseReceiptRequest request, string userCode)
    {
        var period = request.AccPeriod!.Trim();
        var (month, year) = (int.Parse(period[..2]), int.Parse(period[3..]));
        await using var session = await sessions.BeginAsync();
        var found = await receipts.GetAsync(session, id) ?? throw new NotFoundException("No record found!");
        var original = found.Receipt;
        if (Problem(original) is { } problem) throw new FinanceException(problem);

        var (prefixes, _) = await numbering.LookupsAsync(session);
        var newNo = await numbering.NextNumberAsync(session, Prefix(original.ReceiptNo!, prefixes));
        var (result, newId, lines) = await repository.ReverseAsync(session, id, newNo, request.ReversalDate!.Value, month, year, userCode);
        if (result == StoredProcedures.NotFound) throw new NotFoundException("No record found!");
        if (result == StoredProcedures.Conflict || newId is null) throw new FinanceException($"Receipt {original.ReceiptNo} cannot be reversed (it was changed or reversed meanwhile). Search again.");
        await session.CommitAsync();
        log.LogInformation("Receipt {Original} ({Amount}) reversed by {Reversal} with {Lines} claim line(s) by {User}", original.ReceiptNo, original.Amount, newNo, lines, userCode);
        return new ReverseReceiptResult(newId.Value, newNo, lines, $"Receipt {original.ReceiptNo} reversed by {newNo}.");
    }
}
