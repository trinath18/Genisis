-- Case > Case Transfer (legacy frmCaseTransferMBMNo, "Case - Transfer of Membership"). Replaces the raw @strAppend
-- dbo.Case_Transfer modes and the inline recordset writes of TransferProcess / ReCalculatePrevMBMLmt.
-- The worksheet totals are read through the genisis.WS_* synonyms, so the whole transfer is one MainDb transaction
-- (the desktop ran its writes on four connections with no working transaction).

-- Text amounts (ClaimApprove stores them as nvarchar) as a number; anything that is not a plain number is 0.
-- ISNUMERIC is not enough: it accepts a lone minus sign, a lone dot and a dollar sign, none of which can be cast.
CREATE OR ALTER FUNCTION genisis.Util_SafeDecimal (@s nvarchar(60))
RETURNS decimal(18, 4)
AS
BEGIN
    DECLARE @t nvarchar(60) = LTRIM(RTRIM(ISNULL(@s, N'')));
    RETURN CASE
        WHEN @t = N'' OR LEN(@t) > 15 OR @t LIKE N'%[^0-9.-]%' OR @t NOT LIKE N'%[0-9]%'
          OR @t LIKE N'%.%.%' OR @t LIKE N'%-%-%' OR (@t LIKE N'%-%' AND LEFT(@t, 1) <> N'-') THEN 0
        ELSE CAST(@t AS decimal(18, 4))
    END;
END
GO

-- showCAS: the member / cover the case is on now.
CREATE OR ALTER PROCEDURE genisis.CaseTransfer_Case
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(CASNumber) AS CaseNumber, RTRIM(MBMNumber) AS MemberNo, RTRIM(MBMPatientCovID) AS CoverId, RTRIM(CASStatus) AS CaseStatus
    FROM dbo.CAS WHERE CASNumber = @cas;
END
GO

-- MBMInfo: member / patient details. No row when the member does not exist; CoverFound = 0 for an unknown supplementary.
CREATE OR ALTER PROCEDURE genisis.CaseTransfer_Member
    @mbm nvarchar(30), @cover nvarchar(10)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(m.MBMNumber) AS MemberNo, @cover AS CoverId,
        RTRIM(CASE WHEN @cover = N'00' THEN m.MBMName ELSE cp.MBMCName END) AS PatientName,
        RTRIM(m.MBMPolicyNo) AS PolicyNo,
        RTRIM(CASE WHEN @cover = N'00' THEN m.MBMIcBcPp ELSE cp.MBMCICBCPPNo END) AS IcNo,
        RTRIM(m.MBMStatus) AS Status, m.MBMPayorEffDate AS EffDate, m.MBMPayorExpDate AS ExpDate,
        CAST(CASE WHEN @cover = N'00' OR cp.MBMCNumber IS NOT NULL THEN 1 ELSE 0 END AS bit) AS CoverFound
    FROM dbo.MBM m
    LEFT JOIN dbo.MBMCoveredPersons cp ON cp.MBMCNumber = m.MBMNumber AND cp.MBMCCoverID = @cover
    WHERE m.MBMNumber = @mbm;
END
GO

-- ReCalculatePrevMBMLmt / the limit part of TransferProcess for one member. @sign = +1 gives the case's approved amount
-- back to the previous member (@giveBack = 1: always applied, the covered person's own plan code is used for the benefit
-- limit lookup), -1 charges the new member (only applied when the amount is not zero).
-- Limit holder: the principal's MBM.MBMAvailableLimit when the plan shares the limit (SuppLimitStatus A) or the patient is
-- the principal; otherwise the covered person's own MBMCAvailableLimit (status B uses cover 01). The benefit balances
-- (cancer / kidney / home nursing) follow the plan's benefit status the same way. The annual limit row is the one in
-- force on the member's payor effective date (the desktop took an arbitrary row of the plan).
CREATE OR ALTER PROCEDURE genisis.CaseTransfer_MoveLimit
    @mbm nvarchar(30), @cov nvarchar(10), @sign int, @total money, @cancer money, @kidney money, @home money, @giveBack bit
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @hlt nvarchar(4), @ins nvarchar(4), @plan nvarchar(8), @eff datetime;
    SELECT @hlt = HLTCode, @ins = INSCode, @plan = PLNCode, @eff = MBMPayorEffDate FROM dbo.MBM WHERE MBMNumber = @mbm;

    DECLARE @status varchar(1);
    SELECT TOP (1) @status = SuppLimitStatus FROM dbo.AnnualLimit
    WHERE PLNCode = @plan AND INSCode = @ins AND HLTCode = @hlt AND AnnualEffDate <= ISNULL(@eff, '19000101')
    ORDER BY AnnualEffDate DESC;
    IF @status IS NULL
        SELECT TOP (1) @status = SuppLimitStatus FROM dbo.AnnualLimit WHERE PLNCode = @plan ORDER BY AnnualEffDate DESC;
    SET @status = CASE WHEN RTRIM(ISNULL(@status, '')) = '' THEN 'A' ELSE RTRIM(@status) END;

    IF @giveBack = 1 OR @total <> 0
    BEGIN
        IF @status = 'A' OR @cov = N'00'
            UPDATE dbo.MBM SET MBMAvailableLimit = ISNULL(MBMAvailableLimit, 0) + @sign * @total WHERE MBMNumber = @mbm;
        ELSE
        BEGIN
            DECLARE @holder nvarchar(10) = CASE WHEN @status = 'B' THEN N'01' ELSE @cov END;
            UPDATE dbo.MBMCoveredPersons SET MBMCAvailableLimit = ISNULL(MBMCAvailableLimit, 0) + @sign * @total
            WHERE MBMCNumber = @mbm AND MBMCCoverID = @holder;
            IF @giveBack = 1
            BEGIN
                DECLARE @coveredPlan nvarchar(8) = (SELECT TOP (1) RTRIM(MBMCPLNCode) FROM dbo.MBMCoveredPersons WHERE MBMCNumber = @mbm AND MBMCCoverID = @holder);
                IF ISNULL(@coveredPlan, N'') <> N'' SET @plan = @coveredPlan;
            END
        END
    END

    DECLARE @benefitStatus varchar(1);
    SELECT TOP (1) @benefitStatus = SuppALBStatus FROM dbo.BnfAnnLmt
    WHERE INSCode = @ins AND PLNCode = @plan AND HLTCode = @hlt AND ALBEffDate <= ISNULL(@eff, '19000101')
    ORDER BY ALBEffDate DESC;
    DECLARE @benefitHolder nvarchar(6) = CASE WHEN RTRIM(ISNULL(@benefitStatus, '')) = 'B' THEN N'01' ELSE @cov END;

    UPDATE dbo.MBMBnfLmt
    SET MBMCancerBalLmt = ISNULL(MBMCancerBalLmt, 0) + @sign * @cancer,
        MBMKidneyBalLmt = ISNULL(MBMKidneyBalLmt, 0) + @sign * @kidney,
        MBMHomeNCBalLmt = ISNULL(MBMHomeNCBalLmt, 0) + @sign * @home
    WHERE MBMNumber = @mbm AND MBMCoveredID = @benefitHolder;
END
GO

-- TransferProcess. The API has already checked the target member / cover and status; the existence checks here only
-- protect the data. Result: 0 transferred, 1 case not found, 2 target member / cover not found, 3 the case is already on
-- that member / cover, 4 the previous member is not on the member table (nothing to give the limit back to).
-- Returns the approved total that moved.
CREATE OR ALTER PROCEDURE genisis.CaseTransfer_Apply
    @cas nvarchar(15), @newMbm nvarchar(30), @newCov nvarchar(10), @userCode nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @oldMbm nvarchar(30), @oldCov nvarchar(10);
    SELECT @oldMbm = RTRIM(MBMNumber), @oldCov = RTRIM(MBMPatientCovID) FROM dbo.CAS WITH (UPDLOCK, ROWLOCK) WHERE CASNumber = @cas;
    IF @oldMbm IS NULL BEGIN SELECT 1 AS Result, CAST(0 AS money) AS Moved; RETURN; END
    IF NOT EXISTS (SELECT 1 FROM dbo.MBM WHERE MBMNumber = @newMbm)
       OR (@newCov <> N'00' AND NOT EXISTS (SELECT 1 FROM dbo.MBMCoveredPersons WHERE MBMCNumber = @newMbm AND MBMCCoverID = @newCov))
    BEGIN SELECT 2 AS Result, CAST(0 AS money) AS Moved; RETURN; END
    IF @oldMbm = @newMbm AND @oldCov = @newCov BEGIN SELECT 3 AS Result, CAST(0 AS money) AS Moved; RETURN; END
    IF NOT EXISTS (SELECT 1 FROM dbo.MBM WHERE MBMNumber = @oldMbm) BEGIN SELECT 4 AS Result, CAST(0 AS money) AS Moved; RETURN; END

    -- What the case has been approved for, summed over every claim version (CLMTotalApproved) and the
    -- cancer / kidney / home nursing benefit amounts (stored as text on ClaimApprove).
    DECLARE @total money = ISNULL((SELECT SUM(CLMTotalApproved) FROM genisis.WS_Claim WHERE CASNumber = @cas), 0);
    DECLARE @cancer money, @kidney money, @home money;
    SELECT @cancer = ISNULL(SUM(genisis.Util_SafeDecimal(CLMIndCancerTreatment)), 0),
        @kidney = ISNULL(SUM(genisis.Util_SafeDecimal(CLMIndKidneyDial)), 0),
        @home = ISNULL(SUM(genisis.Util_SafeDecimal(CLMHomeNCAmt)), 0)
    FROM genisis.WS_ClaimApprove WHERE CASNumber = @cas;

    DECLARE @today datetime = CAST(CAST(GETDATE() AS date) AS datetime);
    DECLARE @note nvarchar(500) = N'~TransMBM by : ' + @userCode + N' Date : ' + CONVERT(nvarchar(8), GETDATE(), 112) + N','
        + @oldMbm + N'-' + @oldCov + N'->' + @newMbm + N'-' + @newCov;

    UPDATE genisis.Maint_CASCrossReference
    SET TransRemarks = RIGHT(ISNULL(TransRemarks, N'') + @note, 2000), MBMNumber = @newMbm, MBMPatientCovID = @newCov
    WHERE CASNumber = @cas;
    UPDATE dbo.CAS
    SET MBMNumber = @newMbm, MBMPatientCovID = @newCov, CASLastUpdateUser = @userCode, CASLastUpdateDate = @today
    WHERE CASNumber = @cas;

    EXEC genisis.CaseTransfer_MoveLimit @oldMbm, @oldCov, 1, @total, @cancer, @kidney, @home, 1;
    EXEC genisis.CaseTransfer_MoveLimit @newMbm, @newCov, -1, @total, @cancer, @kidney, @home, 0;

    SELECT 0 AS Result, @total AS Moved;
END
GO
