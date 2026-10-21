-- Invoice Processing (legacy frmInvoice, menu Invoice > Invoice Processing), read side: the case an invoice belongs to, the case's invoices, and one invoice's amounts by worksheet row.
-- The row mapping is the one the worksheet engine uses (WorksheetEngine_Actual), so an invoice's rows add up to the worksheet's actual amounts.

CREATE OR ALTER PROCEDURE genisis.Invoice_Case
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(c.CASNumber) AS CaseNo, RTRIM(c.MBMNumber) AS MemberNo, RTRIM(c.MBMPatientCovID) AS CoverId, RTRIM(m.MBMName) AS MemberName, RTRIM(cp.MBMCName) AS PatientName,
        RTRIM(m.PAYCode) AS Payor, RTRIM(m.PLNCode) AS PlanCode, RTRIM(m.INSCode) AS InsuredType, RTRIM(m.HLTCode) AS HealthType, RTRIM(c.CASHOSCode) AS HospitalCode, RTRIM(h.HOSName) AS Hospital,
        c.CASDateAdmitted AS Admitted, c.CASDischargeDate AS Discharged, RTRIM(c.CASStayType) AS StayType, RTRIM(c.CASPAYType) AS PayType, RTRIM(c.CASClaimability) AS Claimability,
        (SELECT MAX(w.ClaimVersion) FROM genisis.WS_Claim w WHERE w.CASNumber = c.CASNumber) AS LastWorksheetVersion,
        (SELECT ISNULL(MAX(w.ClaimVersion), 0) + 1 FROM genisis.WS_Claim w WHERE w.CASNumber = c.CASNumber AND RTRIM(w.ClaimStatus) = N'C') AS NextWorksheetVersion, RTRIM(c.ModalityCode) AS Modality,
        (SELECT TOP (1) r.Remarks FROM genisis.WS_ClaimInvoiceRemarks r WHERE r.CASNumber = c.CASNumber) AS Remarks
    FROM dbo.CAS c
    JOIN dbo.MBM m ON m.MBMNumber = c.MBMNumber
    LEFT JOIN dbo.HOS h ON h.HOSCode = c.CASHOSCode
    LEFT JOIN dbo.MBMCoveredPersons cp ON cp.MBMCNumber = c.MBMNumber AND cp.MBMCCoverID = c.MBMPatientCovID
    WHERE c.CASNumber = @cas;
END
GO

CREATE OR ALTER PROCEDURE genisis.Invoice_List
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT i.INVIndex AS [Index], RTRIM(i.InvNo) AS InvoiceNo, i.InvDate AS InvoiceDate, RTRIM(i.INVPayTo) AS PayTo, RTRIM(i.INVPayee) AS Payee,
        CASE WHEN RTRIM(i.INVPayTo) = N'H' THEN RTRIM(h.HOSName) ELSE RTRIM(i.INVPayee) END AS PayeeName,
        i.INVAmount AS Amount, CAST(CASE WHEN ISNULL(i.INVWSed, 0) = 0 THEN 0 ELSE 1 END AS bit) AS InWorksheet, RTRIM(i.INVWSversion) AS WorksheetVersion,
        RTRIM(i.INVBordxRefNo) AS BordxNo, RTRIM(i.INVStayType) AS StayType, RTRIM(i.INVPAYType) AS PayType, RTRIM(i.CLMINVRecMode) AS ReceiptMode,
        i.CLMINVRecDate AS ReceiptDate, i.INVFolUpDate AS FollowUpDate, RTRIM(i.INVRoomType) AS RoomType, i.InvExcessAmt AS ExcessAmount, i.InvPayAmt AS PayAmount,
        i.INVInitialActAmt AS InitialAmount, RTRIM(i.CLMUHCNo) AS UhcNo, RTRIM(i.INVEnteredBy) AS EnteredBy, i.INVEnteredDate AS EnteredDate
    FROM genisis.WS_ClaimInvoice i
    LEFT JOIN dbo.HOS h ON h.HOSCode = RTRIM(i.INVPayee) AND RTRIM(i.INVPayTo) = N'H'
    WHERE i.CASNumber = @cas
    ORDER BY i.INVIndex;
END
GO

CREATE OR ALTER PROCEDURE genisis.Invoice_Rows
    @invoice int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT n.Slot, ISNULL(SUM(inv.Amt), 0) AS Amount
    FROM (VALUES (1), (2), (3), (4), (5), (6), (7), (8), (9), (10), (11), (12), (13), (14), (15), (16), (17), (18), (19), (20), (21), (22), (23), (24), (25), (26), (27), (28), (29)) n(Slot)
    LEFT JOIN (
        SELECT v.Slot, v.Amt
        FROM genisis.WS_CASInvoice i
        CROSS APPLY (VALUES
            (1, genisis.Worksheet_Amount(i.CLMBoardAmt) + genisis.Worksheet_Amount(i.CLMNewRBTax)),
            (2, genisis.Worksheet_Amount(i.CLMICUAmt) + genisis.Worksheet_Amount(i.CLMNewICUTax)),
            (3, genisis.Worksheet_Amount(i.CLMServices)),
            (4, genisis.Worksheet_Amount(i.CLMTheatre)),
            (5, genisis.Worksheet_Amount(i.CLMMRI)),
            (6, genisis.Worksheet_Amount(i.CLMPreHSAmt)),
            (7, genisis.Worksheet_Amount(i.CLMPreHosp)),
            (8, genisis.Worksheet_Amount(i.CLMSurgicalAmt)),
            (9, genisis.Worksheet_Amount(i.CLMAna)),
            (10, genisis.Worksheet_Amount(i.CLMNSPreHos)),
            (11, genisis.Worksheet_Amount(i.CLMPhysician)),
            (12, genisis.Worksheet_Amount(i.CLMPost)),
            (13, genisis.Worksheet_Amount(i.CLMMPreHosAmt)),
            (14, genisis.Worksheet_Amount(i.CLMAccPostAmt)),
            (15, genisis.Worksheet_Amount(i.CLMAmbulance)),
            (16, genisis.Worksheet_Amount(i.CLMOutpatient)),
            (17, genisis.Worksheet_Amount(i.CLMMonthly)),
            (18, genisis.Worksheet_Amount(i.CLMOrgan)),
            (19, genisis.Worksheet_Amount(i.CLMCashAmt)),
            (20, genisis.Worksheet_Amount(i.CLMGuardianAmt) + genisis.Worksheet_Amount(i.CLMNewLodgerTax)),
            (21, genisis.Worksheet_Amount(i.CLMMedical)),
            (22, genisis.Worksheet_Amount(i.CLMPhone)),
            (23, genisis.Worksheet_Amount(i.CLMAdmin)),
            (24, genisis.Worksheet_Amount(i.CLMHomeNCAmt)),
            (25, genisis.Worksheet_Amount(i.CLMOthers1)),
            (26, genisis.Worksheet_Amount(i.CLMOthersNotCoPay)),
            (27, genisis.Worksheet_Amount(i.CLMRBTax)),
            (28, genisis.Worksheet_Amount(i.CLMICUTax)),
            (29, genisis.Worksheet_Amount(i.CLMLodgerTax))
        ) v(Slot, Amt)
        WHERE i.INVIndex = @invoice
    ) inv ON inv.Slot = n.Slot
    GROUP BY n.Slot
    ORDER BY n.Slot;
END
GO
