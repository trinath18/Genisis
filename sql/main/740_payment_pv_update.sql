-- Payment > PV Update (Manual) (legacy FrmPaymentManual; its source is not in the source tree, so the screen is rebuilt on the views it read, VIEW_Payment2HOS_Manual and VIEW_Payment2MBM_Manual: the
-- payment rows already written, to hospitals (Payment2HOS) and to members (Payment2MBM)). A PV is found by its number, a case or its date, and the details of the whole PV are corrected: the cheque
-- number and date, the payment mode, the payee's bank and account, the payee (members) and the remark. The amounts, the PV number and date, the status and the accounting period are not changed; the
-- adjustment columns (bank charges, gain / loss, discount, difference) are 0 on every row of both tables and are left alone. A PV posted to SUN (PVSunHOSNo / PVSUNNo) or a VOID is refused.
-- @source: 'hospital' or 'member'.

CREATE OR ALTER PROCEDURE genisis.PvUpdate_Find
    @source varchar(8), @pvNo nvarchar(30) = NULL, @caseNo nvarchar(30) = NULL, @dateFrom datetime = NULL, @dateTo datetime = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    IF @source = 'hospital'
        SELECT TOP (@max) RTRIM(PVHOSNo) AS PvNo, MIN(PVHOSDate) AS PvDate, RTRIM(MIN(PVPayee)) AS PayTo, MAX(PVHOSChequeAmt) AS Amount, COUNT(*) AS Claims, RTRIM(MIN(PVHOSChequeNo)) AS ChequeNo,
            RTRIM(MIN(PVPymtMode)) AS PaymentMode, MIN(PVStatus) AS Status, RTRIM(MAX(PVSunHOSNo)) AS SunNo
        FROM genisis.Pay_Payment2HOS
        WHERE PVHOSNo IS NOT NULL AND (@pvNo IS NULL OR PVHOSNo = @pvNo) AND (@caseNo IS NULL OR PVHOSNo IN (SELECT PVHOSNo FROM genisis.Pay_Payment2HOS WHERE CASNumber = @caseNo))
          AND (@dateFrom IS NULL OR PVHOSDate >= @dateFrom) AND (@dateTo IS NULL OR PVHOSDate < DATEADD(day, 1, @dateTo))
        GROUP BY PVHOSNo
        ORDER BY MIN(PVHOSDate) DESC, PVHOSNo DESC
        OPTION (RECOMPILE);
    ELSE
        SELECT TOP (@max) RTRIM(PVNo) AS PvNo, MIN(PVDate) AS PvDate, RTRIM(MIN(PVPayto)) AS PayTo, MAX(PVChequeAmt) AS Amount, COUNT(*) AS Claims, RTRIM(MIN(PVChequeNo)) AS ChequeNo,
            RTRIM(MIN(PVPymtMode)) AS PaymentMode, MIN(PVStatus) AS Status, RTRIM(MAX(PVSUNNo)) AS SunNo
        FROM genisis.Pay_Payment2MBM
        WHERE PVNo IS NOT NULL AND PVNo <> N'VOID' AND (@pvNo IS NULL OR PVNo = @pvNo)
          AND (@caseNo IS NULL OR PVNo IN (SELECT PVNo FROM genisis.Pay_Payment2MBM WHERE CASNumber = @caseNo))
          AND (@dateFrom IS NULL OR PVDate >= @dateFrom) AND (@dateTo IS NULL OR PVDate < DATEADD(day, 1, @dateTo))
        GROUP BY PVNo
        ORDER BY MIN(PVDate) DESC, PVNo DESC
        OPTION (RECOMPILE);
END
GO

-- The rows of a PV, with the PV's details (the same on every row as written; the first row's are shown).
CREATE OR ALTER PROCEDURE genisis.PvUpdate_Rows
    @source varchar(8), @pvNo nvarchar(30)
AS
BEGIN
    SET NOCOUNT ON;
    IF @source = 'hospital'
        SELECT PVHOSIndex AS Id, RTRIM(PVHOSNo) AS PvNo, PVHOSDate AS PvDate, RTRIM(CASNumber) AS CaseNo, RTRIM(ClaimVersion) AS ClaimVersion, HOSPaidAmt AS Paid, PVHOSChequeAmt AS Amount,
            RTRIM(PVHOSChequeNo) AS ChequeNo, PVHOSChequeDate AS ChequeDate, RTRIM(PVPymtMode) AS PaymentMode, RTRIM(PVPayee) AS PayTo, RTRIM(PVBank) AS Bank, RTRIM(PVAccountNo) AS Account,
            RTRIM(HOSRemark) AS Remark, PVStatus AS Status, RTRIM(PVSunHOSNo) AS SunNo, RTRIM(HOSAccPeriodMth) AS AccMonth, RTRIM(HOSAccPeriodYr) AS AccYear
        FROM genisis.Pay_Payment2HOS WHERE PVHOSNo = @pvNo ORDER BY PVHOSIndex;
    ELSE
        SELECT PVMBMIndex AS Id, RTRIM(PVNo) AS PvNo, PVDate, RTRIM(CASNumber) AS CaseNo, RTRIM(ClaimVersion) AS ClaimVersion, MBMPaidAmt AS Paid, PVChequeAmt AS Amount,
            RTRIM(PVChequeNo) AS ChequeNo, PVChequeDate AS ChequeDate, RTRIM(PVPymtMode) AS PaymentMode, RTRIM(PVPayto) AS PayTo, RTRIM(PVPayeeBank) AS Bank, RTRIM(PVPayeeACNo) AS Account,
            RTRIM(Remark) AS Remark, PVStatus AS Status, RTRIM(PVSUNNo) AS SunNo, RTRIM(PAYAccPeriodMth) AS AccMonth, RTRIM(PAYAccPeriodYr) AS AccYear
        FROM genisis.Pay_Payment2MBM WHERE PVNo = @pvNo AND PVNo <> N'VOID' ORDER BY PVMBMIndex;
END
GO

-- Saves the details on every row of the PV. Result 0 saved (with the number of rows), 1 no such PV, 2 posted to SUN, 3 a stopped (C / E) member cheque. The rows are locked until the transaction ends.
-- @payTo applies to member PVs only (a hospital PV is made out to the hospital's payee).
CREATE OR ALTER PROCEDURE genisis.PvUpdate_Save
    @source varchar(8), @pvNo nvarchar(30), @chequeNo nvarchar(40), @chequeDate datetime, @paymentMode nvarchar(20), @payTo nvarchar(120), @bank nvarchar(100), @account nvarchar(60),
    @remark nvarchar(400), @userCode nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @rows int, @sun int, @stopped int, @day datetime = CONVERT(datetime, CONVERT(char(8), @chequeDate, 112));
    IF @source = 'hospital'
    BEGIN
        SELECT @rows = COUNT(*), @sun = SUM(CASE WHEN ISNULL(PVSunHOSNo, N'') <> N'' THEN 1 ELSE 0 END), @stopped = 0
        FROM genisis.Pay_Payment2HOS WITH (UPDLOCK, HOLDLOCK) WHERE PVHOSNo = @pvNo;
        IF @rows = 0 BEGIN SELECT 1 AS Result, 0 AS Rows; RETURN; END
        IF @sun > 0 BEGIN SELECT 2 AS Result, 0 AS Rows; RETURN; END
        UPDATE genisis.Pay_Payment2HOS SET PVHOSChequeNo = @chequeNo, PVHOSChequeDate = @day, PVPymtMode = @paymentMode, PVBank = @bank, PVAccountNo = @account, HOSRemark = ISNULL(@remark, N''),
            PVChequeStatus = CASE WHEN @paymentMode = N'ONLINE' THEN 'Y' ELSE NULL END, Pay2HOSLastUpdateDate = CONVERT(datetime, CONVERT(char(8), GETDATE(), 112)), Pay2HOSLastUpdateUser = @userCode
        WHERE PVHOSNo = @pvNo;
        SELECT 0 AS Result, @rows AS Rows;
        RETURN;
    END

    SELECT @rows = COUNT(*), @sun = SUM(CASE WHEN ISNULL(PVSUNNo, N'') <> N'' THEN 1 ELSE 0 END), @stopped = SUM(CASE WHEN PVStatus IN ('C', 'E') THEN 1 ELSE 0 END)
    FROM genisis.Pay_Payment2MBM WITH (UPDLOCK, HOLDLOCK) WHERE PVNo = @pvNo AND PVNo <> N'VOID';
    IF @rows = 0 BEGIN SELECT 1 AS Result, 0 AS Rows; RETURN; END
    IF @sun > 0 BEGIN SELECT 2 AS Result, 0 AS Rows; RETURN; END
    IF @stopped > 0 BEGIN SELECT 3 AS Result, 0 AS Rows; RETURN; END
    UPDATE genisis.Pay_Payment2MBM SET PVChequeNo = @chequeNo, PVChequeDate = @day, PVPymtMode = @paymentMode, PVPayto = ISNULL(@payTo, PVPayto), PVPayeeBank = @bank, PVPayeeACNo = @account,
        Remark = ISNULL(@remark, N''), PVChequeStatus = CASE WHEN @paymentMode = N'ONLINE' THEN 'Y' ELSE NULL END, Pay2MBMLastUpdateDate = CONVERT(datetime, CONVERT(char(8), GETDATE(), 112)),
        Pay2MBMLastUpdateUser = LEFT(@userCode, 20)
    WHERE PVNo = @pvNo;
    SELECT 0 AS Result, @rows AS Rows;
END
GO
