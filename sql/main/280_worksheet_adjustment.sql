-- Worksheet > Worksheet Adjustment (legacy frmWorksheetAdj2013), step 1: the Update button (cmdUpdate_Click / SaveOtherInfo), which saves the payment details
-- and the remarks of one worksheet version. The rest of the form (Approve, credit / debit notes, letters) follows in later steps.

-- The pay-to of a version's invoices as the form worked it out: MBMHosStatus is the pay-to of the last invoice listed (H hospital, M member).
CREATE OR ALTER PROCEDURE genisis.WorksheetAdjustment_PayTo
    @cas nvarchar(30), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(InvPayTo) AS PayTo
    FROM genisis.WS_CASInvoice
    WHERE CASNumber = @cas AND INVWSversion = CAST(@version AS nvarchar(10))
    ORDER BY INVIndex DESC;
END
GO

-- cboBank: the banks that have accounts (Bank, from BankAccounts of the maintenance database).
CREATE OR ALTER PROCEDURE genisis.WorksheetAdjustment_Banks
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(Bank) AS Name FROM genisis.Maint_BankAccounts WHERE Bank IS NOT NULL AND RTRIM(Bank) <> N'' GROUP BY RTRIM(Bank) ORDER BY RTRIM(Bank);
END
GO

-- cboCLMState: the states (SearchState).
CREATE OR ALTER PROCEDURE genisis.WorksheetAdjustment_States
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(STADescription) AS Name FROM dbo.STA WHERE STADescription IS NOT NULL AND RTRIM(STADescription) <> N'' ORDER BY RTRIM(STADescription);
END
GO

-- SaveOtherInfo: Claim gets the last document date (only when one was given), pay-to, bank, account number and GP; ClaimOthers (when the version has a row) gets the
-- remarks, the member's address, the note, the payment mode and the attention. Empty text is stored as empty text, as the desktop did.
-- Result: 0 saved, 1 the worksheet version does not exist.
CREATE OR ALTER PROCEDURE genisis.WorksheetAdjustment_UpdatePayment
    @cas nvarchar(30), @version int, @lastDocDate datetime,
    @payTo nvarchar(50), @gpName nvarchar(50), @bank nvarchar(40), @accountNo nvarchar(30),
    @paymentMode nvarchar(50), @attention nvarchar(60),
    @address1 nvarchar(50), @address2 nvarchar(50), @address3 nvarchar(50), @postCode nvarchar(6), @city nvarchar(50), @state nvarchar(50),
    @note nvarchar(100), @wsRemarks nvarchar(200), @cdRemarks nvarchar(200)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF NOT EXISTS (SELECT 1 FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @version)
    BEGIN
        SELECT 1 AS Result;
        RETURN;
    END

    UPDATE genisis.WS_Claim
    SET CLMLastDocDate = ISNULL(@lastDocDate, CLMLastDocDate), CLMPayTo = @payTo, CLMAccountNo = @accountNo, CLMBank = @bank, CLMGPName = @gpName
    WHERE CASNumber = @cas AND ClaimVersion = @version;

    UPDATE genisis.WS_ClaimOthers
    SET CLMWSRemarks = @wsRemarks, CLMDCNoteRemarks = @cdRemarks, MBMAdd1 = @address1, MBMAdd2 = @address2, MBMAdd3 = @address3, MBMPostCode = @postCode,
        MBMCity = @city, MBMState = @state, CLMNote = @note, CLMPayMode = @paymentMode, CLMAttention = @attention
    WHERE CASNumber = @cas AND CLaimVersion = @version;
    SELECT 0 AS Result;
END
GO
