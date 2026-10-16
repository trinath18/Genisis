-- Membership > Enquiry: the tabs added after the first read-only release (HIS Benefits, Acc History, Fees, Non Disclosure,
-- Policy Terms, LT Limit, Policy Wording, T/O Claims, WS). The desktop built these queries from string fragments; here they take parameters.

-- HIS Benefits (desktop BNFListing / BNFListingSupp): the benefit schedule in force on the payor effective date, latest effective date only.
CREATE OR ALTER PROCEDURE genisis.Enquiry_Benefits
    @plan nvarchar(4000), @hlt nvarchar(4000), @ins nvarchar(4000), @eff datetime
AS
BEGIN
    SET NOCOUNT ON;
    WITH latest AS (
        SELECT MAX(BNFEffDate) AS EffDate FROM dbo.VIEW_BNF
        WHERE PLNCode = @plan AND HLTCode = @hlt AND INSCode = @ins AND BNFEffDate <= @eff)
    SELECT RTRIM(b.BNFCode) AS BenefitCode, RTRIM(b.BNFdescription) AS Description, ISNULL(b.BNFNoOfDays, 0) AS Days,
        RTRIM(b.BNFClaimAbleAmount) AS RatePerDay, RTRIM(b.BNFRMPerDis) AS PerDisability, b.BNFEffDate AS EffectiveDate
    FROM dbo.VIEW_BNF b JOIN latest l ON b.BNFEffDate = l.EffDate
    WHERE b.PLNCode = @plan AND b.HLTCode = @hlt AND b.INSCode = @ins
    ORDER BY b.BNFCode;
END
GO

-- Acc History (desktop GetAccHisListing): claims paid and receipts for the IC, from the worksheet database.
CREATE OR ALTER PROCEDURE genisis.Enquiry_AccountHistory
    @ic nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(MBMNumber) AS MembershipNo, MBMPayorEffDate AS EffectiveDate, CASDateAdmitted AS Admitted, CaseRegDate AS Registered,
        RTRIM(CASNumber) + '-' + CAST(ClaimVersion AS nvarchar(10)) AS CaseNo,
        CASE WHEN ISNULL(CLMPayableByMedix2M, 0) <> 0 THEN RTRIM(DebitCreditRefNo) END AS DebitCreditRefNo,
        CASE WHEN ISNULL(CLMPayableByMedix2M, 0) <> 0 THEN DebitCreditDate END AS DebitCreditDate,
        CLMPayableByMedix2M AS Payable, RTRIM(ReceiptNo) AS ReceiptNo, ReceiptDate, ISNULL(ReceiptAmt, 0) AS ReceiptAmount,
        ISNULL(ReceiptAmt, 0) + ISNULL(CLMPayableByMedix2M, 0) AS Balance
    FROM genisis.WS_AccountHistory WHERE MBMIcBcPp = @ic;
END
GO

-- Fees tab: premium, MCO and access fee with the refund recorded for a cancelled membership (desktop showMBM).
CREATE OR ALTER PROCEDURE genisis.Enquiry_Fees
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT o.MBMPremium AS Premium, o.MBMMCOFee AS McoFee, o.MBMAccessFee AS AccessFee, o.MBMBasicPrem AS BasicPremium, o.MBMBasicMCO AS BasicMco,
        r.MBMPremReduction AS PremiumRefund, r.MBMMCORefund AS McoRefund, r.MBMProviderRef AS AccessFeeRefund, r.MBMProviderEff AS AccessFeeEffective,
        r.MBMMCOEffective AS McoEffective, r.MBMPremEffective AS PremiumEffective
    FROM dbo.MBM m
    LEFT JOIN dbo.MBMOthers o ON o.MBMNumber = m.MBMNumber
    OUTER APPLY (SELECT TOP (1) * FROM dbo.MBMRefund x WHERE x.MBMNumber = m.MBMNumber AND m.MBMStatus = 'C' ORDER BY x.MIndex) r
    WHERE m.MBMNumber = @number;
END
GO

-- Non Disclosure (desktop SearchPrincipalIll / Case_SearchIllness): pre-existing and underlying illnesses recorded on cases for the IC.
CREATE OR ALTER PROCEDURE genisis.Enquiry_NonDisclosure
    @ic nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(MBMNumber) AS MembershipNo, RTRIM(CASNumber) AS CaseNo, RTRIM(MBMPatientCovID) AS CoverId, CASDateAdmitted AS Admitted,
        CASPreIllDate AS PreExistingDate,
        CASE RTRIM(CASPreIllCAT) WHEN '1' THEN 'Illness' WHEN '2' THEN 'Injury' WHEN '3' THEN 'Hospitalisation' WHEN '4' THEN CASPreIllOthers END AS PreExisting,
        RTRIM(CASPreIllInfBy) AS PreExistingInformedBy, CASUndIllDate AS UnderlyingDate,
        CASE RTRIM(CASUndIllCAT) WHEN '1' THEN 'Diabetes' WHEN '2' THEN 'Hypertension' WHEN '3' THEN CASUndIllOthers END AS Underlying,
        RTRIM(CASUndIllInfBy) AS UnderlyingInformedBy
    FROM dbo.VIEW_Cas_SearchIll WHERE MBMIcBcPp = @ic;
END
GO

-- Policy Terms (desktop PolTermsListing / Case_Registration case 6): health type N plans are per payor.
CREATE OR ALTER PROCEDURE genisis.Enquiry_PolicyTerms
    @plan nvarchar(4000), @hlt nvarchar(4000), @pay nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) PLNRefRqdAdmin AS ReferralForAdmission, PLNRefRqdPreSpec AS ReferralForSpecialist, PLNMinDurHosp AS MinimumHospitalStay,
        PLNPhyVstPerDay AS PhysicianVisitsPerDay, PLNSepPeriodPrin AS SeparationPeriodPrincipal, PLNSepPeriodSup AS SeparationPeriodSupplementary,
        PLNLowerAgeLimit AS LowerAgeLimit, PLNUpperAgeLimit AS UpperAgeLimit, PLNStudentAge AS StudentAge, PLNChildUntilAge AS ChildUntilAge
    FROM dbo.PLN WHERE PLNCode = @plan AND (@hlt = 'S' OR (@hlt = 'N' AND PAYCode = @pay));
END
GO

-- LT Limit (desktop LTLmtListing / SP_SearchLTLmt): annual and lifetime balances with current and historical cases, principal then supplementaries.
CREATE OR ALTER PROCEDURE genisis.Enquiry_LifetimeLimits
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(p.MBMNumber) AS MembershipNo, RTRIM(p.MBMCOVID) AS CoverId, RTRIM(p.MBMName) AS Name, ISNULL(p.MBMAvailableLimit, 0) AS AnnualBalance,
        CASE WHEN RTRIM(p.MBMGuaRenewal) = '3' THEN ISNULL((SELECT TOP (1) MBMLifeTimeLmt FROM dbo.View_Search_LTPrinLmt WHERE MBMNumber = p.MBMTopupNumber), 0)
             ELSE ISNULL(p.MBMAvailableLimit, 0) END AS LifetimeBalance,
        ISNULL(c.Cases, 0) AS Cases, ISNULL(h.CasHistory, 0) AS CaseHistory, ISNULL(c.BordxAmt, 0) AS CaseAmount, ISNULL(h.BordxAmt, 0) AS HistoryAmount
    FROM dbo.View_Search_LTPrinLmt p
    OUTER APPLY (SELECT TOP (1) Cases, BordxAmt FROM dbo.View_Search_LTPrinCase WHERE MBMNumber = p.MBMNumber) c
    OUTER APPLY (SELECT TOP (1) CasHistory, BordxAmt FROM dbo.View_Search_LTPrinCaseHistory WHERE MBMTopupNumber = p.MBMTopupNumber) h
    WHERE p.MBMNumber = @number
    UNION ALL
    SELECT RTRIM(s.MBMCNumber), RTRIM(s.MBMCCoverID), RTRIM(s.MBMCName), ISNULL(s.MBMCAvailableLimit, 0),
        CASE WHEN RTRIM(p.MBMGuaRenewal) = '3' THEN ISNULL((SELECT TOP (1) MBMLifeTimeLmt FROM dbo.View_Search_LTSuppLmt
                 WHERE MBMCNumber = p.MBMTopupNumber AND MBMCCoverID = s.MBMCCoverID), 0)
             ELSE ISNULL(s.MBMCAvailableLimit, 0) END,
        ISNULL(c.Cases, 0), ISNULL(h.CasHistory, 0), ISNULL(c.BordxAmt, 0), ISNULL(h.BordxAmt, 0)
    FROM dbo.View_Search_LTPrinLmt p
    JOIN dbo.View_Search_LTSuppLmt s ON s.MBMCNumber = p.MBMNumber
    OUTER APPLY (SELECT TOP (1) Cases, BordxAmt FROM dbo.View_Search_LTSuppCase WHERE MBMCNumber = s.MBMCNumber AND MBMCCoverID = s.MBMCCoverID) c
    OUTER APPLY (SELECT TOP (1) CasHistory, BordxAmt FROM dbo.VIEW_Search_LTSuppCaseHistory WHERE MBMTopupNumber = s.MBMTopupNumber AND MBMCCoverID = s.MBMCCoverID) h
    WHERE p.MBMNumber = @number AND RTRIM(p.INSCode) IN ('F', 'H');
END
GO

-- T/O Claims (desktop TakeoverClaims): previous insurer's claims for the policy number.
CREATE OR ALTER PROCEDURE genisis.Enquiry_TakeoverClaims
    @policy nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CLMEffDate AS EffectiveDate, CLMExpDate AS ExpiryDate, RTRIM(CLMPatientname) AS Patient, RTRIM(CLMPatientIC) AS PatientIc,
        CLMDOA AS Admitted, CLMDOD AS Discharged, RTRIM(CLMHospital) AS Hospital,
        RTRIM(ISNULL(CLMDiagnosis1, '')) + ISNULL('/' + NULLIF(RTRIM(CLMDiagnosis2), ''), '') + ISNULL('/' + NULLIF(RTRIM(CLMDiagnosis3), ''), '') AS Diagnosis,
        RTRIM(ISNULL(CLMDrName1, '')) + ISNULL('/' + NULLIF(RTRIM(CLMDrName2), ''), '') AS Doctor,
        CLMBillAmt AS BillAmount, CLMAppAmt AS ApprovedAmount, CLMExcessAmt AS ExcessAmount
    FROM dbo.CLMMAATakaful WHERE CLMPolNo = @policy;
END
GO

-- Plan policy wording folder name (desktop showMBM: PLN.PLNPolicyWording).
CREATE OR ALTER PROCEDURE genisis.Enquiry_PolicyWording
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(p.PLNPolicyWording) AS PolicyWording
    FROM dbo.MBM m JOIN dbo.PLN p ON p.PLNCode = m.PLNCode AND p.HLTCode = m.HLTCode AND (m.HLTCode = 'S' OR p.PAYCode = m.PAYCode)
    WHERE m.MBMNumber = @number;
END
GO
