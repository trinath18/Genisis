-- Membership > Adjustment (legacy frmNewMBMAdjustment + frmNewMBMOthers). Reads for the screen and the writes behind each
-- endorsement type. All writes run inside the API's transaction; main, maintenance and clinical tables are reached on one
-- connection (maintenance through genisis.Maint_*, clinical through genisis.Cis_*).

-- ===== Reads =====

-- Next (@direction 1) or previous (-1) membership number (desktop cmdNext / cmdPrev).
CREATE OR ALTER PROCEDURE genisis.Adjustment_Navigate
    @from nvarchar(4000), @direction int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) MBMNumber FROM dbo.MBM
    WHERE (@direction = 1 AND MBMNumber > @from) OR (@direction = -1 AND MBMNumber < @from)
    ORDER BY CASE WHEN @direction = -1 THEN NULL ELSE MBMNumber END, MBMNumber DESC
    OPTION (RECOMPILE);
END
GO

-- Result sets: principal, covered persons, clinical plan (MBMCLNOthers), annual limit in force on the payor effective date.
-- Status follows SearchMBMCovPersonsProc: a cancellation dated in the future without a cancel date still shows as active.
CREATE OR ALTER PROCEDURE genisis.Adjustment_Load
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(m.MBMNumber) AS MembershipNo, RTRIM(m.HLTCode) AS HealthCode, RTRIM(m.PAYCode) AS PayorCode, RTRIM(m.INSCode) AS InsuredType,
        RTRIM(m.PLNCode) AS PlanCode, RTRIM(m.MBMPolicyNo) AS PolicyNo, RTRIM(m.MBMName) AS Name, RTRIM(m.MBMIcBcPp) AS IcBcPp,
        m.MBMDOB AS DateOfBirth, RTRIM(m.MBMSex) AS Sex, RTRIM(m.RACCode) AS RaceCode,
        RTRIM(m.MBMAddress1) AS Address1, RTRIM(m.MBMAddress2) AS Address2, RTRIM(m.MBMAddress3) AS Address3,
        RTRIM(m.CTYCode) AS City, RTRIM(m.STACode) AS State, RTRIM(m.MBMPostCode) AS PostCode,
        m.MBMPayorEffDate AS PayorEffectiveDate, m.MBMPayorExpDate AS PayorExpiryDate, m.MBMBordxDate AS BordxDate, m.MBMBatchNo AS BatchNo,
        RTRIM(m.AGECode) AS AgeCode, RTRIM(m.MBMAdultChild) AS AdultChild, RTRIM(m.MBMTakeover) AS TakeOver, RTRIM(m.MBMRenewFlag) AS Renewal,
        RTRIM(m.MBMDataStatus) AS DataStatus,
        RTRIM(CASE WHEN m.MBMStatus = 'C' AND m.MBMPayorEffDate > CAST(GETDATE() AS date) AND t.MBMCancelDate IS NULL THEN 'A' ELSE m.MBMStatus END) AS Status,
        RTRIM(m.MBMExpiredStatus) AS ExpiredStatus, m.MBMAvailableLimit AS AvailableLimit, m.MBMValueDate AS ValueDate,
        RTRIM(m.MBMPolicyYear) AS PolicyYear, m.MBMFinalExpiryDate AS FinalExpiryDate, m.MBMNextDueDate AS NextDueDate,
        RTRIM(m.MBMTopupInd) AS TopupInd, RTRIM(m.MBMTopupNumber) AS TopupNumber,
        RTRIM(t.MBMSalutation) AS Salutation, RTRIM(t.MBMEmail) AS Email, RTRIM(t.LGNCode) AS LgnCode, RTRIM(t.NATCode) AS NationalityCode,
        RTRIM(t.MBMTelnoH) AS TelHome, RTRIM(t.MBMTelNoM) AS TelMobile, RTRIM(t.MBMTelNoO) AS TelOffice, t.MBMMmgtNotes AS MgmtNotes,
        RTRIM(t.MBMIcBcPp2nd) AS OtherIc, RTRIM(t.MBMSendingMode) AS SendingMode, t.MBMCardDate AS CardSentDate, t.MBMCancelDate AS CancelDate,
        RTRIM(o.MBMDepartment) AS Department, RTRIM(o.MBMGroupCompany) AS GroupCompany, RTRIM(o.MBMPOLSubNo1) AS PolicySubNo,
        CAST(CASE WHEN o.LabelStatus = 'Y' THEN 1 ELSE 0 END AS bit) AS LabelStatus, RTRIM(o.MBMEmployeeNo) AS EmployeeNo,
        RTRIM(o.MBMAgentCode) AS AgentCode, RTRIM(o.MBMInstallment) AS InstallmentMode, o.MBMMCOFee AS McoFee, o.MBMBasicMCO AS BasicMco,
        RTRIM(o.MBMBranch) AS Branch, o.MBMBasicPrem AS BasicPremium, o.MBMPremium AS Premium, o.MBMFirstJoinedDate AS DateJoined,
        o.MBMClmNonPayFr AS ClaimNonPayFrom, o.MBMClmNonPayTo AS ClaimNonPayTo, RTRIM(o.MBMNewPlanCode) AS NewPlanCode,
        o.MBMNewPolEffDate AS NewPolicyEffectiveDate, o.MBMPolicyDisc AS PolicyDisc, o.MBMRenewalDisc AS RenewalDisc,
        RTRIM(o.MBMPolCondition) AS PolicyCondition, o.MBMUndExcess AS UnderwritingExcess, RTRIM(o.MBMENDtRefNo) AS EndtRefNo,
        RTRIM(o.MBMCostCenter) AS CostCenter, RTRIM(o.MBMPosition) AS Position, RTRIM(o.MBMGroupCategory) AS GroupCategory,
        RTRIM(o.MBMDivision) AS Division, RTRIM(o.MBMSubDivision) AS SubDivision, RTRIM(o.Subsidiary) AS Subsidiary,
        RTRIM(o.MBMGuaRenewal) AS GuaranteeRenewal, RTRIM(o.MBMInvoiceNo) AS InvoiceNo, o.MBMInvoiceDate AS InvoiceDate,
        RTRIM(o.MBMCRNote) AS CreditNote, o.MBMCRNoteDate AS CreditNoteDate,
        w.MBMAllergic AS Allergic, w.MBMRemarks AS Remarks, RTRIM(w.MBMWeight) AS Weight, RTRIM(w.MBMHeight) AS Height,
        RTRIM(w.MBMWorkNature) AS WorkNature, RTRIM(w.MBMBlood) AS Blood, RTRIM(w.MBMSmoking) AS Smoking, RTRIM(w.MBMAlcohol) AS Alcohol,
        RTRIM(w.MBMMaritalStatus) AS MaritalStatus, RTRIM(w.MBMPrevPLNCode) AS PreviousPlanCode, w.MBMRBAmt AS RbAmount,
        RTRIM(w.MBMPrevInsCom) AS PreviousInsurer, RTRIM(w.MBMOccupation) AS Occupation, RTRIM(w.MBMCoPayRB) AS CoPayRb,
        w.MBMGenWP AS GeneralWaitingDays, w.MBMGenEndWP AS GeneralWaitingEnd, w.MBMPreExWP AS PreExistingWaitingDays,
        w.MBMPreExEndWP AS PreExistingWaitingEnd, w.MBMSpeIllWP AS SpecificIllnessWaitingDays, w.MBMSpeIllEndWP AS SpecificIllnessWaitingEnd,
        RTRIM(w.MBMStaffDec) AS StaffDiscount, RTRIM(w.MBMBTBG) AS Btbg, w.MBMPayorPremGross AS PayorPremiumGross,
        w.MBMPayorPremNet AS PayorPremiumNet, w.MBMPayorMCOGross AS PayorMcoGross, w.MBMPayorMCONet AS PayorMcoNet,
        w.MBMFamDiscAmt AS FamilyDiscountAmount, w.MBMRenewDiscAmt AS RenewalDiscountAmount, w.MBMLoadingPrem AS LoadingPremium,
        w.MBMExclusion AS Exclusion, RTRIM(w.MBMBankACNo) AS BankAccountNo, w.MBMTopupJoinDate AS TopupJoinDate,
        RTRIM(p.PAYGRPCode) AS PayorGroupCode
    FROM dbo.MBM m
    INNER JOIN dbo.MBMTwo t ON t.MBMNumber = m.MBMNumber
    LEFT JOIN dbo.MBMOthers o ON o.MBMNumber = m.MBMNumber
    LEFT JOIN dbo.MBMOthersTwo w ON w.MBMNumber = m.MBMNumber
    LEFT JOIN dbo.PAY p ON p.PAYCode = m.PAYCode
    WHERE m.MBMNumber = @number;

    SELECT RTRIM(c.MBMCCoverID) AS CoverId, RTRIM(c2.MBMCSalutation) AS Salutation, RTRIM(c.MBMCName) AS Name,
        RTRIM(c.MBMCICBCPPNo) AS IcBcPp, c.MBMCDOB AS DateOfBirth, RTRIM(c.MBMCSex) AS Sex, RTRIM(c.MBMCAdultChild) AS AdultChild,
        RTRIM(c.MBMCRELCode) AS Relationship, RTRIM(c.MBMCPLNCode) AS PlanCode,
        RTRIM(CASE WHEN c.MBMCStatus = 'C' AND c.MBMCEffDate > CAST(GETDATE() AS date) AND c.MBMCCancelDate IS NULL THEN 'A' ELSE c.MBMCStatus END) AS Status,
        c2.MBMCExclusion AS Exclusion, c2.MBMCAllergic AS Allergic, c.MBMCAnnualLimit AS AnnualLimit, c.MBMCAvailableLimit AS AvailableLimit,
        c.MBMCEffDate AS EffectiveDate, c.MBMCExpDate AS ExpiryDate, c.MBMCCancelDate AS CancelDate, c2.MBMCUndExcess AS UnderwritingExcess,
        c.MBMCMCO AS Mco, c.MBMCBasicMCO AS BasicMco, c.MBMCPremium AS Premium, c.MBMCBasicPremium AS BasicPremium,
        RTRIM(c.MBMInvoiceNo) AS InvoiceNo, c.MBMInvoiceDate AS InvoiceDate, RTRIM(c2.MBMCRNote) AS CreditNote, c2.MBMCRNoteDate AS CreditNoteDate,
        RTRIM(c2.MBMCPrevPLNCode) AS PreviousPlanCode, c2.MBMCRBAmt AS RbAmount, RTRIM(c2.MBMCPAYRemarks) AS PayorRemarks,
        RTRIM(c2.MBMCPrevInsCom) AS PreviousInsurer, c2.MBMCGenWP AS GeneralWaitingDays, c2.MBMCGenEndWP AS GeneralWaitingEnd,
        c2.MBMCPreExWP AS PreExistingWaitingDays, c2.MBMCPreExEndWP AS PreExistingWaitingEnd, c2.MBMCSpeIllWP AS SpecificIllnessWaitingDays,
        c2.MBMSpeIllEndWP AS SpecificIllnessWaitingEnd, RTRIM(c2.MBMCStaffDec) AS StaffDiscount, c2.MBMCClmNonPayFr AS ClaimNonPayFrom,
        c2.MBMCClmNonPayTo AS ClaimNonPayTo, c2.MBMCPayorPremGross AS PayorPremiumGross, c2.MBMCPayorPremNet AS PayorPremiumNet,
        c2.MBMCPayorMCOGross AS PayorMcoGross, c2.MBMCPayorMCONet AS PayorMcoNet, c2.MBMCRenewalDisc AS RenewalDisc,
        c2.MBMCPolicyDisc AS PolicyDisc, c2.MBMCFamDiscAmt AS FamilyDiscountAmount, c2.MBMCRenewDiscAmt AS RenewalDiscountAmount,
        c2.MBMCLoadingPrem AS LoadingPremium, RTRIM(c2.MBMCPAYNo) AS PayorMemberNo, RTRIM(c2.MBMCPAYSeqNo) AS PayorSeqNo,
        RTRIM(c2.MBMCClientNO) AS ClientNo, RTRIM(c2.MBMCClientID) AS ClientId, RTRIM(c2.MBMCIcBcPp2nd) AS OtherIc
    FROM dbo.MBMCoveredPersons c
    LEFT JOIN dbo.MBMCoveredPersonsTwo c2 ON c2.MBMCNumber = c.MBMCNumber AND c2.MBMCCoverID = c.MBMCCoverID
    WHERE c.MBMCNumber = @number
    ORDER BY c.MBMCCoverID;

    SELECT TOP (1) RTRIM(PLNType) AS PlanCode, RTRIM(INSType) AS InsuredType, MBMPolEffDate AS EffectiveDate, MBMPolExpDate AS ExpiryDate
    FROM dbo.MBMCLNOthers WHERE MBMNumber = @number;

    SELECT TOP (1) a.AnnLimit AS AnnualLimit, a.DisabilityLmt AS DisabilityLimit, RTRIM(a.SuppLimitStatus) AS SuppLimitStatus,
        RTRIM(l.LimitDesc) AS SuppLimitDescription
    FROM dbo.MBM m
    INNER JOIN dbo.AnnualLimit a ON a.PLNCode = m.PLNCode AND a.INSCode = m.INSCode AND a.HLTCode = m.HLTCode AND a.AnnualEffDate <= m.MBMPayorEffDate
    LEFT JOIN dbo.AnnualLimitType l ON l.LimitCode = a.SuppLimitStatus
    WHERE m.MBMNumber = @number
    ORDER BY a.AnnualEffDate DESC;
END
GO

-- Fees tab: refund rows (the desktop shows the last one per cover ID) and reinstatement rows.
CREATE OR ALTER PROCEDURE genisis.Adjustment_FeeAdjustments
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(MBMCovID) AS CoverId, MBMMCOEffective AS McoEffective, MBMMCORefund AS McoRefund,
        MBMPremEffective AS PremiumEffective, MBMPremReduction AS PremiumReduction
    FROM dbo.MBMRefund WHERE MBMNumber = @number ORDER BY MIndex;
    SELECT RTRIM(MBMCoverID) AS CoverId, MBMMCOFee AS Mco, MBMPremium AS Premium, RTRIM(MBMInvoiceNo) AS InvoiceNo, MBMInvoiceDate AS InvoiceDate
    FROM dbo.MBMReinstatement WHERE MBMNumber = @number;
END
GO

-- MBM History tab (desktop MBMHISListing / SearchMBMHistory06 "Princ"): by IC (dashes ignored) when it is longer than 4
-- characters, otherwise by name and date of birth.
CREATE OR ALTER PROCEDURE genisis.Adjustment_History
    @ic nvarchar(4000) = NULL, @name nvarchar(4000) = NULL, @dob datetime = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT TOP (200) RTRIM(x.MBMNumber) AS MembershipNo, RTRIM(x.MBMTopupInd) AS TopupInd, RTRIM(x.MBMTopupNumber) AS TopupNumber,
        RTRIM(x.MBMPolicyNo) AS PolicyNo, x.MBMPayorEffDate AS EffectiveDate, x.MBMPayorExpDate AS ExpiryDate,
        CASE WHEN m.MBMStatus = 'C' THEN t.MBMENDtEffDate END AS CancelDate, RTRIM(m.MBMDataStatus) AS DataStatus,
        RTRIM(m.MBMStatus) AS Status, x.MBMBordxDate AS BordxDate, t.MBMENDtBordxDate AS EndtBordxDate, m.MBMValueDate AS ValueDate
    FROM genisis.Maint_MBMCrossReference x
    INNER JOIN dbo.MBM m ON m.MBMNumber = x.MBMNumber
    INNER JOIN dbo.MBMTwo t ON t.MBMNumber = x.MBMNumber
    WHERE (LEN(@ic) > 4 AND REPLACE(x.MBMIcBcPp, '-', '') = REPLACE(@ic, '-', ''))
       OR ((@ic IS NULL OR LEN(@ic) <= 4) AND x.MBMName = @name AND x.MBMDOB = @dob)
    ORDER BY x.MBMPayorEffDate DESC
    OPTION (RECOMPILE);
END
GO

-- Last endorsement batch for the bordereaux date (desktop txtEndtBordxDate_LostFocus); 1 when none.
CREATE OR ALTER PROCEDURE genisis.Adjustment_LastBatch
    @bordx datetime, @pay nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ISNULL((SELECT TOP (1) BATSequence FROM dbo.BAT
        WHERE BATBordxDate = @bordx AND BordxType = 'E' AND (@pay IS NULL OR PAYCode = @pay) ORDER BY BATSequence DESC), 1) AS LastBatch;
END
GO

-- Another membership using the policy number (desktop txtMBMPolNo_LostFocus warning).
CREATE OR ALTER PROCEDURE genisis.Adjustment_PolicyOwner
    @policy nvarchar(4000), @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(MBMNumber) AS MembershipNo FROM genisis.Maint_MBMCrossReference WHERE MBMPolicyNo = @policy AND MBMNumber <> @number;
END
GO

CREATE OR ALTER PROCEDURE genisis.Adjustment_ValidDates
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) MBMDOBStart AS DobStart, MBMDOBEnd AS DobEnd, MBMEffStart AS EffectiveStart, MBMEffEnd AS EffectiveEnd,
        MBMExpStart AS ExpiryStart, MBMExpEnd AS ExpiryEnd
    FROM dbo.MBMValidDateRange ORDER BY ValidID DESC;
END
GO

-- Plan settings for fee recalculation (desktop MemberCallFees): health type N plans are per payor.
CREATE OR ALTER PROCEDURE genisis.Adjustment_PlanRates
    @plan nvarchar(4000), @hlt nvarchar(4000), @pay nvarchar(4000), @eff datetime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(PLNRoundUpStatus) AS RoundUpStatus, RTRIM(PLNProRate) AS ProRate, RTRIM(PLNPremGenderStatus) AS PremGenderStatus,
        RTRIM(PRMInd) AS PremiumInd
    FROM dbo.PLN
    WHERE PLNCode = @plan AND HLTCode = @hlt AND PLNEffDate <= @eff AND (@hlt <> 'N' OR PAYCode = @pay)
    ORDER BY PLNEffDate DESC;
END
GO

-- Lifetime flag and supplementary age limit of a plan code (desktop GetSuppExpDate / AddSupplementaryAdjustment).
CREATE OR ALTER PROCEDURE genisis.Adjustment_PlanSettings
    @plan nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(PLNLifeTimeStatus) AS LifetimeStatus, PlnAgeLimit AS AgeLimit, RTRIM(PLNRoundUpStatus) AS RoundUpStatus
    FROM dbo.PLN WHERE PLNCode = @plan ORDER BY PLNEffDate DESC;
END
GO

CREATE OR ALTER PROCEDURE genisis.Adjustment_DiscPlan
    @plan nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) PolicyDisc, RenewalDisc FROM genisis.Maint_DiscPlan WHERE PLNCode = @plan ORDER BY DISIndex;
END
GO

-- Supplementary limit status of a plan code: latest annual limit row regardless of insured / health type (desktop AddSupplementaryAdjustment).
CREATE OR ALTER PROCEDURE genisis.Adjustment_SuppLimit
    @plan nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(SuppLimitStatus) AS SuppLimitStatus, SuppLimit AS SuppLimitAmount FROM dbo.AnnualLimit WHERE PLNCode = @plan ORDER BY AnnualEffDate DESC;
END
GO

-- Supplementary premium status and base amount for a plan / age band / insured type (desktop AddSupplementaryAdjustment).
CREATE OR ALTER PROCEDURE genisis.Adjustment_SuppPremium
    @plan nvarchar(4000), @ageCode nvarchar(4000), @ins nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(SuppPrmStatus) AS SuppPrmStatus, SuppPrmAmt FROM dbo.PRM
    WHERE PLNCode = @plan AND AGECode = @ageCode AND INSCode = @ins ORDER BY PRMEffDate DESC;
END
GO

CREATE OR ALTER PROCEDURE genisis.Adjustment_NextCoverId
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ISNULL(MAX(TRY_CAST(MBMCCoverID AS int)), 0) + 1 AS NextCoverId FROM dbo.MBMCoveredPersons WITH (UPDLOCK, HOLDLOCK) WHERE MBMCNumber = @number;
END
GO

CREATE OR ALTER PROCEDURE genisis.Adjustment_Now
AS
BEGIN
    SET NOCOUNT ON;
    SELECT GETDATE() AS Now;
END
GO

-- Outstanding HIS / clinical case on a covered person (desktop CmdRemove_Click).
CREATE OR ALTER PROCEDURE genisis.Adjustment_OpenCases
    @number nvarchar(4000), @coverId nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM genisis.Maint_CASCrossReference WHERE MBMNumber = @number AND MBMPatientCovID = @coverId AND CASStatus = 'O')
                THEN 1 ELSE 0 END AS bit) AS HisCase,
           CAST(CASE WHEN EXISTS (SELECT 1 FROM genisis.Cis_CISCas WHERE MBMnumber = @number AND CoverID = @coverId AND CASStatus = 'O')
                THEN 1 ELSE 0 END AS bit) AS ClinicalCase;
END
GO

-- ===== Clinical (CISDB) reads for a new supplementary on a clinical member (desktop SaveCISCovPerson) =====

CREATE OR ALTER PROCEDURE genisis.Adjustment_CisPlan
    @plan nvarchar(4000), @hlt nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(DCStatus) AS DcStatus, RTRIM(PAYCode) AS PayorCode, RTRIM(SDentalStatus) AS SuppDentalStatus,
        RTRIM(SSpecStatus) AS SuppSpecialistStatus, RTRIM(SMEPStatus) AS SuppMepStatus, RTRIM(PLNRoundUpStatus) AS RoundUpStatus,
        RTRIM(PLNProRate) AS ProRate, RTRIM(MCOProcfeeStatus) AS McoProcFeeStatus
    FROM genisis.Cis_PLN WHERE PLNCode = @plan AND HLTCode = @hlt;
END
GO

CREATE OR ALTER PROCEDURE genisis.Adjustment_CisLimit
    @ins nvarchar(4000), @plan nvarchar(4000), @hlt nvarchar(4000), @eff datetime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(SuppLimitStatus) AS SuppLimitStatus, SuppLimit, SDentalAnnLimit AS DentalLimit, SSpecAnnLimit AS SpecialistLimit,
        SMEPAnnLimit AS MepLimit
    FROM genisis.Cis_AnnualLimit WHERE INSCode = @ins AND PLNCode = @plan AND HLTCode = @hlt AND AnnEffDate <= @eff ORDER BY AnnEffDate DESC;
    SELECT TOP (1) VisitLimit FROM genisis.Cis_VisitLimit
    WHERE INSCode = @ins AND PLNCode = @plan AND HLTCode = @hlt AND VisitEffDate <= @eff ORDER BY VisitEffDate DESC;
END
GO

-- Result sets: premium status (payor filter only for health types other than S), supplementary premium for the age band and payor.
CREATE OR ALTER PROCEDURE genisis.Adjustment_CisPremium
    @ins nvarchar(4000), @plan nvarchar(4000), @hlt nvarchar(4000), @pay nvarchar(4000), @ageCode nvarchar(4000), @eff datetime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(SuppPRMStatus) AS SuppPrmStatus FROM genisis.Cis_PRM
    WHERE PLNCode = @plan AND HLTCode = @hlt AND INSCode = @ins AND (@hlt = 'S' OR PAYCode = @pay) AND PRMEffDate <= @eff ORDER BY PRMEffDate DESC;
    SELECT TOP (1) SuppPRMAmt AS SuppPrmAmt FROM genisis.Cis_PRM
    WHERE INSCode = @ins AND HLTCode = @hlt AND PLNCode = @plan AND AGECode = @ageCode AND PAYCode = @pay AND PRMEffDate <= @eff
    ORDER BY PRMEffDate DESC;
END
GO

-- Plan-specific MCO row, else the default row (PLNCode NULL).
CREATE OR ALTER PROCEDURE genisis.Adjustment_CisMco
    @ins nvarchar(4000), @plan nvarchar(4000), @hlt nvarchar(4000), @eff datetime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(SuppMCOStatus) AS SuppMcoStatus, MCOSuppAdultAmt AS SuppAdult, MCOSuppChildAmt AS SuppChild
    FROM genisis.Cis_MCO
    WHERE INSCode = @ins AND HLTCode = @hlt AND (PLNCode = @plan OR PLNCode IS NULL) AND MCOEffDate <= @eff
    ORDER BY CASE WHEN PLNCode IS NULL THEN 1 ELSE 0 END, MCOEffDate DESC;
END
GO

-- ===== Writes =====

-- Membership and cross-reference status; a cancellation also sets the final expiry date (desktop Write2MBMnHistory).
CREATE OR ALTER PROCEDURE genisis.Adjustment_SetStatus
    @number nvarchar(4000), @status nvarchar(4000), @finalExpiry datetime = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.Maint_MBMCrossReference SET MBMStatus = @status, MBMFinalExpiryDate = COALESCE(@finalExpiry, MBMFinalExpiryDate)
    WHERE MBMNumber = @number;
    UPDATE dbo.MBM SET MBMStatus = @status, MBMFinalExpiryDate = COALESCE(@finalExpiry, MBMFinalExpiryDate) WHERE MBMNumber = @number;
END
GO

CREATE OR ALTER PROCEDURE genisis.Adjustment_SetEndorsement
    @number nvarchar(4000), @type nvarchar(4000), @endtEff datetime, @endtBordx datetime, @cancel bit
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.MBMTwo SET MBMLastEndorseStatus = @type, MBMENDtEffDate = @endtEff, MBMENDtBordxDate = @endtBordx,
        MBMCancelDate = CASE WHEN @cancel = 1 THEN @endtEff ELSE MBMCancelDate END
    WHERE MBMNumber = @number;
END
GO

CREATE OR ALTER PROCEDURE genisis.Adjustment_InsertAmendHistory
    @number nvarchar(4000), @policy nvarchar(4000), @ic nvarchar(4000), @name nvarchar(4000), @expiry datetime = NULL,
    @plan nvarchar(4000), @type nvarchar(4000), @endtBordx datetime, @endtEff datetime, @user nvarchar(4000), @today datetime,
    @batch int = NULL, @coverId nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.MBMAmendHistory (MBMNumber, MBMPolicyNo, MBMIcBcPp, MBMName, MBMExpiryDate, MBMPlanCode, MBMAmendEdtType,
        MBMAEndtBordxDate, MBMAEndtEffDate, MBMAmendUser, MBMAmendDate, MBMAEndtBatchNo, MBMCoverID)
    VALUES (@number, @policy, @ic, @name, @expiry, CASE WHEN @expiry IS NULL THEN NULL ELSE @plan END, @type,
        @endtBordx, @endtEff, @user, @today, @batch, @coverId);
END
GO

-- Every covered person gets @status (desktop Write2MBMnHistory for cancellation, suspension and policy deletion).
CREATE OR ALTER PROCEDURE genisis.Adjustment_SetCoveredStatus
    @number nvarchar(4000), @status nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.MBMCoveredPersons SET MBMCStatus = @status WHERE MBMCNumber = @number;
END
GO

-- Desktop WriteAdjStatus2Cov: covered persons not already cancelled (K / C) take @status; @cancelDate (cancellations) is set on all.
-- @coverId NULL = every covered person.
CREATE OR ALTER PROCEDURE genisis.Adjustment_SetCoveredAdjustment
    @number nvarchar(4000), @status nvarchar(4000), @coverId nvarchar(4000) = NULL, @cancelDate datetime = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.MBMCoveredPersons
    SET MBMCStatus = CASE WHEN RTRIM(ISNULL(MBMCStatus, '')) IN ('K', 'C') THEN MBMCStatus ELSE @status END,
        MBMCCancelDate = COALESCE(@cancelDate, MBMCCancelDate)
    WHERE MBMCNumber = @number AND (@coverId IS NULL OR MBMCCoverID = @coverId);
END
GO

-- MCO / premium refund row (the desktop's own Upload_MBMMcoRefund, called unchanged).
CREATE OR ALTER PROCEDURE genisis.Adjustment_InsertRefund
    @number nvarchar(4000), @coverId nvarchar(4000), @premiumEffective decimal(19, 4), @premiumReduction decimal(19, 4),
    @mcoEffective decimal(19, 4), @mcoRefund decimal(19, 4), @pay nvarchar(4000), @bordx datetime, @batch int = NULL, @hlt nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.Upload_MBMMcoRefund @number, @coverId, @premiumEffective, @premiumReduction, @mcoEffective, @mcoRefund, @pay, @bordx, @batch, @hlt;
END
GO

-- Name / date of birth change on a renewal number moves the renewal sequence key and renames every renewal of the base number
-- (desktop SavePrincipalAdjustment; it matched MBMNumber LIKE '%base%', this matches the base number and its *nn renewals only).
CREATE OR ALTER PROCEDURE genisis.Adjustment_RenameRenewals
    @baseNumber nvarchar(4000), @previousKey nvarchar(4000), @newKey nvarchar(4000), @name nvarchar(4000), @dob datetime
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.Maint_MBMSEQRenew05 SET RewMBMSEQKey = @newKey WHERE RewMBMSEQKey = @previousKey;
    UPDATE dbo.MBM SET MBMName = @name, MBMDOB = @dob
    WHERE MBMNumber = @baseNumber OR MBMNumber LIKE REPLACE(REPLACE(@baseNumber, '[', '[[]'), '_', '[_]') + N'*%';
    UPDATE genisis.Maint_MBMCrossReference SET MBMName = @name, MBMDOB = @dob
    WHERE MBMNumber = @baseNumber OR MBMNumber LIKE REPLACE(REPLACE(@baseNumber, '[', '[[]'), '_', '[_]') + N'*%';
END
GO

-- Principal on MBM and the cross-reference (desktop SavePrincipalAdjustment). @availableLimit NULL keeps the stored limit.
CREATE OR ALTER PROCEDURE genisis.Adjustment_SavePrincipal
    @number nvarchar(4000), @name nvarchar(4000), @policy nvarchar(4000), @sex nvarchar(4000), @add1 nvarchar(4000),
    @add2 nvarchar(4000), @add3 nvarchar(4000), @city nvarchar(4000), @state nvarchar(4000), @postCode nvarchar(4000),
    @ic nvarchar(4000), @dob datetime, @race nvarchar(4000), @ageCode nvarchar(4000), @eff datetime = NULL, @exp datetime = NULL,
    @bordx datetime, @takeOver nvarchar(4000), @renewal nvarchar(4000), @dataStatus nvarchar(4000), @status nvarchar(4000),
    @plan nvarchar(4000), @availableLimit decimal(19, 4) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.MBM SET MBMName = @name, MBMPolicyNo = @policy, MBMSex = @sex, MBMAddress1 = @add1, MBMAddress2 = @add2, MBMAddress3 = @add3,
        CTYCode = @city, STACode = @state, MBMPostCode = @postCode, MBMIcBcPp = @ic, MBMDOB = @dob, RACCode = @race, AGECode = @ageCode,
        MBMPayorEffDate = COALESCE(@eff, MBMPayorEffDate), MBMPayorExpDate = COALESCE(@exp, MBMPayorExpDate), MBMBordxDate = @bordx,
        MBMTakeover = @takeOver, MBMRenewFlag = @renewal, MBMDataStatus = @dataStatus, MBMStatus = @status, PLNCode = @plan,
        MBMAvailableLimit = COALESCE(@availableLimit, MBMAvailableLimit)
    WHERE MBMNumber = @number;
    UPDATE genisis.Maint_MBMCrossReference SET MBMName = @name, MBMPolicyNo = @policy, MBMIcBcPp = @ic, MBMDOB = @dob,
        MBMPayorEffDate = COALESCE(@eff, MBMPayorEffDate), MBMPayorExpDate = COALESCE(@exp, MBMPayorExpDate), MBMBordxDate = @bordx,
        MBMDataStatus = @dataStatus, MBMStatus = @status, PLNCode = @plan
    WHERE MBMNumber = @number;
END
GO

CREATE OR ALTER PROCEDURE genisis.Adjustment_SaveMbmTwo
    @number nvarchar(4000), @salutation nvarchar(4000), @email nvarchar(4000), @lgn nvarchar(4000), @type nvarchar(4000),
    @nat nvarchar(4000) = NULL, @telHome nvarchar(4000), @telMobile nvarchar(4000), @telOffice nvarchar(4000), @now datetime,
    @user nvarchar(4000), @mgmtNotes nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.MBMTwo WHERE MBMNumber = @number) INSERT INTO dbo.MBMTwo (MBMNumber) VALUES (@number);
    UPDATE dbo.MBMTwo SET MBMSalutation = @salutation, MBMEmail = @email, LGNCode = @lgn, MBMLastEndorseStatus = @type,
        NATCode = COALESCE(@nat, NATCode), MBMTelnoH = @telHome, MBMTelNoM = @telMobile, MBMTelNoO = @telOffice,
        MBMLastAdjustmentDate = @now, MBMLastUpdateUser = @user, MBMMmgtNotes = @mgmtNotes
    WHERE MBMNumber = @number;
END
GO

-- MBMOthers and MBMOthersTwo (desktop SavePrincipalAdjustment, including the frmNewMBMOthers fields). Rows are created when missing.
CREATE OR ALTER PROCEDURE genisis.Adjustment_SaveOthers
    @number nvarchar(4000), @department nvarchar(4000), @groupCompany nvarchar(4000), @polSubNo nvarchar(4000), @labelStatus nvarchar(4000) = NULL,
    @employeeNo nvarchar(4000), @agentCode nvarchar(4000), @installment nvarchar(4000), @mco decimal(19, 4) = NULL, @branch nvarchar(4000),
    @basicPremium decimal(19, 4) = NULL, @premium decimal(19, 4) = NULL, @joined datetime = NULL, @clmNonPayFrom datetime = NULL,
    @clmNonPayTo datetime = NULL, @newPlan nvarchar(4000), @newPolicyEff datetime = NULL, @policyDisc decimal(19, 4), @renewalDisc decimal(19, 4),
    @polCondition nvarchar(4000), @undExcess decimal(19, 4), @endtRefNo nvarchar(4000), @costCenter nvarchar(4000), @position nvarchar(4000),
    @groupCategory nvarchar(4000), @division nvarchar(4000), @subDivision nvarchar(4000), @subsidiary nvarchar(4000), @nextDue datetime = NULL,
    @allergic nvarchar(4000), @remarks nvarchar(4000), @weight nvarchar(4000), @height nvarchar(4000), @workNature nvarchar(4000),
    @blood nvarchar(4000), @smoking nvarchar(4000), @alcohol nvarchar(4000), @marital nvarchar(4000) = NULL, @prevPlan nvarchar(4000),
    @rbAmount decimal(19, 4), @prevInsurer nvarchar(4000), @occupation nvarchar(4000), @coPayRb nvarchar(4000),
    @genWp decimal(19, 4) = NULL, @genWpEnd datetime = NULL, @preExWp decimal(19, 4) = NULL, @preExWpEnd datetime = NULL,
    @speIllWp decimal(19, 4) = NULL, @speIllWpEnd datetime = NULL, @staffDisc nvarchar(4000), @btbg nvarchar(4000) = NULL,
    @payorPremGross decimal(19, 4) = NULL, @payorPremNet decimal(19, 4), @payorMcoGross decimal(19, 4), @payorMcoNet decimal(19, 4),
    @familyDiscAmount decimal(19, 4), @renewalDiscAmount decimal(19, 4), @loadingPremium decimal(19, 4), @exclusion nvarchar(4000),
    @bankAccountNo nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.MBMOthers WHERE MBMNumber = @number) INSERT INTO dbo.MBMOthers (MBMNumber) VALUES (@number);
    UPDATE dbo.MBMOthers SET MBMDepartment = @department, MBMGroupCompany = @groupCompany, MBMPOLSubNo1 = @polSubNo, LabelStatus = @labelStatus,
        MBMEmployeeNo = @employeeNo, MBMAgentCode = @agentCode, RELCode = 'P', MBMInstallment = @installment,
        MBMMCOFee = COALESCE(@mco, MBMMCOFee), MBMBranch = @branch, MBMBasicPrem = COALESCE(@basicPremium, MBMBasicPrem),
        MBMPremium = COALESCE(@premium, MBMPremium), MBMFirstJoinedDate = COALESCE(@joined, MBMFirstJoinedDate),
        MBMClmNonPayFr = @clmNonPayFrom, MBMClmNonPayTo = @clmNonPayTo, MBMNewPlanCode = @newPlan, MBMNewPolEffDate = @newPolicyEff,
        MBMPolicyDisc = @policyDisc, MBMRenewalDisc = @renewalDisc, MBMPolCondition = @polCondition, MBMUndExcess = @undExcess,
        MBMENDtRefNo = @endtRefNo, MBMCostCenter = @costCenter, MBMPosition = @position, MBMGroupCategory = @groupCategory,
        MBMDivision = @division, MBMSubDivision = @subDivision, Subsidiary = @subsidiary, MBMNextDueDate = COALESCE(@nextDue, MBMNextDueDate)
    WHERE MBMNumber = @number;

    IF NOT EXISTS (SELECT 1 FROM dbo.MBMOthersTwo WHERE MBMNumber = @number) INSERT INTO dbo.MBMOthersTwo (MBMNumber) VALUES (@number);
    UPDATE dbo.MBMOthersTwo SET MBMAllergic = @allergic, MBMRemarks = @remarks, MBMWeight = @weight, MBMHeight = @height,
        MBMWorkNature = @workNature, MBMBlood = @blood, MBMSmoking = @smoking, MBMAlcohol = @alcohol,
        MBMMaritalStatus = COALESCE(@marital, MBMMaritalStatus), MBMPrevPLNCode = @prevPlan, MBMRBAmt = @rbAmount, MBMPrevInsCom = @prevInsurer,
        MBMOccupation = @occupation, MBMCoPayRB = @coPayRb, MBMGenWP = @genWp, MBMGenEndWP = @genWpEnd, MBMPreExWP = @preExWp,
        MBMPreExEndWP = @preExWpEnd, MBMSpeIllWP = @speIllWp, MBMSpeIllEndWP = @speIllWpEnd, MBMStaffDec = @staffDisc, MBMBTBG = @btbg,
        MBMPayorPremGross = COALESCE(@payorPremGross, MBMPayorPremGross), MBMPayorPremNet = @payorPremNet, MBMPayorMCOGross = @payorMcoGross,
        MBMPayorMCONet = @payorMcoNet, MBMFamDiscAmt = @familyDiscAmount, MBMRenewDiscAmt = @renewalDiscAmount, MBMLoadingPrem = @loadingPremium,
        MBMExclusion = @exclusion, MBMBankACNo = @bankAccountNo
    WHERE MBMNumber = @number;
END
GO

-- One existing covered person (desktop SaveSupplementaryAdjustment). @status NULL keeps the status; null dates keep the stored ones.
CREATE OR ALTER PROCEDURE genisis.Adjustment_UpdateCovered
    @number nvarchar(4000), @coverId nvarchar(4000), @name nvarchar(4000), @ic nvarchar(4000), @dob datetime, @sex nvarchar(4000),
    @adultChild nvarchar(4000), @rel nvarchar(4000), @eff datetime = NULL, @exp datetime = NULL, @premium decimal(19, 4) = NULL,
    @plan nvarchar(4000) = NULL, @status nvarchar(4000) = NULL, @salutation nvarchar(4000), @allergic nvarchar(4000), @exclusion nvarchar(4000),
    @clmNonPayFrom datetime = NULL, @clmNonPayTo datetime = NULL, @policyDisc decimal(19, 4), @renewalDisc decimal(19, 4),
    @prevPlan nvarchar(4000), @rbAmount decimal(19, 4), @payRemarks nvarchar(4000), @prevInsurer nvarchar(4000),
    @genWp decimal(19, 4) = NULL, @genWpEnd datetime = NULL, @preExWp decimal(19, 4) = NULL, @preExWpEnd datetime = NULL,
    @speIllWp decimal(19, 4) = NULL, @speIllWpEnd datetime = NULL, @staffDisc nvarchar(4000) = NULL, @payorPremGross decimal(19, 4) = NULL,
    @payorPremNet decimal(19, 4), @payorMcoGross decimal(19, 4), @payorMcoNet decimal(19, 4), @familyDiscAmount decimal(19, 4),
    @renewalDiscAmount decimal(19, 4), @loadingPremium decimal(19, 4), @undExcess decimal(19, 4), @payNo nvarchar(4000),
    @paySeqNo nvarchar(4000), @clientNo nvarchar(4000), @clientId nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.MBMCoveredPersons SET MBMCName = @name, MBMCICBCPPNo = @ic, MBMCDOB = @dob, MBMCSex = @sex, MBMCAdultChild = @adultChild,
        MBMCRELCode = @rel, MBMCEffDate = COALESCE(@eff, MBMCEffDate), MBMCExpDate = COALESCE(@exp, MBMCExpDate),
        MBMCPremium = COALESCE(@premium, MBMCPremium), MBMCPLNCode = COALESCE(@plan, MBMCPLNCode), MBMCStatus = COALESCE(@status, MBMCStatus)
    WHERE MBMCNumber = @number AND MBMCCoverID = @coverId;
    SELECT @@ROWCOUNT AS Updated;

    IF NOT EXISTS (SELECT 1 FROM dbo.MBMCoveredPersonsTwo WHERE MBMCNumber = @number AND MBMCCoverID = @coverId)
        INSERT INTO dbo.MBMCoveredPersonsTwo (MBMCNumber, MBMCCoverID) VALUES (@number, @coverId);
    UPDATE dbo.MBMCoveredPersonsTwo SET MBMCSalutation = @salutation, MBMCAllergic = @allergic, MBMCExclusion = @exclusion,
        MBMCClmNonPayFr = @clmNonPayFrom, MBMCClmNonPayTo = @clmNonPayTo, MBMCPolicyDisc = @policyDisc, MBMCRenewalDisc = @renewalDisc,
        MBMCPrevPLNCode = @prevPlan, MBMCRBAmt = @rbAmount, MBMCPAYRemarks = @payRemarks, MBMCPrevInsCom = @prevInsurer,
        MBMCGenWP = @genWp, MBMCGenEndWP = @genWpEnd, MBMCPreExWP = @preExWp, MBMCPreExEndWP = @preExWpEnd, MBMCSpeIllWP = @speIllWp,
        MBMSpeIllEndWP = @speIllWpEnd, MBMCStaffDec = COALESCE(@staffDisc, MBMCStaffDec), MBMCPayorPremGross = @payorPremGross,
        MBMCPayorPremNet = @payorPremNet, MBMCPayorMCOGross = @payorMcoGross, MBMCPayorMCONet = @payorMcoNet,
        MBMCFamDiscAmt = @familyDiscAmount, MBMCRenewDiscAmt = @renewalDiscAmount, MBMCLoadingPrem = @loadingPremium,
        MBMCUndExcess = @undExcess, MBMCPAYNo = @payNo, MBMCPAYSeqNo = @paySeqNo, MBMCClientNO = @clientNo, MBMCClientID = @clientId
    WHERE MBMCNumber = @number AND MBMCCoverID = @coverId;
END
GO

-- New covered person (desktop AddSupplementaryAdjustment). Clinical-only members (no HIS plan) get no effective / expiry date.
CREATE OR ALTER PROCEDURE genisis.Adjustment_InsertCovered
    @number nvarchar(4000), @coverId nvarchar(4000), @ageCode nvarchar(4000), @status nvarchar(4000), @bordx datetime, @batch int = NULL,
    @name nvarchar(4000), @ic nvarchar(4000), @dob datetime, @sex nvarchar(4000), @adultChild nvarchar(4000), @rel nvarchar(4000),
    @today datetime, @plan nvarchar(4000), @eff datetime = NULL, @exp datetime = NULL, @annual decimal(19, 4), @basicPremium decimal(19, 4),
    @premium decimal(19, 4), @basicMco decimal(19, 4), @mco decimal(19, 4), @salutation nvarchar(4000), @allergic nvarchar(4000),
    @exclusion nvarchar(4000), @payorPremNet decimal(19, 4), @payorMcoGross decimal(19, 4), @payorMcoNet decimal(19, 4),
    @loadingPremium decimal(19, 4), @familyDiscAmount decimal(19, 4), @renewalDiscAmount decimal(19, 4), @undExcess decimal(19, 4),
    @renewalDisc decimal(19, 4), @policyDisc decimal(19, 4), @clmNonPayFrom datetime = NULL, @clmNonPayTo datetime = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.MBMCoveredPersons (MBMCNumber, MBMCCoverID, MBMCAgeband, MBMCStatus, MBMCBordxDate, MBMCBatchNo, MBMCName, MBMCICBCPPNo,
        MBMCDOB, MBMCSex, MBMCAdultChild, MBMCRELCode, MBMCMemberType, MBMCRegDate, MBMCPLNCode, MBMCEffDate, MBMCExpDate,
        MBMCAnnualLimit, MBMCAvailableLimit, MBMCBasicPremium, MBMCPremium, MBMCBasicMCO, MBMCMCO)
    VALUES (@number, @coverId, @ageCode, @status, @bordx, @batch, @name, @ic, @dob, @sex, @adultChild, @rel, 'C', @today, @plan, @eff, @exp,
        @annual, @annual, @basicPremium, @premium, @basicMco, @mco);
    INSERT INTO dbo.MBMCoveredPersonsTwo (MBMCNumber, MBMCCoverID, MBMCSalutation, MBMCAllergic, MBMCExclusion, MBMCPayorPremNet,
        MBMCPayorMCOGross, MBMCPayorMCONet, MBMCLoadingPrem, MBMCFamDiscAmt, MBMCRenewDiscAmt, MBMCUndExcess, MBMCRenewalDisc,
        MBMCPolicyDisc, MBMCClmNonPayFr, MBMCClmNonPayTo)
    VALUES (@number, @coverId, @salutation, @allergic, @exclusion, @payorPremNet, @payorMcoGross, @payorMcoNet, @loadingPremium,
        @familyDiscAmount, @renewalDiscAmount, @undExcess, @renewalDisc, @policyDisc,
        CASE WHEN @clmNonPayFrom IS NOT NULL AND @clmNonPayTo IS NOT NULL THEN @clmNonPayFrom END,
        CASE WHEN @clmNonPayFrom IS NOT NULL AND @clmNonPayTo IS NOT NULL THEN @clmNonPayTo END);
END
GO

-- Clinical covered person row (the desktop's own Upload_MBMCovPersonsCis, called unchanged).
CREATE OR ALTER PROCEDURE genisis.Adjustment_InsertCoveredClinical
    @number nvarchar(4000), @coverId nvarchar(4000), @mco decimal(19, 4), @basicMco decimal(19, 4), @premium decimal(19, 4),
    @basicPremium decimal(19, 4), @annual decimal(19, 4), @visit decimal(19, 4), @dental decimal(19, 4), @specialist decimal(19, 4),
    @mep decimal(19, 4), @today datetime, @user nvarchar(4000), @expiry datetime = NULL, @plan nvarchar(4000),
    @invoiceNo nvarchar(4000), @invoiceDate datetime = NULL, @invoiceBy nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.Upload_MBMCovPersonsCis @number, @coverId, @mco, @basicMco, @premium, @basicPremium, @annual, @visit, @dental, @specialist, @mep,
        @today, @user, @today, @user, @expiry, '', '', 'A', @plan, @invoiceNo, @invoiceDate, @invoiceBy;
END
GO

-- Removes a covered person and its limits (desktop DeleteSupplementary). @topupNo is set for guarantee renewal 3 (lifetime rows).
CREATE OR ALTER PROCEDURE genisis.Adjustment_DeleteCovered
    @number nvarchar(4000), @coverId nvarchar(4000), @topupNo nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.MBMCoveredPersons WHERE MBMCNumber = @number AND MBMCCoverID = @coverId;
    DELETE FROM dbo.MBMCoveredPersonsTwo WHERE MBMCNumber = @number AND MBMCCoverID = @coverId;
    DELETE FROM dbo.MBMCoveredPersonsCLN WHERE MBMCNumber = @number AND MBMCCoverID = @coverId;
    IF NULLIF(@topupNo, N'') IS NOT NULL DELETE FROM dbo.MBMLifeTime WHERE MBMNumber = @topupNo AND MBMCovID = @coverId;
    DELETE FROM dbo.MBMBnfLmt WHERE MBMNumber = @number AND MBMCoveredID = @coverId;
END
GO

CREATE OR ALTER PROCEDURE genisis.Adjustment_UpdateMcoPending
    @number nvarchar(4000), @bordx datetime
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.Maint_MCOPending SET MBMNewBordxDate = @bordx WHERE MBMNumber = @number;
END
GO
