-- Membership > Upload Membership Excel (Pluto) and Generate Membership ASCII (Pluto)
-- (legacy frmMembershipExcelPluto / frmMembershipASCIIPluto). The desktop used the Pluto connection (MediConnectPluto.UDL),
-- which points at the main database; the staging table is dbo.MBMExcellUploadShelly.

-- One worksheet row (cmdReturnDate_Click). Empty dates are stored as NULL (the desktop's '' became 01/01/1900).
CREATE OR ALTER PROCEDURE genisis.Pluto_InsertUpload
    @No decimal(18, 0), @RELCode nvarchar(4000), @MBMPolicyNo nvarchar(4000), @MBMName nvarchar(4000), @MBMAddress1 nvarchar(4000),
    @MBMAddress2 nvarchar(4000), @MBMAddress3 nvarchar(4000), @CTYCode nvarchar(4000), @STACode nvarchar(4000),
    @MBMPostCode nvarchar(4000), @MBMGroupCompany nvarchar(4000), @MBMDepartment nvarchar(4000), @BRNCode nvarchar(4000),
    @MBMSex nvarchar(4000), @MBMRenewFlag nvarchar(4000), @MBMIcBcPp nvarchar(4000), @MBMDOB datetime, @PLNCode nvarchar(4000),
    @INSCode nvarchar(4000), @MBMPayorEffDate datetime, @MBMPayorExpDate datetime, @MBMExclusion nvarchar(4000),
    @MBMCLNPlanCode nvarchar(4000), @MBMCLNInsType nvarchar(4000), @MBMCLNEffDate datetime, @MBMCLNExpDate datetime,
    @MBMCLNExclusion nvarchar(4000), @PAYCode nvarchar(4000), @MBMBordxDate datetime
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.MBMExcellUploadShelly (No, RELCode, MBMPolicyNo, MBMName, MBMAddress1, MBMAddress2, MBMAddress3, CTYCode, STACode,
        MBMPostCode, MBMGroupCompany, MBMDepartment, BRNCode, MBMSex, MBMRenewFlag, MBMIcBcPp, MBMDOB, PLNCode, INSCode,
        MBMPayorEffDate, MBMPayorExpDate, MBMExclusion, MBMCLNPlanCode, MBMCLNInsType, MBMCLNEffDate, MBMCLNExpDate,
        MBMCLNExclusion, PAYCode, MBMBordxDate)
    VALUES (@No, @RELCode, @MBMPolicyNo, @MBMName, @MBMAddress1, @MBMAddress2, @MBMAddress3, @CTYCode, @STACode,
        @MBMPostCode, @MBMGroupCompany, @MBMDepartment, @BRNCode, @MBMSex, @MBMRenewFlag, @MBMIcBcPp, @MBMDOB, @PLNCode, @INSCode,
        @MBMPayorEffDate, @MBMPayorExpDate, @MBMExclusion, @MBMCLNPlanCode, @MBMCLNInsType, @MBMCLNEffDate, @MBMCLNExpDate,
        @MBMCLNExclusion, @PAYCode, @MBMBordxDate);
END
GO

-- The staged rows of one payor and bordx date, in worksheet order (CmdGenerate_Click). Health type only filters the payor list.
CREATE OR ALTER PROCEDURE genisis.Pluto_AsciiRows
    @bordxDate datetime, @pay nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RELCode, MBMPolicyNo, MBMAgentCode, MBMSalutation, MBMName, MBMAddress1, MBMAddress2, MBMAddress3, CTYCode, MBMPostCode,
        STACode, MBMTelNoH, MBMTelNoO, MBMTelNoM, MBMIcBcPp, MBMDOB, MBMSex, NATCode, MBMWeight, MBMHeight, LGNCode, RACCode,
        MBMOccupation, MBMWorkNature, MBMBlood, MBMAllergic, MBMPayorEffDate, MBMPayorExpDate, PLNCode, INSCode, MBMGroupCompany,
        MBMRenewFlag, MBMExclusion, MBMSmoking, MBMAlcohol, MBMMaritalStatus, BRNCode, MBMPrevPolNo, MBMPrevMemNo, MBMEmployeeNo,
        MBMJoinedDate, Clinical, ClinicName1, ClinicName2, ClinicName3, ClinicName4, MBMDepartment, MBMGRPCompanyAdd1,
        MBMGRPCompanyAdd2, MBMGRPCompanyAdd3, MBMGRPCompanyAdd4, MBMPrevTakeOverPlnCode, MBMInstallment, MBMPOLSubNo1, MBMPOLSubNo2,
        MBMPOLIssuedDate, MBMRiskNo, MBMTransNo, MBMPAYNo, MBMPAYSeqNo, MBMClientNo, MBMClientID, MBMPrevPLNCode, MBMRBAmt,
        MBMPrevInsCom, MBMPAYRemarks, MBMCLMNonPayFr, MBMCLMNonPayTo, MBMUpgradedPLN, MBMNewEffDate, MBMEndorsementNo, MBMAppName,
        MBMPayorPremNet, MBMPayorPremGross, MBMPayorMCONet, MBMPayorMCOGross, MBMFamPolicyDisc, MBMRenewDisc, MBMIcBcPp2nd,
        MBMRnBStatus, MBMLoadPrem, MBMFamDiscAmt, MBMRenewDiscAmt, MBMBlackListed, MBMBankACNo, MBMUnderwritingExcess,
        MBMCLNPlanCode, MBMCLNInsType, MBMCLNEffDate, MBMCLNExpDate, MBMCLNExclusion
    FROM dbo.MBMExcellUploadShelly
    WHERE MBMBordxDate = @bordxDate AND PAYCode = @pay
    ORDER BY No;
END
GO

-- Payors of one health type for the ASCII screen (desktop cboHLTCode_Click / SearchPayorHLTType).
CREATE OR ALTER PROCEDURE genisis.Pluto_Payors
    @hlt nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name FROM dbo.PAY WHERE HLTCode = @hlt ORDER BY PAYCode;
END
GO