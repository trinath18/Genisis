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

-- savePayMBMStatus: the Y/N flag of each worksheet row of a version (ClaimPayMBMStatus has one row per version). The desktop edited a recordset: a row is added when the version has none, otherwise
-- its columns are set. A column given as NULL (the ones the plan layout does not write) is left as it is. Returns 1 when the row was added and 0 when it was changed.
CREATE OR ALTER PROCEDURE genisis.WorksheetSave_PayMbmStatus
    @cas nvarchar(30), @version smallint,
    @CLMBoardAmt char(1) = NULL,
    @CLMICUAmt char(1) = NULL,
    @CLMServices char(1) = NULL,
    @CLMTheatre char(1) = NULL,
    @CLMMRI char(1) = NULL,
    @CLMAccPostAmt char(1) = NULL,
    @CLMAmbulance char(1) = NULL,
    @CLMOutpatient char(1) = NULL,
    @CLMMonthly char(1) = NULL,
    @CLMOrgan char(1) = NULL,
    @CLMCashDays char(1) = NULL,
    @CLMGuardianAmt char(1) = NULL,
    @CLMMedical char(1) = NULL,
    @CLMPhone char(1) = NULL,
    @CLMAdmin char(1) = NULL,
    @CLMOthers1 char(1) = NULL,
    @CLMOthersNotCoPay char(1) = NULL,
    @CLMBoardTaxAmt char(1) = NULL,
    @CLMICUTaxAmt char(1) = NULL,
    @CLMLodgerTaxAmt char(1) = NULL,
    @CLMPreHSAmt char(1) = NULL,
    @CLMPreHosp char(1) = NULL,
    @CLMSurgicalAmt char(1) = NULL,
    @CLMAna char(1) = NULL,
    @CLMNSPreHos char(1) = NULL,
    @CLMPhysician char(1) = NULL,
    @CLMPost char(1) = NULL,
    @CLMMPreHSAmt char(1) = NULL,
    @CLMCOPreHSSum char(1) = NULL,
    @CLMCOMPreHSSum char(1) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM genisis.WS_ClaimPayMBMStatus WITH (UPDLOCK, HOLDLOCK) WHERE CASNumber = @cas AND CLMVersion = @version)
    BEGIN
        UPDATE genisis.WS_ClaimPayMBMStatus SET
        CLMBoardAmt = COALESCE(@CLMBoardAmt, CLMBoardAmt),
        CLMICUAmt = COALESCE(@CLMICUAmt, CLMICUAmt),
        CLMServices = COALESCE(@CLMServices, CLMServices),
        CLMTheatre = COALESCE(@CLMTheatre, CLMTheatre),
        CLMMRI = COALESCE(@CLMMRI, CLMMRI),
        CLMAccPostAmt = COALESCE(@CLMAccPostAmt, CLMAccPostAmt),
        CLMAmbulance = COALESCE(@CLMAmbulance, CLMAmbulance),
        CLMOutpatient = COALESCE(@CLMOutpatient, CLMOutpatient),
        CLMMonthly = COALESCE(@CLMMonthly, CLMMonthly),
        CLMOrgan = COALESCE(@CLMOrgan, CLMOrgan),
        CLMCashDays = COALESCE(@CLMCashDays, CLMCashDays),
        CLMGuardianAmt = COALESCE(@CLMGuardianAmt, CLMGuardianAmt),
        CLMMedical = COALESCE(@CLMMedical, CLMMedical),
        CLMPhone = COALESCE(@CLMPhone, CLMPhone),
        CLMAdmin = COALESCE(@CLMAdmin, CLMAdmin),
        CLMOthers1 = COALESCE(@CLMOthers1, CLMOthers1),
        CLMOthersNotCoPay = COALESCE(@CLMOthersNotCoPay, CLMOthersNotCoPay),
        CLMBoardTaxAmt = COALESCE(@CLMBoardTaxAmt, CLMBoardTaxAmt),
        CLMICUTaxAmt = COALESCE(@CLMICUTaxAmt, CLMICUTaxAmt),
        CLMLodgerTaxAmt = COALESCE(@CLMLodgerTaxAmt, CLMLodgerTaxAmt),
        CLMPreHSAmt = COALESCE(@CLMPreHSAmt, CLMPreHSAmt),
        CLMPreHosp = COALESCE(@CLMPreHosp, CLMPreHosp),
        CLMSurgicalAmt = COALESCE(@CLMSurgicalAmt, CLMSurgicalAmt),
        CLMAna = COALESCE(@CLMAna, CLMAna),
        CLMNSPreHos = COALESCE(@CLMNSPreHos, CLMNSPreHos),
        CLMPhysician = COALESCE(@CLMPhysician, CLMPhysician),
        CLMPost = COALESCE(@CLMPost, CLMPost),
        CLMMPreHSAmt = COALESCE(@CLMMPreHSAmt, CLMMPreHSAmt),
        CLMCOPreHSSum = COALESCE(@CLMCOPreHSSum, CLMCOPreHSSum),
        CLMCOMPreHSSum = COALESCE(@CLMCOMPreHSSum, CLMCOMPreHSSum)
        WHERE CASNumber = @cas AND CLMVersion = @version;
        SELECT 0;
    END
    ELSE
    BEGIN
        INSERT genisis.WS_ClaimPayMBMStatus (CASNumber, CLMVersion, CLMBoardAmt, CLMICUAmt, CLMServices, CLMTheatre, CLMMRI, CLMAccPostAmt, CLMAmbulance, CLMOutpatient, CLMMonthly, CLMOrgan, CLMCashDays, CLMGuardianAmt, CLMMedical, CLMPhone, CLMAdmin, CLMOthers1, CLMOthersNotCoPay, CLMBoardTaxAmt, CLMICUTaxAmt, CLMLodgerTaxAmt, CLMPreHSAmt, CLMPreHosp, CLMSurgicalAmt, CLMAna, CLMNSPreHos, CLMPhysician, CLMPost, CLMMPreHSAmt, CLMCOPreHSSum, CLMCOMPreHSSum)
        VALUES (@cas, @version, @CLMBoardAmt, @CLMICUAmt, @CLMServices, @CLMTheatre, @CLMMRI, @CLMAccPostAmt, @CLMAmbulance, @CLMOutpatient, @CLMMonthly, @CLMOrgan, @CLMCashDays, @CLMGuardianAmt, @CLMMedical, @CLMPhone, @CLMAdmin, @CLMOthers1, @CLMOthersNotCoPay, @CLMBoardTaxAmt, @CLMICUTaxAmt, @CLMLodgerTaxAmt, @CLMPreHSAmt, @CLMPreHosp, @CLMSurgicalAmt, @CLMAna, @CLMNSPreHos, @CLMPhysician, @CLMPost, @CLMMPreHSAmt, @CLMCOPreHSSum, @CLMCOMPreHSSum);
        SELECT 1;
    END
END
GO
