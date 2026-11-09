-- Payment > To Hospital (legacy FrmPaymentHospital, "Payment To Hospital"; its source is not in the source tree, so the screen is rebuilt on what it read and what it left in HISPayment.Payment2HOS).
-- What the desktop read: SP_Payment 'Pay2Hosp' over VIEW_Payment2HOSNew (the claims the hospital is paid for: CLMPayableByMedix2H <> 0, a member on CASCrossReference, payors MH, EQ, ET, EM,
-- EC, ES, EP and MY left out). What it wrote, read from the 24,600 rows of Payment2HOS:
--   * one payment voucher (PV) per hospital and payment run, one Payment2HOS row per claim (never two rows of a claim on one PV); INVNo is always empty;
--   * the PV number is the payor's PAY.PVInd (CB, CC or CG: 24,388 of 24,393 PVs) and the next number of its counter (CB: PVNSeq2013, CC: PVMedicaSeq2013, CG: PVMedicaMXSeq), 8 digits;
--   * PVHOSChequeAmt is the PV total on every row, HOSPaidAmt the claim's amount, PVHOSDate = PVHOSChequeDate, HOSAccPeriodMth / Yr the month (2 digits) and year of the PV date;
--   * PVPayHOSStatus F, PVStatus A, the charges, gain / loss, discount, excess and Medix payment 0, PVPayee the hospital's payee (HOS.HOSPayee);
--   * PVPymtMode ONLINE (the cheque number is the PV date as ddMMyyyy and PVChequeStatus Y) or CHEQUE (a real cheque number, PVChequeStatus empty);
--   * PVAmtWording / PVAmtWordingTwo the amount in words (written by the API: CurrencyConversion.bas).

-- The next PV number of a prefix. Shared by the Payment screens that write vouchers (To Hospital, Payor, Others, To Member). The counter row is locked until the transaction ends, so two people wait for
-- each other; a number already on Payment2HOS, Payment2MBM, Payment2PAY or PaymentOthers is skipped (the counters are shared by the forms). Result 0 and the number, or 1: the prefix has no counter.
CREATE OR ALTER PROCEDURE genisis.Payment_NextPv
    @prefix nvarchar(2)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @seq int, @pv nvarchar(30);
    IF @prefix = N'CB' SELECT @seq = CAST(PVSeqNo AS int) FROM genisis.Pay_PVNSeq2013 WITH (UPDLOCK, HOLDLOCK);
    ELSE IF @prefix = N'CC' SELECT @seq = CAST(PVSeqNo AS int) FROM genisis.Pay_PVMedicaSeq2013 WITH (UPDLOCK, HOLDLOCK);
    ELSE IF @prefix = N'CG' SELECT @seq = CAST(PVSeqNo AS int) FROM genisis.Pay_PVMedicaMXSeq WITH (UPDLOCK, HOLDLOCK);
    IF @seq IS NULL
    BEGIN
        SELECT 1 AS Result, CAST(NULL AS nvarchar(30)) AS PvNo;
        RETURN;
    END

    SET @seq = @seq + 1;
    SET @pv = @prefix + RIGHT(N'00000000' + CAST(@seq AS nvarchar(10)), 8);
    WHILE EXISTS (SELECT 1 FROM genisis.Pay_Payment2HOS WHERE RTRIM(PVHOSNo) = @pv) OR EXISTS (SELECT 1 FROM genisis.Pay_Payment2MBM WHERE RTRIM(PVNo) = @pv)
       OR EXISTS (SELECT 1 FROM genisis.Pay_Payment2PAY WHERE RTRIM(PVPAYNo) = @pv) OR EXISTS (SELECT 1 FROM genisis.Pay_PaymentOthers WHERE RTRIM(PVNumber) = @pv)
    BEGIN
        SET @seq = @seq + 1;
        SET @pv = @prefix + RIGHT(N'00000000' + CAST(@seq AS nvarchar(10)), 8);
    END

    IF @prefix = N'CB' UPDATE genisis.Pay_PVNSeq2013 SET PVSeqNo = RIGHT(N'00000000' + CAST(@seq AS nvarchar(10)), 8);
    ELSE IF @prefix = N'CC' UPDATE genisis.Pay_PVMedicaSeq2013 SET PVSeqNo = RIGHT(N'00000000' + CAST(@seq AS nvarchar(10)), 8);
    ELSE UPDATE genisis.Pay_PVMedicaMXSeq SET PVSeqNo = RIGHT(N'00000000' + CAST(@seq AS nvarchar(10)), 8);
    SELECT 0 AS Result, @pv AS PvNo;
END
GO

-- The claims not yet paid to their hospital: one line per claim (the view has a line per invoice; the claim's amount is CLMPayableByMedix2H, the hospital the invoices' payee). @caseNo alone finds one claim,
-- paid or not (the Create check). PayPending is CASCrossReference.CASPayPending (Y: the claim's payment is held); PvInd the payor's PV prefix.
CREATE OR ALTER PROCEDURE genisis.PaymentToHospital_Search
    @payor nvarchar(4) = NULL, @hospital nvarchar(100) = NULL, @caseFrom nvarchar(30) = NULL, @caseTo nvarchar(30) = NULL, @bordxFrom nvarchar(40) = NULL, @bordxTo nvarchar(40) = NULL,
    @bordxDateFrom datetime = NULL, @bordxDateTo datetime = NULL, @caseNo nvarchar(30) = NULL, @version nvarchar(100) = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(v.CASNumber) AS CaseNo, RTRIM(v.ClaimVersion) AS ClaimVersion, RTRIM(MIN(v.MBMNumber)) AS MemberNo, RTRIM(MIN(v.INVPayee)) AS HospitalCode,
        RTRIM(MIN(v.HOSName)) AS HospitalName, RTRIM(MIN(v.HOSPayee)) AS Payee, RTRIM(MIN(v.BordxRefNo)) AS BordxNo, MIN(v.BordxDate) AS BordxDate, RTRIM(MIN(v.PAYCode)) AS PayorCode,
        RTRIM(MIN(p.PVInd)) AS PvInd, MIN(CAST(v.CLMPayableByMedix2H AS money)) AS Amount, COUNT(*) AS Invoices, MIN(v.InvDate) AS InvDate,
        RTRIM(MAX(v.CASPayPending)) AS PayPending, RTRIM(MAX(CAST(v.caspymtpdingremark AS nvarchar(400)))) AS PendingRemark, RTRIM(MAX(v.PVHOSNo)) AS PvNo
    FROM genisis.Pay_Payment2HOSNew v
    LEFT JOIN dbo.PAY p ON p.PAYCode = v.PAYCode
    WHERE (@caseNo IS NOT NULL AND v.CASNumber = @caseNo AND v.ClaimVersion = @version)
       OR (@caseNo IS NULL AND v.PVHOSNo IS NULL
           AND (@payor IS NULL OR v.PAYCode = @payor) AND (@hospital IS NULL OR v.HOSName LIKE N'%' + @hospital + N'%' OR v.INVPayee = @hospital)
           AND (@caseFrom IS NULL OR v.CASNumber >= @caseFrom) AND (@caseTo IS NULL OR v.CASNumber <= @caseTo)
           AND (@bordxFrom IS NULL OR v.BordxRefNo >= @bordxFrom) AND (@bordxTo IS NULL OR v.BordxRefNo <= @bordxTo)
           AND (@bordxDateFrom IS NULL OR v.BordxDate >= @bordxDateFrom) AND (@bordxDateTo IS NULL OR v.BordxDate <= @bordxDateTo))
    GROUP BY v.CASNumber, v.ClaimVersion
    ORDER BY MIN(v.HOSName), v.CASNumber, v.ClaimVersion
    OPTION (RECOMPILE);
END
GO

-- Locks a claim's payment rows until the transaction ends and says whether it is paid already (Result 2) or not (0), so two people cannot pay one claim twice.
CREATE OR ALTER PROCEDURE genisis.PaymentToHospital_Lock
    @caseNo nvarchar(30), @version nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM genisis.Pay_Payment2HOS WITH (UPDLOCK, HOLDLOCK) WHERE CASNumber = @caseNo AND ClaimVersion = @version)
        SELECT 2 AS Result;
    ELSE
        SELECT 0 AS Result;
END
GO

-- One Payment2HOS row: a claim on a PV. The API computes the PV total and its words; the accounting period is the PV date's month and year.
CREATE OR ALTER PROCEDURE genisis.PaymentToHospital_Insert
    @pvNo nvarchar(30), @pvDate datetime, @total money, @chequeNo nvarchar(200), @caseNo nvarchar(30), @version nvarchar(100), @amount money, @remark nvarchar(400),
    @payee nvarchar(400), @wording nvarchar(600), @wordingTwo nvarchar(100), @paymentMode nvarchar(20), @userCode nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @day datetime = CONVERT(datetime, CONVERT(char(8), @pvDate, 112));
    INSERT INTO genisis.Pay_Payment2HOS (PVHOSNo, PVHOSDate, PVHOSChequeDate, PVHOSChequeAmt, PVHOSChequeNo, CASNumber, ClaimVersion, INVNo, HOSRemark, Pay2HOSLastUpdateDate,
        Pay2HOSLastUpdateUser, HOSPaidAmt, HOSAccPeriodMth, HOSAccPeriodYr, PVPayHOSStatus, PVBankCharges, PVHOSClmGainLoss, PVHOSMiscDiff, PVHOSDiscount, PVHOSCaseRemark,
        PVStatus, PVExcess, PVMedixPayment, PVPayee, PVAmtWording, PVAmtWordingTwo, PVChequeStatus, PVPymtMode)
    VALUES (@pvNo, @day, @day, @total, @chequeNo, @caseNo, @version, NULL, ISNULL(@remark, N''), CONVERT(datetime, CONVERT(char(8), GETDATE(), 112)),
        @userCode, @amount, RIGHT(N'0' + CAST(MONTH(@day) AS nvarchar(2)), 2), CAST(YEAR(@day) AS nvarchar(4)), 'F', 0, 0, 0, 0, N'',
        'A', 0, 0, @payee, @wording, @wordingTwo, CASE WHEN @paymentMode = N'ONLINE' THEN 'Y' ELSE NULL END, @paymentMode);
    SELECT CAST(SCOPE_IDENTITY() AS int) AS Id;
END
GO
