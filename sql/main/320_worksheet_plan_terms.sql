-- Worksheet calculation engine, step 5: the plan's co-payment terms. The plan row (PLN) of the patient's plan, payor and health type: the co-insurance percentage and its sign
-- (PLNCoINSPerc, PLNCoInsSign: a share of the approved total the member pays), and the Room & Board co-payment percentage with its indicator (PLNCoPayPerc, CoPaymentInd).
-- The plan is the covered person's own when the patient is a dependant and has one, otherwise the member's.
-- Also the member's own co-payment percentage and its indicator (MBM.MBMTopupCopay, MBMTopupCopayInd): payor TK members carry 20 there, and the desktop's major-medical formula uses it.
CREATE OR ALTER PROCEDURE genisis.WorksheetEngine_PlanTerms
    @cas nvarchar(15), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1)
        ISNULL(p.PLNCoINSPerc, 0) AS CoInsurancePercent, RTRIM(p.PLNCoInsSign) AS CoInsuranceSign,
        ISNULL(p.PLNCoPayPerc, 0) AS RoomCoPayPercent, RTRIM(p.CoPaymentInd) AS CoPaymentInd,
        ISNULL(m.MBMTopupCopay, 0) AS MemberCoPayPercent, RTRIM(m.MBMTopupCopayInd) AS MemberCoPayInd,
        -- the Room & Board co-payment (frmWorksheetRegGST2015, cmdDisplay): the plan family, the new SM flag and its MRI share, the major-medical flag, whether the payor (the first two letters of the case number)
        -- carries the Room & Board tax, and what the version's invoices say about the room: the new Room & Board tax, the gross Room & Board and the latest invoice date (the cent rounding applies from 1 April 2008)
        LEFT(RTRIM(p.PLNCode), 1) AS PlanFamily, RTRIM(p.NewSMPlan) AS NewSmPlan, CASE WHEN RTRIM(p.NewSMPlan) = N'Y' THEN ISNULL(p.PLNMRIPercentage, 0) ELSE 0 END AS MriPercent,
        RTRIM(p.PLNMajorMedInd) AS MajorMedInd, LEFT(@cas, 2) AS PayorCode, RTRIM(c.CASHOSCode) AS HospitalCode, (SELECT TOP (1) RTRIM(y.PAYRBTax) FROM dbo.PAY y WHERE y.PAYCode = LEFT(@cas, 2)) AS PayorRoomTax,
        ISNULL(inv.RoomTaxNew, 0) AS RoomTaxNew, ISNULL(inv.GrossRoomBoard, 0) AS GrossRoomBoard, inv.LastInvoiceDate
    FROM dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v
    JOIN dbo.MBM m ON m.MBMNumber = v.MBMNumber
    JOIN dbo.CAS c ON c.CASNumber = v.CASNumber
    OUTER APPLY (SELECT SUM(genisis.Worksheet_Amount(a.CLMNewRBTax)) AS RoomTaxNew, SUM(genisis.Worksheet_Amount(a.CLMGrossRB)) AS GrossRoomBoard, MAX(i.InvDate) AS LastInvoiceDate
                 FROM genisis.WS_ClaimInvoice i JOIN genisis.WS_ClaimInvoiceActual a ON a.INVIndex = i.INVIndex
                 WHERE i.CASNumber = @cas AND i.INVWSversion = CAST(@version AS nvarchar(10))) inv
    JOIN dbo.PLN p ON p.PLNCode = RTRIM(CASE WHEN v.MBMPatientCovID <> N'00' AND ISNULL(RTRIM(v.MBMCPLNCode), N'') <> N'' THEN v.MBMCPLNCode ELSE v.PLNCode END)
                  AND p.PAYCode = v.PAYCode AND p.HLTCode = v.HLTCode
    WHERE v.CASNumber = @cas AND v.ClaimVersion = @version AND v.MBMNumber IS NOT NULL;
END
GO
