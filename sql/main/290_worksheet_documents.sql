-- Worksheet > Worksheet Adjustment (legacy frmWorksheetAdj2013), step 2: the credit / debit note and the Excess letter. The desktop filled Excel and Word templates
-- (CreditNote.xlt, DebitNote.xlt, DebitNoteCompany.xlt, EXCLetterMAS.DOT, ExcessLetterEtiqa.DOT, MedixExcLetter.DOT) that are not in the source package; these procedures
-- return the data the templates were filled with.

-- The member's address as the notes printed it (the member master, not the address typed on the worksheet) and whether the plan is billed to the company.
CREATE OR ALTER PROCEDURE genisis.WorksheetNote_Member
    @cas nvarchar(30), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(v.MBMAddress1) AS Address1, RTRIM(v.MBMAddress2) AS Address2, RTRIM(v.MBMAddress3) AS Address3, RTRIM(v.MBMPostCode) AS PostCode,
        CAST(CASE WHEN RTRIM(v.PLNCompanyDN) = N'C' THEN 1 ELSE 0 END AS bit) AS CompanyPlan
    FROM dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v
    WHERE v.CASNumber = @cas AND v.ClaimVersion = @version AND v.MBMNumber IS NOT NULL;
END
GO

-- cmdDebit_Click: a debit note that has no number yet gets the next one of its payor: D + payor + five digits (DRSeq, GenerateDebitNo), saved with today's date on the
-- worksheet (UpdateCreditDebit). A worksheet that has a number keeps it. Result: 0 ok, 1 the worksheet version does not exist.
CREATE OR ALTER PROCEDURE genisis.WorksheetNote_AssignDebitNo
    @cas nvarchar(30), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @number nvarchar(30);
    SELECT @number = RTRIM(DebitCreditRefNo) FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @version;
    IF @@ROWCOUNT = 0
    BEGIN
        SELECT 1 AS Result, CAST(NULL AS nvarchar(30)) AS Number;
        RETURN;
    END
    IF ISNULL(@number, N'') <> N''
    BEGIN
        SELECT 0 AS Result, @number AS Number;
        RETURN;
    END

    DECLARE @payor nvarchar(10) = LEFT(@cas, 2), @seq int;
    IF NOT EXISTS (SELECT 1 FROM genisis.WS_DRSeq WITH (UPDLOCK, HOLDLOCK) WHERE Payor = @payor)
    BEGIN
        INSERT genisis.WS_DRSeq (DRSeq, Payor) VALUES (1, @payor);
        SET @seq = 1;
    END
    ELSE
        UPDATE genisis.WS_DRSeq SET @seq = DRSeq = DRSeq + 1 WHERE Payor = @payor;

    SET @number = N'D' + @payor + CASE WHEN @seq > 99999 THEN CAST(@seq AS nvarchar(10)) ELSE RIGHT(N'00000' + CAST(@seq AS nvarchar(10)), 5) END;
    UPDATE genisis.WS_Claim SET DebitCreditRefNo = @number, DebitCreditDate = CAST(CAST(GETDATE() AS date) AS datetime)
    WHERE CASNumber = @cas AND ClaimVersion = @version;
    SELECT 0 AS Result, @number AS Number;
END
GO

-- cmdExcLetter_Click / bookmark: the excess record of the case (CAS_Excess), the one saved for the version when there is one, else the latest, with the amounts as numbers
-- (they are text in the table: "-" or an amount), the hospital's contact and fax, and the member details the letter prints.
CREATE OR ALTER PROCEDURE genisis.WorksheetExcess_Get
    @cas nvarchar(30), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1)
        RTRIM(e.EXCNumber) AS ExcessNo, e.EXCDate AS ExcessDate, RTRIM(e.INVNo) AS InvoiceNo, genisis.Worksheet_Amount(e.INVAmt) AS InvoiceAmount,
        genisis.Worksheet_Amount(e.EXCBoardAmt) AS RoomAndBoard, genisis.Worksheet_Amount(e.EXCICUAmt) AS Icu, genisis.Worksheet_Amount(e.EXCServices) AS Services,
        genisis.Worksheet_Amount(e.EXCTheatre) AS Theatre, genisis.Worksheet_Amount(e.EXCSurgicalAmt) AS Surgical, genisis.Worksheet_Amount(e.EXCAna) AS Anaesthetist,
        genisis.Worksheet_Amount(e.EXCPhysician) AS Physician, genisis.Worksheet_Amount(e.EXCAmbulance) AS Ambulance, genisis.Worksheet_Amount(e.EXCTax) AS Tax,
        genisis.Worksheet_Amount(e.EXCGuardianAmt) AS Guardian, genisis.Worksheet_Amount(e.EXCMedical) AS MedicalReport, genisis.Worksheet_Amount(e.EXCAdmin) AS Admin,
        genisis.Worksheet_Amount(e.EXCBoardTaxAmt) AS RoomAndBoardTax, genisis.Worksheet_Amount(e.EXCICUTaxAmt) AS IcuTax, genisis.Worksheet_Amount(e.EXCLodgerTaxAmt) AS LodgerTax,
        genisis.Worksheet_Amount(e.EXCOthers1) AS Others, RTRIM(e.EXCOthersDesc) AS OthersDescription,
        genisis.Worksheet_Amount(e.EXCTotalExceed) AS LimitExceeded, genisis.Worksheet_Amount(e.EXCCoPayment) AS CoPayment, genisis.Worksheet_Amount(e.EXCUndExcess) AS UndertakenExcess,
        RTRIM(h.HOSContact) AS HospitalContact, RTRIM(h.HOSFaxNo) AS HospitalFax,
        RTRIM(v.MBMEmployeeNo) AS StaffNo, RTRIM(v.MBMCostCenter) AS CostCenter, RTRIM(v.MBMDepartment) AS Department,
        RTRIM(CASE WHEN RTRIM(v.MBMPatientCovID) <> N'00' AND ISNULL(RTRIM(cp.MBMCICBCPPNo), N'') <> N'' THEN cp.MBMCICBCPPNo ELSE v.MBMIcBcPp END) AS PatientIc,
        RTRIM(v.PLNExcessPrivate) AS ExcessTemplate
    FROM genisis.WS_ExcessLetter e
    LEFT JOIN dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v ON v.CASNumber = e.CASNumber AND v.ClaimVersion = @version AND v.MBMNumber IS NOT NULL
    LEFT JOIN dbo.MBMCoveredPersons cp WITH (NOLOCK) ON cp.MBMCNumber = v.MBMNumber AND cp.MBMCCoverID = v.MBMPatientCovID
    LEFT JOIN dbo.HOS h WITH (NOLOCK) ON h.HOSCode = v.CASHOSCode
    WHERE e.CASNumber = @cas
    ORDER BY CASE WHEN e.ClaimVersion = @version THEN 0 ELSE 1 END, e.EXCDate DESC;
END
GO

-- cmdExcLetter_Click, after the letter is made: the case is marked as having had its excess letter (tblRecord.ExcessLetter = 'Y').
CREATE OR ALTER PROCEDURE genisis.WorksheetExcess_MarkIssued
    @cas nvarchar(30)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.Maint_tblRecord SET ExcessLetter = 'Y' WHERE CaseNo = @cas;
    SELECT @@ROWCOUNT AS Marked;
END
GO
