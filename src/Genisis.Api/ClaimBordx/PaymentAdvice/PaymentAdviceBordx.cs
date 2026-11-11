using FluentValidation;
using Genisis.Api.Case.Listing;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;

namespace Genisis.Api.ClaimBordx.PaymentAdvice;

/// <summary>A worksheet waiting for a payment advice bordx (a line of <c>lstWorksheetTrackList</c>).</summary>
public record PayAdviceRow
{
    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Claim version.</summary>
    public int ClaimVersion { get; set; }

    /// <summary>Payor code.</summary>
    public string? Payor { get; set; }

    /// <summary>Approved amount.</summary>
    public decimal? AppAmt { get; set; }

    /// <summary>Date of admission.</summary>
    public DateTime? Doa { get; set; }

    /// <summary>Date of discharge.</summary>
    public DateTime? Dod { get; set; }

    /// <summary>Paid to: H hospital, M member.</summary>
    public string? CashType { get; set; }

    /// <summary>Cover id of the patient (00 for the member).</summary>
    public string? CoverId { get; set; }

    /// <summary>Name of the covered person.</summary>
    public string? CoveredName { get; set; }

    /// <summary>Hospital.</summary>
    public string? Hospital { get; set; }

    /// <summary>Amount payable to the hospital.</summary>
    public decimal? HosAmt { get; set; }

    /// <summary>Name of the member.</summary>
    public string? MemberName { get; set; }

    /// <summary>Group company.</summary>
    public string? GroupCompany { get; set; }

    /// <summary>Plan code.</summary>
    public string? PlanCode { get; set; }

    /// <summary>Final ICD code.</summary>
    public string? IcdCode { get; set; }

    /// <summary>BTBG flag.</summary>
    public string? Btbg { get; set; }

    /// <summary>Insured type.</summary>
    public string? InsuredType { get; set; }

    /// <summary>Claimability.</summary>
    public string? Claimability { get; set; }

    /// <summary>Cost centre.</summary>
    public string? CostCenter { get; set; }

    /// <summary>Group category.</summary>
    public string? GroupCategory { get; set; }

    /// <summary>State of the hospital.</summary>
    public string? HospitalState { get; set; }

    /// <summary>"Wks No": the case number and version, as the desktop's list showed it.</summary>
    public string WksNo => $"{CaseNo.Trim()}-{ClaimVersion}";

    /// <summary>The patient: the member, or the covered person.</summary>
    public string? Patient => CoverId?.Trim() == "00" ? MemberName : CoveredName;
}

/// <summary>A worksheet as the bordx needs it (what <see cref="PayAdviceRules"/> checks and adds up).</summary>
public record PayAdviceItem
{
    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Claim version.</summary>
    public int ClaimVersion { get; set; }

    /// <summary>Payor code.</summary>
    public string? Payor { get; set; }

    /// <summary>Date of discharge.</summary>
    public DateTime? Dod { get; set; }

    /// <summary>Hospital.</summary>
    public string? Hospital { get; set; }

    /// <summary>Final ICD code.</summary>
    public string? IcdCode { get; set; }

    /// <summary>Group company.</summary>
    public string? GroupCompany { get; set; }

    /// <summary>Insured type.</summary>
    public string? InsuredType { get; set; }

    /// <summary>Claimability.</summary>
    public string? Claimability { get; set; }

    /// <summary>Cost centre.</summary>
    public string? CostCenter { get; set; }

    /// <summary>Group category.</summary>
    public string? GroupCategory { get; set; }

    /// <summary>BTBG flag.</summary>
    public string? Btbg { get; set; }

    /// <summary>Paid to: H hospital, M member.</summary>
    public string? CashType { get; set; }

    /// <summary>Amount payable to the hospital.</summary>
    public decimal Payable2H { get; set; }

    /// <summary>Amount payable by the member.</summary>
    public decimal Payable2M { get; set; }

    /// <summary>Approved amount.</summary>
    public decimal TotalApproved { get; set; }

    /// <summary>Case management fee.</summary>
    public decimal CaseMgmtFees { get; set; }
}

/// <summary>The criteria of the worksheet search.</summary>
public class PayAdviceFilter
{
    /// <summary>"Use Scan": one worksheet number (and version) instead of a range.</summary>
    public bool UseScan { get; set; } = true;

    /// <summary>Worksheet (case) number, or the start of the range.</summary>
    public string? WksNoFrom { get; set; }

    /// <summary>End of the range (range mode only).</summary>
    public string? WksNoTo { get; set; }

    /// <summary>Claim version (scan mode only).</summary>
    public int? Version { get; set; }

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

    /// <summary>Cash Type: H or M (or "H - Hospital", "M - Member").</summary>
    public string? CashType { get; set; }

    /// <summary>Hospital (its name).</summary>
    public string? Hospital { get; set; }
}

/// <summary>The ticked worksheets (the bordx is always one bordx for all of them).</summary>
public class PayAdviceBordxRequest
{
    /// <summary>The ticked worksheets, in list order.</summary>
    public List<ListingPick> Items { get; set; } = [];
}

/// <summary>The bordx that was generated.</summary>
public record PayAdviceBordxResult(string BordxNo, DateTime BordxDate, decimal BordxAmount, int Claims, List<BordxLine> Lines);

/// <summary>CmdSearch_Click's check: the worksheet number in scan mode.</summary>
public sealed class PayAdviceFilterValidator : RequestValidator<PayAdviceFilter>
{
    /// <summary>Declares the rules.</summary>
    public PayAdviceFilterValidator()
    {
        RuleFor(f => f).Must(f => !f.UseScan || !string.IsNullOrWhiteSpace(f.WksNoFrom)).WithMessage("Please Enter Worksheet No");
        RuleFor(f => f.CashType).Must(c => string.IsNullOrWhiteSpace(c) || c.Trim()[0] is 'H' or 'M').WithMessage("Cash Type is H or M.");
    }
}

/// <summary>CmdBordx_Click's check: at least one ticked worksheet.</summary>
public sealed class PayAdviceBordxRequestValidator : RequestValidator<PayAdviceBordxRequest>
{
    /// <summary>Declares the rules.</summary>
    public PayAdviceBordxRequestValidator() => RuleFor(r => r.Items).Must(items => items.Count > 0).WithMessage("Please select at least one record to be updated! ");
}

/// <summary>The bordx generation refused the request; the message says why.</summary>
public class PayAdviceBordxException(string message) : BusinessRuleException(message);

/// <summary>frmPayAdviceHosp rules: the checks when a worksheet is ticked and the amount a worksheet adds.</summary>
public static class PayAdviceRules
{
    /// <summary>The payment advice number every worksheet of the bordx gets (the desktop's tmpPANo).</summary>
    public const string PaymentAdviceNo = "001";

    private static string Text(string? value) => (value ?? "").Trim();

    /// <summary>
    /// lstWorksheetTrackList_ItemCheck, in the desktop's order. <paramref name="hasDocuments"/> is null when the document store is not configured (the check is skipped). Unlike Inpatient Case Listing the
    /// final ICD code, the cost centre and the group category must be there, and every worksheet needs scanned documents. The claimability test keeps the desktop's literal "N".
    /// </summary>
    public static string? SelectionProblem(PayAdviceItem i, bool? hasDocuments)
    {
        if (i.Dod is null) return "Date of Discharge is Empty.";
        if (Text(i.Hospital).Length == 0) return "Hospital cannot be empty!";
        if (Text(i.IcdCode).Length == 0) return "Final ICD Code cannot be empty!";
        if (Text(i.Claimability) == "N") return "Claimability is Not Decided!";
        if (Text(i.CostCenter).Length == 0) return "Cost Centre is empty";
        if (Text(i.GroupCategory).Length == 0) return "Group Category is empty";
        if (hasDocuments == false) return "Please ensure scanned documents are in system!";
        return Text(i.InsuredType) is "G" or "H" && Text(i.GroupCompany).Length == 0 ? "Group Company cannot be empty!" : null;
    }

    /// <summary>The amount a worksheet adds to the bordx: the payable amount of whoever is paid when the BTBG flag is on, otherwise the approved amount, plus the case management fee.</summary>
    public static decimal Amount(PayAdviceItem i) =>
        (Text(i.Btbg) == "Y" && Text(i.CashType) == "H" ? i.Payable2H : Text(i.Btbg) == "Y" && Text(i.CashType) == "M" ? i.Payable2M : i.TotalApproved) + i.CaseMgmtFees;
}

/// <summary>Reads the worksheets waiting for a payment advice bordx and writes the bordx.</summary>
public interface IPayAdviceRepository
{
    /// <summary>The worksheets that match the filter (at most 5,000).</summary>
    Task<List<PayAdviceRow>> SearchAsync(IDbSession session, PayAdviceFilter filter);

    /// <summary>A worksheet that is still in the list, or null.</summary>
    Task<PayAdviceItem?> GetItemAsync(IDbSession session, string cas, int version);

    /// <summary>The next bordx number of a payor (taken atomically, the sequence Inpatient Case Listing uses).</summary>
    Task<string> NextBordxNoAsync(IDbSession session, string payor);

    /// <summary>Marks a worksheet as bordxed (claim, invoice, case flags and the case remark) and gives it the payment advice number.</summary>
    Task ApplyAsync(IDbSession session, string cas, int version, string bordxNo, string userCode);

    /// <summary>Inserts the Bordx row.</summary>
    Task SaveBordxAsync(IDbSession session, string bordxNo, decimal amount, int claims, string cashType, string userCode);
}

/// <inheritdoc />
public sealed class PayAdviceRepository : IPayAdviceRepository
{
    private const int MaxRows = 5000;

    private static string? From(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <inheritdoc />
    public Task<List<PayAdviceRow>> SearchAsync(IDbSession session, PayAdviceFilter f) =>
        session.Connection.ProcQueryAsync<PayAdviceRow>("genisis.PayAdvice_Search", new
        {
            scan = f.UseScan, wksFrom = From(f.WksNoFrom), wksTo = f.UseScan ? null : From(f.WksNoTo), version = f.UseScan ? f.Version : null, doaFrom = f.DoaEmpty ? null : f.DoaFrom?.Date,
            doaTo = f.DoaEmpty ? null : f.DoaTo?.Date, doaEmpty = f.DoaEmpty, dodFrom = f.DodEmpty ? null : f.DodFrom?.Date, dodTo = f.DodEmpty ? null : f.DodTo?.Date, dodEmpty = f.DodEmpty,
            cashType = From(f.CashType) is { } c ? c[..1].ToUpperInvariant() : null, hospital = From(f.Hospital), max = MaxRows,
        }, session.Transaction);

    /// <inheritdoc />
    public Task<PayAdviceItem?> GetItemAsync(IDbSession session, string cas, int version) =>
        session.Connection.ProcFirstOrDefaultAsync<PayAdviceItem>("genisis.PayAdvice_Item", new { cas, version }, session.Transaction);

    /// <inheritdoc />
    public Task<string> NextBordxNoAsync(IDbSession session, string payor) =>
        session.Connection.ProcSingleAsync<string>("genisis.CaseListing_NextBordxNo", new { pay = payor }, session.Transaction);

    /// <inheritdoc />
    public Task ApplyAsync(IDbSession session, string cas, int version, string bordxNo, string userCode) =>
        session.Connection.ProcExecAsync("genisis.CaseListing_Apply", new { cas, version, bordxNo, userCode, payAdvice = PayAdviceRules.PaymentAdviceNo }, session.Transaction);

    /// <inheritdoc />
    public Task SaveBordxAsync(IDbSession session, string bordxNo, decimal amount, int claims, string cashType, string userCode) =>
        session.Connection.ProcExecAsync("genisis.CaseListing_SaveBordx", new { bordxNo, amount, claims, status = cashType.Trim() == "H" ? "C" : "R", userCode }, session.Transaction);
}

/// <summary>Claim Bordx > Payment Advice > Generate Bordx No (legacy <c>frmPayAdviceHosp</c>, "Payment Advice - Generate Bordx").</summary>
public class PayAdviceBordxService(IDbSessions sessions, IPayAdviceRepository repository, IScannedDocuments documents, ILogger<PayAdviceBordxService> log)
{
    /// <summary>CmdSearch_Click: the worksheets waiting for a payment advice bordx.</summary>
    public async Task<List<PayAdviceRow>> SearchAsync(PayAdviceFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        return await repository.SearchAsync(session, filter);
    }

    /// <summary>The scanned-document check for the ticked cases. <c>Available</c> is false when the document store is not configured.</summary>
    public (bool Available, Dictionary<string, bool> Cases) Documents(IEnumerable<string> cases) =>
        (documents.Available, cases.Select(c => c.Trim()).Where(c => c.Length > 0).Distinct().ToDictionary(c => c, c => documents.Available && documents.HasDocuments(c)));

    /// <summary>
    /// CmdBordx_Click: one bordx (numbered from the payor code in the first worksheet's case number) for all the ticked worksheets, each marked as bordxed and given payment advice 001, in one transaction.
    /// A worksheet that is no longer in the list, or breaks a selection rule, is refused with <see cref="PayAdviceBordxException"/> and nothing is saved. The Bordx row carries the total of all the
    /// worksheets and their number (the desktop reset both for every worksheet, so its row held the last worksheet's amount and a count of 1) and is status C when the last worksheet is paid to the hospital.
    /// </summary>
    public async Task<PayAdviceBordxResult> GenerateAsync(PayAdviceBordxRequest request, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        var today = DateTime.Today;
        var first = request.Items[0].CaseNo.Trim();
        var bordxNo = await repository.NextBordxNoAsync(session, first[..Math.Min(2, first.Length)]);
        decimal total = 0;
        string? cashType = null;
        var lines = new List<(ListingPick Pick, decimal Amount)>();
        foreach (var pick in request.Items)
        {
            var caseNo = pick.CaseNo.Trim();
            var item = await repository.GetItemAsync(session, caseNo, pick.ClaimVersion)
                ?? throw new PayAdviceBordxException($"Worksheet {caseNo}-{pick.ClaimVersion} is no longer waiting for a payment advice bordx. Search again.");
            if (PayAdviceRules.SelectionProblem(item, documents.Available ? documents.HasDocuments(caseNo) : null) is { } problem) throw new PayAdviceBordxException($"{caseNo}-{pick.ClaimVersion}: {problem}");
            var amount = PayAdviceRules.Amount(item);
            total += amount;
            cashType = item.CashType;
            await repository.ApplyAsync(session, caseNo, pick.ClaimVersion, bordxNo, userCode);
            lines.Add((pick, amount));
        }

        await repository.SaveBordxAsync(session, bordxNo, total, lines.Count, cashType ?? "", userCode);
        await session.CommitAsync();
        log.LogInformation("Payment advice bordx {Bordx} generated by {User}: {Claims} worksheet(s)", bordxNo, userCode, lines.Count);
        return new PayAdviceBordxResult(bordxNo, today, total, lines.Count, lines.Select(l => new BordxLine($"{l.Pick.CaseNo.Trim()}-{l.Pick.ClaimVersion}", bordxNo, today, l.Amount)).ToList());
    }
}
