-- Claim Bordx > Print Recovery (legacy frmGenerateRecovery, "Generate Recovery Listing"): the recoveries of a BTBG client (a group company): worksheets whose amount payable by the member
-- (CLMPayableByMedix2M) is negative, listed from the claim report view (VIEW_NEWCLMReport), for the recovery listing the client is sent. The desktop called ClaimReport1 with case 234 and
-- a WHERE fragment, twice (once for the list, once for the Excel sheet); here there is one procedure with typed parameters. Read only. A blank criterion is not applied; dates compare with the
-- stored datetime exactly as the desktop's 'mm/dd/yyyy' text did.

-- combolist: the group companies that are BTBG clients and the report layout each is sent (BTBGRpt: 1 the standard Recovery Listing, 2 the MDEC one).
CREATE OR ALTER PROCEDURE genisis.PrintRecovery_Companies
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(CoGRPName) AS Name, RTRIM(BTBGRpt) AS ReportType FROM dbo.CompanyGrp WHERE BTBGClient = 'Y' ORDER BY CoGRPName;
END
GO

-- SearchRecords + PrintBordxListing + the rows PrintTemplate wrote into the Excel template (both layouts use the same columns of the view). Asked for all at once, the claim report view takes
-- about 40 seconds for one company (the desktop waited up to 300), because the diagnosis columns make the optimiser join the whole case report for every claim. The rows are taken first
-- (about a second), then each one's diagnoses are looked up by case number.
CREATE OR ALTER PROCEDURE genisis.PrintRecovery_Search
    @company nvarchar(100), @bordxFrom nvarchar(40) = NULL, @bordxTo nvarchar(40) = NULL, @bordxDateFrom datetime = NULL, @bordxDateTo datetime = NULL,
    @openFrom datetime = NULL, @openTo datetime = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) CASNumber, ClaimVersion, PatientName, ICNo, MBMNumber, MBMName, MBMIcBcPp, MBMEmployeeNo, MBMDeptCode, MBMDepartment, MBMCostCenter, PLNCode, CASDateAdmitted, CASDischargeDate,
        ClaimOpenDate, InvDate, HOSName, DebitCreditRefNo, CLMBordxAmt, CLMPayableByMedix2M, CLMTotalActual, BordxRefNo, PAYCompanyName, MBMPolicyNo, MBMPayorEffDate, MBMPayorExpDate
    INTO #recovery
    FROM genisis.WS_ClaimReport
    WHERE CLMPayableByMedix2M < 0 AND MBMGroupCompany = @company
      AND (@bordxFrom IS NULL OR BordxRefNo >= @bordxFrom) AND (@bordxTo IS NULL OR BordxRefNo <= @bordxTo)
      AND (@bordxDateFrom IS NULL OR BordxDate >= @bordxDateFrom) AND (@bordxDateTo IS NULL OR BordxDate <= @bordxDateTo)
      AND (@openFrom IS NULL OR ClaimOpenDate >= @openFrom) AND (@openTo IS NULL OR ClaimOpenDate <= @openTo)
    OPTION (RECOMPILE);

    SELECT RTRIM(r.CASNumber) AS CaseNo, r.ClaimVersion, RTRIM(r.PatientName) AS PatientName, RTRIM(r.ICNo) AS PatientIc, RTRIM(r.MBMNumber) AS MemberNo, RTRIM(r.MBMName) AS MemberName,
        RTRIM(r.MBMIcBcPp) AS MemberIc, RTRIM(r.MBMEmployeeNo) AS EmployeeNo, RTRIM(r.MBMDeptCode) AS DeptCode, RTRIM(r.MBMDepartment) AS Department, RTRIM(r.MBMCostCenter) AS CostCenter,
        RTRIM(r.PLNCode) AS PlanCode, r.CASDateAdmitted AS DateAdmitted, r.CASDischargeDate AS DateDischarged, r.ClaimOpenDate AS ClaimOpenDate, r.InvDate AS InvoiceDate, RTRIM(r.HOSName) AS Hospital,
        RTRIM(d.DIAFinalDiag1) AS Diagnosis1, RTRIM(d.DIAFinalDiag2) AS Diagnosis2, RTRIM(d.DIAFinalDiag3) AS Diagnosis3, RTRIM(r.DebitCreditRefNo) AS DebitNoteNo,
        r.CLMBordxAmt AS BordxAmount, r.CLMPayableByMedix2M AS Recovery, r.CLMTotalActual AS ActualAmount, RTRIM(r.BordxRefNo) AS BordxNo,
        RTRIM(r.PAYCompanyName) AS PayorName, RTRIM(r.MBMPolicyNo) AS PolicyNo, r.MBMPayorEffDate AS PayorEffDate, r.MBMPayorExpDate AS PayorExpDate
    FROM #recovery r
    OUTER APPLY (SELECT TOP (1) c.DIAFinalDiag1, c.DIAFinalDiag2, c.DIAFinalDiag3 FROM genisis.WS_ClaimReportCas c WHERE c.CASNumber = r.CASNumber) d
    ORDER BY r.CASNumber, r.ClaimVersion;
END
GO
