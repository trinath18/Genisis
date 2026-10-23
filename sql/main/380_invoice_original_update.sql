-- Invoice > Original Invoice Update (legacy frmUpdateInvoice, "Update Invoice"). The desktop opened a recordset on the first ClaimInvoice row of a
-- case and worksheet version and edited it in place; here a version's invoices are listed (most versions have one, 4,899 of 75,859 have more, and the
-- desktop could only ever reach the first of those) and one is updated by its key, with typed parameters.

-- cmdShow_Click / ShowInvoice: the invoices of a case and worksheet version, with the hospital the invoice is paid to. The hospital is found by its code;
-- the desktop found it by name, which is not unique.
CREATE OR ALTER PROCEDURE genisis.InvoiceOriginal_Find
    @cas nvarchar(30), @version nvarchar(4)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(i.INVIndex AS int) AS Id, RTRIM(i.CASNumber) AS CaseNo, RTRIM(i.INVWSversion) AS Version, RTRIM(i.InvNo) AS InvoiceNo,
           NULLIF(i.InvDate, '19000101') AS InvoiceDate, NULLIF(i.INVOrigRecDate, '19000101') AS OriginalReceivedDate, RTRIM(i.INVOrigRecStatus) AS OriginalStatus,
           RTRIM(i.INVPayTo) AS PayTo, RTRIM(i.INVPayee) AS HospitalCode, RTRIM(h.HOSAccCode) AS HospitalAccCode, RTRIM(h.HOSName) AS HospitalName
    FROM genisis.WS_ClaimInvoice i
    LEFT JOIN dbo.HOS h ON h.HOSCode = i.INVPayee
    WHERE i.CASNumber = @cas AND i.INVWSversion = @version
    ORDER BY i.INVIndex;
END
GO

-- cmdUpdate_Click: the invoice number, the date the original invoice was received, the invoice date and the hospital, and the original-receipt mark ('Y').
-- The hospital changes only when the code is a hospital (the desktop changed it only when the name it looked up was found); a blank invoice date leaves the
-- date as it was (the desktop assigned the mask text to the date column and failed). Returns the number of invoices changed.
CREATE OR ALTER PROCEDURE genisis.InvoiceOriginal_Update
    @id int, @invoiceNo nvarchar(100), @invoiceDate datetime = NULL, @originalDate datetime, @hospital nvarchar(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE i
    SET InvNo = @invoiceNo, INVOrigRecDate = @originalDate, InvDate = ISNULL(@invoiceDate, i.InvDate), INVOrigRecStatus = 'Y',
        INVPayee = CASE WHEN @hospital IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.HOS WHERE HOSCode = @hospital) THEN @hospital ELSE i.INVPayee END
    FROM genisis.WS_ClaimInvoice i
    WHERE i.INVIndex = @id;
    SELECT @@ROWCOUNT;
END
GO
