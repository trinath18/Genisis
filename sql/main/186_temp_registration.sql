-- Membership > Temp MBM Reg (legacy FrmTempMBMReg): a registration pre-filled from a previous membership (cmdShow_Click on
-- SearchMBMCovPersonsProc, with showMBMTwo / showMBMOthers / showMBMOthersTwo / showMBMCov). The new membership is saved by the
-- Registration procedures.

-- Result 1: the previous membership; result 2: its covered persons (family plans, cover 00 excluded).
CREATE OR ALTER PROCEDURE genisis.TempRegistration_Source
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(m.MBMNumber) AS MembershipNo, RTRIM(m.HLTCode) AS HealthCode, RTRIM(m.PAYCode) AS PayorCode,
        RTRIM(m.MBMPolicyNo) AS PolicyNo, m.MBMPolicyYear AS PolicyYear, RTRIM(t.MBMSalutation) AS Salutation, RTRIM(m.MBMName) AS Name,
        RTRIM(m.MBMIcBcPp) AS IcBcPp, RTRIM(t.MBMIcBcPp2nd) AS OtherIc, m.MBMDOB AS DateOfBirth, RTRIM(m.MBMSex) AS Sex,
        RTRIM(m.RACCode) AS RaceCode, RTRIM(t.NATCode) AS NationalityCode, RTRIM(w.MBMMaritalStatus) AS MaritalStatus,
        RTRIM(m.MBMAddress1) AS Address1, RTRIM(m.MBMAddress2) AS Address2, RTRIM(m.MBMAddress3) AS Address3, RTRIM(m.CTYCode) AS City,
        RTRIM(m.MBMPostCode) AS PostCode, RTRIM(m.STACode) AS State, RTRIM(t.MBMTelnoH) AS TelHome, RTRIM(t.MBMTelNoM) AS TelMobile,
        RTRIM(t.MBMTelNoO) AS TelOffice, RTRIM(t.MBMEmail) AS Email, RTRIM(m.INSCode) AS InsuredType, RTRIM(m.PLNCode) AS PlanCode,
        o.MBMFirstJoinedDate AS DateJoined, RTRIM(m.MBMTakeover) AS TakeOver, RTRIM(o.MBMGuaRenewal) AS GuaranteeRenewal,
        RTRIM(o.MBMInstallment) AS InstallmentMode, RTRIM(o.MBMGroupCompany) AS GroupCompany, RTRIM(o.MBMEmployeeNo) AS EmployeeNo,
        RTRIM(o.MBMDepartment) AS Department, RTRIM(o.MBMBranch) AS Branch, RTRIM(o.MBMAgentCode) AS AgentCode,
        RTRIM(w.MBMExclusion) AS Exclusion, RTRIM(w.MBMAllergic) AS Allergic, RTRIM(w.MBMRemarks) AS Remarks
    FROM dbo.MBM m
    LEFT JOIN dbo.MBMTwo t ON t.MBMNumber = m.MBMNumber
    LEFT JOIN dbo.MBMOthers o ON o.MBMNumber = m.MBMNumber
    LEFT JOIN dbo.MBMOthersTwo w ON w.MBMNumber = m.MBMNumber
    WHERE m.MBMNumber = @number;

    SELECT RTRIM(c.MBMCCoverID) AS CoverId, RTRIM(c.MBMCRELCode) AS Relationship, RTRIM(c2.MBMCSalutation) AS Salutation,
        RTRIM(c.MBMCName) AS Name, RTRIM(c.MBMCICBCPPNo) AS IcBcPp, c.MBMCDOB AS DateOfBirth, RTRIM(c.MBMCSex) AS Sex,
        RTRIM(c2.MBMCOccupation) AS Occupation, RTRIM(c2.MBMCExclusion) AS Exclusion, RTRIM(c2.MBMCAllergic) AS Allergic
    FROM dbo.MBM m
    JOIN dbo.MBMCoveredPersons c ON c.MBMCNumber = m.MBMNumber
    LEFT JOIN dbo.MBMCoveredPersonsTwo c2 ON c2.MBMCNumber = c.MBMCNumber AND c2.MBMCCoverID = c.MBMCCoverID
    WHERE m.MBMNumber = @number AND m.INSCode IN ('F', 'H') AND RTRIM(c.MBMCCoverID) <> '00'
    ORDER BY c.MBMCCoverID;
END
GO
