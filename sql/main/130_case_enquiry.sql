-- Case > Enquiry and Case > Tracking (read-only).
-- Replaces the legacy Case_Inquiry / CaseTracking dispatchers, which appended a raw @strAppend
-- string into dynamic SQL; these procs take typed parameters instead.
-- SearchCas, SearchCasAdjustment, SearchCasHistory, SearchHOS and SearchPayor are existing
-- procedures and are called by the API as they are.
-- dbo.CaseEnquiryNotes lives on the worksheet database (VIEW_WS_Listing) and is called on the
-- WorkSheet connection as it is.

-- @by: number | member | patient | ic | policy. @prefix is an escaped LIKE prefix ending in %,
-- @contains is an escaped LIKE fragment wrapped in % by the caller.
CREATE OR ALTER PROCEDURE genisis.CaseEnquiry_Search
    @max int, @by nvarchar(4000), @q nvarchar(4000), @prefix nvarchar(4000), @contains nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) c.CASNumber, c.MBMNumber, c.MBMPatientCovID,
           COALESCE(cp.MBMCName, x.MBMName) AS PatientName,
           c.CASStatus, c.CASClaimability, c.CASDateAdmitted, c.CASDischargeDate,
           c.CaseRegDate, c.CaseOpenBy, c.CASClaimAdmin, c.CASUserToken, c.CASTokenBatch,
           c.CASStayType, c.CASPAYType, c.ClaimNatureCode, c.CASDocType,
           h.HOSCode, h.HOSName,
           x.MBMPolicyNo, x.MBMIcBcPp, x.MBMName, x.PAYCode,
           f.DIAPrelimDiag1 AS FirstDiag, d.DIAFinalDiag1 AS FinalDiag
    FROM dbo.CAS c WITH (NOLOCK)
    LEFT JOIN genisis.Maint_MBMCrossReference x ON c.MBMNumber = x.MBMNumber
    LEFT JOIN dbo.MBMCoveredPersons cp WITH (NOLOCK)
        ON c.MBMNumber = cp.MBMCNumber AND c.MBMPatientCovID = cp.MBMCCoverID
    LEFT JOIN dbo.HOS h WITH (NOLOCK) ON c.CASHOSCode = h.HOSCode
    LEFT JOIN dbo.DIAFirst f WITH (NOLOCK) ON c.CASNumber = f.CASNumber
    LEFT JOIN dbo.DIAFinal d WITH (NOLOCK) ON c.CASNumber = d.CASNumber
    WHERE (@by = N'number' AND c.CASNumber LIKE @prefix)
       OR (@by = N'member' AND c.MBMNumber LIKE @prefix)
       OR (@by = N'patient' AND (cp.MBMCName LIKE @contains OR x.MBMName LIKE @contains))
       OR (@by = N'ic' AND (x.MBMIcBcPp = @q OR REPLACE(x.MBMIcBcPp, '-', '') = REPLACE(@q, '-', '')))
       OR (@by = N'policy' AND x.MBMPolicyNo LIKE @prefix)
    ORDER BY c.CaseRegDate DESC, c.CASNumber
    OPTION (RECOMPILE);
END
GO

-- Diagnosis / repudiation block of frmCaseInquiry (legacy Case_Inquiry @strAppendCase 2, 5 and 6).
-- Result sets: first diagnosis, final diagnosis, repudiation codes with descriptions.
CREATE OR ALTER PROCEDURE genisis.CaseEnquiry_Diagnoses
    @casNumber nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ICDCode1, ICDCode2, ICDCode3, DIAPrelimDiag1, DIAPrelimDiag2, DIAPrelimDiag3,
           DIADOS1, DIADOS2, DIADOS3, DIALastAdjustmentUser, DIALastAdjustmentDate
    FROM dbo.DIAFirst WITH (NOLOCK) WHERE CASNumber = @casNumber;
    SELECT ICDCode1, ICDCode2, ICDCode3, DIAFinalDiag1, DIAFinalDiag2, DIAFinalDiag3,
           DIADOS1, DIADOS2, DIADOS3, DIALastAdjustmentUser, DIALastAdjustmentDate
    FROM dbo.DIAFinal WITH (NOLOCK) WHERE CASNumber = @casNumber;
    SELECT r.CASREPCode1, r.CASREPCode2, r.CASREPCode3,
           r.CASREPReasons1, r.CASREPReasons2, r.CASREPReasons3,
           r.CASREPIssuedBy, r.CASREPDate, r.CASPersonnel, r.CASAttention,
           r.CASUnderlying, r.CASUnderlyingRemark, r.CASLetter, r.Remarks,
           r1.RepDescription AS RepDesc1, r2.RepDescription AS RepDesc2, r3.RepDescription AS RepDesc3
    FROM dbo.CASRepudiation r WITH (NOLOCK)
    LEFT JOIN dbo.RepCode r1 ON r1.RepCode = r.CASREPCode1
    LEFT JOIN dbo.RepCode r2 ON r2.RepCode = r.CASREPCode2
    LEFT JOIN dbo.RepCode r3 ON r3.RepCode = r.CASREPCode3
    WHERE r.CASNumber = @casNumber;
END
GO

-- Lookup lists for the enquiry / tracking filter dropdowns.
-- Result sets: hospitals, payors, repudiation codes, claim nature codes, health types, insured types.
CREATE OR ALTER PROCEDURE genisis.Case_Lookups
AS
BEGIN
    SET NOCOUNT ON;
    SELECT HOSCode, HOSName FROM dbo.HOS ORDER BY HOSName;
    SELECT PAYCode, PAYCompanyName FROM dbo.PAY ORDER BY PAYCompanyName;
    SELECT RepCode, RepDescription FROM dbo.RepCode ORDER BY RepCode;
    SELECT ClaimNatureCode, ClaimNatureName FROM dbo.ClaimNature ORDER BY ClaimNatureCode;
    SELECT HLTCode, HLTDescription FROM dbo.HLT ORDER BY HLTCode;
    SELECT INSCode, INSDescription FROM dbo.INS ORDER BY INSCode;
END
GO

-- FrmCaseTracking main list (legacy CaseTracking @strAppendCase = 1).
-- Same column set and GROUP BY over VIEW_CAS_TRACKING2; every filter is optional.
-- @status: 'O', 'D', or 'OC' for open-or-closed. @claimability: 'A' approved set, 'R' repudiated set,
-- or an exact 2-letter code. @pending: 'Y' or 'N' (N also matches blank/NULL). @memberType:
-- 'principal' or 'supplementary'. LIKE filters take already-escaped text; the proc wraps it in %.
CREATE OR ALTER PROCEDURE genisis.CaseTracking_List
    @max int,
    @payor nvarchar(4000) = NULL,
    @caseFrom nvarchar(4000) = NULL, @caseTo nvarchar(4000) = NULL,
    @mbmFrom nvarchar(4000) = NULL, @mbmTo nvarchar(4000) = NULL,
    @regFrom datetime = NULL, @regTo datetime = NULL,
    @batch nvarchar(4000) = NULL,
    @patient nvarchar(4000) = NULL,
    @grp nvarchar(4000) = NULL,
    @status nvarchar(4000) = NULL,
    @claimability nvarchar(4000) = NULL,
    @pending nvarchar(4000) = NULL,
    @regBy nvarchar(4000) = NULL,
    @payType nvarchar(4000) = NULL,
    @userToken nvarchar(4000) = NULL,
    @sex nvarchar(4000) = NULL,
    @memberType nvarchar(4000) = NULL,
    @hltCode nvarchar(4000) = NULL,
    @insCode nvarchar(4000) = NULL,
    @renewFlag nvarchar(4000) = NULL,
    @takeover nvarchar(4000) = NULL,
    @agentCode nvarchar(4000) = NULL,
    @policyNo nvarchar(4000) = NULL,
    @doctor nvarchar(4000) = NULL,
    @repFrom nvarchar(4000) = NULL, @repTo nvarchar(4000) = NULL,
    @claimNatureFrom nvarchar(4000) = NULL, @claimNatureTo nvarchar(4000) = NULL,
    @hosName nvarchar(4000) = NULL,
    @stayType nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) CASNumber, MBMPolicyNo, MBMNumber, PatientName, CaseOpenBy,
           CASDateAdmitted, CASStayType, CASPAYType, CASClaimAdmin,
           CASDischargeDate, CaseRegDate, CASStatus, HOSName,
           FirstDiag, FinalDiag, CASUserToken, CASTokenDate, CASQuery,
           CASClaimability, CASAdmissionReason, ActualAmt, CASDRD, CLMBTBGAmt,
           NoofDay, DurationStay, ApprovedAmt, BordxRefNo, CLMBordxDate,
           ClaimOpenDate, ClaimOpenBy, CLMTotalActualGST, PatientIC
    FROM dbo.VIEW_CAS_TRACKING2
    WHERE (@payor IS NULL OR PAYCode = @payor)
      AND (@caseFrom IS NULL OR CASNumber >= @caseFrom)
      AND (@caseTo IS NULL OR CASNumber <= @caseTo)
      AND (@mbmFrom IS NULL OR MBMNumber >= @mbmFrom)
      AND (@mbmTo IS NULL OR MBMNumber <= @mbmTo)
      AND (@regFrom IS NULL OR CaseRegDate >= @regFrom)
      AND (@regTo IS NULL OR CaseRegDate < DATEADD(day, 1, @regTo))
      AND (@batch IS NULL OR CASTokenBatch = @batch)
      AND (@patient IS NULL OR PatientName LIKE '%' + @patient + '%')
      AND (@grp IS NULL OR MBMGroupCompany = @grp)
      AND (@status IS NULL OR (@status = N'OC' AND CASStatus IN (N'O', N'C')) OR CASStatus = @status)
      AND (@claimability IS NULL
           OR (@claimability = N'A' AND CASClaimability IN (N'AA', N'SA', N'NA', N'PA', N'RA'))
           OR (@claimability = N'R' AND CASClaimability IN (N'RR', N'NR', N'PR', N'AR', N'BR'))
           OR (@claimability NOT IN (N'A', N'R') AND CASClaimability = @claimability))
      AND (@pending IS NULL
           OR (@pending = N'N' AND (CASPending = N'N' OR CASPending = '' OR CASPending IS NULL))
           OR (@pending = N'Y' AND CASPending = N'Y'))
      AND (@regBy IS NULL OR CaseOpenBy = @regBy)
      AND (@payType IS NULL OR CASPAYType = @payType)
      AND (@userToken IS NULL OR CASUserToken = @userToken)
      AND (@sex IS NULL OR Sex = @sex)
      AND (@memberType IS NULL
           OR (@memberType = N'principal' AND MBMPatientCovID = '00')
           OR (@memberType = N'supplementary' AND MBMPatientCovID <> '00'))
      AND (@hltCode IS NULL OR HLTCode = @hltCode)
      AND (@insCode IS NULL OR INSCode = @insCode)
      AND (@renewFlag IS NULL
           OR (@renewFlag = N'N' AND (MBMRenewFlag IS NULL OR MBMRenewFlag = '' OR MBMRenewFlag = 'N'))
           OR (@renewFlag <> N'N' AND MBMRenewFlag = @renewFlag))
      AND (@takeover IS NULL OR MBMTakeover = @takeover)
      AND (@agentCode IS NULL OR MBMAgentCode LIKE '%' + @agentCode + '%')
      AND (@policyNo IS NULL OR MBMPolicyNo LIKE '%' + @policyNo + '%')
      AND (@doctor IS NULL OR CASAttendDoctor1 LIKE '%' + @doctor + '%')
      AND (@repFrom IS NULL OR CASREPCode1 >= @repFrom)
      AND (@repTo IS NULL OR CASREPCode1 <= @repTo)
      AND (@claimNatureFrom IS NULL OR ClaimNatureCode >= @claimNatureFrom)
      AND (@claimNatureTo IS NULL OR ClaimNatureCode <= @claimNatureTo)
      AND (@hosName IS NULL OR HOSName = @hosName)
      AND (@stayType IS NULL OR CASStayType = @stayType)
    GROUP BY CASNumber, MBMPolicyNo, MBMNumber, PatientName, CaseOpenBy,
             CASDateAdmitted, CASStayType, CASPAYType, CASClaimAdmin,
             CASDischargeDate, CaseRegDate, CASStatus, HOSName,
             FirstDiag, FinalDiag, CASUserToken, CASTokenDate, CASQuery,
             CASClaimability, CASAdmissionReason, ActualAmt, CASDRD, CLMBTBGAmt,
             NoofDay, DurationStay, ApprovedAmt, BordxRefNo, CLMBordxDate,
             ClaimOpenDate, ClaimOpenBy, CLMTotalActualGST, PatientIC
    ORDER BY MAX(CASNumber), CASNumber
    OPTION (RECOMPILE);
END
GO

-- FrmCaseTracking totals (legacy CaseTracking summary modes).
-- Same filter set over VIEW_CAS_TRACKINGSUM, plus @claimPending: 'P' pending, 'C' closed, 'B' both-side.
CREATE OR ALTER PROCEDURE genisis.CaseTracking_Summary
    @payor nvarchar(4000) = NULL,
    @caseFrom nvarchar(4000) = NULL, @caseTo nvarchar(4000) = NULL,
    @mbmFrom nvarchar(4000) = NULL, @mbmTo nvarchar(4000) = NULL,
    @regFrom datetime = NULL, @regTo datetime = NULL,
    @batch nvarchar(4000) = NULL,
    @patient nvarchar(4000) = NULL,
    @grp nvarchar(4000) = NULL,
    @status nvarchar(4000) = NULL,
    @claimability nvarchar(4000) = NULL,
    @pending nvarchar(4000) = NULL,
    @claimPending nvarchar(4000) = NULL,
    @regBy nvarchar(4000) = NULL,
    @payType nvarchar(4000) = NULL,
    @userToken nvarchar(4000) = NULL,
    @sex nvarchar(4000) = NULL,
    @memberType nvarchar(4000) = NULL,
    @hltCode nvarchar(4000) = NULL,
    @insCode nvarchar(4000) = NULL,
    @renewFlag nvarchar(4000) = NULL,
    @takeover nvarchar(4000) = NULL,
    @agentCode nvarchar(4000) = NULL,
    @policyNo nvarchar(4000) = NULL,
    @doctor nvarchar(4000) = NULL,
    @repFrom nvarchar(4000) = NULL, @repTo nvarchar(4000) = NULL,
    @claimNatureFrom nvarchar(4000) = NULL, @claimNatureTo nvarchar(4000) = NULL,
    @hosName nvarchar(4000) = NULL,
    @stayType nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(DISTINCT CASNumber) AS CaseCount,
           SUM(CLMTotalActual) AS ActualAmt,
           SUM(CLMTotalApproved) AS ApprovedAmt
    FROM dbo.VIEW_CAS_TRACKINGSUM
    WHERE (@payor IS NULL OR PAYCode = @payor)
      AND (@caseFrom IS NULL OR CASNumber >= @caseFrom)
      AND (@caseTo IS NULL OR CASNumber <= @caseTo)
      AND (@mbmFrom IS NULL OR MBMNumber >= @mbmFrom)
      AND (@mbmTo IS NULL OR MBMNumber <= @mbmTo)
      AND (@regFrom IS NULL OR CaseRegDate >= @regFrom)
      AND (@regTo IS NULL OR CaseRegDate < DATEADD(day, 1, @regTo))
      AND (@patient IS NULL OR PatientName LIKE '%' + @patient + '%')
      AND (@grp IS NULL OR MBMGroupCompany = @grp)
      AND (@status IS NULL OR (@status = N'OC' AND CASStatus IN (N'O', N'C')) OR CASStatus = @status)
      AND (@claimability IS NULL
           OR (@claimability = N'A' AND CASClaimability IN (N'AA', N'SA', N'NA', N'PA', N'RA'))
           OR (@claimability = N'R' AND CASClaimability IN (N'RR', N'NR', N'PR', N'AR', N'BR'))
           OR (@claimability NOT IN (N'A', N'R') AND CASClaimability = @claimability))
      AND (@claimPending IS NULL OR CLMPending = @claimPending)
      AND (@regBy IS NULL OR CaseOpenBy = @regBy)
      AND (@payType IS NULL OR CASPAYType = @payType)
      AND (@sex IS NULL OR Sex = @sex)
      AND (@memberType IS NULL
           OR (@memberType = N'principal' AND MBMPatientCovID = '00')
           OR (@memberType = N'supplementary' AND MBMPatientCovID <> '00'))
      AND (@hltCode IS NULL OR HLTCode = @hltCode)
      AND (@insCode IS NULL OR INSCode = @insCode)
      AND (@renewFlag IS NULL
           OR (@renewFlag = N'N' AND (MBMRenewFlag IS NULL OR MBMRenewFlag = '' OR MBMRenewFlag = 'N'))
           OR (@renewFlag <> N'N' AND MBMRenewFlag = @renewFlag))
      AND (@takeover IS NULL OR MBMTakeover = @takeover)
      AND (@agentCode IS NULL OR MBMAgentCode LIKE '%' + @agentCode + '%')
      AND (@policyNo IS NULL OR MBMPolicyNo LIKE '%' + @policyNo + '%')
      AND (@repFrom IS NULL OR CASREPCode1 >= @repFrom)
      AND (@repTo IS NULL OR CASREPCode1 <= @repTo)
      AND (@claimNatureFrom IS NULL OR ClaimNatureCode >= @claimNatureFrom)
      AND (@claimNatureTo IS NULL OR ClaimNatureCode <= @claimNatureTo)
      AND (@hosName IS NULL OR HOSName = @hosName)
      AND (@stayType IS NULL OR CASStayType = @stayType)
    OPTION (RECOMPILE);
END
GO
