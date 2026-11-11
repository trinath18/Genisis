-- Receipt > Payor (legacy FrmReceiptPayor, "Receipt From Payor"): the receipts of what the payors pay for a bordx (By Bordx: one receipt for the whole bordx) or for single worksheets (By Worksheet: partial
-- or full payments, with the contra case and the other amounts). The desktop read SP_Receipt ('ReceiptPayByBordx' / 'ReceiptPayByWS') with a WHERE fragment, took the receipt number with GenerateSRecNo and wrote
-- ReceiptfrPAY, ContraAllocation, Claim and Bordx through text SQL and open recordsets; here each step is a procedure with typed parameters.

-- The remark line the desktop added to the claim's case remarks (each page is filled to about 1,800 characters before the next is used, in the order CASRemarks.CASORemarks, CASORemarksTwo,
-- CASRemarksTwo.CASORemarks, CASORemarksTwo, CASRemarkThree.CASRemarks, CASRemarksFive). The same routine as genisis.CaseListing_Apply's, as its own procedure.
CREATE OR ALTER PROCEDURE genisis.Case_AppendRemark
    @cas nvarchar(30), @add nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
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
    ELSE IF @l1 = 0
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.CASRemarks WHERE CASNumber = @cas) UPDATE dbo.CASRemarks SET CASORemarks = @add WHERE CASNumber = @cas;
        ELSE INSERT dbo.CASRemarks (CASNumber, CASORemarks) VALUES (@cas, @add);
    END
END
GO

-- ReceiptBordxListing: the bordx of a payor with what each has received (VIEW_WksReceiptPayBordx; Received is the sum of the check amounts of its ReceiptfrPAY rows). @status (Bordx Status): 0 all, 1 receipt
-- number given, 2 none, 3 partly received (P), 4 partly received or with a receipt, 5 without a receipt or partly received.
CREATE OR ALTER PROCEDURE genisis.ReceiptPayor_Bordx
    @payor nvarchar(4) = NULL, @bordxFrom nvarchar(40) = NULL, @bordxTo nvarchar(40) = NULL, @status int = 0, @recFrom nvarchar(30) = NULL, @recTo nvarchar(30) = NULL, @recDateFrom datetime = NULL,
    @recDateTo datetime = NULL, @mthFrom int = NULL, @mthTo int = NULL, @yrFrom int = NULL, @yrTo int = NULL, @bordxDateFrom datetime = NULL, @bordxDateTo datetime = NULL, @groupCompany nvarchar(300) = NULL,
    @policy nvarchar(60) = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(v.BordxRefNo) AS BordxNo, RTRIM(v.BordxPAYCode) AS PayorCode, RTRIM(v.ReceiptNo) AS ReceiptNo, v.ReceiptDate, v.ReceiptAmt, v.RECAccPeriodMth AS AccMonth, v.RECAccPeriodYR AS AccYear,
        RTRIM(v.ChequeNo) AS ChequeNo, v.PAYPVDate AS PvDate, RTRIM(v.PAYPVNo) AS PvNo, v.CheckAmt, RTRIM(v.BordxPAYFRStatus) AS BordxStatus, RTRIM(v.Remark) AS Remark, v.BordxDate, RTRIM(v.RCInd) AS ReceiptPrefix,
        v.BordxClaimAmt AS BordxAmount, RTRIM(v.MBMPolicyNo) AS PolicyNo, RTRIM(v.MBMGroupCompany) AS GroupCompany,
        (SELECT SUM(r.CheckAmt) FROM genisis.Pay_ReceiptfrPAY r WHERE r.BordxNo = v.BordxRefNo) AS Received
    FROM genisis.WS_ReceiptPayBordx v
    WHERE (@payor IS NULL OR v.BordxPAYCode = @payor) AND (@bordxFrom IS NULL OR v.BordxRefNo >= @bordxFrom) AND (@bordxTo IS NULL OR v.BordxRefNo <= @bordxTo)
      AND (@status = 0 OR (@status = 1 AND v.ReceiptNo IS NOT NULL AND v.ReceiptNo <> N'') OR (@status = 2 AND (v.ReceiptNo = N'' OR v.ReceiptNo IS NULL)) OR (@status = 3 AND v.BordxPAYFRStatus = 'P')
        OR (@status = 4 AND (v.BordxPAYFRStatus = 'P' OR v.ReceiptNo IS NOT NULL)) OR (@status = 5 AND (v.ReceiptNo = N'' OR v.ReceiptNo IS NULL OR v.BordxPAYFRStatus = 'P')))
      AND (@recFrom IS NULL OR v.ReceiptNo >= @recFrom) AND (@recTo IS NULL OR v.ReceiptNo <= @recTo) AND (@recDateFrom IS NULL OR v.ReceiptDate >= @recDateFrom) AND (@recDateTo IS NULL OR v.ReceiptDate <= @recDateTo)
      AND (@mthFrom IS NULL OR v.RECAccPeriodMth >= @mthFrom) AND (@mthTo IS NULL OR v.RECAccPeriodMth <= @mthTo) AND (@yrFrom IS NULL OR v.RECAccPeriodYR >= @yrFrom) AND (@yrTo IS NULL OR v.RECAccPeriodYR <= @yrTo)
      AND (@bordxDateFrom IS NULL OR v.BordxDate >= @bordxDateFrom) AND (@bordxDateTo IS NULL OR v.BordxDate <= @bordxDateTo)
      AND (@groupCompany IS NULL OR v.MBMGroupCompany = @groupCompany) AND (@policy IS NULL OR v.MBMPolicyNo = @policy)
    ORDER BY v.BordxRefNo, v.ReceiptNo
    OPTION (RECOMPILE);
END
GO

-- ReceiptWorksheetListing: the claims with what each has received (VIEW_WKSReceiptPAY; Received is the sum of the check amounts of all the claim's ReceiptfrPAY rows; the patient is the covered person, or the
-- member). The Bordx Status criteria are the claim's status here (the desktop applied BordxPAYFrStatus to a view that has no such column). @wsStatus: 1 with a receipt number, 2 without. @closeFrom/@closeTo: the
-- date the claim was closed.
CREATE OR ALTER PROCEDURE genisis.ReceiptPayor_Worksheet
    @payor nvarchar(4) = NULL, @bordxFrom nvarchar(40) = NULL, @bordxTo nvarchar(40) = NULL, @wsFrom nvarchar(30) = NULL, @wsTo nvarchar(30) = NULL, @wsStatus int = 0, @status int = 0, @recFrom nvarchar(30) = NULL,
    @recTo nvarchar(30) = NULL, @recDateFrom datetime = NULL, @recDateTo datetime = NULL, @mthFrom int = NULL, @mthTo int = NULL, @yrFrom int = NULL, @yrTo int = NULL, @bordxDateFrom datetime = NULL,
    @bordxDateTo datetime = NULL, @closeFrom datetime = NULL, @closeTo datetime = NULL, @groupCompany nvarchar(300) = NULL, @policy nvarchar(60) = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(v.CASNumber) AS CaseNo, CAST(v.ClaimVersion AS int) AS ClaimVersion, RTRIM(v.BordxRefNo) AS BordxNo, v.CLMTotalApproved AS Amount, RTRIM(v.ReceiptNo) AS ReceiptNo, v.ReceiptDate, v.ReceiptAmt,
        v.RECAccPeriodMth AS AccMonth, v.RECAccPeriodYR AS AccYear, v.CheckAmt, RTRIM(v.PVRecPayorStatus) AS ClaimStatus, v.PAYBankCharge AS BankCharge, RTRIM(v.ContraCaseNo) AS ContraCaseNo,
        RTRIM(v.ContraVersionNo) AS ContraVersionNo, v.ContraCaseAmt, v.PAYGainLoss AS GainLoss, v.PAYMisc AS Misc, v.PAYDiscount AS Discount, v.PAYWriteOff AS WriteOff, v.PAYMedixReceipt AS MedixReceipt,
        RTRIM(v.PAYRemarks) AS PayRemarks, RTRIM(v.ChequeNo) AS ChequeNo, RTRIM(v.Remark) AS Remark, v.RecPAYIndex AS ReceiptIndex, v.BordxDate, v.ClaimCloseDate, RTRIM(v.RCInd) AS ReceiptPrefix, RTRIM(v.MBMPolicyNo) AS PolicyNo,
        RTRIM(v.MBMGroupCompany) AS GroupCompany, RTRIM(v.MBMNumber) AS MemberNo, RTRIM(v.MBMPatientCovID) AS CoverId,
        RTRIM(CASE WHEN v.MBMPatientCovID <> N'00' THEN ISNULL((SELECT TOP (1) c.MBMCName FROM dbo.MBMCoveredPersons c WHERE c.MBMCNumber = v.MBMNumber AND c.MBMCCoverID = v.MBMPatientCovID), N'') ELSE ISNULL(v.MBMName, N'') END) AS PatientName,
        (SELECT SUM(r.CheckAmt) FROM genisis.Pay_ReceiptfrPAY r WHERE r.CASNumber = v.CASNumber AND r.ClaimVersion = CAST(v.ClaimVersion AS nvarchar(100))) AS Received, (SELECT TOP (1) RTRIM(r.PAYPVNo) FROM genisis.Pay_ReceiptfrPAY r WHERE r.RecPAYIndex = v.RecPAYIndex) AS PvNo,
        (SELECT TOP (1) r.PAYPVDate FROM genisis.Pay_ReceiptfrPAY r WHERE r.RecPAYIndex = v.RecPAYIndex) AS PvDate
    FROM genisis.WS_ReceiptPayWs v
    WHERE (@payor IS NULL OR v.BordxPAYCode = @payor) AND (@bordxFrom IS NULL OR v.BordxRefNo >= @bordxFrom) AND (@bordxTo IS NULL OR v.BordxRefNo <= @bordxTo) AND (@wsFrom IS NULL OR v.CASNumber >= @wsFrom)
      AND (@wsTo IS NULL OR v.CASNumber <= @wsTo)
      AND (@wsStatus = 0 OR (@wsStatus = 1 AND v.ReceiptNo IS NOT NULL AND v.ReceiptNo <> N'') OR (@wsStatus = 2 AND (v.ReceiptNo = N'' OR v.ReceiptNo IS NULL)))
      AND (@status = 0 OR (@status = 1 AND v.ReceiptNo IS NOT NULL AND v.ReceiptNo <> N'') OR (@status = 2 AND (v.ReceiptNo = N'' OR v.ReceiptNo IS NULL)) OR (@status = 3 AND v.PVRecPayorStatus = 'P')
        OR (@status = 4 AND (v.PVRecPayorStatus = 'P' OR v.ReceiptNo IS NOT NULL)) OR (@status = 5 AND (v.ReceiptNo = N'' OR v.ReceiptNo IS NULL OR v.PVRecPayorStatus = 'P')))
      AND (@recFrom IS NULL OR v.ReceiptNo >= @recFrom) AND (@recTo IS NULL OR v.ReceiptNo <= @recTo) AND (@recDateFrom IS NULL OR v.ReceiptDate >= @recDateFrom) AND (@recDateTo IS NULL OR v.ReceiptDate <= @recDateTo)
      AND (@mthFrom IS NULL OR v.RECAccPeriodMth >= @mthFrom) AND (@mthTo IS NULL OR v.RECAccPeriodMth <= @mthTo) AND (@yrFrom IS NULL OR v.RECAccPeriodYR >= @yrFrom) AND (@yrTo IS NULL OR v.RECAccPeriodYR <= @yrTo)
      AND (@bordxDateFrom IS NULL OR v.BordxDate >= @bordxDateFrom) AND (@bordxDateTo IS NULL OR v.BordxDate <= @bordxDateTo) AND (@closeFrom IS NULL OR v.ClaimCloseDate >= @closeFrom)
      AND (@closeTo IS NULL OR v.ClaimCloseDate <= @closeTo) AND (@groupCompany IS NULL OR v.MBMGroupCompany = @groupCompany) AND (@policy IS NULL OR v.MBMPolicyNo = @policy)
    ORDER BY v.BordxRefNo, v.CASNumber, v.ClaimVersion, v.RecPAYIndex
    OPTION (RECOMPILE);
END
GO

-- cboPayor_Click: the group companies of a payor (CompanyGrp).
CREATE OR ALTER PROCEDURE genisis.ReceiptPayor_GroupCompanies
    @payor nvarchar(4)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(CoGRPName) AS GroupCompany FROM dbo.CompanyGrp WHERE CoGRPPaycode = @payor ORDER BY CoGRPName;
END
GO

-- lstBordx_ItemCheck: how the bordx stands in the Bordx table (BordxPAYFRStatus F fully received, P partly).
CREATE OR ALTER PROCEDURE genisis.ReceiptPayor_BordxStatus
    @bordxNo nvarchar(40)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(ISNULL(BordxPAYFRStatus, N'')) AS BordxStatus FROM genisis.WS_Bordx WHERE BordxRefNo = @bordxNo;
END
GO

-- UpdateBordxStatus: the date the bordx was received and its status (F full, P partial).
CREATE OR ALTER PROCEDURE genisis.ReceiptPayor_SetBordxStatus
    @bordxNo nvarchar(40), @receiptDate datetime, @status char(1)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.WS_Bordx SET BordxPAYRecDate = @receiptDate, BordxPAYFRStatus = @status WHERE BordxRefNo = @bordxNo;
END
GO

-- SaveRecByBordx: one receipt for a whole bordx. For each claim of the bordx a ReceiptfrPAY row (the receipt amount on every row, the claim's approved amount as the check amount) is added, or, for an update,
-- the rows of this receipt number are changed; the claim is marked F, a "Claim Receipt" line is added to its remarks, and the bordx is marked F. The desktop added one row for every row the view gave (a bordx
-- that was received before gave each claim twice); one row per claim is added here.
CREATE OR ALTER PROCEDURE genisis.ReceiptPayor_SaveBordx
    @bordxNo nvarchar(40), @receiptNo nvarchar(30), @receiptDate datetime, @receiptAmt money, @chequeNo nvarchar(40), @pvNo nvarchar(40) = NULL, @pvDate datetime = NULL, @accMonth int = NULL,
    @accYear int = NULL, @remark nvarchar(400) = NULL, @insert bit, @userCode nvarchar(20), @today datetime
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @claims TABLE (cas nvarchar(30), ver int, approved money);
    INSERT @claims SELECT DISTINCT CASNumber, CAST(ClaimVersion AS int), CLMTotalApproved FROM genisis.WS_ReceiptPayWs WHERE BordxRefNo = @bordxNo;
    DECLARE @cas nvarchar(30), @ver int, @approved money, @n int = 0;
    DECLARE @line nvarchar(max) = N'~' + NCHAR(10) + CONVERT(nvarchar(10), @today, 103) + N'   Claim Receipt  -  ' + @receiptNo + N' on ' + CONVERT(nvarchar(10), @receiptDate, 103) + REPLICATE(N' ', 54) + @userCode + N' ';
    DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT cas, ver, approved FROM @claims ORDER BY cas, ver;
    OPEN c;
    FETCH NEXT FROM c INTO @cas, @ver, @approved;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF @insert = 1
            INSERT genisis.Pay_ReceiptfrPAY (CASNumber, ClaimVersion, ReceiptNo, ReceiptDate, ReceiptAmt, ChequeNo, BordxNo, PAYLastUpdatedate, PAYLastUpdateUser, RecPAYAmt, CheckAmt, PAYPVNo, PAYPVDate, RECAccPeriodMth,
                RECAccPeriodYR, Remark)
            VALUES (@cas, CAST(@ver AS nvarchar(100)), @receiptNo, @receiptDate, @receiptAmt, @chequeNo, @bordxNo, @today, @userCode, @receiptAmt, @approved, ISNULL(@pvNo, N''), @pvDate, @accMonth, @accYear, @remark);
        ELSE
            UPDATE genisis.Pay_ReceiptfrPAY SET ReceiptDate = @receiptDate, ReceiptAmt = @receiptAmt, ChequeNo = @chequeNo, BordxNo = @bordxNo, PAYLastUpdatedate = @today, PAYLastUpdateUser = @userCode,
                RecPAYAmt = @receiptAmt, PAYPVNo = ISNULL(@pvNo, N''), PAYPVDate = @pvDate, RECAccPeriodMth = @accMonth, RECAccPeriodYR = @accYear, Remark = @remark
            WHERE ReceiptNo = @receiptNo AND CASNumber = @cas AND ClaimVersion = CAST(@ver AS nvarchar(100));
        UPDATE genisis.WS_Claim SET PVRecPayorStatus = 'F' WHERE BordxRefNo = @bordxNo AND CASNumber = @cas AND ClaimVersion = @ver;
        EXEC genisis.Case_AppendRemark @cas, @line;
        SET @n += 1;
        FETCH NEXT FROM c INTO @cas, @ver, @approved;
    END
    CLOSE c;
    DEALLOCATE c;
    UPDATE genisis.WS_Bordx SET BordxPAYRecDate = @receiptDate, BordxPAYFRStatus = 'F' WHERE BordxRefNo = @bordxNo;
    SELECT @n AS Claims;
END
GO

-- SaveRecByWS + UpdateContra + the bordx status of ProcessWS: one claim's receipt. @recIndex NULL adds a ReceiptfrPAY row (the check amount is what is paid for the claim, the receipt amount the cheque's);
-- a row given is changed, and the rows of its receipt number get the same receipt header (UPdateWholeRecInfo). The claim is marked @status (F or P), the "Claim Receipt" line is added to its remarks, a contra case
-- updates or adds the claim's ContraAllocation row, and the bordx is marked F when every claim of it is F, otherwise P.
CREATE OR ALTER PROCEDURE genisis.ReceiptPayor_SaveWorksheet
    @cas nvarchar(30), @version int, @bordxNo nvarchar(40), @status char(1), @paymentAmt money, @bankCharge money = 0, @gainLoss money = 0, @misc money = 0, @discount money = 0, @writeOff money = 0, @medixReceipt money = 0,
    @payRemarks nvarchar(400) = NULL, @receiptNo nvarchar(30), @receiptDate datetime, @receiptAmt money, @chequeNo nvarchar(40), @receiptRemark nvarchar(400) = NULL, @pvNo nvarchar(40) = NULL, @pvDate datetime = NULL,
    @accMonth int = NULL, @accYear int = NULL, @contraCase nvarchar(100) = NULL, @contraVersion nvarchar(10) = NULL, @contraAmt money = NULL, @recIndex int = NULL, @userCode nvarchar(20), @today datetime
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ver nvarchar(100) = CAST(@version AS nvarchar(100));
    IF @recIndex IS NULL
        INSERT genisis.Pay_ReceiptfrPAY (CASNumber, ClaimVersion, ReceiptNo, ReceiptDate, ReceiptAmt, ChequeNo, Remark, BordxNo, PAYLastUpdatedate, PAYLastUpdateUser, RecPAYAmt, CheckAmt, PAYPVNo, PAYPVDate, RECAccPeriodMth,
            RECAccPeriodYR, PAYBankCharge, PAYGainLoss, PAYMisc, PAYDiscount, PAYWriteOff, PAYMedixReceipt, PAYRemarks)
        VALUES (@cas, @ver, @receiptNo, @receiptDate, @receiptAmt, @chequeNo, @receiptRemark, @bordxNo, @today, @userCode, @receiptAmt, @paymentAmt, ISNULL(@pvNo, N''), @pvDate, @accMonth, @accYear, @bankCharge, @gainLoss, @misc,
            @discount, @writeOff, @medixReceipt, @payRemarks);
    ELSE
    BEGIN
        UPDATE genisis.Pay_ReceiptfrPAY SET ReceiptDate = @receiptDate, ReceiptAmt = @receiptAmt, ChequeNo = @chequeNo, BordxNo = @bordxNo, PAYLastUpdatedate = @today, PAYLastUpdateUser = @userCode, RecPAYAmt = @receiptAmt,
            CheckAmt = @paymentAmt, PAYPVNo = ISNULL(@pvNo, N''), PAYPVDate = @pvDate, RECAccPeriodMth = @accMonth, RECAccPeriodYR = @accYear, Remark = @receiptRemark, PAYRemarks = @payRemarks, PAYGainLoss = @gainLoss,
            PAYMisc = @misc, PAYDiscount = @discount, PAYWriteOff = @writeOff, PAYMedixReceipt = @medixReceipt, PAYBankCharge = @bankCharge
        WHERE RecPAYIndex = @recIndex;
        UPDATE genisis.Pay_ReceiptfrPAY SET ReceiptDate = @receiptDate, ReceiptAmt = @receiptAmt, ChequeNo = @chequeNo, PAYLastUpdatedate = @today, PAYLastUpdateUser = @userCode, RecPAYAmt = @receiptAmt, PAYPVNo = ISNULL(@pvNo, N''),
            PAYPVDate = @pvDate, RECAccPeriodMth = @accMonth, RECAccPeriodYR = @accYear, PAYRemarks = @receiptRemark
        WHERE ReceiptNo = @receiptNo;
    END
    UPDATE genisis.WS_Claim SET PVRecPayorStatus = @status WHERE CASNumber = @cas AND ClaimVersion = @version;
    DECLARE @line nvarchar(max) = N'~' + NCHAR(10) + CONVERT(nvarchar(10), @today, 103) + N'   Claim Receipt  -  ' + @receiptNo + N' on ' + CONVERT(nvarchar(10), @receiptDate, 103) + REPLICATE(N' ', 54) + @userCode + N' ';
    EXEC genisis.Case_AppendRemark @cas, @line;
    IF NULLIF(@contraCase, N'') IS NOT NULL
    BEGIN
        DECLARE @contraIndex int = (SELECT TOP (1) CONIndex FROM genisis.Pay_ContraAllocation WHERE CaseNo = @cas AND VersionNo = @ver ORDER BY CONIndex);
        IF @contraIndex IS NULL
            INSERT genisis.Pay_ContraAllocation (RefNo, CaseNo, VersionNo, CaseAmt, ContraCaseNo, ContraVersionNo, ContraCaseAmt) VALUES (@receiptNo, @cas, @ver, @paymentAmt, @contraCase, @contraVersion, @contraAmt);
        ELSE
            UPDATE genisis.Pay_ContraAllocation SET RefNo = @receiptNo, CaseAmt = @paymentAmt, ContraCaseNo = @contraCase, ContraVersionNo = @contraVersion, ContraCaseAmt = @contraAmt WHERE CONIndex = @contraIndex;
    END
    DECLARE @all int = (SELECT COUNT(*) FROM genisis.WS_Claim WHERE BordxRefNo = @bordxNo), @full int = (SELECT COUNT(*) FROM genisis.WS_Claim WHERE BordxRefNo = @bordxNo AND PVRecPayorStatus = 'F');
    UPDATE genisis.WS_Bordx SET BordxPAYRecDate = @receiptDate, BordxPAYFRStatus = CASE WHEN @all = @full THEN 'F' ELSE 'P' END WHERE BordxRefNo = @bordxNo;
    SELECT CASE WHEN @all = @full THEN 'F' ELSE 'P' END AS BordxStatus;
END
GO

-- DeleteRecords: By Bordx deletes every ReceiptfrPAY row of the bordx and clears the claims' and the bordx's receipt status; By Worksheet deletes the rows of the claim and clears its status.
CREATE OR ALTER PROCEDURE genisis.ReceiptPayor_Delete
    @bordxNo nvarchar(40) = NULL, @cas nvarchar(30) = NULL, @version int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @n int;
    IF @bordxNo IS NOT NULL
    BEGIN
        DELETE genisis.Pay_ReceiptfrPAY WHERE BordxNo = @bordxNo;
        SET @n = @@ROWCOUNT;
        UPDATE genisis.WS_Claim SET PVRecPayorStatus = NULL WHERE BordxRefNo = @bordxNo;
        UPDATE genisis.WS_Bordx SET BordxPAYFRStatus = NULL WHERE BordxRefNo = @bordxNo;
    END
    ELSE
    BEGIN
        DELETE genisis.Pay_ReceiptfrPAY WHERE CASNumber = @cas AND ClaimVersion = CAST(@version AS nvarchar(100));
        SET @n = @@ROWCOUNT;
        UPDATE genisis.WS_Claim SET PVRecPayorStatus = NULL WHERE CASNumber = @cas AND ClaimVersion = @version;
    END
    SELECT @n AS Deleted;
END
GO

-- cmdPrintPV_Click: the lines of the official receipt (View_ReceiptFrPayorRec): the payor, the date, the cheque and the amount, and the bordx of each row.
CREATE OR ALTER PROCEDURE genisis.ReceiptPayor_OfficialReceipt
    @receiptNo nvarchar(30)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(ReceiptNo) AS ReceiptNo, ReceiptDate, RTRIM(BordxNo) AS BordxNo, ReceiptAmt, RTRIM(ChequeNo) AS ChequeNo, RTRIM(PAYCompanyName) AS PayorName
    FROM genisis.Pay_ViewReceiptfrPayorRec WHERE ReceiptNo = @receiptNo ORDER BY BordxNo;
END
GO
