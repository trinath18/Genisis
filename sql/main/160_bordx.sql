-- Membership > Upload Bordx (legacy FrmNewMBMEndt10), standard format. The desktop stages new members in UploadDb through its
-- TmpUpload_* procedures and copies them into HISDB with SaveUploadDate; the API does the same, inside one transaction, keyed by a
-- per-run @runKey so concurrent uploads never see each other's staged rows.

-- ===== Lookups =====

CREATE OR ALTER PROCEDURE genisis.Bordx_Payor
    @pay nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(BordxFormatType) AS BordxFormatType, RTRIM(CLNStatus) AS ClinicalStatus, RTRIM(MBMCancelStatus) AS CancelStatus,
        RTRIM(PAYMBMRegStatus) AS RegStatus
    FROM dbo.PAY WHERE PAYCode = @pay;
END
GO

-- Codes the verification checks against: insured types, relationships (data types), renewal types.
CREATE OR ALTER PROCEDURE genisis.Bordx_Codes
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(INSCode) AS Code FROM dbo.INS WHERE INSCode IS NOT NULL;
    SELECT RTRIM(RELCode) AS Code FROM dbo.REL WHERE RELCode IS NOT NULL;
    SELECT RTRIM(RenewalType) AS Code FROM genisis.Maint_Renewal WHERE RenewalType IS NOT NULL;
END
GO

-- Plan by HIS plan code (@plan) or by the payor's own plan code (@payorPlan, ZURICH / TAK format).
CREATE OR ALTER PROCEDURE genisis.Bordx_Plan
    @plan nvarchar(4000) = NULL, @payorPlan nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(PLNCode) AS PlanCode, RTRIM(PLNLifeTimeStatus) AS LifetimeStatus, RTRIM(PLNPayorCode) AS PayorCode, PlnAgeLimit AS AgeLimit
    FROM dbo.PLN
    WHERE (@plan IS NOT NULL AND PLNCode = @plan) OR (@payorPlan IS NOT NULL AND PLNPayorPlan = @payorPlan);
END
GO

-- Active cross-reference rows for an IC (desktop "Same IC ... Existed" check).
CREATE OR ALTER PROCEDURE genisis.Bordx_ActiveByIc
    @ic nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(MBMNumber) AS MembershipNo, RTRIM(MBMIcBcPp) AS Ic, MBMPayorExpDate AS ExpiryDate
    FROM genisis.Maint_MBMCrossReference WHERE MBMIcBcPp = @ic AND MBMStatus = 'A';
END
GO

-- Latest active membership for the IC / payor / insured type (desktop renewal-type top-up check).
CREATE OR ALTER PROCEDURE genisis.Bordx_TopupCheck
    @ic nvarchar(4000), @pay nvarchar(4000), @ins nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(MBMNumber) AS MembershipNo, RTRIM(INSCode) AS InsuredType, RTRIM(MBMTopupNumber) AS TopupNumber
    FROM genisis.Maint_MBMCrossReference
    WHERE MBMIcBcPp = @ic AND MBMStatus = 'A' AND PAYCode = @pay AND INSCode = @ins
    ORDER BY MBMPayorEffDate DESC, MBMNumber;
END
GO

-- Endorsement file checks: open case / already cancelled.
CREATE OR ALTER PROCEDURE genisis.Bordx_EndorsementState
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM genisis.Maint_CASCrossReference WHERE CASStatus = 'O' AND MBMNumber = @number) THEN 1 ELSE 0 END AS bit) AS OpenCase,
           CAST(CASE WHEN EXISTS (SELECT 1 FROM genisis.Maint_MBMCrossReference WHERE MBMStatus = 'C' AND MBMNumber = @number) THEN 1 ELSE 0 END AS bit) AS Cancelled;
END
GO

-- ===== New member calculation reads (desktop SPHISDBTableRecAssign / SPCisDBTableRecAssign and the Get* helpers) =====

CREATE OR ALTER PROCEDURE genisis.Bordx_HisPlan
    @plan nvarchar(4000), @hlt nvarchar(4000), @pay nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(PLNTopUPStatus) AS TopupStatus, RTRIM(PLNProRate) AS ProRate, RTRIM(PRMInd) AS PremiumInd, RTRIM(MCOInd) AS McoInd,
        RTRIM(AnnLmtIND) AS AnnualLimitInd, RTRIM(PLNLifeTimeStatus) AS LifetimeStatus, PlnAgeLimit AS AgeLimit,
        RTRIM(PLNRoundUpStatus) AS RoundUpStatus, RTRIM(PLNPremGenderStatus) AS PremGenderStatus, RTRIM(PLNGuaranteeStatus) AS GuaranteeStatus
    FROM dbo.PLN WHERE PLNCode = @plan AND HLTCode = @hlt AND (@hlt <> 'N' OR PAYCode = @pay)
    ORDER BY PLNEffDate DESC;
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_CisPlan
    @plan nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(DCStatus) AS DcStatus, RTRIM(PRMInd) AS PremiumInd, RTRIM(MCOInd) AS McoInd, RTRIM(ANNLmtInd) AS AnnualLimitInd,
        RTRIM(VISLmtInd) AS VisitLimitInd, RTRIM(PAYCode) AS PayorCode, RTRIM(PDentalStatus) AS DentalStatus, RTRIM(PSpecStatus) AS SpecialistStatus,
        RTRIM(PMEPStatus) AS MepStatus, PLNAgeLimit AS AgeLimit, RTRIM(PLNRoundUpStatus) AS RoundUpStatus, RTRIM(MCOProcfeeStatus) AS McoProcFeeStatus,
        RTRIM(PLNProRate) AS ProRate
    FROM genisis.Cis_PLN WHERE PLNCode = @plan ORDER BY PLNEffDate DESC;
END
GO

-- Clinical rates for one person. Result sets: annual limit row, visit limit, premium row (payor filter except health type S),
-- MCO row (plan-specific, else default).
CREATE OR ALTER PROCEDURE genisis.Bordx_CisRates
    @ins nvarchar(4000), @plan nvarchar(4000), @hlt nvarchar(4000), @eff datetime, @premiumDate datetime, @pay nvarchar(4000), @ageCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(SuppLimitStatus) AS SuppLimitStatus, AnnLimit, SuppLimit, PDentalAnnLimit AS DentalLimit, PSpecAnnLimit AS SpecialistLimit,
        PMEPAnnLimit AS MepLimit
    FROM genisis.Cis_AnnualLimit WHERE INSCode = @ins AND PLNCode = @plan AND HLTCode = @hlt AND AnnEffDate <= @eff ORDER BY AnnEffDate DESC;
    SELECT TOP (1) VisitLimit FROM genisis.Cis_VisitLimit
    WHERE INSCode = @ins AND PLNCode = @plan AND HLTCode = @hlt AND VisitEffDate <= @eff ORDER BY VisitEffDate DESC;
    SELECT TOP (1) RTRIM(SuppPRMStatus) AS SuppPrmStatus, PRMAmount, SuppPRMAmt AS SuppPrmAmt FROM genisis.Cis_PRM
    WHERE INSCode = @ins AND HLTCode = @hlt AND PLNCode = @plan AND AGECode = @ageCode AND PRMEffDate <= @premiumDate AND (@hlt = 'S' OR PAYCode = @pay)
    ORDER BY PRMEffDate DESC;
    SELECT TOP (1) RTRIM(SuppMCOStatus) AS SuppMcoStatus, MCOAmtAdult AS Adult, MCOAmtChild AS Child, MCOSuppAdultAmt AS SuppAdult, MCOSuppChildAmt AS SuppChild
    FROM genisis.Cis_MCO WHERE INSCode = @ins AND HLTCode = @hlt AND (PLNCode = @plan OR PLNCode IS NULL) AND MCOEffDate <= @eff
    ORDER BY CASE WHEN PLNCode IS NULL THEN 1 ELSE 0 END, MCOEffDate DESC;
END
GO

-- Top-up: latest cross-reference row for the IC under the payor and insured type (desktop PLNTopUpStatus = Y).
CREATE OR ALTER PROCEDURE genisis.Bordx_TopupByIc
    @ic nvarchar(4000), @pay nvarchar(4000), @ins nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(MBMNumber) AS MembershipNo FROM genisis.Maint_MBMCrossReference
    WHERE MBMIcBcPp = @ic AND LEFT(MBMNumber, 2) = @pay AND INSCode = @ins ORDER BY MBMPayorEffDate DESC;
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_PreviousCoveredLimit
    @previousNo nvarchar(4000), @name nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) MBMCAvailableLimit AS Value FROM dbo.MBMCoveredPersons WHERE MBMCNumber = @previousNo AND MBMCName = @name;
END
GO

-- Clinical-only membership sequence (desktop GenerateMembershipNo, MBMSEQ by payor + insured type): A00001 .. Z99999.
CREATE OR ALTER PROCEDURE genisis.Bordx_NextClinicalSeq
    @pay nvarchar(4000), @ins nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @code nvarchar(20) = LTRIM(RTRIM(@pay + @ins)), @current nvarchar(10), @next nvarchar(10);
    SELECT @current = RTRIM(SEQNumber) FROM genisis.Maint_MBMSEQ WITH (UPDLOCK, HOLDLOCK) WHERE SEQCode = @code;
    IF @current IS NULL
    BEGIN
        SET @next = N'A00001';
        INSERT INTO genisis.Maint_MBMSEQ (SEQCode, PAYCode, INSCode, SEQNumber) VALUES (@code, @pay, @ins, @next);
    END
    ELSE
    BEGIN
        DECLARE @n int = TRY_CAST(SUBSTRING(@current, 2, 5) AS int), @c nchar(1) = LEFT(@current, 1);
        SET @next = CASE WHEN @n = 99999 THEN NCHAR(UNICODE(@c) + 1) + N'00001' ELSE @c + RIGHT(N'00000' + CAST(@n + 1 AS nvarchar(5)), 5) END;
        UPDATE genisis.Maint_MBMSEQ SET SEQNumber = @next WHERE SEQCode = @code;
    END
    SELECT @next AS Sequence;
END
GO

-- Desktop CheckBatch: next batch number for the payor / bordereaux date / file type.
CREATE OR ALTER PROCEDURE genisis.Bordx_NextBatch
    @pay nvarchar(4000), @bordx datetime, @type nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @batch int;
    SELECT @batch = BATSequence + 1 FROM dbo.BAT WITH (UPDLOCK, HOLDLOCK) WHERE PAYCode = @pay AND BATBordxDate = @bordx AND BordxType = @type;
    IF @batch IS NULL
    BEGIN
        SET @batch = 1;
        INSERT INTO dbo.BAT (PAYCode, BATBordxDate, BATSequence, BordxType) VALUES (@pay, @bordx, 1, @type);
    END
    ELSE UPDATE dbo.BAT SET BATSequence = @batch WHERE PAYCode = @pay AND BATBordxDate = @bordx AND BordxType = @type;
    SELECT @batch AS Batch;
END
GO

-- ===== Staging (the desktop's own procedures, called with named parameters) =====
-- The current TmpUpload_MBM_HIS / _MBMCovPersonsNew / _MBMOthers_2015 have required parameters the desktop never passes
-- (top-up IC, deductible, CMC / agent name); they are sent blank here.

CREATE OR ALTER PROCEDURE genisis.Bordx_ClearStaging
    @runKey nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.DeleteTmpUploadDate @UpdatedBy = @runKey;
END
GO

-- Copies the staged rows into HISDB / HISMaintenance. SaveUploadDate swallows its own errors with a bare ROLLBACK, which inside the
-- API's transaction surfaces as a transaction-count error, so a failed copy can never look like a success.
CREATE OR ALTER PROCEDURE genisis.Bordx_CommitStaging
    @runKey nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.SaveUploadDate @UpdatedBy = @runKey;
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_StagePrincipal
    @runKey nvarchar(4000), @pay nvarchar(4000), @hlt nvarchar(4000), @number nvarchar(4000), @policy nvarchar(4000), @coverId nvarchar(4000),
    @ic nvarchar(4000), @name nvarchar(4000), @dob datetime = NULL, @sex nvarchar(4000), @race nvarchar(4000), @status nvarchar(4000),
    @dataStatus nvarchar(4000), @memberType nvarchar(4000), @add1 nvarchar(4000), @add2 nvarchar(4000), @add3 nvarchar(4000), @city nvarchar(4000),
    @postCode nvarchar(4000), @state nvarchar(4000), @expiredStatus nvarchar(4000), @valueDate datetime, @eff datetime = NULL, @exp datetime = NULL,
    @takeOver nvarchar(4000), @renewal nvarchar(4000), @bordx datetime, @batch int, @plan nvarchar(4000), @ins nvarchar(4000), @ageCode nvarchar(4000),
    @adultChild nvarchar(4000), @available decimal(19, 4), @hsClnInd nvarchar(4000), @topupNo nvarchar(4000), @topupInd nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.TmpUpload_MBM_HIS @PAYCode = @pay, @HLTCode = @hlt, @MBMNumber = @number, @MBMPolicyNo = @policy, @MBMCOVID = @coverId,
        @MBMIcBcPp = @ic, @MBMName = @name, @MBMDOB = @dob, @MBMSex = @sex, @RACCode = @race, @MBMStatus = @status, @MBMDataStatus = @dataStatus,
        @MBMMemberType = @memberType, @MBMAddress1 = @add1, @MBMAddress2 = @add2, @MBMAddress3 = @add3, @CTYCode = @city, @MBMPostCode = @postCode,
        @STACode = @state, @MBMExpiredStatus = @expiredStatus, @MBMValueDate = @valueDate, @MBMPayorEffDate = @eff, @MBMPayorEXPDate = @exp,
        @MBMTakeOver = @takeOver, @MBMRenewFlag = @renewal, @MBMBordxDate = @bordx, @MBMBatchNo = @batch, @PLNCode = @plan, @INSCode = @ins,
        @AgeCode = @ageCode, @MBMAdultChild = @adultChild, @MBMAvailableLimit = @available, @HSClnInd = @hsClnInd, @MBMTopupNumber = @topupNo,
        @MBMTopupInd = @topupInd, @UpdatedBy = @runKey, @MBMTINumber = '', @MBMTIcoverID = '', @DEDUCT_LIMIT = NULL;
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_StageMbmTwo
    @runKey nvarchar(4000), @number nvarchar(4000), @salutation nvarchar(4000), @telHome nvarchar(4000), @telMobile nvarchar(4000),
    @telOffice nvarchar(4000), @nat nvarchar(4000), @language nvarchar(4000), @user nvarchar(4000), @clinical nvarchar(4000), @appName nvarchar(4000),
    @otherIc nvarchar(4000), @fileName nvarchar(4000), @today datetime, @agentName nvarchar(4000), @agentEmail nvarchar(4000), @agentPhone nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.TmpUpload_MBMTwo_Agent @MBMNumber = @number, @MBMSalutation = @salutation, @MBMTelnoH = @telHome, @MBMTelNoM = @telMobile,
        @MBMTelNoO = @telOffice, @NATCode = @nat, @LGNCode = @language, @SRVCodeList = 'ME', @MBMRegUser = @user, @MBMLastUpdateUser = @user,
        @MBMClinical = @clinical, @MBMAppName = @appName, @MBMIcBcPp2nd = @otherIc, @MBMUploadStatus = 'A', @MBMFilename = @fileName,
        @MBMENDtEffDate = @today, @UpdatedBy = @runKey, @MBMAgentName = @agentName, @MBMAgentEmail = @agentEmail, @MBMAgentPhoneNo = @agentPhone;
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_StageXref
    @runKey nvarchar(4000), @number nvarchar(4000), @policy nvarchar(4000), @ic nvarchar(4000), @name nvarchar(4000), @eff datetime = NULL,
    @exp datetime = NULL, @ageCode nvarchar(4000), @ins nvarchar(4000), @plan nvarchar(4000), @bordx datetime, @topupNo nvarchar(4000),
    @topupInd nvarchar(4000), @dataStatus nvarchar(4000), @status nvarchar(4000), @dob datetime = NULL, @hlt nvarchar(4000), @pay nvarchar(4000), @batch int
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.TmpUpload_MBMXref_HIS @MBMNumber = @number, @MBMCOVID = '00', @MBMPolicyNo = @policy, @MBMIcBcPp = @ic, @MBMName = @name,
        @MBMPayorEffDate = @eff, @MBMPayorEXPDate = @exp, @AgeCode = @ageCode, @INSCode = @ins, @PLNCode = @plan, @MBMBordxDate = @bordx,
        @MBMTopupNumber = @topupNo, @MBMTopupInd = @topupInd, @MBMDataStatus = @dataStatus, @MBMStatus = @status, @MBMDOB = @dob, @HLTCode = @hlt,
        @PAYCode = @pay, @MBMBatchNo = @batch, @UpdatedBy = @runKey;
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_StageOthers
    @runKey nvarchar(4000), @number nvarchar(4000), @agentCode nvarchar(4000), @labelStatus nvarchar(4000), @premium decimal(19, 4),
    @basicPremium decimal(19, 4), @basicMco decimal(19, 4), @mco decimal(19, 4), @rel nvarchar(4000), @policyDisc decimal(19, 4),
    @renewalDisc decimal(19, 4), @groupCompany nvarchar(4000), @prevTakeOverPlan nvarchar(4000), @joined datetime = NULL, @prevPolicy nvarchar(4000),
    @prevMember nvarchar(4000), @employeeNo nvarchar(4000), @department nvarchar(4000), @installment nvarchar(4000), @polSubNo1 nvarchar(4000),
    @polSubNo2 nvarchar(4000), @branch nvarchar(4000), @newPolicyEff datetime = NULL, @endtRefNo nvarchar(4000), @polCondition nvarchar(4000),
    @undExcess decimal(19, 4), @guarantee nvarchar(4000), @clmNonPayFrom datetime = NULL, @clmNonPayTo datetime = NULL, @invoiceNo nvarchar(4000),
    @gstAmount decimal(19, 4) = NULL, @gstPercent decimal(19, 4) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.TmpUpload_MBMOthers_2015 @MBMNumber = @number, @MBMAgentCode = @agentCode, @LabelStatus = @labelStatus, @MBMFirstMEMNo = @number,
        @MBMPremium = @premium, @MBMBasicPrem = @basicPremium, @MBMBasicMCO = @basicMco, @MBMMCOFee = @mco, @RELCODE = @rel,
        @MBMPolicyDisc = @policyDisc, @MBMRenewalDisc = @renewalDisc, @MBMGroupCompany = @groupCompany, @MBMPrevTakeOverPlnCode = @prevTakeOverPlan,
        @MBMFirstJoinedDate = @joined, @MBMPrevPolNo = @prevPolicy, @MBMPrevMemNo = @prevMember, @MBMEmployeeNo = @employeeNo,
        @MBMDepartment = @department, @MBMInstallment = @installment, @MBMPolSubNo1 = @polSubNo1, @MBMPOLSubNo2 = @polSubNo2, @MBMBranch = @branch,
        @MBMNewPlanCode = '', @MBMNewPolEffDate = @newPolicyEff, @MBMENDtRefNo = @endtRefNo, @MBMPolCondition = @polCondition,
        @MBMUndExcess = @undExcess, @MBMGuaRenewal = @guarantee, @MBMClmNonPayFr = @clmNonPayFrom, @MBMClmNonPayTo = @clmNonPayTo,
        @MBMInvoiceNo = @invoiceNo, @MBMPaymentDate = NULL, @UpdatedBy = @runKey, @MBMGSTAmt = @gstAmount, @MBMGSTPerc = @gstPercent,
        @CMCName = '', @AgentName = '';
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_StageOthersTwo
    @runKey nvarchar(4000), @number nvarchar(4000), @weight nvarchar(4000), @height nvarchar(4000), @workNature nvarchar(4000), @blood nvarchar(4000),
    @occupation nvarchar(4000), @smoking nvarchar(4000), @alcohol nvarchar(4000), @marital nvarchar(4000), @grpAdd1 nvarchar(4000),
    @grpAdd2 nvarchar(4000), @grpAdd3 nvarchar(4000), @grpAdd4 nvarchar(4000), @riskNo nvarchar(4000), @transNo nvarchar(4000), @payNo nvarchar(4000),
    @paySeqNo nvarchar(4000), @bankAccountNo nvarchar(4000), @clientNo nvarchar(4000), @clientId nvarchar(4000), @rbAmount decimal(19, 4),
    @prevInsurer nvarchar(4000), @prevPlan nvarchar(4000), @payRemarks nvarchar(4000), @coPayRb nvarchar(4000), @exclusion nvarchar(4000),
    @btbg nvarchar(4000), @payorPremNet decimal(19, 4), @payorMcoNet decimal(19, 4), @payorMcoGross decimal(19, 4), @loadingPremium decimal(19, 4),
    @familyDiscAmount decimal(19, 4), @renewalDiscAmount decimal(19, 4), @payorPremGross decimal(19, 4), @policyIssued datetime = NULL,
    @allergic nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.TmpUpload_MBMOthersTwoNew @MBMNumber = @number, @MBMWeight = @weight, @MBMHeight = @height, @MBMWorkNature = @workNature,
        @MBMBlood = @blood, @MBMOccupation = @occupation, @MBMSmoking = @smoking, @MBMAlcohol = @alcohol, @MBMMaritalStatus = @marital,
        @MBMGRPCompanyAdd1 = @grpAdd1, @MBMGRPCompanyAdd2 = @grpAdd2, @MBMGRPCompanyAdd3 = @grpAdd3, @MBMGRPCompanyAdd4 = @grpAdd4,
        @MBMRiskNo = @riskNo, @MBMTransNo = @transNo, @MBMPayNo = @payNo, @MBMPaySeqNo = @paySeqNo, @MBMBankACNo = @bankAccountNo,
        @MBMClientNo = @clientNo, @MBMClientID = @clientId, @MBMRBAmt = @rbAmount, @MBMPrevInsCom = @prevInsurer, @MBMPrevPlnCode = @prevPlan,
        @MBMPayRemarks = @payRemarks, @MBMCoPayRB = @coPayRb, @MBMExclusion = @exclusion, @MBMBTBG = @btbg, @MBMPayorPremNet = @payorPremNet,
        @MBMPayorMcoNet = @payorMcoNet, @MBMPayorMCoGross = @payorMcoGross, @MBMLoadingPrem = @loadingPremium, @MBMFamDiscAmt = @familyDiscAmount,
        @MBMRenewDiscAmt = @renewalDiscAmount, @MBMPayorPremGross = @payorPremGross, @MBMPOLIssuedDate = @policyIssued, @MBMAllergic = @allergic,
        @UpdatedBy = @runKey;
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_StageClinical
    @runKey nvarchar(4000), @memberType nvarchar(4000), @pay nvarchar(4000), @number nvarchar(4000), @policy nvarchar(4000), @plan nvarchar(4000),
    @ins nvarchar(4000), @warranty nvarchar(4000), @basicPremium decimal(19, 4), @premium decimal(19, 4), @basicMco decimal(19, 4), @mco decimal(19, 4),
    @balance decimal(19, 4), @visit decimal(19, 4), @dental decimal(19, 4), @specialist decimal(19, 4), @mep decimal(19, 4), @eff datetime = NULL,
    @exp datetime = NULL, @cardType nvarchar(4000), @invoiced nvarchar(4000), @invoiceNo nvarchar(4000), @invoiceDate datetime = NULL, @invoiceBy nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.TmpUpload_MBMClnOthers @MBMCLNMemberType = @memberType, @PAYCode = @pay, @MBMNumber = @number, @MBMPolNumber = @policy,
        @MBMCLNStatus = 'A', @MBMCLNDataStatus = 'A', @MBMCLNExpiryStatus = 'N', @PLNType = @plan, @InsType = @ins, @MBMCLNWarranty = @warranty,
        @MBMCLNBasicPrem = @basicPremium, @MBMCLNPremium = @premium, @MBMCLNBasicMCO = @basicMco, @MBMCLNMCOFee = @mco, @MBMCLNBalLmt = @balance,
        @MBMCLNVisitLmt = @visit, @MBMDentalLmt = @dental, @MBMSpecLmt = @specialist, @MBMMEPLmt = @mep, @MBMPolEffDate = @eff, @MBMPolExpDate = @exp,
        @MBMCardType = @cardType, @MBMCInvoiced = @invoiced, @MBMCInvoiceNo = @invoiceNo, @MBMCInvoiceDate = @invoiceDate, @MBMCInvoiceBy = @invoiceBy,
        @UpdatedBy = @runKey;
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_StageCovered
    @runKey nvarchar(4000), @rel nvarchar(4000), @number nvarchar(4000), @coverId nvarchar(4000), @dob datetime = NULL, @memberType nvarchar(4000),
    @name nvarchar(4000), @status nvarchar(4000), @bordx datetime, @batch int, @ic nvarchar(4000), @sex nvarchar(4000), @plan nvarchar(4000),
    @ageCode nvarchar(4000), @adultChild nvarchar(4000), @annual decimal(19, 4), @basicMco decimal(19, 4), @mco decimal(19, 4),
    @basicPremium decimal(19, 4), @premium decimal(19, 4), @today datetime, @eff datetime = NULL, @exp datetime = NULL
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.TmpUpload_MBMCovPersonsNew @MBMCRELCode = @rel, @MBMCNumber = @number, @MBMCCoverID = @coverId, @MBMCDOB = @dob,
        @MBMCMemberType = @memberType, @MBMCName = @name, @MBMCStatus = @status, @MBMCBordxDate = @bordx, @MBMCBatchNo = @batch,
        @MBMCICBCPPNo = @ic, @MBMCSex = @sex, @MBMCPLNCode = @plan, @MBMCAgeband = @ageCode, @MBMCAdultChild = @adultChild,
        @MBMCAnnualLimit = @annual, @MBMCAvailableLimit = @annual, @MBMCBasicMCO = @basicMco, @MBMCMCO = @mco, @MBMCBasicPremium = @basicPremium,
        @MBMCPremium = @premium, @MBMCRegDate = @today, @MBMCEffDate = @eff, @MBMCExpDate = @exp, @UpdatedBy = @runKey,
        @MBMTICNumber = '', @MBMTICCoverID = '';
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_StageCoveredTwo
    @runKey nvarchar(4000), @number nvarchar(4000), @coverId nvarchar(4000), @salutation nvarchar(4000), @occupation nvarchar(4000),
    @workNature nvarchar(4000), @blood nvarchar(4000), @allergic nvarchar(4000), @smoking nvarchar(4000), @alcohol nvarchar(4000),
    @prevPlan nvarchar(4000), @prevInsurer nvarchar(4000), @payRemarks nvarchar(4000), @exclusion nvarchar(4000), @payNo nvarchar(4000),
    @paySeqNo nvarchar(4000), @clientNo nvarchar(4000), @clientId nvarchar(4000), @rbAmount decimal(19, 4), @payorPremNet decimal(19, 4),
    @payorMcoNet decimal(19, 4), @payorMcoGross decimal(19, 4), @otherIc nvarchar(4000), @loadingPremium decimal(19, 4),
    @familyDiscAmount decimal(19, 4), @renewalDiscAmount decimal(19, 4), @payorPremGross decimal(19, 4), @undExcess decimal(19, 4),
    @clmNonPayFrom datetime = NULL, @clmNonPayTo datetime = NULL, @policyDisc decimal(19, 4), @renewalDisc decimal(19, 4)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.TmpUpload_MBMCovPersonsTwo @MBMCNumber = @number, @MBMCCoverID = @coverId, @MBMCSalutation = @salutation, @MBMCOccupation = @occupation,
        @MBMCWorkNature = @workNature, @MBMCBloodType = @blood, @MBMCAllergic = @allergic, @MBMCSmoking = @smoking, @MBMCAlcohol = @alcohol,
        @MBMCPrevPlnCode = @prevPlan, @MBMCPrevInsCom = @prevInsurer, @MBMCPayRemarks = @payRemarks, @MBMCExclusion = @exclusion,
        @MBMCPayNo = @payNo, @MBMCPaySeqNo = @paySeqNo, @MBMCClientNo = @clientNo, @MBMCClientID = @clientId, @MBMCRBAmt = @rbAmount,
        @MBMCPayorPremNet = @payorPremNet, @MBMCPayorMcoNet = @payorMcoNet, @MBMCPayorMCoGross = @payorMcoGross, @MBMCIcBcPp2nd = @otherIc,
        @MBMCLoadingPrem = @loadingPremium, @MBMCFamDiscAmt = @familyDiscAmount, @MBMCRenewDiscAmt = @renewalDiscAmount,
        @MBMCPayorPremGross = @payorPremGross, @MBMCUndExcess = @undExcess, @MBMCClmNonPayFr = @clmNonPayFrom, @MBMCClmNonPayTo = @clmNonPayTo,
        @MBMCPolicyDisc = @policyDisc, @MBMCRenewalDisc = @renewalDisc, @UpdatedBy = @runKey;
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_StageCoveredClinical
    @runKey nvarchar(4000), @number nvarchar(4000), @coverId nvarchar(4000), @mco decimal(19, 4), @basicMco decimal(19, 4), @premium decimal(19, 4),
    @basicPremium decimal(19, 4), @balance decimal(19, 4), @visit decimal(19, 4), @dental decimal(19, 4), @specialist decimal(19, 4),
    @mep decimal(19, 4), @today datetime, @user nvarchar(4000), @exp datetime = NULL, @cardType nvarchar(4000), @warranty nvarchar(4000),
    @plan nvarchar(4000), @invoiceNo nvarchar(4000), @invoiceDate datetime = NULL, @invoiceBy nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.TmpUpload_MBMCovPersonsCis @MBMCNumber = @number, @MBMCCoverID = @coverId, @MBMCCLNMCO = @mco, @MBMCCLNBasicMCO = @basicMco,
        @MBMCCLNPrem = @premium, @MBMCCLNBasicPrem = @basicPremium, @MBMCCLNBalLmt = @balance, @MBMCCLNVisitLmt = @visit,
        @MBMCCLNDentalLmt = @dental, @MBMCCLNSpecLmt = @specialist, @MBMCCLNMEPLmt = @mep, @MBMCCLNRegDate = @today, @MBMCCLNRegUser = @user,
        @MBMCCLNLastUpdateDate = @today, @MBMCCLNLastUpdateUser = @user, @MBMCCLNExpDate = @exp, @MBMCCardType = @cardType,
        @MBMCCLNWarranty = @warranty, @MBMCCLNStatus = 'A', @MBMCCLNPLNCode = @plan, @MBMCInvoiceNo = @invoiceNo, @MBMCInvoiceDate = @invoiceDate,
        @MBMCInvoiceBy = @invoiceBy, @UpdatedBy = @runKey;
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_StageRefund
    @runKey nvarchar(4000), @number nvarchar(4000), @coverId nvarchar(4000), @premiumEffective decimal(19, 4), @premiumReduction decimal(19, 4),
    @mcoEffective decimal(19, 4), @mcoRefund decimal(19, 4), @pay nvarchar(4000), @bordx datetime, @batch int, @hlt nvarchar(4000),
    @fileName nvarchar(4000), @endtRefNo nvarchar(4000), @cancelDate datetime, @gstAmount decimal(19, 4) = NULL, @gstPercent decimal(19, 4) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.TmpUpload_MBMMcoRefund_2015 @MBMNumber = @number, @MBMCovID = @coverId, @MBMPREMEffective = @premiumEffective,
        @MBMPremReduction = @premiumReduction, @MBMMCOEffective = @mcoEffective, @MBMMCORefund = @mcoRefund, @PAYCode = @pay,
        @MBMBordxDate = @bordx, @MBMBatchNO = @batch, @HLTCode = @hlt, @MBMFileName = @fileName, @MBMENDtRefNo = @endtRefNo,
        @MBMCancerdate = @cancelDate, @UpdatedBy = @runKey, @MBMGSTAmt = @gstAmount, @MBMGSTPerc = @gstPercent;
END
GO

-- ===== Cancellation and reinstatement (written straight to HISDB, as the desktop does) =====

-- Membership to cancel: policy number + payor expiry date, optionally limited to the payor plan (desktop EndtCancelPrincipalData).
CREATE OR ALTER PROCEDURE genisis.Bordx_FindForCancel
    @policy nvarchar(4000), @exp datetime, @payorPlan nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(m.MBMNumber) AS MembershipNo, RTRIM(m.MBMStatus) AS Status, RTRIM(m.MBMPolicyNo) AS PolicyNo, RTRIM(m.MBMIcBcPp) AS Ic,
        RTRIM(m.MBMName) AS Name, m.MBMPayorExpDate AS ExpiryDate, RTRIM(m.PLNCode) AS PlanCode, m.MBMPayorEffDate AS EffectiveDate,
        o.MBMPremium AS Premium, o.MBMMCOFee AS Mco
    FROM dbo.MBM m
    LEFT JOIN dbo.MBMOthers o ON o.MBMNumber = m.MBMNumber
    WHERE m.MBMPolicyNo = @policy AND m.MBMPayorExpDate = @exp AND m.MBMStatus <> 'D'
      AND (@payorPlan IS NULL OR EXISTS (SELECT 1 FROM dbo.PLN p WHERE p.PLNCode = m.PLNCode AND p.PLNPayorPlan = @payorPlan));
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_Cancel
    @number nvarchar(4000), @today datetime, @user nvarchar(4000), @bordx datetime, @cancelDate datetime, @fileName nvarchar(4000),
    @policy nvarchar(4000), @ic nvarchar(4000), @name nvarchar(4000), @exp datetime, @plan nvarchar(4000), @batch int
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.MBMTwo SET MBMLastAdjustmentDate = @today, MBMLastUpdateUser = @user, MBMLastEndorseStatus = 'C', MBMENDtBordxDate = @bordx,
        MBMCancelDate = @cancelDate, MBMENDtEffDate = @cancelDate, MBMFilename = @fileName
    WHERE MBMNumber = @number;
    UPDATE genisis.Maint_MBMCrossReference SET MBMStatus = 'C' WHERE MBMNumber = @number;
    INSERT INTO dbo.MBMAmendHistory (MBMNumber, MBMPolicyNo, MBMIcBcPp, MBMName, MBMExpiryDate, MBMPlanCode, MBMAmendEdtType, MBMAEndtBordxDate,
        MBMAEndtEffDate, MBMAmendUser, MBMAmendDate, MBMAEndtBatchNo)
    VALUES (@number, @policy, @ic, @name, @exp, @plan, 'C', @bordx, @cancelDate, @user, @today, @batch);
    UPDATE dbo.MBM SET MBMStatus = 'C' WHERE MBMNumber = @number;
END
GO

-- Lapsed membership being reinstated: same policy and payor expiry date (payor ZN excluded, as in the desktop).
CREATE OR ALTER PROCEDURE genisis.Bordx_FindForReinstate
    @policy nvarchar(4000), @exp datetime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(MBMNumber) AS MembershipNo FROM dbo.MBM WHERE PAYCode <> 'ZN' AND MBMPolicyNo = @policy AND MBMPayorExpDate = @exp;
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_Reinstate
    @number nvarchar(4000), @bordx datetime, @mco decimal(19, 4), @premium decimal(19, 4), @reinstateDate datetime, @user nvarchar(4000),
    @today datetime, @batch int, @fileName nvarchar(4000), @endtRefNo nvarchar(4000), @gstAmount decimal(19, 4) = NULL, @gstPercent decimal(19, 4) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.MBMReinstatement (MBMNumber, MBMCoverID, MBMEndtBordxDate, MBMMCOFee, MBMPremium, MBMLapsedDate, MBMReinstateDate,
        LastUpdateUSR, LastUpdateDate, MBMEndtBordxBatch, MBMFileName, MBMENDtRefNo, MBMGSTAmt, MBMGSTPerc)
    VALUES (@number, '00', @bordx, @mco, @premium, NULL, @reinstateDate, @user, @today, @batch, @fileName, @endtRefNo, @gstAmount, @gstPercent);
    UPDATE dbo.MBM SET MBMStatus = 'A' WHERE MBMNumber = @number;
END
GO

-- Reinstatement fees (desktop MemberCallFees): plan settings on the reinstatement date.
CREATE OR ALTER PROCEDURE genisis.Bordx_ReinstatePlan
    @plan nvarchar(4000), @hlt nvarchar(4000), @pay nvarchar(4000), @eff datetime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(PLNRoundUpStatus) AS RoundUpStatus, RTRIM(PLNProRate) AS ProRate, RTRIM(PLNPremGenderStatus) AS PremGenderStatus
    FROM dbo.PLN WHERE PLNCode = @plan AND HLTCode = @hlt AND PLNEffDate <= @eff AND (@hlt <> 'N' OR PAYCode = @pay)
    ORDER BY PLNEffDate DESC;
END
GO

-- ===== Generate ASCII =====

-- Members for the ASCII file: one bordereaux date + batch, or a bordereaux date range optionally limited to a plan-code range.
CREATE OR ALTER PROCEDURE genisis.Bordx_AsciiMembers
    @hlt nvarchar(4000), @pay nvarchar(4000), @from datetime, @to datetime, @batch int = NULL, @planFrom nvarchar(4000) = NULL, @planTo nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(m.MBMNumber) AS MembershipNo, RTRIM(m.MBMMemberType) AS MemberType, RTRIM(m.INSCode) AS InsuredType, RTRIM(m.MBMPolicyNo) AS PolicyNo,
        m.MBMPayorEffDate AS EffectiveDate, RTRIM(m.MBMIcBcPp) AS Ic, RTRIM(m.MBMName) AS Name, RTRIM(m.PLNCode) AS PlanCode,
        RTRIM(o.MBMEmployeeNo) AS EmployeeNo, RTRIM(o.MBMGroupCompany) AS GroupCompany, RTRIM(o.MBMPOLSubNo1) AS PolicySubNo,
        w.MBMPOLIssuedDate AS PolicyIssued, RTRIM(w.MBMTransNo) AS TransNo, RTRIM(w.MBMPayNo) AS PayNo, RTRIM(w.MBMPaySeqNo) AS PaySeqNo,
        RTRIM(w.MBMClientNo) AS ClientNo, RTRIM(w.MBMClientID) AS ClientId
    FROM dbo.MBM m
    LEFT JOIN dbo.MBMOthers o ON o.MBMNumber = m.MBMNumber
    LEFT JOIN dbo.MBMOthersTwo w ON w.MBMNumber = m.MBMNumber
    WHERE m.HLTCode = @hlt AND m.PAYCode = @pay AND m.MBMStatus <> 'D' AND m.MBMDataStatus <> 'D'
      AND m.MBMBordxDate >= @from AND m.MBMBordxDate <= @to AND (@batch IS NULL OR m.MBMBatchNo = @batch)
      AND (@planFrom IS NULL OR (m.PLNCode >= @planFrom AND m.PLNCode <= @planTo))
    ORDER BY m.MBMNumber;
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_AsciiCovered
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(c.MBMCRELCode) AS Relationship, RTRIM(c.MBMCName) AS Name, RTRIM(c.MBMCICBCPPNo) AS Ic, RTRIM(c.MBMCNumber) AS MembershipNo,
        RTRIM(c.MBMCCoverID) AS CoverId, RTRIM(c2.MBMCPAYNo) AS PayNo, RTRIM(c2.MBMCPAYSeqNo) AS PaySeqNo, RTRIM(c2.MBMCClientNO) AS ClientNo,
        RTRIM(c2.MBMCClientID) AS ClientId
    FROM dbo.MBMCoveredPersons c
    LEFT JOIN dbo.MBMCoveredPersonsTwo c2 ON c2.MBMCNumber = c.MBMCNumber AND c2.MBMCCoverID = c.MBMCCoverID
    WHERE c.MBMCNumber = @number
    ORDER BY c.MBMCCoverID;
END
GO
