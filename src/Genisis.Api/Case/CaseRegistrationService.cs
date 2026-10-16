using System.Data;
using Dapper;
using Genisis.Api.Data;
using Microsoft.Data.SqlClient;
using static Genisis.Api.Data.RowMapper;

namespace Genisis.Api.Case;

public record CaseAlert(string Text, bool Critical);
public record CasePatientOption(string CoverId, string Name);
public record CaseMemberRow(string CoverId, string? Name, string? Ic, string? Relation, DateTime? Dob, decimal? AnnualLimit, string? Exclusion);

public record CaseMemberView(
    Dictionary<string, object?> Member,
    string PolStatus,
    string AnnualLimit,
    List<CasePatientOption> Patients,
    List<CaseMemberRow> Members,
    string? PrincipalExclusion,
    int? PlanStartAge,
    int? PlanEndAge,
    string? PolicyWording,
    string? AgentLabel,
    bool WaitingPeriod,
    string? ExpiryLabel,
    bool CanRegister,
    bool LgAllowed,
    List<CaseAlert> Alerts,
    DateTime Today);

/// <summary>The member fields NewCaseAddmission checks before saving.</summary>
public record CaseMemberInfo(string PayCode, string? PolicyNo, string? Status, string? SuspendStatus, DateTime? SuspendDate,
    DateTime EffDate, DateTime ExpDate, string? CostCenter);

public record CaseHospitalInfo(string? HOSName, string? HOSStatus, string? PAYChkHosp, bool OnPanel);
public record CasePreChecks(bool NotActivated, bool RecentlyActivated, bool SameDayAdmission);

/// <summary>Case > Registration (legacy frmCaseAdmission): member load, checks and the transactional save.</summary>
public class CaseRegistrationService(DbConnections db, ILogger<CaseRegistrationService> log)
{
    private static readonly string[] FamilyInsuredTypes = ["F", "H"];

    private record PlanPrompt(string? AnnLmtIND, string? MessagePromt, int? PLNStartAge, int? PLNEndAge, string? PLNPolicyWording);
    private record Principal(string? MBMName, string? MBMIcBcPp, string? MBMMemberType, DateTime? MBMDOB, string? MBMTopupInd, string? PAYChkHosp);
    private record Covered(string MBMCCoverID, string? MBMCName, string? MBMCICBCPPNo, string? MBMCRELCode, DateTime? MBMCDOB,
        decimal? MBMCAnnualLimit, string? MBMCExclusion);

    private static string S(object? value) => (value?.ToString() ?? "").Trim();
    private static DateTime? D(object? value) => value as DateTime?;

    /// <summary>cmdShow_Click: SearchMBMCas plus the plan / limit / agent reads, with the desktop's alerts in order.</summary>
    public async Task<CaseMemberView?> LoadMemberAsync(string mbmNumber)
    {
        await using var conn = db.MainDb();
        var rows = ToDictionaries(await conn.QueryAsync("SearchMBMCas", new { MBMNumber = mbmNumber.Trim() },
            commandType: CommandType.StoredProcedure));
        if (rows.Count == 0) return null;
        var m = rows[0];
        string pay = S(m["PAYCode"]), ins = S(m["INSCode"]), plan = S(m["PLNCode"]), hlt = S(m["HLTCode"]);
        var eff = D(m["MBMPayorEffDate"]) ?? DateTime.MinValue;
        var exp = D(m["MBMPayorExpDate"]) ?? DateTime.MinValue;
        var agentCode = S(m["MBMAgentCode"]);

        using var grid = await conn.ProcMultipleAsync("genisis.CaseReg_MemberExtras", new
        {
            mbm = S(m["MBMNumber"]), plan, pay, hlt, ins, eff, agent = agentCode.Length > 0 ? agentCode : null,
        });
        var prompts = (await grid.ReadAsync<PlanPrompt>()).ToList();
        var limit = await grid.ReadFirstOrDefaultAsync<decimal?>();
        var agentName = await grid.ReadFirstOrDefaultAsync<string?>();
        var principal = await grid.ReadFirstOrDefaultAsync<Principal>();
        var principalExclusion = await grid.ReadFirstOrDefaultAsync<string?>();
        var covered = (await grid.ReadAsync<Covered>()).ToList();
        var now = await grid.ReadSingleAsync<DateTime>();

        var alerts = new List<CaseAlert>();
        foreach (var p in prompts.Where(p => !string.IsNullOrWhiteSpace(p.MessagePromt)))
            alerts.Add(new(p.MessagePromt!.Trim(), false));
        var last = prompts.LastOrDefault();

        string annualLimit;
        if (S(last?.AnnLmtIND) == "Y")
        {
            annualLimit = limit is null ? "" : limit.Value.ToString("#,##0.00");
            if (limit is null)
                alerts.Add(new("Annual Limit Information Is Not Correct!\nPlease check you Maintenance Table", true));
        }
        else annualLimit = "FULL";

        string? agentLabel = null;
        if (agentName is not null)
        {
            agentLabel = $"Alert... Agent Details : {agentName.Trim()}({agentCode})";
            alerts.Add(new(agentLabel, true));
        }
        if (pay == "XL" && (now - eff).TotalDays <= 90) alerts.Add(new("90 days Non-Cashless!", true));

        var canRegister = true;
        string? expiryLabel = null;
        if (exp < now)
        {
            alerts.Add(new("Membership has been expired!", true));
            expiryLabel = "Expired";
            canRegister = false;
        }
        else if ((exp - now).TotalDays <= 30) expiryLabel = "< 30 Days to Expiry";

        var polStatus = S(m["MBMSuspendStatus"]) == "Y" ? "S" : S(m["MBMStatus"]);
        if (polStatus == "S")
        {
            alerts.Add(new($"Membership has been suspended since {D(m["MBMSuspendDate"])?.ToString("dd/MM/yyyy")}.", true));
            canRegister = false;
        }
        if (polStatus == "C")
        {
            alerts.Add(new("Membership has been cancelled.", true));
            canRegister = false;
        }
        if (pay == "AX" && D(m["MBMPolEffDate"]) != eff) alerts.Add(new("Please check Take Over clause", true));
        var glAllowed = S(m["MBMGLIssuance"]) != "N";
        if (!glAllowed) alerts.Add(new("GL is NOT allowed for this member", false));

        var family = FamilyInsuredTypes.Contains(ins);
        var patients = new List<CasePatientOption> { new("00", S(m["MBMName"])) };
        if (family)
            patients.AddRange(rows.Where(r => S(r["MBMCCoverID"]).Length > 0)
                .Select(r => new CasePatientOption(S(r["MBMCCoverID"]), S(r["MBMCName"]))));

        var members = new List<CaseMemberRow>();
        if (principal is not null)
            members.Add(new("00", principal.MBMName?.Trim(), principal.MBMIcBcPp?.Trim(), principal.MBMMemberType?.Trim(), principal.MBMDOB, null, principalExclusion?.Trim()));
        if (family)
            members.AddRange(covered.Select(c => new CaseMemberRow(c.MBMCCoverID.Trim(), c.MBMCName?.Trim(), c.MBMCICBCPPNo?.Trim(),
                c.MBMCRELCode?.Trim(), c.MBMCDOB, c.MBMCAnnualLimit, c.MBMCExclusion?.Trim())));

        return new CaseMemberView(m, polStatus, annualLimit, patients, members, principalExclusion?.Trim(),
            last?.PLNStartAge, last?.PLNEndAge, last?.PLNPolicyWording?.Trim(), agentLabel,
            (now - eff).TotalDays <= 30, expiryLabel, canRegister, S(m["MBMTerminateStatus"]) != "Y" && glAllowed, alerts, now);
    }

    public async Task<CaseHospitalInfo?> HospitalAsync(SqlConnection conn, string hospitalCode, string pay, string? policyNo, SqlTransaction? tx = null) =>
        await conn.ProcFirstOrDefaultAsync<CaseHospitalInfo>("genisis.CaseReg_HospitalCheck",
            new { hosCode = hospitalCode.Trim(), pay = pay.Trim(), policyNo = policyNo?.Trim() }, tx);

    public static bool IsNamedHospital(string? name) =>
        CaseRegistrationValidator.NamedHospitals.Contains((name ?? "").Trim().ToUpperInvariant());

    /// <summary>txtHOSCode_LostFocus: a cashless case at a private hospital needs the hospital on the payor's panel
    /// when the payor checks hospitals.</summary>
    public static bool NotOnPanel(CaseHospitalInfo h, string? payType) =>
        payType?.Trim().StartsWith('C') == true && !IsNamedHospital(h.HOSName)
        && h.HOSStatus?.Trim() == "P" && h.PAYChkHosp?.Trim() == "Y" && !h.OnPanel;

    /// <summary>The checks NewCaseAddmission and the date LostFocus handlers make against the member, in that order.</summary>
    internal static string? CheckAgainstMember(CaseRegistrationRequest r, CaseMemberInfo m, CaseHospitalInfo? h, CasePreChecks p, DateTime now)
    {
        var today = now.Date;
        var cashless = r.PayType?.Trim().StartsWith('C') == true;
        if (h is null) return "Please Enter Hospital Information";
        if (IsNamedHospital(h.HOSName) && string.IsNullOrWhiteSpace(r.HospitalName)) return "Please Enter Hospital Name or Clinic Name!";
        if (NotOnPanel(h, r.PayType)) return "The selected hospital is not on the panel!";

        if (r.CheckAdmitted && r.DateAdmitted is { } doa)
        {
            if (doa.Date > today) return "Admission Date cannot greater than system date !";
            if (doa.Date < m.EffDate.Date) return "Admission Date cannot be less than Effective Date!";
            if (cashless && m.ExpDate.Date < doa.Date) return "Admission Date cannot be greater than Expiry Date!";
        }
        if (r.CheckDischarged && r.DateDischarged is { } dod)
        {
            if (dod.Date > today) return "Discharge Date cannot be greater than system date !";
            if (dod.Date < m.EffDate.Date) return "Discharge Date cannot be less than Effective Date!";
            if (cashless && m.ExpDate.Date < dod.Date) return "Discharge Date cannot be greater than Expiry Date!";
        }

        // cmdShow disables Register for these; the desktop re-enables it only when both D.O.A and D.O.D are unticked.
        if (r.CheckAdmitted || r.CheckDischarged)
        {
            if (m.ExpDate < now) return "Membership has been expired!";
            if (m.SuspendStatus?.Trim() == "Y") return $"Membership has been suspended since {m.SuspendDate?.ToString("dd/MM/yyyy")}.";
        }
        if (m.Status?.Trim() == "C") return "Policy has been cancelled!";
        if (r.CheckAdmitted && p.NotActivated) return "Policy not yet activated!";
        return null;
    }

    /// <summary>Runs every save check without writing; returns the flags behind the desktop's confirmation questions.</summary>
    public async Task<CasePreChecks> ValidateAsync(CaseRegistrationRequest r)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        var (_, checks, _) = await CheckAsync(conn, null, r);
        return checks;
    }

    private async Task<(CaseMemberInfo, CasePreChecks, DateTime)> CheckAsync(SqlConnection conn, SqlTransaction? tx, CaseRegistrationRequest r)
    {
        var mbm = S(r.MbmNumber);
        var row = (await conn.QueryAsync("SearchMBMCas", new { MBMNumber = mbm }, tx, commandType: CommandType.StoredProcedure))
            .Select(ToDictionary).FirstOrDefault() ?? throw new CaseRegistrationException("No record found!");
        var member = new CaseMemberInfo(S(row["PAYCode"]), S(row["MBMPolicyNo"]), S(row["MBMStatus"]), S(row["MBMSuspendStatus"]),
            D(row["MBMSuspendDate"]), D(row["MBMPayorEffDate"]) ?? DateTime.MinValue, D(row["MBMPayorExpDate"]) ?? DateTime.MinValue,
            S(row["MBMCostCenter"]));
        var hospital = await HospitalAsync(conn, S(r.HospitalCode), member.PayCode, member.PolicyNo, tx);
        var checks = await conn.ProcSingleAsync<CasePreChecks>("genisis.CaseReg_PreChecks",
            new { mbm, coverId = S(r.CoverId), doa = r.DateAdmitted!.Value.Date }, tx);
        var now = await conn.ExecuteScalarAsync<DateTime>("SELECT GETDATE()", transaction: tx);
        if (CheckAgainstMember(r, member, hospital, checks, now) is { } error) throw new CaseRegistrationException(error);
        return (member, checks, now);
    }

    public async Task<CaseRegistrationResult> RegisterAsync(CaseRegistrationRequest r, string userCode)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            var caseNumber = await SaveAsync(conn, tx, r, userCode);
            await tx.CommitAsync();
            log.LogInformation("Case {CaseNumber} registered for member {MbmNumber} by {User}", caseNumber, r.MbmNumber, userCode);
            return new(caseNumber);
        }
        catch (CaseRegistrationException)
        {
            await tx.RollbackAsync();
            throw;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            log.LogError(ex, "Case registration failed for member {MbmNumber} by {User}; transaction rolled back", r.MbmNumber, userCode);
            throw;
        }
    }

    /// <summary>NewCaseAddmission: checks, then CAS, remarks, cross reference, diagnoses, UD, case others,
    /// balance limit and repudiation, all inside the caller's transaction (the desktop wrote the cross reference
    /// outside its transaction).</summary>
    internal async Task<string> SaveAsync(SqlConnection conn, SqlTransaction tx, CaseRegistrationRequest r, string userCode)
    {
        var (member, _, now) = await CheckAsync(conn, tx, r);
        var caseNumber = await conn.ProcSingleAsync<string>("genisis.CaseReg_NextCaseNo", new { payor = member.PayCode }, tx);
        await InsertAsync(conn, tx, r, caseNumber, member, userCode, now.Date);
        return caseNumber;
    }

    private static Task Exec(SqlConnection conn, SqlTransaction tx, string proc, object p) =>
        conn.ExecuteAsync(proc, p, tx, commandType: CommandType.StoredProcedure);

    /// <summary>Remarks are stored with the desktop's line break marker (CR replaced by ~).</summary>
    internal static string Remark(string? text) => S(text).Replace("\r\n", "\n").Replace("\n", "~\n");

    private static (string, string) Split1900(string text) =>
        text.Length <= 1900 ? (text, "") : (text[..1900], text[1900..]);

    private static async Task InsertAsync(SqlConnection conn, SqlTransaction tx, CaseRegistrationRequest r, string caseNumber,
        CaseMemberInfo member, string userCode, DateTime today)
    {
        static T? At<T>(List<T> list, int i) => i < list.Count ? list[i] : default;
        static int Count(IEnumerable<string?> values) => values.Count(v => !string.IsNullOrWhiteSpace(v));

        var mbm = S(r.MbmNumber);
        var cover = S(r.CoverId);
        var payType = S(r.PayType);
        var reimbursement = payType.StartsWith('R');
        var claimability = S(r.Claimability).ToUpperInvariant();
        var hospital = S(r.HospitalCode);
        var dod = r.DateDischarged?.Date;

        var cas = new DynamicParameters(new
        {
            CasNumber = caseNumber, MBMNumber = mbm, MBMPatientCovID = cover, CASStatus = "O", CASPending = S(r.Pending),
            CASReserveAmount = r.ReserveAmount ?? 0, CASLGAmt = r.LgAmount ?? 0,
            CASDateAdmitted = r.DateAdmitted?.Date, CASDischargeDate = dod, CASClaimability = claimability,
            CASInd = S(r.SpecialApproval) is { Length: > 0 } sa ? sa[..1] : "",
            ModalityCode = S(r.TreatmentMode), CASStayType = S(r.StayType), CASPAYType = payType,
            CASClaimAdmin = reimbursement ? "1" : "0",
            LINKCASNo = "", ClaimNatureCode = S(r.ClaimNature), CaseOpenBy = userCode, CaseRegDate = today,
            CASTopupLmt = S(r.TopUpLimit), CASHOSCode = hospital, CASDRD = (DateTime?)null, CASQuery = S(r.QueryStatus),
            CASTakeOverAmt = r.TakeOverAmount ?? 0, CASTakeOverInd = S(r.TakeOverInd), CASTakeOverPlan = S(r.TakeOverPlan),
            CASLGTime = S(r.LgTime), CASPolYear = int.TryParse(r.PolicyYear, out var year) ? year : 0,
            CASCostCenter = member.CostCenter ?? "",
        });
        if (reimbursement)
        {
            cas.Add("CASTokenDate", today);
            cas.Add("CASUserToken", userCode);
        }
        await Exec(conn, tx, reimbursement ? "dbo.Case_RegInsertCAS2013" : "dbo.Case_RegInsertCAS32013", cas);

        var (remark1a, remark1b) = Split1900(Remark(At(r.Remarks, 0)));
        await Exec(conn, tx, "dbo.Case_RegInsertCASRemark", new { CasNumber = caseNumber, CASORemarks = remark1a, CASORemarksTwo = remark1b });
        var (remark2a, remark2b) = Split1900(Remark(At(r.Remarks, 1)));
        await Exec(conn, tx, "dbo.Case_RegInsertCASRemark2", new { CasNumber = caseNumber, CASORemarks = remark2a, CASORemarksTwo = remark2b });
        await Exec(conn, tx, "dbo.Case_RegInsertCASRemark3", new
        {
            CasNumber = caseNumber, CASRemarks = Remark(At(r.Remarks, 2)), CASRemarksFour = Remark(At(r.Remarks, 3)), CASRemarksFive = Remark(At(r.Remarks, 4)),
        });

        var xref = new DynamicParameters(new
        {
            CasNumber = caseNumber, MBMNumber = mbm, MBMPatientCovID = cover, CASStatus = "O", CASPending = S(r.Pending),
            CASHOSCode = hospital, CASDateAdmitted = r.DateAdmitted?.Date,
            CASClaimability = claimability.Length > 0 ? claimability[..1] : "",
            LINKCASNo = "", CASTopupLmt = S(r.TopUpLimit), CASPayPending = S(r.PaymentPending),
        });
        if (dod is not null) xref.Add("CASDischargeDate", dod);
        await Exec(conn, tx, dod is not null ? "genisis.Maint_Case_RegInsertCASRef" : "genisis.Maint_Case_RegInsertCASRef1", xref);

        DiagnosisLine Diag(List<DiagnosisLine> list, int i) => At(list, i) ?? new DiagnosisLine(null, null, null);
        DiagnosisLine p1 = Diag(r.PrelimDiagnoses, 0), p2 = Diag(r.PrelimDiagnoses, 1), p3 = Diag(r.PrelimDiagnoses, 2);
        await Exec(conn, tx, "dbo.Case_RegInsertDIAFirst", new
        {
            CasNumber = caseNumber, ICDCode1 = S(p1.Icd), ICDCode2 = S(p2.Icd), ICDCode3 = S(p3.Icd),
            DIAPrelimDiag1 = S(p1.Text), DIAPrelimDiag2 = S(p2.Text), DIAPrelimDiag3 = S(p3.Text),
            DIADOS1 = p1.Onset?.Date, DIADOS2 = p2.Onset?.Date, DIADOS3 = p3.Onset?.Date,
        });
        DiagnosisLine f1 = Diag(r.FinalDiagnoses, 0), f2 = Diag(r.FinalDiagnoses, 1), f3 = Diag(r.FinalDiagnoses, 2);
        await Exec(conn, tx, "dbo.Case_RegInsertDIAFinal", new
        {
            CasNumber = caseNumber, ICDCode1 = S(f1.Icd), ICDCode2 = S(f2.Icd), ICDCode3 = S(f3.Icd),
            DIAFinalDiag1 = S(f1.Text), DIAFinalDiag2 = S(f2.Text), DIAFinalDiag3 = S(f3.Text),
            DIADOS1 = f1.Onset?.Date, DIADOS2 = f2.Onset?.Date, DIADOS3 = f3.Onset?.Date,
        });
        DiagnosisLine u1 = Diag(r.UnderlyingDiseases, 0), u2 = Diag(r.UnderlyingDiseases, 1), u3 = Diag(r.UnderlyingDiseases, 2);
        static string Ud(string? code) => S(code) is { Length: > 5 } c ? c[..5] : S(code);
        await Exec(conn, tx, "dbo.Case_RegInsertUD", new
        {
            CasNumber = caseNumber, CASUDCode1 = Ud(u1.Icd), CASUDCode2 = Ud(u2.Icd), CASUDCode3 = Ud(u3.Icd),
            CASUDReasons1 = S(u1.Text), CASUDReasons2 = S(u2.Text), CASUDReasons3 = S(u3.Text),
            CASUDDOS1 = u1.Onset?.Date, CASUDDOS2 = u2.Onset?.Date, CASUDDOS3 = u3.Onset?.Date,
        });

        ProcedureLine Proc(List<ProcedureLine> list, int i) => At(list, i) ?? new ProcedureLine(null, null);
        var others = new DynamicParameters(new
        {
            CasNumber = caseNumber,
            CASOPROCInvestigationDone1 = S(Proc(r.Investigations, 0).Procedure),
            CASOPROCInvestigationDone2 = S(Proc(r.Investigations, 1).Procedure),
            CASOPROCInvestigationDone3 = S(Proc(r.Investigations, 2).Procedure),
            CASOMedSurMgmt1 = S(At(r.MedicalManagement, 0)), CASOMedSurMgmt2 = S(At(r.MedicalManagement, 1)), CASOMedSurMgmt3 = S(At(r.MedicalManagement, 2)),
            CASOCondWillRecur = S(r.Recur), CASOFollowUpRequired = S(r.FollowUpRequired),
            CASFirstCall = r.FirstCall?.ToString("dd/MM/yyyy") ?? "", CASFirstTreated = r.FirstTreated?.ToString("dd/MM/yyyy") ?? "",
            CASRedAmt = r.ReductionAmount ?? 0,
            CASInvCode1 = S(Proc(r.Investigations, 0).Code), CASInvCode2 = S(Proc(r.Investigations, 1).Code), CASInvCode3 = S(Proc(r.Investigations, 2).Code),
            CASFolUp = S(r.FollowUp), CASPayPending = S(r.PaymentPending),
            CASPatientAge = S(r.PatientAge), CASPatientDayYr = S(r.AgeUnit),
        });
        for (var i = 0; i < 6; i++)
        {
            others.Add($"CASOPROCRequired{i + 1}", S(Proc(r.SurgicalProcedures, i).Procedure));
            others.Add($"CASCptCode{i + 1}", S(Proc(r.SurgicalProcedures, i).Code));
            others.Add($"CASONonSurgPROCRequired{i + 1}", S(Proc(r.NonSurgicalProcedures, i).Procedure));
            others.Add($"CASNonSurgCptCode{i + 1}", S(Proc(r.NonSurgicalProcedures, i).Code));
        }
        await Exec(conn, tx, "dbo.Case_RegInsertCASOthers", others);

        var doctors = Enumerable.Range(0, 5).Select(i => S(At(r.AttendingDoctors, i))).ToList();
        var anaes = Enumerable.Range(0, 3).Select(i => S(At(r.AnaesDoctors, i))).ToList();
        await Exec(conn, tx, "dbo.Case_RegInsertCASOthers2", new
        {
            CasNumber = caseNumber,
            CASAttendDoctor1 = doctors[0], CASAttendDoctor2 = doctors[1], CASAttendDoctor3 = doctors[2], CASAttendDoctor4 = doctors[3], CASAttendDoctor5 = doctors[4],
            CASAnaesName1 = anaes[0], CASAnaesName2 = anaes[1], CASAnaesName3 = anaes[2],
            CASTotalDoctors = Count(doctors), CASTotalAnaes = Count(anaes),
            CASTotalFinalDiag = Count([f1.Icd, f2.Icd, f3.Icd]),
            CASTotalProc = Count(Enumerable.Range(0, 6).Select(i => Proc(r.SurgicalProcedures, i).Procedure)),
            CASAdmissionReason = S(r.Reason), ReferedBy = S(r.ReferredBy),
            CASFirstDocDate = r.FirstDocDate?.Date, CASSubSeqDocDate = r.SubsequentDocDate?.Date,
            CASIntExtTO = S(r.IntExtTakeOver), CASHOSName = S(r.HospitalName),
        });

        if (r.ReductionAmount is { } reduction)
            await Exec(conn, tx, "genisis.CaseReg_ReduceLimit", new { mbm, amount = reduction });

        if (CaseRegistrationValidator.IsRepudiated(claimability))
            await Exec(conn, tx, "dbo.Case_RegInsertCASRep", new
            {
                CasNumber = caseNumber,
                CASREPCode1 = S(At(r.RepudiationCodes, 0)), CASREPCode2 = S(At(r.RepudiationCodes, 1)), CASREPCode3 = S(At(r.RepudiationCodes, 2)),
                CASREPIssuedBy = userCode, CASREPDate = today,
                CASDocument = S(r.RepDocument), CASPersonnel = S(r.RepPersonnel), CASAttention = S(r.RepAttention),
                CASUnderlying = S(r.RepUnderlying) == "Y" ? "Y" : "N",
            });
    }
}
