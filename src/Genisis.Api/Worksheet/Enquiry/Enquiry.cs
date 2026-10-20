using System.Text.RegularExpressions;
using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Worksheet.Enquiry;

/// <summary>The criteria of the Search button (frmCASList): what the text is, and the text.</summary>
public class EnquiryCaseSearch
{
    /// <summary>What <see cref="Q"/> is: number (case number), member, patient, ic or policy.</summary>
    public string? By { get; set; } = "number";

    /// <summary>The text to look for.</summary>
    public string? Q { get; set; }
}

/// <summary>Which worksheet to show: the version selection and the claim type filter (Both, Cashless, Reimbursement).</summary>
public class EnquiryQuery
{
    /// <summary>n for one version, Mn for versions 1 to n added together, Master for all of them; blank means version 1.</summary>
    public string? Version { get; set; }

    /// <summary>B both, C cashless (pay to hospital), R reimbursement (pay to member); blank means both. Filters the invoice list only.</summary>
    public string? ClaimType { get; set; }
}

/// <summary>How a version selection reads.</summary>
public enum EnquiryVersionKind
{
    /// <summary>One version: the worksheet of that version alone.</summary>
    Single,

    /// <summary>Mn: versions 1 to n added together.</summary>
    Cumulative,

    /// <summary>Master: every version added together.</summary>
    Master,
}

/// <summary>A parsed version selection.</summary>
/// <param name="Kind">Single, cumulative or master.</param>
/// <param name="Number">The version number (the n of n / Mn); 0 for Master.</param>
public readonly record struct EnquirySelection(EnquiryVersionKind Kind, int Number)
{
    /// <summary>The selection as the version drop-down spells it.</summary>
    public string Text => Kind switch { EnquiryVersionKind.Master => "Master", EnquiryVersionKind.Cumulative => $"M{Number}", _ => Number.ToString() };
}

/// <summary>The rules and wording of the enquiry screen, apart from the database.</summary>
public static partial class EnquiryRules
{
    /// <summary>Search choices the Search dialog offers.</summary>
    public static readonly string[] SearchKinds = ["number", "member", "patient", "ic", "policy"];

    /// <summary>Claim type choices (cboClaimType: Both, C - Cashless, R - Reimbursement).</summary>
    public static readonly string[] ClaimTypes = ["B", "C", "R"];

    [GeneratedRegex(@"^(?:(?<n>\d{1,3})|M(?<m>\d{1,3})|(?<master>Master))$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    /// <summary>Reads a version selection (n, Mn or Master); null when the text is none of them.</summary>
    public static EnquirySelection? ParseVersion(string? text)
    {
        var m = VersionPattern().Match((text ?? "").Trim());
        if (!m.Success) return null;
        if (m.Groups["master"].Success) return new EnquirySelection(EnquiryVersionKind.Master, 0);
        return m.Groups["m"].Success
            ? new EnquirySelection(EnquiryVersionKind.Cumulative, int.Parse(m.Groups["m"].Value))
            : new EnquirySelection(EnquiryVersionKind.Single, int.Parse(m.Groups["n"].Value));
    }

    /// <summary>The invoice PayTo code a claim type keeps (cashless H, reimbursement M); null keeps both.</summary>
    public static string? PayTo(string? claimType) => claimType?.Trim().ToUpperInvariant() switch { "C" => "H", "R" => "M", _ => null };

    private static readonly Dictionary<string, string> Claimabilities = new()
    {
        ["AA"] = "Accepted", ["RR"] = "Repudiated", ["SA"] = "Special Approval", ["CC"] = "Cancelled", ["PP"] = "Pay & Claim", ["NN"] = "Not Decided",
        ["NA"] = "Not Decided/Accepted", ["NR"] = "Not Decided/Rejected", ["PA"] = "Pay & Claim/Accepted", ["PR"] = "Pay & Claim/Rejected",
        ["AR"] = "Accepted/Rejected", ["RA"] = "Rejected/Accepted", ["BR"] = "BTBG/Rejected",
    };

    private static readonly Dictionary<string, string> WorksheetStatuses = new()
    {
        ["AA"] = "Accepted", ["RR"] = "Rejected", ["CC"] = "Cancelled", ["SA"] = "Special Approval", ["PP"] = "Pay & Claim", ["NN"] = "Not Decided",
        ["NA"] = "Not Decided/Accepted", ["NR"] = "Not Decided/Rejected", ["PA"] = "Pay&Claim/Accepted", ["PR"] = "Pay&Claim/Rejected",
        ["AR"] = "Accepted/Rejected", ["RA"] = "Rejected/Accepted", ["BR"] = "BTBG/Rejected", ["XR"] = "Recovery/Rejected", ["AI"] = "Accepted/Insurance",
    };

    /// <summary>lblClaimability: the case's claimability code in words; null for a code the desktop did not name.</summary>
    public static string? ClaimabilityText(string? code) => Claimabilities.GetValueOrDefault((code ?? "").Trim());

    /// <summary>cboWSStatus: "AA - Accepted" style text of a worksheet status; null when the code is blank or unknown.</summary>
    public static string? WorksheetStatusText(string? code)
    {
        var c = (code ?? "").Trim();
        return WorksheetStatuses.TryGetValue(c, out var text) ? $"{c} - {text}" : null;
    }

    /// <summary>lblCASStatus: Open, Close or Repudiated.</summary>
    public static string? CaseStatusText(string? code) => (code ?? "").Trim().ToUpperInvariant() switch { "O" => "Open", "C" => "Close", "R" => "Repudiated", _ => null };

    /// <summary>The benefit's name; for payor IB the desktop renamed row 24 "Maternity" and row 16 "Psychiatric".</summary>
    public static string BenefitName(int slot, string name, string? payor) =>
        string.Equals(payor?.Trim(), "IB", StringComparison.OrdinalIgnoreCase) ? slot switch { 24 => "Maternity", 16 => "Psychiatric", _ => name } : name;

    /// <summary>The column totals of the benefit rows.</summary>
    public static EnquiryTotals Totals(IEnumerable<EnquiryBenefit> rows)
    {
        var list = rows.ToList();
        return new EnquiryTotals
        {
            Actual = list.Sum(r => r.Actual ?? 0), Approved = list.Sum(r => r.Approved ?? 0),
            Hospital = list.Sum(r => r.Hospital ?? 0), Member = list.Sum(r => r.Member ?? 0),
        };
    }

    /// <summary>LGListing: B or L, the case number, a dash and the letter number.</summary>
    public static string LgReference(string? status, string caseNo, int lgNo) => $"{((status ?? "").Trim() == "B" ? "B" : "L")}{caseNo.Trim()}-{lgNo}";

    /// <summary>Messages the desktop showed after loading a case.</summary>
    public static List<string> Notices(EnquiryHeader header) => (header.GlIssuance ?? "").Trim() == "N" ? ["GL is NOT allowed for this member"] : [];

    /// <summary>Display_Diagnosis: a line with no diagnosis, on-set date or ICD code is not listed.</summary>
    public static bool HasDiagnosis(EnquiryDiagnosis d) => !string.IsNullOrWhiteSpace(d.Diagnosis) || !string.IsNullOrWhiteSpace(d.OnsetDate) || !string.IsNullOrWhiteSpace(d.IcdCode);
}

/// <summary>The checks of the Search button.</summary>
public sealed class EnquiryCaseSearchValidator : RequestValidator<EnquiryCaseSearch>
{
    /// <summary>Declares the rules.</summary>
    public EnquiryCaseSearchValidator()
    {
        RuleFor(s => s.By).Must(b => string.IsNullOrWhiteSpace(b) || EnquiryRules.SearchKinds.Contains(b.Trim().ToLowerInvariant()))
            .WithMessage("Search by number, member, patient, ic or policy.");
        RuleFor(s => s.Q).Must(q => (q ?? "").Trim().Length >= 2).WithMessage("Enter at least 2 characters.");
    }
}

/// <summary>The checks of the version and claim type selection.</summary>
public sealed class EnquiryQueryValidator : RequestValidator<EnquiryQuery>
{
    /// <summary>Declares the rules.</summary>
    public EnquiryQueryValidator()
    {
        RuleFor(q => q.Version).Must(v => string.IsNullOrWhiteSpace(v) || EnquiryRules.ParseVersion(v) is not null)
            .WithMessage("Please select a version nombor!");
        RuleFor(q => q.ClaimType).Must(c => string.IsNullOrWhiteSpace(c) || EnquiryRules.ClaimTypes.Contains(c.Trim().ToUpperInvariant()))
            .WithMessage("Select Both, Cashless or Reimbursement.");
    }
}

/// <summary>Reads for the enquiry screen; every method takes the caller's session.</summary>
public interface IEnquiryRepository
{
    /// <summary>The cases the Search button lists.</summary>
    Task<List<EnquiryCaseRow>> SearchCasesAsync(IDbSession session, EnquiryCaseSearch search, int max);

    /// <summary>The worksheet versions of a case.</summary>
    Task<List<EnquiryVersion>> VersionsAsync(IDbSession session, string caseNo);

    /// <summary>The header of one version; null when the worksheet, its case or its member does not exist.</summary>
    Task<EnquiryHeader?> HeaderAsync(IDbSession session, string caseNo, int version);

    /// <summary>The first and final diagnoses of a case.</summary>
    Task<List<EnquiryDiagnosis>> DiagnosesAsync(IDbSession session, string caseNo);

    /// <summary>The invoices of one version (or all accepted ones when the version is null), optionally of one pay-to kind.</summary>
    Task<List<EnquiryInvoice>> InvoicesAsync(IDbSession session, string caseNo, int? version, string? payTo);

    /// <summary>The benefit rows summed over a range of versions.</summary>
    Task<List<EnquiryBenefit>> BenefitsAsync(IDbSession session, string caseNo, int fromVersion, int toVersion);

    /// <summary>The saved summary figures added up over a range of versions.</summary>
    Task<EnquirySummary> SummaryAsync(IDbSession session, string caseNo, int fromVersion, int toVersion);

    /// <summary>Remarks, payee address and payment details of one version.</summary>
    Task<EnquiryOthers?> OthersAsync(IDbSession session, string caseNo, int version);

    /// <summary>The guarantee letters of a case.</summary>
    Task<List<EnquiryLgRow>> LgListAsync(IDbSession session, string caseNo);
}

/// <inheritdoc />
public sealed class EnquiryRepository : IEnquiryRepository
{
    /// <inheritdoc />
    public Task<List<EnquiryCaseRow>> SearchCasesAsync(IDbSession session, EnquiryCaseSearch search, int max)
    {
        var q = (search.Q ?? "").Trim();
        return session.Connection.ProcQueryAsync<EnquiryCaseRow>("genisis.WorksheetEnquiry_Cases", new
        {
            max, by = (search.By ?? "number").Trim().ToLowerInvariant(), q, prefix = SqlText.EscapeLike(q) + "%", contains = "%" + SqlText.EscapeLike(q) + "%",
        }, session.Transaction);
    }

    /// <inheritdoc />
    public Task<List<EnquiryVersion>> VersionsAsync(IDbSession session, string caseNo) =>
        session.Connection.ProcQueryAsync<EnquiryVersion>("genisis.WorksheetEnquiry_Versions", new { cas = caseNo }, session.Transaction);

    /// <inheritdoc />
    public Task<EnquiryHeader?> HeaderAsync(IDbSession session, string caseNo, int version) =>
        session.Connection.ProcFirstOrDefaultAsync<EnquiryHeader>("genisis.WorksheetEnquiry_Header", new { cas = caseNo, version }, session.Transaction);

    /// <inheritdoc />
    public Task<List<EnquiryDiagnosis>> DiagnosesAsync(IDbSession session, string caseNo) =>
        session.Connection.ProcQueryAsync<EnquiryDiagnosis>("genisis.WorksheetEnquiry_Diagnoses", new { cas = caseNo }, session.Transaction);

    /// <inheritdoc />
    public Task<List<EnquiryInvoice>> InvoicesAsync(IDbSession session, string caseNo, int? version, string? payTo) =>
        session.Connection.ProcQueryAsync<EnquiryInvoice>("genisis.WorksheetEnquiry_Invoices", new { cas = caseNo, version, payTo }, session.Transaction);

    /// <inheritdoc />
    public Task<List<EnquiryBenefit>> BenefitsAsync(IDbSession session, string caseNo, int fromVersion, int toVersion) =>
        session.Connection.ProcQueryAsync<EnquiryBenefit>("genisis.WorksheetEnquiry_Benefits", new { cas = caseNo, fromVersion, toVersion }, session.Transaction);

    /// <inheritdoc />
    public Task<EnquirySummary> SummaryAsync(IDbSession session, string caseNo, int fromVersion, int toVersion) =>
        session.Connection.ProcSingleAsync<EnquirySummary>("genisis.WorksheetEnquiry_Summary", new { cas = caseNo, fromVersion, toVersion }, session.Transaction);

    /// <inheritdoc />
    public Task<EnquiryOthers?> OthersAsync(IDbSession session, string caseNo, int version) =>
        session.Connection.ProcFirstOrDefaultAsync<EnquiryOthers>("genisis.WorksheetEnquiry_Others", new { cas = caseNo, version }, session.Transaction);

    /// <inheritdoc />
    public Task<List<EnquiryLgRow>> LgListAsync(IDbSession session, string caseNo) =>
        session.Connection.ProcQueryAsync<EnquiryLgRow>("genisis.CaseAdj_LGList", new { cas = caseNo }, session.Transaction);
}

/// <summary>Worksheet > Worksheet Enquiry (legacy frmWorksheetEnq): shows a saved worksheet; nothing is changed.</summary>
public class EnquiryService(IDbSessions sessions, IEnquiryRepository repository, ILogger<EnquiryService> log)
{
    /// <summary>Most cases the Search button lists.</summary>
    public const int MaxCases = 200;

    private const string NotFound = "Record not found!";

    /// <summary>cmdSearchCase_Click: the cases that match the text.</summary>
    public async Task<List<EnquiryCaseRow>> SearchCasesAsync(EnquiryCaseSearch search)
    {
        await using var session = await sessions.OpenAsync();
        return await repository.SearchCasesAsync(session, search, MaxCases);
    }

    /// <summary>
    /// cmdDisplay_Click: the header, invoices, benefit grid, remarks, diagnoses and guarantee letters of a case at one version
    /// selection. Throws <see cref="NotFoundException"/> ("Record not found!") when the case has no such worksheet.
    /// </summary>
    public async Task<EnquiryResult> GetAsync(string caseNo, EnquiryQuery query)
    {
        var cas = caseNo.Trim();
        await using var session = await sessions.OpenAsync();
        var versions = await repository.VersionsAsync(session, cas);
        if (versions.Count == 0) throw new NotFoundException(NotFound);

        var numbers = versions.Select(v => v.ClaimVersion).ToList();
        var selection = EnquiryRules.ParseVersion(query.Version) ?? new EnquirySelection(EnquiryVersionKind.Single, 1);
        if (selection.Kind == EnquiryVersionKind.Single && string.IsNullOrWhiteSpace(query.Version) && !numbers.Contains(selection.Number))
            selection = new EnquirySelection(EnquiryVersionKind.Single, numbers.Min());
        if (selection.Kind != EnquiryVersionKind.Master && !numbers.Contains(selection.Number)) throw new NotFoundException(NotFound);

        var headerVersion = selection.Kind == EnquiryVersionKind.Master ? numbers.Max() : selection.Number;
        var header = await repository.HeaderAsync(session, cas, headerVersion) ?? throw new NotFoundException(NotFound);
        header.ClaimabilityText = EnquiryRules.ClaimabilityText(header.Claimability);
        header.CaseStatusText = EnquiryRules.CaseStatusText(header.CaseStatus);
        header.WorksheetStatusText = EnquiryRules.WorksheetStatusText(header.WsStatus);

        var (from, to) = selection.Kind switch
        {
            EnquiryVersionKind.Single => (selection.Number, selection.Number),
            EnquiryVersionKind.Cumulative => (numbers.Min(), selection.Number),
            _ => (numbers.Min(), numbers.Max()),
        };
        var invoices = await repository.InvoicesAsync(session, cas, selection.Kind == EnquiryVersionKind.Single ? selection.Number : null, EnquiryRules.PayTo(query.ClaimType));
        var benefits = (await repository.BenefitsAsync(session, cas, from, to))
            .Select(b => b with { Benefit = EnquiryRules.BenefitName(b.Slot, b.Benefit, header.Payor) }).ToList();
        var summary = await repository.SummaryAsync(session, cas, from, to);
        var diagnoses = (await repository.DiagnosesAsync(session, cas)).Where(EnquiryRules.HasDiagnosis).ToList();
        var lgs = (await repository.LgListAsync(session, cas)).Select(l => l with { Reference = EnquiryRules.LgReference(l.LgStatus, l.CaseNo, l.LgNo) }).ToList();
        var others = await repository.OthersAsync(session, cas, headerVersion);

        log.LogInformation("Worksheet enquiry {Case} {Version} ({Benefits} benefit rows, {Invoices} invoices)", cas, selection.Text, benefits.Count, invoices.Count);
        return new EnquiryResult(header, versions, selection.Text, diagnoses, invoices, benefits, EnquiryRules.Totals(benefits), summary, others, lgs, EnquiryRules.Notices(header));
    }
}
