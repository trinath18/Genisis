using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Case.Listing;

/// <summary>One worksheet waiting for a bordx (a row of the FrmWksBordx list).</summary>
public record ListingRow
{
    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Claim version.</summary>
    public int ClaimVersion { get; set; }

    /// <summary>Payor code.</summary>
    public string? Payor { get; set; }

    /// <summary>Approved amount (CLMTotalApproved).</summary>
    public decimal? AppAmt { get; set; }

    /// <summary>Amount payable by Medix to the hospital (CLMPayableByMedix2H).</summary>
    public decimal? HosAmt { get; set; }

    /// <summary>Date of admission.</summary>
    public DateTime? Doa { get; set; }

    /// <summary>Date of discharge.</summary>
    public DateTime? Dod { get; set; }

    /// <summary>Who is paid: H hospital, M member.</summary>
    public string? CashType { get; set; }

    /// <summary>Patient cover ID.</summary>
    public string? CoverId { get; set; }

    /// <summary>Covered person's name.</summary>
    public string? CoveredName { get; set; }

    /// <summary>Principal's name.</summary>
    public string? MemberName { get; set; }

    /// <summary>Hospital name.</summary>
    public string? Hospital { get; set; }

    /// <summary>Group company.</summary>
    public string? GroupCompany { get; set; }

    /// <summary>Plan code.</summary>
    public string? PlanCode { get; set; }

    /// <summary>First ICD code.</summary>
    public string? IcdCode { get; set; }

    /// <summary>BTBG flag.</summary>
    public string? Btbg { get; set; }

    /// <summary>Insured type.</summary>
    public string? InsuredType { get; set; }

    /// <summary>Case claimability.</summary>
    public string? Claimability { get; set; }

    /// <summary>Health type.</summary>
    public string? HltType { get; set; }

    /// <summary>The "Wks No" column: case number - claim version.</summary>
    public string WksNo => $"{CaseNo}-{ClaimVersion}";

    /// <summary>The patient: the principal for cover ID 00, otherwise the covered person.</summary>
    public string? Patient => (CoverId ?? "") == "00" ? MemberName : CoveredName;
}

/// <summary>A pending worksheet as the bordx generation reads it (rule inputs plus the amounts a bordx can sum).</summary>
public record ListingItem
{
    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Claim version.</summary>
    public int ClaimVersion { get; set; }

    /// <summary>Payor code.</summary>
    public string? Payor { get; set; }

    /// <summary>Date of discharge.</summary>
    public DateTime? Dod { get; set; }

    /// <summary>Hospital name.</summary>
    public string? Hospital { get; set; }

    /// <summary>Group company.</summary>
    public string? GroupCompany { get; set; }

    /// <summary>Insured type.</summary>
    public string? InsuredType { get; set; }

    /// <summary>Case claimability.</summary>
    public string? Claimability { get; set; }

    /// <summary>Health type.</summary>
    public string? HltType { get; set; }

    /// <summary>Who is paid: H hospital, M member.</summary>
    public string? CashType { get; set; }

    /// <summary>BTBG flag.</summary>
    public string? Btbg { get; set; }

    /// <summary>Payable by Medix to the hospital.</summary>
    public decimal Payable2H { get; set; }

    /// <summary>Payable by Medix to the member.</summary>
    public decimal Payable2M { get; set; }

    /// <summary>Total approved.</summary>
    public decimal TotalApproved { get; set; }

    /// <summary>Case management fees.</summary>
    public decimal CaseMgmtFees { get; set; }
}

/// <summary>The criteria of the worksheet search.</summary>
public class ListingFilter
{
    /// <summary>"Use Scan": one worksheet number (and version) instead of a range.</summary>
    public bool UseScan { get; set; } = true;

    /// <summary>Worksheet (case) number, or the start of the range.</summary>
    public string? WksNoFrom { get; set; }

    /// <summary>End of the range (range mode only).</summary>
    public string? WksNoTo { get; set; }

    /// <summary>Claim version (scan mode only).</summary>
    public int? Version { get; set; }

    /// <summary>Payor; only its "IB" prefix matters (the IB payor has its own view).</summary>
    public string? Payor { get; set; }

    /// <summary>Date of admission from.</summary>
    public DateTime? DoaFrom { get; set; }

    /// <summary>Date of admission to.</summary>
    public DateTime? DoaTo { get; set; }

    /// <summary>Only worksheets with no admission date.</summary>
    public bool DoaEmpty { get; set; }

    /// <summary>Date of discharge from.</summary>
    public DateTime? DodFrom { get; set; }

    /// <summary>Date of discharge to.</summary>
    public DateTime? DodTo { get; set; }

    /// <summary>Only worksheets with no discharge date.</summary>
    public bool DodEmpty { get; set; }

    /// <summary>Bordx Status "Yes": only worksheets flagged CLMMHBordx.</summary>
    public bool BordxStatusYes { get; set; }
}

/// <summary>A ticked worksheet.</summary>
/// <param name="CaseNo">Case number.</param>
/// <param name="ClaimVersion">Claim version.</param>
public record ListingPick(string CaseNo, int ClaimVersion);

/// <summary>The ticked worksheets and the bordx option.</summary>
public class BordxRequest
{
    /// <summary>"Single Claim" or "Multiple Claims".</summary>
    public string? Option { get; set; }

    /// <summary>The ticked worksheets, in list order.</summary>
    public List<ListingPick> Items { get; set; } = [];
}

/// <summary>The cases whose scanned documents are to be checked.</summary>
public class DocumentsRequest
{
    /// <summary>Case numbers.</summary>
    public List<string> Cases { get; set; } = [];
}

/// <summary>A scanned-document check is for the rows on screen; a request for thousands of cases is a mistake.</summary>
public sealed class DocumentsRequestValidator : RequestValidator<DocumentsRequest>
{
    /// <summary>Most cases one check may ask about.</summary>
    public const int MaxCases = 1000;

    /// <summary>Declares the rules.</summary>
    public DocumentsRequestValidator() =>
        RuleFor(r => r.Cases).Must(cases => cases.Count <= MaxCases).WithMessage($"Check at most {MaxCases} cases at a time.");
}

/// <summary>One generated bordx line.</summary>
/// <param name="ClaimNo">Case number - claim version.</param>
/// <param name="BordxNo">The bordx the worksheet went into.</param>
/// <param name="BordxDate">Bordx date.</param>
/// <param name="BordxAmount">The bordx's total amount.</param>
public record BordxLine(string ClaimNo, string BordxNo, DateTime BordxDate, decimal BordxAmount);

/// <summary>The bordx generation refused the request; the message says why.</summary>
public class CaseListingException(string message) : BusinessRuleException(message);

/// <summary>FrmWksBordx rules: the checks when a worksheet is ticked, the bordx options and the amount a worksheet adds.</summary>
public static class ListingRules
{
    /// <summary>One bordx per worksheet.</summary>
    public const string Single = "Single Claim";

    /// <summary>One bordx for all the ticked worksheets.</summary>
    public const string Multiple = "Multiple Claims";

    /// <summary>Most rows a search returns.</summary>
    public const int MaxRows = 5000;

    private static string Text(string? value) => (value ?? "").Trim();

    /// <summary>The IB payor reads its own view: a worksheet number starting IB, or the IB payor.</summary>
    public static bool UsesIbView(ListingFilter f) =>
        Text(f.WksNoFrom).StartsWith("IB", StringComparison.Ordinal) || Text(f.Payor).StartsWith("IB", StringComparison.Ordinal);

    /// <summary>
    /// lstWorksheetTrackList_ItemCheck. <paramref name="hasDocuments"/> is null when the document store is not configured (the check is
    /// skipped). The claimability test keeps the desktop's literal "N" (two-letter codes such as NN never match it).
    /// </summary>
    public static string? SelectionProblem(ListingItem i, bool? hasDocuments)
    {
        if (i.Dod is null) return "Date of Discharge is Empty.";
        if (Text(i.Hospital).Length == 0) return "Hospital cannot be empty!";
        if (Text(i.Claimability) == "N") return "Claimability is Not Decided!";
        if (IsGroup(i)) return Text(i.GroupCompany).Length == 0 ? "Group Company cannot be empty!" : null;
        return hasDocuments == false ? "Please ensure scanned documents are in system!" : null;
    }

    /// <summary>Group worksheets (health type N, insured type G or H) are exempt from the scanned-document rule.</summary>
    public static bool IsGroup(ListingItem i) => Text(i.HltType) == "N" && Text(i.InsuredType) is "G" or "H";

    /// <summary>The amount a worksheet adds to its bordx (CmdBordx_Click): the payable amount of whoever is paid when the BTBG flag is on, otherwise the approved amount, plus the case management fee.</summary>
    public static decimal Amount(ListingItem i) =>
        (Text(i.Btbg) == "Y" && Text(i.CashType) == "H" ? i.Payable2H : Text(i.Btbg) == "Y" && Text(i.CashType) == "M" ? i.Payable2M : i.TotalApproved) + i.CaseMgmtFees;
}

/// <summary>CmdSearch_Click's check.</summary>
public sealed class ListingFilterValidator : RequestValidator<ListingFilter>
{
    /// <summary>Declares the rules.</summary>
    public ListingFilterValidator() =>
        RuleFor(f => f).Must(f => !f.UseScan || !string.IsNullOrWhiteSpace(f.WksNoFrom)).WithMessage("Please Enter Worksheet No");
}

/// <summary>CmdBordx_Click's checks, in the desktop's order.</summary>
public sealed class BordxRequestValidator : RequestValidator<BordxRequest>
{
    /// <summary>Declares the rules.</summary>
    public BordxRequestValidator()
    {
        RuleFor(r => r.Items).Must(items => items.Count > 0).WithMessage("Please select at least one record to be updated!");
        RuleFor(r => r.Option).Must(o => (o ?? "").Trim() is ListingRules.Single or ListingRules.Multiple).WithMessage("Please select Bordx option");
    }
}

/// <summary>The store of scanned case documents (ScannedDoc\&lt;case&gt;).</summary>
public interface IScannedDocuments
{
    /// <summary>False when the document store is not configured (the check is skipped).</summary>
    bool Available { get; }

    /// <summary>addTheFiles: does the case's folder hold an image or PDF?</summary>
    bool HasDocuments(string cas);
}

/// <summary>Reads the scanned documents through <see cref="CaseFilesService"/>.</summary>
public sealed class ScannedDocuments(CaseFilesService files) : IScannedDocuments
{
    private static readonly string[] Extensions = [".jpg", ".tif", ".pdf", ".gif"];

    /// <inheritdoc />
    public bool Available => files.Available;

    /// <inheritdoc />
    public bool HasDocuments(string cas) =>
        files.ListCase(cas).GetValueOrDefault("scanned")?.Any(f => Extensions.Contains(Path.GetExtension(f.Name), StringComparer.OrdinalIgnoreCase)) == true;
}

/// <summary>Reads the pending worksheets and writes the bordx.</summary>
public interface IListingRepository
{
    /// <summary>The worksheets waiting for a bordx that match the filter.</summary>
    Task<List<ListingRow>> SearchAsync(IDbSession session, ListingFilter filter);

    /// <summary>A worksheet that is still pending, or null.</summary>
    Task<ListingItem?> GetItemAsync(IDbSession session, string cas, int version);

    /// <summary>The next bordx number of a payor (taken atomically).</summary>
    Task<string> NextBordxNoAsync(IDbSession session, string payor);

    /// <summary>Marks a worksheet as bordxed (claim, invoice, case flags and the case remark).</summary>
    Task ApplyAsync(IDbSession session, string cas, int version, string bordxNo, string userCode);

    /// <summary>Inserts the Bordx row.</summary>
    Task SaveBordxAsync(IDbSession session, string bordxNo, decimal amount, int claims, string cashType, string userCode);
}

/// <inheritdoc />
public sealed class ListingRepository : IListingRepository
{
    private static string? From(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <inheritdoc />
    public Task<List<ListingRow>> SearchAsync(IDbSession session, ListingFilter f) =>
        session.Connection.ProcQueryAsync<ListingRow>("genisis.CaseListing_Search", new
        {
            scan = f.UseScan, wksFrom = From(f.WksNoFrom), wksTo = f.UseScan ? null : From(f.WksNoTo), version = f.UseScan ? f.Version : null,
            ib = ListingRules.UsesIbView(f), doaFrom = f.DoaEmpty ? null : f.DoaFrom?.Date, doaTo = f.DoaEmpty ? null : f.DoaTo?.Date,
            doaEmpty = f.DoaEmpty, dodFrom = f.DodEmpty ? null : f.DodFrom?.Date, dodTo = f.DodEmpty ? null : f.DodTo?.Date,
            dodEmpty = f.DodEmpty, mhBordx = f.BordxStatusYes, max = ListingRules.MaxRows,
        }, session.Transaction);

    /// <inheritdoc />
    public Task<ListingItem?> GetItemAsync(IDbSession session, string cas, int version) =>
        session.Connection.ProcFirstOrDefaultAsync<ListingItem>("genisis.CaseListing_Item", new { cas, version }, session.Transaction);

    /// <inheritdoc />
    public Task<string> NextBordxNoAsync(IDbSession session, string payor) =>
        session.Connection.ProcSingleAsync<string>("genisis.CaseListing_NextBordxNo", new { pay = payor }, session.Transaction);

    /// <inheritdoc />
    public Task ApplyAsync(IDbSession session, string cas, int version, string bordxNo, string userCode) =>
        session.Connection.ProcExecAsync("genisis.CaseListing_Apply", new { cas, version, bordxNo, userCode }, session.Transaction);

    /// <inheritdoc />
    public Task SaveBordxAsync(IDbSession session, string bordxNo, decimal amount, int claims, string cashType, string userCode) =>
        session.Connection.ProcExecAsync("genisis.CaseListing_SaveBordx",
            new { bordxNo, amount, claims, status = cashType.Trim() == "H" ? "C" : "R", userCode }, session.Transaction);
}

/// <summary>Case > Inpatient Case Listing (legacy FrmWksBordx).</summary>
public class ListingService(IDbSessions sessions, IListingRepository repository, IScannedDocuments documents, ILogger<ListingService> log)
{
    /// <summary>CmdSearch_Click: the worksheets waiting for a bordx.</summary>
    public async Task<List<ListingRow>> SearchAsync(ListingFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        return await repository.SearchAsync(session, filter);
    }

    /// <summary>The scanned-document check for the ticked cases. <c>Available</c> is false when the document store is not configured.</summary>
    public (bool Available, Dictionary<string, bool> Cases) Documents(IEnumerable<string> cases) =>
        (documents.Available, cases.Select(c => c.Trim()).Where(c => c.Length > 0).Distinct()
            .ToDictionary(c => c, c => documents.Available && documents.HasDocuments(c)));

    /// <summary>
    /// cmdBordx_Click: generates the bordx for the ticked worksheets in one transaction. Throws <see cref="CaseListingException"/>
    /// with the message to show when a worksheet is no longer pending or breaks a selection rule.
    /// </summary>
    public async Task<List<BordxLine>> GenerateAsync(BordxRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        var lines = await GenerateAsync(session, request, userCode);
        await session.CommitAsync();
        log.LogInformation("Bordx generated by {User}: {Claims} worksheet(s), {Bordx} bordx", userCode, lines.Count, lines.Select(l => l.BordxNo).Distinct().Count());
        return lines;
    }

    /// <summary>Generates inside the caller's unit of work (nothing is committed here).</summary>
    public async Task<List<BordxLine>> GenerateAsync(IDbSession session, BordxRequest request, string userCode)
    {
        var single = request.Option!.Trim() == ListingRules.Single;
        var today = DateTime.Today;
        var picks = new List<(ListingPick Pick, string BordxNo)>();
        var totals = new Dictionary<string, decimal>();
        string? bordxNo = null;
        string? cashType = null;
        decimal total = 0;
        var claims = 0;

        foreach (var pick in request.Items)
        {
            var caseNo = pick.CaseNo.Trim();
            var item = await repository.GetItemAsync(session, caseNo, pick.ClaimVersion)
                ?? throw new CaseListingException($"Worksheet {caseNo}-{pick.ClaimVersion} is no longer waiting for a bordx. Search again.");
            if (ListingRules.SelectionProblem(item, documents.Available ? documents.HasDocuments(caseNo) : null) is { } problem)
                throw new CaseListingException($"{caseNo}-{pick.ClaimVersion}: {problem}");

            if (bordxNo is null || single)
            {
                bordxNo = await repository.NextBordxNoAsync(session, caseNo[..Math.Min(2, caseNo.Length)]);
                total = 0;
                claims = 0;
            }
            total += ListingRules.Amount(item);
            claims++;
            cashType = item.CashType;
            await repository.ApplyAsync(session, caseNo, pick.ClaimVersion, bordxNo, userCode);
            if (single) await repository.SaveBordxAsync(session, bordxNo, total, claims, cashType ?? "", userCode);
            totals[bordxNo] = total;
            picks.Add((pick, bordxNo));
        }
        if (!single) await repository.SaveBordxAsync(session, bordxNo!, total, claims, cashType ?? "", userCode);

        return picks.Select(p => new BordxLine($"{p.Pick.CaseNo.Trim()}-{p.Pick.ClaimVersion}", p.BordxNo, today, totals[p.BordxNo])).ToList();
    }
}
