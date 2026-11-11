-- Receipt > Receipt Tracking (legacy FrmReceiptTracking, "Receipt Tracking"): follow the receipts that came in from members, payors and MNRB. Read only. The desktop called ReceiptTracking
-- (in HISPayment) with a case number from 1 to 12 and a WHERE fragment; here there are two procedures with typed parameters over the same six views:
--   @source 'member' (VIEW_ReceiptfrMBMTracking), 'payor' (VIEW_ReceiptfrPayorTracking), 'mnrb' (VIEW_ReceiptfrMNRBTracking), each with its summary view (...Tracking2).
--   Detail ("By Claim", cases 1, 4, 7): one line per receipt line. Summary ("By Bordx" 2, 5, 8; "By Receipt No" 3, 6, 9; "By Payor" 10, 11, 12): the number of cases and the amount.
-- A NULL criterion is not applied. Text ranges compare as text, like the desktop; the member number and name are "contains" searches.

-- The detail listing. Member receipts show ReceiptAmt and ReceiptRemarks and their accounting period is RECMBMAccPeriodMth / Yr; payor and MNRB receipts show CheckAmt (what the desktop listed
-- in the amount column) and Remark, with RECAccPeriodMth / YR. The amount criterion is on ReceiptAmt in all three, as in the desktop.
CREATE OR ALTER PROCEDURE genisis.ReceiptTracking_Detail
    @source varchar(8), @health char(1) = NULL, @memberNo nvarchar(60) = NULL, @payor nvarchar(4) = NULL, @memberName nvarchar(100) = NULL,
    @caseFrom nvarchar(30) = NULL, @caseTo nvarchar(30) = NULL, @bordxFrom nvarchar(40) = NULL, @bordxTo nvarchar(40) = NULL,
    @receiptFrom nvarchar(40) = NULL, @receiptTo nvarchar(40) = NULL, @dateFrom datetime = NULL, @dateTo datetime = NULL,
    @acMonthFrom nvarchar(2) = NULL, @acMonthTo nvarchar(2) = NULL, @acYearFrom nvarchar(4) = NULL, @acYearTo nvarchar(4) = NULL,
    @debitFrom nvarchar(40) = NULL, @debitTo nvarchar(40) = NULL, @amountFrom decimal(19, 4) = NULL, @amountTo decimal(19, 4) = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    IF @source = 'member'
        SELECT TOP (@max) RTRIM(ReceiptNo) AS ReceiptNo, ReceiptDate, ReceiptAmt AS Amount, RTRIM(PAYCode) AS PayorCode, RTRIM(MBMNumber) AS MemberNo, RTRIM(MBMName) AS MemberName,
            RTRIM(CASNumber) AS CaseNo, ClaimVersion, RTRIM(BordxNo) AS BordxNo, RTRIM(DebitCreditRefNo) AS DebitNoteNo, RTRIM(ReceiptRemarks) AS Remarks,
            RTRIM(RECMBMAccPeriodMth) AS AccMonth, RTRIM(RECMBMAccPeriodYr) AS AccYear
        FROM genisis.Pay_TrackMember
        WHERE (@health IS NULL OR HLTCode = @health) AND (@memberNo IS NULL OR MBMNumber LIKE N'%' + @memberNo + N'%') AND (@payor IS NULL OR PAYCode = @payor)
          AND (@memberName IS NULL OR MBMName LIKE N'%' + @memberName + N'%') AND (@caseFrom IS NULL OR CASNumber >= @caseFrom) AND (@caseTo IS NULL OR CASNumber <= @caseTo)
          AND (@bordxFrom IS NULL OR BordxNo >= @bordxFrom) AND (@bordxTo IS NULL OR BordxNo <= @bordxTo) AND (@receiptFrom IS NULL OR ReceiptNo >= @receiptFrom) AND (@receiptTo IS NULL OR ReceiptNo <= @receiptTo)
          AND (@dateFrom IS NULL OR ReceiptDate >= @dateFrom) AND (@dateTo IS NULL OR ReceiptDate <= @dateTo)
          AND (@acMonthFrom IS NULL OR RECMBMAccPeriodMth >= @acMonthFrom) AND (@acMonthTo IS NULL OR RECMBMAccPeriodMth <= @acMonthTo)
          AND (@acYearFrom IS NULL OR RECMBMAccPeriodYr >= @acYearFrom) AND (@acYearTo IS NULL OR RECMBMAccPeriodYr <= @acYearTo)
          AND (@debitFrom IS NULL OR DebitCreditRefNo >= @debitFrom) AND (@debitTo IS NULL OR DebitCreditRefNo <= @debitTo)
          AND (@amountFrom IS NULL OR ReceiptAmt >= @amountFrom) AND (@amountTo IS NULL OR ReceiptAmt <= @amountTo)
        ORDER BY ReceiptDate, ReceiptNo, CASNumber, ClaimVersion OPTION (RECOMPILE);
    ELSE IF @source = 'payor'
        SELECT TOP (@max) RTRIM(ReceiptNo) AS ReceiptNo, ReceiptDate, CheckAmt AS Amount, RTRIM(PAYCode) AS PayorCode, RTRIM(MBMNumber) AS MemberNo, RTRIM(MBMName) AS MemberName,
            RTRIM(CASNumber) AS CaseNo, ClaimVersion, RTRIM(BordxNo) AS BordxNo, RTRIM(DebitCreditRefNo) AS DebitNoteNo, RTRIM(Remark) AS Remarks,
            RTRIM(RECAccPeriodMth) AS AccMonth, RTRIM(RECAccPeriodYR) AS AccYear
        FROM genisis.Pay_TrackPayor
        WHERE (@health IS NULL OR HLTCode = @health) AND (@memberNo IS NULL OR MBMNumber LIKE N'%' + @memberNo + N'%') AND (@payor IS NULL OR PAYCode = @payor)
          AND (@memberName IS NULL OR MBMName LIKE N'%' + @memberName + N'%') AND (@caseFrom IS NULL OR CASNumber >= @caseFrom) AND (@caseTo IS NULL OR CASNumber <= @caseTo)
          AND (@bordxFrom IS NULL OR BordxNo >= @bordxFrom) AND (@bordxTo IS NULL OR BordxNo <= @bordxTo) AND (@receiptFrom IS NULL OR ReceiptNo >= @receiptFrom) AND (@receiptTo IS NULL OR ReceiptNo <= @receiptTo)
          AND (@dateFrom IS NULL OR ReceiptDate >= @dateFrom) AND (@dateTo IS NULL OR ReceiptDate <= @dateTo)
          AND (@acMonthFrom IS NULL OR RECAccPeriodMth >= @acMonthFrom) AND (@acMonthTo IS NULL OR RECAccPeriodMth <= @acMonthTo)
          AND (@acYearFrom IS NULL OR RECAccPeriodYR >= @acYearFrom) AND (@acYearTo IS NULL OR RECAccPeriodYR <= @acYearTo)
          AND (@debitFrom IS NULL OR DebitCreditRefNo >= @debitFrom) AND (@debitTo IS NULL OR DebitCreditRefNo <= @debitTo)
          AND (@amountFrom IS NULL OR ReceiptAmt >= @amountFrom) AND (@amountTo IS NULL OR ReceiptAmt <= @amountTo)
        ORDER BY ReceiptDate, ReceiptNo, CASNumber, ClaimVersion OPTION (RECOMPILE);
    ELSE
        SELECT TOP (@max) RTRIM(ReceiptNo) AS ReceiptNo, ReceiptDate, CheckAmt AS Amount, RTRIM(PAYCode) AS PayorCode, RTRIM(MBMNumber) AS MemberNo, RTRIM(MBMName) AS MemberName,
            RTRIM(CASNumber) AS CaseNo, ClaimVersion, RTRIM(BordxNo) AS BordxNo, RTRIM(DebitCreditRefNo) AS DebitNoteNo, RTRIM(Remark) AS Remarks,
            RTRIM(RECAccPeriodMth) AS AccMonth, RTRIM(RECAccPeriodYR) AS AccYear
        FROM genisis.Pay_TrackMnrb
        WHERE (@health IS NULL OR HLTCode = @health) AND (@memberNo IS NULL OR MBMNumber LIKE N'%' + @memberNo + N'%') AND (@payor IS NULL OR PAYCode = @payor)
          AND (@memberName IS NULL OR MBMName LIKE N'%' + @memberName + N'%') AND (@caseFrom IS NULL OR CASNumber >= @caseFrom) AND (@caseTo IS NULL OR CASNumber <= @caseTo)
          AND (@bordxFrom IS NULL OR BordxNo >= @bordxFrom) AND (@bordxTo IS NULL OR BordxNo <= @bordxTo) AND (@receiptFrom IS NULL OR ReceiptNo >= @receiptFrom) AND (@receiptTo IS NULL OR ReceiptNo <= @receiptTo)
          AND (@dateFrom IS NULL OR ReceiptDate >= @dateFrom) AND (@dateTo IS NULL OR ReceiptDate <= @dateTo)
          AND (@acMonthFrom IS NULL OR RECAccPeriodMth >= @acMonthFrom) AND (@acMonthTo IS NULL OR RECAccPeriodMth <= @acMonthTo)
          AND (@acYearFrom IS NULL OR RECAccPeriodYR >= @acYearFrom) AND (@acYearTo IS NULL OR RECAccPeriodYR <= @acYearTo)
          AND (@debitFrom IS NULL OR DebitCreditRefNo >= @debitFrom) AND (@debitTo IS NULL OR DebitCreditRefNo <= @debitTo)
          AND (@amountFrom IS NULL OR ReceiptAmt >= @amountFrom) AND (@amountTo IS NULL OR ReceiptAmt <= @amountTo)
        ORDER BY ReceiptDate, ReceiptNo, CASNumber, ClaimVersion OPTION (RECOMPILE);
END
GO

-- The summaries. @by 'bordx' (By Bordx: one line per bordx), 'receipt' (By Receipt No: one line per receipt) or 'payor' (By Payor: the number of receipts and the amount of each payor). The number
-- of cases is COUNT of the bordx number, or of the receipt number, as the desktop counted. Only the criteria the summary views have apply: health type, payor, receipt date, bordx and receipt
-- number ranges.
CREATE OR ALTER PROCEDURE genisis.ReceiptTracking_Summary
    @source varchar(8), @by varchar(8), @health char(1) = NULL, @payor nvarchar(4) = NULL, @dateFrom datetime = NULL, @dateTo datetime = NULL,
    @bordxFrom nvarchar(40) = NULL, @bordxTo nvarchar(40) = NULL, @receiptFrom nvarchar(40) = NULL, @receiptTo nvarchar(40) = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    CREATE TABLE #t (HealthCode char(1) NULL, PayorCode nvarchar(4) NULL, ReceiptNo nvarchar(40) NULL, ReceiptDate datetime NULL, BordxNo nvarchar(40) NULL, BordxDate datetime NULL, Amount money NULL);
    IF @source = 'member'
        INSERT #t SELECT HLTCode, PAYCode, ReceiptNo, ReceiptDate, BordxNo, BordxDate, ReceiptMBMAmt FROM genisis.Pay_TrackMember2;
    ELSE IF @source = 'payor'
        INSERT #t SELECT HLTCode, PAYCode, ReceiptNo, ReceiptDate, BordxNo, BordxDate, ReceiptPAYAmt FROM genisis.Pay_TrackPayor2;
    ELSE
        INSERT #t SELECT HLTCode, PAYCode, ReceiptNo, ReceiptDate, BordxNo, BordxDate, ReceiptMNRBAmt FROM genisis.Pay_TrackMnrb2;

    -- CASE, not NOT (...): a receipt with no receipt number or date makes the test unknown and NOT unknown is unknown, which would keep the row.
    DELETE #t WHERE CASE WHEN ((@health IS NULL OR HealthCode = @health) AND (@payor IS NULL OR PayorCode = @payor) AND (@dateFrom IS NULL OR ReceiptDate >= @dateFrom) AND (@dateTo IS NULL OR ReceiptDate <= @dateTo)
        AND (@bordxFrom IS NULL OR BordxNo >= @bordxFrom) AND (@bordxTo IS NULL OR BordxNo <= @bordxTo) AND (@receiptFrom IS NULL OR ReceiptNo >= @receiptFrom) AND (@receiptTo IS NULL OR ReceiptNo <= @receiptTo)) THEN 1 ELSE 0 END = 0;

    IF @by = 'bordx'
        SELECT TOP (@max) RTRIM(BordxNo) AS Key1, BordxDate AS KeyDate, COUNT(BordxNo) AS Cases, SUM(Amount) AS Amount FROM #t GROUP BY BordxNo, BordxDate ORDER BY BordxNo, BordxDate;
    ELSE IF @by = 'receipt'
        SELECT TOP (@max) RTRIM(ReceiptNo) AS Key1, ReceiptDate AS KeyDate, COUNT(ReceiptNo) AS Cases, SUM(Amount) AS Amount FROM #t GROUP BY ReceiptNo, ReceiptDate ORDER BY ReceiptNo, ReceiptDate;
    ELSE
        SELECT TOP (@max) RTRIM(PayorCode) AS Key1, CAST(NULL AS datetime) AS KeyDate, COUNT(ReceiptNo) AS Cases, SUM(Amount) AS Amount FROM #t GROUP BY PayorCode ORDER BY PayorCode;
END
GO
