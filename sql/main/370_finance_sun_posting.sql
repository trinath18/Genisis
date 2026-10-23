-- Finance > SUN Posting (legacy frmPostingInvoiceMBM, "Member Invoice Posting", PostType 'InvMBM').
-- The desktop sent a WHERE fragment to the legacy dbo.SUN_HIS_Posting (dynamic SQL over HISWorkSheet.dbo.VIEW_SUN_Posting_MBMInv);
-- here every criterion is a typed parameter. Reads genisis.WS_SunPostingMbmInv, writes genisis.WS_SUNPosting / genisis.WS_SunPostBatch.

-- ComboListing: health types, payors and accounting periods (the post / bordx status and post type lists are fixed in the API).
CREATE OR ALTER PROCEDURE genisis.SunPostingMbm_Lookups
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(HLTCode) AS Code, RTRIM(HLTDescription) AS Name FROM dbo.HLT ORDER BY HLTCode;
    SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name FROM dbo.PAY ORDER BY PAYCode;
    SELECT RTRIM(AccMonth) + N'/' + RTRIM(AccYear) AS Code, RTRIM(AccMonth) + N'/' + RTRIM(AccYear) AS Name
    FROM genisis.Maint_AccPeriod
    ORDER BY AccYear DESC, AccMonth DESC;
END
GO

-- SearchRecords + MBMInvListing / MBMInvCRListing / MBMInvDRListing: the invoices of the view with the desktop's filters.
-- Inclusive ranges; NULL = no criterion. Dates compare with the stored datetime exactly as the desktop's 'yyyy/mm/dd' text did
-- (a "to" date matches rows stamped at midnight of that day). @postStatus '0': not posted (InvMStatus and PostStatus blank or 0),
-- '1': posted. @bordxStatus '0': no bordx reference, '1': has one. @amountSign 1: INVAmount > 0 (post type P), -1: < 0 (N),
-- NULL: either (the credit / debit lists). The patient name (covered person when the cover id is not 00) and the product
-- category (PLN.PLNProCat for the health type / payor; the API uses 'SM' for health type S) are joined here instead of one
-- query per row.
CREATE OR ALTER PROCEDURE genisis.SunPostingMbm_Search
    @healthType nvarchar(4) = NULL, @payor nvarchar(4) = NULL,
    @invoiceFrom datetime = NULL, @invoiceTo datetime = NULL,
    @caseFrom nvarchar(30) = NULL, @caseTo nvarchar(30) = NULL,
    @postStatus char(1) = NULL, @postFrom datetime = NULL, @postTo datetime = NULL,
    @bordxStatus char(1) = NULL, @batchFrom nvarchar(5) = NULL, @batchTo nvarchar(5) = NULL,
    @bordxFrom datetime = NULL, @bordxTo datetime = NULL,
    @amountSign smallint = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max)
        RTRIM(v.CASNumber) AS CaseNo, RTRIM(v.INVWSversion) AS ClaimVersion, RTRIM(v.MBMNumber) AS MembershipNo,
        RTRIM(v.MBMPatientCovID) AS PatientCoverId, RTRIM(v.MBMName) AS MemberName,
        CASE WHEN RTRIM(ISNULL(v.MBMPatientCovID, N'')) <> N'00' THEN RTRIM(cp.MBMCName) ELSE RTRIM(v.MBMName) END AS PatientName,
        RTRIM(v.InvMStatus) AS InvMStatus, RTRIM(v.PostStatus) AS PostStatus, v.PostDate, RTRIM(v.AccMonth) AS AccMonth, RTRIM(v.AccYear) AS AccYear,
        RTRIM(v.HLTCode) AS HealthType, RTRIM(v.PAYCode) AS Payor, RTRIM(v.MBMGroupCompany) AS GroupCompany,
        v.INVEnteredDate AS InvoiceDate, v.INVAmount AS Amount, RTRIM(v.BatchNo) AS BatchNo, v.BordxDate,
        RTRIM(v.INVBordxRefNo) AS BordxRefNo, RTRIM(v.CLMBordxRevInd) AS BordxReversal, RTRIM(pl.PLNProCat) AS ProductCategory
    FROM genisis.WS_SunPostingMbmInv v
    OUTER APPLY (SELECT TOP 1 c.MBMCName FROM dbo.MBMCoveredPersons c
                 WHERE c.MBMCNumber = RTRIM(v.MBMNumber) AND c.MBMCCoverID = RTRIM(v.MBMPatientCovID)) cp
    OUTER APPLY (SELECT TOP 1 p.PLNProCat FROM dbo.PLN p WHERE p.HLTCode = RTRIM(v.HLTCode) AND p.PAYCode = RTRIM(v.PAYCode)) pl
    WHERE (@healthType IS NULL OR v.HLTCode = @healthType)
      AND (@payor IS NULL OR v.PAYCode = @payor)
      AND (@invoiceFrom IS NULL OR v.INVEnteredDate >= @invoiceFrom) AND (@invoiceTo IS NULL OR v.INVEnteredDate <= @invoiceTo)
      AND (@caseFrom IS NULL OR v.CASNumber >= @caseFrom) AND (@caseTo IS NULL OR v.CASNumber <= @caseTo)
      AND (@postStatus IS NULL
           OR (@postStatus = '0' AND (v.InvMStatus IS NULL OR v.InvMStatus = '0') AND (v.PostStatus IS NULL OR v.PostStatus = '0'))
           OR (@postStatus = '1' AND v.InvMStatus = '1' AND v.PostStatus = '1'))
      AND (@postFrom IS NULL OR v.PostDate >= @postFrom) AND (@postTo IS NULL OR v.PostDate <= @postTo)
      AND (@bordxStatus IS NULL
           OR (@bordxStatus = '0' AND (v.INVBordxRefNo IS NULL OR v.INVBordxRefNo = N''))
           OR (@bordxStatus = '1' AND v.INVBordxRefNo <> N''))
      AND (@batchFrom IS NULL OR v.BatchNo >= @batchFrom) AND (@batchTo IS NULL OR v.BatchNo <= @batchTo)
      AND (@bordxFrom IS NULL OR v.BordxDate >= @bordxFrom) AND (@bordxTo IS NULL OR v.BordxDate <= @bordxTo)
      AND (@amountSign IS NULL OR (@amountSign = 1 AND v.INVAmount > 0) OR (@amountSign = -1 AND v.INVAmount < 0))
    ORDER BY v.CASNumber, v.INVWSversion
    OPTION (RECOMPILE);
END
GO

-- CheckTime("POSTING") + SpcAccess: the user's special access flag and the end time of the posting window. The API decides
-- with its clock (the desktop closed the form while the time of day was not after TimeMain.EndTime, unless SpecialAccess = 'Y').
CREATE OR ALTER PROCEDURE genisis.SunPosting_Window
    @userCode nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT (SELECT TOP 1 RTRIM(SpecialAccess) FROM genisis.Maint_USR WHERE USRCode = @userCode) AS SpecialAccess,
           (SELECT TOP 1 EndTime FROM genisis.Maint_TimeMain WHERE Process = N'POSTING') AS EndTime;
END
GO

-- GenSUNPostingBatchNo: the next batch number (the last one + 1, first batch 1001). Locked so two users cannot get the same
-- number; runs inside the caller's transaction, so a failed post gives the number back.
CREATE OR ALTER PROCEDURE genisis.SunPosting_NextBatch
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @next int;
    SELECT @next = MAX(CAST(BatchNo AS int)) + 1 FROM genisis.WS_SunPostBatch WITH (UPDLOCK, HOLDLOCK);
    IF @next IS NULL
    BEGIN
        SET @next = 1001;
        INSERT INTO genisis.WS_SunPostBatch (BatchNo) VALUES (CAST(@next AS nvarchar(20)));
    END
    ELSE
        UPDATE genisis.WS_SunPostBatch SET BatchNo = CAST(@next AS nvarchar(20));
    SELECT CAST(@next AS nvarchar(20)) AS BatchNo;
END
GO

-- CmdPost_Click: one SUNPosting row per invoice (InvMStatus 1, PostStatus 1, PostType InvMBM, today's date, the user, the
-- accounting month / year and the batch). The desktop only stopped a posted list in the screen; here an invoice already posted
-- as InvMBM is refused. Result: 0 done, 1 invoice not found, 2 refused (already posted).
CREATE OR ALTER PROCEDURE genisis.SunPostingMbm_Insert
    @cas nvarchar(30), @version nvarchar(4), @accMonth char(2), @accYear char(4), @batchNo nvarchar(5), @userCode nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM genisis.WS_ClaimInvoice WHERE CASNumber = @cas AND INVWSversion = @version)
    BEGIN
        SELECT 1 AS Result;
        RETURN;
    END
    IF EXISTS (SELECT 1 FROM genisis.WS_SUNPosting WITH (UPDLOCK, HOLDLOCK)
               WHERE CASNumber = @cas AND ClmVersion = @version AND PostType = N'InvMBM' AND InvMStatus = '1')
    BEGIN
        SELECT 2 AS Result;
        RETURN;
    END
    INSERT INTO genisis.WS_SUNPosting (CASNumber, ClmVersion, InvMStatus, PostStatus, PostType, PostDate, PostBy, AccMonth, AccYear, BatchNo)
    VALUES (@cas, @version, '1', '1', N'InvMBM', CONVERT(datetime, CONVERT(char(8), GETDATE(), 112)), @userCode, @accMonth, @accYear, @batchNo);
    SELECT 0 AS Result;
END
GO
