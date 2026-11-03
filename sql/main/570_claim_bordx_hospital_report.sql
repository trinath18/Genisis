-- Claim Bordx > Payment Advice > Hospital Report (legacy frmPayAdviceHosRpt, "Payment Advice- By Hospital"): the claims of a range of bordx, and for one bordx the payment advice documents of the MH (Malaysia
-- Airlines) payor: the payments to the panel hospitals (and, apart, to the MAS Wing group, the Sabah hospitals and the Sarawak hospitals), to the members, the recoveries from them, the staff claims and the daily
-- allowances, plus the cost allocation of the CC Report. The desktop called Print_Bordx and CLM_PayAdviceHOS with a WHERE fragment and filled Excel and Word templates; here every set is a procedure with typed
-- parameters. Read only.

-- PrintBordxListing of this form: the claims of the bordx in the range, as Print Bordx lists them (the same columns), with the criteria of this form. The desktop filtered the date of discharge on the date of
-- admission; the date of discharge is used here.
CREATE OR ALTER PROCEDURE genisis.HospitalReport_Search
    @bordxFrom nvarchar(40), @bordxTo nvarchar(40), @hospital nvarchar(100) = NULL, @doaFrom datetime = NULL, @doaTo datetime = NULL, @dodFrom datetime = NULL, @dodTo datetime = NULL,
    @bordxDateFrom datetime = NULL, @bordxDateTo datetime = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT TOP (@max) RTRIM(PLNCode) AS PlanCode, RTRIM(INSCode) AS InsCode, RTRIM(CASNumber) AS CaseNo, ClaimVersion, RTRIM(PatientName) AS PatientName, RTRIM(MBMNumber) AS MemberNo, RTRIM(HOSName) AS Hospital,
        CLMPayableByMedix2H AS PayableToHospital, CLMTotalActual + ISNULL(TotGST, 0) AS TotalActual, CLMTotalApproved AS TotalApproved, CLMPayableByMedix2M AS PayableToMember, RTRIM(DebitCreditRefNo) AS DebitCreditRefNo,
        RTRIM(BordxRefNo) AS BordxNo, BordxPrintedStatus AS Printed, RTRIM(HLTCode) AS HealthType, RTRIM(PAYCode) AS PayorCode, RTRIM(MBMBTBG) AS Btbg, RTRIM(INVPayTo) AS PayTo, CLMCaseMgmtFees AS CaseMgmtFees,
        TotalMgmtFee, BordxDate
    FROM genisis.WS_PrintBordx
    WHERE ClaimBordxStatus = 1 AND INVBordxed = 1 AND BordxRefNo >= @bordxFrom AND BordxRefNo <= @bordxTo
      AND (@hospital IS NULL OR HOSName = @hospital)
      AND (@doaFrom IS NULL OR CASDateAdmitted >= @doaFrom) AND (@doaTo IS NULL OR CASDateAdmitted <= @doaTo)
      AND (@dodFrom IS NULL OR CASDischargeDate >= @dodFrom) AND (@dodTo IS NULL OR CASDischargeDate <= @dodTo)
      AND (@bordxDateFrom IS NULL OR BordxDate >= @bordxDateFrom) AND (@bordxDateTo IS NULL OR BordxDate <= @bordxDateTo)
    ORDER BY BordxNo, CaseNo, ClaimVersion
    OPTION (RECOMPILE);
END
GO

-- CLM_PayAdviceHOS case 2: the payments to the hospitals of one bordx, by hospital. @section: panel (not the MAS Wing group, paid to the hospital, not in Sabah or Sarawak), maswing (the MAS Wing group, paid to the
-- hospital), sabah (the AWUS report: Sabah hospitals) or sarawak. The hospital's state is not null for panel (the desktop's "<> Sabah And <> Sarawak" drops a hospital with no state).
CREATE OR ALTER PROCEDURE genisis.HospitalReport_Hospitals
    @bordxNo nvarchar(40), @section varchar(10)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(HOSCode) AS HospitalCode, RTRIM(HOSName) AS Hospital, COUNT(CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActual, SUM(CLMPayableByMedix2H) AS PayableToHospital
    FROM genisis.WS_PaymentAdviceHos
    WHERE BordxRefNo = @bordxNo AND INVPayTo = 'H'
      AND ((@section = 'panel' AND MBMGroupCategory <> 'MASWING SDN BHD' AND HOSState <> 'Sabah' AND HOSState <> 'Sarawak')
        OR (@section = 'maswing' AND MBMGroupCategory = 'MASWING SDN BHD')
        OR (@section = 'sabah' AND MBMGroupCategory <> 'MASWING SDN BHD' AND HOSState = 'Sabah')
        OR (@section = 'sarawak' AND MBMGroupCategory <> 'MASWING SDN BHD' AND HOSState = 'Sarawak'))
    GROUP BY HOSCode, HOSName ORDER BY HOSName
    OPTION (RECOMPILE);
END
GO

-- The claims behind each hospital of the same sets, with the hospital's payee (HOS.HOSPayee) the cheque is made out to.
CREATE OR ALTER PROCEDURE genisis.HospitalReport_Claims
    @bordxNo nvarchar(40), @section varchar(10)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(v.HOSCode) AS HospitalCode, RTRIM(v.HOSName) AS Hospital, RTRIM(h.HOSPayee) AS Payee, RTRIM(v.MBMEmployeeNo) AS EmployeeNo, RTRIM(v.MBMName) AS MemberName, v.CASDischargeDate AS DischargeDate,
        RTRIM(v.InvNo) AS InvoiceNo, RTRIM(v.CASNumber) AS CaseNo, v.ClaimVersion, v.CLMTotalActual AS TotalActual, v.CLMPayableByMedix2H AS PayableToHospital
    FROM genisis.WS_PaymentAdviceHos v
    LEFT JOIN dbo.HOS h ON h.HOSCode = v.HOSCode
    WHERE v.BordxRefNo = @bordxNo AND v.INVPayTo = 'H'
      AND ((@section = 'panel' AND v.MBMGroupCategory <> 'MASWING SDN BHD' AND v.HOSState <> 'Sabah' AND v.HOSState <> 'Sarawak')
        OR (@section = 'maswing' AND v.MBMGroupCategory = 'MASWING SDN BHD')
        OR (@section = 'sabah' AND v.MBMGroupCategory <> 'MASWING SDN BHD' AND v.HOSState = 'Sabah')
        OR (@section = 'sarawak' AND v.MBMGroupCategory <> 'MASWING SDN BHD' AND v.HOSState = 'Sarawak'))
    ORDER BY v.HOSName, v.MBMEmployeeNo, v.CASNumber, v.ClaimVersion
    OPTION (RECOMPILE);
END
GO

-- CLM_PayAdviceHOS case 5 (View_Paymt_AdviceMBM): the amounts paid to the members of one bordx, with the tax column the staff claim and daily allowance reports split on.
CREATE OR ALTER PROCEDURE genisis.HospitalReport_Members
    @bordxNo nvarchar(40)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(MBMEmployeeNo) AS EmployeeNo, RTRIM(MBMName) AS MemberName, RTRIM(MBMEmail) AS Email, RTRIM(MBMIcBcPp) AS IcNo, RTRIM(MBMCostCenter) AS CostCenter, RTRIM(CASNumber) AS CaseNo, ClaimVersion,
        CLMPayableByMedix2M AS PayableToMember, RTRIM(CLMTax) AS Tax, CLMBordxAmt AS BordxAmount
    FROM genisis.WS_PaymentAdviceMbm
    WHERE BordxRefNo = @bordxNo
    ORDER BY MBMEmployeeNo, CASNumber, ClaimVersion
    OPTION (RECOMPILE);
END
GO

-- CLM_PayAdviceHOS case 6 (View_MAS_MBMRecovery): the amounts recovered from the members of one bordx; the patient is the member, or the covered person of the claim.
CREATE OR ALTER PROCEDURE genisis.HospitalReport_Recovery
    @bordxNo nvarchar(40)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(r.MBMEmployeeNo) AS EmployeeNo, RTRIM(r.MBMName) AS MemberName,
        RTRIM(CASE WHEN r.MBMPatientCovID = N'00' THEN r.MBMName ELSE ISNULL((SELECT TOP (1) c.MBMCName FROM dbo.MBMCoveredPersons c WHERE c.MBMCNumber = r.MBMNumber AND c.MBMCCoverID = r.MBMPatientCovID), r.MBMName) END) AS PatientName,
        RTRIM(r.CASNumber) AS CaseNo, r.ClaimVersion, RTRIM(r.HOSName) AS Hospital, r.CASDischargeDate AS DischargeDate, r.CLMPayableByMedix2M AS PayableToMember, r.CLMBTBGAmt AS BtbgAmount, RTRIM(r.MBMCostCenter) AS CostCenter
    FROM genisis.WS_MasRecovery r
    WHERE r.BordxRefNo = @bordxNo
    ORDER BY r.MBMEmployeeNo, r.CASNumber, r.ClaimVersion
    OPTION (RECOMPILE);
END
GO

-- CC Report, case 3: the cases and amount by cost centre (View_MAS_Rpt_Others) of the bordx in the range. The desktop passed the hospital, admission and discharge dates of the form to these views, which have
-- no such columns, so only the bordx number and bordx date criteria are used.
CREATE OR ALTER PROCEDURE genisis.HospitalReport_CostCentres
    @bordxFrom nvarchar(40) = NULL, @bordxTo nvarchar(40) = NULL, @bordxDateFrom datetime = NULL, @bordxDateTo datetime = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(CASNumber) AS TotalCases, RTRIM(MBMCostCenter) AS CostCenter, SUM(TotalAmt) AS TotalAmount
    FROM genisis.WS_MasRptOthers
    WHERE (@bordxFrom IS NULL OR BordxRefNo >= @bordxFrom) AND (@bordxTo IS NULL OR BordxRefNo <= @bordxTo) AND (@bordxDateFrom IS NULL OR BordxDate >= @bordxDateFrom) AND (@bordxDateTo IS NULL OR BordxDate <= @bordxDateTo)
    GROUP BY MBMCostCenter ORDER BY MBMCostCenter
    OPTION (RECOMPILE);
END
GO

-- CC Report, case 4: the amount by group category (View_MAS_Rpt_MASWing).
CREATE OR ALTER PROCEDURE genisis.HospitalReport_Groups
    @bordxFrom nvarchar(40) = NULL, @bordxTo nvarchar(40) = NULL, @bordxDateFrom datetime = NULL, @bordxDateTo datetime = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(MBMGroupCategory) AS GroupCategory, SUM(CLMBordxAmt) AS TotalAmount
    FROM genisis.WS_MasRptMasWing
    WHERE (@bordxFrom IS NULL OR BordxRefNo >= @bordxFrom) AND (@bordxTo IS NULL OR BordxRefNo <= @bordxTo) AND (@bordxDateFrom IS NULL OR BordxDate >= @bordxDateFrom) AND (@bordxDateTo IS NULL OR BordxDate <= @bordxDateTo)
    GROUP BY MBMGroupCategory
    OPTION (RECOMPILE);
END
GO

-- CC Report, cases 7 and 8: the amount by employee, in cost centre order (View_MAS_Rpt_Others), and by employee of the MAS Wing groups (View_MAS_Rpt_MASWing_Det).
CREATE OR ALTER PROCEDURE genisis.HospitalReport_Expenses
    @bordxFrom nvarchar(40) = NULL, @bordxTo nvarchar(40) = NULL, @bordxDateFrom datetime = NULL, @bordxDateTo datetime = NULL, @maswing bit = 0
AS
BEGIN
    SET NOCOUNT ON;
    IF @maswing = 0
        SELECT RTRIM(MBMCostCenter) AS CostCenter, RTRIM(MBMEmployeeNo) AS EmployeeNo, RTRIM(MBMName) AS MemberName, RTRIM(MBMIcBcPp) AS IcNo, TotalAmt AS TotalAmount
        FROM genisis.WS_MasRptOthers
        WHERE (@bordxFrom IS NULL OR BordxRefNo >= @bordxFrom) AND (@bordxTo IS NULL OR BordxRefNo <= @bordxTo) AND (@bordxDateFrom IS NULL OR BordxDate >= @bordxDateFrom) AND (@bordxDateTo IS NULL OR BordxDate <= @bordxDateTo)
        ORDER BY MBMCostCenter, MBMName OPTION (RECOMPILE);
    ELSE
        SELECT RTRIM(MBMGroupCategory) AS CostCenter, RTRIM(MBMEmployeeNo) AS EmployeeNo, RTRIM(MBMName) AS MemberName, RTRIM(MBMIcBcPp) AS IcNo, TotalAmt AS TotalAmount
        FROM genisis.WS_MasRptMasWingDet
        WHERE (@bordxFrom IS NULL OR BordxRefNo >= @bordxFrom) AND (@bordxTo IS NULL OR BordxRefNo <= @bordxTo) AND (@bordxDateFrom IS NULL OR BordxDate >= @bordxDateFrom) AND (@bordxDateTo IS NULL OR BordxDate <= @bordxDateTo)
        ORDER BY MBMGroupCategory, MBMName OPTION (RECOMPILE);
END
GO
