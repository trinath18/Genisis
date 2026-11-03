using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Genisis.Api.Authorize.Dcu;

/// <summary>One column of a report: its heading as the desktop's list showed it and how a row fills it.</summary>
public sealed class DcuColumnDefinition
{
    /// <summary>The column heading.</summary>
    public string Header { get; set; } = "";

    /// <summary>True for a column the desktop right-aligned (numbers).</summary>
    public bool Right { get; set; }

    /// <summary>text, money, date, claimNo, yearMonth, ratio, duration, icd, notYetBordx, bordxed or efficiency.</summary>
    public string Kind { get; set; } = "text";

    /// <summary>The field of the query row the column shows (the ICD code of an icd column).</summary>
    public string? Field { get; set; }

    /// <summary>text: the label shown when the field is empty.</summary>
    public string? Empty { get; set; }

    /// <summary>text: the desktop trimmed the value.</summary>
    public bool Trim { get; set; }

    /// <summary>money: plain (#,##0.00) or paren (#,##0.00;(#,##0.00)).</summary>
    public string? Format { get; set; }

    /// <summary>money: the text shown when the field is empty.</summary>
    public string? Zero { get; set; }

    /// <summary>money: only a negative value is shown (as a figure), otherwise Zero.</summary>
    public bool NegativeOnly { get; set; }

    /// <summary>ratio: the numerator field.</summary>
    public string? Num { get; set; }

    /// <summary>ratio: the denominator field.</summary>
    public string? Den { get; set; }
}

/// <summary>One report: a Claim or Case list of one report type and selection (a routine of the desktop form).</summary>
public sealed class DcuReportDefinition
{
    /// <summary>claim:typeIndex:selectionIndex.</summary>
    public string Id { get; set; } = "";

    /// <summary>claim or case.</summary>
    public string Mode { get; set; } = "claim";

    /// <summary>Index of the report type (General Listing 0 ... Efficiency 9).</summary>
    public int TypeIndex { get; set; }

    /// <summary>The report type.</summary>
    public string Type { get; set; } = "";

    /// <summary>Index of the selection; null for a type with none (Payor, State).</summary>
    public int? SelectionIndex { get; set; }

    /// <summary>The selection (Report Selection box).</summary>
    public string? Selection { get; set; }

    /// <summary>The desktop routine the report was written in (for the migration notes).</summary>
    public string Routine { get; set; } = "";

    /// <summary>The case of genisis.DcuReport_Run that gives the rows.</summary>
    public int Case { get; set; }

    /// <summary>Case reports: the case that gives the duration of stay of each group (DurationStay).</summary>
    public int? DurationCase { get; set; }

    /// <summary>The fields that tell a group of the duration case from another.</summary>
    public List<string> DurationKeys { get; set; } = [];

    /// <summary>The measure of a ranked list when it is not the default (Total, TotalApproved, TotalCases).</summary>
    public string? OrderBy { get; set; }

    /// <summary>DIR, BORDX, INV, BORDXINV or OS for an efficiency report.</summary>
    public string? Efficiency { get; set; }

    /// <summary>The columns, in the desktop's order.</summary>
    public List<DcuColumnDefinition> Columns { get; set; } = [];
}

/// <summary>A report type with the selections it has for Claim and for Case.</summary>
/// <param name="Index">Index of the type.</param>
/// <param name="Name">The type.</param>
/// <param name="Claim">Selections of the Claim report (empty when the type has none).</param>
/// <param name="Case">Selections of the Case report; null when the type is not available for Case (Efficiency).</param>
public record DcuReportType(int Index, string Name, List<string> Claim, List<string>? Case);

/// <summary>The 88 reports of the form: Claim and Case by report type and selection, read from the catalog the migration extracted from the desktop's routines.</summary>
public sealed class DcuReportCatalog
{
    private static readonly Lazy<DcuReportCatalog> Shared = new(Load);

    /// <summary>The catalog (read once).</summary>
    public static DcuReportCatalog Instance => Shared.Value;

    /// <summary>Every report.</summary>
    public IReadOnlyList<DcuReportDefinition> Reports { get; }

    private readonly Dictionary<string, DcuReportDefinition> byKey;

    /// <summary>Builds the lookup of the reports.</summary>
    public DcuReportCatalog(IReadOnlyList<DcuReportDefinition> reports)
    {
        Reports = reports;
        byKey = reports.ToDictionary(r => Key(r.Mode, r.TypeIndex, r.SelectionIndex));
    }

    private static string Key(string mode, int type, int? selection) => $"{mode}:{type}:{(selection is { } s ? s.ToString() : "-")}";

    /// <summary>The report of a mode, type and selection: no selection is the first, as the desktop's box showed it (a type with no selections ignores the selection); null when there is none.</summary>
    public DcuReportDefinition? Find(string? mode, int type, int? selection) =>
        mode is "claim" or "case"
            ? byKey.GetValueOrDefault(Key(mode, type, selection)) ?? (selection is null ? byKey.GetValueOrDefault(Key(mode, type, 0)) : null) ?? byKey.GetValueOrDefault(Key(mode, type, null))
            : null;

    /// <summary>The report types with their selections (cboReportType, cboReportSelection).</summary>
    public List<DcuReportType> Types() =>
        Reports.GroupBy(r => r.TypeIndex).OrderBy(g => g.Key).Select(g =>
        {
            List<string> Selections(string mode) => g.Where(r => r.Mode == mode && r.Selection is not null).OrderBy(r => r.SelectionIndex).Select(r => r.Selection!).ToList();
            return new DcuReportType(g.Key, g.First().Type, Selections("claim"), g.Any(r => r.Mode == "case") ? Selections("case") : null);
        }).ToList();

    private static DcuReportCatalog Load()
    {
        using var stream = typeof(DcuReportCatalog).Assembly.GetManifestResourceStream("Genisis.Api.Authorize.Dcu.DcuReportCatalog.json")
            ?? throw new InvalidOperationException("The DCU report catalog is not embedded in the assembly.");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
        return new DcuReportCatalog(JsonSerializer.Deserialize<List<DcuReportDefinition>>(stream, options) ?? []);
    }

    /// <summary>The assembly of the catalog (for tests).</summary>
    public static Assembly Assembly => typeof(DcuReportCatalog).Assembly;
}
