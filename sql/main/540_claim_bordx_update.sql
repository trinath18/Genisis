-- Claim Bordx > Update Bordx (legacy frmUpdateWS2Bordx, "Update Bordx"): put pending worksheets into a bordx whose number is typed (a new bordx, or one that exists), with its date, accounting period and the dates
-- it was sent to and received from the payor and MNRB, and optionally the MNRB receipt. The desktop searched VIEW_WS_PendingBordx with a WHERE fragment and wrote through open recordsets and text SQL,
-- worksheet by worksheet; here every step is a procedure with typed parameters, run in one transaction.

-- WorksheetTrackingListing: the worksheets the criteria find. @amount 'le' (approved up to 2000), 'gt' (above 2000) or NULL; @unbordxedOnly 1 keeps the worksheets that are in no bordx (the desktop's
-- "Bordx Status" box). Dates compare with the stored datetime, case and bordx numbers as text ranges.
CREATE OR ALTER PROCEDURE genisis.UpdateBordx_Search
    @payor nvarchar(8) = NULL, @amount char(2) = NULL, @unbordxedOnly bit = 0, @doaFrom datetime = NULL, @doaTo datetime = NULL, @dodFrom datetime = NULL, @dodTo datetime = NULL,
    @caseFrom nvarchar(30) = NULL, @caseTo nvarchar(30) = NULL, @bordxFrom nvarchar(40) = NULL, @bordxTo nvarchar(40) = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(PAYCode) AS PayorCode, RTRIM(CASNumber) AS CaseNo, ClaimVersion, CLMTotalActual AS TotalActual, CLMTotalApproved AS TotalApproved, CLMPayableByMedix2H AS PayableToHospital,
        CLMPayableByMedix2M AS PayableToMember, CASDateAdmitted AS DateAdmitted, CASDischargeDate AS DateDischarged,
        RTRIM(CASE WHEN MBMPatientCovID = N'00' THEN MBMName ELSE MBMCName END) AS PatientName, RTRIM(HOSName) AS Hospital, RTRIM(MBMGroupCompany) AS GroupCompany, RTRIM(PLNCode) AS PlanCode,
        RTRIM(ICDCode1) AS IcdCode, RTRIM(ClaimStatus) AS ClaimStatus, RTRIM(BordxRefNo) AS BordxNo
    FROM genisis.WS_PendingBordx
    WHERE (@payor IS NULL OR PAYCode = @payor)
      AND (@amount IS NULL OR (@amount = 'le' AND CLMTotalApproved <= 2000) OR (@amount = 'gt' AND CLMTotalApproved > 2000))
      AND (@unbordxedOnly = 0 OR BordxRefNo IS NULL OR BordxRefNo = N'')
      AND (@doaFrom IS NULL OR CASDateAdmitted >= @doaFrom) AND (@doaTo IS NULL OR CASDateAdmitted <= @doaTo)
      AND (@dodFrom IS NULL OR CASDischargeDate >= @dodFrom) AND (@dodTo IS NULL OR CASDischargeDate <= @dodTo)
      AND (@caseFrom IS NULL OR CASNumber >= @caseFrom) AND (@caseTo IS NULL OR CASNumber <= @caseTo)
      AND (@bordxFrom IS NULL OR BordxRefNo >= @bordxFrom) AND (@bordxTo IS NULL OR BordxRefNo <= @bordxTo)
    ORDER BY CASNumber, ClaimVersion
    OPTION (RECOMPILE);
END
GO

-- UpdateRecord: one worksheet goes into the bordx (BordxRefNo, ClaimStatus C, ClaimBordxStatus 1) and its invoices are bordxed (or one empty invoice row is added when it has none), as the desktop wrote them
-- (it did not set CLMBordxDate or the case's pending flag, which Inpatient Case Listing does). A worksheet that is in a bordx already is refused, so one cannot be moved from a bordx whose totals would stay.
-- Result: 0 done, 1 there is no such worksheet, 2 it is in a bordx already. The payor and the approved amount of the worksheet come back for the bordx's checks and total.
CREATE OR ALTER PROCEDURE genisis.UpdateBordx_Apply
    @cas nvarchar(30), @version int, @bordxNo nvarchar(40)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @existing nvarchar(40), @found bit = 0, @approved money, @payor nvarchar(8);
    SELECT @found = 1, @existing = RTRIM(BordxRefNo), @approved = CLMTotalApproved FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @version;
    IF @found = 0
    BEGIN
        SELECT 1 AS Result, CAST(NULL AS nvarchar(8)) AS PayorCode, CAST(0 AS money) AS Approved;
        RETURN;
    END
    IF ISNULL(@existing, N'') <> N''
    BEGIN
        SELECT 2 AS Result, CAST(NULL AS nvarchar(8)) AS PayorCode, CAST(0 AS money) AS Approved;
        RETURN;
    END
    SELECT TOP (1) @payor = RTRIM(PAYCode) FROM genisis.WS_PendingBordx WHERE CASNumber = @cas AND ClaimVersion = @version;
    UPDATE genisis.WS_Claim SET BordxRefNo = @bordxNo, ClaimStatus = 'C', ClaimBordxStatus = 1 WHERE CASNumber = @cas AND ClaimVersion = @version;
    IF EXISTS (SELECT 1 FROM genisis.WS_ClaimInvoice WHERE CASNumber = @cas AND INVWSversion = CAST(@version AS nvarchar(4)))
        UPDATE genisis.WS_ClaimInvoice SET INVBordxRefNo = @bordxNo, INVBordxed = 1 WHERE CASNumber = @cas AND INVWSversion = CAST(@version AS nvarchar(4));
    ELSE
        INSERT genisis.WS_ClaimInvoice (CASNumber, INVWSversion, INVBordxed, INVBordxRefNo, INVNo) VALUES (@cas, CAST(@version AS nvarchar(4)), 1, @bordxNo, N'');
    SELECT 0 AS Result, @payor AS PayorCode, ISNULL(@approved, 0) AS Approved;
END
GO

-- SaveCLMBordx: the Bordx row of the number: created with the payor, date, user and the dates given, or (when it exists) given the accounting period and the dates. The totals are worked out from the
-- worksheets that are in the bordx now: the desktop added the new worksheets only to a bordx that had none, so a bordx that already had worksheets kept its old total and count.
-- Result: 0 saved, 1 the bordx belongs to another payor.
CREATE OR ALTER PROCEDURE genisis.UpdateBordx_Save
    @bordxNo nvarchar(40), @payor nvarchar(8), @bordxDate datetime, @userCode nvarchar(15), @acMonth nvarchar(9) = NULL, @acYear nvarchar(9) = NULL,
    @toPayorDate datetime = NULL, @fromPayorDate datetime = NULL, @toMnrbDate datetime = NULL, @fromMnrbDate datetime = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today datetime = CAST(CAST(GETDATE() AS date) AS datetime);
    DECLARE @amount money, @claims int;
    SELECT @amount = ISNULL(SUM(CLMTotalApproved), 0), @claims = COUNT(*) FROM genisis.WS_Claim WHERE BordxRefNo = @bordxNo;
    IF EXISTS (SELECT 1 FROM genisis.WS_Bordx WHERE BordxRefNo = @bordxNo)
    BEGIN
        IF EXISTS (SELECT 1 FROM genisis.WS_Bordx WHERE BordxRefNo = @bordxNo AND RTRIM(BordxPAYCode) <> @payor)
        BEGIN
            SELECT 1 AS Result;
            RETURN;
        END
        UPDATE genisis.WS_Bordx SET BordxClaimAmt = @amount, BordxNoClaims = @claims WHERE BordxRefNo = @bordxNo;
    END
    ELSE
        INSERT genisis.WS_Bordx (BordxRefNo, BordxPAYCode, BordxDate, BordxBy, BordxPrintedDate, BordxPrintedBy, BordxClaimAmt, BordxNoClaims)
        VALUES (@bordxNo, @payor, @bordxDate, @userCode, @today, @userCode, @amount, @claims);
    UPDATE genisis.WS_Bordx
    SET PAYToACPeriodMth = ISNULL(@acMonth, PAYToACPeriodMth), PAYToACPeriodYR = ISNULL(@acYear, PAYToACPeriodYR),
        BordxPAYSentDate = ISNULL(@toPayorDate, BordxPAYSentDate), BordxPAYToStatus = CASE WHEN @toPayorDate IS NOT NULL THEN '1' ELSE BordxPAYToStatus END,
        BordxPAYRecDocDate = ISNULL(@fromPayorDate, BordxPAYRecDocDate), BordxPAYDOCFRStatus = CASE WHEN @fromPayorDate IS NOT NULL THEN '1' ELSE BordxPAYDOCFRStatus END,
        BordxMNRBSentDate = ISNULL(@toMnrbDate, BordxMNRBSentDate), BordxMNRBToStatus = CASE WHEN @toMnrbDate IS NOT NULL THEN '1' ELSE BordxMNRBToStatus END,
        BordxMNRBRecDocDate = ISNULL(@fromMnrbDate, BordxMNRBRecDocDate), BordxMNRBDOCFRStatus = CASE WHEN @fromMnrbDate IS NOT NULL THEN '1' ELSE BordxMNRBDOCFRStatus END
    WHERE BordxRefNo = @bordxNo;
    SELECT 0 AS Result;
END
GO

-- UpdateMNRB: the receipt from MNRB of the bordx (one row per bordx, as the desktop's recordset kept it): receipt number, date, cheque amount and number, and the amount of the worksheets checked.
-- The desktop wrote the row for every update, even without a receipt number, and put the receipt number into the cheque number; the row is written only when a receipt number is given, with the
-- cheque number that was typed.
CREATE OR ALTER PROCEDURE genisis.UpdateBordx_SaveMnrbReceipt
    @bordxNo nvarchar(40), @receiptNo nvarchar(30), @receiptDate datetime = NULL, @chequeAmount money = NULL, @chequeNo nvarchar(40) = NULL, @caseNo nvarchar(30), @version nvarchar(100),
    @checkedAmount money, @userCode nvarchar(20), @today datetime
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @index int = (SELECT TOP (1) RecMNRBIndex FROM genisis.Pay_ReceiptfrMNRB WHERE BordxNo = @bordxNo ORDER BY RecMNRBIndex);
    IF @index IS NULL
        INSERT genisis.Pay_ReceiptfrMNRB (ReceiptNo, ReceiptDate, ReceiptAmt, ChequeNo, BordxNo, CASNumber, ClaimVersion, MNRBLastUpdatedate, MNRBLastUpdateUser, RecMNRBAmt)
        VALUES (@receiptNo, @receiptDate, @chequeAmount, @chequeNo, @bordxNo, @caseNo, @version, @today, @userCode, @checkedAmount);
    ELSE
        UPDATE genisis.Pay_ReceiptfrMNRB
        SET ReceiptNo = @receiptNo, ReceiptDate = ISNULL(@receiptDate, ReceiptDate), ReceiptAmt = ISNULL(@chequeAmount, ReceiptAmt), ChequeNo = ISNULL(@chequeNo, ChequeNo), CASNumber = @caseNo,
            ClaimVersion = @version, RecMNRBAmt = @checkedAmount, MNRBLastUpdatedate = @today, MNRBLastUpdateUser = @userCode
        WHERE RecMNRBIndex = @index;
    SELECT 0 AS Result;
END
GO
