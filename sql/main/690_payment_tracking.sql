-- Payment > Payment Tracking (legacy FrmPaymentTracking, "Payment Tracking"; its source is not in the source tree, so the screen is rebuilt on what the desktop called). Read only.
-- The desktop called HISPayment.dbo.PaymentTracking with a case number from 1 to 17 and a WHERE fragment (@strAppend); here one procedure with typed parameters reads the same two views:
--   @source 'hospital': VIEW_PaymentHos_Tracking (Payment2HOS, the payments to hospitals),  cases 1 to 8;
--   @source 'member':   VIEW_Payment_Tracking    (Payment2MBM, the payments to members), cases 9 to 16.
-- @by: 'claim' the detail (cases 1, 9), 'bordx' (2, 10), 'case' (3, 11: the amount paid for each claim), 'hospitalDate' (4, 12), 'member' (5, 13), 'payorDate' (6, 14), 'pv' (7, 15), 'hospital' (8, 16).
-- Case 17 (invoices of a hospital payment) selected INVBordxRefNo, InvNo and InvAmount, which VIEW_PaymentHos_Tracking does not have: it could not run and is not reproduced.
-- Every criterion applies to every layout, as the desktop appended the same fragment to all of them. A NULL criterion is not applied; text ranges compare as text; names are "contains".

CREATE OR ALTER PROCEDURE genisis.PaymentTracking_Search
    @source varchar(8), @by varchar(12) = 'claim', @health char(1) = NULL, @payor nvarchar(4) = NULL, @memberNo nvarchar(60) = NULL, @memberName nvarchar(100) = NULL,
    @hospital nvarchar(100) = NULL, @status nvarchar(2) = NULL, @caseFrom nvarchar(30) = NULL, @caseTo nvarchar(30) = NULL, @bordxFrom nvarchar(40) = NULL, @bordxTo nvarchar(40) = NULL,
    @pvFrom nvarchar(30) = NULL, @pvTo nvarchar(30) = NULL, @dateFrom datetime = NULL, @dateTo datetime = NULL, @acMonthFrom nvarchar(2) = NULL, @acMonthTo nvarchar(2) = NULL,
    @acYearFrom nvarchar(4) = NULL, @acYearTo nvarchar(4) = NULL, @debitFrom nvarchar(40) = NULL, @debitTo nvarchar(40) = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    CREATE TABLE #t (MemberNo nvarchar(60) NULL, MemberName nvarchar(200) NULL, HospitalName nvarchar(200) NULL, Status nvarchar(2) NULL, PayorCode nvarchar(10) NULL, HealthCode nvarchar(2) NULL,
        BordxNo nvarchar(40) NULL, BordxDate datetime NULL, PvNo nvarchar(30) NULL, PvDate datetime NULL, Amount money NULL, CaseNo nvarchar(30) NULL, ClaimVersion nvarchar(100) NULL,
        DebitNoteNo nvarchar(40) NULL, PayTo nvarchar(200) NULL, Remark nvarchar(400) NULL, AccMonth nvarchar(32) NULL, AccYear nvarchar(32) NULL);

    IF @source = 'hospital'
        INSERT #t SELECT MBMNumber, MBMName, HOSName, RTRIM(PVPayHOSStatus), PAYCode, HLTCode, BordxRefNo, BordxDate, PVNo, PVDate, PVPaidAmt, CASNumber, ClaimVersion,
            DebitCreditRefNo, CAST(NULL AS nvarchar(200)), Remark, AccPeriodMth, AccPeriodYr
        FROM genisis.Pay_TrackHospital;
    ELSE
        INSERT #t SELECT MBMNumber, MBMName, HOSName, RTRIM(PVPayMBMStatus), PAYCode, HLTCode, BordxRefNo, BordxDate, PVNo, PVDate, PVPaidAmt, CASNumber, ClaimVersion,
            DebitCreditRefNo, PVPayto, Remark, AccPeriodMth, AccPeriodYr
        FROM genisis.Pay_TrackPaymentMember;

    -- CASE, not NOT (...): a row with a NULL column makes a test unknown, and NOT unknown would keep it.
    DELETE #t WHERE CASE WHEN ((@health IS NULL OR HealthCode = @health) AND (@payor IS NULL OR PayorCode = @payor)
        AND (@memberNo IS NULL OR MemberNo LIKE N'%' + @memberNo + N'%') AND (@memberName IS NULL OR MemberName LIKE N'%' + @memberName + N'%')
        AND (@hospital IS NULL OR HospitalName LIKE N'%' + @hospital + N'%') AND (@status IS NULL OR Status = @status)
        AND (@caseFrom IS NULL OR CaseNo >= @caseFrom) AND (@caseTo IS NULL OR CaseNo <= @caseTo)
        AND (@bordxFrom IS NULL OR BordxNo >= @bordxFrom) AND (@bordxTo IS NULL OR BordxNo <= @bordxTo)
        AND (@pvFrom IS NULL OR PvNo >= @pvFrom) AND (@pvTo IS NULL OR PvNo <= @pvTo)
        AND (@dateFrom IS NULL OR PvDate >= @dateFrom) AND (@dateTo IS NULL OR PvDate <= @dateTo)
        AND (@acMonthFrom IS NULL OR AccMonth >= @acMonthFrom) AND (@acMonthTo IS NULL OR AccMonth <= @acMonthTo)
        AND (@acYearFrom IS NULL OR AccYear >= @acYearFrom) AND (@acYearTo IS NULL OR AccYear <= @acYearTo)
        AND (@debitFrom IS NULL OR DebitNoteNo >= @debitFrom) AND (@debitTo IS NULL OR DebitNoteNo <= @debitTo)) THEN 1 ELSE 0 END = 0;

    IF @by = 'claim'
        SELECT TOP (@max) RTRIM(PvNo) AS PvNo, PvDate, Amount, RTRIM(CaseNo) AS CaseNo, RTRIM(ClaimVersion) AS ClaimVersion, RTRIM(MemberNo) AS MemberNo, RTRIM(MemberName) AS MemberName,
            RTRIM(HospitalName) AS HospitalName, RTRIM(PayorCode) AS PayorCode, RTRIM(BordxNo) AS BordxNo, RTRIM(DebitNoteNo) AS DebitNoteNo, RTRIM(PayTo) AS PayTo, Status,
            RTRIM(Remark) AS Remark, RTRIM(AccMonth) AS AccMonth, RTRIM(AccYear) AS AccYear
        FROM #t ORDER BY PvDate, PvNo, CaseNo, ClaimVersion;
    ELSE
        -- Key1 / KeyDate are the grouping columns; Cases is COUNT of the grouping column, as the desktop counted (a NULL key counts 0).
        SELECT TOP (@max) Key1, KeyDate, Cases, Amount FROM (
            SELECT RTRIM(BordxNo) AS Key1, BordxDate AS KeyDate, COUNT(BordxNo) AS Cases, SUM(Amount) AS Amount FROM #t WHERE @by = 'bordx' GROUP BY BordxNo, BordxDate
            UNION ALL SELECT RTRIM(CaseNo) + N'-' + RTRIM(ISNULL(ClaimVersion, N'')), NULL, COUNT(CaseNo), SUM(Amount) FROM #t WHERE @by = 'case' GROUP BY CaseNo, ClaimVersion
            UNION ALL SELECT RTRIM(HospitalName), PvDate, COUNT(HospitalName), SUM(Amount) FROM #t WHERE @by = 'hospitalDate' GROUP BY HospitalName, PvDate
            UNION ALL SELECT RTRIM(MemberNo), NULL, COUNT(MemberNo), SUM(Amount) FROM #t WHERE @by = 'member' GROUP BY MemberNo
            UNION ALL SELECT RTRIM(PayorCode), PvDate, COUNT(PayorCode), SUM(Amount) FROM #t WHERE @by = 'payorDate' GROUP BY PayorCode, PvDate
            UNION ALL SELECT RTRIM(PvNo), PvDate, COUNT(PvNo), SUM(Amount) FROM #t WHERE @by = 'pv' GROUP BY PvNo, PvDate
            UNION ALL SELECT RTRIM(HospitalName), NULL, COUNT(HospitalName), SUM(Amount) FROM #t WHERE @by = 'hospital' GROUP BY HospitalName
        ) s ORDER BY Key1, KeyDate;
END
GO
