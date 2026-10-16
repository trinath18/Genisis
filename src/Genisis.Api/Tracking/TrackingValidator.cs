using System.Globalization;

namespace Genisis.Api.Tracking;

/// <summary>Checks a Tracking request and converts its filter text to typed values.</summary>
public static class TrackingValidator
{
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "dd/MM/yyyy"];
    private const int MaxTextLength = 200;

    /// <returns>An error message, or <c>null</c> with <paramref name="query"/> set.</returns>
    public static string? Validate(TrackingRequest request, out TrackingQuery? query)
    {
        query = null;
        var report = TrackingCatalog.Find(request.Report);
        if (report is null) return "Please select a report type.";

        var memberType = MemberType(request.MemberType);
        if (memberType is null) return "MBM Type must be Both, P or S.";
        var principalType = PrincipalType(request.PrincipalType);

        var fields = TrackingCatalog.Fields.ToDictionary(f => f.Key);
        var values = new Dictionary<string, string>();
        foreach (var (key, raw) in request.Values)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            if (!fields.TryGetValue(key, out var field) || field.Range || field.BlankOnly) return $"Unknown filter '{key}'.";
            var value = raw.Trim();
            if (value.Length > MaxTextLength) return $"{field.Label} cannot be longer than {MaxTextLength} characters.";
            if (field.Options is { Count: > 0 } options && !options.Any(o => o.Code == value)) return $"'{value}' is not a valid {field.Label}.";
            values[key] = value;
        }

        var ranges = new Dictionary<string, (object? From, object? To)>();
        foreach (var (key, range) in request.Ranges)
        {
            if (!fields.TryGetValue(key, out var field) || !field.Range) return $"Unknown filter '{key}'.";
            if (Parse(field, range.From, out var from) is { } fromError) return fromError;
            if (Parse(field, range.To, out var to) is { } toError) return toError;
            if (from is not null || to is not null) ranges[key] = (from, to);
        }

        foreach (var key in request.Blanks)
            if (!fields.TryGetValue(key, out var field) || !field.Blankable) return $"Unknown filter '{key}'.";

        query = new TrackingQuery(report, memberType, principalType, values, ranges, request.Blanks.Distinct().ToList());
        return null;
    }

    private static string? MemberType(string? value) => (value ?? MemberTypeSelection.Principal).Trim().ToUpperInvariant() switch
    {
        "" or MemberTypeSelection.Principal => MemberTypeSelection.Principal,
        MemberTypeSelection.Supplementary => MemberTypeSelection.Supplementary,
        MemberTypeSelection.Both or "BOTH" => MemberTypeSelection.Both,
        _ => null,
    };

    private static string PrincipalType(string? value) => (value ?? "").Trim().ToUpperInvariant() switch
    {
        var v when v.StartsWith(PrincipalTypeSelection.CombinedLimit, StringComparison.Ordinal) => PrincipalTypeSelection.CombinedLimit,
        var v when v.StartsWith(PrincipalTypeSelection.SeparateLimit, StringComparison.Ordinal) => PrincipalTypeSelection.SeparateLimit,
        _ => PrincipalTypeSelection.Both,
    };

    private static string? Parse(TrackingField field, string? text, out object? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return null;
        var trimmed = text.Trim();
        switch (field.Kind)
        {
            case FieldKind.Number:
                if (!decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)) return Invalid(field, trimmed);
                value = number;
                return null;
            case FieldKind.Date:
                if (!DateTime.TryParseExact(trimmed, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return Invalid(field, trimmed);
                value = date.Date;
                return null;
            default:
                if (trimmed.Length > MaxTextLength) return $"{field.Label} cannot be longer than {MaxTextLength} characters.";
                value = trimmed;
                return null;
        }
    }

    private static string Invalid(TrackingField field, string text) => $"'{text}' is not a valid value for {field.Label}.";
}
