-- Receipt > Others (legacy FrmReceiptHOS, form name FrmReceiptOther, "Receipt (Others)"): the receipts of HISPayment.ReceiptFrHOS, money received from a hospital (or from "OTHER") with its cheque, bank charge and
-- accounting period. The desktop read them through VIEW_ReceiptFrOthers, which shows the hospital's name but not its code (the column HOSName holds the code), and wrote them with recordsets; here the rows are read
-- with their hospital code and name, and each change is a typed procedure.

-- ComboListing / SearchHOS: the hospitals.
CREATE OR ALTER PROCEDURE genisis.ReceiptOthers_Hospitals
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(HOSCode) AS Code, RTRIM(HOSName) AS Name FROM dbo.HOS ORDER BY HOSName, HOSCode;
END
GO

-- cmdSearch_Click / SearchRecord: the receipts. A blank criterion is no criterion; the "empty" flags list the receipts with no value. The desktop found a receipt number by testing the "from" box twice (it ignored
-- the "to" box); both boxes are used here.
CREATE OR ALTER PROCEDURE genisis.ReceiptOthers_Search
    @hospital nvarchar(10) = NULL, @noFrom nvarchar(30) = NULL, @noTo nvarchar(30) = NULL, @blankNo bit = 0, @dateFrom datetime = NULL, @dateTo datetime = NULL, @blankDate bit = 0,
    @monthFrom int = NULL, @monthTo int = NULL, @blankMonth bit = 0, @yearFrom int = NULL, @yearTo int = NULL, @blankYear bit = 0, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) r.ReceiptIndex AS Id, RTRIM(r.HOSName) AS HospitalCode, RTRIM(h.HOSName) AS HospitalName, RTRIM(r.ReceiptNo) AS ReceiptNo, r.ReceiptDate, r.ReceiptAmt AS Amount,
        r.HOSBankCharge AS BankCharge, RTRIM(r.ReceiptChequeNo) AS ChequeNo, RTRIM(r.ReceiptRemarks) AS Remarks, CAST(r.RECMBMAccPeriodMth AS int) AS AccMonth, CAST(r.RECMBMAccPeriodYr AS int) AS AccYear,
        RTRIM(r.ReceiptLastUpdateUser) AS UpdatedBy, r.ReceiptLastUpdateDate AS UpdatedOn
    FROM genisis.Pay_ReceiptfrHOS r
    LEFT JOIN dbo.HOS h ON h.HOSCode = r.HOSName
    WHERE (@hospital IS NULL OR r.HOSName = @hospital)
      AND (@noFrom IS NULL OR r.ReceiptNo >= @noFrom) AND (@noTo IS NULL OR r.ReceiptNo <= @noTo)
      AND (@blankNo = 0 OR r.ReceiptNo IS NULL OR r.ReceiptNo = '')
      AND (@dateFrom IS NULL OR r.ReceiptDate >= @dateFrom) AND (@dateTo IS NULL OR r.ReceiptDate <= @dateTo)
      AND (@blankDate = 0 OR r.ReceiptDate IS NULL)
      AND (@monthFrom IS NULL OR r.RECMBMAccPeriodMth >= @monthFrom) AND (@monthTo IS NULL OR r.RECMBMAccPeriodMth <= @monthTo)
      AND (@blankMonth = 0 OR r.RECMBMAccPeriodMth IS NULL)
      AND (@yearFrom IS NULL OR r.RECMBMAccPeriodYr >= @yearFrom) AND (@yearTo IS NULL OR r.RECMBMAccPeriodYr <= @yearTo)
      AND (@blankYear = 0 OR r.RECMBMAccPeriodYr IS NULL)
    ORDER BY r.ReceiptIndex;
END
GO

-- cmdSave_Click: adds a receipt and stamps the user and the day. Returns the new ReceiptIndex.
CREATE OR ALTER PROCEDURE genisis.ReceiptOthers_Add
    @hospital nvarchar(10), @receiptNo nvarchar(30), @receiptDate datetime, @amount money, @bankCharge money = NULL, @chequeNo nvarchar(40), @month int = NULL, @year int = NULL,
    @remarks nvarchar(100), @userCode nvarchar(20), @today datetime
AS
BEGIN
    SET NOCOUNT ON;
    INSERT genisis.Pay_ReceiptfrHOS (HOSName, ReceiptNo, ReceiptDate, ReceiptAmt, HOSBankCharge, ReceiptChequeNo, RECMBMAccPeriodMth, RECMBMAccPeriodYr, ReceiptRemarks, ReceiptLastUpdateUser, ReceiptLastUpdateDate)
    VALUES (@hospital, @receiptNo, @receiptDate, @amount, @bankCharge, @chequeNo, @month, @year, @remarks, @userCode, @today);
    SELECT CAST(SCOPE_IDENTITY() AS int) AS Id;
END
GO

-- cmdUpdate_Click: changes a receipt (the desktop added a new one when the row it had opened was gone; here that is "not found"). Returns the number of rows changed.
CREATE OR ALTER PROCEDURE genisis.ReceiptOthers_Update
    @id int, @hospital nvarchar(10), @receiptNo nvarchar(30), @receiptDate datetime, @amount money, @bankCharge money = NULL, @chequeNo nvarchar(40), @month int = NULL, @year int = NULL,
    @remarks nvarchar(100), @userCode nvarchar(20), @today datetime
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.Pay_ReceiptfrHOS
    SET HOSName = @hospital, ReceiptNo = @receiptNo, ReceiptDate = @receiptDate, ReceiptAmt = @amount, HOSBankCharge = @bankCharge, ReceiptChequeNo = @chequeNo,
        RECMBMAccPeriodMth = @month, RECMBMAccPeriodYr = @year, ReceiptRemarks = @remarks, ReceiptLastUpdateUser = @userCode, ReceiptLastUpdateDate = @today
    WHERE ReceiptIndex = @id;
    SELECT @@ROWCOUNT AS Changed;
END
GO

-- CmdDelete_Click: deletes a receipt. Returns the number of rows removed.
CREATE OR ALTER PROCEDURE genisis.ReceiptOthers_Delete
    @id int
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM genisis.Pay_ReceiptfrHOS WHERE ReceiptIndex = @id;
    SELECT @@ROWCOUNT AS Removed;
END
GO
