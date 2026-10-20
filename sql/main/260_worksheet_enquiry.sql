-- Worksheet > Worksheet Enquiry (frmWorksheetEnq): read-only enquiry on a saved worksheet. The 29 benefit slots of ClaimActual /
-- ClaimApprove / ClaimHos / ClaimMember / ClaimLimit use the row -> column map the desktop's save routines (saveActual, saveApprove,
-- saveHos, saveMember, saveLimit) write: row 1 Room & Board ... row 29 Service Tax - Lodger. The amounts are stored as the text the
-- grid showed ("1,200.00", "-", "(50.00)"), hence Worksheet_Amount.

CREATE OR ALTER FUNCTION genisis.Worksheet_Amount (@s nvarchar(60))
RETURNS decimal(18, 2)
AS
BEGIN
    DECLARE @t nvarchar(60) = REPLACE(REPLACE(LTRIM(RTRIM(ISNULL(@s, N''))), N',', N''), N' ', N'');
    DECLARE @neg bit = CASE WHEN LEFT(@t, 1) = N'(' AND RIGHT(@t, 1) = N')' THEN 1 ELSE 0 END;
    IF @neg = 1 SET @t = SUBSTRING(@t, 2, LEN(@t) - 2);
    RETURN CASE
        WHEN @t = N'' OR LEN(@t) > 15 OR @t LIKE N'%[^0-9.-]%' OR @t NOT LIKE N'%[0-9]%'
          OR @t LIKE N'%.%.%' OR @t LIKE N'%-%-%' OR (@t LIKE N'%-%' AND LEFT(@t, 1) <> N'-') THEN 0
        WHEN @neg = 1 THEN -ABS(CAST(@t AS decimal(18, 2)))
        ELSE CAST(@t AS decimal(18, 2))
    END;
END
GO

-- cmdSearchCase_Click (frmCASList): the cases to pick from, found by case / member number prefix, patient name, I/C or policy number.
-- Versions is how many worksheet versions the case has (0: nothing to enquire on yet).
CREATE OR ALTER PROCEDURE genisis.WorksheetEnquiry_Cases
    @max int, @by nvarchar(20), @q nvarchar(100), @prefix nvarchar(200), @contains nvarchar(200)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(c.CASNumber) AS CaseNo, RTRIM(c.MBMNumber) AS MemberNo, RTRIM(COALESCE(cp.MBMCName, x.MBMName)) AS PatientName,
        RTRIM(h.HOSName) AS Hospital, c.CASDateAdmitted AS Admitted, c.CASDischargeDate AS Discharged, RTRIM(x.MBMPolicyNo) AS PolicyNo,
        RTRIM(x.PAYCode) AS Payor, RTRIM(c.CASStatus) AS CaseStatus, RTRIM(c.CASClaimability) AS Claimability,
        (SELECT COUNT(*) FROM genisis.WS_Claim w WHERE w.CASNumber = c.CASNumber) AS Versions
    FROM dbo.CAS c WITH (NOLOCK)
    LEFT JOIN genisis.Maint_MBMCrossReference x ON c.MBMNumber = x.MBMNumber
    LEFT JOIN dbo.MBMCoveredPersons cp WITH (NOLOCK) ON c.MBMNumber = cp.MBMCNumber AND c.MBMPatientCovID = cp.MBMCCoverID
    LEFT JOIN dbo.HOS h WITH (NOLOCK) ON c.CASHOSCode = h.HOSCode
    WHERE (@by = N'number' AND c.CASNumber LIKE @prefix)
       OR (@by = N'member' AND c.MBMNumber LIKE @prefix)
       OR (@by = N'patient' AND (cp.MBMCName LIKE @contains OR x.MBMName LIKE @contains))
       OR (@by = N'ic' AND (x.MBMIcBcPp = @q OR REPLACE(x.MBMIcBcPp, '-', '') = REPLACE(@q, '-', '')))
       OR (@by = N'policy' AND x.MBMPolicyNo LIKE @prefix)
    ORDER BY c.CaseRegDate DESC, c.CASNumber
    OPTION (RECOMPILE);
END
GO

-- The versions of a case's worksheet (cboVersion lists each as "n" and "Mn", then "Master").
CREATE OR ALTER PROCEDURE genisis.WorksheetEnquiry_Versions
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ClaimVersion, RTRIM(ClaimStatus) AS ClaimStatus, RTRIM(CLMWSStatus) AS WsStatus, CAST(ISNULL(ClaimBordxStatus, 0) AS bit) AS Bordxed
    FROM genisis.WS_Claim WHERE CASNumber = @cas ORDER BY ClaimVersion;
END
GO

-- cmdDisplay_Click: the case, member, policy and worksheet header of one version (SearchCasMBMClaim), the policy status text and the
-- live limits. No row when the case has no such worksheet or its case / member is missing (the desktop query joins them: "Record not found!").
-- Balance limit (txtBalLimit), for members without a top-up: the member's available limit; a "Q" member of a supplementary plan with
-- limit status B / C and a dependant patient uses the covered person's limit instead (the principal's "01" row for B, the patient's for C).
-- Top-up policies add their own balance in the desktop; that figure is shown from the saved worksheet on the Topup tab instead.
CREATE OR ALTER PROCEDURE genisis.WorksheetEnquiry_Header
    @cas nvarchar(15), @version int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @mbm nvarchar(20), @cov nvarchar(4), @plan nvarchar(10), @ins nvarchar(4), @admit datetime, @type nvarchar(4),
        @lifeStatus nvarchar(2), @guaRenewal nvarchar(2), @supp nvarchar(2), @available money, @lifetime money, @found int;

    SELECT TOP (1) @mbm = RTRIM(MBMNumber), @cov = RTRIM(MBMPatientCovID), @ins = RTRIM(INSCode), @admit = CASDateAdmitted, @type = RTRIM(MBMMemberType),
        @lifeStatus = RTRIM(PLNLifeTimeStatus), @guaRenewal = RTRIM(MBMGuaRenewal),
        @plan = RTRIM(CASE WHEN RTRIM(MBMPatientCovID) <> N'00' AND ISNULL(RTRIM(MBMCPLNCode), N'') <> N'' THEN MBMCPLNCode ELSE PLNCode END)
    FROM dbo.VIEW_Claim_MBM_Case_worksheetAdjustment WHERE CASNumber = @cas AND ClaimVersion = @version AND MBMNumber IS NOT NULL;
    SET @found = @@ROWCOUNT;
    IF @found = 0 RETURN;

    SELECT TOP (1) @supp = RTRIM(SuppLimitStatus) FROM dbo.AnnualLimit
    WHERE PLNCode = @plan AND INSCode = @ins AND AnnualEffDate <= @admit ORDER BY AnnualEffDate DESC;

    SELECT TOP (1) @available = MBMAvailableLimit FROM dbo.MBM WHERE MBMNumber = @mbm;
    IF @type = N'Q' AND @cov <> N'00' AND @supp IN (N'B', N'C')
        SELECT TOP (1) @available = MBMCAvailableLimit FROM dbo.MBMCoveredPersons
        WHERE MBMCNumber = @mbm AND MBMCCoverID = CASE WHEN @supp = N'B' THEN N'01' ELSE @cov END;

    IF @lifeStatus = N'Y' AND @guaRenewal = N'3'
        SELECT TOP (1) @lifetime = MBMLifeTimeLmt FROM dbo.MBMLifeTime
        WHERE MBMNumber = @mbm AND MBMCovID = CASE WHEN @type = N'Q' AND @supp = N'B' AND @cov <> N'00' THEN N'01'
                                                   WHEN @type = N'Q' AND @supp = N'C' AND @cov <> N'00' THEN @cov ELSE N'00' END;

    SELECT TOP (1)
        RTRIM(v.CASNumber) AS CaseNo, v.ClaimVersion, RTRIM(v.MBMNumber) AS MemberNo, RTRIM(v.MBMPatientCovID) AS PatientCovId,
        RTRIM(v.MBMName) AS MemberName,
        CASE WHEN RTRIM(v.MBMPatientCovID) <> N'00' AND ISNULL(RTRIM(v.MBMCName), N'') <> N'' THEN RTRIM(v.MBMCName) ELSE RTRIM(v.MBMName) END AS PatientName,
        RTRIM(v.MBMPolicyNo) AS PolicyNo, RTRIM(v.PAYCode) AS Payor, RTRIM(v.PAYCompanyName) AS PayorName,
        @plan AS PlanCode, RTRIM(v.INSCode) AS InsCode, RTRIM(v.HLTCode) AS HltCode, RTRIM(v.GRPCompany) AS GroupCompany,
        v.MBMPayorEffDate AS PayorEffDate, v.MBMPayorExpDate AS PayorExpDate, v.MBMPolEffDate AS PolicyEffDate,
        RTRIM(v.MBMStatus) AS PolicyStatusCode, RTRIM(ps.PStatusDesc) AS PolicyStatus, RTRIM(v.MBMExpiredStatus) AS ExpiredStatus,
        RTRIM(v.CASStatus) AS CaseStatus, RTRIM(v.CASClaimability) AS Claimability, RTRIM(v.PayTypeCode) AS PayType,
        RTRIM(v.CASHOSCode) AS HosCode, RTRIM(v.HOSName) AS Hospital, v.CASDateAdmitted AS Admitted, v.CASDischargeDate AS Discharged,
        RTRIM(v.ClaimStatus) AS ClaimStatus, RTRIM(v.CLMWSStatus) AS WsStatus, v.ClaimOpenDate AS WorksheetOpened,
        CAST(ISNULL(v.ClaimBordxStatus, 0) AS bit) AS Bordxed, RTRIM(v.BordxRefNo) AS BordxRefNo, RTRIM(v.CLMExGratia) AS ExGratia,
        RTRIM(v.CLMMHBordx) AS BordxInd, RTRIM(v.CLMLinkCase) AS LinkCase, v.CLMLinkVersion AS LinkVersion, RTRIM(v.MBMGLIssuance) AS GlIssuance,
        RTRIM(v.MBMTopupNumber) AS TopupNumber, RTRIM(v.MBMTopupInd) AS TopupInd,
        @available AS AvailableLimit, @lifetime AS LifetimeLimit, v.CASTakeOverAmt AS TakeOverLimit,
        v.CLMLimitExceeded AS LimitExceeded, v.CLMPreviousBalance AS PreviousBalance, v.CLMBalanceLimit AS BalanceAfterClaim,
        v.CLMLifetimeBal AS LifetimeBalance, v.CLMTotalApproved AS TotalApproved, v.CLMFinalApprove AS FinalApprove,
        RTRIM(v.LimitCollectedBy) AS LimitCollectedBy, RTRIM(v.ExcessCollectedBy) AS ExcessCollectedBy,
        CASE WHEN RTRIM(v.CLMNonPayment) = N'Y' THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS NonPayment,
        v.CLMExGratiaAmt AS ExGratiaAmount, v.CLMCaseMgmtFees AS ProcessingFee, v.CLMMajorMedFee AS MajorMedicalFee, v.CLMMainTUAmt AS MainTopupAmount,
        v.CLMComApp AS CompanyApproved, v.CLMTopUpApp AS TopupApproved, v.CLMPreviousNewLimit AS TopupPreviousBalance, v.CLMPreviousUndExcess AS UnderwritingExcess,
        RTRIM(v.DebitCreditRefNo) AS DebitCreditRefNo, v.DebitCreditDate, RTRIM(v.DebitCreditHRefNo) AS HosDebitCreditRefNo, v.DebitCreditHAmt AS HosDebitCreditAmount,
        v.CLMLastDocDate AS LastDocumentDate, RTRIM(v.CLMPayTo) AS PayTo, RTRIM(v.CLMBank) AS Bank, RTRIM(v.CLMAccountNo) AS AccountNo, RTRIM(v.CLMGPName) AS GpName
    FROM dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v
    LEFT JOIN dbo.PolicyStatus ps ON ps.PStatusCode = v.MBMStatus
    WHERE v.CASNumber = @cas AND v.ClaimVersion = @version AND v.MBMNumber IS NOT NULL;
END
GO

-- Display_Diagnosis: the first (preliminary) and final diagnoses of the case, up to three each.
CREATE OR ALTER PROCEDURE genisis.WorksheetEnquiry_Diagnoses
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT d.Source, d.Slot, RTRIM(d.Diagnosis) AS Diagnosis, RTRIM(d.OnsetDate) AS OnsetDate, RTRIM(d.IcdCode) AS IcdCode
    FROM (
        SELECT 1 AS SourceOrder, N'First' AS Source, s.Slot, s.Diagnosis, s.OnsetDate, s.IcdCode
        FROM dbo.DIAFirst f CROSS APPLY (VALUES
            (1, f.DIAPrelimDiag1, f.DIADOS1, f.ICDCode1), (2, f.DIAPrelimDiag2, f.DIADOS2, f.ICDCode2), (3, f.DIAPrelimDiag3, f.DIADOS3, f.ICDCode3)
        ) s(Slot, Diagnosis, OnsetDate, IcdCode) WHERE f.CASNumber = @cas
        UNION ALL
        SELECT 2, N'Final', s.Slot, s.Diagnosis, s.OnsetDate, s.IcdCode
        FROM dbo.DIAFinal f CROSS APPLY (VALUES
            (1, f.DIAFinalDiag1, f.DIADOS1, f.ICDCode1), (2, f.DIAFinalDiag2, f.DIADOS2, f.ICDCode2), (3, f.DIAFinalDiag3, f.DIADOS3, f.ICDCode3)
        ) s(Slot, Diagnosis, OnsetDate, IcdCode) WHERE f.CASNumber = @cas
    ) d
    WHERE LTRIM(RTRIM(ISNULL(d.Diagnosis, N''))) <> N'' OR LTRIM(RTRIM(ISNULL(d.OnsetDate, N''))) <> N'' OR LTRIM(RTRIM(ISNULL(d.IcdCode, N''))) <> N''
    ORDER BY d.SourceOrder, d.Slot;
END
GO

-- ShowInvoice: the invoices of one version (a numeric version) or every accepted invoice of the case (Mn / Master), optionally only the
-- cashless (pay to hospital, 'H') or reimbursement (pay to member, 'M') ones. The last eight columns feed the Reimbursement Not Cover tab.
CREATE OR ALTER PROCEDURE genisis.WorksheetEnquiry_Invoices
    @cas nvarchar(15), @version int = NULL, @payTo char(1) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT INVIndex AS InvIndex, CAST(ISNULL(INVBordxed, 0) AS bit) AS Bordxed, CAST(ISNULL(INVWSed, 0) AS bit) AS WsDone, RTRIM(INVWSversion) AS WsVersion,
        RTRIM(INVNo) AS InvNo, InvDate, INVAmount AS InvAmount, RTRIM(InvPayTo) AS PayTo, RTRIM(INVPayee) AS Payee, RTRIM(ClaimType) AS ClaimType,
        INVFolUpDate AS FollowUpDate, CLMBoardDay AS DailyRb, CLMRB AS RbRate, CLMICUDay AS IcuDays, CLMICU AS IcuRate, CLMServices AS HospitalServices,
        CLMTheatre AS OperatingTheatre, CLMPreHSAmt AS PreHospitalService, CLMPhysician AS PhysicianVisit, CLMPost AS PostHospitalTreatment,
        PreH60 AS PreHospitalOver60, PreGP AS PreHospitalGpFee, preMedic AS PreHospitalMedicine, PreMR AS PreHospitalMedicalReport, PreSpec AS PreHospitalSpecialistFee,
        PostHosp AS PostHospitalOver31Days, PostNotSame AS PostHospitalDifferentPhysician, Others AS NotCoveredOthers
    FROM genisis.WS_CASInvoice
    WHERE CASNumber = @cas
      AND ((@version IS NOT NULL AND INVWSversion = CAST(@version AS nvarchar(10))) OR (@version IS NULL AND ClaimStatus = N'C'))
      AND (@payTo IS NULL OR InvPayTo = @payTo)
    ORDER BY INVIndex;
END
GO

-- The benefit rows of the worksheet grid (25 benefits, then Others - No Co-Pay and the three service-tax rows) for a range of versions (one version, "Mn" = 1..n, "Master" = all): actual, approved,
-- payable to hospital and due to / (from) member are summed over the range; limit and days are those of the last version in the range.
CREATE OR ALTER PROCEDURE genisis.WorksheetEnquiry_Benefits
    @cas nvarchar(15), @fromVersion int, @toVersion int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @last int = (SELECT MAX(CLMVersion) FROM genisis.WS_ClaimLimit WHERE CASNumber = @cas AND CLMVersion BETWEEN @fromVersion AND @toVersion);
    DECLARE @days TABLE (Slot int PRIMARY KEY, Days nvarchar(30));
    INSERT @days SELECT s.Slot, s.Days FROM genisis.WS_ClaimLimit l CROSS APPLY (VALUES
        (1, l.CLMBoardDays), (2, l.CLMICUDays), (10, l.CLMINHOSPhyDays), (16, l.CLMKIDDCDays), (18, l.CLMCashDays), (19, l.CLMGurdianDays)
    ) s(Slot, Days) WHERE l.CASNumber = @cas AND l.CLMVersion = @last;

    WITH act AS (
        SELECT v.Slot, SUM(genisis.Worksheet_Amount(v.Amt)) AS Amt
        FROM genisis.WS_ClaimActual a
        CROSS APPLY (VALUES
            (1, a.CLMBoardAmt), (2, a.CLMICUAmt), (3, a.CLMServices), (4, a.CLMTheatre), (5, a.CLMMRI),
            (6, a.CLMPreHSAmt), (7, a.CLMPreHosp), (8, a.CLMSurgicalAmt), (9, a.CLMAna), (10, a.CLMNSPreHos),
            (11, a.CLMPhysician), (12, a.CLMPost), (13, a.CLMMPreHSAmt), (14, a.CLMAccPostAmt), (15, a.CLMAmbulance),
            (16, a.CLMOutpatient), (17, a.CLMMonthly), (18, a.CLMOrgan), (19, a.CLMTax), (20, a.CLMGuardianAmt),
            (21, a.CLMMedical), (22, a.CLMPhone), (23, a.CLMAdmin), (24, a.CLMHomeNCAmt), (25, a.CLMOthers1),
            (26, a.CLMOthersNotCoPay), (27, a.CLMBoardTaxAmt), (28, a.CLMICUTaxAmt), (29, a.CLMLodgerTaxAmt)
        ) v(Slot, Amt)
        WHERE a.CASNumber = @cas AND a.CLMVersion BETWEEN @fromVersion AND @toVersion
        GROUP BY v.Slot
    ), app AS (
        SELECT v.Slot, SUM(genisis.Worksheet_Amount(v.Amt)) AS Amt
        FROM genisis.WS_ClaimApprove p
        CROSS APPLY (VALUES
            (1, p.CLMBoardAmt), (2, p.CLMICUAmt), (3, p.CLMServices), (4, p.CLMTheatre), (5, p.CLMMRI),
            (6, p.CLMPreHSAmt), (7, p.CLMPreHosp), (8, p.CLMSurgicalAmt), (9, p.CLMAna), (10, p.CLMNSPreHos),
            (11, p.CLMPhysician), (12, p.CLMPost), (13, p.CLMMPreHSAmt), (14, p.CLMAccPostAmt), (15, p.CLMAmbulance),
            (16, p.CLMOutpatient), (17, p.CLMMonthly), (18, p.CLMOrgan), (19, p.CLMTax), (20, p.CLMGuardianAmt),
            (21, p.CLMMedical), (22, p.CLMPhone), (23, p.CLMAdmin), (24, p.CLMHomeNCAmt), (25, p.CLMOthers1),
            (26, p.CLMOthersNotCoPay), (27, p.CLMBoardTaxAmt), (28, p.CLMICUTaxAmt), (29, p.CLMLodgerTaxAmt)
        ) v(Slot, Amt)
        WHERE p.CASNumber = @cas AND p.CLMVersion BETWEEN @fromVersion AND @toVersion
        GROUP BY v.Slot
    ), hos AS (
        SELECT v.Slot, SUM(genisis.Worksheet_Amount(v.Amt)) AS Amt
        FROM genisis.WS_ClaimHos h
        CROSS APPLY (VALUES
            (1, h.CLMBoardAmt), (2, h.CLMICUAmt), (3, h.CLMServices), (4, h.CLMTheatre), (5, h.CLMMRI),
            (6, h.CLMPreHSAmt), (7, h.CLMPreHosp), (8, h.CLMSurgicalAmt), (9, h.CLMAna), (10, h.CLMNSPreHos),
            (11, h.CLMPhysician), (12, h.CLMPost), (13, h.CLMMPreHSAmt), (14, h.CLMAccPostAmt), (15, h.CLMAmbulance),
            (16, h.CLMOutpatient), (17, h.CLMMonthly), (18, h.CLMOrgan), (19, h.CLMTax), (20, h.CLMGuardianAmt),
            (21, h.CLMMedical), (22, h.CLMPhone), (23, h.CLMAdmin), (24, h.CLMHomeNCAmt), (25, h.CLMOthers1),
            (26, h.CLMOthersNotCoPay), (27, h.CLMBoardTaxAmt), (28, h.CLMICUTaxAmt), (29, h.CLMLodgerTaxAmt)
        ) v(Slot, Amt)
        WHERE h.CASNumber = @cas AND h.CLMVersion BETWEEN @fromVersion AND @toVersion
        GROUP BY v.Slot
    ), mem AS (
        SELECT v.Slot, SUM(genisis.Worksheet_Amount(v.Amt)) AS Amt
        FROM genisis.WS_ClaimMember m
        CROSS APPLY (VALUES
            (1, m.CLMBoardAmt), (2, m.CLMICUAmt), (3, m.CLMServices), (4, m.CLMTheatre), (5, m.CLMMRI),
            (6, m.CLMPreHSAmt), (7, m.CLMPreHosp), (8, m.CLMSurgicalAmt), (9, m.CLMAna), (10, m.CLMNSPreHos),
            (11, m.CLMPhysician), (12, m.CLMPost), (13, m.CLMMPreHSAmt), (14, m.CLMAccPostAmt), (15, m.CLMAmbulance),
            (16, m.CLMOutpatient), (17, m.CLMMonthly), (18, m.CLMOrgan), (19, m.CLMTax), (20, m.CLMGuardianAmt),
            (21, m.CLMMedical), (22, m.CLMPhone), (23, m.CLMAdmin), (24, m.CLMHomeNCAmt), (25, m.CLMOthers1),
            (26, m.CLMOthersNotCoPay), (27, m.CLMBoardTaxAmt), (28, m.CLMICUTaxAmt), (29, m.CLMLodgerTaxAmt)
        ) v(Slot, Amt)
        WHERE m.CASNumber = @cas AND m.CLMVersion BETWEEN @fromVersion AND @toVersion
        GROUP BY v.Slot
    ), lim AS (
        SELECT v.Slot, genisis.Worksheet_Amount(v.Amt) AS Amt
        FROM genisis.WS_ClaimLimit l
        CROSS APPLY (VALUES
            (1, l.CLMBoardAmt), (2, l.CLMICUAmt), (3, l.CLMServices), (4, l.CLMTheatre), (5, l.CLMMRI),
            (6, l.CLMPreHSAmt), (7, l.CLMPreHosp), (8, l.CLMSurgicalAmt), (9, l.CLMAna), (10, l.CLMNSPreHos),
            (11, l.CLMPhysician), (12, l.CLMPost), (13, l.CLMMPreHSAmt), (14, l.CLMAccPostAmt), (15, l.CLMAmbulance),
            (16, l.CLMOutpatient), (17, l.CLMMonthly), (18, l.CLMOrgan), (19, l.CLMTax), (20, l.CLMGuardianAmt),
            (21, l.CLMMedical), (22, l.CLMPhone), (23, l.CLMAdmin), (24, l.CLMHomeNCAmt), (25, l.CLMOthers1),
            (26, l.CLMOthersNotCoPay), (27, l.CLMBoardTaxAmt), (28, l.CLMICUTaxAmt), (29, l.CLMLodgerTaxAmt)
        ) v(Slot, Amt)
        WHERE l.CASNumber = @cas AND l.CLMVersion = @last
    )
    SELECT n.Slot, n.BenefitCode, n.Category, n.Benefit, d.Days, lim.Amt AS Limit, act.Amt AS Actual, app.Amt AS Approved, hos.Amt AS Hospital, mem.Amt AS Member
    FROM (
        SELECT CAST(ROW_NUMBER() OVER (ORDER BY BNFSubCode) AS int) AS Slot, RTRIM(BNFSubCode) AS BenefitCode, RTRIM(CATDescription) AS Category, RTRIM(BNFSubName) AS Benefit
        FROM dbo.view_bnf_cat_subcat WHERE BNFSubCode NOT IN (N'GenSurgBen', N'ine001')
        UNION ALL SELECT 26, N'', N'Others', N'Others - No Co-Pay'
        UNION ALL SELECT 27, N'', N'Service Tax', N'Service Tax - R&B'
        UNION ALL SELECT 28, N'', N'Service Tax', N'Service Tax - ICU'
        UNION ALL SELECT 29, N'', N'Service Tax', N'Service Tax - Lodger'
    ) n
    LEFT JOIN act ON act.Slot = n.Slot LEFT JOIN app ON app.Slot = n.Slot LEFT JOIN hos ON hos.Slot = n.Slot LEFT JOIN mem ON mem.Slot = n.Slot
    LEFT JOIN lim ON lim.Slot = n.Slot LEFT JOIN @days d ON d.Slot = n.Slot
    ORDER BY n.Slot;
END
GO

-- The saved summary figures of the worksheet (the rows under the benefit grid: co-payment, excess, limit exceeded, ex-gratia, fees, final
-- totals and the bordx amount, as getFinalTotal worked them out and SaveClaim stored them), added up over a range of versions.
CREATE OR ALTER PROCEDURE genisis.WorksheetEnquiry_Summary
    @cas nvarchar(15), @fromVersion int, @toVersion int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT SUM(ISNULL(CLMTotalActual, 0)) AS TotalActual, SUM(ISNULL(CLMTotalApproved, 0)) AS TotalApproved, SUM(ISNULL(CLMCoPayRB, 0)) AS CoPayRb,
        SUM(ISNULL(CLMExcess, 0)) AS TotalExcess, SUM(ISNULL(CLMLimitExceeded, 0)) AS LimitExceeded, SUM(ISNULL(CLMExGratiaAmt, 0)) AS ExGratia,
        SUM(ISNULL(CLMBTBGAmt, 0)) AS Btbg, SUM(ISNULL(CLMCaseMgmtFees, 0)) AS ProcessingFee, SUM(ISNULL(CLMMajorMedFee, 0)) AS MajorMedicalFee,
        SUM(ISNULL(CLMMainTUAmt, 0)) AS MainTopup, SUM(ISNULL(CLMHosPayColl, 0)) AS HospitalPayorToCollect,
        SUM(ISNULL(CLMPayableByMedix2H, 0)) AS PayableToHospital, SUM(ISNULL(CLMPayableByMedix2M, 0)) AS PayableToMember,
        SUM(ISNULL(CLMFinalApprove, 0)) AS FinalApprove, SUM(ISNULL(CLMBordxAmt, 0)) AS BordxAmount
    FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion BETWEEN @fromVersion AND @toVersion;
END
GO

-- ClaimOthers: payee address, note, attention, payment mode and the two remarks of one version.
CREATE OR ALTER PROCEDURE genisis.WorksheetEnquiry_Others
    @cas nvarchar(15), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(CLMWSRemarks) AS WorksheetRemarks, RTRIM(CLMDCNoteRemarks) AS CreditDebitRemarks, RTRIM(MBMAdd1) AS Address1, RTRIM(MBMAdd2) AS Address2,
        RTRIM(MBMAdd3) AS Address3, RTRIM(CLMNote) AS Note, RTRIM(MBMPostCode) AS PostCode, RTRIM(MBMCity) AS City, RTRIM(MBMState) AS State,
        RTRIM(CLMAttention) AS Attention, RTRIM(CLMPayMode) AS PaymentMode
    FROM genisis.WS_ClaimOthers WHERE CASNumber = @cas AND CLaimVersion = @version;
END
GO
