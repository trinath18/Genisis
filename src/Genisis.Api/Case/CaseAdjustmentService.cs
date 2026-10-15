using System.Data;
using System.Text;
using Dapper;
using Genisis.Api.Data;
using Microsoft.Data.SqlClient;
using static Genisis.Api.Data.RowMapper;

namespace Genisis.Api.Case;

public record CaseAdjustmentView(
    Dictionary<string, object?> Case,
    CaseMemberView? Member,
    Dictionary<string, object?>? FirstDiagnosis,
    Dictionary<string, object?>? FinalDiagnosis,
    Dictionary<string, object?>? Repudiation,
    string? PendingReason,
    string? PolicyWording,
    bool HasLg,
    bool CanUpdate,
    bool LgEnabled,
    bool AskResetClaimability,
    List<CaseAlert> Alerts);

public record CaseNotesTemplate(string Page1, string Page2);

/// <summary>Case > Adjustment (legacy frmCaseAdjustmentNew): load, navigation, the transactional update,
/// repudiation and the case-progress actions.</summary>
public class CaseAdjustmentService(DbConnections db, CaseRegistrationService registration, ILogger<CaseAdjustmentService> log)
{
    private record State(string MBMNumber, string MBMPatientCovID, string? CASStatus, bool HasRemark, bool HasRemark2,
        bool HasRemark3, bool HasXref, bool HasDiaFirst, bool HasDiaFinal, bool HasUD, bool HasOthers, bool HasOthers2,
        bool HasRep, decimal PrevRedAmt, decimal? AvailableLimit);

    private static string S(object? value) => (value?.ToString() ?? "").Trim();
    private static string Code(string? value) => S(value) is var s && s.Contains('-') ? s[..s.IndexOf('-')].Trim() : s;
    private static string First(string? value, int n) => S(value) is { Length: > 0 } s ? s[..Math.Min(n, s.Length)] : "";
    private static Task Exec(SqlConnection conn, SqlTransaction tx, string proc, object p) =>
        conn.ExecuteAsync(proc, p, tx, commandType: CommandType.StoredProcedure);

    /// <summary>CmdNext_Click / cmdPrev_Click.</summary>
    public async Task<string?> NavigateAsync(string cas, bool next)
    {
        await using var conn = db.MainDb();
        return await conn.ProcFirstOrDefaultAsync<string?>("genisis.CaseAdj_Navigate", new { cas = cas.Trim(), next });
    }

    /// <summary>cmdShow_Click / showCAS: the case (SearchCasAdjustment), the member and the side reads.</summary>
    public async Task<CaseAdjustmentView?> LoadAsync(string cas, string userCode)
    {
        await using var conn = db.MainDb();
        var row = await conn.QueryFirstOrDefaultAsync("SearchCasAdjustment", new { CASNumber = cas.Trim() },
            commandType: CommandType.StoredProcedure);
        if (row is null) return null;
        var c = ToDictionary(row);

        var member = await registration.LoadMemberAsync(S(c["MBMNumber"]));
        using var grid = await conn.ProcMultipleAsync("genisis.CaseAdj_Extras", new
        {
            cas = S(c["CASNumber"]), plan = S(c["PLNCode"]), pay = S(c["PAYCode"]), hlt = S(c["HLTCode"]),
        });
        var first = (await grid.ReadAsync()).Select(ToDictionary).FirstOrDefault();
        var final = (await grid.ReadAsync()).Select(ToDictionary).FirstOrDefault();
        var rep = (await grid.ReadAsync()).Select(ToDictionary).FirstOrDefault();
        var pendingReason = await grid.ReadFirstOrDefaultAsync<string?>();
        var wording = await grid.ReadFirstOrDefaultAsync<string?>();
        var hasLg = await grid.ReadSingleAsync<bool>();

        await using var maint = db.Maintenance();
        var blockGl = await maint.ExecuteScalarAsync<string?>("SELECT USRBlockGL FROM dbo.USR WHERE USRCode = @userCode", new { userCode });

        var open = S(c["CASStatus"]) == "O";
        var glAllowed = S(c["MBMGLIssuance"]) != "N";
        var alerts = new List<CaseAlert>();
        if (!glAllowed) alerts.Add(new("GL is NOT allowed for this member", false));
        // showCAS / cmdShow_Click: LG needs an open, non-repudiated case of an active member the user may issue GLs for.
        var lgEnabled = open && !CaseRegistrationValidator.IsRepudiated(S(c["CASClaimability"]))
            && S(c["MBMTerminateStatus"]) != "Y" && glAllowed && S(blockGl) != "Y";

        return new(c, member, first, final, rep, pendingReason?.Trim(), wording?.Trim(), hasLg, open, lgEnabled,
            open && S(c["CASClaimability"]) == "AA" && S(c["CASPending"]) == "Y", alerts);
    }

    /// <summary>UpdateClaimability: the "change Claimability Status?" answer sets the case to NN.</summary>
    public async Task ResetClaimabilityAsync(string cas, string userCode)
    {
        await using var conn = db.MainDb();
        await conn.ExecuteAsync("dbo.Case_AdjUpdateCAS1", new { CasNumber = cas.Trim(), CASClaimability = "NN" },
            commandType: CommandType.StoredProcedure);
        log.LogInformation("Case {CaseNumber} claimability reset to NN by {User}", cas, userCode);
    }

    /// <summary>cmdUpdate1_Click: every write of the desktop update, in its order, inside one transaction
    /// (the desktop ran them without a transaction across two databases).</summary>
    public async Task UpdateAsync(CaseAdjustmentRequest r, string userCode)
    {
        var cas = S(r.CaseNumber);
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            await UpdateAsync(conn, tx, r, userCode);
            await tx.CommitAsync();
            log.LogInformation("Case {CaseNumber} adjusted by {User}", cas, userCode);
        }
        catch (CaseRegistrationException)
        {
            await tx.RollbackAsync();
            throw;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            log.LogError(ex, "Case adjustment failed for {CaseNumber} by {User}; transaction rolled back", cas, userCode);
            throw;
        }
    }

    /// <summary>The update inside the caller's transaction.</summary>
    internal static async Task UpdateAsync(SqlConnection conn, SqlTransaction tx, CaseAdjustmentRequest r, string userCode)
    {
        var cas = S(r.CaseNumber);
        var state = await conn.ProcFirstOrDefaultAsync<State>("genisis.CaseAdj_State", new { cas }, tx)
            ?? throw new CaseRegistrationException("No record found!");
        if (S(state.CASStatus) != "O") throw new CaseRegistrationException("Closed or deleted cases cannot be updated.");
        await CheckHospitalNameAsync(conn, tx, r);
        var today = (await conn.ExecuteScalarAsync<DateTime>("SELECT GETDATE()", transaction: tx)).Date;
        await WriteAsync(conn, tx, r, cas, state, userCode, today);
    }

    /// <summary>The placeholder hospitals need the actual hospital / clinic name typed in.</summary>
    private static async Task CheckHospitalNameAsync(SqlConnection conn, SqlTransaction tx, CaseAdjustmentRequest r)
    {
        if (S(r.HospitalCode).Length == 0 || S(r.HospitalName).Length > 0) return;
        var name = await conn.ExecuteScalarAsync<string?>("SELECT TOP (1) HOSName FROM dbo.HOS WHERE HOSCode = @code",
            new { code = S(r.HospitalCode) }, tx);
        if (CaseRegistrationService.IsNamedHospital(name)) throw new CaseRegistrationException("Please Enter Hospital Name or Clinic Name!");
    }

    private static async Task WriteAsync(SqlConnection conn, SqlTransaction tx, CaseAdjustmentRequest r, string cas, State state,
        string userCode, DateTime today)
    {
        static T? At<T>(List<T> list, int i) => i < list.Count ? list[i] : default;
        static int Count(IEnumerable<string?> values) => values.Count(v => !string.IsNullOrWhiteSpace(v));
        var claimability = First(r.Claimability, 2).ToUpperInvariant();

        await Exec(conn, tx, "dbo.Case_AdjUpdateCAS", new
        {
            CasNumber = cas, CASStatus = First(r.Status, 1), CASClaimability = claimability,
            CASDateAdmitted = r.DateAdmitted?.Date, CASDischargeDate = r.DateDischarged?.Date,
            ModalityCode = First(r.TreatmentMode, 1), CASStayType = First(r.StayType, 1), CASPAYType = First(r.PayType, 1),
            ClaimNatureCode = First(r.ClaimNature, 3), CASPending = First(r.Pending, 1),
            CASReserveAmount = r.ReserveAmount ?? 0, LINKCASNo = S(r.LinkCaseNo), CASInd = First(r.SpecialApproval, 1),
            CASLastUpdateUser = userCode, CASLastUpdateDate = today, CASLGAmt = r.LgAmount ?? 0,
            CASTopupLmt = First(r.TopUpLimit, 1), CASDRD = r.DocReceivedDate?.Date, CASQuery = First(r.QueryStatus, 1),
            CASTakeOverAmt = r.TakeOverAmount ?? 0, CASTakeOverInd = First(r.TakeOverInd, 1), CASTakeOverPlan = S(r.TakeOverPlan),
            CASRoomBoardDay = 0, CASLGTime = S(r.LgTime), CASPolYear = int.Parse(S(r.PolicyYear)),
        });

        var (remark1a, remark1b) = Split1900(CaseRegistrationService.Remark(At(r.Remarks, 0)));
        await Exec(conn, tx, state.HasRemark ? "dbo.Case_AdjUpdateCASRemark" : "dbo.Case_AdjInsertCASRemark",
            new { CASNumber = cas, CASORemarks = remark1a, CASORemarksTwo = remark1b });
        var (remark2a, remark2b) = Split1900(CaseRegistrationService.Remark(At(r.Remarks, 1)));
        await Exec(conn, tx, state.HasRemark2 ? "dbo.Case_AdjUpdateCASRemark2" : "dbo.Case_AdjInsertCASRemark2",
            new { CASNumber = cas, CASORemarks = remark2a, CASORemarksTwo = remark2b });
        await Exec(conn, tx, state.HasRemark3 ? "dbo.Case_AdjUpdateCASRemark3" : "dbo.Case_AdjInsertCASRemark3", new
        {
            CASNumber = cas, CASRemarks = CaseRegistrationService.Remark(At(r.Remarks, 2)),
            CASRemarksFour = CaseRegistrationService.Remark(At(r.Remarks, 3)), CASRemarksFive = CaseRegistrationService.Remark(At(r.Remarks, 4)),
        });

        await Exec(conn, tx, state.HasXref ? "genisis.Maint_Case_AdjUpdateCASRef" : "genisis.Maint_Case_AdjInsertCASRef", new
        {
            CasNumber = cas, CASStatus = First(r.Status, 1), CASClaimability = claimability,
            CASDateAdmitted = r.DateAdmitted?.Date, CASDischargeDate = r.DateDischarged?.Date, CASPending = First(r.Pending, 1),
            LINKCASNo = S(r.LinkCaseNo), CASTopupLmt = First(r.TopUpLimit, 1), MBMNumber = state.MBMNumber,
            MBMPatientCovID = state.MBMPatientCovID, CASPayPending = First(r.PaymentPending, 1),
        });

        DiagnosisLine Diag(List<DiagnosisLine> list, int i) => At(list, i) ?? new DiagnosisLine(null, null, null);
        DiagnosisLine p1 = Diag(r.PrelimDiagnoses, 0), p2 = Diag(r.PrelimDiagnoses, 1), p3 = Diag(r.PrelimDiagnoses, 2);
        await Exec(conn, tx, state.HasDiaFirst ? "dbo.Case_AdjUpdateDIAFirst" : "dbo.Case_AdjInsertDIAFirst", new
        {
            CasNumber = cas, ICDCode1 = First(p1.Icd, 3), ICDCode2 = First(p2.Icd, 3), ICDCode3 = First(p3.Icd, 3),
            DIAPrelimDiag1 = S(p1.Text), DIAPrelimDiag2 = S(p2.Text), DIAPrelimDiag3 = S(p3.Text),
            DIADOS1 = p1.Onset?.Date, DIADOS2 = p2.Onset?.Date, DIADOS3 = p3.Onset?.Date,
        });
        DiagnosisLine f1 = Diag(r.FinalDiagnoses, 0), f2 = Diag(r.FinalDiagnoses, 1), f3 = Diag(r.FinalDiagnoses, 2);
        await Exec(conn, tx, state.HasDiaFinal ? "dbo.Case_AdjUpdateDIAFinal" : "dbo.Case_AdjInsertDIAFinal", new
        {
            CasNumber = cas, ICDCode1 = First(f1.Icd, 3), ICDCode2 = First(f2.Icd, 3), ICDCode3 = First(f3.Icd, 3),
            DIAFinalDiag1 = S(f1.Text), DIAFinalDiag2 = S(f2.Text), DIAFinalDiag3 = S(f3.Text),
            DIADOS1 = f1.Onset?.Date, DIADOS2 = f2.Onset?.Date, DIADOS3 = f3.Onset?.Date,
            DIALastAdjustmentUser = userCode, DIALastAdjustmentDate = today,
        });
        DiagnosisLine u1 = Diag(r.UnderlyingDiseases, 0), u2 = Diag(r.UnderlyingDiseases, 1), u3 = Diag(r.UnderlyingDiseases, 2);
        await Exec(conn, tx, state.HasUD ? "dbo.Case_AdjUpdateUD" : "dbo.Case_AdjInsertUD", new
        {
            CasNumber = cas, CASUDCode1 = First(u1.Icd, 3), CASUDCode2 = First(u2.Icd, 3), CASUDCode3 = First(u3.Icd, 3),
            CASUDReasons1 = S(u1.Text), CASUDReasons2 = S(u2.Text), CASUDReasons3 = S(u3.Text),
            CASUDDOS1 = u1.Onset?.Date, CASUDDOS2 = u2.Onset?.Date, CASUDDOS3 = u3.Onset?.Date,
        });

        ProcedureLine Proc(List<ProcedureLine> list, int i) => At(list, i) ?? new ProcedureLine(null, null);
        var others = new DynamicParameters(new
        {
            CasNumber = cas,
            CASOPROCInvestigationDone1 = S(Proc(r.Investigations, 0).Procedure),
            CASOPROCInvestigationDone2 = S(Proc(r.Investigations, 1).Procedure),
            CASOPROCInvestigationDone3 = S(Proc(r.Investigations, 2).Procedure),
            CASOMedSurMgmt1 = S(At(r.MedicalManagement, 0)), CASOMedSurMgmt2 = S(At(r.MedicalManagement, 1)), CASOMedSurMgmt3 = S(At(r.MedicalManagement, 2)),
            CASOCondWillRecur = First(r.Recur, 1), CASOFollowUpRequired = First(r.FollowUpRequired, 1),
            CASFirstCall = r.FirstCall?.ToString("dd/MM/yyyy") ?? "", CASFirstTreated = r.FirstTreated?.ToString("dd/MM/yyyy") ?? "",
            CASRedAmt = r.ReductionAmount ?? 0,
            CASInvCode1 = S(Proc(r.Investigations, 0).Code), CASInvCode2 = S(Proc(r.Investigations, 1).Code), CASInvCode3 = S(Proc(r.Investigations, 2).Code),
            CASFolUp = First(r.FollowUp, 1), CASPayPending = First(r.PaymentPending, 1),
            CASPatientAge = S(r.PatientAge), CASPatientDayYr = First(r.AgeUnit, 1),
        });
        for (var i = 0; i < 6; i++)
        {
            others.Add($"CASOPROCRequired{i + 1}", S(Proc(r.SurgicalProcedures, i).Procedure));
            others.Add($"CASCptCode{i + 1}", S(Proc(r.SurgicalProcedures, i).Code));
            others.Add($"CASONonSurgPROCRequired{i + 1}", S(Proc(r.NonSurgicalProcedures, i).Procedure));
            others.Add($"CASNonSurgCptCode{i + 1}", S(Proc(r.NonSurgicalProcedures, i).Code));
        }
        await Exec(conn, tx, state.HasOthers ? "dbo.Case_AdjUpdateCASOthers" : "dbo.Case_AdjInsertCASOthers", others);

        var doctors = Enumerable.Range(0, 5).Select(i => S(At(r.AttendingDoctors, i))).ToList();
        var anaes = Enumerable.Range(0, 3).Select(i => S(At(r.AnaesDoctors, i))).ToList();
        await Exec(conn, tx, state.HasOthers2 ? "dbo.Case_AdjUpdateCASOthers2" : "dbo.Case_AdjInsertCASOthers2", new
        {
            CasNumber = cas,
            CASAttendDoctor1 = doctors[0], CASAttendDoctor2 = doctors[1], CASAttendDoctor3 = doctors[2], CASAttendDoctor4 = doctors[3], CASAttendDoctor5 = doctors[4],
            CASAnaesName1 = anaes[0], CASAnaesName2 = anaes[1], CASAnaesName3 = anaes[2],
            CASTotalDoctors = Count(doctors), CASTotalAnaes = Count(anaes),
            CASTotalFinalDiag = Count([f1.Icd, f2.Icd, f3.Icd]),
            CASTotalProc = Count(Enumerable.Range(0, 5).Select(i => Proc(r.SurgicalProcedures, i).Procedure)),
            CASAdmissionReason = S(r.Reason), ReferedBy = S(r.ReferredBy),
            CASFirstDocDate = r.FirstDocDate?.Date, CASSubSeqDocDate = r.SubsequentDocDate?.Date, CASIntExtTO = First(r.IntExtTakeOver, 1),
            CASDOATime = (DateTime?)null, CASDODTime = (DateTime?)null, CASDOALGTime = (DateTime?)null, CASDODLGTime = (DateTime?)null,
            CASDOAComment = (DateTime?)null, CASDODComment = (DateTime?)null, CASComplaintDate = (DateTime?)null,
            CASComplaintIssue = "", CASComplainant = "", CASCompAction = "", CASHosName = S(r.HospitalName),
        });

        if (S(r.HospitalCode).Length > 0)
            await Exec(conn, tx, "dbo.Case_AdjUpdateHOS", new { CASNumber = cas, CASHOSCode = S(r.HospitalCode) });

        // UpdateBalLmt: give back the reduction saved before, then take off the new one.
        if (r.ReductionAmount is { } reduction)
        {
            var balance = (state.AvailableLimit ?? 0) + state.PrevRedAmt;
            await Exec(conn, tx, "dbo.Case_AdjUpdateBalLmt",
                new { MBMNumber = state.MBMNumber, MBMAvaliableLimit = balance > reduction ? balance - reduction : 0 });
        }

        if (claimability != "AA")
        {
            await SaveRepudiationAsync(conn, tx, r, cas, claimability, state.HasRep, userCode, today);
            await Exec(conn, tx, "genisis.CaseAdj_SetProgress", new { cas, isRejected = "1", isLGIssued = "1", progress = "CASE DECLINE" });
        }
        await Exec(conn, tx, "genisis.CaseAdj_SetProgress", new { cas, docRemark = S(r.PendingReason), setDocRemark = true });
    }

    private static (string, string) Split1900(string text) => text.Length <= 1900 ? (text, "") : (text[..1900], text[1900..]);

    /// <summary>SaveRepudiation: the case claimability plus the repudiation codes and letter selections.</summary>
    private static async Task SaveRepudiationAsync(SqlConnection conn, SqlTransaction tx, CaseAdjustmentRequest r, string cas,
        string claimability, bool hasRep, string userCode, DateTime today)
    {
        static T? At<T>(List<T> list, int i) => i < list.Count ? list[i] : default;
        await Exec(conn, tx, "dbo.Case_AdjUpdateCAS1", new { CasNumber = cas, CASClaimability = claimability });
        await Exec(conn, tx, hasRep ? "dbo.Case_AdjUpdateCASRep" : "dbo.Case_AdjInsertCASRep", new
        {
            CasNumber = cas, CASREPCode1 = Code(At(r.RepudiationCodes, 0)), CASREPCode2 = Code(At(r.RepudiationCodes, 1)),
            CASREPCode3 = Code(At(r.RepudiationCodes, 2)), CASREPIssuedBy = userCode, CASREPDate = today,
            CASDocument = Code(r.RepDocument), CASPersonnel = Code(r.RepPersonnel), CASAttention = Code(r.RepAttention),
            CASUnderlying = S(r.RepUnderlying) == "Y" ? "Y" : "N",
        });
    }

    /// <summary>cmdRepudiate_Click: save the repudiation and mark the case declined (letter built by the caller).</summary>
    public async Task RepudiateAsync(CaseAdjustmentRequest r, string userCode)
    {
        var cas = S(r.CaseNumber);
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            var state = await conn.ProcFirstOrDefaultAsync<State>("genisis.CaseAdj_State", new { cas }, tx)
                ?? throw new CaseRegistrationException("No record found!");
            var today = (await conn.ExecuteScalarAsync<DateTime>("SELECT GETDATE()", transaction: tx)).Date;
            await SaveRepudiationAsync(conn, tx, r, cas, First(r.Claimability, 2).ToUpperInvariant(), state.HasRep, userCode, today);
            await Exec(conn, tx, "genisis.CaseAdj_SetProgress", new { cas, isRejected = "1", isLGIssued = "1", progress = "CASE DECLINED" });
            await tx.CommitAsync();
            log.LogInformation("Case {CaseNumber} repudiated by {User}", cas, userCode);
        }
        catch (Exception ex) when (ex is not CaseRegistrationException)
        {
            await tx.RollbackAsync();
            log.LogError(ex, "Repudiation failed for {CaseNumber} by {User}; transaction rolled back", cas, userCode);
            throw;
        }
    }

    /// <summary>The tblRecord progress buttons: C. Ref AA / C. Ref RR / Clear Doc Record / Doc Disch.</summary>
    public static readonly Dictionary<string, (string? Rejected, string? LgIssued, string? Progress, string Message)> ProgressActions = new()
    {
        ["referral-accept"] = ("1", "1", "CASE APPROVED FOR REFERRAL DOCTOR", "Cross Referral Doctor has been accepted"),
        ["referral-reject"] = ("1", "1", "CASE DECLINED FOR REFERRAL DOCTOR", "Cross Referral Doctor has been rejected"),
        ["clear-doc"] = (null, "1", null, "Record Updated Successful..."),
        ["doc-discharge"] = ("0", "0", "PENDING DOCUMENT FROM HOSPITAL", "Record Updated Successful..."),
    };

    public async Task<string> SetProgressAsync(string cas, string action, string userCode)
    {
        if (!ProgressActions.TryGetValue(action, out var a)) throw new CaseRegistrationException($"Unknown action '{action}'.");
        await using var conn = db.MainDb();
        await conn.ExecuteAsync("genisis.CaseAdj_SetProgress", new { cas = cas.Trim(), isRejected = a.Rejected, isLGIssued = a.LgIssued, progress = a.Progress },
            commandType: CommandType.StoredProcedure);
        log.LogInformation("Case {CaseNumber} progress '{Action}' by {User}", cas, action, userCode);
        return a.Message;
    }

    public async Task<List<Dictionary<string, object?>>> LgListAsync(string cas)
    {
        await using var conn = db.MainDb();
        return ToDictionaries(await conn.ProcQueryAsync<dynamic>("genisis.CaseAdj_LGList", new { cas = cas.Trim() }));
    }

    /// <summary>cmdDocReqAdm_Click ("Delete" on the LG tab).</summary>
    public async Task DeleteLgAsync(string cas, int lgNo, string userCode)
    {
        await using var conn = db.MainDb();
        var deleted = await conn.ProcSingleAsync<int>("genisis.CaseAdj_DeleteLG", new { cas = cas.Trim(), lgNo });
        if (deleted == 0) throw new CaseRegistrationException("No record found!");
        log.LogInformation("GL {LgNo} of case {CaseNumber} deleted by {User}", lgNo, cas, userCode);
    }

    /// <summary>WSListing: the worksheet claim versions of the case (VIEW_WS_Listing on the worksheet database).</summary>
    public async Task<List<Dictionary<string, object?>>> WorksheetAsync(string cas)
    {
        await using var ws = db.WorkSheet();
        return ToDictionaries(await ws.QueryAsync(@"
            SELECT CASNumber, ClaimVersion, CLMTotalActual, CLMTotalApproved, CLMPayableByMedix2H, CLMPayableByMedix2M,
                BordxRefNo, BordxDate, CLMBordxAmt, CLMPayorAppStatus, CLMPayorAppDate, CLMPayorAppCode, CLMBordxRevInd,
                CRTCartonNo, DoSend, DebitCreditRefNo, DebitCreditDate, CLMRevBordxStatus
            FROM dbo.VIEW_WS_Listing WHERE CASNumber = @cas ORDER BY ClaimVersion", new { cas = cas.Trim() }));
    }

    /// <summary>SSTab1_Click (Case Mgmt Notes, empty page 1): the desktop's note template with the policy and
    /// claim history (MBMHistory / ListCases1).</summary>
    public async Task<CaseNotesTemplate> NotesTemplateAsync(CaseAdjustmentRequest r, string userCode)
    {
        await using var conn = db.MainDb();
        var cas = S(r.CaseNumber);
        object row = await conn.QueryFirstOrDefaultAsync("SearchCasAdjustment", new { CASNumber = cas },
            commandType: CommandType.StoredProcedure) ?? throw new CaseRegistrationException("No record found!");
        Dictionary<string, object?> c = ToDictionary(row);
        var member = await registration.LoadMemberAsync(S(c["MBMNumber"]));
        var m = member?.Member ?? new Dictionary<string, object?>();
        var ic = S(m.GetValueOrDefault("MBMIcBcPp"));
        var cover = S(c["MBMPatientCovID"]);
        var patientIc = cover == "00" ? ic : S(member?.Members.FirstOrDefault(x => x.CoverId == cover)?.Ic);
        var dob = (cover == "00" ? c["MBMDOB"] : c["MBMCDOB"]) as DateTime?;
        var eff = m.GetValueOrDefault("MBMPayorEffDate") as DateTime?;
        var age = dob is null || eff is null ? "" : (eff.Value.Year - dob.Value.Year).ToString();
        var patient = cover == "00" ? S(c["MBMName"]) : S(c["MBMCName"]);
        var doa = r.DateAdmitted?.ToString("dd/MM/yyyy") ?? "";
        var dod = r.DateDischarged?.ToString("dd/MM/yyyy") ?? "";
        var now = await conn.ExecuteScalarAsync<DateTime>("SELECT GETDATE()");

        var policies = (await conn.ProcQueryAsync<dynamic>("genisis.CaseAdj_PolicyHistory", new { ic })).ToList();
        var polHistory = new StringBuilder();
        foreach (var (p, i) in policies.Select((p, i) => (p, i)))
            polHistory.Append(i == 0 ? "\n                         Policy History" : "")
                .Append($"\n                         {S(p.MBMNumber)} - {S(p.MBMPolicyNo)} - {p.MBMPayorEffDate:dd/MM/yyyy} - {p.MBMPayorExpDate:dd/MM/yyyy}");

        var caseHistory = new StringBuilder();
        if (patientIc.Length > 5 && r.DateAdmitted is not null)
        {
            var count = 0;
            foreach (var proc in new[] { ("SearchCasHistorymgmt", patientIc), ("SearchCasHistorySuppmgmt", ic) })
            {
                var rows = await conn.QueryAsync(proc.Item1, new { ICNumber = proc.Item2, DOA = r.DateAdmitted.Value.Date },
                    commandType: CommandType.StoredProcedure);
                foreach (var h in rows.Select(ToDictionary))
                {
                    count++;
                    var name = S(h.GetValueOrDefault("MBMPatientCovID")) == "00" ? S(h.GetValueOrDefault("MBMName")) : S(h.GetValueOrDefault("MBMCName"));
                    var diag = string.Join(";", new[] { "DIAFinalDiag1", "DIAFinalDiag2", "DIAFinalDiag3" }
                        .Select(k => S(h.GetValueOrDefault(k))).TakeWhile(x => x.Length > 0));
                    if (count == 1) caseHistory.Append("\n                        CLAIM HISTORY");
                    caseHistory.Append($"\n                        [{count}]  {S(h.GetValueOrDefault("CASNumber"))}       {name}         ")
                        .Append($"{h.GetValueOrDefault("CASDateAdmitted"):dd/MM/yyyy} - {h.GetValueOrDefault("CASDischargeDate"):dd/MM/yyyy} ")
                        .Append($"\n                        {diag}                                    {h.GetValueOrDefault("CLMBordxAmt") ?? 0}     ");
                }
            }
        }

        static string Join(IEnumerable<string?> values) => string.Join(";", values.Select(S).TakeWhile(x => x.Length > 0));
        var prov = Join(r.PrelimDiagnoses.Select(d => d.Text));
        var finalDiag = Join(r.FinalDiagnoses.Select(d => d.Text));
        var exclusion = S(member?.PrincipalExclusion) is { Length: > 0 } x ? x : "NIL";
        var hospital = S(r.HospitalName).Length > 0 ? S(r.HospitalName) : S(c["HOSName"]);
        var pad = "                        ";
        var doctors = string.Concat(Enumerable.Range(0, 5).Select(i => $"\n{pad}DR {i + 1}) {S(i < r.AttendingDoctors.Count ? r.AttendingDoctors[i] : null)}"));
        var payMode = S(m.GetValueOrDefault("MBMInstallment"));
        var group = S(m.GetValueOrDefault("MBMGroupCompany"));
        var ud = r.UnderlyingDiseases.Select(d => S(d.Text)).Concat(["", "", ""]).ToList();

        string text;
        if (First(r.PayType, 1) == "R")
            text = $"{pad}Document Received On {r.FirstDocDate:dd/MM/yyyy}{new string(' ', 74)}{userCode}\n" +
                $"\n{pad}PT Name: {patient}     Age : {age}  \n{pad}DOA: {doa} \n{pad}HOSP: {hospital} " +
                $"\n{now:dd/MM/yyyy HH:mm:ss}   RCVD M/FORM @                      REGISTERED @       \n" +
                $"\n{pad}PYMT MODE:{payMode} NEXT DUE :{new string(' ', 88)}{userCode}\n{pad}RECOVERY: \n" +
                $"\n                         EXCLUSION : {exclusion}\n\n                          {polHistory}\n\n                          {caseHistory}\n" +
                $"\n{pad}GROUP CO: {group}\n\n{pad}PROV DIAG: {prov}\n{pad}SYMPTOM: \n{pad}DURATION: \n{pad}U/ILLNESS: \n\n{pad}ADM KIT:             TTA DRUGS:" +
                $"\n\n{pad}FINAL DIAG: {finalDiag}{doctors}\n\n{pad}PROCEDURE:\n{pad}                     MMA RATE         HOSP   " +
                $"\n{pad}S/FEE:    \n{pad}A/FEE:   \n{pad}DOA : {doa} DOD : {dod} ";
        else
            text = $"{pad}PT Name: {patient}    Age : {age}\n{pad}DOA: {doa} \n{pad}HOSP: {hospital} " +
                $"\n{now:dd/MM/yyyy HH:mm:ss}   RCVD M/FORM @                      REGISTERED @       \n" +
                $"\n{pad}PYMT MODE: {payMode} NEXT DUE :{new string(' ', 82)}{userCode} \n\n{pad}GROUP CO: {group} \n{pad}RECOVERY: \n" +
                $"\n                         EXCLUSION : {exclusion}\n                          {polHistory}\n\n                          {caseHistory}\n" +
                $"\n{pad}PROV DIAG: {prov} \n{pad}SYMPTOM: \n{pad}DURATION: \n{pad}U/ILLNESS 1: {ud[0]} \n{pad}U/ILLNESS 2: {ud[1]} \n{pad}U/ILLNESS 3: {ud[2]} \n" +
                $"\n{pad}CASE COVERED, LG FAXED @\n \n{pad}RCVD FINAL BILL RM @ " +
                $"\n\n{pad}TOTAL EXCESS : RM \n{pad}ADM KIT:             TTA DRUGS:\n{pad}EXCESS LETTER & FINAL LG FAXED @ \n" +
                $"\n{pad}FINAL DIAG: {finalDiag}{doctors}\n\n{pad}PROCEDURE: \n{pad}                     MMA RATE         HOSP   " +
                $"\n{pad}S/FEE:    \n{pad}A/FEE:   \n{pad}DOA : {doa} DOD : {dod}\n{pad}Annual Limit:   \n{pad}Balance Limit:   ";

        return text.Length > 3750 ? new(text[..3750], text[3750..]) : new(text, "");
    }
}
