namespace Genisis.Api.Tracking;

public enum ReportKind
{
    /// <summary>Member-level listing: principal case, supplementary-only case, and the supplementary case used with "Both".</summary>
    Detail,
    /// <summary>Summary where each principal row gets the supplementary count / MCO / premium for the same keys.</summary>
    Joined,
    /// <summary>Summary where supplementary rows are listed after the principal rows, marked (S).</summary>
    Appended,
    /// <summary>Summaries with extra count columns from other cases (Bordx / MCO invoice by insured &amp; member type, branch by agent).</summary>
    Multi,
}

/// <summary>One report of the Tracking screen (CboReportType + CboRptSelection) and the MBMTracking cases behind it.</summary>
public sealed record TrackingReport(string Key, int TypeIndex, string Type, string Selection, ReportKind Kind, int PrincipalCase, int SupplementaryCase,
    int? BothSupplementaryCase = null, bool PrincipalNameFilter = false);

public enum FieldKind { Select, Text, Number, Date }

/// <summary>A filter on the Tracking screen. <see cref="Range"/> fields take from / to; <see cref="Blankable"/> ones have the desktop's
/// "blank" check box; <see cref="BlankOnly"/> ones have nothing else. <see cref="Key"/> is also the genisis.Tracking_Run parameter
/// name (ranges add From / To).</summary>
public sealed record TrackingField(string Key, string Label, string Group, FieldKind Kind, bool Range, bool Blankable,
    IReadOnlyList<TrackingOption>? Options = null, bool BlankOnly = false);

public sealed record TrackingOption(string Code, string Name);

public sealed record PayorOption(string Code, string Name, string? HealthCode);

public sealed record TrackingLookups(List<PayorOption> Payors, List<TrackingOption> States, List<TrackingOption> ProductCategories);

public sealed record TrackingReportInfo(string Key, int TypeIndex, string Type, string Selection, string Kind);

public sealed record TrackingMeta(IReadOnlyList<TrackingReportInfo> Reports, IReadOnlyList<TrackingField> Fields, IReadOnlyDictionary<string, string?> PayorHealth);

public class TrackingRange
{
    public string? From { get; set; }
    public string? To { get; set; }
}

/// <summary>Request DTO of the Tracking screen.</summary>
public class TrackingRequest
{
    public string? Report { get; set; }
    /// <summary>B = both, P = principal, S = supplementary (desktop "MBM Type").</summary>
    public string? MemberType { get; set; } = MemberTypeSelection.Principal;
    /// <summary>Both, C = combined limit, S = separated limit.</summary>
    public string? PrincipalType { get; set; } = "Both";
    public Dictionary<string, string?> Values { get; set; } = [];
    public Dictionary<string, TrackingRange> Ranges { get; set; } = [];
    public List<string> Blanks { get; set; } = [];
}

/// <summary>A validated request: the report, normalised selections and typed filter values (dates as <see cref="DateTime"/>, numbers as decimal).</summary>
public sealed record TrackingQuery(
    TrackingReport Report,
    string MemberType,
    string PrincipalType,
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyDictionary<string, (object? From, object? To)> Ranges,
    IReadOnlyList<string> Blanks);

/// <summary>Per-call options of genisis.Tracking_Run on top of the screen filters.</summary>
public sealed record TrackingCaseOptions(string? MemberTypeFilter = null, string? CountFilter = null, bool WithSupplementaryRegistrationDate = false);

public sealed record TrackingTotals(decimal Principals, decimal Supplementaries, decimal Total, decimal Mco, decimal Premium, decimal AccessFee);

public sealed record TrackingResult(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows, TrackingTotals Totals, bool Truncated);

public sealed record TrackingExport(string FileName, byte[] Content);
