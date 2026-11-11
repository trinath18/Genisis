using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;

namespace Genisis.Api.ClaimBordx.Control;

/// <summary>A payor with the layout its bordereaux are followed in: N non-Sihat, S Sihat (the payor's HLTCode).</summary>
public record BordxControlPayor(string Code, string? Name, string? HealthCode);

/// <summary>One line of the Bordx Control list: a bordx and the dates it moved on. The first group (days, sent, received) is for every payor; the MNRB dates are for Sihat payors and the money from the payor for the others.</summary>
public record BordxControlRow
{
    /// <summary>Payor code.</summary>
    public string? PayorCode { get; set; }

    /// <summary>Bordx number.</summary>
    public string BordxNo { get; set; } = "";

    /// <summary>Bordx date.</summary>
    public DateTime? BordxDate { get; set; }

    /// <summary>Number of claims.</summary>
    public int? Claims { get; set; }

    /// <summary>Bordx amount.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Bordx status.</summary>
    public string? Status { get; set; }

    /// <summary>DTP: date sent to the payor.</summary>
    public DateTime? SentToPayor { get; set; }

    /// <summary>DRP (Doc): date the documents came back from the payor.</summary>
    public DateTime? DocReceived { get; set; }

    /// <summary>Days from DTP to DRP (a same-day return counts as 1; blank when a date is missing).</summary>
    public int? Days { get; set; }

    /// <summary>DTMNRB: date sent to MNRB (Sihat payors).</summary>
    public DateTime? SentToMnrb { get; set; }

    /// <summary>DFMNRB (Money): date the money came back from MNRB (Sihat payors).</summary>
    public DateTime? MoneyFromMnrb { get; set; }

    /// <summary>DFPayor (Money): date the money came back from the payor (other payors).</summary>
    public DateTime? MoneyFromPayor { get; set; }

    /// <summary>Days from DTMNRB to DFMNRB, or from DTP to the payor's payment (same-day counts as 1).</summary>
    public int? Days2 { get; set; }

    /// <summary>Total days (Sihat payors): the two day counts added, a blank counting as 0.</summary>
    public int? TotalDays { get; set; }
}

/// <summary>The list, with the layout it is in and the total of the amounts.</summary>
public record BordxControlResult(string Kind, List<BordxControlRow> Rows, decimal TotalAmount, bool Truncated);

/// <summary>The criteria of the Bordx Control search.</summary>
public class BordxControlFilter
{
    /// <summary>Payor: one payor, "allSihat" or "allNonSihat" (the desktop's two "All" boxes).</summary>
    public string? Scope { get; set; } = "payor";

    /// <summary>Payor code (or "CODE - Name"), for the one-payor scope.</summary>
    public string? Payor { get; set; }

    /// <summary>Bordx number from.</summary>
    public string? BordxFrom { get; set; }

    /// <summary>Bordx number to.</summary>
    public string? BordxTo { get; set; }

    /// <summary>Bordx date from.</summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>Bordx date to.</summary>
    public DateTime? DateTo { get; set; }

    /// <summary>To Payor: 1 sent, 0 not yet sent, empty both.</summary>
    public string? PayTo { get; set; }

    /// <summary>From Payor (Doc): 1 received, 0 not yet received, empty both.</summary>
    public string? PayDocFr { get; set; }

    /// <summary>To MNRB (Sihat): 1 sent, 0 not yet sent, B both, empty not applied.</summary>
    public string? MnrbTo { get; set; }

    /// <summary>From MNRB (Money, Sihat): F paid, P partial paid, U unpaid, empty not applied.</summary>
    public string? MnrbFr { get; set; }

    /// <summary>From Payor (Money, non-Sihat): F paid, P partial paid, U unpaid, empty not applied.</summary>
    public string? PayFr { get; set; }
}

/// <summary>The rules of the Bordx Control search.</summary>
public static class BordxControlRules
{
    /// <summary>The scopes.</summary>
    public static readonly string[] Scopes = ["payor", "allSihat", "allNonSihat"];

    /// <summary>"CODE - Name" gives CODE.</summary>
    public static string PayorCode(string? text) => (text ?? "").Split(" - ")[0].Trim().ToUpperInvariant();

    /// <summary>A one-character flag, or null when blank.</summary>
    public static string? Flag(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim()[..1].ToUpperInvariant();

    /// <summary>The desktop showed a same-day count as 1 day and left a missing one blank.</summary>
    public static int? ShownDays(int? days) => days is null ? null : days == 0 ? 1 : days;
}

/// <summary>cmdSearch_Click's check: a payor, unless one of the two "All" boxes is ticked ("Please Enter Payor Code."); and the choices have to be the ones the radio buttons offer.</summary>
public sealed class BordxControlFilterValidator : RequestValidator<BordxControlFilter>
{
    private static bool Within(string? value, string allowed) => BordxControlRules.Flag(value) is not { } f || allowed.Contains(f);

    /// <summary>Declares the rules.</summary>
    public BordxControlFilterValidator()
    {
        RuleFor(f => f.Scope).Must(s => BordxControlRules.Scopes.Contains(s)).WithMessage("Select a payor, All Sihat or All Non-Sihat.");
        RuleFor(f => f).Must(f => f.Scope != "payor" || !string.IsNullOrWhiteSpace(BordxControlRules.PayorCode(f.Payor))).WithMessage("Please Enter Payor Code.");
        RuleFor(f => f.PayTo).Must(v => Within(v, "01")).WithMessage("To Payor is Sent or Not Yet Sent.");
        RuleFor(f => f.PayDocFr).Must(v => Within(v, "01")).WithMessage("From Payor (Doc) is Received or Not Yet Received.");
        RuleFor(f => f.MnrbTo).Must(v => Within(v, "01B")).WithMessage("To MNRB is Sent, Not Yet Sent or Both.");
        RuleFor(f => f.MnrbFr).Must(v => Within(v, "FPU")).WithMessage("From MNRB (Money) is Paid, Partial Paid or Unpaid.");
        RuleFor(f => f.PayFr).Must(v => Within(v, "FPU")).WithMessage("From Payor (Money) is Paid, Partial Paid or Unpaid.");
    }
}

/// <summary>Reads the bordereaux with their movements.</summary>
public interface IBordxControlRepository
{
    /// <summary>The payors with their layout.</summary>
    Task<List<BordxControlPayor>> PayorsAsync(IDbSession session);

    /// <summary>The Sihat layout: <paramref name="payor"/> null means all the Sihat payors.</summary>
    Task<List<BordxControlRow>> SihatAsync(IDbSession session, string? payor, BordxControlFilter filter);

    /// <summary>The non-Sihat layout: <paramref name="payor"/> null means all the non-Sihat payors.</summary>
    Task<List<BordxControlRow>> NonSihatAsync(IDbSession session, string? payor, BordxControlFilter filter);
}

/// <inheritdoc />
public sealed class BordxControlRepository : IBordxControlRepository
{
    private static string? Bordx(string? text) => WorksheetLimits.Text(text)?.ToUpperInvariant();

    /// <inheritdoc />
    public async Task<List<BordxControlPayor>> PayorsAsync(IDbSession session) => (await session.Connection.ProcQueryAsync<BordxControlPayor>("genisis.BordxControl_Payors", null, session.Transaction)).ToList();


    /// <inheritdoc />
    public Task<List<BordxControlRow>> SihatAsync(IDbSession session, string? payor, BordxControlFilter f) =>
        session.Connection.ProcQueryAsync<BordxControlRow>("genisis.BordxControl_Sihat", new
        {
            payor, bordxFrom = Bordx(f.BordxFrom), bordxTo = Bordx(f.BordxTo), dateFrom = WorksheetLimits.Day(f.DateFrom), dateTo = WorksheetLimits.Day(f.DateTo), payTo = BordxControlRules.Flag(f.PayTo),
            payDocFr = BordxControlRules.Flag(f.PayDocFr), mnrbTo = BordxControlRules.Flag(f.MnrbTo), mnrbFr = BordxControlRules.Flag(f.MnrbFr), max = WorksheetLimits.FetchRows,
        }, session.Transaction);

    /// <inheritdoc />
    public Task<List<BordxControlRow>> NonSihatAsync(IDbSession session, string? payor, BordxControlFilter f) =>
        session.Connection.ProcQueryAsync<BordxControlRow>("genisis.BordxControl_NonSihat", new
        {
            payor, bordxFrom = Bordx(f.BordxFrom), bordxTo = Bordx(f.BordxTo), dateFrom = WorksheetLimits.Day(f.DateFrom), dateTo = WorksheetLimits.Day(f.DateTo), payTo = BordxControlRules.Flag(f.PayTo),
            payDocFr = BordxControlRules.Flag(f.PayDocFr), payFr = BordxControlRules.Flag(f.PayFr), max = WorksheetLimits.FetchRows,
        }, session.Transaction);
}

/// <summary>Claim Bordx > Monitoring > Bordx Control (legacy <c>FrmBordxControl</c>).</summary>
public class BordxControlService(IDbSessions sessions, IBordxControlRepository repository)
{
    /// <summary>PayorComboListing: the payors with their layout.</summary>
    public async Task<List<BordxControlPayor>> PayorsAsync()
    {
        await using var session = await sessions.OpenAsync();
        return await repository.PayorsAsync(session);
    }

    /// <summary>
    /// cmdSearch_Click: the bordx of one payor in the layout the payor's HLTCode gives (N non-Sihat, anything else Sihat, as the desktop took it), or of all the Sihat or all the non-Sihat payors.
    /// An unknown payor code is a <see cref="NotFoundException"/>.
    /// </summary>
    public async Task<BordxControlResult> SearchAsync(BordxControlFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        var payor = filter.Scope == "payor" ? BordxControlRules.PayorCode(filter.Payor) : null;
        var kind = filter.Scope switch { "allSihat" => "S", "allNonSihat" => "N", _ => null };
        if (kind is null)
        {
            var known = (await repository.PayorsAsync(session)).FirstOrDefault(p => string.Equals(p.Code, payor, StringComparison.OrdinalIgnoreCase)) ?? throw new NotFoundException("Payor not found");
            payor = known.Code;
            kind = string.Equals(known.HealthCode, "N", StringComparison.OrdinalIgnoreCase) ? "N" : "S";
        }

        var rows = kind == "S" ? await repository.SihatAsync(session, payor, filter) : await repository.NonSihatAsync(session, payor, filter);
        var capped = WorksheetLimits.Cap(rows);
        foreach (var row in capped.Rows)
        {
            row.Days = BordxControlRules.ShownDays(row.Days);
            row.Days2 = BordxControlRules.ShownDays(row.Days2);
            if (kind == "S") row.TotalDays = (row.Days ?? 0) + (row.Days2 ?? 0);
        }

        return new BordxControlResult(kind, capped.Rows, capped.Rows.Sum(r => r.Amount ?? 0), capped.Truncated);
    }
}
