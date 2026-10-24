-- Worksheet > Worksheet Registration, the save (legacy frmWorksheetRegGST2015, cmdApprove_Click). The rows of the version are written by the desktop's own parameterised procedures
-- (reached through the genisis.WS_Insert_WS_* synonyms of 000_setup.sql); the three routines below are the ones the desktop wrote as recordset edits or text.

-- SaveClaim: after the version row of Claim, the case is no longer pending (CAS.CASPending = 'N'). Returns how many cases were changed.
CREATE OR ALTER PROCEDURE genisis.WorksheetSave_ClosePending
    @cas nvarchar(30)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.CAS SET CASPending = 'N' WHERE CASNumber = @cas;
    SELECT @@ROWCOUNT;
END
GO

-- The desktop's Insert_WS_Claim_2012 takes the bank account number as nvarchar(15) although Claim.CLMAccountNo holds 30 characters, so a longer number (16 digits is usual for some
-- banks; 375 of the numbers saved since July 2020 are 16 to 19 long, after being corrected through Adjustment's Update) is cut to 15 characters without a word. The save writes the
-- whole number right after the procedure. Returns how many versions were changed.
CREATE OR ALTER PROCEDURE genisis.WorksheetSave_SetAccountNo
    @cas nvarchar(30), @version int, @accountNo nvarchar(30)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.WS_Claim SET CLMAccountNo = @accountNo WHERE CASNumber = @cas AND ClaimVersion = @version;
    SELECT @@ROWCOUNT;
END
GO

-- UpdateInvoice: the invoices of the case that belong to the version are marked as worksheeted; a void worksheet (status XR) also marks them bordxed under the reference VOID.
-- Returns how many invoices were changed.
CREATE OR ALTER PROCEDURE genisis.WorksheetSave_MarkInvoices
    @cas nvarchar(30), @version int, @void bit
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.WS_ClaimInvoice
    SET INVWSed = 1,
        INVBordxRefNo = CASE WHEN @void = 1 THEN 'VOID' ELSE INVBordxRefNo END,
        INVBordxed = CASE WHEN @void = 1 THEN '1' ELSE INVBordxed END
    WHERE CASNumber = @cas AND INVWSversion = CAST(@version AS nvarchar(4));
    SELECT @@ROWCOUNT;
END
GO

-- UpdateCasReserve: the case's reserve amount is cleared once a worksheet is saved. Returns how many cases were changed.
CREATE OR ALTER PROCEDURE genisis.WorksheetSave_ClearReserve
    @cas nvarchar(30)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.CAS SET CASReserveAmount = 0 WHERE CASNumber = @cas;
    SELECT @@ROWCOUNT;
END
GO
