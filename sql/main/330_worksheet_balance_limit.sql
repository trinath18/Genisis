-- Worksheet calculation engine, step 5b: the member's available limit (the balance limit of the Registration screen). Whose limit it is depends on the plan's annual limit type
-- (AnnualLimit.SuppLimitStatus, the latest row up to the admission date): for the principal, or a patient with cover 00, it is the member's (MBM.MBMAvailableLimit); for a dependant
-- with type B it is the first covered person's (cover 01), with type C the patient's own (MBMCoveredPersons.MBMCAvailableLimit), and with any other type (A, or no row) the member's.
CREATE OR ALTER PROCEDURE genisis.WorksheetEngine_BalanceLimit
    @cas nvarchar(15), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1)
        CASE WHEN RTRIM(v.MBMMemberType) = N'P' OR v.MBMPatientCovID = N'00' THEN m.MBMAvailableLimit
             WHEN a.SuppLimitStatus = N'B' THEN (SELECT TOP (1) c.MBMCAvailableLimit FROM dbo.MBMCoveredPersons c WHERE c.MBMCNumber = v.MBMNumber AND c.MBMCCoverID = N'01')
             WHEN a.SuppLimitStatus = N'C' THEN (SELECT TOP (1) c.MBMCAvailableLimit FROM dbo.MBMCoveredPersons c WHERE c.MBMCNumber = v.MBMNumber AND c.MBMCCoverID = v.MBMPatientCovID)
             ELSE m.MBMAvailableLimit END AS Balance,
        RTRIM(a.SuppLimitStatus) AS LimitType
    FROM dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v
    JOIN dbo.MBM m ON m.MBMNumber = v.MBMNumber
    OUTER APPLY (SELECT TOP (1) al.SuppLimitStatus FROM dbo.AnnualLimit al
                 WHERE al.PLNCode = RTRIM(v.PLNCode) AND al.INSCode = RTRIM(v.INSCode) AND al.AnnualEffDate <= v.CASDateAdmitted ORDER BY al.AnnualEffDate DESC) a
    WHERE v.CASNumber = @cas AND v.ClaimVersion = @version AND v.MBMNumber IS NOT NULL;
END
GO
