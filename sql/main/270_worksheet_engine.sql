-- Worksheet calculation engine, step 1: the actual amounts of one worksheet version, added up from its invoices.
-- This is getDatatoWS of the desktop's worksheet forms: A1..A29 add up the invoice columns below for every invoice of the version, and H1..H29 the same for
-- the invoices paid to the hospital (InvPayTo = 'H'). The slot -> columns map was read out of the VB6 source (ShowInvoice puts each View_CAS_Invoice column in a
-- list sub-item; getDatatoWS adds the sub-items of each slot) and is checked against the totals the desktop saved: it reproduces ClaimActual for 35,495 saved
-- versions (99.96 to 100 per cent per slot; see WorksheetEngineTests).
-- Slots 1 to 29 are the rows of the grid: 1 Room & Board ... 25 Emergency Sickness Treatment, 26 Others - No Co-Pay, 27 to 29 the service taxes.
CREATE OR ALTER PROCEDURE genisis.WorksheetEngine_Actual
    @cas nvarchar(15), @version int
AS
BEGIN
    SET NOCOUNT ON;
    WITH inv AS (
        SELECT i.InvPayTo, v.Slot, v.Amt
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
        WHERE i.CASNumber = @cas AND i.INVWSversion = CAST(@version AS nvarchar(10))
    )
    SELECT n.Slot, ISNULL(SUM(inv.Amt), 0) AS Actual, ISNULL(SUM(CASE WHEN inv.InvPayTo = N'H' THEN inv.Amt END), 0) AS ToHospital
    FROM (VALUES (1), (2), (3), (4), (5), (6), (7), (8), (9), (10), (11), (12), (13), (14), (15), (16), (17), (18), (19), (20), (21), (22), (23), (24), (25), (26), (27), (28), (29)) n(Slot)
    LEFT JOIN inv ON inv.Slot = n.Slot
    GROUP BY n.Slot
    ORDER BY n.Slot;
END
GO

-- Worksheet calculation engine, step 2: the limit text the desktop saved for each row of a version (ClaimLimit).
-- The limit column of the grid is text: an amount ("200.00", "1,000.00"), the word FULL, or nothing / a dash. The approved amount of a row is worked out from
-- the actual amount and this text (WorksheetRules.Approve). Reading the saved text lets a saved worksheet be re-evaluated; the limits of a worksheet that is
-- being registered come from the plan (a later step).
CREATE OR ALTER PROCEDURE genisis.WorksheetEngine_Limits
    @cas nvarchar(15), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT v.Slot, LTRIM(RTRIM(v.LimitText)) AS LimitText
    FROM genisis.WS_ClaimLimit l
    CROSS APPLY (VALUES
            (1, l.CLMBoardAmt),
            (2, l.CLMICUAmt),
            (3, l.CLMServices),
            (4, l.CLMTheatre),
            (5, l.CLMMRI),
            (6, l.CLMPreHSAmt),
            (7, l.CLMPreHosp),
            (8, l.CLMSurgicalAmt),
            (9, l.CLMAna),
            (10, l.CLMNSPreHos),
            (11, l.CLMPhysician),
            (12, l.CLMPost),
            (13, l.CLMMPreHSAmt),
            (14, l.CLMAccPostAmt),
            (15, l.CLMAmbulance),
            (16, l.CLMOutpatient),
            (17, l.CLMMonthly),
            (18, l.CLMOrgan),
            (19, l.CLMTax),
            (20, l.CLMGuardianAmt),
            (21, l.CLMMedical),
            (22, l.CLMPhone),
            (23, l.CLMAdmin),
            (24, l.CLMHomeNCAmt),
            (25, l.CLMOthers1),
            (26, l.CLMOthersNotCoPay),
            (27, l.CLMBoardTaxAmt),
            (28, l.CLMICUTaxAmt),
            (29, l.CLMLodgerTaxAmt)
    ) v(Slot, LimitText)
    WHERE l.CASNumber = @cas AND l.CLMVersion = @version
    ORDER BY v.Slot;
END
GO

-- Worksheet calculation engine, step 3: the inputs of the summary rows that come from the plan and the member: the co-payment (getFinalTotal's CoPayApp) and the
-- member's available limit before the worksheet (CLMPreviousBalance), as saved with the version.
CREATE OR ALTER PROCEDURE genisis.WorksheetEngine_SummaryInputs
    @cas nvarchar(15), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ISNULL(CLMCoPayRB, 0) AS CoPayment, ISNULL(CLMPreviousBalance, 0) AS PreviousBalance
    FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @version;
END
GO
