-- Maintenance > Plan, Premium tab (one PRM row per age band).

-- Result: 0 saved, 2 a row with the same INS/HLT/PLN/age band/effective date (and payor unless health type S) exists.
CREATE OR ALTER PROCEDURE genisis.Premium_Insert
    @ins nvarchar(4000), @pay nvarchar(4000), @hlt nvarchar(4000), @age nvarchar(4000), @plan nvarchar(4000), @status nvarchar(4000),
    @amount decimal(19, 4), @supp decimal(19, 4), @effective datetime, @version int, @userCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1 FROM dbo.PRM WITH (UPDLOCK, HOLDLOCK)
        WHERE INSCode = @ins AND HLTCode = @hlt AND PLNCode = @plan AND AGECode = @age AND PRMEffDate = @effective
          AND (@hlt = N'S' OR ISNULL(PAYCode, N'') = ISNULL(@pay, N'')))
    BEGIN SELECT 2 AS Result, CAST(NULL AS int) AS PremiumCode; RETURN; END

    INSERT INTO dbo.PRM (INSCode, PAYCode, HLTCode, AGECode, PLNCode, SuppPrmStatus, PRMAmount, SuppPrmAmt, PRMEffDate, PRMVersion,
        PRMLastUpdateUser, PRMLastUpdateDate)
    VALUES (@ins, @pay, @hlt, @age, @plan, @status, @amount, @supp, @effective, @version, @userCode, CAST(CAST(GETDATE() AS date) AS datetime));
    SELECT 0 AS Result, CAST(SCOPE_IDENTITY() AS int) AS PremiumCode;
END
GO

-- Edits one row's amounts, date and version; codes and age band stay fixed.
-- Result: 0 saved, 1 record not found, 2 another row already has this key and date.
-- A row that already shares its key and date with another row can still be edited as long as the date is kept.
CREATE OR ALTER PROCEDURE genisis.Premium_Update
    @code int, @amount decimal(19, 4), @supp decimal(19, 4), @effective datetime, @version int, @userCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.PRM WITH (UPDLOCK, HOLDLOCK) WHERE PRMCode = @code)
    BEGIN SELECT 1 AS Result; RETURN; END

    IF EXISTS (
        SELECT 1 FROM dbo.PRM o WITH (UPDLOCK, HOLDLOCK) JOIN dbo.PRM t ON t.PRMCode = @code
        WHERE o.PRMCode <> @code AND o.INSCode = t.INSCode AND o.HLTCode = t.HLTCode AND o.PLNCode = t.PLNCode AND o.AGECode = t.AGECode
          AND o.PRMEffDate = @effective AND (t.HLTCode = N'S' OR ISNULL(o.PAYCode, N'') = ISNULL(t.PAYCode, N''))
          AND (t.PRMEffDate IS NULL OR t.PRMEffDate <> @effective))
    BEGIN SELECT 2 AS Result; RETURN; END

    UPDATE dbo.PRM SET PRMAmount = @amount, SuppPrmAmt = CASE WHEN SuppPrmStatus IN ('B', 'C') THEN @supp ELSE SuppPrmAmt END,
        PRMEffDate = @effective, PRMVersion = @version, PRMLastUpdateUser = @userCode,
        PRMLastUpdateDate = CAST(CAST(GETDATE() AS date) AS datetime)
    WHERE PRMCode = @code;
    SELECT 0 AS Result;
END
GO

-- Desktop DeletePRM. Result: 0 deleted, 1 record not found,
-- 2 a member is on the row's insured/health/plan/age band (and payor unless health type S).
CREATE OR ALTER PROCEDURE genisis.Premium_Delete
    @code int
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.PRM WITH (UPDLOCK, HOLDLOCK) WHERE PRMCode = @code)
    BEGIN SELECT 1 AS Result; RETURN; END
    IF EXISTS (
        SELECT 1 FROM dbo.MBM m JOIN dbo.PRM t ON t.PRMCode = @code
        WHERE m.INSCode = t.INSCode AND m.HLTCode = t.HLTCode AND m.PLNCode = t.PLNCode AND m.AGECode = t.AGECode
          AND (t.HLTCode = N'S' OR m.PAYCode = t.PAYCode))
    BEGIN SELECT 2 AS Result; RETURN; END
    DELETE FROM dbo.PRM WHERE PRMCode = @code;
    SELECT 0 AS Result;
END
GO

CREATE OR ALTER PROCEDURE genisis.Premium_Search
    @top int, @insured nvarchar(4000) = NULL, @payor nvarchar(4000) = NULL, @health nvarchar(4000) = NULL,
    @age nvarchar(4000) = NULL, @plan nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@top) PRMCode AS Code, RTRIM(INSCode) AS InsuredCode, RTRIM(PAYCode) AS PayorCode, RTRIM(HLTCode) AS HealthCode,
        RTRIM(AGECode) AS AgeCode, RTRIM(PLNCode) AS PlanCode, PRMAmount AS Amount, SuppPrmAmt AS SuppAmount, PRMEffDate AS EffectiveDate,
        PRMVersion AS Version, SuppPrmStatus AS SuppStatus
    FROM dbo.PRM
    WHERE (@insured IS NULL OR INSCode = @insured) AND (@payor IS NULL OR PAYCode = @payor) AND (@health IS NULL OR HLTCode = @health)
      AND (@age IS NULL OR AGECode = @age) AND (@plan IS NULL OR PLNCode = @plan)
    ORDER BY PRMCode DESC
    OPTION (RECOMPILE);
END
GO
