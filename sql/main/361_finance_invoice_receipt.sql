-- Finance > Invoice Receipt (Outstanding) and Notification / Suspension (legacy FrmRecMCO / frmRecMCOManual). The desktop forms in the
-- source write RECMCO / RECOthers, which have no rows since 2010; the receipts entered since then are in HISPayment.dbo.ReceiptSummary
-- (fee receipts: ReceiptType IPFEE / OPFEE, ReceiptDesc = the member MCO invoice number, sometimes "<payor>-<invoice>", RecPayCode =
-- the invoice's payor, receipt number from RecSeq exactly like the desktop's GenerateSRecNo). The screens are rebuilt on those
-- tables; nothing existing is altered.

-- The receipt number prefixes in use (RecSeq.RCInd).
CREATE OR ALTER PROCEDURE genisis.InvoiceReceipt_Lookups
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(RCInd) AS Code, RTRIM(RCInd) AS Name FROM genisis.Pay_RecSeq WHERE RTRIM(ISNULL(RCInd, N'')) <> N'' ORDER BY RCInd;
    SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name FROM dbo.PAY ORDER BY PAYCode;
END
GO

-- The member MCO invoices with what has been received against them. Received = fee receipts whose description is the invoice
-- number or ends with "-<invoice number>". @payor: invoices with a member of that payor (membership numbers start with the payor
-- code). @outstandingOnly 1: invoices with nothing received or a balance above zero. @partPaidOnly 1 (Short Payment Receipt):
-- invoices with at least one receipt and a balance still above zero.
CREATE OR ALTER PROCEDURE genisis.InvoiceReceipt_SearchInvoices
    @invoiceFrom nvarchar(40) = NULL, @invoiceTo nvarchar(40) = NULL, @dateFrom datetime = NULL, @dateTo datetime = NULL,
    @payor nvarchar(4) = NULL, @outstandingOnly bit = 1, @partPaidOnly bit = 0, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(ReceiptDesc) AS ReceiptDesc, ReceiptAmt INTO #fees
    FROM genisis.Pay_ReceiptSummary WHERE ReceiptType IN (N'IPFEE', N'OPFEE') AND RTRIM(ISNULL(ReceiptDesc, N'')) <> N'';

    SELECT RTRIM(i.MCOInvNo) AS InvoiceNo, MIN(i.MCOInvDate) AS InvoiceDate, COUNT(*) AS Members, SUM(ISNULL(i.BillMCOAmt, 0)) AS BillAmount,
        MIN(LEFT(i.MBMNumber, 2)) AS Payor
    INTO #inv
    FROM dbo.MBMMCOInvoice i
    WHERE RTRIM(ISNULL(i.MCOInvNo, N'')) <> N''
      AND (@invoiceFrom IS NULL OR i.MCOInvNo >= @invoiceFrom) AND (@invoiceTo IS NULL OR i.MCOInvNo <= @invoiceTo)
      AND (@dateFrom IS NULL OR i.MCOInvDate >= @dateFrom) AND (@dateTo IS NULL OR i.MCOInvDate <= @dateTo)
    GROUP BY i.MCOInvNo
    HAVING @payor IS NULL OR SUM(CASE WHEN LEFT(i.MBMNumber, 2) = @payor THEN 1 ELSE 0 END) > 0
    OPTION (RECOMPILE);

    SELECT TOP (@max) v.InvoiceNo, v.InvoiceDate, v.Members, v.BillAmount, v.Payor, ISNULL(r.Receipts, 0) AS Receipts,
        ISNULL(r.Received, 0) AS Received, v.BillAmount - ISNULL(r.Received, 0) AS Outstanding
    FROM #inv v
    OUTER APPLY (SELECT COUNT(*) AS Receipts, SUM(f.ReceiptAmt) AS Received FROM #fees f
                 WHERE f.ReceiptDesc = v.InvoiceNo OR f.ReceiptDesc LIKE N'%-' + v.InvoiceNo) r
    WHERE (@outstandingOnly = 0 OR r.Receipts IS NULL OR r.Receipts = 0 OR v.BillAmount - ISNULL(r.Received, 0) > 0)
      AND (@partPaidOnly = 0 OR (r.Receipts > 0 AND v.BillAmount - ISNULL(r.Received, 0) > 0))
    ORDER BY v.InvoiceDate DESC, v.InvoiceNo DESC
    OPTION (RECOMPILE);
END
GO

-- The fee receipts recorded against one invoice (same matching as above), newest first.
CREATE OR ALTER PROCEDURE genisis.InvoiceReceipt_Receipts
    @invoiceNo nvarchar(40)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ReceiptIndex AS Id, RTRIM(ReceiptNo) AS ReceiptNo, ReceiptDate, ReceiptAmt AS Amount, RTRIM(ReceiptType) AS ReceiptType,
        RTRIM(ReceiptDesc) AS Description, RTRIM(RecChqNo) AS ChequeNo, RTRIM(RecPayCode) AS Payor, RTRIM(RecBatchNo) AS BatchNo,
        CAST(CASE WHEN RecPayStatus = N'1' THEN 1 ELSE 0 END AS bit) AS Posted
    FROM genisis.Pay_ReceiptSummary
    WHERE ReceiptType IN (N'IPFEE', N'OPFEE') AND (RTRIM(ReceiptDesc) = @invoiceNo OR RTRIM(ReceiptDesc) LIKE N'%-' + @invoiceNo)
    ORDER BY ReceiptDate DESC, ReceiptIndex DESC;
END
GO

-- GenerateSRecNo(prefix): the next receipt number <prefix><5 digits> from RecSeq, locked so two users cannot take the same one.
-- Numbers already used in ReceiptSummary / ReceiptfrPAY are skipped. A prefix without a RecSeq row starts at 10001 (the desktop
-- returned "10001" without the prefix there; here the prefix is kept). Runs inside the caller's transaction.
CREATE OR ALTER PROCEDURE genisis.FinanceReceipt_NextNumber
    @prefix nvarchar(2)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @seq int, @no nvarchar(40);
    SELECT @seq = CAST(RecSeqNo AS int) FROM genisis.Pay_RecSeq WITH (UPDLOCK, HOLDLOCK) WHERE RCInd = @prefix;
    IF @seq IS NULL
    BEGIN
        SET @seq = 10001;
        INSERT INTO genisis.Pay_RecSeq (RecSeqNo, RCInd) VALUES (N'10001', @prefix);
    END
    ELSE
        SET @seq = @seq + 1;
    SET @no = @prefix + RIGHT(N'00000' + CAST(@seq AS nvarchar(10)), 5);
    WHILE EXISTS (SELECT 1 FROM genisis.Pay_ReceiptSummary WHERE ReceiptNo = @no) OR EXISTS (SELECT 1 FROM genisis.Pay_ReceiptfrPAY WHERE ReceiptNo = @no)
    BEGIN
        SET @seq = @seq + 1;
        SET @no = @prefix + RIGHT(N'00000' + CAST(@seq AS nvarchar(10)), 5);
    END
    UPDATE genisis.Pay_RecSeq SET RecSeqNo = RIGHT(N'00000' + CAST(@seq AS nvarchar(10)), 5) WHERE RCInd = @prefix;
    SELECT @no AS ReceiptNo;
END
GO

-- Saves one receipt header (unposted: no SUN batch). Refused when the receipt number is already used. Result: 0 done (with the new
-- ReceiptIndex), 2 refused.
CREATE OR ALTER PROCEDURE genisis.FinanceReceipt_Insert
    @receiptNo nvarchar(40), @receiptDate date, @amount money, @receiptType nvarchar(30), @description nvarchar(100),
    @chequeNo nvarchar(40), @payor nvarchar(4)
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM genisis.Pay_ReceiptSummary WITH (UPDLOCK, HOLDLOCK) WHERE ReceiptNo = @receiptNo)
       OR EXISTS (SELECT 1 FROM genisis.Pay_ReceiptfrPAY WHERE ReceiptNo = @receiptNo)
    BEGIN
        SELECT 2 AS Result, CAST(NULL AS int) AS Id;
        RETURN;
    END
    INSERT INTO genisis.Pay_ReceiptSummary (ReceiptNo, ReceiptDate, ReceiptAmt, ReceiptType, ReceiptDesc, RecChqNo, RecPayCode)
    VALUES (@receiptNo, @receiptDate, @amount, @receiptType, @description, @chequeNo, @payor);
    SELECT 0 AS Result, CAST(SCOPE_IDENTITY() AS int) AS Id;
END
GO
