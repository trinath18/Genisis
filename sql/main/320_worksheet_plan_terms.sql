-- Worksheet calculation engine, step 5: the plan's co-payment terms. The plan row (PLN) of the patient's plan, payor and health type: the co-insurance percentage and its sign
-- (PLNCoINSPerc, PLNCoInsSign: a share of the approved total the member pays), and the Room & Board co-payment percentage with its indicator (PLNCoPayPerc, CoPaymentInd).
-- The plan is the covered person's own when the patient is a dependant and has one, otherwise the member's.
CREATE OR ALTER PROCEDURE genisis.WorksheetEngine_PlanTerms
    @cas nvarchar(15), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1)
        ISNULL(p.PLNCoINSPerc, 0) AS CoInsurancePercent, RTRIM(p.PLNCoInsSign) AS CoInsuranceSign,
        ISNULL(p.PLNCoPayPerc, 0) AS RoomCoPayPercent, RTRIM(p.CoPaymentInd) AS CoPaymentInd
    FROM dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v
    JOIN dbo.PLN p ON p.PLNCode = RTRIM(CASE WHEN v.MBMPatientCovID <> N'00' AND ISNULL(RTRIM(v.MBMCPLNCode), N'') <> N'' THEN v.MBMCPLNCode ELSE v.PLNCode END)
                  AND p.PAYCode = v.PAYCode AND p.HLTCode = v.HLTCode
    WHERE v.CASNumber = @cas AND v.ClaimVersion = @version AND v.MBMNumber IS NOT NULL;
END
GO
