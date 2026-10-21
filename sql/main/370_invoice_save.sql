-- Invoice Processing (legacy frmInvoice), write side. The save itself calls the legacy parameterized procedures of HISWorkSheet through the genisis.WS_* synonyms (000_setup.sql);
-- what is here are the reads the form needs and the two writes that have no legacy procedure (case remarks, remove).

-- getVersion: the worksheet version a new invoice is attached to: the case's last completed version plus one, or 1.
CREATE OR ALTER PROCEDURE genisis.Invoice_NextVersion
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ISNULL(MAX(w.ClaimVersion), 0) + 1 AS NextVersion FROM genisis.WS_Claim w WHERE w.CASNumber = @cas AND RTRIM(w.ClaimStatus) = N'C';
END
GO

-- Form_Load: the lists of the form. Stay types are PayType without the C (claim) and R (reimbursement) ones, as SearchStayType has them.
CREATE OR ALTER PROCEDURE genisis.Invoice_StayTypes
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(PayTypeCode) AS Code, RTRIM(PayTypeName) AS Name FROM dbo.PayType
    WHERE SUBSTRING(PayTypeCode, 1, 1) <> N'C' AND SUBSTRING(PayTypeCode, 1, 1) <> N'R' ORDER BY PayTypeCode;
END
GO

CREATE OR ALTER PROCEDURE genisis.Invoice_ReceiptModes
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(RecModeCode) AS Code, RTRIM(RecModeDesc) AS Name FROM genisis.Maint_InvRecMode ORDER BY RecModeCode;
END
GO

CREATE OR ALTER PROCEDURE genisis.Invoice_RoomTypes
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(RoomType) AS Code FROM genisis.Maint_HOSRoomType ORDER BY RoomType;
END
GO

CREATE OR ALTER PROCEDURE genisis.Invoice_Hospitals
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(HOSCode) AS Code, RTRIM(HOSName) AS Name FROM dbo.HOS ORDER BY HOSName;
END
GO

-- lstInvoice_Click: one invoice with every amount, as column name and value, so the form can be filled and saved again without the web page knowing the 106 columns.
CREATE OR ALTER PROCEDURE genisis.Invoice_Form
    @invoice int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM genisis.WS_ClaimInvoice WHERE INVIndex = @invoice;
    SELECT * FROM genisis.WS_ClaimInvoiceActual WHERE INVIndex = @invoice;
    SELECT * FROM genisis.WS_ClaimInvoiceActualOthers WHERE INVIndex = @invoice;
    SELECT * FROM genisis.WS_ClaimInvoiceNotCover WHERE INVIndex = @invoice;
END
GO

CREATE OR ALTER PROCEDURE genisis.Invoice_Remarks
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Remarks FROM genisis.WS_ClaimInvoiceRemarks WHERE CASNumber = @cas;
END
GO

-- SaveINVRemarks: one remark per case, added or replaced. An empty remark is not added for a case that has none (the desktop adds a blank row; it means nothing and every invoice save would leave one).
CREATE OR ALTER PROCEDURE genisis.Invoice_SaveRemarks
    @cas nvarchar(15),
    @remarks nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM genisis.WS_ClaimInvoiceRemarks WHERE CASNumber = @cas) AND ISNULL(@remarks, N'') = N''
        RETURN;
    IF EXISTS (SELECT 1 FROM genisis.WS_ClaimInvoiceRemarks WHERE CASNumber = @cas)
        UPDATE genisis.WS_ClaimInvoiceRemarks SET Remarks = ISNULL(@remarks, N'') WHERE CASNumber = @cas;
    ELSE
        INSERT INTO genisis.WS_ClaimInvoiceRemarks (CASNumber, Remarks) VALUES (@cas, ISNULL(@remarks, N''));
END
GO

-- CmdRemove_Click: the desktop deletes ClaimInvoice, ClaimInvoiceActual and ClaimInvoiceNotCover; the other two rows of the invoice (the entries and the GST amounts) are deleted too so none is left behind.
CREATE OR ALTER PROCEDURE genisis.Invoice_Remove
    @invoice int
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM genisis.WS_ClaimInvoiceActualGST WHERE INVIndex = @invoice;
    DELETE FROM genisis.WS_ClaimInvoiceActualOthers WHERE INVIndex = @invoice;
    DELETE FROM genisis.WS_ClaimInvoiceNotCover WHERE INVIndex = @invoice;
    DELETE FROM genisis.WS_ClaimInvoiceActual WHERE INVIndex = @invoice;
    DELETE FROM genisis.WS_ClaimInvoice WHERE INVIndex = @invoice;
END
GO

-- The new invoice's key: the legacy insert does not return it, so it is read back by case and number (the desktop takes the highest key of the table).
CREATE OR ALTER PROCEDURE genisis.Invoice_Key
    @cas nvarchar(15),
    @invoiceNo nvarchar(30)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) INVIndex AS InvoiceKey FROM genisis.WS_ClaimInvoice WHERE CASNumber = @cas AND RTRIM(InvNo) = RTRIM(@invoiceNo) ORDER BY INVIndex DESC;
END
GO

-- The payee hospital: its name (the form tests the generic provider names) and whether a discount is due (AX).
CREATE OR ALTER PROCEDURE genisis.Invoice_Hospital
    @code nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(HOSCode) AS Code, RTRIM(HOSName) AS Name, RTRIM(HOSDiscInd) AS DiscountIndicator FROM dbo.HOS WHERE RTRIM(HOSCode) = RTRIM(@code);
END
GO

-- saveActual: the hospital sums are kept for plans 2 to 5 and for new SM plans (PLN.NewSMPlan = Y).
CREATE OR ALTER PROCEDURE genisis.Invoice_PlanIsNewSm
    @plan nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.PLN WHERE RTRIM(PLNCode) = RTRIM(@plan) AND RTRIM(NewSMPlan) = N'Y') THEN 1 ELSE 0 END AS bit) AS NewSm;
END
GO
