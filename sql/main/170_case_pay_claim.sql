-- Case > Pay & Claim (legacy frmCASPayClaim). Reads replace dbo.Case_CASPayClaim / dbo.Search_CAS_PayClaim; the save
-- replaces dbo.Case_SavePayClaim / dbo.Case_UpdatePayClaim and leaves the AgentName / CasRemarks columns untouched.

-- showMBM: the principal and, for family / group insured types (F, H), the covered persons.
CREATE OR ALTER PROCEDURE genisis.PayClaim_Members
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(m.MBMNumber) AS MemberNo, N'00' AS CoverId, RTRIM(m.MBMName) AS PatientName, RTRIM(m.MBMPolicyNo) AS PolicyNo,
        m.MBMPayorEffDate AS PolicyEffective, m.MBMPayorExpDate AS PolicyExpiry,
        RTRIM(m.MBMAddress1) AS Add1, RTRIM(m.MBMAddress2) AS Add2, RTRIM(m.MBMAddress3) AS Add3,
        RTRIM(m.MBMPostCode) AS PostCode, RTRIM(m.STACode) AS State, RTRIM(p.PAYCompanyName) AS InsuranceCompany, 0 AS Sort
    FROM dbo.CAS c
    INNER JOIN dbo.MBM m ON m.MBMNumber = c.MBMNumber
    LEFT JOIN dbo.PAY p ON p.PAYCode = m.PAYCode
    WHERE c.CASNumber = @cas
    UNION ALL
    SELECT RTRIM(cp.MBMCNumber), RTRIM(cp.MBMCCoverID), RTRIM(cp.MBMCName), RTRIM(m.MBMPolicyNo),
        m.MBMPayorEffDate, m.MBMPayorExpDate, NULL, NULL, NULL, NULL, NULL, NULL, 1
    FROM dbo.CAS c
    INNER JOIN dbo.MBM m ON m.MBMNumber = c.MBMNumber
    INNER JOIN dbo.MBMCoveredPersons cp ON cp.MBMCNumber = m.MBMNumber
    WHERE c.CASNumber = @cas AND RTRIM(m.INSCode) IN (N'F', N'H')
    ORDER BY Sort, CoverId;
END
GO

-- ShowMBMDetails: the case header the second tab shows (the patient is the principal for cover ID 00).
CREATE OR ALTER PROCEDURE genisis.PayClaim_Case
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(c.CASNumber) AS CaseNumber, RTRIM(c.MBMNumber) AS MembershipNo, RTRIM(m.MBMPolicyNo) AS PolicyNo,
        RTRIM(m.MBMName) AS PrincipalName,
        RTRIM(CASE WHEN c.MBMPatientCovID = N'00' THEN m.MBMName ELSE ISNULL(cp.MBMCName, m.MBMName) END) AS PatientName,
        c.CASDateAdmitted AS AdmissionDate, c.CASDischargeDate AS DischargeDate, RTRIM(h.HOSName) AS HospitalName
    FROM dbo.CAS c
    LEFT JOIN dbo.MBM m ON m.MBMNumber = c.MBMNumber
    LEFT JOIN dbo.MBMCoveredPersons cp ON cp.MBMCNumber = c.MBMNumber AND cp.MBMCCoverID = c.MBMPatientCovID
    LEFT JOIN dbo.HOS h ON h.HOSCode = c.CASHOSCode
    WHERE c.CASNumber = @cas;
END
GO

-- showPayClaim: the case's Pay & Claim record (at most one per case).
CREATE OR ALTER PROCEDURE genisis.PayClaim_Get
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(CASNumber) AS CaseNo, CASPayCLMDate AS PayClaimDate,
        RTRIM(CASLab) AS LabTest, RTRIM(CASConfInfo) AS ConflictingInfo, RTRIM(CASInfoInv) AS Investigation,
        RTRIM(CASCovgSub) AS Radiology, RTRIM(CASNonPan) AS NonPanel, RTRIM(CASNonCash) AS NoCashless,
        RTRIM(CASCOthers) AS Others, RTRIM(CASOthers) AS Remarks
    FROM dbo.CASPayClaim WHERE CASNumber = @cas;
END
GO

-- SavePayClaim. @isNew = 1 inserts (Result 2 when the case already has a record), 0 updates (Result 1 when it has none).
CREATE OR ALTER PROCEDURE genisis.PayClaim_Save
    @cas nvarchar(15), @isNew bit, @payClaimDate datetime,
    @lab char(1), @confInfo char(1), @infoInv char(1), @covgSub char(1), @nonPan char(1), @nonCash char(1),
    @cOthers char(1), @others nvarchar(4000), @userCode nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.CAS WHERE CASNumber = @cas)
    BEGIN
        SELECT 1 AS Result;
        RETURN;
    END

    DECLARE @exists bit = CASE WHEN EXISTS (SELECT 1 FROM dbo.CASPayClaim WITH (UPDLOCK, ROWLOCK) WHERE CASNumber = @cas) THEN 1 ELSE 0 END;
    IF @isNew = 1 AND @exists = 1 BEGIN SELECT 2 AS Result; RETURN; END
    IF @isNew = 0 AND @exists = 0 BEGIN SELECT 1 AS Result; RETURN; END

    DECLARE @now datetime = GETDATE();
    IF @isNew = 1
        INSERT dbo.CASPayClaim (CASNumber, CASPayCLMDate, CASLab, CASConfInfo, CASInfoInv, CASCovgSub, CASNonPan, CASNonCash,
            CASCOthers, CASOthers, CASRegUser, CASRegDate, LastUpdateUser, LastUpdateDate)
        VALUES (@cas, @payClaimDate, @lab, @confInfo, @infoInv, @covgSub, @nonPan, @nonCash,
            @cOthers, @others, @userCode, @now, @userCode, @now);
    ELSE
        UPDATE dbo.CASPayClaim
        SET CASPayCLMDate = @payClaimDate, CASLab = @lab, CASConfInfo = @confInfo, CASInfoInv = @infoInv, CASCovgSub = @covgSub,
            CASNonPan = @nonPan, CASNonCash = @nonCash, CASCOthers = @cOthers, CASOthers = @others,
            LastUpdateUser = @userCode, LastUpdateDate = @now
        WHERE CASNumber = @cas;

    SELECT 0 AS Result;
END
GO
