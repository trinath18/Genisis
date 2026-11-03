-- Contribution Billing > Generate Invoice (legacy frmMCOFeesListingNew, "Member MCO Fees"): the MCO fees of a bordx batch or range of a payor (or of an invoice) from VIEW_MCO_LISTING_All2, and the MCO invoice for
-- them. The desktop called SP_MCOFeesNew with the field list and the WHERE as text, copied the rows into MCOInvoicePluto for the Crystal reports, took the invoice number from MBMMCOINVQ with an optimistic
-- recordset and wrote the members member by member in two transactions; here each step is a procedure with typed parameters and the number comes from genisis.McoReinstatement_NextInvoice (the same counters,
-- taken under an update lock). The wording "unbilled" below is the desktop's: no invoice number yet.

-- MCOListing: the members of the batch (a bordx date and batch number) or range (bordx dates) of a payor that have no invoice yet, or of an invoice (billed). @groupCompany keeps one group company.
CREATE OR ALTER PROCEDURE genisis.McoInvoice_Search
    @payor nvarchar(4) = NULL, @bordxDate datetime = NULL, @batch int = NULL, @bordxFrom datetime = NULL, @bordxTo datetime = NULL, @invoiceNo nvarchar(40) = NULL,
    @groupCompany nvarchar(300) = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT TOP (@max) RTRIM(PAYCode) AS PayorCode, RTRIM(MBMNumber) AS MemberNo, RTRIM(MBMCCoverID) AS CoverId, RTRIM(MBMCRELCode) AS Relation, MBMBordxDate AS BordxDate, RTRIM(MBMPolicyNo) AS PolicyNo,
        RTRIM(HLTCode) AS HealthType, MBMBatchNo AS BatchNo, RTRIM(MBMName) AS MemberName, RTRIM(MBMCName) AS CoveredName, RTRIM(MBMGroupCompany) AS GroupCompany, RTRIM(MBMDepartment) AS Department,
        RTRIM(MBMInvoiceNo) AS InvoiceNo, MBMInvoiceDate AS InvoiceDate, RTRIM(PLNCode) AS PlanCode, RTRIM(INSCode) AS InsCode, MBMPayorEffDate AS EffDate, MBMPayorExpDate AS ExpDate, RTRIM(Type) AS AdultChild,
        MBMBasicMCO AS BasicMco, MBMMCOFee AS McoFee, MBMPremium AS Premium, RTRIM(MBMIcBcPp) AS IcNo
    FROM dbo.VIEW_MCO_LISTING_All2
    WHERE ((@invoiceNo IS NOT NULL AND MBMInvoiceNo = @invoiceNo)
        OR (@invoiceNo IS NULL AND @bordxDate IS NOT NULL AND MBMBordxDate = @bordxDate AND MBMBatchNo = @batch AND PAYCode = @payor AND (MBMInvoiceNo IS NULL OR MBMInvoiceNo = N''))
        OR (@invoiceNo IS NULL AND @bordxDate IS NULL AND MBMBordxDate >= @bordxFrom AND MBMBordxDate <= @bordxTo AND PAYCode = @payor AND (MBMInvoiceNo IS NULL OR MBMInvoiceNo = N'')))
      AND (@groupCompany IS NULL OR MBMGroupCompany = @groupCompany)
    ORDER BY MemberNo, CoverId
    OPTION (RECOMPILE);
END
GO

-- cboMCOPayor_Click: the group companies of a payor (SearchComboListing 1), for the By Department / By Group Company options.
CREATE OR ALTER PROCEDURE genisis.McoInvoice_GroupCompanies
    @payor nvarchar(4)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT RTRIM(MBMGroupCompany) AS GroupCompany FROM dbo.MBMOthers o INNER JOIN dbo.MBM m ON m.MBMNumber = o.MBMNumber
    WHERE m.PAYCode = @payor AND RTRIM(ISNULL(o.MBMGroupCompany, N'')) <> N'' ORDER BY GroupCompany;
END
GO

-- GetPayorInfo / GetThirdPartyInfo: who the invoice is made out to. A payor whose BillingPartyStatus is C is billed through its broker (the Broker table; invoice prefix S), the others to themselves (prefix
-- PAY.MCOInvoicePrefix, S when it has none).
CREATE OR ALTER PROCEDURE genisis.McoInvoice_PayorInfo
    @payor nvarchar(4)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @third bit = CASE WHEN EXISTS (SELECT 1 FROM dbo.PAY WHERE PAYCode = @payor AND RTRIM(ISNULL(BillingPartyStatus, N'')) = N'C') THEN 1 ELSE 0 END;
    IF @third = 1 AND EXISTS (SELECT 1 FROM dbo.Broker WHERE PAYCode = @payor)
        SELECT TOP (1) RTRIM(PAYCode) AS Code, RTRIM(BrokerCompanyName) AS Name, RTRIM(BrokerAddress1) AS Address1, RTRIM(BrokerAddress2) AS Address2, RTRIM(BrokerAddress3) AS Address3,
            RTRIM(BrokerMAINContact) AS Contact, RTRIM(BrokerTELno) AS Telephone, RTRIM(BrokerFAXno) AS Fax, CAST(N'S' AS nvarchar(5)) AS Prefix, CAST(1 AS bit) AS ThirdParty
        FROM dbo.Broker WHERE PAYCode = @payor;
    ELSE
        SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name, RTRIM(PAYAddress1) AS Address1, RTRIM(PAYAddress2) AS Address2, RTRIM(PAYAddress3) AS Address3, RTRIM(PAYMainContact) AS Contact,
            RTRIM(PAYTELno) AS Telephone, RTRIM(PAYFAXno) AS Fax, CAST(CASE WHEN RTRIM(ISNULL(MCOInvoicePrefix, '')) = '' THEN 'S' ELSE RTRIM(MCOInvoicePrefix) END AS nvarchar(5)) AS Prefix, CAST(0 AS bit) AS ThirdParty
        FROM dbo.PAY WHERE PAYCode = @payor;
END
GO

-- UpdateINVPrintingFile + UpdateMCOPending + UPdateMBMInvoice for one member or covered person: the invoice number, date and user on the member (MBMOthers, cover 00) or the covered person
-- (MBMCoveredPersons), the member's MCOPending row to status B, and a row in MBMMCOInvoice.
CREATE OR ALTER PROCEDURE genisis.McoInvoice_Stamp
    @memberNo nvarchar(80), @coverId nvarchar(10), @invoiceNo nvarchar(40), @invoiceDate datetime, @userCode nvarchar(60), @planCode nvarchar(4) = NULL, @insCode nvarchar(2) = NULL,
    @effDate datetime = NULL, @expDate datetime = NULL, @adultChild char(1) = NULL, @basicMco money = 0, @mcoFee money = 0
AS
BEGIN
    SET NOCOUNT ON;
    IF @coverId = N'00'
        UPDATE dbo.MBMOthers SET MBMInvoiceDate = @invoiceDate, MBMInvoiceNo = @invoiceNo, MBMInvoiceBy = @userCode WHERE MBMNumber = @memberNo;
    ELSE
        UPDATE dbo.MBMCoveredPersons SET MBMInvoiceDate = @invoiceDate, MBMInvoiceNo = @invoiceNo, MBMInvoiceBy = @userCode WHERE MBMCNumber = @memberNo AND MBMCCoverID = @coverId;
    UPDATE genisis.Maint_MCOPending SET Status = 'B', LastUpdateDate = @invoiceDate, LastUpdateUser = @userCode WHERE MBMNumber = @memberNo;
    INSERT dbo.MBMMCOInvoice (MBMNumber, MBMCovID, MCOInvNo, MCOInvDate, MCoInvBy, PlanCode, InsCode, MBMEffDate, MBMExpDate, AdultChild, BasicMCO, BillMCOAmt)
    VALUES (@memberNo, @coverId, @invoiceNo, @invoiceDate, @userCode, @planCode, @insCode, @effDate, @expDate, @adultChild, @basicMco, @mcoFee);
END
GO
