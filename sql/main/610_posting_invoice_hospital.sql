-- Posting > Invoice > Hospital (legacy frmPostingInvoiceHosp, "Posting - HospInv", PostType 'InvHosp'): the hospital invoices of the bordx the SUN journal is made from. The desktop sent a WHERE fragment to the
-- legacy dbo.SUN_HIS_Posting (dynamic SQL over HISWorkSheet.dbo.VIEW_SUN_Posting_HospInv); here every criterion is a typed parameter. Reads genisis.WS_SunPostingHospInv, writes genisis.WS_SUNPosting. The batch
-- number and the posting window are genisis.SunPosting_NextBatch and genisis.SunPosting_Window (Finance > SUN Posting's).

-- ComboListing + ChkHospital_Click + chkPayor_Click: health types, payors (with their health type, so the screen can offer the payors of one health type), hospitals and accounting periods.
CREATE OR ALTER PROCEDURE genisis.SunPostingHosp_Lookups
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(HLTCode) AS Code, RTRIM(HLTDescription) AS Name FROM dbo.HLT ORDER BY HLTCode;
    SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name, RTRIM(HLTCode) AS HealthType FROM dbo.PAY ORDER BY PAYCode;
    SELECT RTRIM(HOSCode) AS Code, RTRIM(HOSName) AS Name FROM dbo.HOS ORDER BY HOSName;
    SELECT RTRIM(AccMonth) + N'/' + RTRIM(AccYear) AS Code, RTRIM(AccMonth) + N'/' + RTRIM(AccYear) AS Name FROM genisis.Maint_AccPeriod ORDER BY AccYear DESC, AccMonth DESC;
END
GO

-- SearchRecord + HospInvListing / HospInvCRListing / HospInvDNListing: the hospital invoices of the view with the desktop's filters. Inclusive ranges; NULL = no criterion. @hospital is the hospital's name.
-- @postStatus '0': not posted (InvHStatus and PostStatus blank or 0), '1': posted. @bordxStatus '0': no bordx reference, '1': has one. The patient name (the covered person when the cover id is not 00) and the
-- product category (PLN.PLNProCat for the health type and payor) are joined here instead of one query per row.
CREATE OR ALTER PROCEDURE genisis.SunPostingHosp_Search
    @healthType nvarchar(4) = NULL, @hospital nvarchar(200) = NULL, @payor nvarchar(4) = NULL, @invoiceFrom datetime = NULL, @invoiceTo datetime = NULL, @invNoFrom nvarchar(40) = NULL, @invNoTo nvarchar(40) = NULL,
    @postStatus char(1) = NULL, @postFrom datetime = NULL, @postTo datetime = NULL, @bordxStatus char(1) = NULL, @batchFrom nvarchar(5) = NULL, @batchTo nvarchar(5) = NULL, @bordxFrom datetime = NULL,
    @bordxTo datetime = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max)
        RTRIM(v.INVIndex) AS InvoiceIndex, RTRIM(v.InvNo) AS InvoiceNo, RTRIM(v.CASNumber) AS CaseNo, RTRIM(CAST(v.ClaimVersion AS nvarchar(10))) AS ClaimVersion, RTRIM(v.MBMNumber) AS MembershipNo,
        RTRIM(v.MBMPatientCovID) AS PatientCoverId, RTRIM(v.MBMName) AS MemberName,
        CASE WHEN RTRIM(ISNULL(v.MBMPatientCovID, N'')) <> N'00' THEN ISNULL(RTRIM(cp.MBMCName), RTRIM(v.MBMName)) ELSE RTRIM(v.MBMName) END AS PatientName,
        RTRIM(v.InvHStatus) AS InvHStatus, RTRIM(v.PostStatus) AS PostStatus, v.PostDate, RTRIM(v.AccMonth) AS AccMonth, RTRIM(v.AccYear) AS AccYear, RTRIM(v.HLTCode) AS HealthType, RTRIM(v.PAYCode) AS Payor,
        v.INVEnteredDate AS InvoiceDate, v.INVAmount AS Amount, RTRIM(v.BatchNo) AS BatchNo, v.BordxDate, RTRIM(v.INVBordxRefNo) AS BordxRefNo, RTRIM(v.HOSAccCode) AS HospitalAccCode, RTRIM(v.HOSName) AS HospitalName,
        RTRIM(pl.PLNProCat) AS ProductCategory
    FROM genisis.WS_SunPostingHospInv v
    OUTER APPLY (SELECT TOP 1 c.MBMCName FROM dbo.MBMCoveredPersons c WHERE c.MBMCNumber = RTRIM(v.MBMNumber) AND c.MBMCCoverID = RTRIM(v.MBMPatientCovID)) cp
    OUTER APPLY (SELECT TOP 1 p.PLNProCat FROM dbo.PLN p WHERE p.HLTCode = RTRIM(v.HLTCode) AND p.PAYCode = RTRIM(v.PAYCode)) pl
    WHERE (@healthType IS NULL OR v.HLTCode = @healthType) AND (@hospital IS NULL OR v.HOSName = @hospital) AND (@payor IS NULL OR v.PAYCode = @payor)
      AND (@invoiceFrom IS NULL OR v.INVEnteredDate >= @invoiceFrom) AND (@invoiceTo IS NULL OR v.INVEnteredDate <= @invoiceTo) AND (@invNoFrom IS NULL OR v.InvNo >= @invNoFrom) AND (@invNoTo IS NULL OR v.InvNo <= @invNoTo)
      AND (@postStatus IS NULL OR (@postStatus = '0' AND (v.InvHStatus IS NULL OR v.InvHStatus = '0') AND (v.PostStatus IS NULL OR v.PostStatus = '0')) OR (@postStatus = '1' AND v.InvHStatus = '1' AND v.PostStatus = '1'))
      AND (@postFrom IS NULL OR v.PostDate >= @postFrom) AND (@postTo IS NULL OR v.PostDate <= @postTo)
      AND (@bordxStatus IS NULL OR (@bordxStatus = '0' AND (v.INVBordxRefNo IS NULL OR v.INVBordxRefNo = N'')) OR (@bordxStatus = '1' AND v.INVBordxRefNo <> N''))
      AND (@batchFrom IS NULL OR v.BatchNo >= @batchFrom) AND (@batchTo IS NULL OR v.BatchNo <= @batchTo) AND (@bordxFrom IS NULL OR v.BordxDate >= @bordxFrom) AND (@bordxTo IS NULL OR v.BordxDate <= @bordxTo)
    ORDER BY v.CASNumber, v.ClaimVersion, v.INVIndex
    OPTION (RECOMPILE);
END
GO

-- CmdPost_Click: one SUNPosting row per invoice (InvHStatus 1, PostStatus 1, PostType InvHosp, today's date, the user, the accounting month and year and the batch). The desktop only stopped a posted list in the
-- screen; here an invoice already posted as InvHosp is refused. Result: 0 done, 1 invoice not found, 2 refused (already posted).
CREATE OR ALTER PROCEDURE genisis.SunPostingHosp_Insert
    @invIndex nvarchar(40), @accMonth char(2), @accYear char(4), @batchNo nvarchar(5), @userCode nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM genisis.WS_ClaimInvoice WHERE CAST(INVIndex AS nvarchar(40)) = @invIndex)
    BEGIN
        SELECT 1 AS Result;
        RETURN;
    END
    IF EXISTS (SELECT 1 FROM genisis.WS_SUNPosting WITH (UPDLOCK, HOLDLOCK) WHERE INVIndex = @invIndex AND PostType = N'InvHosp' AND InvHStatus = '1')
    BEGIN
        SELECT 2 AS Result;
        RETURN;
    END
    INSERT INTO genisis.WS_SUNPosting (INVIndex, InvHStatus, PostStatus, PostType, PostDate, PostBy, AccMonth, AccYear, BatchNo)
    VALUES (@invIndex, '1', '1', N'InvHosp', CONVERT(datetime, CONVERT(char(8), GETDATE(), 112)), @userCode, @accMonth, @accYear, @batchNo);
    SELECT 0 AS Result;
END
GO
