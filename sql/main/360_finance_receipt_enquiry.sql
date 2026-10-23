-- Finance > Receipt Enquiry (legacy frmReceiptEnquiry; its source is not available). Rebuilt from the production data: the receipt
-- header is HISPayment.dbo.ReceiptSummary (receipt no. from RecSeq, type IPCLM / OPCLM / IPFEE / OPFEE, SUN batch and posted flag);
-- claim receipts have their allocation lines in HISPayment.dbo.ReceiptfrPAY (same ReceiptNo, one line per case / version / bordx,
-- CheckAmt = amount allocated); fee receipts name the member MCO invoice (HISDB.dbo.MBMMCOInvoice.MCOInvNo) in ReceiptDesc.
-- Read only: nothing is written.

-- The receipt types in use and the payors.
CREATE OR ALTER PROCEDURE genisis.ReceiptEnquiry_Lookups
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT RTRIM(ReceiptType) AS Code, RTRIM(ReceiptType) AS Name
    FROM genisis.Pay_ReceiptSummary WHERE RTRIM(ISNULL(ReceiptType, N'')) <> N'' ORDER BY Code;
    SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name FROM dbo.PAY ORDER BY PAYCode;
END
GO

-- The receipts that match; NULL = no criterion, ranges inclusive. @posted '1': posted to SUN (RecPayStatus 1), '0': not posted.
-- @caseNo: receipts with an allocation line for that case. Each row carries its number of allocation lines and their total, and
-- ReversedBy: the receipt that reversed it (Reverse Receipt writes the reversal's description as "REV <receipt no>-...").
CREATE OR ALTER PROCEDURE genisis.ReceiptEnquiry_Search
    @receiptFrom nvarchar(40) = NULL, @receiptTo nvarchar(40) = NULL, @dateFrom date = NULL, @dateTo date = NULL,
    @type nvarchar(30) = NULL, @payor nvarchar(4) = NULL, @chequeNo nvarchar(40) = NULL, @batchNo nvarchar(30) = NULL,
    @posted char(1) = NULL, @description nvarchar(100) = NULL, @caseNo nvarchar(30) = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CASE WHEN CHARINDEX(N'-', ReceiptDesc, 5) > 5 THEN SUBSTRING(ReceiptDesc, 5, CHARINDEX(N'-', ReceiptDesc, 5) - 5) END AS OriginalNo,
        RTRIM(ReceiptNo) AS ReversalNo
    INTO #rev FROM genisis.Pay_ReceiptSummary WHERE ReceiptDesc LIKE N'REV %-%';

    SELECT TOP (@max)
        s.ReceiptIndex AS Id, RTRIM(s.ReceiptNo) AS ReceiptNo, s.ReceiptDate, s.ReceiptAmt AS Amount, RTRIM(s.ReceiptType) AS ReceiptType,
        RTRIM(s.ReceiptDesc) AS Description, RTRIM(s.RecChqNo) AS ChequeNo, RTRIM(s.RecPayCode) AS Payor, RTRIM(s.RecBatchNo) AS BatchNo,
        CAST(CASE WHEN s.RecPayStatus = N'1' THEN 1 ELSE 0 END AS bit) AS Posted, s.RecPostDate AS PostDate, RTRIM(s.RecPostUser) AS PostUser,
        ISNULL(a.Lines, 0) AS AllocationLines, a.Allocated, rv.ReversalNo AS ReversedBy
    FROM genisis.Pay_ReceiptSummary s
    OUTER APPLY (SELECT COUNT(*) AS Lines, SUM(p.CheckAmt) AS Allocated FROM genisis.Pay_ReceiptfrPAY p
                 WHERE p.ReceiptNo = s.ReceiptNo AND RTRIM(ISNULL(s.ReceiptNo, N'')) <> N'') a
    OUTER APPLY (SELECT TOP 1 r.ReversalNo FROM #rev r WHERE r.OriginalNo = RTRIM(s.ReceiptNo) AND RTRIM(ISNULL(s.ReceiptNo, N'')) <> N'') rv
    WHERE (@receiptFrom IS NULL OR s.ReceiptNo >= @receiptFrom) AND (@receiptTo IS NULL OR s.ReceiptNo <= @receiptTo)
      AND (@dateFrom IS NULL OR s.ReceiptDate >= @dateFrom) AND (@dateTo IS NULL OR s.ReceiptDate <= @dateTo)
      AND (@type IS NULL OR s.ReceiptType = @type)
      AND (@payor IS NULL OR s.RecPayCode = @payor)
      AND (@chequeNo IS NULL OR s.RecChqNo = @chequeNo)
      AND (@batchNo IS NULL OR s.RecBatchNo = @batchNo)
      AND (@posted IS NULL OR (@posted = '1' AND s.RecPayStatus = N'1') OR (@posted = '0' AND ISNULL(s.RecPayStatus, N'') <> N'1'))
      AND (@description IS NULL OR s.ReceiptDesc LIKE N'%' + @description + N'%')
      AND (@caseNo IS NULL OR EXISTS (SELECT 1 FROM genisis.Pay_ReceiptfrPAY p WHERE p.ReceiptNo = s.ReceiptNo AND p.CASNumber = @caseNo))
    ORDER BY s.ReceiptDate DESC, s.ReceiptNo DESC, s.ReceiptIndex DESC
    OPTION (RECOMPILE);
END
GO

-- One receipt: the header, its claim allocation lines, the fee invoice it names (ReceiptDesc = MCOInvNo, or the part after the last
-- '-', e.g. "IC-S33942") and its SUN posting rows (SUNPosting.TransRef = receipt no.). Four result sets; the first is empty when the
-- receipt does not exist.
CREATE OR ALTER PROCEDURE genisis.ReceiptEnquiry_Get
    @id int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @no nvarchar(40), @desc nvarchar(100);
    SELECT @no = RTRIM(ReceiptNo), @desc = RTRIM(ReceiptDesc) FROM genisis.Pay_ReceiptSummary WHERE ReceiptIndex = @id;

    SELECT s.ReceiptIndex AS Id, RTRIM(s.ReceiptNo) AS ReceiptNo, s.ReceiptDate, s.ReceiptAmt AS Amount, RTRIM(s.ReceiptType) AS ReceiptType,
        RTRIM(s.ReceiptDesc) AS Description, RTRIM(s.RecChqNo) AS ChequeNo, RTRIM(s.RecPayCode) AS Payor, RTRIM(s.RecBatchNo) AS BatchNo,
        CAST(CASE WHEN s.RecPayStatus = N'1' THEN 1 ELSE 0 END AS bit) AS Posted, s.RecPostDate AS PostDate, RTRIM(s.RecPostUser) AS PostUser,
        0 AS AllocationLines, CAST(NULL AS money) AS Allocated,
        (SELECT TOP 1 RTRIM(r.ReceiptNo) FROM genisis.Pay_ReceiptSummary r
         WHERE @no IS NOT NULL AND @no <> N'' AND r.ReceiptDesc LIKE N'REV ' + @no + N'-%' ORDER BY r.ReceiptIndex) AS ReversedBy
    FROM genisis.Pay_ReceiptSummary s WHERE s.ReceiptIndex = @id;

    SELECT p.RecPAYIndex AS Id, RTRIM(p.CASNumber) AS CaseNo, RTRIM(p.ClaimVersion) AS ClaimVersion, RTRIM(p.BordxNo) AS BordxNo,
        p.CheckAmt AS Allocated, p.RecPAYAmt AS ReceiptAmount, p.PAYBankCharge AS BankCharge, RTRIM(p.ChequeNo) AS ChequeNo,
        CASE WHEN p.RECAccPeriodMth IS NULL THEN NULL
             ELSE RIGHT(N'0' + CAST(CAST(p.RECAccPeriodMth AS int) AS nvarchar(2)), 2) + N'/' + CAST(CAST(p.RECAccPeriodYR AS int) AS nvarchar(4)) END AS AccPeriod,
        RTRIM(p.Remark) AS Remark, RTRIM(p.PAYLastUpdateUser) AS UpdatedBy, p.PAYLastUpdatedate AS UpdatedOn
    FROM genisis.Pay_ReceiptfrPAY p
    WHERE @no IS NOT NULL AND @no <> N'' AND p.ReceiptNo = @no
    ORDER BY p.CASNumber, p.ClaimVersion, p.RecPAYIndex;

    DECLARE @invoice nvarchar(40) = CASE WHEN CHARINDEX(N'-', ISNULL(@desc, N'')) > 0 THEN RIGHT(@desc, CHARINDEX(N'-', REVERSE(@desc)) - 1) ELSE @desc END;
    SELECT RTRIM(i.MCOInvNo) AS InvoiceNo, MIN(i.MCOInvDate) AS InvoiceDate, COUNT(*) AS Members, SUM(i.BillMCOAmt) AS BillAmount,
        SUM(ISNULL(i.GSTAmt, 0)) AS GstAmount
    FROM dbo.MBMMCOInvoice i
    WHERE @desc IS NOT NULL AND @desc <> N'' AND (i.MCOInvNo = @desc OR i.MCOInvNo = @invoice)
    GROUP BY i.MCOInvNo;

    SELECT RTRIM(PostType) AS PostType, RTRIM(BatchNo) AS BatchNo, PostDate, RTRIM(PostBy) AS PostBy,
        RTRIM(AccMonth) + N'/' + RTRIM(AccYear) AS AccPeriod, COUNT(*) AS Rows
    FROM genisis.WS_SUNPosting
    WHERE @no IS NOT NULL AND @no <> N'' AND TransRef = @no
    GROUP BY PostType, BatchNo, PostDate, PostBy, AccMonth, AccYear
    ORDER BY PostDate;
END
GO
