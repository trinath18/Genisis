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

-- GetExcessCode: the next sequence of the case's excess letters (EXC_SEQ: one row per case, 1 the first time) and the letter number E + case + - + sequence. The desktop calls it at every save
-- that leaves the member an excess or a limit exceeded, before it looks whether the case has a letter, so the sequence moves on at each such save (773 of the 805 cases with two or more
-- such versions have a sequence above 1) while the letter keeps the number it was first given.
CREATE OR ALTER PROCEDURE genisis.WorksheetSave_NextExcessNumber
    @cas varchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @seq int;
    IF EXISTS (SELECT 1 FROM genisis.WS_ExcessSequence WITH (UPDLOCK, HOLDLOCK) WHERE CASNumber = @cas)
    BEGIN
        UPDATE genisis.WS_ExcessSequence SET @seq = EXCSequence = EXCSequence + 1 WHERE CASNumber = @cas;
    END
    ELSE
    BEGIN
        INSERT genisis.WS_ExcessSequence (CASNumber, EXCSequence) VALUES (@cas, 1);
        SET @seq = 1;
    END
    SELECT 'E' + RTRIM(@cas) + '-' + CAST(@seq AS varchar(10));
END
GO

-- saveExcessLetter: the case's letter is added when it has none (CAS_Excess holds one row per case). Returns 1 when the letter was added and 0 when the case already had one.
CREATE OR ALTER PROCEDURE genisis.WorksheetSave_ExcessLetter
    @cas varchar(15),
    @number varchar(15),
    @date datetime,
    @invoiceNo varchar(1000) = NULL,
    @invoiceAmount money = NULL,
    @excessAmount money = NULL,
    @issuedBy nvarchar(50) = NULL,
    @EXCBoardAmt nvarchar(15) = NULL,
    @EXCICUAmt nvarchar(15) = NULL,
    @EXCServices nvarchar(15) = NULL,
    @EXCTheatre nvarchar(15) = NULL,
    @EXCPreHSAmt nvarchar(15) = NULL,
    @EXCSurgicalAmt nvarchar(15) = NULL,
    @EXCAna nvarchar(15) = NULL,
    @EXCNSPreHOS nvarchar(15) = NULL,
    @EXCPhysician nvarchar(15) = NULL,
    @EXCPost nvarchar(15) = NULL,
    @EXCEmergency nvarchar(15) = NULL,
    @EXCAmbulance nvarchar(15) = NULL,
    @EXCOutpatient nvarchar(15) = NULL,
    @EXCMonthly nvarchar(15) = NULL,
    @EXCOrgan nvarchar(15) = NULL,
    @EXCTax nvarchar(15) = NULL,
    @EXCGuardianAMT nvarchar(15) = NULL,
    @EXCMedical nvarchar(15) = NULL,
    @EXCPhone nvarchar(15) = NULL,
    @EXCAdmin nvarchar(15) = NULL,
    @EXCOthers1 nvarchar(15) = NULL,
    @EXCBoardTaxAmt nvarchar(15) = NULL,
    @EXCICUTaxAmt nvarchar(15) = NULL,
    @EXCLodgerTaxAmt nvarchar(15) = NULL,
    @EXCTotalExceed nvarchar(15) = NULL,
    @EXCMRI nvarchar(15) = NULL,
    @EXCMPreHSAmt nvarchar(15) = NULL,
    @EXCCoPayment nvarchar(15) = NULL,
    @EXCPreHosp nvarchar(15) = NULL,
    @EXCUndExcess nvarchar(15) = NULL,
    @othersDesc nvarchar(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM genisis.WS_ExcessLetter WITH (UPDLOCK, HOLDLOCK) WHERE CASNumber = @cas)
    BEGIN
        SELECT 0;
        RETURN;
    END
    INSERT genisis.WS_ExcessLetter (EXCNumber, CASNumber, EXCDate, INVNo, INVAmt, ExcessAmt, EXCIssuedBy, EXCIssuedDate, EXCBoardAmt, EXCICUAmt, EXCServices, EXCTheatre, EXCPreHSAmt, EXCSurgicalAmt, EXCAna, EXCNSPreHOS, EXCPhysician, EXCPost, EXCEmergency, EXCAmbulance, EXCOutpatient, EXCMonthly, EXCOrgan, EXCTax, EXCGuardianAMT, EXCMedical, EXCPhone, EXCAdmin, EXCOthers1, EXCBoardTaxAmt, EXCICUTaxAmt, EXCLodgerTaxAmt, EXCTotalExceed, EXCMRI, EXCMPreHSAmt, EXCCoPayment, EXCPreHosp, EXCUndExcess, EXCOthersDesc)
    VALUES (@number, @cas, @date, @invoiceNo, @invoiceAmount, @excessAmount, @issuedBy, @date, @EXCBoardAmt, @EXCICUAmt, @EXCServices, @EXCTheatre, @EXCPreHSAmt, @EXCSurgicalAmt, @EXCAna, @EXCNSPreHOS, @EXCPhysician, @EXCPost, @EXCEmergency, @EXCAmbulance, @EXCOutpatient, @EXCMonthly, @EXCOrgan, @EXCTax, @EXCGuardianAMT, @EXCMedical, @EXCPhone, @EXCAdmin, @EXCOthers1, @EXCBoardTaxAmt, @EXCICUTaxAmt, @EXCLodgerTaxAmt, @EXCTotalExceed, @EXCMRI, @EXCMPreHSAmt, @EXCCoPayment, @EXCPreHosp, @EXCUndExcess, @othersDesc);
    SELECT 1;
END
GO
