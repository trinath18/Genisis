using System.Globalization;

namespace Genisis.Api.Authorize.Dcu;

/// <summary>A row of a query, by field name (case-insensitive).</summary>
public sealed class DcuRow(IDictionary<string, object?> values)
{
    private readonly Dictionary<string, object?> fields = new(values, StringComparer.OrdinalIgnoreCase);

    /// <summary>The value of a field; null when it is empty or absent.</summary>
    public object? this[string name] => fields.TryGetValue(name, out var v) && v is not DBNull ? v : null;

    /// <summary>The field as the desktop's <c>Len(...)</c> saw it: the text, null when the field has none.</summary>
    public string? Text(string name) => this[name] switch
    {
        null => null,
        string s => s.Length == 0 ? null : s,
        DateTime d => d.ToString("s", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        var o => o.ToString(),
    };

    /// <summary>The field as a number; null when it is empty or not a number.</summary>
    public decimal? Number(string name) => this[name] switch
    {
        null => null,
        decimal d => d,
        string s => decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null,
        IConvertible c => Convert.ToDecimal(c, CultureInfo.InvariantCulture),
        _ => null,
    };
}

/// <summary>What the lists need besides the query: the duration of stay of the case groups, the ICD descriptions and the bordx amounts of the cases.</summary>
/// <param name="Durations">Sum of the duration of stay by group (see <see cref="DcuReportEngine.GroupKey"/>).</param>
/// <param name="IcdDescriptions">ICD code to description.</param>
/// <param name="CaseBordx">Case number to the approved amounts of its claims not yet in a bordx and in one.</param>
public record DcuLookupData(IReadOnlyDictionary<string, decimal> Durations, IReadOnlyDictionary<string, string> IcdDescriptions, IReadOnlyDictionary<string, (decimal? NotYetBordx, decimal? Bordxed)> CaseBordx);

/// <summary>The cells of a report's list (the routines of FrmDCUReport1 that filled a ListView, written once for every report): a pure function of the report, the rows and the lookups.</summary>
public static class DcuReportEngine
{
    private const string Plain = "#,##0.00";
    private const string Paren = "#,##0.00;(#,##0.00)";

    /// <summary>The key of a group: its fields trimmed and joined, an empty and a missing value being the same group (the desktop asked for "Sex = '' OR Sex IS NULL").</summary>
    public static string GroupKey(DcuRow row, IEnumerable<string> keys) => string.Join('\u001f', keys.Select(k => row.Text(k)?.Trim() ?? ""));

    /// <summary>The duration of each group of the duration case, with the empty and missing keys summed (as the desktop's one question for such a key did).</summary>
    public static Dictionary<string, decimal> SumDurations(IEnumerable<DcuRow> rows, IReadOnlyList<string> keys)
    {
        var sums = new Dictionary<string, decimal>();
        foreach (var row in rows)
        {
            var key = GroupKey(row, keys);
            sums[key] = sums.GetValueOrDefault(key) + (row.Number("NoofDay") ?? 0m);
        }

        return sums;
    }

    /// <summary>The ICD codes the rows show, once each.</summary>
    public static List<string> IcdCodes(DcuReportDefinition report, IEnumerable<DcuRow> rows) =>
        report.Columns.Where(c => c.Kind == "icd").Select(c => c.Field!).SelectMany(f => rows.Select(r => r.Text(f)?.Trim())).Where(c => !string.IsNullOrEmpty(c)).Distinct(StringComparer.OrdinalIgnoreCase).Select(c => c!).ToList();

    /// <summary>The case numbers of a case general listing (for the bordx amounts), once each.</summary>
    public static List<string> CaseNumbers(DcuReportDefinition report, IEnumerable<DcuRow> rows) =>
        report.Columns.Any(c => c.Kind is "notYetBordx" or "bordxed") ? rows.Select(r => r.Text("CASNumber")?.Trim()).Where(c => !string.IsNullOrEmpty(c)).Distinct().Select(c => c!).ToList() : [];

    /// <summary>The cells of every row.</summary>
    public static List<string[]> Build(DcuReportDefinition report, IEnumerable<DcuRow> rows, DcuLookupData lookups) =>
        rows.Select(row => report.Columns.Select(c => Cell(report, c, row, lookups)).ToArray()).ToList();

    private static string Cell(DcuReportDefinition report, DcuColumnDefinition c, DcuRow row, DcuLookupData lookups) => c.Kind switch
    {
        "text" => Text(c, row),
        "money" => Money(c, row),
        "date" => row[c.Field!] is DateTime d ? d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : "",
        "claimNo" => $"{row.Text("CASNumber")}-{row.Text("ClaimVersion")}",
        "yearMonth" => YearMonth(row),
        "ratio" => Ratio(c, row),
        "duration" => Duration(report, row, lookups),
        "icd" => row.Text(c.Field!)?.Trim() is { Length: > 0 } code && lookups.IcdDescriptions.TryGetValue(code, out var description) ? description : "",
        "notYetBordx" => Amount(row.Text("CASNumber")?.Trim() is { } no && lookups.CaseBordx.TryGetValue(no, out var a) ? a.NotYetBordx : null),
        "bordxed" => Amount(row.Text("CASNumber")?.Trim() is { } no2 && lookups.CaseBordx.TryGetValue(no2, out var b) ? b.Bordxed : null),
        _ => "",
    };

    private static string Amount(decimal? value) => value is { } v ? v.ToString(Paren, CultureInfo.InvariantCulture) : "";

    /// <summary>A text column: the field, trimmed when the desktop trimmed it; an empty field is the label ("Empty") the column has, or nothing.</summary>
    private static string Text(DcuColumnDefinition c, DcuRow row) => row.Text(c.Field!) is { } text ? (c.Trim ? text.Trim() : text) : c.Empty ?? "";

    private static string Money(DcuColumnDefinition c, DcuRow row)
    {
        var value = row.Number(c.Field!);
        var format = c.Format == "paren" ? Paren : Plain;
        if (value is null) return c.Zero ?? "";
        if (c.NegativeOnly && value >= 0) return c.Zero ?? "";
        return value.Value.ToString(format, CultureInfo.InvariantCulture);
    }

    /// <summary>Year/Month with the month on two digits; an empty month and year give "/".</summary>
    public static string YearMonth(DcuRow row)
    {
        var month = row.Text("MONTH")?.Trim() ?? "";
        var year = row.Text("YEAR")?.Trim() ?? "";
        return year + "/" + (month.Length == 1 ? "0" + month : month);
    }

    private static string Ratio(DcuColumnDefinition c, DcuRow row)
    {
        var den = row.Number(c.Den!) ?? 0m;
        return den == 0 ? 0m.ToString(Plain, CultureInfo.InvariantCulture) : Math.Round((row.Number(c.Num!) ?? 0m) / den, 0, MidpointRounding.ToEven).ToString(Plain, CultureInfo.InvariantCulture);
    }

    /// <summary>The duration of stay: the query's NoofDay for a Claim report or the general listing, the duration case's sum of the group for a Case report; none is shown as 1 (the desktop's rule), nothing as blank.</summary>
    private static string Duration(DcuReportDefinition report, DcuRow row, DcuLookupData lookups)
    {
        decimal? days;
        if (report.DurationCase is null) days = row.Number("NoofDay");
        else days = lookups.Durations.TryGetValue(GroupKey(row, report.DurationKeys), out var sum) ? sum : null;
        return days is null ? "" : days == 0 ? "1" : days.Value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}

/// <summary>The efficiency reports (DIREfficiencyRpt, BordxEfficiencyRpt, InvEfficiencyRpt, BordxInvEfficiencyRpt, OSBordxEfficiencyRpt): the claims by month and how old they are.</summary>
public static class DcuEfficiency
{
    private static readonly string[] MonthNames = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];

    /// <summary>One row per month: the total, the claims in each age band and what is outstanding (the total less the bands).</summary>
    public static List<string[]> AgeingRows(IEnumerable<DcuRow> rows) =>
        rows.Select(row =>
        {
            var month = (int?)row.Number("MONTH");
            var total = row.Number("Total") ?? 0m;
            var bands = new[] { "D0to15", "D15to30", "D31to60", "D61to90", "D90" }.Select(b => row.Number(b) ?? 0m).ToArray();
            string[] cells = [month is >= 1 and <= 12 ? MonthNames[month.Value - 1] : "Empty", Text(total), .. bands.Select(Text), Text(total - bands.Sum())];
            return cells;
        }).ToList();

    /// <summary>The outstanding bordereaux by registration month: what was carried forward, what was registered, what went into a bordx that month and what is left, month after month.</summary>
    public static List<string[]> OutstandingRows(IEnumerable<DcuRow> registered, IEnumerable<DcuRow> bordxed)
    {
        var bordxMonths = new Dictionary<string, decimal>();
        foreach (var row in bordxed) bordxMonths[Month(row)] = row.Number("Total") ?? 0m;
        var result = new List<string[]>();
        var carried = 0m;
        foreach (var row in registered)
        {
            var label = DcuReportEngine.YearMonth(row).Trim();
            var total = row.Number("Total") ?? 0m;
            var bordx = bordxMonths.GetValueOrDefault(Month(row));
            var outstanding = carried + total - bordx;
            result.Add([label, Text(carried), Text(total), Text(bordx), Text(outstanding)]);
            carried = outstanding;
        }

        return result;
    }

    private static string Month(DcuRow row) => $"{row.Number("YEAR")}/{row.Number("MONTH")}";

    private static string Text(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
