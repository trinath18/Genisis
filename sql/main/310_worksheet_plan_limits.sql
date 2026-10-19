-- Worksheet calculation engine, step 4: the limits the plan fixes (display_benefit of the Registration form). The benefit rows (BNF) of the patient's plan, insured type and health
-- type that are in force on the admission date: for each benefit code the row with the latest effective date up to it. The plan is the covered person's own when the patient is a
-- dependant and has one, otherwise the member's.
CREATE OR ALTER PROCEDURE genisis.WorksheetEngine_PlanBenefits
    @cas nvarchar(15), @version int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @plancode nvarchar(20), @ins nvarchar(5), @hlt nvarchar(5), @adm datetime;
    SELECT TOP (1)
        @plancode = RTRIM(CASE WHEN v.MBMPatientCovID <> N'00' AND ISNULL(RTRIM(v.MBMCPLNCode), N'') <> N'' THEN v.MBMCPLNCode ELSE v.PLNCode END),
        @ins = RTRIM(v.INSCode), @hlt = RTRIM(v.HLTCode), @adm = v.CASDateAdmitted
    FROM dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v
    WHERE v.CASNumber = @cas AND v.ClaimVersion = @version AND v.MBMNumber IS NOT NULL;

    SELECT x.Code, x.Days, x.Claimable, x.[Limit]
    FROM (SELECT RTRIM(b.BNFCode) AS Code, RTRIM(b.BNFNoOfDays) AS Days, RTRIM(b.BNFClaimAbleAmount) AS Claimable, LTRIM(RTRIM(b.BNFRMPerDis)) AS [Limit],
                 ROW_NUMBER() OVER (PARTITION BY b.BNFCode ORDER BY b.BNFEffDate DESC) AS rn
          FROM dbo.BNF b
          WHERE b.PLNCode = @plancode AND b.INSCode = @ins AND b.HLTCode = @hlt AND (@adm IS NULL OR b.BNFEffDate <= @adm)) x
    WHERE x.rn = 1
    ORDER BY x.Code;
END
GO
