-- Case > Claim ASCII (legacy frmNewClaimAscii) and Case > Case ASCII (legacy frmCaseASCII): the rows of the fixed-width
-- payor files, the daily batch number and the "exported" flags. Replaces the raw @strAppend dbo.Case_ClaimAscii modes
-- (1, 2, 4, 5), Medix.bas GetCLMAsciiBatch / GetCASAsciiBatch (read-then-update) and dbo.Case_CLMAscUpdate.

-- Medix.bas GetCLMAsciiBatch / GetCASAsciiBatch: the next 5-digit batch number of a payor for a day, taken atomically.
-- @kind = CLM (HISWorkSheet.CLMAsciiSEQ) or CAS (dbo.CASAsciiSEQ).
CREATE OR ALTER PROCEDURE genisis.Ascii_NextBatch
    @kind char(3), @pay nvarchar(8), @date datetime
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @day datetime = CAST(CAST(@date AS date) AS datetime), @n nvarchar(10);
    IF @kind = 'CLM'
    BEGIN
        UPDATE genisis.WS_CLMAsciiSEQ WITH (UPDLOCK, ROWLOCK)
        SET @n = SEQBatchNo = RIGHT(N'00000' + CAST(CAST(SEQBatchNo AS int) + 1 AS nvarchar(10)), 5)
        WHERE SEQPayCode = @pay AND SEQBatchDate = @day;
        IF @@ROWCOUNT = 0
        BEGIN
            SET @n = N'00001';
            INSERT genisis.WS_CLMAsciiSEQ (SEQPayCode, SEQBatchDate, SEQBatchNo) VALUES (@pay, @day, @n);
        END
    END
    ELSE
    BEGIN
        UPDATE dbo.CASAsciiSEQ WITH (UPDLOCK, ROWLOCK)
        SET @n = SEQBatchNo = RIGHT(N'00000' + CAST(CAST(SEQBatchNo AS int) + 1 AS nvarchar(10)), 5)
        WHERE SEQPayCode = @pay AND SEQBatchDate = @day;
        IF @@ROWCOUNT = 0
        BEGIN
            SET @n = N'00001';
            INSERT dbo.CASAsciiSEQ (SEQPayCode, SEQBatchDate, SEQBatchNo) VALUES (@pay, @day, @n);
        END
    END
    SELECT @n AS BatchNo;
END
GO

-- PrincipalASCII (accepted cases, Case_ClaimAscii mode 1): worksheets with a bordx dated in the range for the payor.
-- The "to" dates are inclusive of the whole day: the desktop compared with <= 'to date' (midnight), which dropped the
-- cases registered later on the last day because CaseRegDate carries the time.
-- @reprint = 0 keeps the claims not exported yet (CLMBordxWSASCII N), 1 the ones already exported (Y).
-- @wsStatus: 0 = new and amended claims (CLMBordxWSStatus A or N), 1 = deleted claims (D).
CREATE OR ALTER PROCEDURE genisis.AsciiClaim_Accepted
    @pay nvarchar(8), @dateFrom datetime, @dateTo datetime, @reprint bit, @wsStatus char(1)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM genisis.WS_CLMReport
    WHERE BordxRefNo IS NOT NULL AND BordxDate >= @dateFrom AND BordxDate < DATEADD(day, 1, @dateTo) AND PAYCode = @pay
      AND CLMBordxWSASCII = CASE WHEN @reprint = 1 THEN N'Y' ELSE N'N' END
      AND ((@wsStatus = '0' AND CLMBordxWSStatus IN (N'A', N'N')) OR (@wsStatus = '1' AND CLMBordxWSStatus = N'D'))
    ORDER BY BordxRefNo, CASNumber, ClaimVersion;
END
GO

-- PrincipalASCIIRejectCase (Case_ClaimAscii mode 4): repudiated cases (second claimability letter R) registered in the range.
CREATE OR ALTER PROCEDURE genisis.AsciiClaim_Rejected
    @pay nvarchar(8), @dateFrom datetime, @dateTo datetime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM genisis.WS_CLMReport
    WHERE SUBSTRING(CASClaimability, 2, 1) = N'R' AND CaseRegDate >= @dateFrom AND CaseRegDate < DATEADD(day, 1, @dateTo) AND PAYCode = @pay
    ORDER BY CASNumber, ClaimVersion;
END
GO

-- UpdateWSAscii: the worksheet has been exported. The desktop flagged every version of the case (Case_CLMAscUpdate is
-- by case number); only the exported version is flagged here.
CREATE OR ALTER PROCEDURE genisis.AsciiClaim_MarkExported
    @cas nvarchar(30), @version int
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.WS_Claim SET CLMBordxWSASCII = N'Y' WHERE CASNumber = @cas AND ClaimVersion = @version;
END
GO

-- Case ASCII (Case_ClaimAscii mode 5): the cases of the payor registered in the range. @reprint = 1 keeps the cases already
-- exported (CASASCIIGenStatus Y). @endorsement: 0 = every case (the desktop never filtered), 1 = deleted (D), 2 = amended (A) by CASASCIIAmendStatus.
CREATE OR ALTER PROCEDURE genisis.AsciiCase_Rows
    @pay nvarchar(8), @dateFrom datetime, @dateTo datetime, @reprint bit, @endorsement char(1)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM genisis.WS_CLMReportCas
    WHERE CaseRegDate >= @dateFrom AND CaseRegDate < DATEADD(day, 1, @dateTo) AND PAYCode = @pay
      AND (@reprint = 0 OR CASASCIIGenStatus = N'Y')
      AND (@endorsement = '0' OR (@endorsement = '1' AND CASASCIIAmendStatus = N'D') OR (@endorsement = '2' AND CASASCIIAmendStatus = N'A'))
    ORDER BY CASNumber;
END
GO

-- The case has been exported (the desktop's UpdateCASAscii called a procedure that does not exist, so reprint never worked).
CREATE OR ALTER PROCEDURE genisis.AsciiCase_MarkExported
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.CASOthersTwo SET CASASCIIGenStatus = N'Y' WHERE CASNumber = @cas;
END
GO
