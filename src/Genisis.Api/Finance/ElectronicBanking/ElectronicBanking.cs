using System.Globalization;
using System.Text;
using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Microsoft.Extensions.Options;

namespace Genisis.Api.Finance.ElectronicBanking;

/// <summary>
/// Layout of the bank payment file (configuration section "Finance:BankFile"). No bank layout exists in the legacy code or databases, so
/// the default is a CSV listing; the real bank layout can be set here without code changes.
/// </summary>
public class BankFileOptions
{
    /// <summary>Configuration section.</summary>
    public const string Section = "Finance:BankFile";

    /// <summary>The fields a column can show.</summary>
    public static readonly string[] Fields = ["CreditDate", "PvNo", "PvDate", "CreditRef", "PayeeType", "Payee", "Bank", "Account", "Amount", "Cases"];

    /// <summary>Column separator.</summary>
    public string Delimiter { get; set; } = ",";

    /// <summary>Write the field names as the first line.</summary>
    public bool IncludeHeader { get; set; } = true;

    /// <summary>The fields, in file order (names from <see cref="Fields"/>).</summary>
    public List<string> Columns { get; set; } = ["CreditDate", "PvNo", "PayeeType", "Payee", "Bank", "Account", "Amount", "Cases"];

    /// <summary>Date format of CreditDate / PvDate.</summary>
    public string DateFormat { get; set; } = "dd/MM/yyyy";

    /// <summary>File name start; the date and ".csv" / ".txt" are added.</summary>
    public string FileNamePrefix { get; set; } = "ONLINE-PAYMENTS";

    /// <summary>File extension.</summary>
    public string Extension { get; set; } = "csv";
}

/// <summary>One ONLINE payment as read (with the full account number; never sent to the browser).</summary>
public record BankPaymentSource
{
    /// <summary>M member, H hospital.</summary>
    public string PayeeType { get; set; } = "";

    /// <summary>PV number.</summary>
    public string PvNo { get; set; } = "";

    /// <summary>PV date.</summary>
    public DateTime? PvDate { get; set; }

    /// <summary>The voucher's cheque number: the crediting date (ddmmyyyy) on ONLINE payments.</summary>
    public string? CreditRef { get; set; }

    /// <summary>Payee.</summary>
    public string? Payee { get; set; }

    /// <summary>Bank.</summary>
    public string? Bank { get; set; }

    /// <summary>Account number.</summary>
    public string? Account { get; set; }

    /// <summary>Amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>Number of cases paid.</summary>
    public int Cases { get; set; }

    /// <summary>Account length the bank expects (BankAccounts).</summary>
    public int? AccountDigits { get; set; }

    /// <summary>Second allowed account length.</summary>
    public int? AccountDigitsTwo { get; set; }
}

/// <summary>One ONLINE payment as listed (account masked).</summary>
/// <param name="Key">"M:PVNo" / "H:PVNo".</param>
/// <param name="PayeeType">MEMBER / HOSPITAL.</param>
/// <param name="PvNo">PV number.</param>
/// <param name="PvDate">PV date.</param>
/// <param name="CreditDate">Crediting date (from the voucher's reference, else the PV date).</param>
/// <param name="Payee">Payee.</param>
/// <param name="Bank">Bank.</param>
/// <param name="AccountMasked">Account, last 4 digits.</param>
/// <param name="Amount">Amount.</param>
/// <param name="Cases">Cases paid.</param>
/// <param name="Problem">Why it cannot go into the file, or null.</param>
public record BankPaymentRow(string Key, string PayeeType, string PvNo, DateTime? PvDate, DateTime? CreditDate, string? Payee, string? Bank, string? AccountMasked,
    decimal Amount, int Cases, string? Problem);

/// <summary>A search result.</summary>
/// <param name="Rows">Payments (at most <see cref="FinanceLimits.MaxRows"/>).</param>
/// <param name="Total">Sum of the listed amounts.</param>
/// <param name="Ready">Payments that can go into the file.</param>
/// <param name="Truncated">True when more matched.</param>
public record BankPaymentSearchResult(List<BankPaymentRow> Rows, decimal Total, int Ready, bool Truncated);

/// <summary>The search criteria.</summary>
public class BankPaymentFilter
{
    /// <summary>M members, H hospitals, blank both.</summary>
    public string? PayeeType { get; set; }

    /// <summary>PV date from.</summary>
    public DateTime? PvDateFrom { get; set; }

    /// <summary>PV date to.</summary>
    public DateTime? PvDateTo { get; set; }

    /// <summary>Crediting reference (ddmmyyyy) on the vouchers.</summary>
    public string? CreditRef { get; set; }

    /// <summary>PV number from.</summary>
    public string? PvNoFrom { get; set; }

    /// <summary>PV number to.</summary>
    public string? PvNoTo { get; set; }
}

/// <summary>Build the file from the ticked payments of a search.</summary>
public class BankFileRequest : BankPaymentFilter
{
    /// <summary>The ticked payments ("M:PVNo" / "H:PVNo").</summary>
    public List<string> Keys { get; set; } = [];
}

/// <summary>The bank file.</summary>
/// <param name="FileName">File name.</param>
/// <param name="Content">Bytes.</param>
public record BankFile(string FileName, byte[] Content);

/// <summary>Search checks.</summary>
public sealed class BankPaymentFilterValidator : RequestValidator<BankPaymentFilter>
{
    /// <summary>Declares the rules.</summary>
    public BankPaymentFilterValidator() => BankPaymentRules.Add(this);
}

/// <summary>File checks.</summary>
public sealed class BankFileRequestValidator : RequestValidator<BankFileRequest>
{
    /// <summary>Declares the rules.</summary>
    public BankFileRequestValidator()
    {
        RuleFor(r => r.Keys).Must(k => k.Count > 0).WithMessage("Please select the record");
        BankPaymentRules.Add(this);
    }
}

/// <summary>The shared search rules and the payment rules (pure, unit-tested).</summary>
public static class BankPaymentRules
{
    /// <summary>Adds the search rules.</summary>
    public static void Add<T>(AbstractValidator<T> v) where T : BankPaymentFilter
    {
        v.RuleFor(f => f).Must(f => f.PvDateFrom is not null || f.PvDateTo is not null || new[] { f.CreditRef, f.PvNoFrom, f.PvNoTo }.Any(s => !string.IsNullOrWhiteSpace(s)))
            .WithMessage("Please enter the PV date, the credit reference or the PV no.");
        v.RuleFor(f => f.PayeeType).Must(t => FinanceLimits.IsBlankOr(t, "M", "H")).WithMessage("Select a valid payee type.");
    }

    /// <summary>Why a payment cannot go into the file: no bank / account, or an account length the bank does not use.</summary>
    public static string? Problem(BankPaymentSource p)
    {
        var account = p.Account?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(p.Bank) || account.Length == 0) return "No bank account on record.";
        if (p.AccountDigits is { } digits && account.Length != digits && (p.AccountDigitsTwo is null || account.Length != p.AccountDigitsTwo))
            return $"Account No has {account.Length} digits; {p.Bank!.Trim()} uses {digits}{(p.AccountDigitsTwo is { } two ? $" or {two}" : "")}.";
        return null;
    }

    /// <summary>The crediting date: the voucher reference read as ddmmyyyy, else the PV date.</summary>
    public static DateTime? CreditDate(BankPaymentSource p) =>
        DateTime.TryParseExact(p.CreditRef?.Trim(), "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : p.PvDate;

    /// <summary>"****1234".</summary>
    public static string? Mask(string? account)
    {
        var a = account?.Trim() ?? "";
        return a.Length == 0 ? null : a.Length <= 4 ? a : "****" + a[^4..];
    }

    /// <summary>The listed row.</summary>
    public static BankPaymentRow ToRow(BankPaymentSource p) =>
        new($"{p.PayeeType}:{p.PvNo}", p.PayeeType == "M" ? "MEMBER" : "HOSPITAL", p.PvNo, p.PvDate, CreditDate(p), p.Payee, p.Bank, Mask(p.Account), p.Amount, p.Cases, Problem(p));

    /// <summary>The file in the configured layout. Throws <see cref="InvalidOperationException"/> when the layout names an unknown field.</summary>
    public static byte[] Write(IEnumerable<BankPaymentSource> payments, BankFileOptions o)
    {
        var unknown = o.Columns.FirstOrDefault(c => !BankFileOptions.Fields.Contains(c));
        if (unknown is not null) throw new InvalidOperationException($"Finance:BankFile column '{unknown}' is not one of {string.Join(", ", BankFileOptions.Fields)}.");
        var text = new StringBuilder();
        if (o.IncludeHeader) text.AppendLine(string.Join(o.Delimiter, o.Columns));
        foreach (var p in payments)
            text.AppendLine(string.Join(o.Delimiter, o.Columns.Select(c => Cell(Field(p, c, o), o.Delimiter))));
        return Encoding.UTF8.GetBytes(text.ToString());
    }

    private static string Field(BankPaymentSource p, string field, BankFileOptions o) => field switch
    {
        "CreditDate" => CreditDate(p)?.ToString(o.DateFormat, CultureInfo.InvariantCulture) ?? "",
        "PvNo" => p.PvNo,
        "PvDate" => p.PvDate?.ToString(o.DateFormat, CultureInfo.InvariantCulture) ?? "",
        "CreditRef" => p.CreditRef?.Trim() ?? "",
        "PayeeType" => p.PayeeType == "M" ? "MEMBER" : "HOSPITAL",
        "Payee" => p.Payee?.Trim() ?? "",
        "Bank" => p.Bank?.Trim() ?? "",
        "Account" => p.Account?.Trim() ?? "",
        "Amount" => p.Amount.ToString("0.00", CultureInfo.InvariantCulture),
        _ => p.Cases.ToString(CultureInfo.InvariantCulture),
    };

    private static string Cell(string text, string delimiter)
    {
        if (text.Length > 0 && text[0] is '=' or '+' or '@' && !decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out _)) text = "'" + text;
        return text.Contains(delimiter, StringComparison.Ordinal) || text.IndexOfAny(['"', '\r', '\n']) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }
}

/// <summary>Reads the ONLINE payments.</summary>
public interface IElectronicBankingRepository
{
    /// <summary>The payments that match (at most <see cref="FinanceLimits.FetchRows"/>).</summary>
    Task<List<BankPaymentSource>> SearchAsync(IDbSession session, BankPaymentFilter filter);
}

/// <inheritdoc />
public sealed class ElectronicBankingRepository : IElectronicBankingRepository
{
    /// <inheritdoc />
    public Task<List<BankPaymentSource>> SearchAsync(IDbSession session, BankPaymentFilter f) =>
        session.Connection.ProcQueryAsync<BankPaymentSource>("genisis.ElectronicBanking_Search", new
        {
            payeeType = FinanceLimits.Text(f.PayeeType), dateFrom = FinanceLimits.Day(f.PvDateFrom), dateTo = FinanceLimits.Day(f.PvDateTo),
            creditRef = FinanceLimits.Text(f.CreditRef), pvFrom = FinanceLimits.Text(f.PvNoFrom), pvTo = FinanceLimits.Text(f.PvNoTo), max = FinanceLimits.FetchRows,
        }, session.Transaction);
}

/// <summary>Finance &gt; Electronic Banking (legacy frmFileExportBankESP, source not available): the ONLINE payments and their bank file.</summary>
public class ElectronicBankingService(IDbSessions sessions, IElectronicBankingRepository repository, IOptions<BankFileOptions> options, IFinanceClock clock,
    ILogger<ElectronicBankingService> log)
{
    /// <summary>Payee types.</summary>
    public static readonly CodeName[] PayeeTypes = [new("M", "M - Member"), new("H", "H - Hospital")];

    /// <summary>The ONLINE payments that match, account numbers masked.</summary>
    public async Task<BankPaymentSearchResult> SearchAsync(BankPaymentFilter filter)
    {
        var (rows, truncated) = await ReadAsync(filter);
        var listed = rows.Select(BankPaymentRules.ToRow).ToList();
        return new BankPaymentSearchResult(listed, listed.Sum(r => r.Amount), listed.Count(r => r.Problem is null), truncated);
    }

    /// <summary>The bank file of the ticked payments. Refused when one is gone or has no usable bank account.</summary>
    public async Task<BankFile> FileAsync(BankFileRequest request, string userCode)
    {
        var (rows, _) = await ReadAsync(request);
        var byKey = rows.ToDictionary(r => $"{r.PayeeType}:{r.PvNo}");
        var picked = new List<BankPaymentSource>();
        foreach (var key in request.Keys.Select(k => k.Trim()).Distinct())
        {
            if (!byKey.TryGetValue(key, out var payment)) throw new FinanceException($"Payment {key[(key.IndexOf(':') + 1)..]} is no longer in the list. Search again.");
            if (BankPaymentRules.Problem(payment) is { } problem) throw new FinanceException($"PV {payment.PvNo}: {problem}");
            picked.Add(payment);
        }
        var o = options.Value;
        var content = BankPaymentRules.Write(picked, o);
        log.LogInformation("Bank file with {Count} payment(s), total {Total}, built by {User}", picked.Count, picked.Sum(p => p.Amount), userCode);
        return new BankFile($"{o.FileNamePrefix}-{clock.Now:yyyyMMdd-HHmm}.{o.Extension}", content);
    }

    private async Task<(List<BankPaymentSource> Rows, bool Truncated)> ReadAsync(BankPaymentFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        var rows = await repository.SearchAsync(session, filter);
        var truncated = rows.Count > FinanceLimits.MaxRows;
        return (truncated ? rows.Take(FinanceLimits.MaxRows).ToList() : rows, truncated);
    }
}
