-- Maintenance > Plan, MCO Fee tab (dbo.MCO, one row per plan / health / insured / supp. status / effective date).

CREATE OR ALTER PROCEDURE genisis.McoFee_Search
    @top int, @plan nvarchar(4000) = NULL, @health nvarchar(4000) = NULL, @insured nvarchar(4000) = NULL, @effective datetime = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@top) MCOCode AS [Index], RTRIM(PLNCode) AS PlanCode, RTRIM(HLTCode) AS HealthCode, RTRIM(INSCode) AS InsuredCode,
        RTRIM(SuppMCOStatus) AS SuppStatus, MCOAmountAdult AS Adult, MCOAmountChild AS Child, MCOSuppAdultAmt AS SuppAdult,
        MCOSuppChildAmt AS SuppChild, MCOEffDate AS EffectiveDate, RTRIM(CovIDChrg) AS CoveredIdCharge
    FROM dbo.MCO
    WHERE (@plan IS NULL OR PLNCode = @plan) AND (@health IS NULL OR HLTCode = @health) AND (@insured IS NULL OR INSCode = @insured)
      AND (@effective IS NULL OR MCOEffDate = @effective)
    ORDER BY MCOCode DESC
    OPTION (RECOMPILE);
END
GO

-- Result: 0 saved, 1 no plan with this code and health type, 2 a row with the same plan/health/insured/status/date exists.
CREATE OR ALTER PROCEDURE genisis.McoFee_Insert
    @plan nvarchar(4000), @health nvarchar(4000), @insured nvarchar(4000), @status nvarchar(4000), @effective datetime,
    @adult decimal(19, 4), @child decimal(19, 4), @suppAdult decimal(19, 4), @suppChild decimal(19, 4),
    @coveredIdCharge nvarchar(4000), @userCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.PLN WHERE PLNCode = @plan AND HLTCode = @health)
    BEGIN SELECT 1 AS Result, CAST(NULL AS int) AS [Index]; RETURN; END
    IF EXISTS (
        SELECT 1 FROM dbo.MCO WITH (UPDLOCK, HOLDLOCK)
        WHERE PLNCode = @plan AND HLTCode = @health AND INSCode = @insured AND SuppMCOStatus = @status AND MCOEffDate = @effective)
    BEGIN SELECT 2 AS Result, CAST(NULL AS int) AS [Index]; RETURN; END

    INSERT INTO dbo.MCO (PLNCode, HLTCode, INSCode, SuppMCOStatus, MCOEffDate, MCOAmountAdult, MCOAmountChild, MCOSuppAdultAmt,
        MCOSuppChildAmt, CovIDChrg, MCOLastUpdateUser, MCOLastUpdateDate)
    VALUES (@plan, @health, @insured, @status, @effective, @adult, @child, @suppAdult, @suppChild, @coveredIdCharge, @userCode,
        CAST(CAST(GETDATE() AS date) AS datetime));
    SELECT 0 AS Result, CAST(SCOPE_IDENTITY() AS int) AS [Index];
END
GO

-- Edits one row's amounts, date and covered ID charge; its codes and status stay fixed.
-- Result: 0 saved, 1 record not found, 2 another row already has this key and date.
-- A row that already shares its key and date with another row can still be edited as long as the date is kept.
CREATE OR ALTER PROCEDURE genisis.McoFee_Update
    @index int, @effective datetime, @adult decimal(19, 4), @child decimal(19, 4), @suppAdult decimal(19, 4),
    @suppChild decimal(19, 4), @coveredIdCharge nvarchar(4000), @userCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.MCO WITH (UPDLOCK, HOLDLOCK) WHERE MCOCode = @index)
    BEGIN SELECT 1 AS Result; RETURN; END
    IF EXISTS (
        SELECT 1 FROM dbo.MCO o WITH (UPDLOCK, HOLDLOCK) JOIN dbo.MCO t ON t.MCOCode = @index
        WHERE o.MCOCode <> @index AND o.PLNCode = t.PLNCode AND o.HLTCode = t.HLTCode AND o.INSCode = t.INSCode
          AND o.SuppMCOStatus = t.SuppMCOStatus AND o.MCOEffDate = @effective
          AND (t.MCOEffDate IS NULL OR t.MCOEffDate <> @effective))
    BEGIN SELECT 2 AS Result; RETURN; END

    UPDATE dbo.MCO SET MCOEffDate = @effective, MCOAmountAdult = @adult, MCOAmountChild = @child,
        MCOSuppAdultAmt = CASE WHEN SuppMCOStatus IN ('B', 'C', 'D') AND INSCode IN ('F', 'H') THEN @suppAdult ELSE MCOSuppAdultAmt END,
        MCOSuppChildAmt = CASE WHEN SuppMCOStatus IN ('B', 'C', 'D') AND INSCode IN ('F', 'H') THEN @suppChild ELSE MCOSuppChildAmt END,
        CovIDChrg = CASE WHEN SuppMCOStatus = 'D' AND INSCode IN ('F', 'H') THEN @coveredIdCharge ELSE CovIDChrg END,
        MCOLastUpdateUser = @userCode, MCOLastUpdateDate = CAST(CAST(GETDATE() AS date) AS datetime)
    WHERE MCOCode = @index;
    SELECT 0 AS Result;
END
GO

-- Result: 0 deleted, 1 record not found, 2 a member is on the row's plan / health / insured.
CREATE OR ALTER PROCEDURE genisis.McoFee_Delete
    @index int
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.MCO WITH (UPDLOCK, HOLDLOCK) WHERE MCOCode = @index)
    BEGIN SELECT 1 AS Result; RETURN; END
    IF EXISTS (
        SELECT 1 FROM dbo.MBM m JOIN dbo.MCO t ON t.MCOCode = @index
        WHERE m.PLNCode = t.PLNCode AND m.HLTCode = t.HLTCode AND m.INSCode = t.INSCode)
    BEGIN SELECT 2 AS Result; RETURN; END
    DELETE FROM dbo.MCO WHERE MCOCode = @index;
    SELECT 0 AS Result;
END
GO
