-- Case > Case Audit Review (legacy frmCaseAuditReview). The case, member, diagnoses and repudiation are read with the
-- procedures Case Adjustment already uses; these cover what is specific to the audit review: its own record
-- (dbo.CASAuditReview, shown through dbo.VIEW_CAS_AuditReview) and the hospital payments (HISPayment.Payment2HOS).
-- They replace the raw @strAppend dbo.Case_AuditReview modes and dbo.Case_AuditInsert / dbo.Case_AuditUpdate.

-- showOther (audit part) + showPaymentRecord + the member. The visit date is stored by the desktop as an mm/dd/yyyy string.
CREATE OR ALTER PROCEDURE genisis.CaseAudit_Get
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SET DATEFORMAT mdy;
    SELECT RTRIM(CASInvNo) AS InvoiceNo, INVDate AS InvoiceDate, CASInvAmt AS InvoiceAmount, CASDisRec AS DiscountReceived,
        RTRIM(CASDiscType) AS DiscountType, CASLetterSent AS LetterSent, CASHospReply AS LetterReply,
        RTRIM(CASARStatus) AS Status, CASAROpenDate AS DateOpen, CASDTC AS DateToClaims,
        CASReviewAudit AS Query, CASARAnswer AS Answer, CASARRemarks AS Remarks,
        CASE WHEN ISDATE(NULLIF(RTRIM(CASDOV), N'')) = 1 THEN CONVERT(datetime, RTRIM(CASDOV), 101) END AS VisitDate, RTRIM(CASDOVTime) AS VisitTime,
        CASVRemarks AS VisitAction, RTRIM(Officer) AS Officer
    FROM dbo.CASAuditReview WHERE CASNumber = @cas;

    IF OBJECT_ID(N'genisis.Pay_Payment2HOS', N'SN') IS NOT NULL
        EXEC sp_executesql N'SELECT RTRIM(PVHOSNo) AS PvNo, PVHOSDate AS PvDate, HOSPaidAmt AS AmountPaid
            FROM genisis.Pay_Payment2HOS WHERE CASNumber = @cas ORDER BY PVHOSDate, PVHOSIndex',
            N'@cas nvarchar(15)', @cas;
    ELSE
        SELECT CAST(NULL AS nvarchar(30)) AS PvNo, CAST(NULL AS datetime) AS PvDate, CAST(NULL AS money) AS AmountPaid WHERE 1 = 0;

    -- The member straight from dbo.MBM: SearchMBMCas (used for the case's member panel) returns nothing for a member
    -- that has no MBMOthers / MBMOthersTwo / MBMTwo row, and the audit review still has to show who the member is.
    SELECT RTRIM(m.MBMNumber) AS MemberNo, RTRIM(m.MBMName) AS Name, RTRIM(m.MBMIcBcPp) AS Ic, RTRIM(m.PAYCode) AS Payor,
        RTRIM(m.MBMPolicyNo) AS PolicyNo, RTRIM(m.MBMRenewFlag) AS Renewal, RTRIM(m.MBMTakeover) AS TakeOver,
        m.MBMPayorEffDate AS EffectiveDate, m.MBMPayorExpDate AS ExpiryDate, RTRIM(m.MBMAddress1) AS Address1,
        RTRIM(m.MBMAddress2) AS Address2, RTRIM(m.MBMAddress3) AS Address3, RTRIM(m.MBMPostCode) AS PostCode,
        RTRIM(m.CTYCode) AS Town, RTRIM(m.STACode) AS State, m.MBMDOB AS Dob, m.MBMAvailableLimit AS AvailableLimit,
        RTRIM(m.MBMStatus) AS Status, RTRIM(CAST(o.MBMInstallment AS nvarchar(50))) AS Installment
    FROM dbo.CAS c
    INNER JOIN dbo.MBM m ON m.MBMNumber = c.MBMNumber
    LEFT JOIN dbo.MBMOthers o ON o.MBMNumber = m.MBMNumber
    WHERE c.CASNumber = @cas;
END
GO

-- cmdUpdate1_Click: Case_AuditInsert / Case_AuditUpdate on the case's audit row plus Case_AuditUpdate on the maintenance
-- cross reference (the audit status). Blank amounts are stored as 0, blank dates as NULL, like the desktop.
-- Result: 0 saved, 1 case not found.
CREATE OR ALTER PROCEDURE genisis.CaseAudit_Save
    @cas nvarchar(15), @invoiceNo nvarchar(4000), @invoiceDate datetime, @invoiceAmount money, @discountReceived money,
    @discountType nvarchar(4000), @letterSent datetime, @letterReply datetime, @status nvarchar(4000),
    @dateOpen datetime, @dateToClaims datetime, @query nvarchar(4000), @answer nvarchar(4000), @remarks nvarchar(4000),
    @visitDate datetime, @visitTime nvarchar(4000), @visitAction nvarchar(4000), @officer nvarchar(4000), @userCode nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @mbm nvarchar(30) = (SELECT MBMNumber FROM dbo.CAS WHERE CASNumber = @cas);
    IF @mbm IS NULL
    BEGIN
        SELECT 1 AS Result;
        RETURN;
    END

    DECLARE @today datetime = CAST(CAST(GETDATE() AS date) AS datetime), @user nvarchar(10) = LEFT(@userCode, 10);
    DECLARE @visit nvarchar(10) = CONVERT(nvarchar(10), @visitDate, 101);
    DECLARE @amount money = ISNULL(@invoiceAmount, 0), @discount money = ISNULL(@discountReceived, 0);

    IF EXISTS (SELECT 1 FROM dbo.CASAuditReview WITH (UPDLOCK, ROWLOCK) WHERE CASNumber = @cas)
        UPDATE dbo.CASAuditReview
        SET CASInvNo = @invoiceNo, INVDate = @invoiceDate, CASInvAmt = @amount, CASLetterSent = @letterSent, CASHospReply = @letterReply,
            CASDOV = @visit, CASDOVTime = @visitTime, Officer = @officer, CASVRemarks = @visitAction, CASDisRec = @discount,
            CASDTC = @dateToClaims, CASAROpenDate = @dateOpen, CASARAnswer = @answer, CASARRemarks = @remarks,
            CASReviewAudit = @query, CASARStatus = @status, CASLastUpdateUser = @user, CASLastUpdateDate = @today,
            CASDiscType = @discountType
        WHERE CASNumber = @cas;
    ELSE
        INSERT dbo.CASAuditReview (CASNumber, CASInvNo, INVDate, CASInvAmt, CASLetterSent, CASHospReply, CASDOV, CASDOVTime,
            Officer, CASVRemarks, CASDisRec, CASDTC, CASAROpenDate, CASARAnswer, CASARRemarks, CASReviewAudit, CASARStatus,
            CASLastUpdateUser, CASLastUpdateDate, CASDiscType)
        VALUES (@cas, @invoiceNo, @invoiceDate, @amount, @letterSent, @letterReply, @visit, @visitTime,
            @officer, @visitAction, @discount, @dateToClaims, @dateOpen, @answer, @remarks, @query, @status,
            @user, @today, @discountType);

    UPDATE genisis.Maint_CASCrossReference SET CASARStatus = @status WHERE CASNumber = @cas AND MBMNumber = @mbm;

    SELECT 0 AS Result;
END
GO
