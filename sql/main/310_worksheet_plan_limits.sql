-- Worksheet calculation engine, step 4: the limits the plan fixes (display_benefit of the Registration form). The benefit rows (BNF) of the plan the desktop reads, the row with the latest
-- effective date up to its bound for each benefit code. Which plan, in the desktop's order:
--   1. a case with a take-over plan (CAS.CASTakeOverPlan): that plan and the insured type, whatever the health type and date;
--   2. a member with a new plan after renewal (MBMNewPlanCode): the current plan with rows up to the payor effective date when the patient was admitted on or before it, otherwise
--      the new plan with rows up to the new policy effective date;
--   3. a dependant with a plan of their own (MBMCPLNCode): that plan, any date;
--   4. otherwise the member's plan, any date.
-- Not ported: the top-up policy's own plan.
CREATE OR ALTER PROCEDURE genisis.WorksheetEngine_PlanBenefits
    @cas nvarchar(15), @version int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @plancode nvarchar(20), @ins nvarchar(5), @hlt nvarchar(5), @bound datetime, @anyHealthType bit = 0;
    DECLARE @own nvarchar(20), @dependant nvarchar(20), @newPlan nvarchar(20), @takeOver nvarchar(20), @patient nvarchar(2), @adm datetime, @payorEff datetime, @newEff datetime;
    SELECT TOP (1)
        @own = RTRIM(v.PLNCode), @dependant = RTRIM(v.MBMCPLNCode), @newPlan = RTRIM(v.MBMNewPlanCode), @patient = v.MBMPatientCovID,
        @ins = RTRIM(v.INSCode), @hlt = RTRIM(v.HLTCode), @adm = v.CASDateAdmitted, @payorEff = v.MBMPayorEffDate, @newEff = v.MBMNewPolEffDate,
        @takeOver = RTRIM(c.CASTakeOverPlan)
    FROM dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v
    JOIN dbo.CAS c ON c.CASNumber = v.CASNumber
    WHERE v.CASNumber = @cas AND v.ClaimVersion = @version AND v.MBMNumber IS NOT NULL;

    IF ISNULL(@takeOver, N'') <> N'' BEGIN SET @plancode = @takeOver; SET @anyHealthType = 1; END
    ELSE IF ISNULL(@newPlan, N'') <> N''
    BEGIN
        IF @adm <= @payorEff BEGIN SET @plancode = @own; SET @bound = @payorEff; END
        ELSE BEGIN SET @plancode = @newPlan; SET @bound = @newEff; END
    END
    ELSE IF @patient <> N'00' AND ISNULL(@dependant, N'') <> N'' SET @plancode = @dependant;
    ELSE SET @plancode = @own;

    SELECT x.Code, x.Days, x.Claimable, x.[Limit]
    FROM (SELECT RTRIM(b.BNFCode) AS Code, RTRIM(b.BNFNoOfDays) AS Days, RTRIM(b.BNFClaimAbleAmount) AS Claimable, LTRIM(RTRIM(b.BNFRMPerDis)) AS [Limit],
                 ROW_NUMBER() OVER (PARTITION BY b.BNFCode ORDER BY b.BNFEffDate DESC) AS rn
          FROM dbo.BNF b
          WHERE b.PLNCode = @plancode AND b.INSCode = @ins AND (@anyHealthType = 1 OR b.HLTCode = @hlt)
            AND (@bound IS NULL OR b.BNFEffDate <= @bound)) x
    WHERE x.rn = 1
    ORDER BY x.Code;
END
GO
