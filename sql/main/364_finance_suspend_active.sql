-- Finance > Suspend / Active (legacy frmPaymentMBMReissue; its source is not available). Rebuilt on the member payment vouchers in
-- HISPayment.dbo.Payment2MBM and the status codes frmPostingPay2MBM lists: A Active, C Cancel, E Expired, R Reissue. The data shows:
--   * stopping a payment (cancel / expired cheque) adds a row with the SAME PV no., the negative amount, the original cheque no. and
--     cheque date, the action date as PV date, status C or E and cheque status Y; the original row stays A;
--   * re-issuing adds a row with a NEW PV no. of the same prefix (CB: PVNSeq2013, CC: PVMedicaSeq2013, CG: PVMedicaMXSeq), the positive
--     amount, the new cheque no. / transfer reference, the action date and status R.
-- Nothing is updated or deleted; SUN posting (frmPostingPay2MBM) picks the C / E / R rows up as before.

-- The vouchers that match; NULL = no criterion. Each row says whether it has been stopped (the C / E row of its PV no.) and the
-- reissue that replaced it (an R row of the same case / version added after the stop).
CREATE OR ALTER PROCEDURE genisis.SuspendActive_Search
    @pvFrom nvarchar(30) = NULL, @pvTo nvarchar(30) = NULL, @dateFrom datetime = NULL, @dateTo datetime = NULL,
    @caseNo nvarchar(30) = NULL, @chequeNo nvarchar(40) = NULL, @payee nvarchar(120) = NULL, @status char(1) = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max)
        p.PVMBMIndex AS Id, RTRIM(p.PVNo) AS PvNo, p.PVDate, p.PVChequeAmt AS Amount, RTRIM(p.PVChequeNo) AS ChequeNo, p.PVChequeDate AS ChequeDate,
        RTRIM(p.PVStatus) AS Status, RTRIM(p.PVChequeStatus) AS ChequeStatus, RTRIM(p.PVPymtMode) AS PaymentMode, RTRIM(p.PVPayto) AS PayTo,
        RTRIM(p.PVPayeeBank) AS PayeeBank,
        CASE WHEN LEN(RTRIM(ISNULL(p.PVPayeeACNo, N''))) > 4 THEN N'****' + RIGHT(RTRIM(p.PVPayeeACNo), 4) ELSE RTRIM(p.PVPayeeACNo) END AS PayeeAccount,
        RTRIM(p.CASNumber) AS CaseNo, RTRIM(p.ClaimVersion) AS ClaimVersion, RTRIM(p.Remark) AS Remark,
        RTRIM(p.Pay2MBMLastUpdateUser) AS UpdatedBy, p.Pay2MBMLastUpdateDate AS UpdatedOn,
        st.StopStatus AS StoppedAs, st.StopDate AS StoppedOn, ri.PvNo AS ReissuedAs
    FROM genisis.Pay_Payment2MBM p
    OUTER APPLY (SELECT TOP 1 RTRIM(s.PVStatus) AS StopStatus, s.PVDate AS StopDate, s.PVMBMIndex AS StopId FROM genisis.Pay_Payment2MBM s
                 WHERE RTRIM(s.PVNo) = RTRIM(p.PVNo) AND s.PVStatus IN ('C', 'E') AND s.PVMBMIndex <> p.PVMBMIndex
                 ORDER BY s.PVMBMIndex) st
    OUTER APPLY (SELECT TOP 1 RTRIM(r.PVNo) AS PvNo FROM genisis.Pay_Payment2MBM r
                 WHERE st.StopId IS NOT NULL AND r.CASNumber = p.CASNumber AND r.ClaimVersion = p.ClaimVersion AND r.PVStatus = 'R'
                   AND r.PVMBMIndex > st.StopId
                 ORDER BY r.PVMBMIndex) ri
    WHERE LEFT(p.PVNo, 2) IN (N'CB', N'CC', N'CG')
      AND (@pvFrom IS NULL OR p.PVNo >= @pvFrom) AND (@pvTo IS NULL OR p.PVNo <= @pvTo)
      AND (@dateFrom IS NULL OR p.PVDate >= @dateFrom) AND (@dateTo IS NULL OR p.PVDate <= @dateTo)
      AND (@caseNo IS NULL OR p.CASNumber = @caseNo)
      AND (@chequeNo IS NULL OR p.PVChequeNo = @chequeNo)
      AND (@payee IS NULL OR p.PVPayto LIKE N'%' + @payee + N'%')
      AND (@status IS NULL OR p.PVStatus = @status)
    ORDER BY p.PVDate DESC, p.PVNo DESC, p.PVMBMIndex DESC
    OPTION (RECOMPILE);
END
GO

-- Cancel (C) or expire (E) voucher @id. Only an active (A) or re-issued (R) voucher with a positive amount that has not been stopped
-- yet. Result: 0 done, 1 not found, 2 refused.
CREATE OR ALTER PROCEDURE genisis.SuspendActive_Stop
    @id int, @status char(1), @actionDate datetime, @remark nvarchar(400), @userCode nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @pv nvarchar(30), @amount money, @current char(1);
    SELECT @pv = RTRIM(PVNo), @amount = PVChequeAmt, @current = PVStatus FROM genisis.Pay_Payment2MBM WITH (UPDLOCK, HOLDLOCK) WHERE PVMBMIndex = @id;
    IF @pv IS NULL
    BEGIN
        SELECT 1 AS Result;
        RETURN;
    END
    IF @status NOT IN ('C', 'E') OR ISNULL(@current, '') NOT IN ('A', 'R') OR ISNULL(@amount, 0) <= 0
       OR EXISTS (SELECT 1 FROM genisis.Pay_Payment2MBM WITH (UPDLOCK, HOLDLOCK) WHERE RTRIM(PVNo) = @pv AND PVStatus IN ('C', 'E'))
    BEGIN
        SELECT 2 AS Result;
        RETURN;
    END
    INSERT INTO genisis.Pay_Payment2MBM (PVNo, PVDate, PVChequeAmt, PVChequeDate, PVChequeNo, PVPayto, PVPayeeBank, PVPayeeACNo, InvNo, CASNumber,
        ClaimVersion, Remark, Pay2MBMLastUpdateDate, Pay2MBMLastUpdateUser, MBMPaidAmt, PAYAccPeriodMth, PAYAccPeriodYr, PVPayMBMStatus, PVStatus,
        PVChequeStatus, PVCaseRemark)
    SELECT PVNo, CONVERT(datetime, CONVERT(char(8), @actionDate, 112)), -PVChequeAmt, PVChequeDate, PVChequeNo, PVPayto, PVPayeeBank, PVPayeeACNo,
        InvNo, CASNumber, ClaimVersion, ISNULL(NULLIF(@remark, N''), Remark), CONVERT(datetime, CONVERT(char(8), GETDATE(), 112)), @userCode,
        -MBMPaidAmt, RIGHT(N'0' + CAST(MONTH(@actionDate) AS nvarchar(2)), 2), CAST(YEAR(@actionDate) AS nvarchar(4)), PVPayMBMStatus, @status,
        'Y', PVCaseRemark
    FROM genisis.Pay_Payment2MBM WHERE PVMBMIndex = @id;
    SELECT 0 AS Result;
END
GO

-- Re-issue stopped voucher @id under a new PV no. of its prefix. Only a voucher that has been stopped and not re-issued yet.
-- Result: 0 done (with the new PV no.), 1 not found, 2 refused.
CREATE OR ALTER PROCEDURE genisis.SuspendActive_Reissue
    @id int, @actionDate datetime, @chequeNo nvarchar(40), @paymentMode nvarchar(20), @payeeBank nvarchar(200), @payeeAccount nvarchar(60),
    @remark nvarchar(400), @userCode nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @pv nvarchar(30), @prefix nvarchar(2), @cas nvarchar(30), @ver nvarchar(100), @stopId int, @seq int, @newPv nvarchar(30);
    SELECT @pv = RTRIM(PVNo), @prefix = LEFT(PVNo, 2), @cas = CASNumber, @ver = ClaimVersion
    FROM genisis.Pay_Payment2MBM WITH (UPDLOCK, HOLDLOCK) WHERE PVMBMIndex = @id AND ISNULL(PVChequeAmt, 0) > 0;
    IF @pv IS NULL
    BEGIN
        SELECT 1 AS Result, CAST(NULL AS nvarchar(30)) AS PvNo;
        RETURN;
    END
    SELECT TOP 1 @stopId = PVMBMIndex FROM genisis.Pay_Payment2MBM WHERE RTRIM(PVNo) = @pv AND PVStatus IN ('C', 'E') ORDER BY PVMBMIndex;
    IF @stopId IS NULL OR @prefix NOT IN (N'CB', N'CC', N'CG')
       OR EXISTS (SELECT 1 FROM genisis.Pay_Payment2MBM WITH (UPDLOCK, HOLDLOCK)
                  WHERE CASNumber = @cas AND ClaimVersion = @ver AND PVStatus = 'R' AND PVMBMIndex > @stopId)
    BEGIN
        SELECT 2 AS Result, CAST(NULL AS nvarchar(30)) AS PvNo;
        RETURN;
    END

    IF @prefix = N'CB'
    BEGIN
        SELECT @seq = CAST(PVSeqNo AS int) FROM genisis.Pay_PVNSeq2013 WITH (UPDLOCK, HOLDLOCK);
        SET @seq = @seq + 1;
        WHILE EXISTS (SELECT 1 FROM genisis.Pay_Payment2MBM WHERE RTRIM(PVNo) = N'CB' + RIGHT(N'00000000' + CAST(@seq AS nvarchar(10)), 8))
           OR EXISTS (SELECT 1 FROM genisis.Pay_Payment2HOS WHERE RTRIM(PVHOSNo) = N'CB' + RIGHT(N'00000000' + CAST(@seq AS nvarchar(10)), 8))
            SET @seq = @seq + 1;
        UPDATE genisis.Pay_PVNSeq2013 SET PVSeqNo = RIGHT(N'00000000' + CAST(@seq AS nvarchar(10)), 8);
    END
    ELSE IF @prefix = N'CC'
    BEGIN
        SELECT @seq = CAST(PVSeqNo AS int) FROM genisis.Pay_PVMedicaSeq2013 WITH (UPDLOCK, HOLDLOCK);
        SET @seq = @seq + 1;
        WHILE EXISTS (SELECT 1 FROM genisis.Pay_Payment2MBM WHERE RTRIM(PVNo) = N'CC' + RIGHT(N'00000000' + CAST(@seq AS nvarchar(10)), 8))
           OR EXISTS (SELECT 1 FROM genisis.Pay_Payment2HOS WHERE RTRIM(PVHOSNo) = N'CC' + RIGHT(N'00000000' + CAST(@seq AS nvarchar(10)), 8))
            SET @seq = @seq + 1;
        UPDATE genisis.Pay_PVMedicaSeq2013 SET PVSeqNo = RIGHT(N'00000000' + CAST(@seq AS nvarchar(10)), 8);
    END
    ELSE
    BEGIN
        SELECT @seq = CAST(PVSeqNo AS int) FROM genisis.Pay_PVMedicaMXSeq WITH (UPDLOCK, HOLDLOCK);
        SET @seq = @seq + 1;
        WHILE EXISTS (SELECT 1 FROM genisis.Pay_Payment2MBM WHERE RTRIM(PVNo) = N'CG' + RIGHT(N'00000000' + CAST(@seq AS nvarchar(10)), 8))
           OR EXISTS (SELECT 1 FROM genisis.Pay_Payment2HOS WHERE RTRIM(PVHOSNo) = N'CG' + RIGHT(N'00000000' + CAST(@seq AS nvarchar(10)), 8))
            SET @seq = @seq + 1;
        UPDATE genisis.Pay_PVMedicaMXSeq SET PVSeqNo = RIGHT(N'00000000' + CAST(@seq AS nvarchar(10)), 8);
    END
    SET @newPv = @prefix + RIGHT(N'00000000' + CAST(@seq AS nvarchar(10)), 8);

    INSERT INTO genisis.Pay_Payment2MBM (PVNo, PVDate, PVChequeAmt, PVChequeDate, PVChequeNo, PVPayto, PVPayeeBank, PVPayeeACNo, InvNo, CASNumber,
        ClaimVersion, Remark, Pay2MBMLastUpdateDate, Pay2MBMLastUpdateUser, MBMPaidAmt, PAYAccPeriodMth, PAYAccPeriodYr, PVPayMBMStatus, PVStatus,
        PVPymtMode, PVCaseRemark)
    SELECT @newPv, CONVERT(datetime, CONVERT(char(8), @actionDate, 112)), PVChequeAmt, CONVERT(datetime, CONVERT(char(8), @actionDate, 112)), @chequeNo,
        PVPayto, ISNULL(NULLIF(@payeeBank, N''), PVPayeeBank), ISNULL(NULLIF(@payeeAccount, N''), PVPayeeACNo), InvNo, CASNumber, ClaimVersion,
        @remark, CONVERT(datetime, CONVERT(char(8), GETDATE(), 112)), @userCode, MBMPaidAmt, RIGHT(N'0' + CAST(MONTH(@actionDate) AS nvarchar(2)), 2),
        CAST(YEAR(@actionDate) AS nvarchar(4)), PVPayMBMStatus, 'R', NULLIF(@paymentMode, N''), PVCaseRemark
    FROM genisis.Pay_Payment2MBM WHERE PVMBMIndex = @id;
    SELECT 0 AS Result, @newPv AS PvNo;
END
GO
