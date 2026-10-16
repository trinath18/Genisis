using System.Globalization;

namespace Genisis.Api.Tracking;

public interface ITrackingService
{
    Task<TrackingMeta> GetMetaAsync(CancellationToken cancellationToken);

    /// <param name="rowLimit">Rows to return (the totals always cover every row); <c>null</c> returns all rows.</param>
    Task<TrackingResult> RunAsync(TrackingQuery query, int? rowLimit, CancellationToken cancellationToken);

    Task<TrackingExport> ExportAsync(TrackingQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// Membership &gt; Tracking (legacy FrmNewMBMTracking). Runs the report's MBMTracking case(s) through <see cref="ITrackingRepository"/>
/// and combines principal and supplementary results the way the desktop's listing routines did.
/// </summary>
public sealed class TrackingService(ITrackingRepository repository) : ITrackingService
{
    private sealed record CountColumn(string Label, int Case, string CountFilter, string CountSource, string AmountSource, bool Principals);

    private static readonly CountColumn[] BordxMcoColumns =
    [
        new("Ind Child", 39, TrackingCountFilters.IndividualChild, "TotalIC", "MCOFEE", true),
        new("Ind Adult", 40, TrackingCountFilters.IndividualAdult, "TotalIA", "MCOFEE", true),
        new("Family", 41, TrackingCountFilters.Family, "TotalF", "MCOFEE", true),
        new("Grp Ind Child", 42, TrackingCountFilters.GroupIndividualChild, "TotalGC", "MCOFEE", true),
        new("Grp Ind Adult", 43, TrackingCountFilters.GroupIndividualAdult, "TotalGA", "MCOFEE", true),
        new("Grp Family", 44, TrackingCountFilters.GroupFamily, "TotalH", "MCOFEE", true),
        new("Family Supp", 45, TrackingCountFilters.Family, "TotalFS", "MCOFee", false),
        new("Grp Family Supp", 46, TrackingCountFilters.GroupFamily, "TotalHS", "MCOFee", false),
    ];

    private static readonly CountColumn[] BordxPremiumColumns =
    [
        new("Ind Child", 48, TrackingCountFilters.IndividualChild, "TotalIC", "Premium", true),
        new("Ind Adult", 49, TrackingCountFilters.IndividualAdult, "TotalIA", "Premium", true),
        new("Family", 50, TrackingCountFilters.Family, "TotalF", "Premium", true),
        new("Family Supp", 51, TrackingCountFilters.Family, "TotalFS", "Premium", false),
        new("Grp Ind Child", 52, TrackingCountFilters.GroupIndividualChild, "TotalGC", "Premium", true),
        new("Grp Ind Adult", 53, TrackingCountFilters.GroupIndividualAdult, "TotalGA", "Premium", true),
        new("Grp Family", 54, TrackingCountFilters.GroupFamily, "TotalH", "Premium", true),
        new("Grp Family Supp", 55, TrackingCountFilters.GroupFamily, "TotalHS", "Premium", false),
    ];

    /// <summary>MCO invoice "INS Type &amp; MBM Type": the same principal / supplementary case filtered per insured type.</summary>
    private static CountColumn[] InvoiceColumns(int principalCase, int supplementaryCase, string amountSource) =>
    [
        new("Ind Child", principalCase, TrackingCountFilters.IndividualChild, "TotalMBM", amountSource, true),
        new("Ind Adult", principalCase, TrackingCountFilters.IndividualAdult, "TotalMBM", amountSource, true),
        new("Family", principalCase, TrackingCountFilters.Family, "TotalMBM", amountSource, true),
        new("Family Supp", supplementaryCase, TrackingCountFilters.Family, "TotalMBM", amountSource, false),
        new("Grp Ind Child", principalCase, TrackingCountFilters.GroupIndividualChild, "TotalMBM", amountSource, true),
        new("Grp Ind Adult", principalCase, TrackingCountFilters.GroupIndividualAdult, "TotalMBM", amountSource, true),
        new("Grp Family", principalCase, TrackingCountFilters.GroupFamily, "TotalMBM", amountSource, true),
        new("Grp Family Supp", supplementaryCase, TrackingCountFilters.GroupFamily, "TotalMBM", amountSource, false),
    ];

    private static readonly IReadOnlyDictionary<string, CountColumn[]> MultiReports = new Dictionary<string, CountColumn[]>
    {
        [TrackingReportKeys.BordxMco] = BordxMcoColumns,
        [TrackingReportKeys.BordxPremium] = BordxPremiumColumns,
        [TrackingReportKeys.InvoiceMco] = InvoiceColumns(132, 133, "MCOFEE"),
        [TrackingReportKeys.InvoicePremium] = InvoiceColumns(134, 135, "Premium"),
    };

    private static readonly string[] MoneyTotalColumns = [TrackingColumns.Mco, TrackingColumns.SupplementaryMco, "MCOFEE", "MBMMCOFee"];
    private static readonly string[] PremiumTotalColumns = [TrackingColumns.Premium, TrackingColumns.SupplementaryPremium, "Premium", "MBMPremium", BranchByAgentColumns.TotalPremium];
    private static readonly string[] AccessFeeTotalColumns = [TrackingColumns.AccessFee, "MBMAccessFee"];

    public async Task<TrackingMeta> GetMetaAsync(CancellationToken cancellationToken)
    {
        var lookups = await repository.GetLookupsAsync(cancellationToken);
        var fields = TrackingCatalog.Fields.Select(f => f.Key switch
        {
            TrackingFieldKeys.Payor => f with { Options = lookups.Payors.Select(p => new TrackingOption(p.Code, p.Name)).ToList() },
            TrackingFieldKeys.State => f with { Options = lookups.States },
            TrackingFieldKeys.ProductCategory => f with { Options = lookups.ProductCategories },
            _ => f,
        }).ToList();
        var reports = TrackingCatalog.Reports.Select(r => new TrackingReportInfo(r.Key, r.TypeIndex, r.Type, r.Selection, r.Kind.ToString())).ToList();
        return new TrackingMeta(reports, fields, lookups.Payors.ToDictionary(p => p.Code, p => p.HealthCode));
    }

    public async Task<TrackingResult> RunAsync(TrackingQuery query, int? rowLimit, CancellationToken cancellationToken)
    {
        var rows = query.Report.Kind switch
        {
            ReportKind.Detail => await DetailAsync(query, cancellationToken),
            ReportKind.Joined => await JoinedAsync(query, cancellationToken),
            ReportKind.Appended => await AppendedAsync(query, cancellationToken),
            _ when MultiReports.TryGetValue(query.Report.Key, out var columns) => await CountColumnsAsync(query, columns, cancellationToken),
            _ => await BranchByAgentAsync(query, cancellationToken),
        };
        var totals = Totals(query.Report, rows);
        var truncated = rowLimit is { } limit && rows.Count > limit;
        IReadOnlyList<IReadOnlyDictionary<string, object?>> shown = truncated ? rows.Take(rowLimit!.Value).ToList() : rows;
        var columnNames = rows.SelectMany(r => r.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return new TrackingResult(columnNames, shown, totals, truncated);
    }

    public async Task<TrackingExport> ExportAsync(TrackingQuery query, CancellationToken cancellationToken)
    {
        var result = await RunAsync(query, rowLimit: null, cancellationToken);
        return new TrackingExport(TrackingCsvWriter.FileName(query.Report), TrackingCsvWriter.Write(result));
    }

    private Task<List<Dictionary<string, object?>>> CaseAsync(TrackingQuery query, int caseNumber, CancellationToken cancellationToken,
        TrackingCaseOptions? options = null) =>
        repository.RunCaseAsync(query, caseNumber, options ?? PrincipalNameOptions(query), cancellationToken);

    /// <summary>General Listing (Prin. Name) always narrows member types; other reports only in their detail listings.</summary>
    private static TrackingCaseOptions PrincipalNameOptions(TrackingQuery query) => query.Report.PrincipalNameFilter
        ? new TrackingCaseOptions(MemberTypeFilters.PrincipalName(query.MemberType, query.PrincipalType))
        : new TrackingCaseOptions();

    private static TrackingCaseOptions DetailOptions(TrackingQuery query, bool supplementary)
    {
        if (query.Report.PrincipalNameFilter) return PrincipalNameOptions(query) with { WithSupplementaryRegistrationDate = supplementary };
        var memberFilter = query.PrincipalType switch
        {
            PrincipalTypeSelection.CombinedLimit => MemberTypeFilters.DetailCombined,
            PrincipalTypeSelection.SeparateLimit => MemberTypeFilters.DetailSeparate,
            _ => null,
        };
        return new TrackingCaseOptions(memberFilter, WithSupplementaryRegistrationDate: supplementary);
    }

    private static List<Dictionary<string, object?>> Tag(List<Dictionary<string, object?>> rows, string rowType) =>
        rows.Select(row =>
        {
            var tagged = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { [TrackingColumns.RowType] = rowType };
            foreach (var (key, value) in row) tagged[key] = value;
            return tagged;
        }).ToList();

    /// <summary>GeneralListing / ICListing / CheckDate / ...: principal case, supplementary-only case, or both (desktop MBM type).</summary>
    private async Task<List<Dictionary<string, object?>>> DetailAsync(TrackingQuery query, CancellationToken cancellationToken)
    {
        var report = query.Report;
        if (report.SupplementaryCase == report.PrincipalCase) return await CaseAsync(query, report.PrincipalCase, cancellationToken);

        async Task<List<Dictionary<string, object?>>> Principals() =>
            Tag(await CaseAsync(query, report.PrincipalCase, cancellationToken, DetailOptions(query, supplementary: false)), TrackingRowTypes.Principal);
        async Task<List<Dictionary<string, object?>>> Supplementaries(int caseNumber) =>
            Tag(await CaseAsync(query, caseNumber, cancellationToken, DetailOptions(query, supplementary: true)), TrackingRowTypes.Supplementary);

        return query.MemberType switch
        {
            MemberTypeSelection.Principal => await Principals(),
            MemberTypeSelection.Supplementary => await Supplementaries(report.SupplementaryCase),
            _ => [.. await Principals(), .. await Supplementaries(report.BothSupplementaryCase ?? report.SupplementaryCase)],
        };
    }

    /// <summary>Summaries where each principal row carries the supplementary count / MCO / premium of the same group (desktop CountSupp* routines).</summary>
    private async Task<List<Dictionary<string, object?>>> JoinedAsync(TrackingQuery query, CancellationToken cancellationToken)
    {
        var report = query.Report;
        if (query.MemberType == MemberTypeSelection.Supplementary)
            return Tag(await CaseAsync(query, report.SupplementaryCase, cancellationToken), TrackingRowTypes.Supplementary);
        var principals = await CaseAsync(query, report.PrincipalCase, cancellationToken);
        if (query.MemberType == MemberTypeSelection.Principal) return principals;

        var supplementaries = await CaseAsync(query, report.SupplementaryCase, cancellationToken);
        var (principalKeys, supplementaryKeys) = TrackingRowMatcher.JoinKeys(report);
        var bySupplementaryKey = supplementaries.ToLookup(s => TrackingRowMatcher.Key(s, supplementaryKeys));
        foreach (var row in principals)
        {
            var matches = bySupplementaryKey[TrackingRowMatcher.Key(row, principalKeys)].ToList();
            row[TrackingColumns.SupplementaryCount] = TrackingRowMatcher.Sum(matches, TrackingColumns.SupplementaryCount);
            row[TrackingColumns.SupplementaryMco] = TrackingRowMatcher.Sum(matches, TrackingColumns.Mco);
            row[TrackingColumns.SupplementaryPremium] = TrackingRowMatcher.Sum(matches, TrackingColumns.Premium);
        }
        return principals;
    }

    /// <summary>Summaries that list supplementary rows after the principal rows (desktop CountSuppRecords / AGE / Gender ...).</summary>
    private async Task<List<Dictionary<string, object?>>> AppendedAsync(TrackingQuery query, CancellationToken cancellationToken)
    {
        var rows = new List<Dictionary<string, object?>>();
        if (query.MemberType != MemberTypeSelection.Supplementary)
            rows.AddRange(Tag(await CaseAsync(query, query.Report.PrincipalCase, cancellationToken), TrackingRowTypes.Principal));
        if (query.MemberType != MemberTypeSelection.Principal)
            rows.AddRange(Tag(await CaseAsync(query, query.Report.SupplementaryCase, cancellationToken), TrackingRowTypes.Supplementary));
        return rows;
    }

    /// <summary>Bordx / MCO invoice "INS Type &amp; MBM Type": each row gets a count and amount per insured / member type.</summary>
    private async Task<List<Dictionary<string, object?>>> CountColumnsAsync(TrackingQuery query, CountColumn[] columns, CancellationToken cancellationToken)
    {
        var baseCase = query.MemberType == MemberTypeSelection.Supplementary ? query.Report.SupplementaryCase : query.Report.PrincipalCase;
        var rows = await CaseAsync(query, baseCase, cancellationToken);
        var keys = TrackingCaseKeys.All[baseCase];
        foreach (var column in columns)
        {
            var included = column.Principals ? query.MemberType != MemberTypeSelection.Supplementary : query.MemberType != MemberTypeSelection.Principal;
            var lookup = included
                ? (await CaseAsync(query, column.Case, cancellationToken, new TrackingCaseOptions(CountFilter: column.CountFilter)))
                    .ToLookup(r => TrackingRowMatcher.Key(r, TrackingCaseKeys.All[column.Case]))
                : null;
            foreach (var row in rows)
            {
                var matches = lookup?[TrackingRowMatcher.Key(row, keys)].ToList() ?? [];
                row[column.Label + " Count"] = TrackingRowMatcher.Sum(matches, column.CountSource);
                row[column.Label + " Amount"] = TrackingRowMatcher.Sum(matches, column.AmountSource);
            }
        }
        return rows;
    }

    /// <summary>Branch &gt; By Agent (desktop BranchByAgent): policies, premium and claims by insured type for each branch / agent.</summary>
    private async Task<List<Dictionary<string, object?>>> BranchByAgentAsync(TrackingQuery query, CancellationToken cancellationToken)
    {
        var principals = query.MemberType != MemberTypeSelection.Supplementary;
        var supplementaries = query.MemberType != MemberTypeSelection.Principal;
        var baseRows = await CaseAsync(query, principals ? query.Report.PrincipalCase : query.Report.SupplementaryCase, cancellationToken);
        string[] branchAgent = [TrackingColumns.Branch, TrackingColumns.Agent];
        var empty = Enumerable.Empty<Dictionary<string, object?>>().ToLookup(_ => "");

        async Task<ILookup<string, Dictionary<string, object?>>> Group(bool included, int caseNumber, string countFilter) => included
            ? (await CaseAsync(query, caseNumber, cancellationToken, new TrackingCaseOptions(CountFilter: countFilter))).ToLookup(r => TrackingRowMatcher.Key(r, branchAgent))
            : empty;

        var individual = await Group(principals, BranchByAgentCases.Principals, TrackingCountFilters.Individual);
        var groupIndividual = await Group(principals, BranchByAgentCases.Principals, TrackingCountFilters.GroupIndividual);
        var family = await Group(principals, BranchByAgentCases.Principals, TrackingCountFilters.Family);
        var familySupplementary = await Group(supplementaries, BranchByAgentCases.Supplementaries, TrackingCountFilters.Family);
        var groupFamily = await Group(principals, BranchByAgentCases.Principals, TrackingCountFilters.GroupFamily);
        var groupFamilySupplementary = await Group(supplementaries, BranchByAgentCases.Supplementaries, TrackingCountFilters.GroupFamily);
        var individualClaims = await Group(principals, BranchByAgentCases.Claims, TrackingCountFilters.Individual);
        var groupIndividualClaims = await Group(principals, BranchByAgentCases.Claims, TrackingCountFilters.GroupIndividual);
        var familyClaims = await Group(true, BranchByAgentCases.Claims, TrackingCountFilters.Family);
        var groupFamilyClaims = await Group(true, BranchByAgentCases.Claims, TrackingCountFilters.GroupFamily);

        return baseRows.Select(row =>
        {
            var key = TrackingRowMatcher.Key(row, branchAgent);
            decimal Sum(ILookup<string, Dictionary<string, object?>> source, string column) => TrackingRowMatcher.Sum(source[key], column);
            return BranchByAgentColumns.Row(
                TrackingRowMatcher.Text(row.GetValueOrDefault(TrackingColumns.Branch)),
                TrackingRowMatcher.Text(row.GetValueOrDefault(TrackingColumns.Agent)),
                counts: [Sum(individual, TrackingColumns.PrincipalCount),
                    Sum(family, TrackingColumns.PrincipalCount) + Sum(familySupplementary, TrackingColumns.SupplementaryCount),
                    Sum(groupFamily, TrackingColumns.PrincipalCount) + Sum(groupFamilySupplementary, TrackingColumns.SupplementaryCount),
                    Sum(groupIndividual, TrackingColumns.PrincipalCount)],
                premiums: [Sum(individual, TrackingColumns.Premium),
                    Sum(family, TrackingColumns.Premium) + Sum(familySupplementary, TrackingColumns.Premium),
                    Sum(groupFamily, TrackingColumns.Premium) + Sum(groupFamilySupplementary, TrackingColumns.Premium),
                    Sum(groupIndividual, TrackingColumns.Premium)],
                claims: [Sum(individualClaims, TrackingColumns.ClaimCount), Sum(familyClaims, TrackingColumns.ClaimCount),
                    Sum(groupFamilyClaims, TrackingColumns.ClaimCount), Sum(groupIndividualClaims, TrackingColumns.ClaimCount)],
                paid: [Sum(individualClaims, TrackingColumns.ClaimApproved), Sum(familyClaims, TrackingColumns.ClaimApproved),
                    Sum(groupFamilyClaims, TrackingColumns.ClaimApproved), Sum(groupIndividualClaims, TrackingColumns.ClaimApproved)]);
        }).ToList();
    }

    /// <summary>The desktop's footer labels: principal / supplementary counts, total, MCO, premium and access fee.</summary>
    internal static TrackingTotals Totals(TrackingReport report, IReadOnlyCollection<Dictionary<string, object?>> rows)
    {
        decimal Sum(params string[] columns) => rows.Sum(r => columns.Sum(c => TrackingRowMatcher.Number(r.GetValueOrDefault(c))));
        decimal principals, supplementaries;
        if (report.Kind == ReportKind.Detail)
        {
            principals = rows.Count(r => (r.GetValueOrDefault(TrackingColumns.RowType) as string ?? TrackingRowTypes.Principal) == TrackingRowTypes.Principal);
            supplementaries = rows.Count - principals;
        }
        else if (report.Key == TrackingReportKeys.BranchByAgent)
        {
            principals = Sum(BranchByAgentColumns.TotalPolicy);
            supplementaries = 0;
        }
        else
        {
            principals = Sum(TrackingColumns.PrincipalCount);
            supplementaries = Sum(TrackingColumns.SupplementaryCount);
        }
        return new TrackingTotals(principals, supplementaries, principals + supplementaries, Sum(MoneyTotalColumns), Sum(PremiumTotalColumns), Sum(AccessFeeTotalColumns));
    }
}

/// <summary>Matching of grouped rows between MBMTracking cases.</summary>
internal static class TrackingRowMatcher
{
    /// <summary>Supplementary key columns matched position by position with the principal case's GROUP BY (AGECode ↔ PrinAGECode, MBMSex ↔ PrinSex, ...).</summary>
    public static (string[] Principal, string[] Supplementary) JoinKeys(TrackingReport report)
    {
        var principal = TrackingCaseKeys.All[report.PrincipalCase];
        var supplementary = TrackingCaseKeys.All[report.SupplementaryCase];
        if (principal.Length == supplementary.Length) return (principal, supplementary);
        var common = principal.Intersect(supplementary, StringComparer.OrdinalIgnoreCase).ToArray();
        return (common, common);
    }

    public static string Key(IReadOnlyDictionary<string, object?> row, IEnumerable<string> columns) =>
        string.Join("|", columns.Select(c => Text(row.GetValueOrDefault(c))));

    public static string Text(object? value) => value switch
    {
        null => "",
        DateTime d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)!.Trim(),
    };

    public static decimal Number(object? value) => value is null or DBNull ? 0 : Convert.ToDecimal(value, CultureInfo.InvariantCulture);

    public static decimal Sum(IEnumerable<IReadOnlyDictionary<string, object?>> rows, string column) => rows.Sum(r => Number(r.GetValueOrDefault(column)));
}

/// <summary>Columns of Branch &gt; By Agent.</summary>
internal static class BranchByAgentColumns
{
    public const string TotalPolicy = "Total Policy";
    public const string TotalPremium = "Total Premium";
    private const string EmptyValue = "Empty";
    private static readonly string[] Types = ["Individual", "Family", "Grp Family", "Grp Individual"];
    private static readonly string[] Short = ["Ind", "Family", "Grp Family", "Grp Ind"];

    public static Dictionary<string, object?> Row(string branch, string agent, decimal[] counts, decimal[] premiums, decimal[] claims, decimal[] paid)
    {
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Branch"] = branch == "" ? EmptyValue : branch,
            ["Agent"] = agent == "" ? EmptyValue : agent,
        };
        for (var i = 0; i < Types.Length; i++) row[Types[i]] = counts[i];
        row[TotalPolicy] = counts.Sum();
        for (var i = 0; i < Short.Length; i++) row[Short[i] + " Premium"] = premiums[i];
        row[TotalPremium] = premiums.Sum();
        for (var i = 0; i < Short.Length; i++) row[Short[i] + " Claims"] = claims[i];
        row["Total Claims"] = claims.Sum();
        for (var i = 0; i < Short.Length; i++) row[Short[i] + " Paid"] = paid[i];
        row["Total Paid"] = paid.Sum();
        var policies = counts.Sum();
        var premium = premiums.Sum();
        row["Claims / Policy"] = policies == 0 ? 0 : Math.Round(claims.Sum() / policies, 0, MidpointRounding.ToEven);
        row["Paid / Premium"] = paid.Sum() != 0 && premium != 0 ? Math.Round(paid.Sum() / premium, 2, MidpointRounding.ToEven) : 0;
        return row;
    }
}
