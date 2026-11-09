-- Payment > To Member > Pay2MBM and Pay2MBM(Auto) (legacy FrmPayMember and frmPayMemberJul08; their sources are not in the source tree, so the screens are rebuilt on the views they read and the rows they
-- left in HISPayment.Payment2MBM).
--   Pay2MBM read VIEW_Payment2MBMNew (SP_Payment 'Pay2MBM'): the claims with a debit / credit note (DebitCreditRefNo), payors MH and AX left out unless the plan pays members (PLNPymtInd Y). It lists
--   claims with nothing payable too, and those were closed with a VOID row: 6,542 rows with PVNo and PVChequeNo VOID, MBMPaidAmt 0, no status, the month of the date unpadded.
--   Pay2MBM(Auto) read VIEW_Payment2MBMJul08: the claims with an amount payable to the member (CLMPayableByMedix2M <> 0), with the payor's receipt (ReceiptfrPAY) and the claim's pay mode.
-- What they wrote, read from the 636 member PVs:
--   * one PV per claim (618 of 631 PVs); PVChequeAmt = MBMPaidAmt = CLMPayableByMedix2M; the PV prefix is the payor's PAY.PVInd (634 of 636), numbered by genisis.Payment_NextPv;
--   * PVChequeDate = PVDate, PAYAccPeriodMth / Yr the month (2 digits) and year of the PV date, PVPayMBMStatus F, PVStatus A, PVBankCharges 0, PVGainLoss / Discount / MiscDiff / MedixPayment 0;
--   * PVPayto, PVPayeeBank, PVPayeeACNo the claim's payee, bank and account (Claim.CLMPayTo, CLMBank, CLMAccountNo; the member's name when there is no payee);
--   * PVPymtMode ONLINE (the cheque number is the PV date as ddMMyyyy, PVChequeStatus Y) or CHEQUE; PVAmtWording / PVAmtWordingTwo the amount in words (written by the API).

-- The claims not yet paid to (or voided for) the member: one line per claim. @source 'dn' (Pay2MBM) or 'auto' (Pay2MBM(Auto)). @caseNo alone finds one claim of the source, paid or not.
CREATE OR ALTER PROCEDURE genisis.PaymentToMember_Search
    @source varchar(4) = 'dn', @payor nvarchar(4) = NULL, @memberNo nvarchar(60) = NULL, @memberName nvarchar(100) = NULL, @caseFrom nvarchar(30) = NULL, @caseTo nvarchar(30) = NULL,
    @noteFrom nvarchar(40) = NULL, @noteTo nvarchar(40) = NULL, @closeFrom datetime = NULL, @closeTo datetime = NULL, @caseNo nvarchar(30) = NULL, @version nvarchar(100) = NULL,
    @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    CREATE TABLE #c (CaseNo nvarchar(30) NOT NULL, ClaimVersion nvarchar(100) NOT NULL, MemberNo nvarchar(60) NULL, MemberName nvarchar(200) NULL, PatientName nvarchar(200) NULL,
        DebitNoteNo nvarchar(40) NULL, Amount money NULL, PayTo nvarchar(200) NULL, Bank nvarchar(200) NULL, Account nvarchar(100) NULL, PayMode nvarchar(40) NULL,
        ReceiptNo nvarchar(60) NULL, ReceiptDate datetime NULL, PayPending nvarchar(10) NULL, BordxNo nvarchar(40) NULL, CloseDate datetime NULL, PvNo nvarchar(30) NULL);

    IF @source = 'auto'
        INSERT #c SELECT CASNumber, CAST(ClaimVersion AS nvarchar(100)), MBMNumber, MBMName, MBMCName, DebitCreditRefNo, ClmPayAmt, CLMPayTo, CLMBank, CLMAccountNo, CLMPayMode,
            ReceiptNo, ReceiptDate, CASPayPending, BordxRefNo, ClaimCloseDate, PVNo
        FROM genisis.Pay_ViewPayment2MBMAuto
        WHERE CASNumber IS NOT NULL AND ((@caseNo IS NOT NULL AND CASNumber = @caseNo AND CAST(ClaimVersion AS nvarchar(100)) = @version) OR (@caseNo IS NULL AND PVNo IS NULL));
    ELSE
        INSERT #c SELECT CASNumber, CAST(ClaimVersion AS nvarchar(100)), MBMNumber, MBMName, MBMCName, DebitCreditRefNo, CLMPayableByMedix2M, CLMPayTo, CLMBank, CLMAccountNo, NULL,
            NULL, NULL, CASPayPending, BordxRefNo, ClaimCloseDate, PVNo
        FROM genisis.Pay_ViewPayment2MBMNew
        WHERE CASNumber IS NOT NULL AND ((@caseNo IS NOT NULL AND CASNumber = @caseNo AND CAST(ClaimVersion AS nvarchar(100)) = @version) OR (@caseNo IS NULL AND PVNo IS NULL));

    SELECT TOP (@max) RTRIM(c.CaseNo) AS CaseNo, RTRIM(c.ClaimVersion) AS ClaimVersion, RTRIM(MIN(c.MemberNo)) AS MemberNo, RTRIM(MIN(c.MemberName)) AS MemberName,
        RTRIM(MIN(c.PatientName)) AS PatientName, LEFT(c.CaseNo, 2) AS PayorCode, RTRIM(MIN(p.PVInd)) AS PvInd, RTRIM(MIN(c.DebitNoteNo)) AS DebitNoteNo, MIN(c.Amount) AS Amount,
        RTRIM(ISNULL(NULLIF(LTRIM(MIN(c.PayTo)), N''), MIN(c.MemberName))) AS PayTo, RTRIM(MIN(c.Bank)) AS Bank, RTRIM(MIN(c.Account)) AS Account, RTRIM(MIN(c.PayMode)) AS PayMode,
        RTRIM(MAX(c.ReceiptNo)) AS ReceiptNo, MAX(c.ReceiptDate) AS ReceiptDate, RTRIM(MAX(c.PayPending)) AS PayPending, RTRIM(MIN(c.BordxNo)) AS BordxNo, MIN(c.CloseDate) AS CloseDate,
        RTRIM(MAX(c.PvNo)) AS PvNo
    FROM #c c
    LEFT JOIN dbo.PAY p ON p.PAYCode = LEFT(c.CaseNo, 2)
    WHERE @caseNo IS NOT NULL
       OR ((@payor IS NULL OR LEFT(c.CaseNo, 2) = @payor) AND (@memberNo IS NULL OR c.MemberNo LIKE N'%' + @memberNo + N'%')
           AND (@memberName IS NULL OR c.MemberName LIKE N'%' + @memberName + N'%') AND (@caseFrom IS NULL OR c.CaseNo >= @caseFrom) AND (@caseTo IS NULL OR c.CaseNo <= @caseTo)
           AND (@noteFrom IS NULL OR c.DebitNoteNo >= @noteFrom) AND (@noteTo IS NULL OR c.DebitNoteNo <= @noteTo)
           AND (@closeFrom IS NULL OR c.CloseDate >= @closeFrom) AND (@closeTo IS NULL OR c.CloseDate < DATEADD(day, 1, @closeTo)))
    GROUP BY c.CaseNo, c.ClaimVersion
    ORDER BY c.CaseNo, c.ClaimVersion;
END
GO

-- Locks a claim's rows of Payment2MBM until the transaction ends: Result 2 when it has a row already (a PV or a VOID), 0 when not.
CREATE OR ALTER PROCEDURE genisis.PaymentToMember_Lock
    @caseNo nvarchar(30), @version nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM genisis.Pay_Payment2MBM WITH (UPDLOCK, HOLDLOCK) WHERE CASNumber = @caseNo AND ClaimVersion = @version)
        SELECT 2 AS Result;
    ELSE
        SELECT 0 AS Result;
END
GO

-- One Payment2MBM row: a claim on a PV (the columns of Insert_Payment2mbm).
CREATE OR ALTER PROCEDURE genisis.PaymentToMember_Insert
    @pvNo nvarchar(30), @pvDate datetime, @total money, @chequeNo nvarchar(40), @payTo nvarchar(120), @bank nvarchar(200), @account nvarchar(60), @caseNo nvarchar(30), @version nvarchar(100),
    @amount money, @remark nvarchar(400), @wording nvarchar(600), @wordingTwo nvarchar(100), @paymentMode nvarchar(20), @userCode nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @day datetime = CONVERT(datetime, CONVERT(char(8), @pvDate, 112));
    INSERT INTO genisis.Pay_Payment2MBM (CASNumber, ClaimVersion, PVNo, PVDate, PVStatus, PVChequeDate, PVChequeNo, PVChequeAmt, PVPayto, PVPayeeBank, PVPayeeACNo, Remark, MBMPaidAmt,
        Pay2MBMLastUpdateDate, Pay2MBMLastUpdateUser, PAYAccPeriodMth, PAYAccPeriodYr, PVPayMBMStatus, PVBankCharges, PVGainLoss, PVMiscDiff, PVDiscount, PVMedixPayment, PVCaseRemark,
        PVAmtWording, PVAmtWordingTwo, PVChequeStatus, PVPymtMode)
    VALUES (@caseNo, @version, @pvNo, @day, 'A', @day, @chequeNo, @total, @payTo, @bank, @account, ISNULL(@remark, N''), @amount,
        CONVERT(datetime, CONVERT(char(8), GETDATE(), 112)), LEFT(@userCode, 20), RIGHT(N'0' + CAST(MONTH(@day) AS nvarchar(2)), 2), CAST(YEAR(@day) AS nvarchar(4)), 'F', 0, 0, 0, 0, 0, N'',
        @wording, @wordingTwo, CASE WHEN @paymentMode = N'ONLINE' THEN 'Y' ELSE NULL END, @paymentMode);
    SELECT CAST(SCOPE_IDENTITY() AS int) AS Id;
END
GO

-- Void: closes a claim with nothing to pay the member, as the desktop did (PVNo and PVChequeNo VOID, MBMPaidAmt 0, no status, the month unpadded).
CREATE OR ALTER PROCEDURE genisis.PaymentToMember_Void
    @caseNo nvarchar(30), @version nvarchar(100), @voidDate datetime, @remark nvarchar(400), @userCode nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @day datetime = CONVERT(datetime, CONVERT(char(8), @voidDate, 112));
    INSERT INTO genisis.Pay_Payment2MBM (CASNumber, ClaimVersion, PVNo, PVDate, PVChequeNo, Remark, MBMPaidAmt, Pay2MBMLastUpdateDate, Pay2MBMLastUpdateUser, PAYAccPeriodMth, PAYAccPeriodYr)
    VALUES (@caseNo, @version, N'VOID', @day, N'VOID', NULLIF(@remark, N''), 0, CONVERT(datetime, CONVERT(char(8), GETDATE(), 112)), LEFT(@userCode, 20), CAST(MONTH(@day) AS nvarchar(2)),
        CAST(YEAR(@day) AS nvarchar(4)));
    SELECT CAST(SCOPE_IDENTITY() AS int) AS Id;
END
GO
