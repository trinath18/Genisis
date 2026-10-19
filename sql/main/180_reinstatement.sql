-- Membership > Member Reinstatement (legacy frmMBMReintstatement).
-- Status, final expiry, covered-person status and the amendment history reuse the Adjustment_* writers;
-- the fees reuse Adjustment_PlanRates / Registration_Mco / Registration_Premium (desktop MemberCallFees).

-- The membership as cmdSearch_Click shows it (Search_MBM / VIEW_Search) plus the lapse date (MBMTwo.MBMCancelDate).
CREATE OR ALTER PROCEDURE genisis.Reinstatement_Load
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(m.MBMNumber) AS MembershipNo, RTRIM(m.MBMName) AS Name, RTRIM(m.MBMPolicyNo) AS PolicyNo,
        RTRIM(m.MBMIcBcPp) AS IcBcPp, RTRIM(m.MBMStatus) AS Status, m.MBMPayorEffDate AS PayorEffectiveDate,
        m.MBMPayorExpDate AS PayorExpiryDate, m.MBMFinalExpiryDate AS FinalExpiryDate, RTRIM(m.INSCode) AS InsuredType,
        RTRIM(m.PLNCode) AS PlanCode, RTRIM(m.MBMAdultChild) AS AdultChild, RTRIM(m.PAYCode) AS PayorCode,
        RTRIM(m.HLTCode) AS HealthCode, RTRIM(m.AGECode) AS AgeCode, RTRIM(m.MBMSex) AS Sex,
        o.MBMMCOFee AS McoFee, o.MBMPremium AS Premium, t.MBMCancelDate AS LapsedDate
    FROM dbo.MBM m
    LEFT JOIN dbo.MBMOthers o ON o.MBMNumber = m.MBMNumber
    LEFT JOIN dbo.MBMTwo t ON t.MBMNumber = m.MBMNumber
    WHERE m.MBMNumber = @number;
END
GO

-- Every covered person, whatever its status: the desktop reinstates all of them.
CREATE OR ALTER PROCEDURE genisis.Reinstatement_Covered
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(MBMCCoverID) AS CoverId, RTRIM(MBMCAdultChild) AS AdultChild, RTRIM(MBMCAgeband) AS AgeCode,
        RTRIM(MBMCSex) AS Sex, MBMCMCO AS McoFee, MBMCPremium AS Premium
    FROM dbo.MBMCoveredPersons WHERE MBMCNumber = @number ORDER BY MBMCCoverID;
END
GO

-- One MBMReinstatement row per reinstated cover (00 = principal); invoiced later by Contribution Billing > MCO Reinstatement.
CREATE OR ALTER PROCEDURE genisis.Reinstatement_Insert
    @number nvarchar(4000), @coverId nvarchar(4000), @endtBordx datetime, @batch nvarchar(4000), @mco money, @premium money,
    @lapsed datetime, @reinstate datetime, @user nvarchar(4000), @today datetime
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.MBMReinstatement (MBMNumber, MBMCoverID, MBMEndtBordxDate, MBMMCOFee, MBMPremium, MBMLapsedDate,
        MBMReinstateDate, LastUpdateUSR, LastUpdateDate, MBMEndtBordxBatch)
    VALUES (@number, @coverId, @endtBordx, @mco, @premium, @lapsed, @reinstate, @user, @today, @batch);
END
GO
