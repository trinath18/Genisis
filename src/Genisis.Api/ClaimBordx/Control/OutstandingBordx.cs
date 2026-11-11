using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;

namespace Genisis.Api.ClaimBordx.Control;

/// <summary>One line of an outstanding list (a bordx) or of a summary (a payor, with <see cref="Claims"/>).</summary>
public record OutstandingRow
{
    /// <summary>Running item number.</summary>
    public int Item { get; set; }

    /// <summary>Bordx number (empty in a summary).</summary>
    public string? BordxNo { get; set; }

    /// <summary>Payor code.</summary>
    public string? PayorCode { get; set; }

    /// <summary>Number of claims (summary only).</summary>
    public int? Claims { get; set; }

    /// <summary>Amount outstanding.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Date sent (to the payor, or to MNRB in the MNRB list).</summary>
    public DateTime? SentDate { get; set; }

    /// <summary>Days outstanding from the "as at" date, counting the day it was sent.</summary>
    public int? Days { get; set; }

    /// <summary>The amount when it has been outstanding 14 days or less.</summary>
    public decimal? Within14 { get; set; }

    /// <summary>The amount when it has been outstanding more than 14 days.</summary>
    public decimal? Over14 { get; set; }
}

/// <summary>One list of the screen with its totals.</summary>
public record OutstandingSection(string Key, string Title, List<OutstandingRow> Rows, decimal SubTotal, decimal Within14, decimal Over14);

/// <summary>The lists, the layout they are in, the "as at" date and the total due.</summary>
public record OutstandingResult(string Kind, string Mode, DateTime AsAt, List<OutstandingSection> Sections, decimal? TotalDue, bool Truncated);

/// <summary>The criteria of the Outstanding Bordx search.</summary>
public class OutstandingFilter
{
    /// <summary>detail, summaryS (General Summary (S)) or summaryN (General Summary (N)).</summary>
    public string? Mode { get; set; } = "detail";

    /// <summary>For the detail listing: payor, allSihat or allNonSihat.</summary>
    public string? Scope { get; set; } = "payor";

    /// <summary>Payor code (or "CODE - Name"): needed for a detail listing of one payor, optional in a summary.</summary>
    public string? Payor { get; set; }

    /// <summary>"Outstanding Bordereaux As At": the day the days are counted to (today when empty).</summary>
    public DateTime? AsAt { get; set; }
}

/// <summary>cmdSearch_Click's check: a payor for the detail listing of one payor ("Please Enter Payor Code.").</summary>
public sealed class OutstandingFilterValidator : RequestValidator<OutstandingFilter>
{
    /// <summary>Declares the rules.</summary>
    public OutstandingFilterValidator()
    {
        RuleFor(f => f.Mode).Must(m => m is "detail" or "summaryS" or "summaryN").WithMessage("Select Detail Listing, General Summary (S) or General Summary (N).");
        RuleFor(f => f.Scope).Must(s => BordxControlRules.Scopes.Contains(s)).WithMessage("Select a payor, All Sihat or All Non-Sihat.");
        RuleFor(f => f).Must(f => f.Mode != "detail" || f.Scope != "payor" || !string.IsNullOrWhiteSpace(BordxControlRules.PayorCode(f.Payor))).WithMessage("Please Enter Payor Code.");
    }
}

/// <summary>Reads the outstanding bordereaux.</summary>
public interface IOutstandingBordxRepository
{
    /// <summary>The bordereaux of a section; <paramref name="health"/> is S, N or null.</summary>
    Task<List<OutstandingRow>> DetailAsync(IDbSession session, string section, string? payor, string? health);

    /// <summary>The summary of a section by payor.</summary>
    Task<List<OutstandingRow>> SummaryAsync(IDbSession session, string section, string? payor, string? health);
}

/// <inheritdoc />
public sealed class OutstandingBordxRepository : IOutstandingBordxRepository
{
    /// <inheritdoc />
    public Task<List<OutstandingRow>> DetailAsync(IDbSession session, string section, string? payor, string? health) =>
        session.Connection.ProcQueryAsync<OutstandingRow>("genisis.OutstandingBordx_Detail", new { section, payor, health, max = WorksheetLimits.FetchRows }, session.Transaction);

    /// <inheritdoc />
    public Task<List<OutstandingRow>> SummaryAsync(IDbSession session, string section, string? payor, string? health) =>
        session.Connection.ProcQueryAsync<OutstandingRow>("genisis.OutstandingBordx_Summary", new { section, payor, health }, session.Transaction);
}

/// <summary>Claim Bordx > Monitoring > Outstanding Bordx (legacy <c>FrmOSBordx</c>).</summary>
public class OutstandingBordxService(IDbSessions sessions, IBordxControlRepository payorsRepository, IOutstandingBordxRepository repository)
{
    private const int Fortnight = 14;

    /// <summary>The "as at" date's days outstanding: from the day it was sent, that day counted; a result of 0 shows as 1, as the desktop did.</summary>
    public static int? DaysOutstanding(DateTime asAt, DateTime? sent)
    {
        if (sent is null) return null;
        var days = (asAt.Date - sent.Value.Date).Days + 1;
        return days == 0 ? 1 : days;
    }

    private static string Title(string section) => section switch { "payor" => "Bordx with Payor", "mnrb" => "Not Paid by MNRB", _ => "Non-Sihat Malaysia" };

    /// <summary>The payors with their layout.</summary>
    public async Task<List<BordxControlPayor>> PayorsAsync()
    {
        await using var session = await sessions.OpenAsync();
        return await payorsRepository.PayorsAsync(session);
    }

    /// <summary>
    /// cmdSearch_Click: the detail listing of one payor (the lists its HLTCode gives: Sihat has Bordx with Payor and Not Paid by MNRB, the others one list), of all the Sihat or all the non-Sihat
    /// payors, or a general summary by payor. An unknown payor is a <see cref="NotFoundException"/>. The total due is the two Sihat lists added.
    /// </summary>
    public async Task<OutstandingResult> SearchAsync(OutstandingFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        var asAt = (filter.AsAt ?? DateTime.Today).Date;
        var summary = filter.Mode != "detail";
        string? payor = BordxControlRules.PayorCode(filter.Payor) is { Length: > 0 } code ? code : null;
        string kind;
        string? health = null;
        if (summary)
        {
            kind = filter.Mode == "summaryS" ? "S" : "N";
            health = kind;
        }
        else if (filter.Scope == "allSihat")
            (kind, health, payor) = ("S", "S", null);
        else if (filter.Scope == "allNonSihat")
            (kind, health, payor) = ("N", "N", null);
        else
        {
            var known = (await payorsRepository.PayorsAsync(session)).FirstOrDefault(p => string.Equals(p.Code, payor, StringComparison.OrdinalIgnoreCase)) ?? throw new NotFoundException("Payor not found");
            payor = known.Code;
            kind = string.Equals(known.HealthCode, "N", StringComparison.OrdinalIgnoreCase) ? "N" : "S";
        }

        var sections = new List<OutstandingSection>();
        var truncated = false;
        foreach (var section in kind == "S" ? new[] { "payor", "mnrb" } : new[] { "nonSihat" })
        {
            var rows = summary ? await repository.SummaryAsync(session, section, payor, health) : await repository.DetailAsync(session, section, payor, health);
            if (!summary && rows.Count > WorksheetLimits.MaxRows)
            {
                truncated = true;
                rows = rows.Take(WorksheetLimits.MaxRows).ToList();
            }

            decimal within = 0, over = 0, total = 0;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                row.Item = i + 1;
                total += row.Amount ?? 0;
                if (summary) continue;
                row.Days = DaysOutstanding(asAt, row.SentDate);
                if (row.Days is <= Fortnight) (row.Within14, within) = (row.Amount, within + (row.Amount ?? 0));
                else (row.Over14, over) = (row.Amount, over + (row.Amount ?? 0));
            }

            sections.Add(new OutstandingSection(section, Title(section), rows, total, within, over));
        }

        return new OutstandingResult(kind, filter.Mode ?? "detail", asAt, sections, kind == "S" ? sections.Sum(s => s.SubTotal) : null, truncated);
    }
}
