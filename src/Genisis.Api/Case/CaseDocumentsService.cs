using System.Data;
using Dapper;
using Genisis.Api.Data;
using Microsoft.Data.SqlClient;
using static Genisis.Api.Data.RowMapper;

namespace Genisis.Api.Case;

public record RepReasonDetail(string Code, string Reason, string? CategoryName, string? CategoryDescription);
public record CaseLetterResult(string Letter, string Title, Dictionary<string, string?> Fields, List<RepReasonDetail>? RepReasons = null);
public record CaseLgResult(string LgNumber, CaseLetterResult Letter, string BalanceLimit);
public record CaseLetterRequest(string Letter, string? CaseNumber, string? LgNumber, CaseRegistrationRequest Form);
public record CaseLgRequest(string? CaseNumber, CaseRegistrationRequest Form);

/// <summary>frmCaseAdmission's letter buttons (funcword/bookmark) and the LG flow (cmdLG_Click:
/// CheckLimit, GetLGCode, Case_AdjInsertCASLG, MobileNotification).</summary>
public class CaseDocumentsService(DbConnections db, CaseRegistrationService registration, ILogger<CaseDocumentsService> log)
{
    private static string S(object? value) => (value?.ToString() ?? "").Trim();
    private const string DateFmt = "dd/MM/yyyy";

    private record LgContext(
        string CASNumber, string MBMNumber, string? MBMPatientCovID, DateTime? CASDateAdmitted, DateTime? CASDischargeDate,
        string? CASTopupLmt, string? CASHOSCode, string? HOSName, string? CASClaimability,
        string? MBMPolicyNo, string? MBMName, string? MBMIcBcPp, string? INSCode, string? PAYCode, string? HLTCode,
        string? PLNCode, DateTime? MBMPayorEffDate, DateTime? MBMPayorExpDate, string? MBMTopupInd, string? MBMTopupNumber,
        DateTime? MBMPolEffDate, string? MBMMemberType, string? PatientName, string? PatientIC, string? MBMCPLNCode,
        string? MBMGroupCompany, string? AnnLmtIND, string? PLNLifeTimeStatus, string? PLNSOF, string? PLNBTBG,
        string? PLNPerVisit, string? PLNRBType, string? PLNLGType, string? CoPaymentInd, string? PLNMeal,
        string? PLNNursing, string? PLNTax, string? PLNMRI);

    private record AnnualLimit(decimal? AnnLimit, decimal? SuppLimit, decimal? LifeTimeLimit, string? SuppLimitStatus);
    private record PayorRow(string? PAYCompanyName, string? PAYAddress1, string? PAYAddress2, string? PAYAddress3,
        string? PAYMainContact, string? PAYAdmissionContact);
    private record HospitalRow(string? HOSContact, string? HOSFaxNo);
    private record PlanTerms(string? CoPaymentInd, string? PLNMeal, string? PLNNursing, string? PLNTax, string? PLNMRI);
    private record RepDetail(string? RepDescription, string? RepCatName, string? REPCatDescription);
    private record LgInfo(string? LGPurpose, string? PatientName, DateTime? LGDate, string? MBMPatientCovID);

    /// <summary>CheckLimit: how much of the member's annual/lifetime limit is left after worksheet claims.
    /// Ported branch-for-branch; the desktop's Topup flag is always false, so only those paths run.
    /// The adjustment screen's copy also skips the check (null) for plans without an annual limit and for
    /// principals on 'D' supplementary-limit plans, and reads the type C view for their dependants.</summary>
    private async Task<decimal?> BalanceLimitAsync(SqlConnection main, LgContext ctx, bool adjustment = false)
    {
        if (adjustment && S(ctx.AnnLmtIND) == "N") return null;
        var cov = S(ctx.MBMPatientCovID);
        var mbm = S(ctx.MBMNumber);
        var lifeTime = S(ctx.PLNLifeTimeStatus) == "Y";
        var topupNumber = S(ctx.MBMTopupNumber);

        // Top-up lookups only run when the case's member has a top-up number on the maintenance xref.
        var ltRenewal = false;
        var topMbm = mbm;
        // (Top-up xref + member reads run only when the case's member has a top-up number.)
        if (topupNumber.Length > 0)
        {
            using var grid = await main.ProcMultipleAsync("genisis.CaseReg_TopupContext", new { topup = topupNumber, cov });
            var xref = await grid.ReadFirstOrDefaultAsync();
            if (xref is not null)
            {
                var top = await grid.ReadFirstOrDefaultAsync();
                if (top is not null) topMbm = S(top.MBMNumber);
                ltRenewal = (bool)(await grid.ReadFirstOrDefaultAsync())!.LTGuaranteedRenewal;
            }
        }

        var limit = await main.ProcFirstOrDefaultAsync<dynamic>("genisis.CaseReg_LGAnnualLimit",
            new { plan = S(ctx.PLNCode), ins = S(ctx.INSCode), doa = ctx.CASDateAdmitted ?? DateTime.Today });
        var annLimit = N(limit?.AnnLimit);
        var suppLimit = N(limit?.SuppLimit);
        var lifeLimit = N(limit?.LifeTimeLimit);
        var suppStatus = S(limit?.SuppLimitStatus);
        if (adjustment && suppStatus == "D" && cov == "00") return null;

        decimal totalBordx = 0, totalLtBordx = 0;
        if (!lifeTime)
        {
            totalBordx = await LastBordxAsync(suppStatus switch
            {
                "D" when adjustment => cov == "00" ? "dbo.View_Claim_GetPrinLimit WHERE MBMNUmber = @mbm"
                                                   : "dbo.View_Claim_GetSuppLimit_TypeC WHERE MBMNUmber = @mbm",
                "B" => cov == "00" ? "dbo.View_Claim_GetPrinLimit WHERE MBMNUmber = @mbm"
                                   : "dbo.View_Claim_GetSuppLimit_TypeB WHERE MBMNUmber = @mbm",
                "C" => cov == "00" ? "dbo.View_Claim_GetPrinLimit WHERE MBMNUmber = @mbm"
                                   : "dbo.View_Claim_GetSuppLimit_TypeC WHERE MBMNUmber = @mbm AND MBMPatientCovID = @cov",
                _ => "dbo.View_Claim_GetLimit_TypeA WHERE MBMNUmber = @mbm",
            }, mbm, cov, topMbm);
        }
        else
        {
            totalLtBordx = await LastBordxAsync("dbo.View_Claim_GetLifetime_Limit WHERE MBMTopupNumber = @top AND MBMPatientCovID = @cov",
                mbm, cov, topMbm);
            totalBordx = await LastBordxAsync(cov == "00"
                    ? "dbo.View_Claim_GetPrinLimit WHERE MBMNUmber = @mbm"
                    : "dbo.View_Claim_GetSuppLimit_TypeC WHERE MBMNUmber = @mbm AND MBMPatientCovID = @cov",
                mbm, cov, topMbm);
        }

        // 'Q' members on supplementary plans B/C use SuppLimit; every other case uses AnnLimit.
        var cap = S(ctx.MBMMemberType) == "Q" && suppStatus is "B" or "C" && cov != "00" ? suppLimit : annLimit;
        var balance = !lifeTime ? cap - totalBordx
            : ltRenewal ? Math.Min(lifeLimit - totalLtBordx, cap - totalBordx)
            : cap - totalLtBordx;
        return balance;
    }

    /// <summary>The desktop loops the claim-balance view and keeps the last row.</summary>
    private async Task<decimal> LastBordxAsync(string where, string mbm, string cov, string top)
    {
        await using var ws = db.WorkSheet();
        var rows = await ws.QueryAsync<decimal?>($"SELECT CLMBordxAmt FROM {where}", new { mbm, cov, top });
        return rows.LastOrDefault() ?? 0;
    }

    private static decimal N(object? value) => decimal.TryParse(S(value), out var n) ? n : 0;

    /// <summary>cmdLG_Click: issue a guarantee letter — balance check, LG sequence, CASLG row, then the
    /// data for the LG / BTBG letter. The maintenance writes run in one transaction; the mobile
    /// notification rows are best-effort afterwards (the SOAP push to the legacy device service is not
    /// migrated).</summary>
    public async Task<CaseLgResult> IssueLGAsync(CaseLgRequest req, string userCode, string userName)
    {
        var r = req.Form;
        var cas = S(req.CaseNumber);
        if (cas.Length == 0) throw new CaseRegistrationException("Register the case before issuing an LG.");
        if (S(r.MbmNumber).Length == 0) throw new CaseRegistrationException("Please Enter Membership Information");
        if (S(r.CoverId).Length == 0) throw new CaseRegistrationException("Please Enter Patient Information");
        if (S(r.HospitalCode).Length == 0) throw new CaseRegistrationException("Please Enter Hospital Information");
        if (r.DateAdmitted is null) throw new CaseRegistrationException("Please Enter Admission Date");
        if (r.LgAmount is null or <= 0) throw new CaseRegistrationException("Please Enter Reserve Amount");
        if (S(r.LgTime).Length == 0) throw new CaseRegistrationException("Please Enter LG Time");

        await using var main = db.MainDb();
        var ctx = await main.ProcFirstOrDefaultAsync<LgContext>("genisis.CaseReg_LGContext", new { cas })
            ?? throw new CaseRegistrationException("No record found!");

        // CboClaimability_Change: the desktop never enables LG for repudiated claimability codes.
        if (S(ctx.CASClaimability) is { Length: >= 2 } claim && "RR,NR,PR,AR,BR,ER".Contains(claim[..2]))
            throw new CaseRegistrationException("Repudiated cases do not get a guarantee letter.");

        var balance = (await BalanceLimitAsync(main, ctx))!.Value;
        if (r.LgAmount.Value > balance) throw new CaseRegistrationException("Please Check Balance Limit");

        var letter = S(ctx.PLNLGType);
        if (letter != "C" && S(ctx.PLNBTBG) == "Y") letter = "B";
        if (letter.Length == 0) letter = "L";

        // GetLGCode + Case_AdjInsertCASLG on the maintenance database, in one transaction.
        int seq;
        await using (var maint = db.Maintenance())
        {
            await maint.OpenAsync();
            await using var tx = (SqlTransaction)await maint.BeginTransactionAsync();
            try
            {
                seq = await maint.ExecuteScalarAsync<int>(@"
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM dbo.LGSeq WITH (UPDLOCK, HOLDLOCK) WHERE CASNumber = @cas)
                        UPDATE dbo.LGSeq SET LGSequence = LGSequence + 1 WHERE CASNumber = @cas;
                    ELSE
                        INSERT INTO dbo.LGSeq (CASNumber, LGSequence) VALUES (@cas, 1);
                    SELECT LGSequence FROM dbo.LGSeq WHERE CASNumber = @cas;", new { cas }, tx);
                await maint.ExecuteAsync("dbo.Case_AdjInsertCASLG", new
                {
                    CaseNo = cas, LGNo = seq, LGDate = DateTime.Today, LGAmt = r.LgAmount, LGIssuedBy = userCode,
                    LGRemark = S(r.LgRemark), LGStatus = letter, PerVisit = S(ctx.PLNPerVisit),
                    PLNRBType = S(ctx.PLNRBType), LGUserTime = S(r.LgTime), LGSystemTime = DateTime.Now.ToString("HH:mm"),
                }, tx, commandType: CommandType.StoredProcedure);
                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                log.LogError(ex, "LG issue failed for case {CaseNumber} by {User}; transaction rolled back", cas, userCode);
                throw;
            }
        }

        var lgNumber = $"{(letter == "B" ? "B" : "L")}{cas}-{seq}";
        log.LogInformation("LG {LgNumber} issued for case {CaseNumber} by {User}", lgNumber, cas, userCode);
        await NotifyMemberAsync(cas, S(r.MbmNumber), lgNumber, userCode);

        var fields = await LetterFieldsAsync(main, ctx, null, r, userCode, userName);
        ApplyLgFields(fields, letter, ctx, lgNumber, S(r.LgTime), S(r.LgRemark));
        fields["lgAmount"] = r.LgAmount?.ToString("#,##0.00");
        return new(lgNumber, new(letter, LgTitle(letter, ctx.PLNPerVisit), fields, null), balance.ToString("#,##0.00"));
    }

    private record GLRow(int LGNo, string? LGPurpose, string? LGIndicator);
    private record GLPlan(string? PLNPerVisit, string? PLNRBType, string? PLNLGType, string? PLNLGPrivate, string? PLNLGGov);

    /// <summary>frmGLMenu inputs: the request date / time already recorded for the case and the last GL purpose.</summary>
    public async Task<(DateTime? RequestDate, string? RequestTime, string? LastPurpose)> GLDefaultsAsync(string cas)
    {
        await using var main = db.MainDb();
        using var grid = await main.ProcMultipleAsync("genisis.CaseAdj_GLContext", new { cas = cas.Trim() });
        var gls = (await grid.ReadAsync<GLRow>()).ToList();
        var rec = await grid.ReadFirstOrDefaultAsync<(DateTime? CaseDate, string? CaseTime)>();
        string? time = S(rec.CaseTime) is { Length: >= 5 } t ? t[..5] : null;
        return (rec.CaseDate, time, gls.LastOrDefault()?.LGPurpose?.Trim());
    }

    /// <summary>The adjustment cmdLG_Click's CheckLimit: is the GL amount within the balance limit?</summary>
    public async Task<CaseGLCheck> CheckGLAsync(string cas, decimal? amount)
    {
        await using var main = db.MainDb();
        var ctx = await main.ProcFirstOrDefaultAsync<LgContext>("genisis.CaseReg_LGContext", new { cas = cas.Trim() })
            ?? throw new CaseRegistrationException("No record found!");
        var balance = await BalanceLimitAsync(main, ctx, adjustment: true);
        return new(balance is null || amount is null || amount.Value <= balance.Value, balance?.ToString("#,##0.00"));
    }

    /// <summary>The adjustment cmdLG_Click after frmGLMenu: re-issue the GL of the same purpose or take a new LG
    /// sequence, close the open GL of another purpose, write CASLG with the request details and mark the case
    /// "GL ISSUED" — all in one transaction — then notify the member and build the letter.</summary>
    public async Task<CaseGLResult> IssueGLAsync(string cas, CaseGLRequest req, string userCode, string userName)
    {
        cas = cas.Trim();
        var r = req.Form;
        var (_, _, lastPurpose) = await GLDefaultsAsync(cas);
        if (CaseGLRules.Validate(req, lastPurpose) is { } error) throw new CaseRegistrationException(error);
        if (S(r.MbmNumber).Length == 0) throw new CaseRegistrationException("Please Enter Membership Information");
        if (S(r.CoverId).Length == 0) throw new CaseRegistrationException("Please Enter Patient Information");
        if (S(r.HospitalCode).Length == 0) throw new CaseRegistrationException("Please Enter Hospital Information");
        if (r.DateAdmitted is null) throw new CaseRegistrationException("Please Enter Admission Date");

        await using var main = db.MainDb();
        await main.OpenAsync();
        var ctx = await main.ProcFirstOrDefaultAsync<LgContext>("genisis.CaseReg_LGContext", new { cas })
            ?? throw new CaseRegistrationException("No record found!");
        var purpose = req.Purpose!.Trim();
        string letter, lgNumber;

        await using (var tx = (SqlTransaction)await main.BeginTransactionAsync())
        {
            try
            {
                using var grid = await main.ProcMultipleAsync("genisis.CaseAdj_GLContext", new { cas }, tx);
                var gls = (await grid.ReadAsync<GLRow>()).ToList();
                await grid.ReadAsync();
                var same = gls.LastOrDefault(g => S(g.LGPurpose) == purpose);
                var open = gls.LastOrDefault(g => S(g.LGIndicator) is "O" or "");
                if (purpose != "Follow-up" && open is not null && S(open.LGPurpose) != purpose)
                    await main.ExecuteAsync("genisis.CaseAdj_CloseLG", new { cas, lgNo = open.LGNo }, tx, commandType: CommandType.StoredProcedure);

                var seq = same?.LGNo ?? await main.ProcSingleAsync<int>("genisis.CaseAdj_NextLGSeq", new { cas }, tx);
                using var planGrid = await main.ProcMultipleAsync("genisis.CaseAdj_GLPlan",
                    new { mbm = S(r.MbmNumber), plan = S(ctx.PLNCode), pay = S(ctx.PAYCode) }, tx);
                var plan = await planGrid.ReadFirstOrDefaultAsync<GLPlan>();
                var btbg = await planGrid.ReadSingleAsync<bool>();
                letter = S(plan?.PLNLGType) is { Length: > 0 } t ? t : "L";
                if ((S(plan?.PLNLGPrivate) != "1" || S(plan?.PLNLGGov) != "1") && btbg) letter = "B";
                lgNumber = $"{(letter == "B" ? "B" : "L")}{cas}-{seq}";

                await main.ExecuteAsync(same is null ? "genisis.Maint_Case_AdjInsertCASLG_2011" : "genisis.Maint_Case_AdjUpdateCASLG", new
                {
                    CaseNo = cas, LGNo = seq, LGDate = DateTime.Today, LGAmt = req.LgAmount ?? 0, LGIssuedBy = userCode,
                    LGRemark = S(req.Remarks), LGStatus = letter, PerVisit = S(plan?.PLNPerVisit) == "Y" ? "Y" : "",
                    PLNRBType = S(plan?.PLNRBType), LGUserTime = S(req.GLTime), LGSystemTime = DateTime.Now.ToString("HH:mm"),
                    LGRequestDate = req.RequestDate?.Date, LGRequestTime = S(req.RequestTime), LGRequestMode = S(req.RequestMode),
                    LGRequestBy = S(req.RequestBy), LGFaxDate = req.FaxDate?.Date, LGFaxTime = S(req.FaxTime),
                    LGDelayReason = S(req.DelayReason), LGPurpose = purpose,
                }, tx, commandType: CommandType.StoredProcedure);
                await main.ExecuteAsync("genisis.CaseAdj_SetProgress", new { cas, isLGIssued = "1", progress = "GL ISSUED" },
                    tx, commandType: CommandType.StoredProcedure);
                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                log.LogError(ex, "GL issue failed for case {CaseNumber} by {User}; transaction rolled back", cas, userCode);
                throw;
            }
        }

        log.LogInformation("GL {LgNumber} ({Purpose}) issued for case {CaseNumber} by {User}", lgNumber, purpose, cas, userCode);
        await NotifyMemberAsync(cas, S(r.MbmNumber), lgNumber, userCode);
        var fields = await LetterFieldsAsync(main, ctx, null, r, userCode, userName);
        ApplyLgFields(fields, letter, ctx, lgNumber, S(req.GLTime), S(req.Remarks));
        fields["lgAmount"] = req.LgAmount?.ToString("#,##0.00");
        fields["purpose"] = purpose;
        return new(lgNumber, new(letter, $"{LgTitle(letter, ctx.PLNPerVisit)} — {purpose}", fields));
    }

    /// <summary>MobileNotification: one MBMAndroidNotification row for the member's first registered
    /// device (the desktop also fired a SOAP push per device; that service is external and retired).</summary>
    private async Task NotifyMemberAsync(string cas, string mbm, string lgNumber, string userCode)
    {
        try
        {
            await using var maint = db.Maintenance();
            var info = await maint.ProcFirstOrDefaultAsync<LgInfo>("dbo.Sp_AndroidNotification", new { LGNo = lgNumber });
            if (info is null) return;

            await using var main = db.MainDb();
            var devices = (await main.QueryAsync<string>(@"
                SELECT a.MemberID FROM dbo.MBMLoginDetails l
                INNER JOIN dbo.MBMAndroidID a ON a.MemberID = l.MBMUserName
                WHERE l.MBMNumber = @mbm AND a.Status = 1", new { mbm })).ToList();
            var memberId = devices.FirstOrDefault();
            if (memberId is null) return;

            await main.ExecuteAsync(@"
                INSERT INTO dbo.MBMAndroidNotification
                    (UserName, MBMNumber, MBMCoverId, Notifications, NotificationDt, LGPurpose, LGNumber, NotificationSeen)
                VALUES (@memberId, @mbm, @cover, @msg, @dt, @purpose, @lgNumber, 0)",
                new
                {
                    memberId, mbm, cover = S(info.MBMPatientCovID),
                    msg = $"Notification Alert From MediExpress!{S(info.LGPurpose)} GL Issued on {info.LGDate:dd/MM.yyyy}",
                    dt = info.LGDate, purpose = S(info.LGPurpose), lgNumber,
                });
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Mobile notification for LG {LgNumber} (case {CaseNumber}) could not be recorded by {User}",
                lgNumber, cas, userCode);
        }
    }

    /// <summary>cmdDocReqAdm/Dis, cmdTHDrugs, cmdPrior and the post-registration letters: assemble every
    /// field the corresponding Word bookmark set used.</summary>
    public async Task<CaseLetterResult> LetterAsync(CaseLetterRequest req, string userCode, string userName)
    {
        var r = req.Form;
        var type = req.Letter.Trim().ToUpperInvariant();
        if (S(r.MbmNumber).Length == 0) throw new CaseRegistrationException("Please Enter Membership Information");

        var member = await registration.LoadMemberAsync(S(r.MbmNumber))
            ?? throw new CaseRegistrationException("No record found!");
        var m = member.Member;
        var expiry = m["MBMPayorExpDate"] as DateTime?;
        var today = member.Today.Date;

        if (type is "AUTO" or "")
        {
            // Post-save rule (NewCaseAddmission): "P" when the expiry is within 45 days, "E" once it has
            // passed. cmdPrior_Click used the narrower current-month check; the 45-day rule covers it.
            if (expiry is null) throw new CaseRegistrationException("Membership expiry date is not available.");
            if (expiry.Value.Date < today) type = "E";
            else if ((expiry.Value.Date - today).TotalDays <= 45) type = "P";
            else throw new CaseRegistrationException("Notification is only available within 45 days of the membership expiry.");
        }

        var needsAdmissionFields = type is "A" or "D" or "T";
        if (needsAdmissionFields)
        {
            if (S(r.CoverId).Length == 0) throw new CaseRegistrationException("Please Enter Patient Information");
            if (S(r.HospitalCode).Length == 0) throw new CaseRegistrationException("Please Enter Hospital Information");
            if (r.DateAdmitted is null) throw new CaseRegistrationException("Please Enter Admission Date");
        }

        await using var conn = db.MainDb();
        var fields = await LetterFieldsAsync(conn, null, member, r, userCode, userName);
        fields["caseNo"] = S(req.CaseNumber);

        switch (type)
        {
            case "A": case "D": break;
            case "T": break;
            // Adjustment screen: GP/QA investigation request, reimbursement document request, case management note.
            case "I": case "S": break;
            case "M":
                fields["note"] = string.Join("\n", r.Remarks.Select(x => x ?? "").Where(x => x.Trim().Length > 0));
                break;
            case "P": case "E":
                fields["month"] = expiry?.ToString("MMMM yyyy");
                break;
            case "R":
                var details = await RepudiationFieldsAsync(conn, fields, r);
                return new(type, LetterTitle(type), fields, details);
            default:
                throw new CaseRegistrationException($"Unknown letter type '{req.Letter}'.");
        }
        return new(type, LetterTitle(type), fields);
    }

    /// <summary>The fields every letter shares (patient / member / hospital / payor / plan terms),
    /// from either the LG context row or the loaded member view.</summary>
    private async Task<Dictionary<string, string?>> LetterFieldsAsync(SqlConnection conn, LgContext? ctx,
        CaseMemberView? member, CaseRegistrationRequest r, string userCode, string userName)
    {
        var mbm = S(r.MbmNumber);
        var cover = S(r.CoverId);
        var memberRow = member?.Member;
        string? Get(params string[] keys) =>
            keys.Select(k => ctx is not null ? TryGet(ctx, k) : memberRow?.TryGetValue(k, out var v) == true ? S(v) : null)
                .FirstOrDefault(v => !string.IsNullOrEmpty(v));

        var pay = Get("PAYCode") ?? "";
        var hlt = Get("HLTCode") ?? "";
        var plan = Get("PLNCode") ?? "";
        var cty = memberRow?.TryGetValue("CTYCode", out var c) == true ? S(c) : null;
        var sta = memberRow?.TryGetValue("STACode", out var s) == true ? S(s) : null;
        var hospital = S(r.HospitalName).Length > 0 ? S(r.HospitalName) : S(ctx?.HOSName);

        using var grid = await conn.ProcMultipleAsync("genisis.CaseReg_LetterContext",
            new { pay, hosName = hospital, plan, hlt, cty, sta });
        var payor = await grid.ReadFirstOrDefaultAsync<PayorRow>();
        var hos = await grid.ReadFirstOrDefaultAsync<HospitalRow>();
        var terms = await grid.ReadFirstOrDefaultAsync<PlanTerms>();
        var city = S(await grid.ReadFirstOrDefaultAsync<string?>());
        var state = S(await grid.ReadFirstOrDefaultAsync<string?>());

        var memberName = memberRow?.TryGetValue("MBMName", out var mn) == true ? S(mn) : null;
        var memberIc = memberRow?.TryGetValue("MBMIcBcPp", out var ic) == true ? S(ic) : null;
        var memberExpiry = memberRow?.TryGetValue("MBMPayorExpDate", out var ex) == true ? ex as DateTime? : null;
        var patient = member is null ? S(ctx?.PatientName)
            : member.Patients.FirstOrDefault(p => p.CoverId == cover)?.Name ?? memberName;

        var fields = new Dictionary<string, string?>
        {
            ["systemDate"] = DateTime.Today.ToString(DateFmt),
            ["staffName"] = userName, ["staffCode"] = userCode,
            ["patient"] = patient, ["mbmNo"] = mbm,
            ["patientIC"] = cover == "00" || string.IsNullOrEmpty(ctx?.PatientIC) ? (ctx?.PatientIC ?? memberIc) : S(ctx.PatientIC),
            ["hospital"] = hospital, ["hosContact"] = S(hos?.HOSContact), ["hosFax"] = S(hos?.HOSFaxNo),
            ["policyNo"] = Get("MBMPolicyNo"),
            ["expiryDate"] = (ctx?.MBMPayorExpDate ?? memberExpiry)?.ToString(DateFmt),
            ["doa"] = (r.DateAdmitted ?? ctx?.CASDateAdmitted)?.ToString(DateFmt),
            ["dod"] = (r.DateDischarged ?? ctx?.CASDischargeDate)?.ToString(DateFmt),
            ["payor"] = S(payor?.PAYCompanyName), ["payorAdd1"] = payor?.PAYAddress1, ["payorAdd2"] = payor?.PAYAddress2,
            ["payorAdd3"] = payor?.PAYAddress3, ["payorContact"] = S(payor?.PAYAdmissionContact),
            ["principal"] = Get("MBMName"),
            ["add1"] = memberRow?.TryGetValue("MBMAddress1", out var a1) == true ? S(a1) : null,
            ["add2"] = (memberRow?.TryGetValue("MBMAddress2", out var a2) == true ? S(a2) : "")
                    + (memberRow?.TryGetValue("MBMAddress3", out var a3) == true ? S(a3) : ""),
            ["postCode"] = (memberRow?.TryGetValue("MBMPostCode", out var pc) == true ? S(pc) : "") + city,
            ["state"] = state, ["city"] = city,
            ["diag"] = S(r.Reason),
            ["including"] = Including(terms), ["terms"] = Terms(plan, terms),
            ["coPay"] = terms?.CoPaymentInd, ["mri"] = terms?.PLNMRI,
        };
        return fields;
    }

    private void ApplyLgFields(Dictionary<string, string?> f, string letter, LgContext ctx, string lgNumber,
        string lgTime, string lgRemark)
    {
        f["caseNo"] = ctx.CASNumber;
        f["lgNo"] = lgNumber;
        f["rb"] = S(ctx.PLNRBType);
        f["remark"] = lgRemark;
        f["grpCompany"] = letter == "C" && !string.IsNullOrEmpty(ctx.MBMGroupCompany) ? " - " + ctx.MBMGroupCompany.Trim() : "";
        f["attention"] = f["hosContact"];
        f["fax"] = f["hosFax"];
        f["perVisit"] = ctx.PLNPerVisit;
        f["lgTime"] = lgTime;
    }

    /// <summary>The repudiation letter's REP selections plus per-code reason/category text.</summary>
    private async Task<List<RepReasonDetail>> RepudiationFieldsAsync(SqlConnection conn, Dictionary<string, string?> f, CaseRegistrationRequest r)
    {
        static string? Desc(string? sel) => sel?.Contains('-') == true ? sel[(sel.IndexOf('-') + 1)..].Trim() : sel;
        f["repDocument"] = Desc(r.RepDocument);
        f["repPersonnel"] = Desc(r.RepPersonnel);
        f["repAttention"] = Desc(r.RepAttention);
        var reasons = string.Join(", ", r.FinalDiagnoses.Select(d => S(d.Text)).Where(t => t.Length > 0));
        if (reasons.Length > 0) f["reasons"] = reasons;
        var ud = string.Join(", ", r.UnderlyingDiseases.Select(d => S(d.Text)).Where(t => t.Length > 0));
        if (ud.Length > 0) f["underlying"] = $" With underlying illness as {ud}.";

        var details = new List<RepReasonDetail>();
        foreach (var code in r.RepudiationCodes.Select(c => S(c).Split('-')[0].Trim()).Where(c => c.Length > 0))
        {
            var d = await conn.ProcFirstOrDefaultAsync<RepDetail>("genisis.CaseReg_RepLetterDetail", new { code });
            if (d is not null)
                details.Add(new(code, S(d.RepDescription), d.RepCatName?.Trim(), d.REPCatDescription?.Trim()));
        }
        // LDDesc: the payor paragraph shown when the case already has a guarantee letter.
        await using var maint = db.Maintenance();
        var hasLg = await maint.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.CASLG WHERE CASENo = @cas",
            new { cas = f["caseNo"] }) > 0;
        if (hasLg)
            f["ldDesc"] = "MediExpress has facilitated your admission and settled your hospitalization charges amounting to " +
                "RM2,500.00. We enclose a copy of the bill for your records. We would be pleased if you could let us have " +
                "your payment to MediExpress(M) Sdn. Bhd. within 14 days herewith.";
        return details;
    }

    private static string Including(PlanTerms? t)
    {
        var including = "";
        if (t?.PLNMeal == "Y") including += "Meal";
        if (t?.PLNNursing == "Y") including += " ,Nursing Charges";
        if (t?.PLNTax == "Y") including += " ,Tax";
        return including;
    }

    /// <summary>bookmark()'s terms paragraphs: plan '4*' and the C letter have fixed texts, otherwise the
    /// paragraphs depend on the plan's co-pay and MRI flags.</summary>
    private static string Terms(string plan, PlanTerms? t)
    {
        const string open = "2)    Medical Information relating to the patient and treatment must be truthfully disclosed by the attending \n" +
            "       physician.\n3)    All Doctors and Anaesthetists charges shall NOT exceed the fees as per the MMA schedule of Fees.\n" +
            "4)    The following items are not covered: -\n           (a) Items of personal nature such as telephone calls, newspaper, laundry, television etc.\n" +
            "           (b) Non-Medical items.\n           (c) Medical Report Charges.\n";
        const string copay = "5)    Co-Payment - In the event the Room and Board limit is exceeded, the Insurance will pay only 80% of \n" +
            "       other eligible costs.";
        const string mri = "Insurance will pay only 80% of MRI, CT Scan and Heart Scan charges.";
        var exclusions = "           (d) Registration Fees, Admission Fees, Billing Fees and Admission Kit.\n" +
            "           (e) Appliances, Prosthesis - Crutches, Lenses, Pacemaker, Braces, Corset, Arm sling and Cervical\n" +
            "                Collar.\n           (f) Lodger\n           (g) Supplies & Services not related to illness injury.\n";

        if (plan.StartsWith('4'))
            return open + "           (d) Registration Fees, Admission Fees, Billing Fees and Admission Kit.\n" +
                "           (e) Appliances, Prosthesis - Crutches, Lenses, Pacemaker, Braces, Corset \n" +
                "           (f) Lodger\n           (g) Supplies & Services not related to illness injury.\n" + copay;
        var coPay = t?.CoPaymentInd == "Y";
        var mriFlag = t?.PLNMRI == "Y";
        return (coPay, mriFlag) switch
        {
            (true, true) => open + exclusions + copay + "\n6)    " + mri,
            (false, true) => open + exclusions + "5)    " + mri,
            (true, false) => open + exclusions + copay,
            _ => open + exclusions[..^1],
        };
    }

    private static string LetterTitle(string type) => type switch
    {
        "A" => "Documents Required on Admission",
        "D" => "Documents Required on Discharge",
        "T" => "Take Home Drugs",
        "P" => "Notification — Membership Expiring",
        "E" => "Notification — Membership Expired",
        "R" => "Repudiation Letter",
        "I" => "Investigation Request (GP / QA)",
        "S" => "Reimbursement Documents Request",
        "M" => "Case Management Note",
        _ => "Letter",
    };

    private static string LgTitle(string letter, string? perVisit) =>
        (letter == "B" ? "Back To Back Guarantee" : "Letter of Guarantee") + (perVisit == "Y" ? " (per visit)" : "");

    private static string? TryGet(LgContext ctx, string key) => key switch
    {
        "PAYCode" => ctx.PAYCode, "HLTCode" => ctx.HLTCode, "PLNCode" => ctx.PLNCode,
        "MBMPolicyNo" => ctx.MBMPolicyNo, "MBMName" => ctx.MBMName, "MBMIcBcPp" => ctx.MBMIcBcPp,
        "PatientIC" => ctx.PatientIC,
        _ => null,
    };
}
