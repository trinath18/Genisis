using System.Globalization;
using Genisis.Api.Case;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet.Enquiry;

namespace Genisis.Api.Worksheet.Adjustment;

/// <summary>The guarantee letter details of frmGLMenu, as Final LG takes them, with the answer to the take-over clause question.</summary>
public class FinalLgRequest : CaseGLRequest
{
    /// <summary>True once the user has confirmed "Have you checked Take Over clause?" (asked for payor AX when the policy dates differ).</summary>
    public bool TakeOverChecked { get; set; }
}

/// <summary>What the Final LG checks found before the letter is issued.</summary>
public record FinalLgCheck
{
    /// <summary>The take-over clause must be confirmed first (payor AX, policy dates differ); nothing else was checked.</summary>
    public bool TakeOverCheck { get; set; }

    /// <summary>The LG amount matches the worksheet and is within the member's limit: the desktop's LGStatus.</summary>
    public bool WithinLimit { get; set; }

    /// <summary>The member's balance limit; empty when the plan has none.</summary>
    public string? Balance { get; set; }

    /// <summary>False when the worksheet is paid to the hospital and the LG amount is not its final total.</summary>
    public bool MatchesWorksheet { get; set; } = true;

    /// <summary>The worksheet's final total payable to the hospital.</summary>
    public decimal FinalTotal { get; set; }

    /// <summary>What to ask the user: the take-over question, "Are you sure?" or "Please check LG Amount, do you want to continue?".</summary>
    public string Prompt { get; set; } = "";
}

/// <summary>Rules of Worksheet Adjustment's Final LG (cmdFinalLG_Click).</summary>
public static class FinalLgRules
{
    /// <summary>Asked for payor AX when the policy dates differ.</summary>
    public const string TakeOverQuestion = "Have you checked Take Over clause?";

    /// <summary>Asked when the LG amount does not match the worksheet or is above the limit; the user may go on.</summary>
    public const string CheckAmount = "Please check LG Amount, do you want to continue?";

    /// <summary>Asked when everything matches.</summary>
    public const string Confirm = "Are you sure?";

    /// <summary>Payor AX with a policy effective date that is not the payor effective date: the take-over clause has to be checked.</summary>
    public static bool NeedsTakeOverCheck(string? payor, DateTime? policyEffDate, DateTime? payorEffDate) =>
        string.Equals(payor?.Trim(), "AX", StringComparison.OrdinalIgnoreCase) && policyEffDate?.Date != payorEffDate?.Date;

    /// <summary>The member and the patient name must be on the worksheet, in the desktop's order; returns its message.</summary>
    public static string? MemberProblem(string? memberNo, string? patientName) =>
        string.IsNullOrWhiteSpace(memberNo) ? "Please Enter Membership Information" : string.IsNullOrWhiteSpace(patientName) ? "Please Enter Patient Name" : null;

    /// <summary>A worksheet paid to the hospital (H) needs the LG amount to be its final total, to the cent; any other worksheet does not.</summary>
    public static bool MatchesWorksheet(string? payTo, decimal lgAmount, decimal finalTotal) =>
        !(string.Equals(payTo?.Trim(), "H", StringComparison.OrdinalIgnoreCase)
          && Math.Round(lgAmount, 2, MidpointRounding.AwayFromZero) != Math.Round(finalTotal, 2, MidpointRounding.AwayFromZero));

    /// <summary>
    /// CheckLimit's last test: the LG amount may not be above the member's balance limit plus what this worksheet itself approved (the balance does not yet have it
    /// taken off). A plan with no balance limit has no limit to test.
    /// </summary>
    public static bool WithinBalance(decimal lgAmount, decimal? balance, decimal finalApprove) => balance is null || lgAmount <= balance.Value + finalApprove;

    /// <summary>What to ask: "Are you sure?" when all is well, otherwise the warning the user can go on past.</summary>
    public static string Prompt(bool ok) => ok ? Confirm : CheckAmount;

    /// <summary>The balance the case check reports ("#,##0.00"), as a number.</summary>
    public static decimal? ParseBalance(string? balance) =>
        decimal.TryParse(balance, NumberStyles.Number, CultureInfo.InvariantCulture, out var b) ? b : null;

    /// <summary>The final diagnoses, comma separated: the reason the letter gives.</summary>
    public static string Reason(IEnumerable<EnquiryDiagnosis> diagnoses) => NoteRules.Disability(diagnoses).Replace("\n", ", ");
}

/// <summary>Worksheet > Worksheet Adjustment: Final LG (cmdFinalLG_Click). The letter is issued by <see cref="CaseDocumentsService"/>, as Case Adjustment's is.</summary>
public class FinalLgService(IDbSessions sessions, IEnquiryRepository enquiry, IAdjustmentRepository adjustment, CaseDocumentsService documents)
{
    private sealed record Loaded(EnquiryHeader Header, EnquirySummary Summary, string? PayTo, string Reason);

    private async Task<Loaded> LoadAsync(string cas, int version)
    {
        if (version < 1) throw new WorksheetException("Please select a version nombor!");
        await using var session = await sessions.OpenAsync();
        var header = await enquiry.HeaderAsync(session, cas, version) ?? throw new NotFoundException("Record not found!");
        return new Loaded(header, await enquiry.SummaryAsync(session, cas, version, version), await adjustment.PayToAsync(session, cas, version),
            FinalLgRules.Reason(await enquiry.DiagnosesAsync(session, cas)));
    }

    /// <summary>frmGLMenu defaults: the request date and time on record and the last GL purpose.</summary>
    public async Task<object> DefaultsAsync(string caseNo)
    {
        var (requestDate, requestTime, lastPurpose) = await documents.GLDefaultsAsync(caseNo.Trim());
        return new { requestDate, requestTime, lastPurpose };
    }

    /// <summary>The checks before the letter, in the desktop's order, and the question to put to the user.</summary>
    public async Task<FinalLgCheck> CheckAsync(string caseNo, int version, FinalLgRequest request)
    {
        var cas = caseNo.Trim();
        var (_, _, lastPurpose) = await documents.GLDefaultsAsync(cas);
        if (CaseGLRules.Validate(request, lastPurpose) is { } error) throw new WorksheetException(error);
        var c = await LoadAsync(cas, version);
        if (!request.TakeOverChecked && FinalLgRules.NeedsTakeOverCheck(c.Header.Payor, c.Header.PolicyEffDate, c.Header.PayorEffDate))
            return new FinalLgCheck { TakeOverCheck = true, Prompt = FinalLgRules.TakeOverQuestion };
        if (FinalLgRules.MemberProblem(c.Header.MemberNo, c.Header.PatientName) is { } problem) throw new WorksheetException(problem);

        var amount = request.LgAmount ?? 0;
        var matches = FinalLgRules.MatchesWorksheet(c.PayTo, amount, c.Summary.PayableToHospital);
        decimal? balance = null;
        var within = true;
        if (matches)
        {
            balance = FinalLgRules.ParseBalance((await documents.CheckGLAsync(cas, request.LgAmount)).Balance);
            within = FinalLgRules.WithinBalance(amount, balance, c.Summary.FinalApprove);
        }
        var ok = matches && within;
        return new FinalLgCheck { WithinLimit = ok, Balance = balance?.ToString("#,##0.00"), MatchesWorksheet = matches, FinalTotal = c.Summary.PayableToHospital, Prompt = FinalLgRules.Prompt(ok) };
    }

    /// <summary>
    /// Final LG: issues the guarantee letter for the worksheet's case. The checks are repeated here, so they hold for any client; a mismatch of the LG amount is the
    /// user's to go on past (the desktop asks), so it is not refused. In one transaction the case's other open letter is closed, the LG is written, the version's invoices
    /// get the LG number and the case is marked "FINAL GL Issued"; then the member is notified and the letter is built.
    /// </summary>
    public async Task<CaseGLResult> IssueAsync(string caseNo, int version, FinalLgRequest request, string userCode, string userName)
    {
        var cas = caseNo.Trim();
        var (_, _, lastPurpose) = await documents.GLDefaultsAsync(cas);
        if (CaseGLRules.Validate(request, lastPurpose) is { } error) throw new WorksheetException(error);
        var c = await LoadAsync(cas, version);
        if (!request.TakeOverChecked && FinalLgRules.NeedsTakeOverCheck(c.Header.Payor, c.Header.PolicyEffDate, c.Header.PayorEffDate))
            throw new WorksheetException(FinalLgRules.TakeOverQuestion);
        if (FinalLgRules.MemberProblem(c.Header.MemberNo, c.Header.PatientName) is { } problem) throw new WorksheetException(problem);

        request.Form = new CaseAdjustmentRequest
        {
            MbmNumber = c.Header.MemberNo, CoverId = c.Header.PatientCovId, HospitalCode = c.Header.HosCode, HospitalName = c.Header.Hospital,
            DateAdmitted = c.Header.Admitted, DateDischarged = c.Header.Discharged, Reason = c.Reason,
        };
        return await documents.IssueGLAsync(cas, request, userCode, userName, version);
    }
}
