-- Claim Bordx > Print Bordx (legacy FrmPrintBordx, "Print Bordx"): the claims of a range of bordx, and the bordx as a report for the payor. The desktop called Print_Bordx with a WHERE fragment, copied the
-- claims into TempBordxSummary and printed one of a dozen Crystal reports (by single or multiple claims, Sihat or not, BTBG, float plan, amount); here the claims are read with typed parameters and the report
-- is a file the screen writes from them. Printing the detail report marks the bordx as printed.

-- PrintBordxListing: the claims of the bordx in the range (View_PrintBordx; closed and bordxed worksheets only). The bill amount includes the GST, as the proc added it.
CREATE OR ALTER PROCEDURE genisis.PrintBordx_Search
    @bordxFrom nvarchar(40), @bordxTo nvarchar(40), @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT TOP (@max) RTRIM(PLNCode) AS PlanCode, RTRIM(INSCode) AS InsCode, RTRIM(CASNumber) AS CaseNo, ClaimVersion, RTRIM(PatientName) AS PatientName, RTRIM(MBMNumber) AS MemberNo, RTRIM(HOSName) AS Hospital,
        CLMPayableByMedix2H AS PayableToHospital, CLMTotalActual + ISNULL(TotGST, 0) AS TotalActual, CLMTotalApproved AS TotalApproved, CLMPayableByMedix2M AS PayableToMember, RTRIM(DebitCreditRefNo) AS DebitCreditRefNo,
        RTRIM(BordxRefNo) AS BordxNo, BordxPrintedStatus AS Printed, RTRIM(HLTCode) AS HealthType, RTRIM(PAYCode) AS PayorCode, RTRIM(MBMBTBG) AS Btbg, RTRIM(INVPayTo) AS PayTo, CLMCaseMgmtFees AS CaseMgmtFees,
        TotalMgmtFee, BordxDate
    FROM genisis.WS_PrintBordx
    WHERE ClaimBordxStatus = 1 AND INVBordxed = 1 AND (BordxRefNo >= @bordxFrom AND BordxRefNo <= @bordxTo)
    ORDER BY BordxNo, CaseNo, ClaimVersion
    OPTION (RECOMPILE);
END
GO

-- The report's data (VIEW_PrintClaimBordx_06, what the Crystal reports read): every claim of the bordx in the range with its member, plan, hospital, diagnoses and amounts.
CREATE OR ALTER PROCEDURE genisis.PrintBordx_Report
    @bordxFrom nvarchar(40), @bordxTo nvarchar(40), @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT TOP (@max) RTRIM(BordxRefNo) AS BordxNo, BordxDate, RTRIM(CASNumber) AS CaseNo, ClaimVersion,
        RTRIM(CASE WHEN MBMPatientCovID = N'00' THEN MBMName ELSE MBMCName END) AS PatientName, RTRIM(MBMNumber) AS MemberNo, RTRIM(MBMPolicyNo) AS PolicyNo, RTRIM(MBMIcBcPp) AS MemberIc,
        RTRIM(CASE WHEN MBMPatientCovID = N'00' THEN MBMIcBcPp ELSE MBMCICBCPPNo END) AS PatientIc, RTRIM(CASHosName) AS Hospital, RTRIM(PLNCode) AS PlanCode, RTRIM(INSCode) AS InsCode, RTRIM(HLTCode) AS HealthType,
        RTRIM(PAYCode) AS PayorCode, RTRIM(GRPCompany) AS GroupCompany, RTRIM(MBMDepartment) AS Department, RTRIM(MBMEmployeeNo) AS EmployeeNo, RTRIM(MBMCostCenter) AS CostCenter, CASDateAdmitted AS DateAdmitted,
        CASDischargeDate AS DateDischarged, RTRIM(ICDCode1) AS IcdCode1, RTRIM(ICDDesc1) AS IcdDesc1, RTRIM(ICDCode2) AS IcdCode2, RTRIM(ICDDesc2) AS IcdDesc2, RTRIM(ICDCode3) AS IcdCode3, RTRIM(ICDDesc3) AS IcdDesc3,
        CLMTotalActual AS TotalActual, CLMTotalApproved AS TotalApproved, CLMPayableByMedix2H AS PayableToHospital, CLMPayableByMedix2M AS PayableToMember, CLMCaseMgmtFees AS CaseMgmtFees, TotalMgmtFee,
        CLMBordxAmt AS BordxAmount, RTRIM(DebitCreditRefNo) AS DebitCreditRefNo, RTRIM(INVPayTo) AS PayTo, RTRIM(MBMBTBG) AS Btbg, BordxPrintedStatus AS Printed, RTRIM(CLMWSRemarks) AS Remarks
    FROM genisis.WS_PrintClaimBordx
    WHERE BordxRefNo >= @bordxFrom AND BordxRefNo <= @bordxTo
    ORDER BY BordxNo, CaseNo, ClaimVersion
    OPTION (RECOMPILE);
END
GO

-- GetPayorInfo + GetProdtName: the payor's name and address, and the product of the plan (Sihat for a Sihat payor, the plan's product category for the others; SH when the plan has none).
CREATE OR ALTER PROCEDURE genisis.PrintBordx_PayorInfo
    @payor nvarchar(4), @plan nvarchar(20) = NULL, @health char(1) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @category nvarchar(4) = N'SH';
    IF ISNULL(@health, N'') <> N'S'
        SELECT TOP (1) @category = RTRIM(PLNProCat) FROM dbo.PLN WHERE PLNCode = @plan AND PAYCode = @payor AND RTRIM(ISNULL(PLNProCat, N'')) <> N'';
    SELECT RTRIM(p.PAYCode) AS Code, RTRIM(p.PAYCompanyName) AS Name, RTRIM(p.PAYAddress1) AS Address1, RTRIM(p.PAYAddress2) AS Address2, RTRIM(p.PAYAddress3) AS Address3,
        (SELECT TOP (1) RTRIM(ProductName) FROM genisis.Maint_ProductCategory WHERE ProductCode = @category) AS Product
    FROM dbo.PAY p WHERE p.PAYCode = @payor;
END
GO

-- Print Detail: the bordx of the range are marked as printed (printed date, status and user), their worksheets and invoices are marked CLMPending / CLMInvPending B. Returns the bordx, worksheets and
-- invoices changed.
CREATE OR ALTER PROCEDURE genisis.PrintBordx_Mark
    @bordxFrom nvarchar(40), @bordxTo nvarchar(40), @userCode nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @bordx int, @claims int, @invoices int;
    UPDATE genisis.WS_Bordx SET BordxPrintedDate = CAST(CAST(GETDATE() AS date) AS datetime), BordxPrintedStatus = 1, BordxPrintedBy = @userCode WHERE BordxRefNo >= @bordxFrom AND BordxRefNo <= @bordxTo;
    SET @bordx = @@ROWCOUNT;
    UPDATE genisis.WS_Claim SET CLMPending = 'B' WHERE BordxRefNo >= @bordxFrom AND BordxRefNo <= @bordxTo;
    SET @claims = @@ROWCOUNT;
    UPDATE genisis.WS_ClaimInvoice SET CLMInvPending = 'B' WHERE INVBordxRefNo >= @bordxFrom AND INVBordxRefNo <= @bordxTo;
    SET @invoices = @@ROWCOUNT;
    SELECT @bordx AS Bordx, @claims AS Claims, @invoices AS Invoices;
END
GO
