namespace Genisis.Api.Invoice;

/// <summary>Rules the Invoice HOS and MBM Accounting forms share (<c>FrmInvoiceHOSAC</c>, <c>FrmInvoiceMBMAC</c>).</summary>
public static class InvoiceAccountingRules
{
    /// <summary>Most invoices a search lists, and so the most a print marks (the desktop loaded every row into its list).</summary>
    public const int MaxRows = 5000;

    /// <summary>The message the desktop shows when there is nothing listed to print or update.</summary>
    public const string NothingListed = "Please select a record(s) before proceeding!";

    /// <summary>The message the desktop shows when no print status is chosen.</summary>
    public const string NoPrintStatus = "Please select Print Status";

    /// <summary>"N - New" (not printed yet) or "R - Reprint" (printed): the two choices of the Print Status drop-down.</summary>
    public static bool IsPrintStatus(string? value) => value?.Trim().ToUpperInvariant() is "N" or "R";

    /// <summary>Whether the status asks for a reprint of what was printed (R) and not for the new invoices (N).</summary>
    public static bool IsReprint(string? value) => string.Equals(value?.Trim(), "R", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The debit and credit columns of an invoice's list row, from the amount printed before and the amount payable now, as the listing works them out: the difference when both are
    /// positive or both negative, the sum when only the previous one is negative, the amount payable when the previous one is positive and the payable negative (or there is none).
    /// A negative result is shown as a debit without its sign; a positive one as a credit; zero is a credit of 0.00.
    /// </summary>
    public static (decimal? Debit, decimal? Credit) DebitCredit(decimal? previous, decimal? payable)
    {
        var pay = payable ?? 0m;
        var amount = previous is { } before
            ? before >= 0 && pay >= 0 ? pay - before
            : before < 0 && pay >= 0 ? pay + before
            : before < 0 && pay < 0 ? pay - before
            : pay
            : pay;
        return amount < 0 ? (-amount, null) : (null, amount);
    }

    /// <summary>The day a batch label (<c>H23102026-1</c>) stands for, or <c>null</c> for anything else.</summary>
    public static DateOnly? BatchDay(string? batchDate)
    {
        var text = batchDate?.Trim();
        return text is { Length: 9 } && DateOnly.TryParseExact(text[1..], "ddMMyyyy", null, System.Globalization.DateTimeStyles.None, out var day) ? day : null;
    }

    /// <summary>The batch label of a kind (H or M), a day and the number of the day.</summary>
    public static string BatchLabel(char kind, DateOnly day, int number) => $"{kind}{day:ddMMyyyy}-{number}";
}
