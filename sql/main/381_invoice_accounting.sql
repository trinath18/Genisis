-- Invoice > Invoice HOS Accounting (legacy FrmInvoiceHOSAC) and Invoice MBM Accounting (legacy FrmInvoiceMBMAC). Both list invoices that were not yet handed to
-- accounts (print status 0) or were (1, "reprint"), and "Print to File" gives the ones listed a batch (H or M, the day, a number of the day) and marks them printed.
-- The desktop built the WHERE clause as text; here every criterion is a typed parameter. Differences, all of which were read out of the desktop's code:
--  * The batch date criteria compared the text column INVBatchDate ("H23102026": H, ddmmyyyy) with a typed date as text, which cannot work; the date part of the
--    batch is compared as a date here.
--  * The hospital criterion compared the hospital's name with its hyphens turned into blanks, so a hospital with a hyphen was never found; the hospital's code is used.
--  * The hospital print marked every invoice of the worksheet version, whoever it was paid to (the member print only the member's); only the hospital's here.
--  * The hospital print kept the first digit of the batch number (Mid(batch, 11, 1)); the whole number is kept (the member print kept three digits).

-- The day a batch label stands for: "H23102026" or "M23102026" is 23 October 2026 (the desktop's batches have always been named this way); NULL for anything else.
-- HISDB runs at compatibility level 100, so no TRY_CONVERT: the date is built as yyyymmdd and tested with ISDATE.
CREATE OR ALTER FUNCTION genisis.InvoiceBatchDay (@batchDate nvarchar(100))
RETURNS date
AS
BEGIN
    DECLARE @iso varchar(8) = SUBSTRING(@batchDate, 6, 4) + SUBSTRING(@batchDate, 4, 2) + SUBSTRING(@batchDate, 2, 2);
    RETURN CASE WHEN LEN(@batchDate) = 9 AND @iso NOT LIKE '%[^0-9]%' AND ISDATE(@iso) = 1 THEN CONVERT(date, @iso, 112) END;
END
GO

-- GenerateINVHSeqNo / GenerateINVMSeqNo: the next batch of a kind for a day, "H" or "M" then ddmmyyyy, a hyphen and the number of the day (the first batch of a day is 1).
CREATE OR ALTER PROCEDURE genisis.InvoiceAccounting_NextBatch
    @kind char(1), @day date
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @n int;
    IF @kind = 'H'
    BEGIN
        UPDATE genisis.WS_INVACHSEQ WITH (UPDLOCK, HOLDLOCK) SET @n = InvACBatch = InvACBatch + 1 WHERE CAST(InvACDate AS date) = @day;
        IF @@ROWCOUNT = 0
        BEGIN
            INSERT genisis.WS_INVACHSEQ (InvACDate, InvACBatch) VALUES (@day, 1);
            SET @n = 1;
        END
    END
    ELSE
    BEGIN
        UPDATE genisis.WS_INVACMSEQ WITH (UPDLOCK, HOLDLOCK) SET @n = InvACBatch = InvACBatch + 1 WHERE CAST(InvACDate AS date) = @day;
        IF @@ROWCOUNT = 0
        BEGIN
            INSERT genisis.WS_INVACMSEQ (InvACDate, InvACBatch) VALUES (@day, 1);
            SET @n = 1;
        END
    END
    SELECT CAST(@kind AS varchar(1)) + REPLACE(CONVERT(varchar(10), @day, 103), '/', '') + '-' + CAST(@n AS varchar(10));
END
GO

-- SearchRecord + InvoiceAccountListing of FrmInvoiceHOSAC: the invoices paid to a hospital. @reprint = 0: not printed yet; 1: printed. Every "from"/"to" pair
-- is inclusive and ignored when NULL.
CREATE OR ALTER PROCEDURE genisis.InvoiceHospitalAccounting_Search
    @reprint bit, @invFrom datetime = NULL, @invTo datetime = NULL, @regFrom datetime = NULL, @regTo datetime = NULL, @hospital nvarchar(100) = NULL,
    @batchDateFrom date = NULL, @batchDateTo date = NULL, @batchNoFrom int = NULL, @batchNoTo int = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(INVPayee) AS HospitalCode, RTRIM(HOSName) AS Hospital, RTRIM(InvNo) AS InvoiceNo, NULLIF(InvDate, '19000101') AS InvoiceDate,
           NULLIF(INVEnteredDate, '19000101') AS RegisteredDate, RTRIM(CASNumber) AS CaseNo, RTRIM(INVWSversion) AS Version, RTRIM(BordxRefNo) AS BordxNo,
           RTRIM(MBMNumber) AS MemberNo, RTRIM(MBMName) AS MemberName, RTRIM(PatientName) AS PatientName, CLMPayableByMedix2H AS Payable, InvPreviousAmt AS PreviousAmount,
           RTRIM(DebitCreditRefNo) AS DebitCreditRefNo, RTRIM(INVBatchDate) AS BatchDate, INVBatchNo AS BatchNo
    FROM genisis.WS_HOSInvoice
    WHERE INVPayTo = 'H'
      AND InvPrintStatus = CASE WHEN @reprint = 1 THEN '1' ELSE '0' END
      AND (@invFrom IS NULL OR InvDate >= @invFrom) AND (@invTo IS NULL OR InvDate < DATEADD(DAY, 1, @invTo))
      AND (@regFrom IS NULL OR INVEnteredDate >= @regFrom) AND (@regTo IS NULL OR INVEnteredDate < DATEADD(DAY, 1, @regTo))
      AND (@hospital IS NULL OR INVPayee = @hospital)
      AND (@batchDateFrom IS NULL OR genisis.InvoiceBatchDay(INVBatchDate) >= @batchDateFrom)
      AND (@batchDateTo IS NULL OR genisis.InvoiceBatchDay(INVBatchDate) <= @batchDateTo)
      AND (@batchNoFrom IS NULL OR INVBatchNo >= @batchNoFrom) AND (@batchNoTo IS NULL OR INVBatchNo <= @batchNoTo)
    ORDER BY INVPayee, InvDate, InvNo, CASNumber, INVWSversion;
END
GO

-- UpdatePrintRecord of FrmInvoiceHOSAC: the batch, the user, the printed mark and the amount payable (as the previous amount) on the hospital's invoices of every
-- worksheet version the criteria list. Returns how many invoices were marked.
CREATE OR ALTER PROCEDURE genisis.InvoiceHospitalAccounting_Print
    @batch nvarchar(20), @user nvarchar(20), @invFrom datetime = NULL, @invTo datetime = NULL, @regFrom datetime = NULL, @regTo datetime = NULL, @hospital nvarchar(100) = NULL,
    @batchDateFrom date = NULL, @batchDateTo date = NULL, @batchNoFrom int = NULL, @batchNoTo int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE i
    SET INVBatchDate = LEFT(@batch, 9), INVBatchNo = CASE WHEN SUBSTRING(@batch, 11, 10) NOT LIKE '%[^0-9]%' AND LEN(SUBSTRING(@batch, 11, 10)) > 0 THEN CAST(SUBSTRING(@batch, 11, 10) AS int) END, InvPrintLogon = @user, InvPrintStatus = '1', InvPreviousAmt = ISNULL(v.CLMPayableByMedix2H, 0)
    FROM genisis.WS_ClaimInvoice i
    JOIN (SELECT DISTINCT CASNumber, INVWSversion, CLMPayableByMedix2H FROM genisis.WS_HOSInvoice
          WHERE INVPayTo = 'H' AND InvPrintStatus = '0'
            AND (@invFrom IS NULL OR InvDate >= @invFrom) AND (@invTo IS NULL OR InvDate < DATEADD(DAY, 1, @invTo))
            AND (@regFrom IS NULL OR INVEnteredDate >= @regFrom) AND (@regTo IS NULL OR INVEnteredDate < DATEADD(DAY, 1, @regTo))
            AND (@hospital IS NULL OR INVPayee = @hospital)
            AND (@batchDateFrom IS NULL OR genisis.InvoiceBatchDay(INVBatchDate) >= @batchDateFrom)
            AND (@batchDateTo IS NULL OR genisis.InvoiceBatchDay(INVBatchDate) <= @batchDateTo)
            AND (@batchNoFrom IS NULL OR INVBatchNo >= @batchNoFrom) AND (@batchNoTo IS NULL OR INVBatchNo <= @batchNoTo)) v
      ON v.CASNumber = i.CASNumber AND v.INVWSversion = i.INVWSversion
    WHERE i.INVPayTo = 'H';
    SELECT @@ROWCOUNT;
END
GO

-- SearchRecord + InvoiceAccountListing of FrmInvoiceMBMAC: the worksheets with an amount payable to the member, by invoice, by the worksheet's close date, the payor (the first
-- two letters of the case number), the print status, the batch and the bordx date. Like the desktop it also lists the hospital-paid invoices of such worksheets (94 of
-- 14,681 rows; its first column says who each invoice was paid to), although its print marks only the member-paid ones.
CREATE OR ALTER PROCEDURE genisis.InvoiceMemberAccounting_Search
    @reprint bit, @closeFrom datetime = NULL, @closeTo datetime = NULL, @payor nvarchar(2) = NULL, @batchDateFrom date = NULL, @batchDateTo date = NULL,
    @batchNoFrom int = NULL, @batchNoTo int = NULL, @bordxFrom datetime = NULL, @bordxTo datetime = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(INVPayee) AS PayTo, RTRIM(INVPayTo) AS PayToCode, RTRIM(CASNumber) AS CaseNo, RTRIM(INVWSversion) AS Version, NULLIF(ClaimCloseDate, '19000101') AS ClaimCloseDate,
           RTRIM(DebitCreditRefNo) AS DebitCreditRefNo, RTRIM(BordxRefNo) AS BordxNo, RTRIM(MBMNumber) AS MemberNo, RTRIM(MBMName) AS MemberName,
           RTRIM(PatientName) AS PatientName, CLMPayableByMedix2M AS Payable, InvPreviousAmt AS PreviousAmount, NULLIF(BordxDate, '19000101') AS BordxDate,
           RTRIM(INVBatchDate) AS BatchDate, INVBatchNo AS BatchNo, RTRIM(PLNCode) AS PlanCode, CLMTotalActual AS TotalActual
    FROM genisis.WS_MBMInvoice1
    WHERE InvPrintStatus = CASE WHEN @reprint = 1 THEN '1' ELSE '0' END
      AND (@closeFrom IS NULL OR ClaimCloseDate >= @closeFrom) AND (@closeTo IS NULL OR ClaimCloseDate < DATEADD(DAY, 1, @closeTo))
      AND (@payor IS NULL OR LEFT(CASNumber, 2) = @payor)
      AND (@batchDateFrom IS NULL OR genisis.InvoiceBatchDay(INVBatchDate) >= @batchDateFrom)
      AND (@batchDateTo IS NULL OR genisis.InvoiceBatchDay(INVBatchDate) <= @batchDateTo)
      AND (@batchNoFrom IS NULL OR INVBatchNo >= @batchNoFrom) AND (@batchNoTo IS NULL OR INVBatchNo <= @batchNoTo)
      AND (@bordxFrom IS NULL OR BordxDate >= @bordxFrom) AND (@bordxTo IS NULL OR BordxDate < DATEADD(DAY, 1, @bordxTo))
    ORDER BY CASNumber, INVWSversion;
END
GO

-- UpdatePrintRecord of FrmInvoiceMBMAC: the batch, the user, the printed mark and the amount payable to the member on the member's invoices of every worksheet
-- version the criteria list. Returns how many invoices were marked.
CREATE OR ALTER PROCEDURE genisis.InvoiceMemberAccounting_Print
    @batch nvarchar(20), @user nvarchar(20), @closeFrom datetime = NULL, @closeTo datetime = NULL, @payor nvarchar(2) = NULL, @batchDateFrom date = NULL, @batchDateTo date = NULL,
    @batchNoFrom int = NULL, @batchNoTo int = NULL, @bordxFrom datetime = NULL, @bordxTo datetime = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE i
    SET INVBatchDate = LEFT(@batch, 9), INVBatchNo = CASE WHEN SUBSTRING(@batch, 11, 3) NOT LIKE '%[^0-9]%' AND LEN(SUBSTRING(@batch, 11, 3)) > 0 THEN CAST(SUBSTRING(@batch, 11, 3) AS int) END, InvPrintLogon = @user, InvPrintStatus = '1', InvPreviousAmt = ISNULL(v.CLMPayableByMedix2M, 0)
    FROM genisis.WS_ClaimInvoice i
    JOIN (SELECT DISTINCT CASNumber, INVWSversion, CLMPayableByMedix2M FROM genisis.WS_MBMInvoice1
          WHERE InvPrintStatus = '0'
            AND (@closeFrom IS NULL OR ClaimCloseDate >= @closeFrom) AND (@closeTo IS NULL OR ClaimCloseDate < DATEADD(DAY, 1, @closeTo))
            AND (@payor IS NULL OR LEFT(CASNumber, 2) = @payor)
            AND (@batchDateFrom IS NULL OR genisis.InvoiceBatchDay(INVBatchDate) >= @batchDateFrom)
            AND (@batchDateTo IS NULL OR genisis.InvoiceBatchDay(INVBatchDate) <= @batchDateTo)
            AND (@batchNoFrom IS NULL OR INVBatchNo >= @batchNoFrom) AND (@batchNoTo IS NULL OR INVBatchNo <= @batchNoTo)
            AND (@bordxFrom IS NULL OR BordxDate >= @bordxFrom) AND (@bordxTo IS NULL OR BordxDate < DATEADD(DAY, 1, @bordxTo))) v
      ON v.CASNumber = i.CASNumber AND v.INVWSversion = i.INVWSversion
    WHERE i.INVPayTo = 'M';
    SELECT @@ROWCOUNT;
END
GO

-- UpdateRecord of FrmInvoiceMBMAC (cmdUpdate_Click, "For Update A/C Period"): the accounting month and year of the member-paid invoices of a worksheet version, and who
-- set them and when. The list the ticks come from has no invoice number (its view has none), and the desktop built the ticked rows' keys from the wrong list columns, so its
-- update could not find them; like the print, the update works on the worksheet version. Returns how many invoices were changed.
CREATE OR ALTER PROCEDURE genisis.InvoiceMemberAccounting_SetPeriod
    @cas nvarchar(30), @version nvarchar(4), @month int, @year int, @user nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.WS_ClaimInvoice
    SET AccPeriodMonth = @month, AccPeriodYear = @year, LastAccPeriodUser = @user, LastAccPeriodDate = CAST(GETDATE() AS date)
    WHERE CASNumber = @cas AND INVWSversion = @version AND INVPayTo = 'M';
    SELECT @@ROWCOUNT;
END
GO
