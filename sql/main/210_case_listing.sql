-- Case > Inpatient Case Listing (legacy mnuAppCase -> FrmWksBordx, "Select Worksheets for Bordx"): search the worksheets
-- that are waiting for a bordx (HISWorkSheet views, through the genisis.WS_* synonyms) and generate the bordx.
-- Replaces the raw SQL text FrmWksBordx built for WorksheetTrackingListing / WorksheetTrackingListing2 / UpdateRecord /
-- SaveCLMBordx and the Medix.bas GenerateCLMBordxNo sequence read-then-update (which could hand out a number twice).

-- SearchRecord + WorksheetTrackingListing(2). @scan = 1: one worksheet (case number, optionally a claim version);
-- 0: a case number range. @ib = 1 reads the IB payor view. Dates are inclusive; @doaEmpty / @dodEmpty keep only
-- worksheets with no admission / discharge date; @mhBordx = 1 keeps CLMMHBordx = 'Y' (Bordx Status "Yes").
CREATE OR ALTER PROCEDURE genisis.CaseListing_Search
    @scan bit, @wksFrom nvarchar(30) = NULL, @wksTo nvarchar(30) = NULL, @version int = NULL, @ib bit = 0,
    @doaFrom datetime = NULL, @doaTo datetime = NULL, @doaEmpty bit = 0,
    @dodFrom datetime = NULL, @dodTo datetime = NULL, @dodEmpty bit = 0, @mhBordx bit = 0, @max int = 5000
AS
BEGIN
    SET NOCOUNT ON;
    IF @ib = 1
        SELECT TOP (@max) RTRIM(CASNumber) AS CaseNo, ClaimVersion, RTRIM(PAYCode) AS Payor, CLMTotalApproved AS AppAmt,
            CASDateAdmitted AS Doa, CASDischargeDate AS Dod, RTRIM(INVPayTo) AS CashType, RTRIM(MBMPatientCovID) AS CoverId,
            RTRIM(MBMCName) AS CoveredName, RTRIM(HOSName) AS Hospital, CLMPayableByMedix2H AS HosAmt, RTRIM(MBMName) AS MemberName,
            RTRIM(MBMGroupCompany) AS GroupCompany, RTRIM(PLNCode) AS PlanCode, RTRIM(ICDCode1) AS IcdCode, RTRIM(MBMBTBG) AS Btbg,
            RTRIM(INSCode) AS InsuredType, RTRIM(CASClaimability) AS Claimability, RTRIM(HLTCode) AS HltType
        FROM genisis.WS_PendingBordxIB
        WHERE ClaimStatus <> N'R'
          AND (@scan = 0 OR @wksFrom IS NULL OR CASNumber = @wksFrom)
          AND (@scan = 0 OR @version IS NULL OR ClaimVersion = @version)
          AND (@scan = 1 OR @wksFrom IS NULL OR CASNumber >= @wksFrom)
          AND (@scan = 1 OR @wksTo IS NULL OR CASNumber <= @wksTo)
          AND (@doaFrom IS NULL OR CASDateAdmitted >= @doaFrom) AND (@doaTo IS NULL OR CASDateAdmitted <= @doaTo)
          AND (@dodFrom IS NULL OR CASDischargeDate >= @dodFrom) AND (@dodTo IS NULL OR CASDischargeDate <= @dodTo)
          AND (@doaEmpty = 0 OR CASDateAdmitted IS NULL) AND (@dodEmpty = 0 OR CASDischargeDate IS NULL)
          AND (@mhBordx = 0 OR CLMMHBordx = 'Y')
        ORDER BY CASNumber, ClaimVersion;
    ELSE
        SELECT TOP (@max) RTRIM(CASNumber) AS CaseNo, ClaimVersion, RTRIM(PAYCode) AS Payor, CLMTotalApproved AS AppAmt,
            CASDateAdmitted AS Doa, CASDischargeDate AS Dod, RTRIM(INVPayTo) AS CashType, RTRIM(MBMPatientCovID) AS CoverId,
            RTRIM(MBMCName) AS CoveredName, RTRIM(HOSName) AS Hospital, CLMPayableByMedix2H AS HosAmt, RTRIM(MBMName) AS MemberName,
            RTRIM(MBMGroupCompany) AS GroupCompany, RTRIM(PLNCode) AS PlanCode, RTRIM(ICDCode1) AS IcdCode, RTRIM(MBMBTBG) AS Btbg,
            RTRIM(INSCode) AS InsuredType, RTRIM(CASClaimability) AS Claimability, RTRIM(HLTCode) AS HltType
        FROM genisis.WS_PendingBordx
        WHERE ClaimStatus <> N'R'
          AND (@scan = 0 OR @wksFrom IS NULL OR CASNumber = @wksFrom)
          AND (@scan = 0 OR @version IS NULL OR ClaimVersion = @version)
          AND (@scan = 1 OR @wksFrom IS NULL OR CASNumber >= @wksFrom)
          AND (@scan = 1 OR @wksTo IS NULL OR CASNumber <= @wksTo)
          AND (@doaFrom IS NULL OR CASDateAdmitted >= @doaFrom) AND (@doaTo IS NULL OR CASDateAdmitted <= @doaTo)
          AND (@dodFrom IS NULL OR CASDischargeDate >= @dodFrom) AND (@dodTo IS NULL OR CASDischargeDate <= @dodTo)
          AND (@doaEmpty = 0 OR CASDateAdmitted IS NULL) AND (@dodEmpty = 0 OR CASDischargeDate IS NULL)
          AND (@mhBordx = 0 OR CLMMHBordx = 'Y')
        ORDER BY CASNumber, ClaimVersion;
END
GO

-- One pending worksheet as the bordx needs it: the values the selection rules check, who is paid (INVPayTo, BTBG) and the
-- three amounts a bordx can sum (CmdBordx_Click picks one by BTBG / INVPayTo). No row when it is not (or no longer) pending.
CREATE OR ALTER PROCEDURE genisis.CaseListing_Item
    @cas nvarchar(30), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) p.CaseNo, p.ClaimVersion, p.Payor, p.Dod, p.Hospital, p.GroupCompany, p.InsuredType, p.Claimability, p.HltType,
        p.CashType, p.Btbg, c.CLMPayableByMedix2H AS Payable2H, c.CLMPayableByMedix2M AS Payable2M,
        c.CLMTotalApproved AS TotalApproved, ISNULL(c.CLMCaseMgmtFees, 0) AS CaseMgmtFees
    FROM (
        SELECT RTRIM(CASNumber) AS CaseNo, ClaimVersion, RTRIM(PAYCode) AS Payor, CASDischargeDate AS Dod, RTRIM(HOSName) AS Hospital,
            RTRIM(MBMGroupCompany) AS GroupCompany, RTRIM(INSCode) AS InsuredType, RTRIM(CASClaimability) AS Claimability,
            RTRIM(HLTCode) AS HltType, RTRIM(INVPayTo) AS CashType, RTRIM(MBMBTBG) AS Btbg
        FROM genisis.WS_PendingBordx WHERE CASNumber = @cas AND ClaimVersion = @version AND ClaimStatus <> N'R'
        UNION ALL
        SELECT RTRIM(CASNumber), ClaimVersion, RTRIM(PAYCode), CASDischargeDate, RTRIM(HOSName), RTRIM(MBMGroupCompany),
            RTRIM(INSCode), RTRIM(CASClaimability), RTRIM(HLTCode), RTRIM(INVPayTo), RTRIM(MBMBTBG)
        FROM genisis.WS_PendingBordxIB WHERE CASNumber = @cas AND ClaimVersion = @version AND ClaimStatus <> N'R'
    ) p
    INNER JOIN genisis.WS_Claim c ON c.CASNumber = p.CaseNo AND c.ClaimVersion = p.ClaimVersion;
END
GO

-- GenerateCLMBordxNo: <payor health type><payor code><7-digit sequence per payor code>. The number is taken atomically.
CREATE OR ALTER PROCEDURE genisis.CaseListing_NextBordxNo
    @pay nvarchar(8)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @hlt nvarchar(4) = (SELECT RTRIM(HLTCode) FROM dbo.PAY WHERE PAYCode = @pay);
    DECLARE @seq nvarchar(20);
    UPDATE genisis.WS_BordxSEQ WITH (UPDLOCK, ROWLOCK)
    SET @seq = CLMBordxNo = RIGHT(N'0000000' + CAST(CAST(CLMBordxNo AS int) + 1 AS nvarchar(10)), 7)
    WHERE PAYCode = @pay;
    IF @@ROWCOUNT = 0
    BEGIN
        SET @seq = N'0000001';
        INSERT genisis.WS_BordxSEQ (PAYCode, CLMBordxNo, HLTCode) VALUES (@pay, @seq, @hlt);
    END
    SELECT ISNULL(@hlt, N'') + @pay + @seq AS BordxNo;
END
GO

-- UpdateRecord: mark one worksheet as bordxed (claim, invoice, case pending flags) and append the "BORDX DONE" line to the
-- case remarks. The remark goes into the first remarks page that has room, following the desktop's page order
-- (CASRemarks.CASORemarks, CASORemarksTwo, CASRemarksTwo.CASORemarks, CASORemarksTwo, CASRemarkThree.CASRemarks, CASRemarksFive);
-- the lengths decide the page exactly as UpdateRecord did.
CREATE OR ALTER PROCEDURE genisis.CaseListing_Apply
    @cas nvarchar(30), @version int, @bordxNo nvarchar(40), @userCode nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today datetime = CAST(CAST(GETDATE() AS date) AS datetime);

    UPDATE genisis.WS_Claim SET BordxRefNo = @bordxNo, CLMBordxDate = @today, ClaimBordxStatus = 1, CLMPending = 'N'
    WHERE CASNumber = @cas AND ClaimVersion = @version;
    UPDATE genisis.WS_ClaimInvoice SET INVBordxRefNo = @bordxNo, INVBordxed = 1
    WHERE CASNumber = @cas AND INVWSversion = CAST(@version AS nvarchar(4));
    UPDATE dbo.CAS SET CASPending = 'N' WHERE CASNumber = @cas;
    UPDATE genisis.Maint_CASCrossReference SET CASPending = 'N' WHERE CASNumber = @cas;

    DECLARE @add nvarchar(max) = N'~' + NCHAR(10) + CONVERT(nvarchar(10), GETDATE(), 103) + N'         BORDX DONE BY ' + @userCode
        + N' -  ' + @bordxNo + REPLICATE(N' ', 44) + @userCode + N' ';

    DECLARE @l1 int = 0, @l2 int = 0, @l3 int = 0, @l4 int = 0, @l5 int = 0;
    SELECT @l1 = LEN(ISNULL(CASORemarks, N'') + N'x') - 1, @l2 = LEN(ISNULL(CASORemarksTwo, N'') + N'x') - 1 FROM dbo.CASRemarks WHERE CASNumber = @cas;
    SELECT @l3 = LEN(ISNULL(CASORemarks, N'') + N'x') - 1, @l4 = LEN(ISNULL(CASORemarksTwo, N'') + N'x') - 1 FROM dbo.CASRemarksTwo WHERE CASNumber = @cas;
    SELECT @l5 = LEN(ISNULL(CASRemarks, N'') + N'x') - 1 FROM dbo.CASRemarkThree WHERE CASNumber = @cas;

    IF @l5 >= 1800 AND @l5 <= 3500
        UPDATE dbo.CASRemarkThree SET CASRemarksFive = ISNULL(CASRemarksFive, N'') + @add WHERE CASNumber = @cas;
    ELSE IF (@l5 <> 0 AND @l5 <= 1800) OR @l4 >= 1800
        UPDATE dbo.CASRemarkThree SET CASRemarks = ISNULL(CASRemarks, N'') + @add WHERE CASNumber = @cas;
    ELSE IF (@l4 <> 0 AND @l4 <= 1800) OR @l3 >= 1800
        UPDATE dbo.CASRemarksTwo SET CASORemarksTwo = ISNULL(CASORemarksTwo, N'') + @add WHERE CASNumber = @cas;
    ELSE IF (@l3 <> 0 AND @l3 <= 1800) OR @l2 >= 1800
        UPDATE dbo.CASRemarksTwo SET CASORemarks = ISNULL(CASORemarks, N'') + @add WHERE CASNumber = @cas;
    ELSE IF (@l2 <> 0 AND @l2 <= 1800) OR @l1 >= 1800
        UPDATE dbo.CASRemarks SET CASORemarksTwo = ISNULL(CASORemarksTwo, N'') + @add WHERE CASNumber = @cas;
    ELSE IF @l1 <> 0 AND @l1 < 1800
        UPDATE dbo.CASRemarks SET CASORemarks = ISNULL(CASORemarks, N'') + @add WHERE CASNumber = @cas;
END
GO

-- SaveCLMBordx: the Bordx row. Status C when the claims are paid to the hospital (INVPayTo H), R otherwise.
CREATE OR ALTER PROCEDURE genisis.CaseListing_SaveBordx
    @bordxNo nvarchar(40), @amount money, @claims int, @status char(1), @userCode nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT genisis.WS_Bordx (BordxRefNo, BordxPAYCode, BordxDate, BordxBy, BordxClaimAmt, BordxNoClaims, BordxStatus)
    VALUES (@bordxNo, SUBSTRING(@bordxNo, 2, 2), CAST(CAST(GETDATE() AS date) AS datetime), @userCode, @amount, @claims, @status);
END
GO
