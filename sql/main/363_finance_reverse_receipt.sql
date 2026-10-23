-- Finance > Reverse Receipt (legacy frmReverseReceipt; its source is not available). Rebuilt on the pattern in the data: a receipt is
-- reversed by a NEW receipt with the negative amount (as RB47998 / RB53907), never by deleting it. The reversal's description is
-- "REV <original receipt no>-<original description>": it names the receipt it reverses, and a reversed fee receipt still matches its
-- invoice (the invoice balance logic matches descriptions ending in "-<invoice no>"). A claim receipt's allocation lines
-- (ReceiptfrPAY) are copied with negative amounts under the new number. Claim / Bordx receipt status columns are not changed.

-- Reverses receipt @id under @newNo (taken from FinanceReceipt_NextNumber in the same transaction). Result: 0 done (with the new
-- id and the number of claim lines copied), 1 receipt not found, 2 refused: it is itself a reversal / not positive, or already reversed.
CREATE OR ALTER PROCEDURE genisis.ReverseReceipt_Reverse
    @id int, @newNo nvarchar(40), @reversalDate date, @accMonth int, @accYear int, @userCode nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @no nvarchar(40), @desc nvarchar(100), @amount money, @type nvarchar(30), @cheque nvarchar(40), @payor nvarchar(4);
    SELECT @no = RTRIM(ReceiptNo), @desc = RTRIM(ReceiptDesc), @amount = ReceiptAmt, @type = ReceiptType, @cheque = RecChqNo, @payor = RecPayCode
    FROM genisis.Pay_ReceiptSummary WITH (UPDLOCK, HOLDLOCK) WHERE ReceiptIndex = @id;
    IF @no IS NULL
    BEGIN
        SELECT 1 AS Result, CAST(NULL AS int) AS Id, 0 AS Lines;
        RETURN;
    END
    IF @no = N'' OR ISNULL(@amount, 0) <= 0 OR ISNULL(@desc, N'') LIKE N'REV %'
       OR EXISTS (SELECT 1 FROM genisis.Pay_ReceiptSummary WHERE ReceiptDesc LIKE N'REV ' + @no + N'-%')
       OR EXISTS (SELECT 1 FROM genisis.Pay_ReceiptSummary WHERE ReceiptNo = @newNo)
    BEGIN
        SELECT 2 AS Result, CAST(NULL AS int) AS Id, 0 AS Lines;
        RETURN;
    END

    INSERT INTO genisis.Pay_ReceiptSummary (ReceiptNo, ReceiptDate, ReceiptAmt, ReceiptType, ReceiptDesc, RecChqNo, RecPayCode)
    VALUES (@newNo, @reversalDate, -@amount, @type, LEFT(N'REV ' + @no + N'-' + ISNULL(@desc, N''), 100), @cheque, @payor);
    DECLARE @newId int = CAST(SCOPE_IDENTITY() AS int);

    INSERT INTO genisis.Pay_ReceiptfrPAY (ReceiptNo, ReceiptDate, ReceiptAmt, PAYBankCharge, ChequeNo, Remark, BordxNo, CASNumber, ClaimVersion,
        PAYLastUpdatedate, PAYLastUpdateUser, RecPAYAmt, CheckAmt, RECAccPeriodYR, RECAccPeriodMth, PAYGainLoss, PAYMisc, PAYDiscount,
        PAYWriteOff, PAYMedixReceipt, PAYRemarks)
    SELECT @newNo, @reversalDate, -p.ReceiptAmt, -ISNULL(p.PAYBankCharge, 0), p.ChequeNo, LEFT(N'REVERSAL OF ' + @no, 400), p.BordxNo, p.CASNumber,
        p.ClaimVersion, CONVERT(datetime, CONVERT(char(8), GETDATE(), 112)), @userCode, -p.RecPAYAmt, -p.CheckAmt, @accYear, @accMonth,
        -ISNULL(p.PAYGainLoss, 0), -ISNULL(p.PAYMisc, 0), -ISNULL(p.PAYDiscount, 0), -ISNULL(p.PAYWriteOff, 0), -ISNULL(p.PAYMedixReceipt, 0),
        N'REVERSAL OF ' + @no
    FROM genisis.Pay_ReceiptfrPAY p WHERE p.ReceiptNo = @no;
    DECLARE @lines int = @@ROWCOUNT;

    SELECT 0 AS Result, @newId AS Id, @lines AS Lines;
END
GO
