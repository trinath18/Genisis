-- Case > Registration (frmCaseAdmission).
-- Reads replace the legacy Case_Registration dispatchers (main and maintenance), which appended a raw
-- @strAppend string into dynamic SQL. The inserts reuse the existing parameterized procedures
-- (Case_RegInsertCAS2013 / CAS32013, Case_RegInsertCASRemark / 2 / 3, Case_RegInsertDIAFirst / DIAFinal,
-- Case_RegInsertUD, Case_RegInsertCASOthers / 2, Case_RegInsertCASRep, and the maintenance
-- Case_RegInsertCASRef / Ref1 through synonyms); the API calls them inside one transaction.
-- SearchMBMCas is an existing parameterized procedure and is called as it is.

-- Combo lists filled by ComboListing: special approval, claim nature, cash type, stay type, treatment mode,
-- hospitals, and the repudiation letter selections (frmRepudiationSelection: D = document, P = personnel, A = attention).
CREATE OR ALTER PROCEDURE genisis.CaseReg_Lookups
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(SpecAppCode) AS Code, RTRIM(SpecAppDesc) AS Name FROM genisis.Maint_SpecApprove;
    SELECT RTRIM(ClaimNatureCode) AS Code, RTRIM(ClaimNatureName) AS Name FROM dbo.ClaimNature;
    SELECT RTRIM(PayTypeCode) AS Code, RTRIM(PayTypeName) AS Name FROM dbo.PayType
    WHERE SUBSTRING(PayTypeCode, 1, 1) IN (N'C', N'R');
    SELECT RTRIM(PayTypeCode) AS Code, RTRIM(PayTypeName) AS Name FROM dbo.PayType
    WHERE SUBSTRING(PayTypeCode, 1, 1) NOT IN (N'C', N'R');
    SELECT RTRIM(ModalityCode) AS Code, RTRIM(ModalityName) AS Name FROM dbo.TreatmentModality;
    SELECT RTRIM(HOSCode) AS Code, RTRIM(HOSName) AS Name FROM dbo.HOS WHERE HOSName IS NOT NULL ORDER BY HOSName;
    SELECT RTRIM(REPCode) AS Code, RTRIM(REPDescription) AS Name FROM genisis.Maint_REPSelection ORDER BY REPCode;
END
GO

-- Everything cmdShow_Click / showMBM / showMBMCov read besides SearchMBMCas.
-- Result sets: plan prompts, annual limit, agent, principal (View_MBM_MBMOthers), principal exclusion,
-- covered persons with their exclusion, server date.
CREATE OR ALTER PROCEDURE genisis.CaseReg_MemberExtras
    @mbm nvarchar(4000), @plan nvarchar(4000), @pay nvarchar(4000), @hlt nvarchar(4000),
    @ins nvarchar(4000), @eff datetime, @agent nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT AnnLmtIND, MessagePromt, PLNStartAge, PLNEndAge, PLNPolicyWording
    FROM dbo.PLN WHERE PLNCode = @plan AND (@hlt = N'S' OR PAYCode = @pay);

    SELECT TOP (1) AnnLimit FROM dbo.AnnualLimit
    WHERE INSCode = @ins AND PLNCode = @plan AND HLTCode = @hlt AND AnnualEffDate <= @eff
    ORDER BY AnnualEffDate DESC;

    SELECT TOP (1) AgentName FROM dbo.Agent WHERE @agent IS NOT NULL AND AgentCode = @agent;

    SELECT TOP (1) MBMName, MBMIcBcPp, MBMMemberType, MBMDOB, MBMTopupInd, PAYChkHosp
    FROM dbo.View_MBM_MBMOthers WHERE MBMNumber = @mbm;

    SELECT TOP (1) MBMExclusion FROM dbo.MBMOthersTwo WHERE MBMNumber = @mbm;

    SELECT c.MBMCCoverID, c.MBMCName, c.MBMCICBCPPNo, c.MBMCRELCode, c.MBMCDOB, c.MBMCAnnualLimit, t.MBMCExclusion
    FROM dbo.MBMCoveredPersons c
    LEFT JOIN dbo.MBMCoveredPersonsTwo t ON t.MBMCNumber = c.MBMCNumber AND t.MBMCCoverID = c.MBMCCoverID
    WHERE c.MBMCNumber = @mbm
    ORDER BY c.MBMCCoverID;

    SELECT GETDATE() AS Today;
END
GO

-- cboPatientList_Click: patient IC / relationship / DOB / plan and the pre-existing, specific illness and
-- waiting period dates (principal from MBM + MBMOthersTwo, covered person from MBMCoveredPersons + Two).
CREATE OR ALTER PROCEDURE genisis.CaseReg_Patient
    @mbm nvarchar(4000), @coverId nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) m.MBMIcBcPp AS PatientIC, CAST(N'P' AS nvarchar(5)) AS Relationship, m.MBMDOB AS DOB,
           CAST(NULL AS nvarchar(20)) AS PlanCode,
           o.MBMPreExistIllDate AS PreExistIllDate, o.MBMSpecificIllDate AS SpecificIllDate,
           o.MBMWaitingPeriodDate AS WaitingPeriodDate, o.MBMBDayStatus AS BDayStatus
    FROM dbo.MBM m LEFT JOIN dbo.MBMOthersTwo o ON o.MBMNumber = m.MBMNumber
    WHERE m.MBMNumber = @mbm AND @coverId = N'00';

    SELECT TOP (1) c.MBMCICBCPPNo AS PatientIC, c.MBMCRELCode AS Relationship, c.MBMCDOB AS DOB,
           c.MBMCPLNCode AS PlanCode,
           t.MBMCPreExistIllDate AS PreExistIllDate, t.MBMCSpecificIllDate AS SpecificIllDate,
           t.MBMCWaitingPeriodDate AS WaitingPeriodDate, t.MBMCBDayStatus AS BDayStatus
    FROM dbo.MBMCoveredPersons c
    LEFT JOIN dbo.MBMCoveredPersonsTwo t ON t.MBMCNumber = c.MBMCNumber AND t.MBMCCoverID = c.MBMCCoverID
    WHERE c.MBMCNumber = @mbm AND c.MBMCCoverID = @coverId;
END
GO

-- BNFListing / NewBNFListing (legacy Case_Registration modes 5 and 20).
CREATE OR ALTER PROCEDURE genisis.CaseReg_Benefits
    @plan nvarchar(4000), @hlt nvarchar(4000), @ins nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT BNFCode, BNFDescription, BNFNoOfDays, BNFClaimAbleAmount, BNFRMPerDis
    FROM dbo.VIEW_BNF WHERE PLNCode = @plan AND HLTCode = @hlt AND INSCode = @ins;
END
GO

-- MBMHISListing (maintenance Case_Registration mode 3): memberships held under the same IC.
CREATE OR ALTER PROCEDURE genisis.CaseReg_MemberHistory
    @ic nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT MBMNumber, MBMPolicyNo, MBMPayorEffDate, MBMPayorExpDate, INSCode, PLNCode
    FROM genisis.Maint_MBMCrossReference WHERE MBMIcBcPp = @ic;
END
GO

-- SearchPrincipalIll (legacy Case_SearchIllness): pre-existing / underlying illness recorded on earlier cases.
CREATE OR ALTER PROCEDURE genisis.CaseReg_NonDisclosure
    @ic nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT MBMNumber, CASNumber, MBMPatientCovID, CASDateAdmitted, CASPreIllDate, CASPreIllCAT, CASPreIllOthers,
           CASPreIllInfBy, CASUndIllDate, CASUndIllCAT, CASUndIllOthers, CASUndIllInfBy
    FROM dbo.VIEW_Cas_SearchIll WHERE MBMIcBcPp = @ic;
END
GO

-- PolTermsListing (legacy Case_Registration mode 6): health type S reads by plan, N by plan and payor.
CREATE OR ALTER PROCEDURE genisis.CaseReg_PolicyTerms
    @plan nvarchar(4000), @pay nvarchar(4000), @hlt nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) PLNRefRqdAdmin, PLNRefRqdPreSpec, PLNMinDurHosp, PLNPhyVstPerDay, PLNSepPeriodPrin,
           PLNSepPeriodSup, PLNLowerAgeLimit, PLNUpperAgeLimit, PLNStudentAge, PLNChildUntilAge
    FROM dbo.PLN
    WHERE PLNCode = @plan AND (@hlt = N'S' OR (@hlt = N'N' AND PAYCode = @pay));
END
GO

-- ScheduleSurg: surgical schedule for the payor / plan.
CREATE OR ALTER PROCEDURE genisis.CaseReg_SurgicalSchedule
    @pay nvarchar(4000), @plan nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT PAYCode, PLNCode, SurgMainCat, SurgSubCat, SurgPerc
    FROM dbo.PLNSurgOperation WHERE PAYCode = @pay AND PLNCode = @plan;
END
GO

-- frmSearchICD: exact code and/or description fragments (already LIKE-escaped by the caller).
CREATE OR ALTER PROCEDURE genisis.CaseReg_IcdSearch
    @max int, @code nvarchar(4000) = NULL, @desc nvarchar(4000) = NULL, @desc2 nvarchar(4000) = NULL, @matchAny bit = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(ICDCode) AS ICDCode, RTRIM(ICDDescription) AS ICDDescription
    FROM dbo.ICD
    WHERE (@code IS NULL OR ICDCode = @code)
      AND ((@desc IS NULL AND @desc2 IS NULL)
           OR (@matchAny = 1 AND (ICDDescription LIKE N'%' + @desc + N'%' OR ICDDescription LIKE N'%' + @desc2 + N'%'))
           OR (@matchAny = 0 AND (@desc IS NULL OR ICDDescription LIKE N'%' + @desc + N'%')
                             AND (@desc2 IS NULL OR ICDDescription LIKE N'%' + @desc2 + N'%')))
    ORDER BY ICDCode
    OPTION (RECOMPILE);
END
GO

-- FrmSearchRepudiation: code prefix and/or description fragments (already LIKE-escaped by the caller).
CREATE OR ALTER PROCEDURE genisis.CaseReg_RepCodeSearch
    @max int, @code nvarchar(4000) = NULL, @desc nvarchar(4000) = NULL, @desc2 nvarchar(4000) = NULL, @matchAny bit = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(RepCode) AS RepCode, RTRIM(RepDescription) AS RepDescription, RepNonDisc
    FROM dbo.RepCode
    WHERE (@code IS NULL OR RepCode LIKE @code + N'%')
      AND ((@desc IS NULL AND @desc2 IS NULL)
           OR (@matchAny = 1 AND (RepDescription LIKE N'%' + @desc + N'%' OR RepDescription LIKE N'%' + @desc2 + N'%'))
           OR (@matchAny = 0 AND (@desc IS NULL OR RepDescription LIKE N'%' + @desc + N'%')
                             AND (@desc2 IS NULL OR RepDescription LIKE N'%' + @desc2 + N'%')))
    ORDER BY RepCode
    OPTION (RECOMPILE);
END
GO

-- SearchMMARate: surgeon / anaesthetist MMA fees for an OPCS code (maintenance VIEW_MMA_Schedule).
CREATE OR ALTER PROCEDURE genisis.CaseReg_MmaRate
    @code nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) SurgeonsFees, AnaesthetistsFees FROM genisis.Maint_MMASchedule WHERE OPCSCode = @code;
END
GO

-- frmSearchCPT / frmSearchInv: OPCS code list (maintenance MMAProcedures). Code is a prefix match and the
-- narrative a contains match, like the desktop's strCriteria. Investigations are ProcPart 'A'.
CREATE OR ALTER PROCEDURE genisis.CaseReg_OpcsSearch
    @max int, @code nvarchar(4000) = NULL, @desc nvarchar(4000) = NULL, @part nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(OPCSCode) AS OPCSCode, RTRIM(OPCSNarrative) AS OPCSNarrative
    FROM genisis.Maint_MMAProcedures
    WHERE OPCSCode IS NOT NULL
      AND (@code IS NULL OR OPCSCode LIKE @code + N'%')
      AND (@desc IS NULL OR OPCSNarrative LIKE N'%' + @desc + N'%')
      AND (@part IS NULL OR ProcPart = @part)
    ORDER BY OPCSCode
    OPTION (RECOMPILE);
END
GO

-- txtHOSCode_LostFocus panel check: hospital status, payor check flag, and whether the hospital is on the
-- payor's panel. AX uses the panel code chosen by policy number exactly as the desktop does.
CREATE OR ALTER PROCEDURE genisis.CaseReg_HospitalCheck
    @hosCode nvarchar(4000), @pay nvarchar(4000), @policyNo nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @panelCode nvarchar(100) = CASE
        WHEN @policyNo = N'03215666' THEN N'AX - AXA AFFIN STAFF (NON-EXECUTIVE)'
        WHEN @policyNo IN (N'03215814', N'03215886') THEN N'AX - SPNB'
        WHEN @policyNo = N'03312049' THEN N'AX - EMBASSY OF LIBYA (STAFF&MT)'
        ELSE N'AX - AXA AFFIN' END;
    SELECT RTRIM(h.HOSName) AS HOSName, h.HOSStatus,
           (SELECT TOP (1) PAYChkHosp FROM dbo.PAY WHERE PAYCode = @pay) AS PAYChkHosp,
           CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.PanelHospitals p
                                  WHERE p.HOSCode = @hosCode AND p.PayCode = @pay
                                    AND (@pay <> N'AX' OR p.HosPanelCode = @panelCode))
                THEN 1 ELSE 0 END AS bit) AS OnPanel
    FROM dbo.HOS h WHERE h.HOSCode = @hosCode;
END
GO

-- NewCaseAddmission pre-save checks (maintenance Case_Registration modes 1 and 2):
-- policy not yet active, activated within 30 days, and an existing case for the patient on the same admission date.
CREATE OR ALTER PROCEDURE genisis.CaseReg_PreChecks
    @mbm nvarchar(4000), @coverId nvarchar(4000), @doa date
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        CAST(CASE WHEN EXISTS (SELECT 1 FROM genisis.Maint_MBMCrossReference
                               WHERE MBMNumber = @mbm AND MBMPayorEffDate >= GETDATE()) THEN 1 ELSE 0 END AS bit) AS NotActivated,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM genisis.Maint_MBMCrossReference
                               WHERE MBMNumber = @mbm AND MBMPayorEffDate >= DATEADD(day, -30, GETDATE())
                                 AND MBMPayorEffDate <= GETDATE()) THEN 1 ELSE 0 END AS bit) AS RecentlyActivated,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM genisis.Maint_CASCrossReference
                               WHERE MBMNumber = @mbm AND MBMPatientCovID = @coverId
                                 AND CAST(CASDateAdmitted AS date) = @doa) THEN 1 ELSE 0 END AS bit) AS SameDayAdmission;
END
GO

-- GenerateCaseNo: next number from the maintenance CAQ2013 counter for the payor (payor code + 8 digits).
-- Call inside the registration transaction so the UPDLOCK/HOLDLOCK read lock lasts until commit.
CREATE OR ALTER PROCEDURE genisis.CaseReg_NextCaseNo
    @payor nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @code int, @n bigint;
    SELECT TOP (1) @code = CAQCode, @n = TRY_CAST(CAQNumber AS bigint)
    FROM genisis.Maint_CAQ2013 WITH (UPDLOCK, HOLDLOCK) WHERE PAYCode = @payor ORDER BY CAQCode;

    DECLARE @text nvarchar(20);
    IF @code IS NULL
    BEGIN
        SET @text = N'00000001';
        INSERT INTO genisis.Maint_CAQ2013 (PAYCode, CAQNumber) VALUES (@payor, @text);
    END
    ELSE
    BEGIN
        SET @n = ISNULL(@n, 0) + 1;
        SET @text = CAST(@n AS nvarchar(20));
        IF LEN(@text) < 8 SET @text = REPLICATE(N'0', 8 - LEN(@text)) + @text;
        UPDATE genisis.Maint_CAQ2013 SET CAQNumber = @text WHERE CAQCode = @code;
    END
    SELECT @payor + @text AS CaseNumber;
END
GO

-- UpdateBalLmt: reduce the member's available limit by the reduction amount, never below zero.
CREATE OR ALTER PROCEDURE genisis.CaseReg_ReduceLimit
    @mbm nvarchar(4000), @amount money
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.MBM
    SET MBMAvailableLimit = CASE WHEN MBMAvailableLimit > @amount THEN MBMAvailableLimit - @amount ELSE 0 END
    WHERE MBMNumber = @mbm;
END
GO

-- cmdLG_Click + funcword: the saved case with the member / plan / covered-person fields the LG balance
-- check, the CASLG insert and the letters read. Parameterized subset of SearchCasMBMClaim mode 3.
CREATE OR ALTER PROCEDURE genisis.CaseReg_LGContext
    @cas nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1)
        c.CASNumber, c.MBMNumber, c.MBMPatientCovID, c.CASDateAdmitted, c.CASDischargeDate,
        c.CASTopupLmt, c.CASHOSCode, RTRIM(h.HOSName) AS HOSName, c.CASClaimability,
        m.MBMPolicyNo, RTRIM(m.MBMName) AS MBMName, m.MBMIcBcPp, m.INSCode, m.PAYCode, m.HLTCode,
        m.PLNCode, m.MBMPayorEffDate, m.MBMPayorExpDate, m.MBMTopupInd, m.MBMTopupNumber, m.MBMPolEffDate,
        CASE WHEN c.MBMPatientCovID = '00' THEN m.MBMMemberType ELSE cp.MBMCMemberType END AS MBMMemberType,
        RTRIM(COALESCE(cp.MBMCName, m.MBMName)) AS PatientName, cp.MBMCICBCPPNo AS PatientIC, cp.MBMCPLNCode,
        mo.MBMGroupCompany,
        p.AnnLmtIND, p.PLNLifeTimeStatus, p.PLNSOF, p.PLNBTBG, p.PLNPerVisit, p.PLNRBType, p.PLNLGType,
        p.CoPaymentInd, p.PLNMeal, p.PLNNursing, p.PLNTax, p.PLNMRI
    FROM dbo.CAS c
    INNER JOIN dbo.MBM m ON m.MBMNumber = c.MBMNumber
    LEFT JOIN dbo.MBMCoveredPersons cp
        ON cp.MBMCNumber = c.MBMNumber AND cp.MBMCCoverID = c.MBMPatientCovID
    LEFT JOIN dbo.MBMOthers mo ON mo.MBMNumber = m.MBMNumber
    LEFT JOIN dbo.HOS h ON h.HOSCode = c.CASHOSCode
    LEFT JOIN dbo.PLN p
        ON p.PAYCode = m.PAYCode AND p.HLTCode = m.HLTCode AND p.PLNCode = m.PLNCode
    WHERE c.CASNumber = @cas;
END
GO

-- CheckLimit's AnnualLimit row: the limit effective at the admission date for the plan / insured type.
CREATE OR ALTER PROCEDURE genisis.CaseReg_LGAnnualLimit
    @plan nvarchar(50), @ins nvarchar(50), @doa datetime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) DisabilityLmt, SuppLimitStatus, AnnLimit, SuppLimit, LifeTimeLimit, PLNCode
    FROM dbo.AnnualLimit
    WHERE PLNCode = @plan AND INSCode = @ins AND AnnualEffDate <= @doa
    ORDER BY AnnualEffDate DESC;
END
GO

-- CheckLimit's top-up reads: the cross reference row for the top-up membership, the top-up member itself
-- and the lifetime-guarantee renewal flag (maintenance + main tables the desktop queries separately).
CREATE OR ALTER PROCEDURE genisis.CaseReg_TopupContext
    @topup nvarchar(30), @cov nvarchar(10)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) MBMNumber, INSCode, PLNCode, MBMPayorEffDate, MBMPayorExpDate, MBMPolicyNo, MBMTopupNumber
    FROM genisis.Maint_MBMCrossReference WHERE MBMTopupNumber = @topup;

    SELECT TOP (1) MBMNumber, INSCode, PLNCode, MBMPayorEffDate, MBMPayorExpDate, MBMPolicyNo,
           MBMAvailableLimit, MBMTopupNumber
    FROM dbo.MBM WHERE MBMNumber = @topup;

    SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.MBMLifeTime WHERE MBMNumber = @topup AND MBMCovID = @cov)
                     THEN 1 ELSE 0 END AS bit) AS LTGuaranteedRenewal;
END
GO

-- funcword's bookmark lookups: the payor letterhead, the hospital contact and the plan's co-pay / MRI
-- flags plus city / state names (Case_Registration modes 15, 16 and 17).
CREATE OR ALTER PROCEDURE genisis.CaseReg_LetterContext
    @pay nvarchar(10), @hosName nvarchar(200), @plan nvarchar(50), @hlt nvarchar(10),
    @cty nvarchar(20) = NULL, @sta nvarchar(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(PAYCompanyName) AS PAYCompanyName, PAYAddress1, PAYAddress2, PAYAddress3,
           PAYMainContact, PAYAdmissionContact
    FROM dbo.PAY WHERE PAYCode = @pay;

    SELECT TOP (1) HOSContact, HOSFaxNo FROM dbo.HOS WHERE HOSName = @hosName;

    SELECT TOP (1) CoPaymentInd, PLNMeal, PLNNursing, PLNTax, PLNMRI
    FROM dbo.PLN WHERE PLNCode = @plan AND (@hlt = N'S' OR PAYCode = @pay);

    SELECT TOP (1) RTRIM(CTYDescription) AS City FROM genisis.Maint_CTY WHERE CTYCode = @cty;
    SELECT TOP (1) RTRIM(STADescription) AS [State] FROM dbo.STA WHERE STACode = @sta;
END
GO

-- Repudiation letter detail: the reason description plus its category name and text
-- (Case_Registration modes 18 and 19).
CREATE OR ALTER PROCEDURE genisis.CaseReg_RepLetterDetail
    @code nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(r.RepDescription) AS RepDescription,
           RTRIM(rc.RepCatName) AS RepCatName, RTRIM(rc.REPCatDescription) AS REPCatDescription
    FROM dbo.RepCode r
    LEFT JOIN dbo.RepCodeCategory rc ON rc.REPCatCode = r.REPCatCode
    WHERE r.RepCode = @code;
END
GO
