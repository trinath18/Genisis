-- Payment > Enquiry > Payment To Hospital (legacy FrmEnquiryPaymentHospital, "Enquiry Payment to Hospital"): the claims payable to hospitals and what has been paid. The desktop sent a WHERE fragment built as text to
-- HISPayment.dbo.SP_Payment ('Pay2HospEnq', over View_PaymentHosp_Enq) and then asked two more questions for every row: the receipts of the claim (REceiptInfo, SP_Payment 'Receipt' over VIEW_Receipt) and, for a
-- part-paid claim, the sum of its payments (a query on Payment2HOS). Here the criteria are typed parameters and the receipts and the sum come with the rows.

-- ComboListing / LoadComboListing: the hospitals, the payors and the health types of the drop-downs.
CREATE OR ALTER PROCEDURE genisis.PaymentEnqHos_Lookups
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(HOSCode) AS Code, RTRIM(HOSName) AS Name FROM dbo.HOS ORDER BY HOSName, HOSCode;
    SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name FROM dbo.PAY ORDER BY PAYCode;
    SELECT RTRIM(HLTCode) AS Code, RTRIM(HLTDescription) AS Description FROM dbo.HLT ORDER BY HLTCode;
END
GO

-- cmdSearch_Click / SearchRecord / Payment2HOSListing: one row per payment of a claim payable to a hospital. @payment: 0 all, 1 paid (F), 2 unpaid, 3 part paid (P), 4 paid and part paid, 5 unpaid and part
-- paid. @scanCase is the one claim of "Use Scan" (exact); otherwise @caseFrom..@caseTo. The "empty" flags list the rows with no value. The desktop's empty boxes for the invoice date and the PV number tested
-- columns the view does not have (PVDate, PVNo) and failed; they test the invoice date and the PV number. PaidTotal is the sum of every payment of the claim, Os the amount still to pay, as the desktop worked
-- them out for a part-paid claim (and for an unpaid one the whole amount). The search recompiles: the criteria are optional, and the plan of the first search is the wrong one for the next.
CREATE OR ALTER PROCEDURE genisis.PaymentEnqHos_Search
    @hospital nvarchar(100) = NULL, @payor nvarchar(10) = NULL, @wsStatus nvarchar(2) = NULL, @healthType nvarchar(2) = NULL, @payment int = 0, @scanCase nvarchar(30) = NULL,
    @caseFrom nvarchar(30) = NULL, @caseTo nvarchar(30) = NULL, @invNo nvarchar(100) = NULL, @blankInvNo bit = 0, @invDateFrom datetime = NULL, @invDateTo datetime = NULL, @blankInvDate bit = 0,
    @pvNoFrom nvarchar(40) = NULL, @pvNoTo nvarchar(40) = NULL, @blankPvNo bit = 0, @pvDateFrom datetime = NULL, @pvDateTo datetime = NULL, @blankPvDate bit = 0,
    @sunNoFrom nvarchar(40) = NULL, @sunNoTo nvarchar(40) = NULL, @blankSunNo bit = 0, @sunDateFrom datetime = NULL, @sunDateTo datetime = NULL, @blankSunDate bit = 0, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max)
        RTRIM(e.PVPayHOSStatus) AS PayStatus, RTRIM(e.HOSName) AS HospitalName, RTRIM(e.InvNo) AS InvNo, e.InvDate, e.CLMPayableByMedix2H AS Payable, e.HOSPaidAmt AS PaidAmt,
        CASE WHEN e.MBMPatientCovID <> '00' THEN RTRIM(e.MBMCName) ELSE RTRIM(e.MBMName) END AS PatientName, RTRIM(e.CASNumber) AS CaseNo, RTRIM(e.ClaimVersion) AS ClaimVersion,
        RTRIM(e.BordxRefNo) AS BordxNo, RTRIM(e.CASARStatus) AS ArStatus, e.CASDateAdmitted AS Doa, e.CASDischargeDate AS Dod, e.PVHOSDate AS PaymentDate, RTRIM(e.INVEnteredBy) AS EntryBy,
        e.PVHOSIndex AS PvIndex, RTRIM(e.PVHOSNo) AS PvNo, RTRIM(e.PVHOSChequeNo) AS ChequeNo, e.PVHOSChequeDate AS ChequeDate, CAST(e.HOSAccPeriodMth AS int) AS AccMonth, CAST(e.HOSAccPeriodYr AS int) AS AccYear,
        e.PVBankCharges AS BankCharges, RTRIM(e.CLMWSStatus) AS WsStatus, RTRIM(e.PVSunHOSNo) AS SunPvNo, e.PVSunHOSDate AS SunPvDate,
        CASE WHEN RTRIM(ISNULL(e.PVPayHOSStatus, '')) = 'P' THEN paid.Total END AS PaidTotal,
        CASE WHEN RTRIM(ISNULL(e.PVPayHOSStatus, '')) = 'P' THEN e.CLMPayableByMedix2H - paid.Total WHEN RTRIM(ISNULL(e.PVPayHOSStatus, '')) = '' THEN e.CLMPayableByMedix2H END AS Os,
        CASE WHEN RTRIM(r.ReceiptNoMNRB) <> '' THEN RTRIM(r.ReceiptNoMNRB) + CASE WHEN RTRIM(r.ReceiptNoMBM) <> '' THEN ', ' + RTRIM(r.ReceiptNoMBM) ELSE '' END
             WHEN RTRIM(r.ReceiptNoPAY) <> '' THEN RTRIM(r.ReceiptNoPAY) + CASE WHEN RTRIM(r.ReceiptNoMBM) <> '' THEN ', ' + RTRIM(r.ReceiptNoMBM) ELSE '' END
             ELSE RTRIM(r.ReceiptNoMBM) END AS ReceiptNo,
        ISNULL(r.CheckAmtMNRB, 0) + ISNULL(r.CheckAmtPAY, 0) + ISNULL(r.ReceiptAmtMBM, 0) AS ReceiptAmt
    FROM genisis.Pay_ViewPaymentHospEnq e
    OUTER APPLY (SELECT SUM(p.HOSPaidAmt) AS Total FROM genisis.Pay_Payment2HOS p WHERE p.CASNumber = e.CASNumber AND p.ClaimVersion = e.ClaimVersion) paid
    OUTER APPLY (SELECT TOP (1) v.ReceiptNoMNRB, v.ReceiptNoPAY, v.ReceiptNoMBM, v.CheckAmtMNRB, v.CheckAmtPAY, v.ReceiptAmtMBM
                 FROM genisis.Pay_ViewReceipt v
                 WHERE v.CASNumber = e.CASNumber AND v.ClaimVersion = e.ClaimVersion
                   AND RTRIM(ISNULL(v.ReceiptNoMNRB, '')) + RTRIM(ISNULL(v.ReceiptNoPAY, '')) + RTRIM(ISNULL(v.ReceiptNoMBM, '')) <> ''
                 ORDER BY v.ReceiptNoMNRB DESC, v.ReceiptNoPAY DESC, v.ReceiptNoMBM DESC) r
    WHERE (@hospital IS NULL OR e.INVPayee = @hospital) AND (@payor IS NULL OR e.PAYCode = @payor) AND (@wsStatus IS NULL OR e.CLMWSStatus = @wsStatus) AND (@healthType IS NULL OR e.HLTCode = @healthType)
      AND (@payment <> 1 OR e.PVPayHOSStatus = 'F') AND (@payment <> 2 OR e.PVPayHOSStatus = '' OR e.PVPayHOSStatus IS NULL) AND (@payment <> 3 OR e.PVPayHOSStatus = 'P')
      AND (@payment <> 4 OR e.PVPayHOSStatus IN ('P', 'F')) AND (@payment <> 5 OR e.PVPayHOSStatus = '' OR e.PVPayHOSStatus IS NULL OR e.PVPayHOSStatus = 'P')
      AND (@scanCase IS NULL OR e.CASNumber = @scanCase) AND (@caseFrom IS NULL OR e.CASNumber >= @caseFrom) AND (@caseTo IS NULL OR e.CASNumber <= @caseTo)
      AND (@invNo IS NULL OR e.InvNo = @invNo) AND (@blankInvNo = 0 OR e.InvNo IS NULL OR e.InvNo = '')
      AND (@invDateFrom IS NULL OR e.InvDate >= @invDateFrom) AND (@invDateTo IS NULL OR e.InvDate <= @invDateTo) AND (@blankInvDate = 0 OR e.InvDate IS NULL)
      AND (@pvNoFrom IS NULL OR e.PVHOSNo >= @pvNoFrom) AND (@pvNoTo IS NULL OR e.PVHOSNo <= @pvNoTo) AND (@blankPvNo = 0 OR e.PVHOSNo IS NULL OR e.PVHOSNo = '')
      AND (@pvDateFrom IS NULL OR e.PVHOSDate >= @pvDateFrom) AND (@pvDateTo IS NULL OR e.PVHOSDate <= @pvDateTo) AND (@blankPvDate = 0 OR e.PVHOSDate IS NULL)
      AND (@sunNoFrom IS NULL OR e.PVSunHOSNo >= @sunNoFrom) AND (@sunNoTo IS NULL OR e.PVSunHOSNo <= @sunNoTo) AND (@blankSunNo = 0 OR e.PVSunHOSNo IS NULL OR e.PVSunHOSNo = '')
      AND (@sunDateFrom IS NULL OR e.PVSunHOSDate >= @sunDateFrom) AND (@sunDateTo IS NULL OR e.PVSunHOSDate <= @sunDateTo) AND (@blankSunDate = 0 OR e.PVSunHOSDate IS NULL)
    ORDER BY e.CASNumber, e.ClaimVersion, e.PVHOSIndex
    OPTION (RECOMPILE);
END
GO
