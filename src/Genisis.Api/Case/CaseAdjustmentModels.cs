namespace Genisis.Api.Case;

/// <summary>Case > Adjustment (legacy frmCaseAdjustmentNew): the registration fields plus the adjustment-only ones.</summary>
public class CaseAdjustmentRequest : CaseRegistrationRequest
{
    public string? CaseNumber { get; set; }
    public string? MemberName { get; set; }
    public string? Status { get; set; } = "O";
    public string? LinkCaseNo { get; set; }
    /// <summary>txtDateRECDoc: date the documents were received (CAS.CASDRD).</summary>
    public DateTime? DocReceivedDate { get; set; }
    /// <summary>cboPendingReason: stored on the maintenance tblRecord.DocRemark.</summary>
    public string? PendingReason { get; set; }
}

/// <summary>frmGLMenu: the Generate Guarantee Letter dialog plus the case form it was opened from.</summary>
public class CaseGLRequest
{
    public string? Purpose { get; set; }
    public DateTime? RequestDate { get; set; }
    public string? RequestTime { get; set; }
    public string? RequestMode { get; set; }
    public string? RequestBy { get; set; }
    public DateTime? GLDate { get; set; }
    public string? GLTime { get; set; }
    public DateTime? FaxDate { get; set; }
    public string? FaxTime { get; set; }
    public decimal? LgAmount { get; set; }
    public string? DelayReason { get; set; }
    public string? Remarks { get; set; }
    public CaseAdjustmentRequest Form { get; set; } = new();
}

public record CaseGLCheck(bool WithinLimit, string? Balance);
public record CaseGLResult(string LgNumber, CaseLetterResult Letter);

public static class CaseGLRules
{
    public static readonly string[] Purposes = ["Admission", "Discharge", "Follow-up", "Interim", "Outpatient", "Executive Medical Checkup"];
    public static readonly string[] RequestModes = ["Medical Report", "Email", "Telephone"];
    public static readonly string[] RequestBy = ["Hospital", "Payor", "Agent", "Policyholder"];
    public static readonly string[] DelayReasons =
    [
        "Admission upon Discharge", "Awaiting complete Document", "Awaiting Renewal Data", "Amended GL", "Amended Bill",
        "Late Admission", "Late Discharge", "Refer to Payor", "Query GP", "Query Attending Doctor",
        "Require Investigation Report", "Bill Above RM10K", "Bill Above RM25K", "Bill Above RM50K", "Pre-Admission",
    ];

    public const string NotAllowed = "New GL is not allowed. Please Register A new case.";

    /// <summary>cbPurpose_Change: which new purposes the case's last GL purpose still allows.</summary>
    public static bool PurposeBlocked(string? lastPurpose, string? purpose) => (lastPurpose?.Trim(), purpose?.Trim()) switch
    {
        ("Executive Medical Checkup", _) => true,
        ("Admission", "Outpatient" or "Executive Medical Checkup") => true,
        ("Discharge", "Outpatient" or "Executive Medical Checkup" or "Admission") => true,
        _ => false,
    };

    /// <summary>frmGLMenu.CmdSave_Click, in the desktop order and with its messages.</summary>
    public static string? Validate(CaseGLRequest r, string? lastPurpose)
    {
        static bool Blank(string? s) => string.IsNullOrWhiteSpace(s);
        static TimeOnly? Time(string? s) => TimeOnly.TryParseExact(s?.Trim(), "HH:mm", out var t) ? t : null;

        if (r.RequestDate is null) return "Please Enter Request Date";
        if (Time(r.RequestTime) is null) return Blank(r.RequestTime) ? "Please Enter Request Time" : "Invalid Time Format!";
        if (Blank(r.RequestMode)) return "Please Enter Request Mode";
        if (Blank(r.RequestBy)) return "Please Enter Request By";
        if (r.GLDate is null) return "Please Enter GL Date";
        if (Time(r.GLTime) is null) return Blank(r.GLTime) ? "Please Enter GL Time" : "Invalid Time Format!";
        if (r.LgAmount is null && r.Purpose?.Trim() != "Outpatient") return "Please Enter Reserve Amount";
        if (Blank(r.Purpose)) return "Please Enter purpose for GL";
        if (!Purposes.Contains(r.Purpose!.Trim())) return "Please Enter purpose for GL";
        if (!Blank(r.FaxTime) && Time(r.FaxTime) is null) return "Invalid Time Format!";
        if (Blank(r.DelayReason))
        {
            var late = r.GLDate.Value.Date > r.RequestDate.Value.Date
                || (r.GLDate.Value.Date == r.RequestDate.Value.Date && (Time(r.GLTime)!.Value - Time(r.RequestTime)!.Value).TotalMinutes > 30);
            if (late) return "Please Enter reason for delay";
        }
        if (Blank(r.Remarks)) return "Please Enter remark";
        if (PurposeBlocked(lastPurpose, r.Purpose)) return NotAllowed;
        return null;
    }
}

/// <summary>cmdUpdate1_Click's required-field checks, in the desktop order and with its messages.</summary>
public static class CaseAdjustmentValidator
{
    public static string? Validate(CaseAdjustmentRequest r)
    {
        static bool Blank(string? s) => string.IsNullOrWhiteSpace(s);

        if (Blank(r.CaseNumber)) return "Please Enter Case No!";
        if (Blank(r.Claimability)) return "Please Select Claimability!";
        if (Blank(r.MbmNumber)) return "Please Enter Member No!";
        if (Blank(r.MemberName)) return "Please Enter Member Name!";
        if (r.DateAdmitted is null) return "Please Enter Admission Date";
        if (r.IntExtTakeOver?.Trim() == "I" && Blank(r.TakeOverPlan)) return "Please Enter Takeover Plan";
        if (Blank(r.PolicyYear)) return "Please Enter Policy Year";
        if (!int.TryParse(r.PolicyYear, out _)) return "Policy Year must be a number";
        if (Blank(r.PatientAge)) return "Please Enter Patient's Age";
        if (Blank(r.AgeUnit)) return "Please Select Day/Year";
        if (!Blank(r.LgTime) && !TimeOnly.TryParseExact(r.LgTime!.Trim(), "HH:mm", out _)) return "Invalid Time Format!";
        if (r.Remarks.Any(x => x?.Contains('~') == true)) return "You are not allowed to use the ~ symbol in the remarks";
        return null;
    }

    /// <summary>cmdRepudiate_Click's checks before the repudiation letter.</summary>
    public static string? ValidateRepudiation(CaseAdjustmentRequest r)
    {
        static bool Blank(string? s) => string.IsNullOrWhiteSpace(s);

        if (Blank(r.MbmNumber)) return "Please Enter Membership Information";
        if (Blank(r.CoverId)) return "Please Enter Patient Information";
        if (Blank(r.HospitalCode)) return "Please Enter Hospital Information";
        if (r.DateAdmitted is null) return "Please Enter Admission Date";
        if (r.ReserveAmount is null) return "Please Enter Reserve Amount";
        if (Blank(r.RepudiationCodes.FirstOrDefault())) return "Please Enter Repudiation Reason";
        // Cash (non-reimbursement) cases need the letter selections from the repudiation selection window.
        if (r.PayType?.Trim().StartsWith('R') != true && (Blank(r.RepDocument) || Blank(r.RepPersonnel)))
            return "Please Enter Repudiation Selection Reason";
        return null;
    }
}
