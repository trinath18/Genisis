using Genisis.Api.Common;

namespace Genisis.Api.Worksheet;

/// <summary>A worksheet screen refused the request; the message says why (HTTP 400).</summary>
public class WorksheetException(string message) : BusinessRuleException(message);

/// <summary>A ticked worksheet: case number and claim version.</summary>
/// <param name="CaseNo">Case number.</param>
/// <param name="ClaimVersion">Claim version.</param>
public record WorksheetPick(string CaseNo, int ClaimVersion)
{
    /// <summary>The "Wks No" the screens show: case number - claim version.</summary>
    public string WksNo => $"{CaseNo.Trim()}-{ClaimVersion}";
}

/// <summary>A search result: the rows (never more than <see cref="WorksheetLimits.MaxRows"/>) and whether more rows matched.</summary>
/// <typeparam name="T">Row type.</typeparam>
/// <param name="Rows">The rows.</param>
/// <param name="Truncated">True when the search matched more rows than are returned.</param>
public record SearchResult<T>(List<T> Rows, bool Truncated);

/// <summary>Limits and criteria helpers shared by the worksheet searches.</summary>
public static class WorksheetLimits
{
    /// <summary>Most rows a search returns; a wider search has to be narrowed (the desktop loaded every row into its list).</summary>
    public const int MaxRows = 5000;

    /// <summary>Rows to ask the database for: one more than <see cref="MaxRows"/>, so a cut-off can be reported.</summary>
    public const int FetchRows = MaxRows + 1;

    /// <summary>Trims an over-long list to <see cref="MaxRows"/> and says so.</summary>
    public static SearchResult<T> Cap<T>(List<T> rows) =>
        rows.Count > MaxRows ? new SearchResult<T>(rows.Take(MaxRows).ToList(), true) : new SearchResult<T>(rows, false);

    /// <summary>Blank text is "no criterion" (null); anything else is trimmed.</summary>
    public static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>The date part of a criterion date.</summary>
    public static DateTime? Day(DateTime? value) => value?.Date;

    /// <summary>Token statuses the Send to A/C and Send to A/R searches offer: "0 - Both", "1 - Passed", "2 - Not Yet Passed".</summary>
    public static readonly string[] TokenStatuses = ["0", "1", "2"];

    /// <summary>True for blank (no filter) or one of <see cref="TokenStatuses"/>.</summary>
    public static bool IsTokenStatus(string? value) => string.IsNullOrWhiteSpace(value) || TokenStatuses.Contains(value.Trim());
}
