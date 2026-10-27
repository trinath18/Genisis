-- Worksheet Registration: what the form shows and the save checks before it writes (the desktop's form load, parameterized). Read only.

-- The case with its member, patient, plan, hospital and the defaults the "Get Address" box of the form copies for a member-paid worksheet: the member's address, bank and account.
-- The state is shown by its description (the desktop copies the code), as the list of states holds descriptions. FinalDiagnosisCount: the rows of DIAFinal the case has.
CREATE OR ALTER PROCEDURE genisis.WorksheetReg_Header
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1)
        RTRIM(cs.CASNumber) AS CaseNo, RTRIM(cs.MBMNumber) AS MemberNo, RTRIM(m.MBMName) AS MemberName, RTRIM(cs.MBMPatientCovID) AS PatientId,
        RTRIM(CASE WHEN RTRIM(cs.MBMPatientCovID) = N'00' THEN m.MBMName ELSE cp.MBMCName END) AS PatientName,
        RTRIM(CASE WHEN RTRIM(cs.MBMPatientCovID) <> N'00' AND ISNULL(RTRIM(cp.MBMCPLNCode), N'') <> N'' THEN cp.MBMCPLNCode ELSE m.PLNCode END) AS PlanCode,
        RTRIM(m.PAYCode) AS PayorCode, RTRIM(cs.CASHOSCode) AS HospitalCode, RTRIM(h.HOSName) AS HospitalName, RTRIM(cs.CASClaimability) AS Claimability,
        cs.CASDateAdmitted AS AdmissionDate, cs.CASDischargeDate AS DischargeDate, RTRIM(cs.LINKCASNo) AS LinkCase,
        RTRIM(p.PLNDisIndicator) AS PerDisabilityIndicator, RTRIM(o2.MBMBTBG) AS Btbg,
        RTRIM(m.MBMAddress1) AS Address1, RTRIM(m.MBMAddress2) AS Address2, RTRIM(m.MBMAddress3) AS Address3, RTRIM(m.MBMPostCode) AS PostCode, RTRIM(m.CTYCode) AS City,
        RTRIM(ISNULL(s.STADescription, m.STACode)) AS State, RTRIM(o2.MBMBankName) AS Bank, RTRIM(o2.MBMBankACNo) AS AccountNo,
        (SELECT COUNT(*) FROM dbo.DIAFinal d WHERE d.CASNumber = cs.CASNumber) AS FinalDiagnosisCount
    FROM dbo.CAS cs
    JOIN dbo.MBM m ON m.MBMNumber = cs.MBMNumber
    LEFT JOIN dbo.MBMOthersTwo o2 ON o2.MBMNumber = m.MBMNumber
    LEFT JOIN dbo.MBMCoveredPersons cp ON cp.MBMCNumber = cs.MBMNumber AND cp.MBMCCoverID = cs.MBMPatientCovID
    LEFT JOIN dbo.PLN p ON p.PLNCode = m.PLNCode
    LEFT JOIN dbo.HOS h ON h.HOSCode = cs.CASHOSCode
    LEFT JOIN dbo.STA s ON s.STACode = m.STACode
    WHERE cs.CASNumber = @cas;
END
GO

-- lstInvoice: the invoices the worksheet of a version covers (the ones entered for that version).
CREATE OR ALTER PROCEDURE genisis.WorksheetReg_Invoices
    @cas nvarchar(15), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT i.INVIndex AS [Index], RTRIM(i.INVNo) AS InvoiceNo, i.InvDate AS InvoiceDate, ISNULL(i.INVAmount, 0) AS Amount, ISNULL(i.InvPayAmt, 0) AS PayAmount,
           RTRIM(i.InvPayTo) AS PayTo, RTRIM(i.INVStayType) AS StayType, CAST(ISNULL(i.INVWSed, 0) AS bit) AS Used
    FROM genisis.WS_ClaimInvoice i
    WHERE i.CASNumber = @cas AND RTRIM(i.INVWSversion) = CAST(@version AS nvarchar(10))
    ORDER BY i.INVIndex;
END
GO

-- checkAccNo: the number of digits the accounts of a bank have (BankAccounts.AccountDigits); no row when the bank is not listed.
CREATE OR ALTER PROCEDURE genisis.WorksheetReg_BankDigits
    @bank nvarchar(60)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) b.AccountDigits FROM genisis.Maint_BankAccounts b WHERE RTRIM(b.Bank) = RTRIM(@bank) AND b.AccountDigits IS NOT NULL;
END
GO

-- What the earlier versions of the case approved: taken off the member's available limit already, so the limit before a later version is the available limit plus this.
CREATE OR ALTER PROCEDURE genisis.WorksheetReg_ApprovedBefore
    @cas nvarchar(15), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(ISNULL(SUM(c.CLMFinalApprove), 0) AS decimal(18, 2)) FROM genisis.WS_Claim c WHERE c.CASNumber = @cas AND c.ClaimVersion < @version AND c.ClaimVersion <> 0 AND RTRIM(c.ClaimStatus) = N'C';
END
GO

-- Whether a version of the case has a row in Claim already (a worksheet registered at the same time).
CREATE OR ALTER PROCEDURE genisis.WorksheetReg_Registered
    @cas nvarchar(15), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CASE WHEN EXISTS (SELECT 1 FROM genisis.WS_Claim c WHERE c.CASNumber = @cas AND c.ClaimVersion = @version) THEN 1 ELSE 0 END;
END
GO
