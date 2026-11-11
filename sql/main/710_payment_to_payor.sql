-- Payment > Payor (legacy FrmPaymentPayor, "Payment To Payor"; its source is not in the source tree, so the screen is rebuilt on the view it read and the rows it left in HISPayment.Payment2PAY).
-- The claims paid back to their payor are the reversed claims: VIEW_Payment_Payor, the claims whose CLMTotalApproved is below 0. The 552 rows of Payment2PAY (2007 to 2009) show:
--   * one PV per payor and run, one row per claim; PVPAYto is the payor code (the first two letters of the case number); PVPAYChequeAmt is the PV total on every row (552 of 552), PAYPaidAmt the claim's;
--   * PVPAYDate = PVPAYChequeDate, PAYAccPeriodMth / Yr the month (2 digits) and year of the PV date, PVPayPAYStatus F, PVStatus A, PVBankCharges 0 (the gain / loss, discount, difference, excess and Medix payment empty), a cheque number on every row, the remarks empty.
-- The amount paid back is taken as the reversed amount (-CLMTotalApproved): an assumption, since the claims of the rows written are not in this copy of HISWorkSheet.
-- PV numbers come from genisis.Payment_NextPv (700_payment_to_hospital.sql); the 2007-2009 rows have the older 5-digit numbers (CB58259).

-- The reversed claims not yet paid back: one line per claim. @caseNo alone finds one claim, paid or not.
CREATE OR ALTER PROCEDURE genisis.PaymentToPayor_Search
    @payor nvarchar(4) = NULL, @caseFrom nvarchar(30) = NULL, @caseTo nvarchar(30) = NULL, @bordxFrom nvarchar(40) = NULL, @bordxTo nvarchar(40) = NULL,
    @closeFrom datetime = NULL, @closeTo datetime = NULL, @caseNo nvarchar(30) = NULL, @version nvarchar(100) = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(v.CASNumber) AS CaseNo, RTRIM(v.ClaimVersion) AS ClaimVersion, RTRIM(MIN(v.MBMNumber)) AS MemberNo, RTRIM(MIN(v.MBMName)) AS MemberName,
        LEFT(v.CASNumber, 2) AS PayorCode, RTRIM(MIN(p.PAYCompanyName)) AS PayorName, RTRIM(MIN(p.PVInd)) AS PvInd, RTRIM(MIN(v.BordxRefNo)) AS BordxNo, MIN(v.ClaimCloseDate) AS CloseDate,
        -MIN(CAST(v.CLMTotalApproved AS money)) AS Amount, RTRIM(MAX(v.PVPAYNo)) AS PvNo
    FROM genisis.Pay_ViewPaymentPayor v
    LEFT JOIN dbo.PAY p ON p.PAYCode = LEFT(v.CASNumber, 2)
    WHERE (@caseNo IS NOT NULL AND v.CASNumber = @caseNo AND v.ClaimVersion = @version)
       OR (@caseNo IS NULL AND v.PVPAYNo IS NULL
           AND (@payor IS NULL OR LEFT(v.CASNumber, 2) = @payor)
           AND (@caseFrom IS NULL OR v.CASNumber >= @caseFrom) AND (@caseTo IS NULL OR v.CASNumber <= @caseTo)
           AND (@bordxFrom IS NULL OR v.BordxRefNo >= @bordxFrom) AND (@bordxTo IS NULL OR v.BordxRefNo <= @bordxTo)
           AND (@closeFrom IS NULL OR v.ClaimCloseDate >= @closeFrom) AND (@closeTo IS NULL OR v.ClaimCloseDate < DATEADD(day, 1, @closeTo)))
    GROUP BY v.CASNumber, v.ClaimVersion
    ORDER BY LEFT(v.CASNumber, 2), v.CASNumber, v.ClaimVersion
    OPTION (RECOMPILE);
END
GO

-- Locks a claim's rows of Payment2PAY until the transaction ends: Result 2 when it is paid back already, 0 when not.
CREATE OR ALTER PROCEDURE genisis.PaymentToPayor_Lock
    @caseNo nvarchar(30), @version nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM genisis.Pay_Payment2PAY WITH (UPDLOCK, HOLDLOCK) WHERE CASNumber = @caseNo AND ClaimVersion = @version)
        SELECT 2 AS Result;
    ELSE
        SELECT 0 AS Result;
END
GO

-- One Payment2PAY row: a claim on a PV.
CREATE OR ALTER PROCEDURE genisis.PaymentToPayor_Insert
    @pvNo nvarchar(30), @pvDate datetime, @payTo nvarchar(100), @total money, @chequeNo nvarchar(40), @caseNo nvarchar(30), @version nvarchar(100), @amount money,
    @remark nvarchar(400), @userCode nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @day datetime = CONVERT(datetime, CONVERT(char(8), @pvDate, 112));
    INSERT INTO genisis.Pay_Payment2PAY (PVPAYNo, PVPAYDate, PVPAYto, PVPAYChequeDate, PVPAYChequeAmt, PVPAYChequeNo, CASNumber, ClaimVersion, PAYRemark, Pay2PAYLastUpdateDate,
        Pay2PAYLastUpdateUser, PAYPaidAmt, PAYAccPeriodMth, PAYAccPeriodYr, PVPayPAYStatus, PVBankCharges, PVPAYCaseRemark,
        PVStatus)
    VALUES (@pvNo, @day, @payTo, @day, @total, @chequeNo, @caseNo, @version, ISNULL(@remark, N''), CONVERT(datetime, CONVERT(char(8), GETDATE(), 112)),
        LEFT(@userCode, 20), @amount, RIGHT(N'0' + CAST(MONTH(@day) AS nvarchar(2)), 2), CAST(YEAR(@day) AS nvarchar(4)), 'F', 0, N'', 'A');
    SELECT CAST(SCOPE_IDENTITY() AS int) AS Id;
END
GO
