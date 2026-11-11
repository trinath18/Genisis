-- Contribution Billing > MCO Reinstatement (legacy frmMCOInvoiceReinstatement, "MCO Reinstatement Invoice"): the MCO fees of members whose cover was reinstated, listed by endorsement bordx date and
-- batch for a payor (or by invoice number), and the invoice for them. The desktop called SP_MCOFeesReinstatement with the fields and the WHERE as text, updated MBMReinstatement member by member and
-- took the next invoice number from MBMMCOINVQ with an optimistic recordset; here each step is a procedure with typed parameters and the number is taken under an update lock, so two people
-- printing at once cannot get the same one.

-- MCOListing: the principals and covered persons of the batch of a payor, or of an invoice. The batch is compared as text (the column is char(5); one row holds a value that is not a number).
CREATE OR ALTER PROCEDURE genisis.McoReinstatement_Search
    @payor nvarchar(4) = NULL, @bordxDate datetime = NULL, @batch nvarchar(5) = NULL, @invoiceNo nvarchar(40) = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT TOP (@max) RTRIM(MBMNumber) AS MemberNo, RTRIM(CASE WHEN MBMCRELCode <> N'' THEN MBMCCoverID ELSE MBMCOVID END) AS CoverId,
        RTRIM(CASE WHEN MBMCRELCode <> N'' THEN MBMCName ELSE MBMName END) AS MemberName,
        RTRIM(CASE WHEN MBMCRELCode <> N'' THEN MBMCRELCode ELSE N'P' END) AS Relation,
        RTRIM(CASE WHEN MBMCRELCode <> N'' THEN Type ELSE N'Adult' END) AS AdultChild,
        MBMEndtBordxDate AS BordxDate, RTRIM(MBMEndtBordxBatch) AS BatchNo, RTRIM(MBMPolicyNo) AS PolicyNo, MBMPayorEffDate AS EffDate, MBMPayorExpDate AS ExpDate,
        RTRIM(MBMInvoiceNo) AS InvoiceNo, MBMInvoiceDate AS InvoiceDate, MBMMCOFee AS McoFee, RTRIM(MBMIcBcPp) AS IcNo, RTRIM(CASE WHEN MBMCRELCode <> N'' THEN MBMCICBCPPNo ELSE MBMIcBcPp END) AS PersonIc,
        RTRIM(MBMGroupCompany) AS GroupCompany, DayInsured AS DaysInsured, CASE WHEN MBMCRELCode <> N'' THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsCovered, RTRIM(PAYCode) AS PayorCode
    FROM dbo.VIEW_MBM_MCOReinstatement
    WHERE (@invoiceNo IS NOT NULL AND MBMInvoiceNo = @invoiceNo)
       OR (@invoiceNo IS NULL AND MBMEndtBordxDate = @bordxDate AND LTRIM(RTRIM(MBMEndtBordxBatch)) = @batch AND PAYCode = @payor)
    ORDER BY MemberNo, CoverId
    OPTION (RECOMPILE);
END
GO

-- GetPayorInfo: the payor's name, address, contact and the prefix of its MCO invoice numbers (S when it has none).
CREATE OR ALTER PROCEDURE genisis.McoReinstatement_PayorInfo
    @payor nvarchar(4)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name, RTRIM(PAYAddress1) AS Address1, RTRIM(PAYAddress2) AS Address2, RTRIM(PAYAddress3) AS Address3, RTRIM(PAYMainContact) AS Contact,
        RTRIM(PAYTELno) AS Telephone, RTRIM(PAYFAXno) AS Fax, CASE WHEN RTRIM(ISNULL(MCOInvoicePrefix, '')) = '' THEN 'S' ELSE RTRIM(MCOInvoicePrefix) END AS Prefix
    FROM dbo.PAY WHERE PAYCode = @payor;
END
GO

-- MCOInvoiceExcel: the invoice lines, one per bordx batch, insurance code and adult / child: principals (View_MCOReinstatement_Invoice_P), covered persons and the MCO fee
-- (View_MCOReinstatement_Invoice_CP), which the desktop wrote side by side by position and are joined by their keys here.
CREATE OR ALTER PROCEDURE genisis.McoReinstatement_InvoiceLines
    @payor nvarchar(4) = NULL, @bordxDate datetime = NULL, @batch int = NULL, @invoiceNo nvarchar(40) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.MBMEndtBordxDate AS BordxDate, p.MBMEndtBordxBatch AS BatchNo, RTRIM(p.INSCode) AS InsCode, RTRIM(p.MBMAdultChild) AS AdultChild, p.TotalPrincipal AS Principals,
        cp.TotalCoveredPersons AS CoveredPersons, cp.MBMMCOFee AS McoFee, RTRIM(p.MBMInvoiceNo) AS InvoiceNo, p.MBMInvoiceDate AS InvoiceDate
    FROM dbo.View_MCOReinstatement_Invoice_P p
    LEFT JOIN dbo.View_MCOReinstatement_Invoice_CP cp ON cp.MBMEndtBordxDate = p.MBMEndtBordxDate AND cp.MBMEndtBordxBatch = p.MBMEndtBordxBatch AND cp.PAYCode = p.PAYCode
        AND cp.INSCode = p.INSCode AND cp.MBMAdultChild = p.MBMAdultChild AND ISNULL(cp.MBMInvoiceNo, N'') = ISNULL(p.MBMInvoiceNo, N'')
    WHERE (@invoiceNo IS NOT NULL AND p.MBMInvoiceNo = @invoiceNo)
       OR (@invoiceNo IS NULL AND p.MBMEndtBordxDate = @bordxDate AND p.MBMEndtBordxBatch = @batch AND p.PAYCode = @payor)
    ORDER BY p.INSCode, p.MBMAdultChild
    OPTION (RECOMPILE);
END
GO

-- IncInvoiceNo: the next MCO invoice number. A payor whose prefix is S shares the counter of the first row of MBMMCOINVQ (the one with no payor), any other payor has its own row, created at 00001 when
-- it has none. The row's own prefix, when it has one, is the one used. The row is locked until the transaction ends.
CREATE OR ALTER PROCEDURE genisis.McoReinstatement_NextInvoice
    @payor nvarchar(4), @prefix nvarchar(5)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @index int, @number nvarchar(12), @rowPrefix nvarchar(5), @next nvarchar(12);
    IF @prefix = N'S'
        SELECT TOP (1) @index = MCOInvIndex, @number = MCOInvoice_No, @rowPrefix = RTRIM(Prefix) FROM dbo.MBMMCOINVQ WITH (UPDLOCK, HOLDLOCK) ORDER BY MCOInvIndex;
    ELSE
        SELECT TOP (1) @index = MCOInvIndex, @number = MCOInvoice_No, @rowPrefix = RTRIM(Prefix) FROM dbo.MBMMCOINVQ WITH (UPDLOCK, HOLDLOCK) WHERE PAYCode = @payor ORDER BY MCOInvIndex;
    IF @index IS NULL
    BEGIN
        INSERT dbo.MBMMCOINVQ (PAYCode, Prefix, MCOInvoice_No) VALUES (@payor, @prefix, N'00001');
        SELECT @prefix + N'00001' AS InvoiceNo;
        RETURN;
    END
    SET @next = RIGHT(N'00000' + CAST(CAST(@number AS int) + 1 AS nvarchar(12)), CASE WHEN CAST(@number AS int) + 1 > 99999 THEN 12 ELSE 5 END);
    UPDATE dbo.MBMMCOINVQ SET MCOInvoice_No = @next, Prefix = CASE WHEN ISNULL(@rowPrefix, N'') = N'' THEN N'S' ELSE Prefix END WHERE MCOInvIndex = @index;
    SELECT CASE WHEN ISNULL(@rowPrefix, N'') = N'' THEN @prefix ELSE @rowPrefix END + @next AS InvoiceNo;
END
GO

-- UpdateINVPrintingFile: stamps the invoice number, date and user on one reinstatement row (a member's row, or a covered person's). The desktop updated every row of the member whatever its batch;
-- only the batch listed is stamped here. Returns the rows changed.
CREATE OR ALTER PROCEDURE genisis.McoReinstatement_Stamp
    @memberNo nvarchar(40), @coverId nvarchar(4), @bordxDate datetime, @batch nvarchar(5), @invoiceNo nvarchar(40), @invoiceDate datetime, @userCode nvarchar(60)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.MBMReinstatement SET MBMInvoiceNo = @invoiceNo, MBMInvoiceDate = @invoiceDate, MBMInvoiceBy = @userCode
    WHERE MBMNumber = @memberNo AND MBMCoverID = @coverId AND MBMEndtBordxDate = @bordxDate AND LTRIM(RTRIM(MBMEndtBordxBatch)) = @batch AND (MBMInvoiceNo IS NULL OR MBMInvoiceNo = N'');
    SELECT @@ROWCOUNT AS Updated;
END
GO
