namespace Genisis.Api.Data;

/// <summary>Turns Dapper's dynamic rows (legacy procedures with many columns) into case-insensitive dictionaries.</summary>
public static class RowMapper
{
    /// <summary>Converts a Dapper dynamic row to a dictionary, trimming strings (legacy CHAR padding).</summary>
    public static Dictionary<string, object?> ToDictionary(object row)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in (IDictionary<string, object?>)row)
        {
            if (result.ContainsKey(key)) continue;
            result[key] = value is string s ? s.Trim() : value;
        }
        return result;
    }

    /// <summary>Converts every row with <see cref="ToDictionary"/>.</summary>
    public static List<Dictionary<string, object?>> ToDictionaries(IEnumerable<object> rows) =>
        rows.Select(ToDictionary).ToList();
}
