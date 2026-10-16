-- Case > Adjustment (legacy frmCaseAdjustmentNew / frmGLMenu).
-- The case itself is read with the existing parameterized dbo.SearchCasAdjustment; these procedures replace
-- the raw @strAppend modes of dbo.Case_Adjustment the form used for everything else.

-- cmdPrev_Click / CmdNext_Click: the neighbouring case number on the maintenance cross reference.
CREATE OR ALTER PROCEDURE genisis.CaseAdj_Navigate
    @cas nvarchar(15), @next bit
AS
BEGIN
    SET NOCOUNT ON;
    IF @next = 1
        SELECT TOP (1) RTRIM(CASNumber) FROM genisis.Maint_CASCrossReference WHERE CASNumber > @cas ORDER BY CASNumber;
    ELSE
        SELECT TOP (1) RTRIM(CASNumber) FROM genisis.Maint_CASCrossReference WHERE CASNumber < @cas ORDER BY CASNumber DESC;
END
GO

-- showCAS side reads: ShowFirstDiag, ShowFinalDiag, ShowRepudiation (codes with their descriptions),
-- the pending reason (tblRecord.DocRemark), the plan's policy wording and whether the case has a GL.
CREATE OR ALTER PROCEDURE genisis.CaseAdj_Extras
    @cas nvarchar(15), @plan nvarchar(20), @pay nvarchar(10), @hlt nvarchar(10)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(ICDCode1) AS ICDCode1, RTRIM(ICDCode2) AS ICDCode2, RTRIM(ICDCode3) AS ICDCode3,
        DIAPrelimDiag1, DIAPrelimDiag2, DIAPrelimDiag3, DIADOS1, DIADOS2, DIADOS3
    FROM dbo.DIAFirst WHERE CASNumber = @cas;

    SELECT RTRIM(ICDCode1) AS ICDCode1, RTRIM(ICDCode2) AS ICDCode2, RTRIM(ICDCode3) AS ICDCode3,
        DIAFinalDiag1, DIAFinalDiag2, DIAFinalDiag3, DIADOS1, DIADOS2, DIADOS3
    FROM dbo.DIAFinal WHERE CASNumber = @cas;

    SELECT RTRIM(r.CASREPCode1) + ISNULL(N' - ' + RTRIM(c1.RepDescription), N'') AS Rep1,
        RTRIM(r.CASREPCode2) + ISNULL(N' - ' + RTRIM(c2.RepDescription), N'') AS Rep2,
        RTRIM(r.CASREPCode3) + ISNULL(N' - ' + RTRIM(c3.RepDescription), N'') AS Rep3,
        RTRIM(r.CASDocument) AS CASDocument, RTRIM(r.CASPersonnel) AS CASPersonnel,
        RTRIM(r.CASAttention) AS CASAttention, RTRIM(r.CASUnderlying) AS CASUnderlying
    FROM dbo.CASRepudiation r
    LEFT JOIN dbo.REPCode c1 ON c1.RepCode = NULLIF(RTRIM(r.CASREPCode1), N'')
    LEFT JOIN dbo.REPCode c2 ON c2.RepCode = NULLIF(RTRIM(r.CASREPCode2), N'')
    LEFT JOIN dbo.REPCode c3 ON c3.RepCode = NULLIF(RTRIM(r.CASREPCode3), N'')
    WHERE r.CASNumber = @cas;

    SELECT TOP (1) DocRemark FROM genisis.Maint_tblRecord WHERE CaseNo = @cas ORDER BY RecId DESC;

    SELECT TOP (1) RTRIM(PLNPolicyWording) FROM dbo.PLN
    WHERE PLNCode = @plan AND (@hlt = N'S' OR PAYCode = @pay);

    SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM genisis.Maint_CASLG WHERE CASENo = @cas) THEN 1 ELSE 0 END AS bit);
END
GO

-- cmdUpdate1_Click: which child rows already exist (insert vs update), the values the update reuses from CAS
-- and the balance-limit inputs for UpdateBalLmt. The CAS row is locked for the rest of the transaction.
CREATE OR ALTER PROCEDURE genisis.CaseAdj_State
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(c.MBMNumber) AS MBMNumber, RTRIM(c.MBMPatientCovID) AS MBMPatientCovID, RTRIM(c.CASStatus) AS CASStatus,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.CASRemarks WHERE CASNumber = @cas) THEN 1 ELSE 0 END AS bit) AS HasRemark,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.CASRemarksTwo WHERE CASNumber = @cas) THEN 1 ELSE 0 END AS bit) AS HasRemark2,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.CASRemarkThree WHERE CASNumber = @cas) THEN 1 ELSE 0 END AS bit) AS HasRemark3,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM genisis.Maint_CASCrossReference WHERE CASNumber = @cas) THEN 1 ELSE 0 END AS bit) AS HasXref,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.DIAFirst WHERE CASNumber = @cas) THEN 1 ELSE 0 END AS bit) AS HasDiaFirst,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.DIAFinal WHERE CASNumber = @cas) THEN 1 ELSE 0 END AS bit) AS HasDiaFinal,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.CAS_UD WHERE CASNumber = @cas) THEN 1 ELSE 0 END AS bit) AS HasUD,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.CASOthers WHERE CASNumber = @cas) THEN 1 ELSE 0 END AS bit) AS HasOthers,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.CASOthersTwo WHERE CASNumber = @cas) THEN 1 ELSE 0 END AS bit) AS HasOthers2,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.CASRepudiation WHERE CASNumber = @cas) THEN 1 ELSE 0 END AS bit) AS HasRep,
        ISNULL((SELECT TOP (1) CASRedAmt FROM dbo.CASOthers WHERE CASNumber = @cas), 0) AS PrevRedAmt,
        (SELECT TOP (1) MBMAvailableLimit FROM dbo.MBM WHERE MBMNumber = c.MBMNumber) AS AvailableLimit
    FROM dbo.CAS c WITH (UPDLOCK, ROWLOCK)
    WHERE c.CASNumber = @cas;
END
GO

-- The LG tab (LGListing over View_GL_Listing on the maintenance database).
CREATE OR ALTER PROCEDURE genisis.CaseAdj_LGList
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(CASENo) AS CaseNo, LGNo, LGDate, LGAmt, RTRIM(LGIssuedBy) AS LGIssuedBy, RTRIM(LGRemark) AS LGRemark,
        RTRIM(LGStatus) AS LGStatus, RTRIM(LGUserTime) AS LGUserTime, RTRIM(LGSystemTime) AS LGSystemTime,
        RTRIM(LGPurpose) AS LGPurpose, RTRIM(LGIndicator) AS LGIndicator
    FROM genisis.Maint_CASLG WHERE CASENo = @cas ORDER BY LGNo;
END
GO

-- cmdLG_Click / frmGLMenu inputs: the earlier GLs (purpose rules, same-purpose re-issue, open GL) and the
-- request date / time recorded on tblRecord.
CREATE OR ALTER PROCEDURE genisis.CaseAdj_GLContext
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT LGNo, RTRIM(LGPurpose) AS LGPurpose, RTRIM(LGIndicator) AS LGIndicator
    FROM genisis.Maint_CASLG WHERE CASENo = @cas ORDER BY LGNo;

    SELECT TOP (1) CaseDate, RTRIM(CaseTime) AS CaseTime FROM genisis.Maint_tblRecord WHERE CaseNo = @cas ORDER BY RecId DESC;
END
GO

-- The case progress flags the desktop sets on tblRecord (NULL leaves a flag unchanged).
CREATE OR ALTER PROCEDURE genisis.CaseAdj_SetProgress
    @cas nvarchar(15), @isRejected char(1) = NULL, @isLGIssued char(1) = NULL, @progress nvarchar(100) = NULL,
    @docRemark nvarchar(200) = NULL, @setDocRemark bit = 0
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.Maint_tblRecord
    SET IsRejected = ISNULL(@isRejected, IsRejected),
        IsLGissued = ISNULL(@isLGIssued, IsLGissued),
        CaseProgress = ISNULL(@progress, CaseProgress),
        DocRemark = CASE WHEN @setDocRemark = 1 THEN @docRemark ELSE DocRemark END
    WHERE CaseNo = @cas;
END
GO

-- cmdDocReqAdm_Click ("Delete" on the LG tab): remove one GL of the case.
CREATE OR ALTER PROCEDURE genisis.CaseAdj_DeleteLG
    @cas nvarchar(15), @lgNo int
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM genisis.Maint_CASLG WHERE CASENo = @cas AND LGNo = @lgNo;
    SELECT @@ROWCOUNT;
END
GO

-- cmdLG_Click: close the open GL when a GL for a different purpose is issued.
CREATE OR ALTER PROCEDURE genisis.CaseAdj_CloseLG
    @cas nvarchar(15), @lgNo int
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.Maint_CASLG SET LGIndicator = 'C' WHERE CASENo = @cas AND LGNo = @lgNo;
END
GO

-- GetLGCode: the next LG sequence of the case (LGSeq), locked until the caller's transaction ends.
CREATE OR ALTER PROCEDURE genisis.CaseAdj_NextLGSeq
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM genisis.Maint_LGSeq WITH (UPDLOCK, HOLDLOCK) WHERE CASNumber = @cas)
        UPDATE genisis.Maint_LGSeq SET LGSequence = LGSequence + 1 WHERE CASNumber = @cas;
    ELSE
        INSERT INTO genisis.Maint_LGSeq (CASNumber, LGSequence) VALUES (@cas, 1);
    SELECT CAST(LGSequence AS int) FROM genisis.Maint_LGSeq WHERE CASNumber = @cas;
END
GO

-- Case_Adjustment mode 19 + mode 3: the plan's LG settings and whether the member's plan is back-to-back.
CREATE OR ALTER PROCEDURE genisis.CaseAdj_GLPlan
    @mbm nvarchar(15), @plan nvarchar(20), @pay nvarchar(10)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(PLNPerVisit) AS PLNPerVisit, RTRIM(PLNRBType) AS PLNRBType, RTRIM(PLNLGType) AS PLNLGType,
        RTRIM(PLNLGPrivate) AS PLNLGPrivate, RTRIM(PLNLGGov) AS PLNLGGov
    FROM dbo.PLN WHERE PLNCode = @plan AND (@pay = N'ZN' OR PAYCode = @pay);

    SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.MBM m INNER JOIN dbo.PLN p ON p.PLNCode = m.PLNCode
        WHERE m.MBMNumber = @mbm AND p.PLNBTBG = 'Y') THEN 1 ELSE 0 END AS bit);
END
GO

-- frmCASList (Source = intCaseAdjustment): the case pick-list over View_Case_Search.
CREATE OR ALTER PROCEDURE genisis.CaseAdj_CaseSearch
    @by nvarchar(10), @q nvarchar(100), @plan nvarchar(20) = NULL, @max int = 200
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(CASNumber) AS CASNumber, RTRIM(MBMNumber) AS MBMNumber,
        RTRIM(MBMPatientCovID) AS MBMPatientCovID,
        CASE WHEN RTRIM(MBMPatientCovID) = '00' THEN RTRIM(MBMName) ELSE RTRIM(MBMCName) END AS PatientName,
        RTRIM(MBMPolicyNo) AS MBMPolicyNo, CASDateAdmitted, CASDischargeDate,
        RTRIM(CASStatus) AS CASStatus, RTRIM(CASHOSCode) AS CASHOSCode, CASReserveAmount,
        RTRIM(CASClaimability) AS CASClaimability
    FROM dbo.View_Case_Search
    WHERE MBMName IS NOT NULL AND (@plan IS NULL OR PLNCode = @plan) AND
        (@by = 'case' AND CASNumber LIKE @q + '%'
        OR @by = 'member' AND (MBMNumber LIKE @q + '%' OR MBMCNumber LIKE @q + '%')
        OR @by = 'policy' AND MBMPolicyNo = @q
        OR @by = 'name' AND MBMName LIKE '%' + @q + '%'
        OR @by = 'ic' AND MBMIcBcPp = @q
        OR @by = 'employee' AND MBMEmployeeNo LIKE @q + '%'
        OR @by = 'polsub' AND MBMPOLSubNo1 LIKE @q + '%')
    ORDER BY CASNumber DESC;
END
GO

-- Form_Load's cboPendingReason fill (SearchPendingReason on the maintenance database).
CREATE OR ALTER PROCEDURE genisis.CaseAdj_PendingReasons
AS
BEGIN
    SET NOCOUNT ON;
    EXEC genisis.Maint_SearchPendingReason;
END
GO

-- MBMHistory("Princ", True): the member's policies by IC for the case management note template.
CREATE OR ALTER PROCEDURE genisis.CaseAdj_PolicyHistory
    @ic nvarchar(30)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT RTRIM(MBMNumber) AS MBMNumber, RTRIM(MBMPolicyNo) AS MBMPolicyNo, MBMPayorEffDate, MBMPayorExpDate
    FROM genisis.Maint_MBMCrossReference
    WHERE MBMIcBcPp = @ic AND MBMStatus <> 'D'
    ORDER BY MBMPayorEffDate;
END
GO
