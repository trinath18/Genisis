namespace Genisis.Api.Case;

public record DiagnosisLine(string? Text, DateTime? Onset, string? Icd);
public record ProcedureLine(string? Procedure, string? Code);

/// <summary>Case > Registration (legacy frmCaseAdmission). Codes are the part before " - " in the desktop combos.</summary>
public class CaseRegistrationRequest
{
    // Header
    public string? MbmNumber { get; set; }
    public string? Claimability { get; set; } = "AA";
    public string? Pending { get; set; } = "N";

    // Case Details
    public string? CoverId { get; set; }
    public string? HospitalCode { get; set; }
    public string? HospitalName { get; set; }
    public string? Reason { get; set; }
    public string? ReferredBy { get; set; }
    public string? ClaimNature { get; set; }
    public string? StayType { get; set; }
    public string? TreatmentMode { get; set; }
    public string? PayType { get; set; }
    public DateTime? FirstCall { get; set; }
    public DateTime? FirstTreated { get; set; }
    public DateTime? DateAdmitted { get; set; }
    public DateTime? DateDischarged { get; set; }
    /// <summary>The D.O.A / D.O.D check boxes: when ticked the dates are checked against the policy and today.</summary>
    public bool CheckAdmitted { get; set; } = true;
    public bool CheckDischarged { get; set; } = true;
    public decimal? ReserveAmount { get; set; }
    public decimal? LgAmount { get; set; }
    public string? LgTime { get; set; }
    public string? LgRemark { get; set; }
    public string? Recur { get; set; }
    public string? FollowUpRequired { get; set; }
    public string? FollowUp { get; set; }
    public string? PolicyYear { get; set; }
    public string? PatientAge { get; set; }
    public string? AgeUnit { get; set; }
    public List<DiagnosisLine> PrelimDiagnoses { get; set; } = [];
    public List<DiagnosisLine> FinalDiagnoses { get; set; } = [];
    public List<DiagnosisLine> UnderlyingDiseases { get; set; } = [];
    public List<string?> RepudiationCodes { get; set; } = [];
    public string? RepDocument { get; set; }
    public string? RepPersonnel { get; set; }
    public string? RepAttention { get; set; }
    public string? RepUnderlying { get; set; }

    // Case Others
    public List<ProcedureLine> SurgicalProcedures { get; set; } = [];
    public List<ProcedureLine> NonSurgicalProcedures { get; set; } = [];
    public List<ProcedureLine> Investigations { get; set; } = [];
    public List<string?> MedicalManagement { get; set; } = [];
    public List<string?> AnaesDoctors { get; set; } = [];
    public List<string?> AttendingDoctors { get; set; } = [];

    // Take Over / Others
    public string? TakeOverInd { get; set; }
    public decimal? TakeOverAmount { get; set; }
    public string? IntExtTakeOver { get; set; }
    public string? TakeOverPlan { get; set; }
    public decimal? ReductionAmount { get; set; }
    public string? TopUpLimit { get; set; } = "N";
    public string? SpecialApproval { get; set; }
    public string? QueryStatus { get; set; }
    public string? PaymentPending { get; set; } = "N";
    public DateTime? FirstDocDate { get; set; }
    public DateTime? SubsequentDocDate { get; set; }

    // Case Mgmt Notes (pages 1-5)
    public List<string?> Remarks { get; set; } = [];
}

public record CaseRegistrationResult(string CaseNumber);

public class CaseRegistrationException(string message) : Genisis.Api.Common.BusinessRuleException(message);

/// <summary>Required-field checks of NewCaseAddmission, in the desktop order and with its messages.</summary>
public static class CaseRegistrationValidator
{
    /// <summary>Claimability codes that need repudiation codes and letter selections.</summary>
    public static readonly string[] Repudiated = ["RR", "NR", "PR", "AR", "BR", "ER"];

    /// <summary>Hospital entries that are placeholders; the actual hospital / clinic name must be typed in.</summary>
    public static readonly string[] NamedHospitals =
        ["DENTAL CLINIC", "DIALYSIS CENTRE", "FOREIGN HOSPITAL", "GENERAL PRACTITIONER", "GOVERNMENT HOSPITAL", "NON-PANEL HOSPITAL", "SPECIALIST CLINIC"];

    public static bool IsRepudiated(string? claimability) => Repudiated.Contains((claimability ?? "").Trim().ToUpperInvariant());

    public static string? Validate(CaseRegistrationRequest r)
    {
        static bool Blank(string? s) => string.IsNullOrWhiteSpace(s);
        var cashless = r.PayType?.Trim().StartsWith('C') == true;

        if (Blank(r.MbmNumber)) return "Please Enter Membership Information";
        if (Blank(r.CoverId)) return "Please Enter Patient Information";
        if (Blank(r.Claimability)) return "Please Enter Claimability Status";
        if (Blank(r.HospitalCode)) return "Please Enter Hospital Information";
        if (r.DateAdmitted is null) return "Please Enter Admission Date";
        if (r.ReserveAmount is null) return "Please Enter Reserve Amount";
        if (r.LgAmount is null) return "Please Enter LG Amount";
        if (cashless && Blank(r.PrelimDiagnoses.FirstOrDefault()?.Icd)) return "Please Enter ICD Code";
        if (Blank(r.TopUpLimit)) return "Please Enter Topup Limit Indicator";
        if (r.IntExtTakeOver?.Trim() == "I" && Blank(r.TakeOverPlan)) return "Please Enter Takeover Plan";
        if (cashless && Blank(r.PolicyYear)) return "Please Enter Policy Year";
        if (!Blank(r.PolicyYear) && !int.TryParse(r.PolicyYear, out _)) return "Policy Year must be a number";
        if (Blank(r.PatientAge)) return "Please Enter Patient's Age";
        if (Blank(r.AgeUnit)) return "Please Select Week/Month/Year";
        if (!Blank(r.LgTime) && !TimeOnly.TryParseExact(r.LgTime!.Trim(), "HH:mm", out _)) return "Invalid Time Format!";
        if (r.Remarks.Any(x => x?.Contains('~') == true)) return "You are not allowed to use the ~ symbol in the remarks";
        if (r.DateDischarged is not null && r.DateDischarged < r.DateAdmitted && (r.CheckAdmitted || r.CheckDischarged))
            return "Admission Date cannot greater than Discharge Date!";

        if (IsRepudiated(r.Claimability))
        {
            if (Blank(r.RepudiationCodes.FirstOrDefault())) return "Please Enter Repudiate Code";
            // The desktop also required Attention, but its selection window hides that list, so a repudiated
            // case could never be saved; only the two visible selections are required here.
            if (Blank(r.RepDocument) || Blank(r.RepPersonnel))
                return "Please Enter Repudiation Selection Reason";
        }
        return null;
    }
}
