-- Claim Bordx > Worksheet > Remove Worksheet (legacy FrmBordxRevise, "Bordereaux Revise"): list the worksheets of one bordx and take the ticked ones out of it.
-- The desktop ran dbo.BordxRevise for the list and issued the updates one statement at a time, with no transaction; here the removal of the ticked worksheets is one transaction.

-- dbo.BordxRevise: the worksheets of a bordx with the member and patient. Like that procedure, a worksheet whose case has no cross-reference row or whose member has no
-- cross-reference row is not listed (inner joins); the patient is the member for cover id '00' and the covered person otherwise. SentOut: the bordx was sent to the payor or to MNRB.
CREATE OR ALTER PROCEDURE genisis.BordxRevise_Search
    @bordxNo nvarchar(40)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(c.CASNumber) AS CaseNo, c.ClaimVersion, b.BordxDate, b.BordxClaimAmt, b.BordxNoClaims, b.BordxPrintedDate,
        CAST(CASE WHEN b.BordxPAYToStatus = '1' OR b.BordxMNRBToStatus = '1' THEN 1 ELSE 0 END AS bit) AS SentOut,
        CASE x.MBMPatientCovID WHEN '00' THEN m.MBMName ELSE p.MBMCName END AS PatientName,
        c.ClaimOpenDate, x.MBMNumber AS MbmNumber, x.CASStatus AS CaseStatus, x.CASDateAdmitted AS DateAdmitted, x.CASDischargeDate AS DischargeDate,
        c.CLMTotalApproved AS TotalApproved
    FROM genisis.WS_Bordx b
    JOIN genisis.WS_Claim c ON c.BordxRefNo = b.BordxRefNo
    JOIN genisis.Maint_CASCrossReference x ON x.CASNumber = c.CASNumber
    JOIN genisis.Maint_MBMCrossReference m ON m.MBMNumber = x.MBMNumber
    LEFT JOIN dbo.MBMCoveredPersons p ON p.MBMCNumber = x.MBMNumber AND p.MBMCCoverID = x.MBMPatientCovID
    WHERE b.BordxRefNo = @bordxNo
    ORDER BY c.CASNumber, c.ClaimVersion;
END
GO

-- RemoveFromBordx: takes one worksheet out of its bordx. The bordx loses one claim and the worksheet's approved amount, an archive row records who removed it and when
-- (BordxWSArchive), the worksheet's invoices are un-bordxed, and the worksheet itself is free again (ClaimBordxStatus 0, no bordx; a worksheet already sent to the payor as an ASCII
-- file (CLMBordxWSASCII = 'Y') goes back to 'N' with status 'D', which marks it for deletion from the file). The desktop only disabled the Remove button of a bordx that had been
-- sent out; here the procedure refuses it too. Result: 0 done, 1 worksheet or bordx not found, 2 the worksheet is not in this bordx, 3 the bordx has been sent out.
CREATE OR ALTER PROCEDURE genisis.BordxRevise_Remove
    @bordxNo nvarchar(40), @cas nvarchar(30), @version int, @userCode nvarchar(20), @today datetime
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @claimBordx nvarchar(40), @approved money, @sentOut bit, @bordxDate datetime;
    SELECT @claimBordx = RTRIM(BordxRefNo), @approved = ISNULL(CLMTotalApproved, 0) FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @version;
    IF @@ROWCOUNT = 0
    BEGIN
        SELECT 1 AS Result;
        RETURN;
    END
    SELECT @bordxDate = BordxDate, @sentOut = CASE WHEN BordxPAYToStatus = '1' OR BordxMNRBToStatus = '1' THEN 1 ELSE 0 END FROM genisis.WS_Bordx WHERE BordxRefNo = @bordxNo;
    IF @@ROWCOUNT = 0
    BEGIN
        SELECT 1 AS Result;
        RETURN;
    END
    IF ISNULL(@claimBordx, N'') <> RTRIM(@bordxNo)
    BEGIN
        SELECT 2 AS Result;
        RETURN;
    END
    IF @sentOut = 1
    BEGIN
        SELECT 3 AS Result;
        RETURN;
    END

    UPDATE genisis.WS_Bordx SET BordxNoClaims = BordxNoClaims - 1, BordxClaimAmt = ISNULL(BordxClaimAmt, 0) - @approved WHERE BordxRefNo = @bordxNo;
    INSERT genisis.WS_BordxWSArchive (CASNumber, Claimversion, BordxRefNo, BordxDate, LastUpdateUser, LastUpdateDate)
    VALUES (@cas, CAST(@version AS nvarchar(4)), @bordxNo, @bordxDate, @userCode, @today);
    UPDATE genisis.WS_ClaimInvoice SET INVBordxRefNo = N'', INVBordxed = 0 WHERE INVBordxRefNo = @bordxNo AND CASNumber = @cas AND INVWSversion = CAST(@version AS nvarchar(4));
    UPDATE genisis.WS_Claim
    SET ClaimBordxStatus = 0, BordxRefNo = N'',
        CLMBordxWSStatus = CASE WHEN CLMBordxWSASCII = N'Y' THEN N'D' ELSE CLMBordxWSStatus END,
        CLMBordxWSASCII = CASE WHEN CLMBordxWSASCII = N'Y' THEN N'N' ELSE CLMBordxWSASCII END
    WHERE CASNumber = @cas AND ClaimVersion = @version;
    SELECT 0 AS Result;
END
GO
