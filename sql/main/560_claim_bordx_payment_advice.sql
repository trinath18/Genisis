-- Claim Bordx > Payment Advice > Generate Bordx No (legacy frmPayAdviceHosp, "Payment Advice - Generate Bordx"): the Inpatient Case Listing of the MH payor's worksheets flagged for a payment advice
-- (View_MAS_PaymentAdvice: closed, not bordxed, case open, payor MH, CLMMHBordx Y). It searched with a WHERE fragment through Claim_MAS_PaymentAdvice and wrote through text SQL; here the search and the
-- worksheet are procedures with typed parameters, and the bordx number, the bordx row and the marking of each worksheet are the ones Inpatient Case Listing uses (genisis.CaseListing_NextBordxNo,
-- _SaveBordx and _Apply, which now also sets the payment advice number).

-- SearchRecord + WorksheetTrackingListing(2). @scan = 1: one worksheet (case number, optionally a version); 0: a case number range. Dates are inclusive; @doaEmpty / @dodEmpty keep only worksheets with no
-- admission / discharge date; @cashType H or M (INVPayTo); @hospital is the hospital's name.
CREATE OR ALTER PROCEDURE genisis.PayAdvice_Search
    @scan bit, @wksFrom nvarchar(30) = NULL, @wksTo nvarchar(30) = NULL, @version int = NULL,
    @doaFrom datetime = NULL, @doaTo datetime = NULL, @doaEmpty bit = 0, @dodFrom datetime = NULL, @dodTo datetime = NULL, @dodEmpty bit = 0,
    @cashType char(1) = NULL, @hospital nvarchar(100) = NULL, @max int = 5000
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(CASNumber) AS CaseNo, ClaimVersion, RTRIM(PAYCode) AS Payor, CLMTotalApproved AS AppAmt, CASDateAdmitted AS Doa, CASDischargeDate AS Dod, RTRIM(INVPayTo) AS CashType,
        RTRIM(MBMPatientCovID) AS CoverId, RTRIM(MBMCName) AS CoveredName, RTRIM(HOSName) AS Hospital, CLMPayableByMedix2H AS HosAmt, RTRIM(MBMName) AS MemberName, RTRIM(MBMGroupCompany) AS GroupCompany,
        RTRIM(PLNCode) AS PlanCode, RTRIM(ICDCode1) AS IcdCode, RTRIM(MBMBTBG) AS Btbg, RTRIM(INSCode) AS InsuredType, RTRIM(CASClaimability) AS Claimability, RTRIM(MBMCostCenter) AS CostCenter,
        RTRIM(MBMGroupCategory) AS GroupCategory, RTRIM(HOSState) AS HospitalState
    FROM genisis.WS_MasPaymentAdvice
    WHERE (@scan = 0 OR @wksFrom IS NULL OR CASNumber = @wksFrom)
      AND (@scan = 0 OR @version IS NULL OR ClaimVersion = @version)
      AND (@scan = 1 OR @wksFrom IS NULL OR CASNumber >= @wksFrom)
      AND (@scan = 1 OR @wksTo IS NULL OR CASNumber <= @wksTo)
      AND (@doaFrom IS NULL OR CASDateAdmitted >= @doaFrom) AND (@doaTo IS NULL OR CASDateAdmitted <= @doaTo)
      AND (@dodFrom IS NULL OR CASDischargeDate >= @dodFrom) AND (@dodTo IS NULL OR CASDischargeDate <= @dodTo)
      AND (@doaEmpty = 0 OR CASDateAdmitted IS NULL) AND (@dodEmpty = 0 OR CASDischargeDate IS NULL)
      AND (@cashType IS NULL OR INVPayTo = @cashType) AND (@hospital IS NULL OR HOSName = @hospital)
    ORDER BY CASNumber, ClaimVersion
    OPTION (RECOMPILE);
END
GO

-- One pending worksheet as the bordx needs it: the values the selection rules check, who is paid (INVPayTo, BTBG) and the three amounts a bordx can sum. No row when it is not (or no longer) in the list.
CREATE OR ALTER PROCEDURE genisis.PayAdvice_Item
    @cas nvarchar(30), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(p.CASNumber) AS CaseNo, p.ClaimVersion, RTRIM(p.PAYCode) AS Payor, p.CASDischargeDate AS Dod, RTRIM(p.HOSName) AS Hospital, RTRIM(p.ICDCode1) AS IcdCode,
        RTRIM(p.MBMGroupCompany) AS GroupCompany, RTRIM(p.INSCode) AS InsuredType, RTRIM(p.CASClaimability) AS Claimability, RTRIM(p.MBMCostCenter) AS CostCenter, RTRIM(p.MBMGroupCategory) AS GroupCategory,
        RTRIM(p.MBMBTBG) AS Btbg, RTRIM(p.INVPayTo) AS CashType, c.CLMPayableByMedix2H AS Payable2H, c.CLMPayableByMedix2M AS Payable2M, c.CLMTotalApproved AS TotalApproved, ISNULL(c.CLMCaseMgmtFees, 0) AS CaseMgmtFees
    FROM genisis.WS_MasPaymentAdvice p
    INNER JOIN genisis.WS_Claim c ON c.CASNumber = p.CASNumber AND c.ClaimVersion = p.ClaimVersion
    WHERE p.CASNumber = @cas AND p.ClaimVersion = @version;
END
GO
