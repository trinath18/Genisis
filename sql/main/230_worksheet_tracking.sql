-- Worksheet > Worksheet Tracking (legacy FrmWorksheetTracking). The desktop built a WHERE clause as text (strAppend) and
-- sent it to dbo.WorksheetTracking in HISWorkSheet, which ran it as dynamic SQL; here every criterion is a typed parameter.
-- Reads the worksheet tracking view through genisis.WS_WKSTracking (HISWorkSheet.dbo.VIEW_WKSTrackingNew).

-- SearchRecord + WorksheetTrackingListing(2). @scan = 1: one case number; 0: a case number range. Every "from"/"to" pair is
-- inclusive and ignored when NULL; an "empty" flag keeps only rows where the column is NULL or blank (a date column is
-- blank at 1900-01-01, as the desktop's  = ''  test saw it). The desktop's Ins Type filter read controls that do not exist
-- on the form, so it never filtered; here @insFrom / @insTo really filter.
CREATE OR ALTER PROCEDURE genisis.WorksheetTracking_Search
    @scan bit, @wksFrom nvarchar(30) = NULL, @wksTo nvarchar(30) = NULL, @versionFrom int = NULL, @versionTo int = NULL,
    @payor nvarchar(4) = NULL, @icFrom nvarchar(60) = NULL, @icTo nvarchar(60) = NULL, @icEmpty bit = 0,
    @groupCompany nvarchar(300) = NULL, @claimability char(1) = NULL, @pending char(1) = NULL, @pendingEmpty bit = 0,
    @planFrom nvarchar(8) = NULL, @planTo nvarchar(8) = NULL,
    @approvedFrom money = NULL, @approvedTo money = NULL, @payMFrom money = NULL, @payMTo money = NULL,
    @payHFrom money = NULL, @payHTo money = NULL,
    @policyFrom nvarchar(50) = NULL, @policyTo nvarchar(50) = NULL, @memberFrom nvarchar(30) = NULL, @memberTo nvarchar(30) = NULL,
    @insFrom nvarchar(4) = NULL, @insTo nvarchar(4) = NULL,
    @bordxFrom nvarchar(40) = NULL, @bordxTo nvarchar(40) = NULL, @bordxEmpty bit = 0,
    @dpoFrom datetime = NULL, @dpoTo datetime = NULL, @dpoEmpty bit = 0,
    @doaFrom datetime = NULL, @doaTo datetime = NULL, @doaEmpty bit = 0,
    @dodFrom datetime = NULL, @dodTo datetime = NULL, @dodEmpty bit = 0,
    @firstFrom nvarchar(20) = NULL, @firstTo nvarchar(20) = NULL, @firstEmpty bit = 0,
    @finalFrom nvarchar(20) = NULL, @finalTo nvarchar(20) = NULL, @finalEmpty bit = 0,
    @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(PAYCode) AS Payor, RTRIM(CASNumber) AS CaseNo, ClaimVersion, RTRIM(ClaimStatus) AS WksStatus,
        RTRIM(CLMPending) AS Pending, CLMTotalApproved AS ApprovedAmt, CLMPayableByMedix2H AS PayableToHospital,
        CLMPayableByMedix2M AS PayableToMember, CaseRegDate AS Dor, CASDateAdmitted AS Doa, CASDischargeDate AS Dod,
        ClaimCloseDate AS DClose, CASTokenDate AS Dpo, CLMTokenDate AS DtaC, RTRIM(PatientName) AS Patient, RTRIM(HOSName) AS Hospital,
        NoofDay AS DaysOpen, RTRIM(BordxRefNo) AS BordxNo, BordxClaimAmt AS BordxAmt, BordxDate, RTRIM(DebitCreditRefNo) AS DebitCreditRefNo,
        RTRIM(FirstDiag) AS FirstDiag, RTRIM(FinalDiag) AS FinalDiag, RTRIM(CASAdmissionReason) AS AdmissionReason,
        CAST(ISNULL(ClaimBordxStatus, 0) AS bit) AS Bordxed
    FROM genisis.WS_WKSTracking
    WHERE (@payor IS NULL OR PAYCode = @payor)
      AND (@scan = 0 OR @wksFrom IS NULL OR CASNumber = @wksFrom)
      AND (@scan = 1 OR @wksFrom IS NULL OR CASNumber >= @wksFrom)
      AND (@scan = 1 OR @wksTo IS NULL OR CASNumber <= @wksTo)
      AND (@versionFrom IS NULL OR ClaimVersion >= @versionFrom) AND (@versionTo IS NULL OR ClaimVersion <= @versionTo)
      AND (@icFrom IS NULL OR ICno >= @icFrom) AND (@icTo IS NULL OR ICno <= @icTo)
      AND (@icEmpty = 0 OR ICno IS NULL)
      AND (@groupCompany IS NULL OR MBMGroupCompany = @groupCompany)
      AND (@claimability IS NULL OR CASClaimability = @claimability)
      AND (@pending IS NULL OR CLMPending = @pending)
      AND (@pendingEmpty = 0 OR CLMPending IS NULL OR CLMPending = '')
      AND (@planFrom IS NULL OR PLNCode >= @planFrom) AND (@planTo IS NULL OR PLNCode <= @planTo)
      AND (@approvedFrom IS NULL OR CLMTotalApproved >= @approvedFrom) AND (@approvedTo IS NULL OR CLMTotalApproved <= @approvedTo)
      AND (@payMFrom IS NULL OR CLMPayableByMedix2M >= @payMFrom) AND (@payMTo IS NULL OR CLMPayableByMedix2M <= @payMTo)
      AND (@payHFrom IS NULL OR CLMPayableByMedix2H >= @payHFrom) AND (@payHTo IS NULL OR CLMPayableByMedix2H <= @payHTo)
      AND (@policyFrom IS NULL OR MBMPolicyNo >= @policyFrom) AND (@policyTo IS NULL OR MBMPolicyNo <= @policyTo)
      AND (@memberFrom IS NULL OR MBMNumber >= @memberFrom) AND (@memberTo IS NULL OR MBMNumber <= @memberTo)
      AND (@insFrom IS NULL OR INSCode >= @insFrom) AND (@insTo IS NULL OR INSCode <= @insTo)
      AND (@bordxFrom IS NULL OR BordxRefNo >= @bordxFrom) AND (@bordxTo IS NULL OR BordxRefNo <= @bordxTo)
      AND (@bordxEmpty = 0 OR BordxRefNo IS NULL OR BordxRefNo = '')
      AND (@dpoFrom IS NULL OR CASTokenDate >= @dpoFrom) AND (@dpoTo IS NULL OR CASTokenDate <= @dpoTo)
      AND (@doaFrom IS NULL OR CASDateAdmitted >= @doaFrom) AND (@doaTo IS NULL OR CASDateAdmitted <= @doaTo)
      AND (@dodFrom IS NULL OR CASDischargeDate >= @dodFrom) AND (@dodTo IS NULL OR CASDischargeDate <= @dodTo)
      AND (@dpoEmpty = 0 OR CASTokenDate IS NULL OR CASTokenDate = '19000101')
      AND (@doaEmpty = 0 OR CASDateAdmitted IS NULL OR CASDateAdmitted = '19000101')
      AND (@dodEmpty = 0 OR CASDischargeDate IS NULL OR CASDischargeDate = '19000101')
      AND (@firstFrom IS NULL OR FirstDiag >= @firstFrom) AND (@firstTo IS NULL OR FirstDiag <= @firstTo)
      AND (@firstEmpty = 0 OR FirstDiag IS NULL OR FirstDiag = '')
      AND (@finalFrom IS NULL OR FinalDiag >= @finalFrom) AND (@finalTo IS NULL OR FinalDiag <= @finalTo)
      AND (@finalEmpty = 0 OR FinalDiag IS NULL OR FinalDiag = '')
    ORDER BY CASNumber, ClaimVersion;
END
GO

-- ChkGrpCom_Click: the group companies that have worksheets for a payor (the desktop listed one entry per worksheet).
CREATE OR ALTER PROCEDURE genisis.WorksheetTracking_GroupCompanies
    @payor nvarchar(4)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT RTRIM(MBMGroupCompany) AS GroupCompany
    FROM genisis.WS_WKSTracking
    WHERE PAYCode = @payor AND MBMGroupCompany IS NOT NULL AND RTRIM(MBMGroupCompany) <> N''
    ORDER BY RTRIM(MBMGroupCompany);
END
GO

-- UpdateCLM: sets the pending status of one worksheet and clears the pending flag of its case, in the four places the
-- desktop wrote. The desktop only stopped a bordxed worksheet from being ticked; here the procedure refuses it too.
-- Result: 0 done, 1 worksheet not found, 2 refused (already bordxed).
CREATE OR ALTER PROCEDURE genisis.WorksheetTracking_Update
    @cas nvarchar(30), @version int, @date datetime, @status char(1), @userCode nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @bordxed bit;
    SELECT @bordxed = ClaimBordxStatus FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @version;
    IF @@ROWCOUNT = 0
    BEGIN
        SELECT 1 AS Result;
        RETURN;
    END
    IF ISNULL(@bordxed, 0) = 1
    BEGIN
        SELECT 2 AS Result;
        RETURN;
    END

    UPDATE genisis.WS_Claim SET CLMPendingDate = @date, CLMPending = @status, CLMPendingUser = @userCode
    WHERE CASNumber = @cas AND ClaimVersion = @version;
    UPDATE genisis.WS_ClaimInvoice SET CLMInvPending = @status
    WHERE CASNumber = @cas AND INVWSversion = CAST(@version AS nvarchar(4));
    UPDATE dbo.CAS SET CASPending = 'N' WHERE CASNumber = @cas;
    UPDATE genisis.Maint_CASCrossReference SET CASPending = 'N' WHERE CASNumber = @cas;
    SELECT 0 AS Result;
END
GO

-- ComboListing (SearchINS): the insured types of the Ins Type drop-downs.
CREATE OR ALTER PROCEDURE genisis.Worksheet_InsuredTypes
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(INSCode) AS Code, RTRIM(INSDescription) AS Name FROM dbo.INS ORDER BY INSCode;
END
GO
