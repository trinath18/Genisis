-- Payment > Contra Allocation (legacy frmContraAllocation, "Contra Allocation"): the contra rows of HISPayment.ContraAllocation (a claim's amount set against another claim, written by the Receipt screens) are
-- listed and the ones ticked are allocated: they get one allocation number (six digits, from 100001), today's date and the status A. The desktop built the WHERE from the boxes
-- as text (its RefNo "to" test used the "from" box, so a "from" without a "to" found nothing) and updated "the first row of the case and version"; here the criteria are typed and the rows
-- ticked are updated by their own key.

-- cmdSearch_Click / SearchRecord: the contra rows. @status: 0 all, 1 allocated, 2 not allocated. @scanCase is the one case of "Use Scan" (exact); otherwise @caseFrom..@caseTo.
CREATE OR ALTER PROCEDURE genisis.PaymentContra_Search
    @refFrom nvarchar(100) = NULL, @refTo nvarchar(100) = NULL, @status int = 0, @scanCase nvarchar(30) = NULL, @caseFrom nvarchar(30) = NULL, @caseTo nvarchar(30) = NULL,
    @contraFrom nvarchar(100) = NULL, @contraTo nvarchar(100) = NULL, @allocFrom nvarchar(12) = NULL, @allocTo nvarchar(12) = NULL, @dateFrom datetime = NULL, @dateTo datetime = NULL,
    @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) c.CONIndex AS Id, RTRIM(c.RefNo) AS RefNo, RTRIM(c.CaseNo) AS CaseNo, RTRIM(c.VersionNo) AS VersionNo, c.CaseAmt, RTRIM(c.ContraCaseNo) AS ContraCaseNo,
        RTRIM(c.ContraVersionNo) AS ContraVersionNo, c.ContraCaseAmt, RTRIM(c.AllocationNo) AS AllocationNo, c.AllocationDate, RTRIM(c.ContraStatus) AS ContraStatus
    FROM genisis.Pay_ContraAllocation c
    WHERE (@refFrom IS NULL OR c.RefNo >= @refFrom) AND (@refTo IS NULL OR c.RefNo <= @refTo)
      AND (@status <> 1 OR c.ContraStatus = 'A') AND (@status <> 2 OR c.ContraStatus = '' OR c.ContraStatus IS NULL)
      AND (@scanCase IS NULL OR c.CaseNo = @scanCase)
      AND (@caseFrom IS NULL OR c.CaseNo >= @caseFrom) AND (@caseTo IS NULL OR c.CaseNo <= @caseTo)
      AND (@contraFrom IS NULL OR c.ContraCaseNo >= @contraFrom) AND (@contraTo IS NULL OR c.ContraCaseNo <= @contraTo)
      AND (@allocFrom IS NULL OR c.AllocationNo >= @allocFrom) AND (@allocTo IS NULL OR c.AllocationNo <= @allocTo)
      AND (@dateFrom IS NULL OR c.AllocationDate >= @dateFrom) AND (@dateTo IS NULL OR c.AllocationDate <= @dateTo)
    ORDER BY c.CONIndex;
END
GO

-- GenerateContraNo: the next allocation number, six digits, continuing the numbers already on ContraAllocation (100001 when there is none). The desktop took the first row of HISMaintenance.ContraAllcSeq (a
-- counter that has one row per allocation code: the bank contra, hospital adjustment, bank charge and float allocation forms use AA, AH, AB and AF) and wrote a six-digit number back to its five-character column;
-- here that shared counter is not touched. The application lock makes two people wait for each other until the transaction ends, so they cannot get the same number.
CREATE OR ALTER PROCEDURE genisis.PaymentContra_NextNumber
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @lock int;
    EXEC @lock = sp_getapplock @Resource = N'PaymentContraAllocationNo', @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 30000;
    IF @lock < 0
    BEGIN
        RAISERROR (N'The allocation number is being taken by another user. Please try again.', 16, 1);
        RETURN;
    END
    SELECT RIGHT(N'000000' + CAST(ISNULL(MAX(CAST(RTRIM(AllocationNo) AS int)), 100000) + 1 AS nvarchar(10)), 6) AS AllocationNo
    FROM genisis.Pay_ContraAllocation
    WHERE AllocationNo IS NOT NULL AND LEN(RTRIM(AllocationNo)) = 6 AND RTRIM(AllocationNo) NOT LIKE N'%[^0-9]%';
END
GO

-- UpdateRecord: allocates one contra row. Result: 0 allocated, 1 no such row, 2 it was allocated already ("Record has been allocated!"), 3 the row has no claim (the receipt-only rows the screen lists with blank
-- claim columns: the desktop looked the claim up by its blank number, found nothing and still said it had allocated).
CREATE OR ALTER PROCEDURE genisis.PaymentContra_Allocate
    @id int, @allocationNo nvarchar(12), @today datetime
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @status nvarchar(1), @case nvarchar(30);
    SELECT @status = ISNULL(RTRIM(ContraStatus), N''), @case = RTRIM(CaseNo) FROM genisis.Pay_ContraAllocation WITH (UPDLOCK, HOLDLOCK) WHERE CONIndex = @id;
    IF @@ROWCOUNT = 0 BEGIN SELECT 1 AS Result; RETURN; END
    IF @status = N'A' BEGIN SELECT 2 AS Result; RETURN; END
    IF ISNULL(@case, N'') = N'' BEGIN SELECT 3 AS Result; RETURN; END
    UPDATE genisis.Pay_ContraAllocation SET AllocationNo = @allocationNo, AllocationDate = @today, ContraStatus = 'A' WHERE CONIndex = @id;
    SELECT 0 AS Result;
END
GO
