-- Invoice > Invoice Tracking (legacy FrmInvoiceTracking2). The desktop built the WHERE clause as text (StrAppend) and sent it either to the view VIEW_Invoice_Tracking (the
-- detail listing) or to dbo.InvoiceTracking in HISWorkSheet, which ran it as dynamic SQL (the four summaries); here every criterion is a typed parameter. Read out of the desktop:
--  * The detail listing reads VIEW_Invoice_Tracking as it is, FULL OUTER JOIN and all: it also lists the cases that have no invoice (5,945 of 97,711 rows), which is what the
--    "... is empty" ticks of the form are for.
--  * The four summaries joined worksheets to invoices by case number only, so every invoice was counted with every worksheet version of its case: January 2021 gave an invoice
--    amount of 27,309,937 where the invoices add up to 2,028,764 (and 15,177 "cases" for 2,337 worksheets). They are worked out per worksheet version here: an invoice is counted once,
--    and a worksheet's payable amounts once however many invoices it has. Their criteria also named columns more than one table has (CASNumber, PAYCode), which failed in the desktop.
--  * "Summary (By Hospitals)" asked the procedure for its case 4 and read month columns that case does not return; it gives the layout the form declares for it (hospital, payable
--    to the hospital, payable to the member, invoice amount; the number of worksheets is added).
--  * Stay type "4 - Follow-up & Pre-Hospitalisation" built "AND (INVStayType = 'P' OR INVStayType = 'F'" without its closing bracket and failed; it works here (@stayType = 'PF').
--  * The criteria of the combo boxes the form shows but never read (claimability, insured type, A/C period) are not offered.

-- The invoices and the worksheet each belongs to, with the criteria applied. "From"/"to" pairs are inclusive; an "empty" flag keeps only rows where the column is NULL or blank
-- (a date column is blank at 1900-01-01, as the desktop's  = ''  test saw it).
CREATE OR ALTER FUNCTION genisis.InvoiceTracking_Filtered (
    @invNoFrom nvarchar(100), @invNoTo nvarchar(100), @invNoEmpty bit, @stayType nvarchar(2), @payType char(1),
    @versionFrom int, @versionTo int, @versionEmpty bit, @payTo char(1), @payToEmpty bit,
    @bordxFrom nvarchar(40), @bordxTo nvarchar(40), @bordxEmpty bit,
    @invDateFrom datetime, @invDateTo datetime, @invDateEmpty bit, @regFrom datetime, @regTo datetime, @regEmpty bit,
    @hospital nvarchar(20), @groupCompany nvarchar(300), @groupCompanyEmpty bit,
    @amountFrom money, @amountTo money, @amountEmpty bit, @payor nvarchar(4), @payorEmpty bit,
    @wksFrom nvarchar(30), @wksTo nvarchar(30), @wksEmpty bit,
    @doaFrom datetime, @doaTo datetime, @doaEmpty bit, @dodFrom datetime, @dodTo datetime, @dodEmpty bit,
    @bordxDateFrom datetime, @bordxDateTo datetime, @bordxDateEmpty bit)
RETURNS TABLE
AS RETURN
(
    SELECT v.CASNumber, v.INVWSversion, v.InvNo, v.InvDate, v.INVAmount, v.INVPayTo, v.INVPayee, v.INVStayType, v.INVPAYType, v.INVEnteredDate, v.INVEnteredBy,
           v.INVBordxRefNo, v.BordxDate, v.MBMGroupCompany, v.PAYCode, v.HOSName, v.CASDateAdmitted, v.CASDischargeDate, v.CaseRegDate,
           cl.CLMPayableByMedix2H, cl.CLMPayableByMedix2M, cs.CASHOSCode
    FROM genisis.WS_InvoiceTracking v
    LEFT JOIN genisis.WS_Claim cl ON cl.CASNumber = v.CASNumber AND CAST(cl.ClaimVersion AS nvarchar(10)) = v.INVWSversion
    LEFT JOIN dbo.CAS cs ON cs.CASNumber = v.CASNumber
    WHERE (@invNoFrom IS NULL OR v.InvNo >= @invNoFrom) AND (@invNoTo IS NULL OR v.InvNo <= @invNoTo)
      AND (@invNoEmpty = 0 OR v.InvNo IS NULL OR v.InvNo = '')
      AND (@stayType IS NULL OR (@stayType = 'PF' AND v.INVStayType IN ('P', 'F')) OR v.INVStayType = @stayType)
      AND (@payType IS NULL OR v.INVPAYType = @payType)
      AND (@versionFrom IS NULL OR (v.INVWSversion NOT LIKE '%[^0-9]%' AND v.INVWSversion <> '' AND CAST(v.INVWSversion AS int) >= @versionFrom))
      AND (@versionTo IS NULL OR (v.INVWSversion NOT LIKE '%[^0-9]%' AND v.INVWSversion <> '' AND CAST(v.INVWSversion AS int) <= @versionTo))
      AND (@versionEmpty = 0 OR v.INVWSversion IS NULL OR v.INVWSversion = '')
      AND (@payTo IS NULL OR v.INVPayTo = @payTo) AND (@payToEmpty = 0 OR v.INVPayTo IS NULL OR v.INVPayTo = '')
      AND (@bordxFrom IS NULL OR v.INVBordxRefNo >= @bordxFrom) AND (@bordxTo IS NULL OR v.INVBordxRefNo <= @bordxTo)
      AND (@bordxEmpty = 0 OR v.INVBordxRefNo IS NULL OR v.INVBordxRefNo = '')
      AND (@invDateFrom IS NULL OR v.InvDate >= @invDateFrom) AND (@invDateTo IS NULL OR v.InvDate < DATEADD(DAY, 1, @invDateTo))
      AND (@invDateEmpty = 0 OR v.InvDate IS NULL OR v.InvDate = '19000101')
      AND (@regFrom IS NULL OR v.INVEnteredDate >= @regFrom) AND (@regTo IS NULL OR v.INVEnteredDate < DATEADD(DAY, 1, @regTo))
      AND (@regEmpty = 0 OR v.INVEnteredDate IS NULL OR v.INVEnteredDate = '19000101')
      AND (@hospital IS NULL OR cs.CASHOSCode = @hospital)
      AND (@groupCompany IS NULL OR v.MBMGroupCompany = @groupCompany) AND (@groupCompanyEmpty = 0 OR v.MBMGroupCompany IS NULL OR v.MBMGroupCompany = '')
      AND (@amountFrom IS NULL OR v.INVAmount >= @amountFrom) AND (@amountTo IS NULL OR v.INVAmount <= @amountTo) AND (@amountEmpty = 0 OR v.INVAmount IS NULL)
      AND (@payor IS NULL OR v.PAYCode = @payor) AND (@payorEmpty = 0 OR v.PAYCode IS NULL OR v.PAYCode = '')
      AND (@wksFrom IS NULL OR v.CASNumber >= @wksFrom) AND (@wksTo IS NULL OR v.CASNumber <= @wksTo) AND (@wksEmpty = 0 OR v.CASNumber IS NULL OR v.CASNumber = '')
      AND (@doaFrom IS NULL OR v.CASDateAdmitted >= @doaFrom) AND (@doaTo IS NULL OR v.CASDateAdmitted < DATEADD(DAY, 1, @doaTo))
      AND (@doaEmpty = 0 OR v.CASDateAdmitted IS NULL OR v.CASDateAdmitted = '19000101')
      AND (@dodFrom IS NULL OR v.CASDischargeDate >= @dodFrom) AND (@dodTo IS NULL OR v.CASDischargeDate < DATEADD(DAY, 1, @dodTo))
      AND (@dodEmpty = 0 OR v.CASDischargeDate IS NULL OR v.CASDischargeDate = '19000101')
      AND (@bordxDateFrom IS NULL OR v.BordxDate >= @bordxDateFrom) AND (@bordxDateTo IS NULL OR v.BordxDate < DATEADD(DAY, 1, @bordxDateTo))
      AND (@bordxDateEmpty = 0 OR v.BordxDate IS NULL OR v.BordxDate = '19000101')
)
GO

-- InvTrackListingCLM, report "3 - General Listing(Detail)": one row per invoice (and per case without one).
CREATE OR ALTER PROCEDURE genisis.InvoiceTracking_Detail
    @invNoFrom nvarchar(100) = NULL, @invNoTo nvarchar(100) = NULL, @invNoEmpty bit = 0, @stayType nvarchar(2) = NULL, @payType char(1) = NULL,
    @versionFrom int = NULL, @versionTo int = NULL, @versionEmpty bit = 0, @payTo char(1) = NULL, @payToEmpty bit = 0,
    @bordxFrom nvarchar(40) = NULL, @bordxTo nvarchar(40) = NULL, @bordxEmpty bit = 0,
    @invDateFrom datetime = NULL, @invDateTo datetime = NULL, @invDateEmpty bit = 0, @regFrom datetime = NULL, @regTo datetime = NULL, @regEmpty bit = 0,
    @hospital nvarchar(20) = NULL, @groupCompany nvarchar(300) = NULL, @groupCompanyEmpty bit = 0,
    @amountFrom money = NULL, @amountTo money = NULL, @amountEmpty bit = 0, @payor nvarchar(4) = NULL, @payorEmpty bit = 0,
    @wksFrom nvarchar(30) = NULL, @wksTo nvarchar(30) = NULL, @wksEmpty bit = 0,
    @doaFrom datetime = NULL, @doaTo datetime = NULL, @doaEmpty bit = 0, @dodFrom datetime = NULL, @dodTo datetime = NULL, @dodEmpty bit = 0,
    @bordxDateFrom datetime = NULL, @bordxDateTo datetime = NULL, @bordxDateEmpty bit = 0, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(f.CASNumber) AS CaseNo, RTRIM(f.INVWSversion) AS Version, RTRIM(f.INVBordxRefNo) AS BordxNo, NULLIF(f.BordxDate, '19000101') AS BordxDate,
           RTRIM(f.InvNo) AS InvoiceNo, NULLIF(f.InvDate, '19000101') AS InvoiceDate, f.INVAmount AS InvoiceAmount, NULLIF(f.CaseRegDate, '19000101') AS RegDate,
           NULLIF(f.CASDateAdmitted, '19000101') AS Doa, NULLIF(f.CASDischargeDate, '19000101') AS Dod, RTRIM(f.INVPayee) AS Payee, RTRIM(f.MBMGroupCompany) AS GroupCompany,
           NULLIF(f.INVEnteredDate, '19000101') AS InvoiceRegistered, RTRIM(f.INVEnteredBy) AS EnteredBy, RTRIM(f.INVStayType) AS StayType, RTRIM(f.INVPAYType) AS PayType
    FROM genisis.InvoiceTracking_Filtered(@invNoFrom, @invNoTo, @invNoEmpty, @stayType, @payType, @versionFrom, @versionTo, @versionEmpty, @payTo, @payToEmpty, @bordxFrom, @bordxTo, @bordxEmpty,
        @invDateFrom, @invDateTo, @invDateEmpty, @regFrom, @regTo, @regEmpty, @hospital, @groupCompany, @groupCompanyEmpty, @amountFrom, @amountTo, @amountEmpty, @payor, @payorEmpty,
        @wksFrom, @wksTo, @wksEmpty, @doaFrom, @doaTo, @doaEmpty, @dodFrom, @dodTo, @dodEmpty, @bordxDateFrom, @bordxDateTo, @bordxDateEmpty) f
    ORDER BY f.CASNumber, f.INVWSversion, f.InvNo;
END
GO

-- InvByMthCLM (@report 0), InvByMthHospCLM (1), InvGenListingCLM (2) and InvByHosp (4): the invoices, counted once each and per worksheet version. The columns a report does not
-- have are NULL. 0: by invoice month; 1: by hospital and invoice month; 2: by hospital and invoice; 4: by hospital. Only invoices are counted (not the cases without one).
CREATE OR ALTER PROCEDURE genisis.InvoiceTracking_Summary
    @report int,
    @invNoFrom nvarchar(100) = NULL, @invNoTo nvarchar(100) = NULL, @invNoEmpty bit = 0, @stayType nvarchar(2) = NULL, @payType char(1) = NULL,
    @versionFrom int = NULL, @versionTo int = NULL, @versionEmpty bit = 0, @payTo char(1) = NULL, @payToEmpty bit = 0,
    @bordxFrom nvarchar(40) = NULL, @bordxTo nvarchar(40) = NULL, @bordxEmpty bit = 0,
    @invDateFrom datetime = NULL, @invDateTo datetime = NULL, @invDateEmpty bit = 0, @regFrom datetime = NULL, @regTo datetime = NULL, @regEmpty bit = 0,
    @hospital nvarchar(20) = NULL, @groupCompany nvarchar(300) = NULL, @groupCompanyEmpty bit = 0,
    @amountFrom money = NULL, @amountTo money = NULL, @amountEmpty bit = 0, @payor nvarchar(4) = NULL, @payorEmpty bit = 0,
    @wksFrom nvarchar(30) = NULL, @wksTo nvarchar(30) = NULL, @wksEmpty bit = 0,
    @doaFrom datetime = NULL, @doaTo datetime = NULL, @doaEmpty bit = 0, @dodFrom datetime = NULL, @dodTo datetime = NULL, @dodEmpty bit = 0,
    @bordxDateFrom datetime = NULL, @bordxDateTo datetime = NULL, @bordxDateEmpty bit = 0, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    WITH invoices AS (
        SELECT f.CASNumber, f.INVWSversion, f.InvNo, f.InvDate, f.INVAmount, f.CLMPayableByMedix2H, f.CLMPayableByMedix2M, RTRIM(f.CASHOSCode) AS HospitalCode, RTRIM(f.HOSName) AS Hospital,
               YEAR(f.InvDate) AS InvYear, MONTH(f.InvDate) AS InvMonth,
               CASE WHEN @report = 0 THEN 0 ELSE 1 END AS WithHospital,
               ROW_NUMBER() OVER (PARTITION BY
                   CASE WHEN @report IN (0, 1) THEN YEAR(f.InvDate) END, CASE WHEN @report IN (0, 1) THEN MONTH(f.InvDate) END,
                   CASE WHEN @report <> 0 THEN f.CASHOSCode END, CASE WHEN @report <> 0 THEN f.HOSName END, CASE WHEN @report = 2 THEN f.InvNo END, CASE WHEN @report = 2 THEN f.InvDate END,
                   f.CASNumber, f.INVWSversion ORDER BY f.InvNo) AS FirstOfWorksheet
        FROM genisis.InvoiceTracking_Filtered(@invNoFrom, @invNoTo, @invNoEmpty, @stayType, @payType, @versionFrom, @versionTo, @versionEmpty, @payTo, @payToEmpty, @bordxFrom, @bordxTo, @bordxEmpty,
            @invDateFrom, @invDateTo, @invDateEmpty, @regFrom, @regTo, @regEmpty, @hospital, @groupCompany, @groupCompanyEmpty, @amountFrom, @amountTo, @amountEmpty, @payor, @payorEmpty,
            @wksFrom, @wksTo, @wksEmpty, @doaFrom, @doaTo, @doaEmpty, @dodFrom, @dodTo, @dodEmpty, @bordxDateFrom, @bordxDateTo, @bordxDateEmpty) f
        WHERE f.InvNo IS NOT NULL AND f.CASNumber IS NOT NULL
    )
    SELECT TOP (@max)
           CASE WHEN @report <> 0 THEN HospitalCode END AS HospitalCode, CASE WHEN @report <> 0 THEN Hospital END AS Hospital,
           CASE WHEN @report = 2 THEN RTRIM(InvNo) END AS InvoiceNo, CASE WHEN @report = 2 THEN InvDate END AS InvoiceDate,
           CASE WHEN @report IN (0, 1) THEN InvYear END AS InvYear, CASE WHEN @report IN (0, 1) THEN InvMonth END AS InvMonth,
           COUNT(DISTINCT CASNumber + '-' + INVWSversion) AS TotalCases,
           SUM(CASE WHEN FirstOfWorksheet = 1 THEN CLMPayableByMedix2H END) AS PayableToHospital, SUM(CASE WHEN FirstOfWorksheet = 1 THEN CLMPayableByMedix2M END) AS PayableToMember,
           SUM(INVAmount) AS InvoiceAmount
    FROM invoices
    GROUP BY CASE WHEN @report <> 0 THEN HospitalCode END, CASE WHEN @report <> 0 THEN Hospital END, CASE WHEN @report = 2 THEN RTRIM(InvNo) END, CASE WHEN @report = 2 THEN InvDate END,
             CASE WHEN @report IN (0, 1) THEN InvYear END, CASE WHEN @report IN (0, 1) THEN InvMonth END
    ORDER BY CASE WHEN @report IN (0, 1) THEN InvYear END, CASE WHEN @report IN (0, 1) THEN InvMonth END, CASE WHEN @report <> 0 THEN HospitalCode END, CASE WHEN @report = 2 THEN InvDate END;
END
GO
