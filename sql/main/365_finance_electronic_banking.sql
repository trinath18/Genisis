-- Finance > Electronic Banking (legacy frmFileExportBankESP; its source is not available, and no bank file layout exists in the code or
-- databases). Rebuilt from the data: electronic payments are the payment vouchers with PVPymtMode 'ONLINE' in HISPayment.dbo.Payment2MBM
-- (members: payee, bank, account on the voucher) and Payment2HOS (hospitals: one PV no. per payment, one row per case; no bank details
-- on the voucher). PVChequeNo holds the crediting date (ddmmyyyy) on ONLINE vouchers. The account length each bank expects is in
-- HISMaintenance.dbo.BankAccounts. Read only: the API builds the file; nothing is marked as exported.

-- One row per ONLINE payment (PV no.). @payeeType 'M' members, 'H' hospitals, NULL both. Member payments that were stopped (a C / E row
-- with the same PV no.) and the stop rows themselves are left out; so are hospital vouchers that are not active.
CREATE OR ALTER PROCEDURE genisis.ElectronicBanking_Search
    @payeeType char(1) = NULL, @dateFrom datetime = NULL, @dateTo datetime = NULL, @creditRef nvarchar(40) = NULL,
    @pvFrom nvarchar(30) = NULL, @pvTo nvarchar(30) = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) x.PayeeType, x.PvNo, x.PvDate, x.CreditRef, x.Payee, x.Bank, x.Account, x.Amount, x.Cases,
        d.AccountDigits, d.AccountDigitsTwo
    FROM (
        SELECT 'M' AS PayeeType, RTRIM(m.PVNo) AS PvNo, MIN(m.PVDate) AS PvDate, MIN(RTRIM(m.PVChequeNo)) AS CreditRef, MIN(RTRIM(m.PVPayto)) AS Payee,
            MIN(RTRIM(m.PVPayeeBank)) AS Bank, MIN(RTRIM(m.PVPayeeACNo)) AS Account, SUM(m.PVChequeAmt) AS Amount, COUNT(*) AS Cases
        FROM genisis.Pay_Payment2MBM m
        WHERE (@payeeType IS NULL OR @payeeType = 'M') AND m.PVPymtMode = N'ONLINE' AND m.PVChequeAmt > 0 AND ISNULL(m.PVStatus, 'A') IN ('A', 'R')
          AND NOT EXISTS (SELECT 1 FROM genisis.Pay_Payment2MBM s WHERE RTRIM(s.PVNo) = RTRIM(m.PVNo) AND s.PVStatus IN ('C', 'E'))
          AND (@dateFrom IS NULL OR m.PVDate >= @dateFrom) AND (@dateTo IS NULL OR m.PVDate <= @dateTo)
          AND (@creditRef IS NULL OR m.PVChequeNo = @creditRef)
          AND (@pvFrom IS NULL OR m.PVNo >= @pvFrom) AND (@pvTo IS NULL OR m.PVNo <= @pvTo)
        GROUP BY RTRIM(m.PVNo)
        UNION ALL
        SELECT 'H', RTRIM(h.PVHOSNo), MIN(h.PVHOSDate), MIN(RTRIM(h.PVHOSChequeNo)), MIN(RTRIM(h.PVPayee)), MIN(RTRIM(h.PVBank)), MIN(RTRIM(h.PVAccountNo)),
            MAX(h.PVHOSChequeAmt), COUNT(*)
        FROM genisis.Pay_Payment2HOS h
        WHERE (@payeeType IS NULL OR @payeeType = 'H') AND h.PVPymtMode = N'ONLINE' AND h.PVHOSChequeAmt > 0 AND ISNULL(h.PVStatus, 'A') = 'A'
          AND (@dateFrom IS NULL OR h.PVHOSDate >= @dateFrom) AND (@dateTo IS NULL OR h.PVHOSDate <= @dateTo)
          AND (@creditRef IS NULL OR h.PVHOSChequeNo = @creditRef)
          AND (@pvFrom IS NULL OR h.PVHOSNo >= @pvFrom) AND (@pvTo IS NULL OR h.PVHOSNo <= @pvTo)
        GROUP BY RTRIM(h.PVHOSNo)
    ) x
    OUTER APPLY (SELECT TOP 1 b.AccountDigits, b.AccountDigitsTwo FROM genisis.Maint_BankAccounts b WHERE RTRIM(b.Bank) = x.Bank) d
    ORDER BY x.PvDate DESC, x.PvNo DESC
    OPTION (RECOMPILE);
END
GO
