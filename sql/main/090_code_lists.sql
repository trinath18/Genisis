-- Maintenance > Plan: Health Type, Insured Type, Product Category, Supp. MCO Status and Special Note tabs.
-- *_Save: @update 0 adds, 1 changes the description. Result: 0 saved, 1 record not found, 2 code already exists.
-- *_Delete: Result 0 deleted, 1 record not found, 2 still in use (UsedIn names the table, as in the desktop message).

CREATE OR ALTER PROCEDURE genisis.HealthType_Search
    @code nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(HLTCode) AS Code, RTRIM(HLTDescription) AS Description FROM dbo.HLT
    WHERE @code IS NULL OR HLTCode = @code ORDER BY HLTCode;
END
GO

CREATE OR ALTER PROCEDURE genisis.HealthType_Save
    @code nvarchar(4000), @description nvarchar(4000), @update bit, @userCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today datetime = CAST(CAST(GETDATE() AS date) AS datetime);
    DECLARE @exists bit = CASE WHEN EXISTS (SELECT 1 FROM dbo.HLT WITH (UPDLOCK, HOLDLOCK) WHERE HLTCode = @code) THEN 1 ELSE 0 END;
    IF @update = 1 AND @exists = 0 BEGIN SELECT 1 AS Result; RETURN; END
    IF @update = 0 AND @exists = 1 BEGIN SELECT 2 AS Result; RETURN; END
    IF @update = 1
        UPDATE dbo.HLT SET HLTDescription = @description, HLTLastUpdateUser = @userCode, HLTLastUpdateDate = @today WHERE HLTCode = @code;
    ELSE
        INSERT INTO dbo.HLT (HLTCode, HLTDescription, HLTLastUpdateUser, HLTLastUpdateDate) VALUES (@code, @description, @userCode, @today);
    SELECT 0 AS Result;
END
GO

-- Desktop DeleteHLT checks, in its order; Annual Limit is checked as well.
CREATE OR ALTER PROCEDURE genisis.HealthType_Delete
    @code nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.HLT WITH (UPDLOCK, HOLDLOCK) WHERE HLTCode = @code)
    BEGIN SELECT 1 AS Result, CAST(NULL AS nvarchar(20)) AS UsedIn; RETURN; END
    DECLARE @usedIn nvarchar(20) = CASE
        WHEN EXISTS (SELECT 1 FROM dbo.MBM WHERE HLTCode = @code) THEN N'Member'
        WHEN EXISTS (SELECT 1 FROM dbo.PLN WHERE HLTCode = @code) THEN N'Plan'
        WHEN EXISTS (SELECT 1 FROM dbo.AGE WHERE HLTCode = @code) THEN N'Age'
        WHEN EXISTS (SELECT 1 FROM dbo.BNF WHERE HLTCode = @code) THEN N'Benefit'
        WHEN EXISTS (SELECT 1 FROM dbo.MCO WHERE HLTCode = @code) THEN N'MCO'
        WHEN EXISTS (SELECT 1 FROM dbo.PRM WHERE HLTCode = @code) THEN N'Premium'
        WHEN EXISTS (SELECT 1 FROM dbo.PRV WHERE HLTCode = @code) THEN N'PRV'
        WHEN EXISTS (SELECT 1 FROM dbo.AnnualLimit WHERE HLTCode = @code) THEN N'Annual Limit'
    END;
    IF @usedIn IS NOT NULL BEGIN SELECT 2 AS Result, @usedIn AS UsedIn; RETURN; END
    DELETE FROM dbo.HLT WHERE HLTCode = @code;
    SELECT 0 AS Result, CAST(NULL AS nvarchar(20)) AS UsedIn;
END
GO

CREATE OR ALTER PROCEDURE genisis.InsuredType_Search
    @code nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(INSCode) AS Code, RTRIM(INSDescription) AS Description FROM dbo.INS
    WHERE @code IS NULL OR INSCode = @code ORDER BY INSCode;
END
GO

CREATE OR ALTER PROCEDURE genisis.InsuredType_Save
    @code nvarchar(4000), @description nvarchar(4000), @update bit, @userCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today datetime = CAST(CAST(GETDATE() AS date) AS datetime);
    DECLARE @exists bit = CASE WHEN EXISTS (SELECT 1 FROM dbo.INS WITH (UPDLOCK, HOLDLOCK) WHERE INSCode = @code) THEN 1 ELSE 0 END;
    IF @update = 1 AND @exists = 0 BEGIN SELECT 1 AS Result; RETURN; END
    IF @update = 0 AND @exists = 1 BEGIN SELECT 2 AS Result; RETURN; END
    IF @update = 1
        UPDATE dbo.INS SET INSDescription = @description, INSLastUpdateUser = @userCode, INSLastUpdateDate = @today WHERE INSCode = @code;
    ELSE
        INSERT INTO dbo.INS (INSCode, INSDescription, INSLastUpdateUser, INSLastUpdateDate) VALUES (@code, @description, @userCode, @today);
    SELECT 0 AS Result;
END
GO

-- Desktop DeleteINS checks, in its order; Annual Limit is checked as well.
CREATE OR ALTER PROCEDURE genisis.InsuredType_Delete
    @code nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.INS WITH (UPDLOCK, HOLDLOCK) WHERE INSCode = @code)
    BEGIN SELECT 1 AS Result, CAST(NULL AS nvarchar(20)) AS UsedIn; RETURN; END
    DECLARE @usedIn nvarchar(20) = CASE
        WHEN EXISTS (SELECT 1 FROM dbo.BNF WHERE INSCode = @code) THEN N'Benefit'
        WHEN EXISTS (SELECT 1 FROM dbo.MCO WHERE INSCode = @code) THEN N'MCO'
        WHEN EXISTS (SELECT 1 FROM dbo.MBM WHERE INSCode = @code) THEN N'MBM'
        WHEN EXISTS (SELECT 1 FROM genisis.Maint_MBMSEQ WHERE INSCode = @code) THEN N'MBMSEQ'
        WHEN EXISTS (SELECT 1 FROM dbo.PRM WHERE INSCode = @code) THEN N'PRM'
        WHEN EXISTS (SELECT 1 FROM dbo.PRV WHERE INSCode = @code) THEN N'PRV'
        WHEN EXISTS (SELECT 1 FROM dbo.AnnualLimit WHERE INSCode = @code) THEN N'Annual Limit'
    END;
    IF @usedIn IS NOT NULL BEGIN SELECT 2 AS Result, @usedIn AS UsedIn; RETURN; END
    DELETE FROM dbo.INS WHERE INSCode = @code;
    SELECT 0 AS Result, CAST(NULL AS nvarchar(20)) AS UsedIn;
END
GO

CREATE OR ALTER PROCEDURE genisis.ProductCategory_Search
    @code nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(ProductCode) AS Code, RTRIM(ProductName) AS Description FROM genisis.Maint_ProductCategory
    WHERE @code IS NULL OR ProductCode = @code ORDER BY ProductCode;
END
GO

CREATE OR ALTER PROCEDURE genisis.ProductCategory_Save
    @code nvarchar(4000), @description nvarchar(4000), @update bit, @userCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @exists bit = CASE WHEN EXISTS (SELECT 1 FROM genisis.Maint_ProductCategory WITH (UPDLOCK, HOLDLOCK) WHERE ProductCode = @code) THEN 1 ELSE 0 END;
    IF @update = 1 AND @exists = 0 BEGIN SELECT 1 AS Result; RETURN; END
    IF @update = 0 AND @exists = 1 BEGIN SELECT 2 AS Result; RETURN; END
    IF @update = 1
        UPDATE genisis.Maint_ProductCategory SET ProductName = @description WHERE ProductCode = @code;
    ELSE
        INSERT INTO genisis.Maint_ProductCategory (ProductCode, ProductName) VALUES (@code, @description);
    SELECT 0 AS Result;
END
GO

-- The desktop deletes without a check; the web refuses while a plan uses the category.
CREATE OR ALTER PROCEDURE genisis.ProductCategory_Delete
    @code nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM genisis.Maint_ProductCategory WITH (UPDLOCK, HOLDLOCK) WHERE ProductCode = @code)
    BEGIN SELECT 1 AS Result, CAST(NULL AS nvarchar(20)) AS UsedIn; RETURN; END
    IF EXISTS (SELECT 1 FROM dbo.PLN WHERE PLNProCat = @code) BEGIN SELECT 2 AS Result, N'Plan' AS UsedIn; RETURN; END
    DELETE FROM genisis.Maint_ProductCategory WHERE ProductCode = @code;
    SELECT 0 AS Result, CAST(NULL AS nvarchar(20)) AS UsedIn;
END
GO

CREATE OR ALTER PROCEDURE genisis.SuppMcoStatus_Search
    @code nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(MCOCode) AS Code, RTRIM(MCOStatus) AS Description FROM genisis.Maint_SuppMCOStatus
    WHERE @code IS NULL OR MCOCode = @code ORDER BY MCOCode;
END
GO

CREATE OR ALTER PROCEDURE genisis.SuppMcoStatus_Save
    @code nvarchar(4000), @description nvarchar(4000), @update bit, @userCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @exists bit = CASE WHEN EXISTS (SELECT 1 FROM genisis.Maint_SuppMCOStatus WITH (UPDLOCK, HOLDLOCK) WHERE MCOCode = @code) THEN 1 ELSE 0 END;
    IF @update = 1 AND @exists = 0 BEGIN SELECT 1 AS Result; RETURN; END
    IF @update = 0 AND @exists = 1 BEGIN SELECT 2 AS Result; RETURN; END
    IF @update = 1
        UPDATE genisis.Maint_SuppMCOStatus SET MCOStatus = @description WHERE MCOCode = @code;
    ELSE
        INSERT INTO genisis.Maint_SuppMCOStatus (MCOCode, MCOStatus) VALUES (@code, @description);
    SELECT 0 AS Result;
END
GO

-- The desktop deletes without a check; the web refuses while MCO fees or annual limits use the status.
CREATE OR ALTER PROCEDURE genisis.SuppMcoStatus_Delete
    @code nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM genisis.Maint_SuppMCOStatus WITH (UPDLOCK, HOLDLOCK) WHERE MCOCode = @code)
    BEGIN SELECT 1 AS Result, CAST(NULL AS nvarchar(20)) AS UsedIn; RETURN; END
    DECLARE @usedIn nvarchar(20) = CASE
        WHEN EXISTS (SELECT 1 FROM dbo.MCO WHERE SuppMcoStatus = @code) THEN N'MCO'
        WHEN EXISTS (SELECT 1 FROM dbo.AnnualLimit WHERE SuppLimitStatus = @code) THEN N'Annual Limit'
    END;
    IF @usedIn IS NOT NULL BEGIN SELECT 2 AS Result, @usedIn AS UsedIn; RETURN; END
    DELETE FROM genisis.Maint_SuppMCOStatus WHERE MCOCode = @code;
    SELECT 0 AS Result, CAST(NULL AS nvarchar(20)) AS UsedIn;
END
GO

-- Special Note tab: the note is stored on the plan row (PLN.PLNSpecialNote). @group is a LIKE prefix escaped by the caller.
CREATE OR ALTER PROCEDURE genisis.SpecialNote_Search
    @top int, @plan nvarchar(4000) = NULL, @group nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@top) PLNIndex AS [Index], RTRIM(PLNCode) AS PlanCode, RTRIM(PLNDescription) AS Description,
        RTRIM(GRPCompany) AS GroupCompany, PLNSpecialNote AS Note
    FROM dbo.PLN
    WHERE (@plan IS NULL OR PLNCode = @plan) AND (@group IS NULL OR GRPCompany LIKE @group + N'%')
    ORDER BY PLNIndex DESC
    OPTION (RECOMPILE);
END
GO

-- Sets (or with NULL clears) one plan row's special note. Result: 0 saved, 1 record not found.
CREATE OR ALTER PROCEDURE genisis.SpecialNote_Save
    @index int, @note nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.PLN SET PLNSpecialNote = @note WHERE PLNIndex = @index;
    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END AS Result;
END
GO
