-- Payment > Others (legacy FrmPaymentOthers, "Payment (Others)"; its source is not in the source tree and HISPayment.PaymentOthers has no row, no view and no procedure that reads it, so the screen is
-- rebuilt on the table alone): a payment voucher to any payee, not tied to a hospital's or a member's claim (a case number may be given), one row per PV, paid by cheque.
-- The columns are filled as the other payment tables fill theirs: PaidAmt = PVChequeAmt (one row per PV), AccPeriodMth / Yr the month (2 digits) and year of the PV date, PVStatus A, the gain / loss,
-- discount and difference 0. PV numbers come from genisis.Payment_NextPv (700_payment_to_hospital.sql).

CREATE OR ALTER PROCEDURE genisis.PaymentOthers_Search
    @pvFrom nvarchar(30) = NULL, @pvTo nvarchar(30) = NULL, @dateFrom datetime = NULL, @dateTo datetime = NULL, @payTo nvarchar(120) = NULL, @caseNo nvarchar(30) = NULL,
    @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) PVOthIndex AS Id, RTRIM(PVNumber) AS PvNo, PVDate, RTRIM(PVPayTo) AS PayTo, PVChequeAmt AS Amount, RTRIM(PVChequeNo) AS ChequeNo, PVChequeDate AS ChequeDate,
        RTRIM(PVPayeeBank) AS PayeeBank, RTRIM(PVPayeeACNo) AS PayeeAccount, RTRIM(CASNumber) AS CaseNo, RTRIM(PVRemark) AS Remark, BankCharges, RTRIM(AccPeriodMth) AS AccMonth,
        RTRIM(AccPeriodYr) AS AccYear, PVStatus AS Status, RTRIM(LastUpdateUser) AS UpdatedBy, LastUpdateDate AS UpdatedOn
    FROM genisis.Pay_PaymentOthers
    WHERE (@pvFrom IS NULL OR PVNumber >= @pvFrom) AND (@pvTo IS NULL OR PVNumber <= @pvTo)
      AND (@dateFrom IS NULL OR PVDate >= @dateFrom) AND (@dateTo IS NULL OR PVDate < DATEADD(day, 1, @dateTo))
      AND (@payTo IS NULL OR PVPayTo LIKE N'%' + @payTo + N'%') AND (@caseNo IS NULL OR CASNumber = @caseNo)
    ORDER BY PVDate DESC, PVNumber DESC
    OPTION (RECOMPILE);
END
GO

CREATE OR ALTER PROCEDURE genisis.PaymentOthers_Insert
    @pvNo nvarchar(30), @pvDate datetime, @payTo nvarchar(120), @amount money, @chequeNo nvarchar(40), @chequeDate datetime, @payeeBank nvarchar(200), @payeeAccount nvarchar(60),
    @caseNo nvarchar(30), @remark nvarchar(400), @bankCharges money, @userCode nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @day datetime = CONVERT(datetime, CONVERT(char(8), @pvDate, 112));
    INSERT INTO genisis.Pay_PaymentOthers (CASNumber, PVNumber, PVDate, PVChequeNo, PVChequeAmt, PVChequeDate, PVPayTo, PVPayeeBank, PVPayeeACNo, PVRemark, PaidAmt, AccPeriodMth,
        AccPeriodYr, BankCharges, GainLoss, Discount, MiscDiff, CASRemark, LastUpdateUser, LastUpdateDate, PVStatus)
    VALUES (@caseNo, @pvNo, @day, @chequeNo, @amount, CONVERT(datetime, CONVERT(char(8), ISNULL(@chequeDate, @day), 112)), @payTo, @payeeBank, @payeeAccount, ISNULL(@remark, N''),
        @amount, RIGHT(N'0' + CAST(MONTH(@day) AS nvarchar(2)), 2), CAST(YEAR(@day) AS nvarchar(4)), ISNULL(@bankCharges, 0), 0, 0, 0, N'', LEFT(@userCode, 20), GETDATE(), 'A');
    SELECT CAST(SCOPE_IDENTITY() AS int) AS Id;
END
GO
