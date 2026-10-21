using Genisis.Api.Common;

namespace Genisis.Api.Finance;

/// <summary>A Finance screen refused the request; the message says why (HTTP 400).</summary>
public class FinanceException(string message) : BusinessRuleException(message);

/// <summary>A code and its caption, for the drop-down lists of the Finance screens.</summary>
/// <param name="Code">Code stored in the database.</param>
/// <param name="Name">Caption shown to the user.</param>
public record CodeName(string Code, string Name);

/// <summary>The time of day the Finance rules check against (posting window). Behind an interface so tests can fix the clock.</summary>
public interface IFinanceClock
{
    /// <summary>The current local date and time of the API server.</summary>
    DateTime Now { get; }
}

/// <inheritdoc />
public sealed class SystemFinanceClock : IFinanceClock
{
    /// <inheritdoc />
    public DateTime Now => DateTime.Now;
}

/// <summary>Limits and criteria helpers shared by the Finance searches.</summary>
public static class FinanceLimits
{
    /// <summary>Most invoices / rows a search returns; a wider search has to be narrowed (the desktop loaded every row into its list).</summary>
    public const int MaxRows = 5000;

    /// <summary>Rows to ask the database for: one more than <see cref="MaxRows"/>, so a cut-off can be reported.</summary>
    public const int FetchRows = MaxRows + 1;

    /// <summary>Blank text is "no criterion" (null); anything else is trimmed.</summary>
    public static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>The date part of a criterion date.</summary>
    public static DateTime? Day(DateTime? value) => value?.Date;

    /// <summary>True for blank (no filter) or one of the allowed codes.</summary>
    public static bool IsBlankOr(string? value, params string[] allowed) => string.IsNullOrWhiteSpace(value) || allowed.Contains(value.Trim());
}
