-- Receipt > From Member (legacy FrmReceiptMember, "Receipt From Member"): the debit notes raised to members (claims whose amount payable by MEDIX to the member is negative) and the receipts of the money
-- the members paid back. The desktop searched VIEW_ReceiptFrMBM with a WHERE fragment and wrote ReceiptFrMBM and ContraAllocation through open recordsets; here the search and each write are procedures with
-- typed parameters.

-- cmdSearch_Click + ReceiptFrMBMListing: the debit notes (reference starting D) of members who owe money, with the receipt row of each payment. The payment status: 0 all, 1 paid (F), 2 unpaid (none),
-- 3 partly paid (P), 4 paid and partly paid, 5 unpaid and partly paid. The "...Empty" switches keep the rows with no value. Amounts compare as the list shows them (positive); the desktop compared the
-- negative stored value with a text, which could never match what a user types.
CREATE OR ALTER PROCEDURE genisis.ReceiptMember_Search
    @caseFrom nvarchar(30) = NULL, @caseTo nvarchar(30) = NULL, @memberNo nvarchar(80) = NULL, @memberName nvarchar(100) = NULL, @payor nvarchar(4) = NULL, @status int = 0,
    @recFrom nvarchar(30) = NULL, @recTo nvarchar(30) = NULL, @recEmpty bit = 0, @recDateFrom datetime = NULL, @recDateTo datetime = NULL,
    @drFrom nvarchar(40) = NULL, @drTo nvarchar(40) = NULL, @drEmpty bit = 0, @amtFrom money = NULL, @amtTo money = NULL, @amtEmpty bit = 0,
    @mthFrom int = NULL, @mthTo int = NULL, @mthEmpty bit = 0, @yrFrom int = NULL, @yrTo int = NULL, @yrEmpty bit = 0, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(DebitCreditRefNo) AS DebitNo, RTRIM(PVRecMBMStatus) AS PaymentStatus, RTRIM(CASNumber) AS CaseNo, CAST(ClaimVersion AS int) AS ClaimVersion, CLMPayableByMedix2M AS PayableByMedix,
        ReceiptAmt AS ReceiptAmt, ReceiptIndex, RTRIM(ReceiptNo) AS ReceiptNo, ReceiptDate, RECMBMAccPeriodMth AS AccMonth, RECMBMAccPeriodYr AS AccYear, MBMBankCharge AS BankCharge,
        RTRIM(ContraCaseNo) AS ContraCaseNo, RTRIM(ContraVersionNo) AS ContraVersionNo, ContraCaseAmt, MBMGainLoss AS GainLoss, MBMMisc AS Misc, MBMDiscount AS Discount, MBMAmtPaid AS AmtPaid,
        MBMWriteOff AS WriteOff, MBMMedixReceipt AS MedixReceipt, RTRIM(MBMRemarks) AS Remarks, RTRIM(MBMNumber) AS MemberNo, RTRIM(MBMName) AS MemberName, RTRIM(PayCode) AS PayorCode
    FROM genisis.Pay_ViewReceiptfrMBM
    WHERE CLMPayableByMedix2M <= 0 AND SUBSTRING(DebitCreditRefNo, 1, 1) = 'D'
      AND (@caseFrom IS NULL OR CASNumber >= @caseFrom) AND (@caseTo IS NULL OR CASNumber <= @caseTo)
      AND (@memberNo IS NULL OR MBMNumber LIKE N'%' + @memberNo + N'%') AND (@memberName IS NULL OR MBMName LIKE N'%' + @memberName + N'%') AND (@payor IS NULL OR PayCode = @payor)
      AND (@status = 0 OR (@status = 1 AND PVRecMBMStatus = 'F') OR (@status = 2 AND (PVRecMBMStatus = '' OR PVRecMBMStatus IS NULL)) OR (@status = 3 AND PVRecMBMStatus = 'P')
        OR (@status = 4 AND PVRecMBMStatus IN ('P', 'F')) OR (@status = 5 AND (PVRecMBMStatus = '' OR PVRecMBMStatus IS NULL OR PVRecMBMStatus = 'P')))
      AND (@recFrom IS NULL OR ReceiptNo >= @recFrom) AND (@recTo IS NULL OR ReceiptNo <= @recTo) AND (@recEmpty = 0 OR ReceiptNo IS NULL OR ReceiptNo = N'')
      AND (@recDateFrom IS NULL OR ReceiptDate >= @recDateFrom) AND (@recDateTo IS NULL OR ReceiptDate <= @recDateTo)
      AND (@drFrom IS NULL OR DebitCreditRefNo >= @drFrom) AND (@drTo IS NULL OR DebitCreditRefNo <= @drTo) AND (@drEmpty = 0 OR DebitCreditRefNo IS NULL OR DebitCreditRefNo = N'')
      AND (@amtFrom IS NULL OR -CLMPayableByMedix2M >= @amtFrom) AND (@amtTo IS NULL OR -CLMPayableByMedix2M <= @amtTo) AND (@amtEmpty = 0 OR CLMPayableByMedix2M IS NULL)
      AND (@mthFrom IS NULL OR RECMBMAccPeriodMth >= @mthFrom) AND (@mthTo IS NULL OR RECMBMAccPeriodMth <= @mthTo) AND (@mthEmpty = 0 OR RECMBMAccPeriodMth IS NULL)
      AND (@yrFrom IS NULL OR RECMBMAccPeriodYr >= @yrFrom) AND (@yrTo IS NULL OR RECMBMAccPeriodYr <= @yrTo) AND (@yrEmpty = 0 OR RECMBMAccPeriodYr IS NULL)
    ORDER BY CASNumber, ClaimVersion, ReceiptIndex
    OPTION (RECOMPILE);
END
GO

-- Whether the user is "authorised" (USR.AuthorisedStatus other than N): the desktop gave such a user a new receipt row for every payment and allowed a fully paid row to be received again; a user with
-- status N updates the receipt row of the same receipt number and is stopped by "Payment has been made!". No such user: not authorised.
CREATE OR ALTER PROCEDURE genisis.ReceiptMember_UserAuthorised
    @userCode nvarchar(30)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM genisis.Maint_USR WHERE USRCode = @userCode AND RTRIM(ISNULL(AuthorisedStatus, N'N')) <> N'N') THEN 1 ELSE 0 END AS bit) AS Authorised;
END
GO

-- The receipt rows of a claim (ProcessCheckedItem's look-ups): how it stands in the view for one receipt number.
CREATE OR ALTER PROCEDURE genisis.ReceiptMember_Receipt
    @cas nvarchar(30), @version int, @receiptNo nvarchar(30) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(CASNumber) AS CaseNo, CAST(ClaimVersion AS int) AS ClaimVersion, CLMPayableByMedix2M AS PayableByMedix, RTRIM(DebitCreditRefNo) AS DebitNo, ReceiptIndex, RTRIM(ReceiptNo) AS ReceiptNo,
        ReceiptAmt, RTRIM(PVRecMBMStatus) AS PaymentStatus
    FROM genisis.Pay_ViewReceiptfrMBM
    WHERE CASNumber = @cas AND ClaimVersion = @version AND (@receiptNo IS NULL OR ReceiptNo = @receiptNo)
    ORDER BY ReceiptIndex;
END
GO

-- UpdateRecord + UpdateContra for one ticked claim. @authorised 0: the receipt row of (claim, receipt number) is updated, or added; 1: a new receipt row is always added. Then, for a full payment, the first
-- receipt row of the claim is marked F as well (the desktop's second recordset). The account period, remarks and the numbers follow the desktop (a missing number is stored as 0). A contra case: the claim's
-- ContraAllocation row (case and version) is updated or added with this receipt number, the amount received and the contra case, version and amount.
CREATE OR ALTER PROCEDURE genisis.ReceiptMember_Save
    @cas nvarchar(30), @version int, @status char(1), @paymentAmt money, @receiptNo nvarchar(30), @receiptDate datetime, @chequeNo nvarchar(40), @receiptRemarks nvarchar(100) = NULL,
    @accMonth int = NULL, @accYear int = NULL, @bankCharge money = 0, @gainLoss money = 0, @misc money = 0, @discount money = 0, @amtPaid money = 0, @writeOff money = 0, @medixReceipt money = 0,
    @remarks nvarchar(100) = NULL, @contraCase nvarchar(100) = NULL, @contraVersion nvarchar(10) = NULL, @contraAmt money = NULL, @userCode nvarchar(20), @authorised bit, @today datetime
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ver nvarchar(100) = CAST(@version AS nvarchar(100)), @index int;
    IF @authorised = 0 SELECT TOP (1) @index = ReceiptIndex FROM genisis.Pay_ReceiptfrMBM WHERE CASNumber = @cas AND ClaimVersion = @ver AND ReceiptNo = @receiptNo ORDER BY ReceiptIndex;
    IF @index IS NULL
    BEGIN
        INSERT genisis.Pay_ReceiptfrMBM (ReceiptNo, ReceiptDate, ReceiptChequeNo, ReceiptLastUpdateDate, ReceiptLastUpdateUser, CASNumber, ClaimVersion, ReceiptRemarks, RECMBMAccPeriodMth, RECMBMAccPeriodYr, PVRecMBMStatus,
            ReceiptAmt, MBMBankCharge, MBMGainLoss, MBMMisc, MBMDiscount, MBMAmtPaid, MBMWriteOff, MBMMedixReceipt, MBMRemarks)
        VALUES (@receiptNo, @receiptDate, @chequeNo, @today, @userCode, @cas, @ver, @receiptRemarks, @accMonth, @accYear, @status, @paymentAmt, @bankCharge, @gainLoss, @misc, @discount, @amtPaid, @writeOff, @medixReceipt,
            NULLIF(@remarks, N''));
        SET @index = SCOPE_IDENTITY();
    END
    ELSE
        UPDATE genisis.Pay_ReceiptfrMBM SET ReceiptNo = @receiptNo, ReceiptDate = @receiptDate, ReceiptChequeNo = @chequeNo, ReceiptLastUpdateDate = @today, ReceiptLastUpdateUser = @userCode,
            ReceiptRemarks = @receiptRemarks, RECMBMAccPeriodMth = ISNULL(@accMonth, RECMBMAccPeriodMth), RECMBMAccPeriodYr = ISNULL(@accYear, RECMBMAccPeriodYr), PVRecMBMStatus = @status, ReceiptAmt = @paymentAmt,
            MBMBankCharge = @bankCharge, MBMGainLoss = @gainLoss, MBMMisc = @misc, MBMDiscount = @discount, MBMAmtPaid = @amtPaid, MBMWriteOff = @writeOff, MBMMedixReceipt = @medixReceipt,
            MBMRemarks = ISNULL(NULLIF(@remarks, N''), MBMRemarks)
        WHERE ReceiptIndex = @index;
    IF @status = 'F'
        UPDATE genisis.Pay_ReceiptfrMBM SET PVRecMBMStatus = 'F' WHERE ReceiptIndex = (SELECT TOP (1) ReceiptIndex FROM genisis.Pay_ReceiptfrMBM WHERE CASNumber = @cas AND ClaimVersion = @ver ORDER BY ReceiptIndex);
    IF NULLIF(@contraCase, N'') IS NOT NULL
    BEGIN
        DECLARE @contraIndex int = (SELECT TOP (1) CONIndex FROM genisis.Pay_ContraAllocation WHERE CaseNo = @cas AND VersionNo = @ver ORDER BY CONIndex);
        IF @contraIndex IS NULL
            INSERT genisis.Pay_ContraAllocation (RefNo, CaseNo, VersionNo, CaseAmt, ContraCaseNo, ContraVersionNo, ContraCaseAmt) VALUES (@receiptNo, @cas, @ver, @paymentAmt, @contraCase, @contraVersion, @contraAmt);
        ELSE
            UPDATE genisis.Pay_ContraAllocation SET RefNo = @receiptNo, CaseNo = @cas, VersionNo = @ver, CaseAmt = @paymentAmt, ContraCaseNo = @contraCase, ContraVersionNo = @contraVersion, ContraCaseAmt = @contraAmt
            WHERE CONIndex = @contraIndex;
    END
    SELECT @index AS ReceiptIndex;
END
GO

-- lstReceiptMBM_Click (Edit): one receipt row as it stands, for the entry boxes.
CREATE OR ALTER PROCEDURE genisis.ReceiptMember_Row
    @receiptIndex int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(ReceiptNo) AS ReceiptNo, ReceiptDate, ReceiptAmt, RTRIM(ReceiptChequeNo) AS ChequeNo, RTRIM(ReceiptRemarks) AS ReceiptRemarks, RECMBMAccPeriodMth AS AccMonth, RECMBMAccPeriodYr AS AccYear,
        RTRIM(CASNumber) AS CaseNo, CAST(ClaimVersion AS int) AS ClaimVersion, MBMBankCharge AS BankCharge, RTRIM(ContraCaseNo) AS ContraCaseNo, RTRIM(ContraVersionNo) AS ContraVersionNo, ContraCaseAmt,
        MBMGainLoss AS GainLoss, MBMMisc AS Misc, MBMDiscount AS Discount, MBMAmtPaid AS AmtPaid, MBMWriteOff AS WriteOff, MBMMedixReceipt AS MedixReceipt, RTRIM(MBMRemarks) AS Remarks
    FROM genisis.Pay_ViewReceiptfrMBM WHERE ReceiptIndex = @receiptIndex;
END
GO

-- CmdUpdate_Click: changes one receipt row (an empty optional number is stored as NULL) and its ContraAllocation row (looked up by the receipt number the row had, the case and the version). The desktop added a
-- row that had no case number when the row had no receipt yet; a row that does not exist is not changed here (0 rows) and the service says so.
CREATE OR ALTER PROCEDURE genisis.ReceiptMember_Update
    @receiptIndex int, @receiptNo nvarchar(30), @receiptDate datetime, @receiptAmt money, @chequeNo nvarchar(40), @receiptRemarks nvarchar(100) = NULL, @accMonth int = NULL, @accYear int = NULL,
    @bankCharge money = NULL, @gainLoss money = NULL, @misc money = NULL, @discount money = NULL, @amtPaid money = NULL, @writeOff money = NULL, @medixReceipt money = NULL, @remarks nvarchar(100) = NULL,
    @contraCase nvarchar(100) = NULL, @contraVersion nvarchar(10) = NULL, @contraAmt money = NULL, @userCode nvarchar(20), @today datetime
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @oldReceiptNo nvarchar(30), @cas nvarchar(30), @ver nvarchar(100);
    SELECT @oldReceiptNo = ReceiptNo, @cas = CASNumber, @ver = ClaimVersion FROM genisis.Pay_ReceiptfrMBM WHERE ReceiptIndex = @receiptIndex;
    IF @cas IS NULL
    BEGIN
        SELECT 0 AS Updated;
        RETURN;
    END
    UPDATE genisis.Pay_ReceiptfrMBM SET ReceiptNo = @receiptNo, ReceiptDate = @receiptDate, ReceiptAmt = @receiptAmt, ReceiptChequeNo = @chequeNo, ReceiptRemarks = @receiptRemarks,
        ReceiptLastUpdateDate = @today, ReceiptLastUpdateUser = @userCode, RECMBMAccPeriodMth = ISNULL(@accMonth, RECMBMAccPeriodMth), RECMBMAccPeriodYr = ISNULL(@accYear, RECMBMAccPeriodYr),
        MBMBankCharge = @bankCharge, MBMGainLoss = @gainLoss, MBMMisc = @misc, MBMDiscount = @discount, MBMAmtPaid = @amtPaid, MBMWriteOff = @writeOff, MBMMedixReceipt = @medixReceipt, MBMRemarks = NULLIF(@remarks, N'')
    WHERE ReceiptIndex = @receiptIndex;
    DECLARE @contraIndex int = (SELECT TOP (1) CONIndex FROM genisis.Pay_ContraAllocation WHERE RefNo = @oldReceiptNo AND CaseNo = @cas AND VersionNo = @ver ORDER BY CONIndex);
    IF @contraIndex IS NULL AND (NULLIF(@contraCase, N'') IS NOT NULL OR NULLIF(@contraVersion, N'') IS NOT NULL OR @contraAmt IS NOT NULL)
        INSERT genisis.Pay_ContraAllocation (RefNo, CaseNo, VersionNo, ContraCaseNo, ContraVersionNo, ContraCaseAmt) VALUES (@receiptNo, @cas, @ver, NULLIF(@contraCase, N''), NULLIF(@contraVersion, N''), @contraAmt);
    ELSE IF @contraIndex IS NOT NULL
        UPDATE genisis.Pay_ContraAllocation SET RefNo = @receiptNo, ContraCaseNo = ISNULL(NULLIF(@contraCase, N''), ContraCaseNo), ContraVersionNo = ISNULL(NULLIF(@contraVersion, N''), ContraVersionNo),
            ContraCaseAmt = ISNULL(@contraAmt, ContraCaseAmt) WHERE CONIndex = @contraIndex;
    SELECT 1 AS Updated;
END
GO

-- cmdPrintPV_Click: the lines of an official receipt (View_ReceiptFrMBMRec2): the member, the patient, the debit note and the claim of each receipt row of the receipt number.
CREATE OR ALTER PROCEDURE genisis.ReceiptMember_OfficialReceipt
    @receiptNo nvarchar(30)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(CASNumber) AS CaseNo, RTRIM(ClaimVersion) AS ClaimVersion, RTRIM(ReceiptNo) AS ReceiptNo, ReceiptDate, RTRIM(HLTCode) AS HealthType, RTRIM(ReceiptChequeNo) AS ChequeNo, ReceiptAmt,
        RTRIM(DebitCreditRefNo) AS DebitNo, RTRIM(MBMName) AS MemberName, RTRIM(PatientName) AS PatientName
    FROM genisis.Pay_ViewReceiptfrMBMRec2 WHERE ReceiptNo = @receiptNo ORDER BY CASNumber, ClaimVersion;
END
GO
