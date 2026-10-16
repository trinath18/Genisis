using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Genisis.Api.Tracking;

/// <summary>CSV file of a Tracking result (the desktop's Print to File). UTF-8 with BOM so Excel opens it with the right encoding.</summary>
public static partial class TrackingCsvWriter
{
    private const string DateFormat = "dd/MM/yyyy";

    public static string FileName(TrackingReport report) =>
        NonFileNameCharacters().Replace($"Tracking {report.Type} {report.Selection}".Trim(), "-").Trim('-') + ".csv";

    public static byte[] Write(TrackingResult result)
    {
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", result.Columns.Select(Quote)));
        foreach (var row in result.Rows) csv.AppendLine(string.Join(",", result.Columns.Select(c => Quote(Cell(row.GetValueOrDefault(c))))));
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    private static string Cell(object? value) => value switch
    {
        null => "",
        DateTime d => d.ToString(DateFormat, CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    /// <summary>Quotes a cell when needed, and neutralises leading = + - @ so a spreadsheet does not run it as a formula.</summary>
    private static string Quote(string text)
    {
        if (text.Length > 0 && text[0] is '=' or '+' or '@' && !decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            text = "'" + text;
        return text.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }

    [GeneratedRegex("[^A-Za-z0-9]+")]
    private static partial Regex NonFileNameCharacters();
}
