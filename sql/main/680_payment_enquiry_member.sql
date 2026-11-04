-- Payment > Enquiry > Payment To Member (legacy FrmEnquiryPayMember, "Enquiry Payment to Member"): the claims with a debit / credit note (what Medix pays the member) and their payments, the receipts of those claims and
-- the payments that were reissued. The desktop sent a WHERE fragment built as text to HISPayment.dbo.SP_Payment ('Pay2MBM', a DISTINCT over nine tables) and asked more questions for every row (the sum of a part-paid
-- claim's payments, SP_Payment 'Receipt' for its receipts). Here the criteria are typed parameters, the select keeps the columns the screen shows and the receipts of the whole list come in one call.

-- ComboListing: the payors and the health types of the drop-downs.
CREATE OR ALTER PROCEDURE genisis.PaymentEnqMbm_Lookups
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name FROM dbo.PAY ORDER BY PAYCode;
    SELECT RTRIM(HLTCode) AS Code, RTRIM(HLTDescription) AS Description FROM dbo.HLT ORDER BY HLTCode;
END
GO

-- cmdSearch_Click / SearchRecord / Payment2MBMListing and Payment2MBMReissueListing: one row per payment of a claim with a debit / credit note. @reissue = 1 lists the payments that were reissued (PVStatus R).
-- @payment: 0 all, 1 paid (F), 2 unpaid, 3 part paid (P), 4 paid and part paid, 5 unpaid and part paid. The "empty" flags list the rows with no value. Two things the desktop did are not kept: it asked for
-- PV No both as a range and as equal to the "from" box, which left only one number; and its A/C period year "from" test looked at the "to" box. The A/C period is compared as a number (it is kept as text).
-- PaidTotal is the sum of every payment of the claim and Os what is still to pay for a part-paid claim (the whole amount for an unpaid one), as the desktop worked them out.
CREATE OR ALTER PROCEDURE genisis.PaymentEnqMbm_Search
    @caseNo nvarchar(30) = NULL, @version nvarchar(4) = NULL, @wsStatus nvarchar(2) = NULL, @healthType nvarchar(2) = NULL, @mbmNo nvarchar(30) = NULL, @mbmName nvarchar(100) = NULL, @payor nvarchar(10) = NULL,
    @payment int = 0, @pvNoFrom nvarchar(40) = NULL, @pvNoTo nvarchar(40) = NULL, @blankPvNo bit = 0, @pvDateFrom datetime = NULL, @pvDateTo datetime = NULL, @blankPvDate bit = 0,
    @crNoFrom nvarchar(40) = NULL, @crNoTo nvarchar(40) = NULL, @blankCrNo bit = 0, @monthFrom int = NULL, @monthTo int = NULL, @blankMonth bit = 0, @yearFrom int = NULL, @yearTo int = NULL, @blankYear bit = 0,
    @sunNoFrom nvarchar(40) = NULL, @sunNoTo nvarchar(40) = NULL, @blankSunNo bit = 0, @sunDateFrom datetime = NULL, @sunDateTo datetime = NULL, @blankSunDate bit = 0, @reissue bit = 0, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) q.*, z.PaidTotal, z.Os
    FROM (
        SELECT DISTINCT RTRIM(c.DebitCreditRefNo) AS CrNo, RTRIM(c.CASNumber) AS CaseNo, RTRIM(c.ClaimVersion) AS ClaimVersion, c.CLMPayableByMedix2M AS Payable, RTRIM(m.MBMName) AS MemberName,
            RTRIM(c.CLMPayTo) AS PayTo, RTRIM(m.MBMNumber) AS MbmNo, p.PVMBMIndex AS PvIndex, p.MBMPaidAmt AS PaidAmt, RTRIM(x.CASARStatus) AS ArStatus, RTRIM(p.PVNo) AS PvNo, p.PVDate AS PvDate,
            RTRIM(p.PVStatus) AS PvStatus, RTRIM(p.PVPayMBMStatus) AS PayStatus, RTRIM(p.PVChequeNo) AS ChequeNo, p.PVChequeDate AS ChequeDate, p.PVChequeAmt AS ChequeAmt, RTRIM(p.PVPayto) AS PvPayTo,
            RTRIM(p.PVPayeeBank) AS PayeeBank, RTRIM(p.PVPayeeACNo) AS PayeeAcNo, RTRIM(p.Remark) AS Remark, RTRIM(c.CLMWSStatus) AS WsStatus, RTRIM(p.PVSUNNo) AS SunPvNo, p.PVSUNDate AS SunPvDate,
            p.PVBankCharges AS BankCharges, RTRIM(p.PAYAccPeriodMth) AS AccMonth, RTRIM(p.PAYAccPeriodYr) AS AccYear, RTRIM(p.Pay2MBMLastUpdateUser) AS UpdatedBy
        FROM genisis.WS_Claim c
        LEFT JOIN genisis.Pay_Payment2MBM p ON p.CASNumber = c.CASNumber AND p.ClaimVersion = c.ClaimVersion
        LEFT JOIN genisis.Maint_CASCrossReference x ON x.CASNumber = c.CASNumber
        LEFT JOIN dbo.MBM m ON m.MBMNumber = x.MBMNumber
        WHERE c.DebitCreditRefNo IS NOT NULL
          AND (@caseNo IS NULL OR c.CASNumber = @caseNo) AND (@version IS NULL OR c.ClaimVersion = @version) AND (@wsStatus IS NULL OR c.CLMWSStatus = @wsStatus)
          AND (@healthType IS NULL OR m.HLTCode = @healthType) AND (@mbmNo IS NULL OR m.MBMNumber = @mbmNo) AND (@mbmName IS NULL OR m.MBMName LIKE N'%' + @mbmName + N'%') AND (@payor IS NULL OR m.PAYCode = @payor)
          AND (@payment <> 1 OR p.PVPayMBMStatus = 'F') AND (@payment <> 2 OR p.PVPayMBMStatus = '' OR p.PVPayMBMStatus IS NULL) AND (@payment <> 3 OR p.PVPayMBMStatus = 'P')
          AND (@payment <> 4 OR p.PVPayMBMStatus IN ('P', 'F')) AND (@payment <> 5 OR p.PVPayMBMStatus = '' OR p.PVPayMBMStatus IS NULL OR p.PVPayMBMStatus = 'P')
          AND (@pvNoFrom IS NULL OR p.PVNo >= @pvNoFrom) AND (@pvNoTo IS NULL OR p.PVNo <= @pvNoTo) AND (@blankPvNo = 0 OR p.PVNo IS NULL OR p.PVNo = '')
          AND (@pvDateFrom IS NULL OR p.PVDate >= @pvDateFrom) AND (@pvDateTo IS NULL OR p.PVDate <= @pvDateTo) AND (@blankPvDate = 0 OR p.PVDate IS NULL)
          AND (@crNoFrom IS NULL OR c.DebitCreditRefNo >= @crNoFrom) AND (@crNoTo IS NULL OR c.DebitCreditRefNo <= @crNoTo) AND (@blankCrNo = 0 OR c.DebitCreditRefNo = '')
          AND (@monthFrom IS NULL OR CASE WHEN ISNUMERIC(p.PAYAccPeriodMth) = 1 THEN CAST(p.PAYAccPeriodMth AS int) END >= @monthFrom)
          AND (@monthTo IS NULL OR CASE WHEN ISNUMERIC(p.PAYAccPeriodMth) = 1 THEN CAST(p.PAYAccPeriodMth AS int) END <= @monthTo) AND (@blankMonth = 0 OR p.PAYAccPeriodMth IS NULL)
          AND (@yearFrom IS NULL OR CASE WHEN ISNUMERIC(p.PAYAccPeriodYr) = 1 THEN CAST(p.PAYAccPeriodYr AS int) END >= @yearFrom)
          AND (@yearTo IS NULL OR CASE WHEN ISNUMERIC(p.PAYAccPeriodYr) = 1 THEN CAST(p.PAYAccPeriodYr AS int) END <= @yearTo) AND (@blankYear = 0 OR p.PAYAccPeriodYr IS NULL)
          AND (@sunNoFrom IS NULL OR p.PVSUNNo >= @sunNoFrom) AND (@sunNoTo IS NULL OR p.PVSUNNo <= @sunNoTo) AND (@blankSunNo = 0 OR p.PVSUNNo IS NULL OR p.PVSUNNo = '')
          AND (@sunDateFrom IS NULL OR p.PVSUNDate >= @sunDateFrom) AND (@sunDateTo IS NULL OR p.PVSUNDate <= @sunDateTo) AND (@blankSunDate = 0 OR p.PVSUNDate IS NULL)
          AND (@reissue = 0 OR p.PVStatus = 'R')
    ) q
    OUTER APPLY (SELECT SUM(a.MBMPaidAmt) AS PaidTotal FROM genisis.Pay_Payment2MBM a WHERE a.CASNumber = q.CaseNo AND a.ClaimVersion = q.ClaimVersion) t
    CROSS APPLY (SELECT CASE WHEN ISNULL(q.PayStatus, '') = 'P' THEN t.PaidTotal END AS PaidTotal,
                        CASE WHEN ISNULL(q.PayStatus, '') = 'P' THEN q.Payable - t.PaidTotal WHEN ISNULL(q.PayStatus, '') = '' THEN q.Payable END AS Os) z
    ORDER BY q.CaseNo, q.ClaimVersion, q.PvIndex
    OPTION (RECOMPILE);
END
GO

-- SelectReceipt / ReceivedFrMBMListing: the receipts of the claims listed (<l><i>case|version</i>...</l>), one line for each receipt a claim has from MNRB, from the payor and from the member (the three
-- receipts of a VIEW_Receipt row become three lines). Payable is what Medix pays the member for the claim.
CREATE OR ALTER PROCEDURE genisis.PaymentEnqMbm_Receipts
    @claims xml
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @k TABLE (CaseNo nvarchar(30), Version nvarchar(4), PRIMARY KEY (CaseNo, Version));
    INSERT @k (CaseNo, Version)
    SELECT DISTINCT LEFT(v, CHARINDEX(N'|', v) - 1), SUBSTRING(v, CHARINDEX(N'|', v) + 1, 4)
    FROM (SELECT RTRIM(i.value('.', 'nvarchar(100)')) AS v FROM @claims.nodes('/l/i') t (i)) s WHERE CHARINDEX(N'|', v) > 1;

    SELECT RTRIM(r.CASNumber) AS CaseNo, RTRIM(r.ClaimVersion) AS ClaimVersion, RTRIM(m.MBMNumber) AS MbmNo, r.Kind AS Kind, c.CLMPayableByMedix2M AS Payable, r.Amount AS Amount, RTRIM(r.ReceiptNo) AS ReceiptNo,
        r.ReceiptDate AS ReceiptDate, RTRIM(r.AccMonth) AS AccMonth, RTRIM(r.AccYear) AS AccYear, RTRIM(r.Remarks) AS Remarks
    FROM (
        SELECT v.CASNumber, v.ClaimVersion, N'MNRB' AS Kind, ISNULL(v.CheckAmtMNRB, 0) AS Amount, v.ReceiptNoMNRB AS ReceiptNo, v.ReceiptDateMNRB AS ReceiptDate, v.ReceiptAccMthMNRB AS AccMonth, v.ReceiptAccYRMNRB AS AccYear, v.RemarkMNRB AS Remarks
        FROM genisis.Pay_ViewReceipt v WHERE RTRIM(ISNULL(v.ReceiptNoMNRB, N'')) <> N''
        UNION ALL
        SELECT v.CASNumber, v.ClaimVersion, N'PAY', ISNULL(v.CheckAmtPAY, 0), v.ReceiptNoPAY, v.ReceiptDatePAY, v.ReceiptAccMthPAY, v.ReceiptAccYRPAY, v.RemarkPAY
        FROM genisis.Pay_ViewReceipt v WHERE RTRIM(ISNULL(v.ReceiptNoPAY, N'')) <> N''
        UNION ALL
        SELECT v.CASNumber, v.ClaimVersion, N'MBM', ISNULL(v.ReceiptAmtMBM, 0), v.ReceiptNoMBM, v.ReceiptDateMBM, v.ReceiptAccMthMBM, v.ReceiptMBMAccYrMBM, v.ReceiptRemarksMBM
        FROM genisis.Pay_ViewReceipt v WHERE RTRIM(ISNULL(v.ReceiptNoMBM, N'')) <> N''
    ) r
    INNER JOIN @k k ON k.CaseNo = r.CASNumber AND k.Version = r.ClaimVersion
    LEFT JOIN genisis.WS_Claim c ON c.CASNumber = r.CASNumber AND c.ClaimVersion = r.ClaimVersion
    LEFT JOIN genisis.Maint_CASCrossReference x ON x.CASNumber = r.CASNumber
    LEFT JOIN dbo.MBM m ON m.MBMNumber = x.MBMNumber
    ORDER BY r.CASNumber, r.ClaimVersion, r.Kind, r.ReceiptNo;
END
GO
