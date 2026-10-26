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

-- saveSameLimit: what is left of the case's limits and days after a worksheet. ClaimSameLimit has one row per case: the desktop adds it with the case's first worksheet and then changes it with every
-- later one. A column given as NULL (the ones the plan layout does not write) is left as it is. Returns 1 when the row was added and 0 when it was changed.
CREATE OR ALTER PROCEDURE genisis.WorksheetSave_SameLimit
    @cas nvarchar(30),
    @CLMBoardAmt nvarchar(30) = NULL,
    @CLMICUAmt nvarchar(30) = NULL,
    @CLMServices nvarchar(30) = NULL,
    @CLMTheatre nvarchar(30) = NULL,
    @CLMMRI nvarchar(30) = NULL,
    @CLMAccPostAmt nvarchar(30) = NULL,
    @CLMAmbulance nvarchar(30) = NULL,
    @CLMOutpatient nvarchar(30) = NULL,
    @CLMMonthly nvarchar(30) = NULL,
    @CLMOrgan nvarchar(30) = NULL,
    @CLMTax nvarchar(30) = NULL,
    @CLMGuardianAmt nvarchar(30) = NULL,
    @CLMMedical nvarchar(30) = NULL,
    @CLMPhone nvarchar(30) = NULL,
    @CLMAdmin nvarchar(30) = NULL,
    @CLMOthers1 nvarchar(30) = NULL,
    @CLMBoardTaxAmt nvarchar(30) = NULL,
    @CLMICUTaxAmt nvarchar(30) = NULL,
    @CLMLodgerTaxAmt nvarchar(30) = NULL,
    @CLMPreHSAmt nvarchar(30) = NULL,
    @CLMPreHosp nvarchar(30) = NULL,
    @CLMSurgicalAmt nvarchar(30) = NULL,
    @CLMAna nvarchar(30) = NULL,
    @CLMNSPreHos nvarchar(30) = NULL,
    @CLMPhysician nvarchar(30) = NULL,
    @CLMPost nvarchar(30) = NULL,
    @CLMMPreHSAmt nvarchar(30) = NULL,
    @CLMCOPreHSSum nvarchar(30) = NULL,
    @CLMCOMPreHSSum nvarchar(30) = NULL,
    @CLMBoardDays nvarchar(30) = NULL,
    @CLMICUDays nvarchar(30) = NULL,
    @CLMINHOSPhyDays nvarchar(30) = NULL,
    @CLMKIDDCDays nvarchar(30) = NULL,
    @CLMCashDays nvarchar(30) = NULL,
    @CLMGurdianDays nvarchar(30) = NULL,
    @CLMUndExcess money = NULL,
    @CLMPerDisLmt money = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM genisis.WS_SameLimit WITH (UPDLOCK, HOLDLOCK) WHERE CASNumber = @cas)
    BEGIN
        UPDATE genisis.WS_SameLimit SET
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
            CLMTax = COALESCE(@CLMTax, CLMTax),
            CLMGuardianAmt = COALESCE(@CLMGuardianAmt, CLMGuardianAmt),
            CLMMedical = COALESCE(@CLMMedical, CLMMedical),
            CLMPhone = COALESCE(@CLMPhone, CLMPhone),
            CLMAdmin = COALESCE(@CLMAdmin, CLMAdmin),
            CLMOthers1 = COALESCE(@CLMOthers1, CLMOthers1),
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
            CLMCOMPreHSSum = COALESCE(@CLMCOMPreHSSum, CLMCOMPreHSSum),
            CLMBoardDays = COALESCE(@CLMBoardDays, CLMBoardDays),
            CLMICUDays = COALESCE(@CLMICUDays, CLMICUDays),
            CLMINHOSPhyDays = COALESCE(@CLMINHOSPhyDays, CLMINHOSPhyDays),
            CLMKIDDCDays = COALESCE(@CLMKIDDCDays, CLMKIDDCDays),
            CLMCashDays = COALESCE(@CLMCashDays, CLMCashDays),
            CLMGurdianDays = COALESCE(@CLMGurdianDays, CLMGurdianDays),
            CLMUndExcess = COALESCE(@CLMUndExcess, CLMUndExcess),
            CLMPerDisLmt = COALESCE(@CLMPerDisLmt, CLMPerDisLmt)
        WHERE CASNumber = @cas;
        SELECT 0;
    END
    ELSE
    BEGIN
        INSERT genisis.WS_SameLimit (CASNumber, CLMBoardAmt, CLMICUAmt, CLMServices, CLMTheatre, CLMMRI, CLMAccPostAmt, CLMAmbulance, CLMOutpatient, CLMMonthly, CLMOrgan, CLMTax, CLMGuardianAmt, CLMMedical, CLMPhone, CLMAdmin, CLMOthers1, CLMBoardTaxAmt, CLMICUTaxAmt, CLMLodgerTaxAmt, CLMPreHSAmt, CLMPreHosp, CLMSurgicalAmt, CLMAna, CLMNSPreHos, CLMPhysician, CLMPost, CLMMPreHSAmt, CLMCOPreHSSum, CLMCOMPreHSSum, CLMBoardDays, CLMICUDays, CLMINHOSPhyDays, CLMKIDDCDays, CLMCashDays, CLMGurdianDays, CLMUndExcess, CLMPerDisLmt)
        VALUES (@cas, @CLMBoardAmt, @CLMICUAmt, @CLMServices, @CLMTheatre, @CLMMRI, @CLMAccPostAmt, @CLMAmbulance, @CLMOutpatient, @CLMMonthly, @CLMOrgan, @CLMTax, @CLMGuardianAmt, @CLMMedical, @CLMPhone, @CLMAdmin, @CLMOthers1, @CLMBoardTaxAmt, @CLMICUTaxAmt, @CLMLodgerTaxAmt, @CLMPreHSAmt, @CLMPreHosp, @CLMSurgicalAmt, @CLMAna, @CLMNSPreHos, @CLMPhysician, @CLMPost, @CLMMPreHSAmt, @CLMCOPreHSSum, @CLMCOMPreHSSum, @CLMBoardDays, @CLMICUDays, @CLMINHOSPhyDays, @CLMKIDDCDays, @CLMCashDays, @CLMGurdianDays, @CLMUndExcess, @CLMPerDisLmt);
        SELECT 1;
    END
END
GO

-- The case's same-limit row as it stands: the days and the excess that are left (NULL columns read as none). No row gives no result.
CREATE OR ALTER PROCEDURE genisis.WorksheetSave_SameLimitOf
    @cas nvarchar(30)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ISNULL(CASE WHEN ISNUMERIC(CLMBoardDays) = 1 THEN CONVERT(decimal(18, 2), CLMBoardDays) END, 0) AS Board, ISNULL(CASE WHEN ISNUMERIC(CLMICUDays) = 1 THEN CONVERT(decimal(18, 2), CLMICUDays) END, 0) AS Icu, ISNULL(CASE WHEN ISNUMERIC(CLMINHOSPhyDays) = 1 THEN CONVERT(decimal(18, 2), CLMINHOSPhyDays) END, 0) AS PhysicianVisits,
           ISNULL(CASE WHEN ISNUMERIC(CLMKIDDCDays) = 1 THEN CONVERT(decimal(18, 2), CLMKIDDCDays) END, 0) AS KidneyMonths, ISNULL(CASE WHEN ISNUMERIC(CLMCashDays) = 1 THEN CONVERT(decimal(18, 2), CLMCashDays) END, 0) AS Cash, ISNULL(CASE WHEN ISNUMERIC(CLMGurdianDays) = 1 THEN CONVERT(decimal(18, 2), CLMGurdianDays) END, 0) AS Guardian, CLMUndExcess AS UndertakingExcess
    FROM genisis.WS_SameLimit WHERE CASNumber = @cas;
END
GO

-- UpdateMBMBalance (a member that is not a top-up and not on a lifetime plan): the worksheet's final approve is taken off the available limit of the member or covered person it draws on, never below 0.
-- Whose limit that is follows the kind of limit of the plan (AnnualLimit.SuppLimitStatus): A one limit for the member whoever the patient is; B one for the principal (patient 00) and one for all the
-- dependants together (cover 01); C one for each patient (the principal's is the member's). A member of type P always draws on the member's own limit. The source of the desktop works the limit out again as
-- the plan's limit less the sum of every claim of the member (Claim_GetAnnLimit, a raw WHERE text, never called here) and only for a plan whose supplementary limit is above zero; the desktop in use does not:
-- over the 2,907 pairs of consecutive claims of one limit, the next claim's saved previous balance equals the earlier one's less its final approve in 80 per cent (kind A), 94 (B) and 86 (C) of the claims of plans
-- with a supplementary limit, and in 93 and 75 per cent of those without, but equals limit less the sum of earlier claims in 71 per cent. It is not done for a claim that is repudiated, cancelled, not decided or
-- pay-and-claim rejected, for a plan without an annual limit (the AnnLmtInd of the plan, except for health type S), or for kind D (a dependant on the supplementary limit; 1 of 29 pairs follow the rule).
-- With @apply = 0 nothing is written. One row comes back: Reason (NULL when the limit applies), the record and the figures, Applied (a write was made) and Unsupported (the worksheet is of a kind the save cannot do yet: top-up,
-- lifetime plan, take-over, kind D). The desktop's UpdateMBMLifeTimeBal, UpdateMBMBnfBal (lifetime plans only), UpdateCASTakeOverLmt and the top-up routines are not ported: the lifetime balance of 694 consecutive claims
-- follows no simple rule (20 equal the earlier balance less its final approve), 8 cases have ever been take-over cases, and no saved worksheet has a top-up.
CREATE OR ALTER PROCEDURE genisis.WorksheetSave_MemberBalance
    @cas nvarchar(30), @version int, @apply bit = 1, @amount money = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @mbm nvarchar(30), @patient nvarchar(2), @memType nvarchar(2), @plan nvarchar(20), @ins nvarchar(20), @hlt nvarchar(5), @annInd nvarchar(2), @admitted datetime,
            @claimability nvarchar(2), @topup nvarchar(2), @lifetime nvarchar(2), @takeOver nvarchar(2);
    -- the case's member, patient and plan, as VIEW_Claim_MBM_Case_worksheetAdjustment joins them, but from the base tables: the view starts from the version row, which may not be written yet
    SELECT TOP (1) @mbm = RTRIM(cs.MBMNumber), @patient = RTRIM(cs.MBMPatientCovID), @memType = RTRIM(m.MBMMemberType),
           @plan = RTRIM(CASE WHEN RTRIM(cs.MBMPatientCovID) <> '00' AND ISNULL(RTRIM(cp.MBMCPLNCode), '') <> '' THEN cp.MBMCPLNCode ELSE m.PLNCode END), @ins = RTRIM(m.INSCode), @hlt = RTRIM(m.HLTCode),
           @annInd = RTRIM(pln.AnnLmtIND), @admitted = cs.CASDateAdmitted, @claimability = RTRIM(cs.CASClaimability), @topup = RTRIM(m.MBMTopupInd), @lifetime = RTRIM(pln.PLNLifeTimeStatus),
           @takeOver = RTRIM(cs.CASTakeOverInd)
    FROM dbo.CAS cs JOIN dbo.MBM m ON m.MBMNumber = cs.MBMNumber
    LEFT JOIN dbo.MBMCoveredPersons cp ON cp.MBMCNumber = cs.MBMNumber AND cp.MBMCCoverID = cs.MBMPatientCovID
    LEFT JOIN dbo.PLN pln ON pln.PLNCode = m.PLNCode
    WHERE cs.CASNumber = @cas;

    DECLARE @status nvarchar(2), @finalApprove money;
    SELECT TOP (1) @status = RTRIM(a.SuppLimitStatus) FROM dbo.AnnualLimit a WHERE a.PLNCode = @plan AND a.INSCode = @ins AND a.AnnualEffDate <= @admitted ORDER BY a.AnnualEffDate DESC;
    -- @amount is what the worksheet approves when its version row is not written yet (the check made before anything is written); otherwise the version row is read.
    SET @finalApprove = @amount;
    IF @finalApprove IS NULL SELECT @finalApprove = ISNULL(CLMFinalApprove, 0) FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @version;
    -- What the save cannot do yet is marked Unsupported (the save refuses such a worksheet and writes nothing); the others are cases where the desktop leaves the limit alone.
    DECLARE @unsupported bit = 0;
    DECLARE @reason nvarchar(60) = CASE
        WHEN @mbm IS NULL THEN N'the case has no member'
        WHEN @finalApprove IS NULL THEN N'the version has no claim row'
        WHEN @claimability IN ('RR', 'CC', 'NN', 'NR', 'PR') THEN N'the claim is not accepted'
        WHEN @topup = 'Y' THEN N'a top-up member is not supported'
        WHEN @lifetime = 'Y' THEN N'a lifetime plan is not supported'
        WHEN @takeOver = 'Y' THEN N'a take-over case is not supported'
        WHEN @status IS NULL THEN N'the plan has no annual limit row'
        WHEN @hlt <> 'S' AND @annInd = 'N' THEN N'the plan has no annual limit'
        WHEN @status = 'D' THEN N'a kind D limit is not supported'
        WHEN @status NOT IN ('A', 'B', 'C') THEN N'an unknown kind of limit' END;
    IF @reason IN (N'the version has no claim row', N'a top-up member is not supported', N'a lifetime plan is not supported', N'a take-over case is not supported', N'a kind D limit is not supported', N'an unknown kind of limit') SET @unsupported = 1;

    DECLARE @new money, @record nvarchar(20), @before money, @cover nvarchar(2);
    IF @reason IS NULL
    BEGIN
        SET @record = CASE WHEN @memType = 'P' OR @patient = '00' THEN N'MBM' WHEN @status = 'B' THEN N'MBMCoveredPersons 01' WHEN @status = 'C' THEN N'MBMCoveredPersons ' + @patient ELSE N'MBM' END;
        SET @cover = CASE WHEN @status = 'B' THEN '01' ELSE @patient END;
        IF @record = N'MBM' SELECT @before = MBMAvailableLimit FROM dbo.MBM WHERE RTRIM(MBMNumber) = @mbm;
        ELSE SELECT @before = MBMCAvailableLimit FROM dbo.MBMCoveredPersons WHERE RTRIM(MBMCNumber) = @mbm AND RTRIM(MBMCCoverID) = @cover;
        IF @before IS NULL
        BEGIN
            SET @reason = N'the member has no available limit to take it off';
            SET @unsupported = 1;
        END
        ELSE
        BEGIN
            SET @new = CASE WHEN @before - @finalApprove <= 0 THEN 0 ELSE @before - @finalApprove END;
            IF @apply = 1
            BEGIN
                IF @record = N'MBM' UPDATE dbo.MBM SET MBMAvailableLimit = @new WHERE RTRIM(MBMNumber) = @mbm;
                ELSE UPDATE dbo.MBMCoveredPersons SET MBMCAvailableLimit = @new WHERE RTRIM(MBMCNumber) = @mbm AND RTRIM(MBMCCoverID) = @cover;
            END
        END
    END
    SELECT @reason AS Reason, @record AS Record, @status AS LimitKind, @finalApprove AS FinalApprove, @before AS Before, @new AS After, CAST(CASE WHEN @reason IS NULL AND @apply = 1 THEN 1 ELSE 0 END AS bit) AS Applied, @unsupported AS Unsupported;
END
GO

-- UpdateUnderWriting: the excess undertaking the case had before the worksheet is kept on the version row (Claim.CLMPreviousUndExcess): what the case's same-limit row held, or the member's own for a case
-- without one. Zero in 33,664 of the 33,718 versions saved since July 2020 (empty in 54). Returns how many version rows were changed.
CREATE OR ALTER PROCEDURE genisis.WorksheetSave_PreviousUndertaking
    @cas nvarchar(30), @version int, @amount money
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.WS_Claim SET CLMPreviousUndExcess = @amount WHERE CASNumber = @cas AND ClaimVersion = @version;
    SELECT @@ROWCOUNT;
END
GO
