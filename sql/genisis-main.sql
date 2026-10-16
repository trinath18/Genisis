-- GENERATED FILE: built from sql/main/*.sql. Edit those files, then rebuild this one with
--     GENISIS_WRITE_SQL=1 dotnet test --filter Combined_scripts_are_up_to_date
-- How to run: open this file in SSMS, pick the main database in the "Available Databases" box,
-- check @MaintenanceDb in the first section, then press F5 (Execute).
-- Safe to run again: it only creates or replaces genisis.* procedures and synonyms; tables and data are not changed.

-- ===== main/000_setup.sql =====
-- Setup for the Genesis stored procedures. Run on the main database (the one that has dbo.PLN and dbo.MBM).
-- Creates the genisis schema and the synonyms the procedures use to read the maintenance database.
-- If your maintenance, clinical or worksheet database has another name, change @MaintenanceDb / @CisDb / @WorksheetDb below before running.
SET NOEXEC OFF;
SET NOCOUNT ON;
DECLARE @MaintenanceDb sysname = N'HISMaintenance';
DECLARE @CisDb sysname = N'CISDB';
DECLARE @WorksheetDb sysname = N'HISWorkSheet';

DECLARE @major int = ISNULL(CAST(SERVERPROPERTY('ProductMajorVersion') AS int), 0);
DECLARE @build int = ISNULL(CAST(SERVERPROPERTY('ProductBuild') AS int), 0);
DECLARE @version nvarchar(128) = CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128));
IF @major < 13 OR (@major = 13 AND @build < 4001)
BEGIN
    RAISERROR(N'STOPPED: this script needs SQL Server 2016 SP1 or later, but this server is version %s. Run it on a newer SQL Server.', 16, 1, @version);
    SET NOEXEC ON;
END
ELSE IF OBJECT_ID(N'dbo.PLN', N'U') IS NULL OR OBJECT_ID(N'dbo.MBM', N'U') IS NULL
BEGIN
    DECLARE @current sysname = DB_NAME();
    RAISERROR(N'STOPPED: database "%s" is not the main database (dbo.PLN / dbo.MBM not found). In SSMS, pick the main database in the "Available Databases" box on the toolbar and run this script again.', 16, 1, @current);
    SET NOEXEC ON;
END
ELSE IF DB_ID(@MaintenanceDb) IS NULL OR OBJECT_ID(QUOTENAME(@MaintenanceDb) + N'.dbo.ProductCategory', N'U') IS NULL
BEGIN
    RAISERROR(N'STOPPED: maintenance database "%s" was not found on this server (or it has no dbo.ProductCategory). Change @MaintenanceDb at the top of this script to your maintenance database name and run it again.', 16, 1, @MaintenanceDb);
    SET NOEXEC ON;
END
ELSE IF DB_ID(@CisDb) IS NULL OR OBJECT_ID(QUOTENAME(@CisDb) + N'.dbo.CISCas', N'U') IS NULL
BEGIN
    RAISERROR(N'STOPPED: clinical database "%s" was not found on this server (or it has no dbo.CISCas). Change @CisDb at the top of this script to your clinical (CIS) database name and run it again.', 16, 1, @CisDb);
    SET NOEXEC ON;
END
ELSE IF DB_ID(@WorksheetDb) IS NULL OR OBJECT_ID(QUOTENAME(@WorksheetDb) + N'.dbo.VIEW_Account_History', N'V') IS NULL
BEGIN
    RAISERROR(N'STOPPED: worksheet database "%s" was not found on this server (or it has no dbo.VIEW_Account_History). Change @WorksheetDb at the top of this script to your worksheet database name and run it again.', 16, 1, @WorksheetDb);
    SET NOEXEC ON;
END

IF SCHEMA_ID(N'genisis') IS NULL EXEC (N'CREATE SCHEMA genisis AUTHORIZATION dbo');

DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'IF OBJECT_ID(N''genisis.' + s + N''', N''SN'') IS NOT NULL DROP SYNONYM genisis.' + s + N';
CREATE SYNONYM genisis.' + s + N' FOR ' + QUOTENAME(@MaintenanceDb) + N'.dbo.' + t + N';
'
FROM (VALUES (N'Maint_ProductCategory', N'ProductCategory'), (N'Maint_PolicyCategory', N'PolicyCategory'),
    (N'Maint_MBMCrossReference', N'MBMCrossReference'), (N'Maint_MBMSEQ05', N'MBMSEQ05'),
    (N'Maint_MBMSEQRenew05', N'MBMSEQRenew05'), (N'Maint_MBMSEQRenewPol', N'MBMSEQRenewPol'),
    (N'Maint_MBMSEQRenewHistory', N'MBMSEQRenewHistory'), (N'Maint_CTY', N'CTY'), (N'Maint_SuppMCOStatus', N'SuppMCOStatus'), (N'Maint_MBMSEQ', N'MBMSEQ'), (N'Maint_CaseMgmtFee', N'CaseMgmtFee'),
    (N'Maint_MMAProcedures', N'MMAProcedures'), (N'Maint_MMAFees', N'MMAFees'), (N'Maint_MMASchedule', N'VIEW_MMA_Schedule'),
    (N'Maint_DiscPlan', N'DiscPlan'), (N'Maint_MCOPending', N'MCOPending'), (N'Maint_CASCrossReference', N'CASCrossReference'),
    (N'Maint_Renewal', N'Renewal')) v (s, t);
SELECT @sql += N'IF OBJECT_ID(N''genisis.' + s + N''', N''SN'') IS NOT NULL DROP SYNONYM genisis.' + s + N';
CREATE SYNONYM genisis.' + s + N' FOR ' + QUOTENAME(@CisDb) + N'.dbo.' + t + N';
'
FROM (VALUES (N'Cis_PLN', N'PLN'), (N'Cis_AnnualLimit', N'AnnualLimit'), (N'Cis_PRM', N'PRM'), (N'Cis_MCO', N'MCO'),
    (N'Cis_VisitLimit', N'VisitLimit'), (N'Cis_CISCas', N'CISCas')) v (s, t);
SELECT @sql += N'IF OBJECT_ID(N''genisis.WS_AccountHistory'', N''SN'') IS NOT NULL DROP SYNONYM genisis.WS_AccountHistory;
CREATE SYNONYM genisis.WS_AccountHistory FOR ' + QUOTENAME(@WorksheetDb) + N'.dbo.VIEW_Account_History;
';
EXEC (@sql);
PRINT N'genisis schema and synonyms ready (maintenance database: ' + @MaintenanceDb + N', clinical database: ' + @CisDb
    + N', worksheet database: ' + @WorksheetDb + N').';
GO

-- ===== main/010_plan.sql =====
-- Maintenance > Plan, Plan tab.

CREATE OR ALTER PROCEDURE genisis.PlanMaintenance_Lookups
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(HLTCode) AS Code, RTRIM(HLTDescription) AS Name FROM dbo.HLT ORDER BY HLTCode;
    SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name FROM dbo.PAY WHERE PAYCode IS NOT NULL ORDER BY PAYCode;
    SELECT RTRIM(ProductCode) AS Code, RTRIM(ProductName) AS Name FROM genisis.Maint_ProductCategory ORDER BY ProductCode;
    SELECT RTRIM(PolicyWording) AS PolicyWording FROM genisis.Maint_PolicyCategory ORDER BY PolicyWording;
END
GO

CREATE OR ALTER PROCEDURE genisis.Plan_ReferencesExist
    @health nvarchar(4000), @payor nvarchar(4000), @productCategory nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.HLT WHERE HLTCode = @health) THEN 1 ELSE 0 END AS bit) AS HealthExists,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.PAY WHERE PAYCode = @payor) THEN 1 ELSE 0 END AS bit) AS PayorExists,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM genisis.Maint_ProductCategory WHERE ProductCode = @productCategory) THEN 1 ELSE 0 END AS bit) AS ProductCategoryExists;
END
GO

-- Legacy GeneratePLNSeq: per-letter counter in PLNSeq ("A001".."A999"). Returns the next number;
-- a result above 999 means the letter is used up and nothing was changed.
CREATE OR ALTER PROCEDURE genisis.Plan_NextCode
    @letter nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @index int, @seq nvarchar(3);
    SELECT TOP 1 @index = PLNIndex, @seq = PLNNoSeq FROM dbo.PLNSeq WITH (UPDLOCK, HOLDLOCK) WHERE PLNSeqCode = @letter ORDER BY PLNIndex;
    IF @index IS NULL
    BEGIN
        INSERT INTO dbo.PLNSeq (PLNNoSeq, PLNSeqCode) VALUES (N'001', @letter);
        SELECT 1 AS NextNumber;
        RETURN;
    END
    DECLARE @next int = ISNULL(TRY_CAST(LTRIM(RTRIM(@seq)) AS int), 0) + 1;
    IF @next <= 999
        UPDATE dbo.PLNSeq SET PLNNoSeq = RIGHT(N'00' + CAST(@next AS nvarchar(3)), 3) WHERE PLNIndex = @index;
    SELECT @next AS NextNumber;
END
GO

CREATE OR ALTER PROCEDURE genisis.Plan_Insert
    @code nvarchar(4000), @description nvarchar(4000), @health nvarchar(4000), @payor nvarchar(4000), @groupCompany nvarchar(4000),
    @topUp nvarchar(4000), @annualLimit nvarchar(4000), @premium nvarchar(4000), @mco nvarchar(4000), @coPayment nvarchar(4000),
    @meal nvarchar(4000), @nursing nvarchar(4000), @tax nvarchar(4000), @mri nvarchar(4000), @sof nvarchar(4000), @smPlan nvarchar(4000),
    @gracePeriod nvarchar(4000), @lifetime nvarchar(4000), @productCategory nvarchar(4000), @graceDays decimal(18, 0), @effective datetime,
    @managementFee nvarchar(4000), @userCode nvarchar(4000), @disIndicator nvarchar(4000), @startAge int, @endAge int,
    @policyWording nvarchar(4000), @clientPlan nvarchar(4000), @clientPolicyNo nvarchar(4000), @coPayPercent int,
    @lgGov nvarchar(4000), @lgPrivate int, @excessPrivate nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.PLN (PLNCode, PLNDescription, HLTCode, PAYCode, GRPCompany, PLNTopUPStatus, AnnLmtIND, PRMInd, MCOInd,
        CoPaymentInd, PLNMeal, PLNNursing, PLNTax, PLNMRI, PLNSOF, NewSMPlan, SpeGPeriodStatus, PLNLifeTimeStatus, PLNProCat,
        SpeGPeriodDays, PLNEffDate, PLNMgmtFee, PLNLastUpdateUser, PLNLastUpdateDate, PLNDisIndicator, PLNStartAge, PLNEndAge,
        PLNPolicyWording, PLNPayorPlan, PLNPolNo, PLNCoPayPerc, PLNLGGov, PLNLGPrivate, PLNExcessPrivate)
    VALUES (@code, @description, @health, @payor, @groupCompany, @topUp, @annualLimit, @premium, @mco,
        @coPayment, @meal, @nursing, @tax, @mri, @sof, @smPlan, @gracePeriod, @lifetime, @productCategory,
        @graceDays, @effective, @managementFee, @userCode, CAST(CAST(GETDATE() AS date) AS datetime), @disIndicator, @startAge, @endAge,
        @policyWording, @clientPlan, @clientPolicyNo, @coPayPercent, @lgGov, @lgPrivate, @excessPrivate);
    SELECT CAST(SCOPE_IDENTITY() AS int) AS PlanIndex;
END
GO

-- Edits one plan row in place. Plan code, health type and payor identify the plan for members, premiums and limits,
-- so they must match the stored row. Result: 0 updated, 1 record not found, 2 code / health type / payor differ.
CREATE OR ALTER PROCEDURE genisis.Plan_Update
    @index int, @health nvarchar(4000), @payor nvarchar(4000), @code nvarchar(4000) = NULL, @description nvarchar(4000),
    @groupCompany nvarchar(4000), @topUp nvarchar(4000), @annualLimit nvarchar(4000), @premium nvarchar(4000), @mco nvarchar(4000),
    @coPayment nvarchar(4000), @meal nvarchar(4000), @nursing nvarchar(4000), @tax nvarchar(4000), @mri nvarchar(4000),
    @sof nvarchar(4000), @smPlan nvarchar(4000), @gracePeriod nvarchar(4000), @lifetime nvarchar(4000),
    @productCategory nvarchar(4000), @graceDays decimal(18, 0), @effective datetime, @managementFee nvarchar(4000),
    @userCode nvarchar(4000), @disIndicator nvarchar(4000), @startAge int, @endAge int, @policyWording nvarchar(4000),
    @clientPlan nvarchar(4000), @clientPolicyNo nvarchar(4000), @coPayPercent int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @storedCode nvarchar(4000), @storedHealth nvarchar(4000), @storedPayor nvarchar(4000);
    SELECT @storedCode = RTRIM(PLNCode), @storedHealth = RTRIM(HLTCode), @storedPayor = ISNULL(RTRIM(PAYCode), N'')
    FROM dbo.PLN WITH (UPDLOCK, HOLDLOCK) WHERE PLNIndex = @index;
    IF @@ROWCOUNT = 0 BEGIN SELECT 1 AS Result; RETURN; END
    IF @storedHealth <> @health OR @storedPayor <> @payor OR (@code IS NOT NULL AND @storedCode <> @code)
    BEGIN SELECT 2 AS Result; RETURN; END
    UPDATE dbo.PLN SET PLNDescription = @description, GRPCompany = @groupCompany, PLNTopUPStatus = @topUp, AnnLmtIND = @annualLimit,
        PRMInd = @premium, MCOInd = @mco, CoPaymentInd = @coPayment, PLNMeal = @meal, PLNNursing = @nursing, PLNTax = @tax,
        PLNMRI = @mri, PLNSOF = @sof, NewSMPlan = @smPlan, SpeGPeriodStatus = @gracePeriod, PLNLifeTimeStatus = @lifetime,
        PLNProCat = @productCategory, SpeGPeriodDays = @graceDays, PLNEffDate = @effective, PLNMgmtFee = @managementFee,
        PLNLastUpdateUser = @userCode, PLNLastUpdateDate = CAST(CAST(GETDATE() AS date) AS datetime), PLNDisIndicator = @disIndicator,
        PLNStartAge = @startAge, PLNEndAge = @endAge, PLNPolicyWording = @policyWording, PLNPayorPlan = @clientPlan,
        PLNPolNo = @clientPolicyNo, PLNCoPayPerc = @coPayPercent
    WHERE PLNIndex = @index;
    SELECT 0 AS Result;
END
GO

-- @group and @plan are LIKE patterns already escaped by the caller.
CREATE OR ALTER PROCEDURE genisis.Plan_Search
    @top int, @health nvarchar(4000) = NULL, @payor nvarchar(4000) = NULL, @group nvarchar(4000) = NULL, @plan nvarchar(4000) = NULL,
    @productCategory nvarchar(4000) = NULL, @topUp nvarchar(4000) = NULL, @coPayment nvarchar(4000) = NULL, @sof nvarchar(4000) = NULL,
    @gracePeriod nvarchar(4000) = NULL, @meal nvarchar(4000) = NULL, @nursing nvarchar(4000) = NULL, @tax nvarchar(4000) = NULL,
    @mri nvarchar(4000) = NULL, @disIndicator nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@top) PLNIndex AS [Index], RTRIM(HLTCode) AS HealthCode, RTRIM(PLNCode) AS Code, RTRIM(NewPLNCode) AS NewCode,
        PLNDescription AS Description, RTRIM(PAYCode) AS PayorCode, GRPCompany AS GroupCompany, PLNTopUPStatus AS TopUpStatus,
        AnnLmtIND AS AnnualLimitInd, PRMInd AS PremiumInd, MCOInd AS McoInd, CoPaymentInd AS CoPayment, PLNEffDate AS EffectiveDate,
        SpeGPeriodStatus AS SpecialGracePeriod, SpeGPeriodDays AS SpecialGracePeriodDays, NewSMPlan AS SmPlan,
        PLNLifeTimeStatus AS LifetimeStatus, PLNSOF AS Sof, RTRIM(PLNProCat) AS ProductCategory, PLNMeal AS Meal, PLNNursing AS Nursing,
        PLNTax AS Tax, PLNMRI AS Mri, PLNMgmtFee AS ManagementFee, PLNDisIndicator AS DisIndicator, PLNStartAge AS StartAge,
        PLNEndAge AS EndAge, PLNPolicyWording AS PolicyWording, PLNPayorPlan AS ClientPlan, PLNPolNo AS ClientPolicyNo,
        PLNCoPayPerc AS CoPayPercent
    FROM dbo.PLN
    WHERE (@health IS NULL OR HLTCode = @health)
      AND (@payor IS NULL OR PAYCode = @payor)
      AND (@group IS NULL OR GRPCompany LIKE N'%' + @group + N'%')
      AND (@plan IS NULL OR PLNCode LIKE N'%' + @plan + N'%')
      AND (@productCategory IS NULL OR PLNProCat = @productCategory)
      AND (@topUp IS NULL OR PLNTopUPStatus = @topUp)
      AND (@coPayment IS NULL OR CoPaymentInd = @coPayment)
      AND (@sof IS NULL OR PLNSOF = @sof)
      AND (@gracePeriod IS NULL OR SpeGPeriodStatus = @gracePeriod)
      AND (@meal IS NULL OR PLNMeal = @meal)
      AND (@nursing IS NULL OR PLNNursing = @nursing)
      AND (@tax IS NULL OR PLNTax = @tax)
      AND (@mri IS NULL OR PLNMRI = @mri)
      AND (@disIndicator IS NULL OR PLNDisIndicator = @disIndicator)
    ORDER BY PLNIndex DESC
    OPTION (RECOMPILE);
END
GO

-- Result: 0 deleted, 1 record not found, 2 a member is on this plan code.
CREATE OR ALTER PROCEDURE genisis.Plan_Delete
    @index int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @code nvarchar(4);
    SELECT @code = PLNCode FROM dbo.PLN WITH (UPDLOCK) WHERE PLNIndex = @index;
    IF @@ROWCOUNT = 0 BEGIN SELECT 1 AS Result; RETURN; END
    IF EXISTS (SELECT 1 FROM dbo.MBM WHERE PLNCode = @code) BEGIN SELECT 2 AS Result; RETURN; END
    DELETE FROM dbo.PLN WHERE PLNIndex = @index;
    SELECT 0 AS Result;
END
GO

-- ===== main/020_annual_limit.sql =====
-- Maintenance > Plan, Annual Limit tab.

CREATE OR ALTER PROCEDURE genisis.Lookup_InsuredTypes
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(INSCode) AS Code, RTRIM(INSDescription) AS Name FROM dbo.INS ORDER BY INSCode;
END
GO

CREATE OR ALTER PROCEDURE genisis.Plan_IsLifetime
    @plan nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.PLN WHERE PLNCode = @plan AND PLNLifeTimeStatus = 'Y') THEN 1 ELSE 0 END AS bit) AS IsLifetime;
END
GO

-- @index NULL = new row. Result: 0 saved, 1 record not found, 2 another row has the same INS/HLT/PLN.
-- An edit that keeps its own key is allowed, so rows that are already duplicated can still be maintained.
-- Status A leaves any existing supplementary limits untouched on update, like the desktop.
CREATE OR ALTER PROCEDURE genisis.AnnualLimit_Save
    @index int = NULL, @plan nvarchar(4000), @ins nvarchar(4000), @hlt nvarchar(4000), @pay nvarchar(4000), @grp nvarchar(4000),
    @annual decimal(19, 4), @lifetime decimal(19, 4), @status nvarchar(4000), @supp decimal(19, 4), @suppLifetime decimal(19, 4),
    @effective datetime, @version int, @userCode nvarchar(4000), @disability decimal(19, 4)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today datetime = CAST(CAST(GETDATE() AS date) AS datetime);
    IF @index IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.AnnualLimit WITH (UPDLOCK, HOLDLOCK) WHERE AnnualIndex = @index)
    BEGIN SELECT 1 AS Result, @index AS AnnualIndex; RETURN; END

    IF EXISTS (
        SELECT 1 FROM dbo.AnnualLimit WITH (UPDLOCK, HOLDLOCK)
        WHERE INSCode = @ins AND HLTCode = @hlt AND PLNCode = @plan
          AND (@index IS NULL OR (AnnualIndex <> @index AND NOT EXISTS (
              SELECT 1 FROM dbo.AnnualLimit c WHERE c.AnnualIndex = @index AND c.INSCode = @ins AND c.HLTCode = @hlt AND c.PLNCode = @plan))))
    BEGIN SELECT 2 AS Result, @index AS AnnualIndex; RETURN; END

    IF @index IS NULL
    BEGIN
        INSERT INTO dbo.AnnualLimit (PLNCode, INSCode, HLTCode, PAYCode, GRPCompany, AnnLimit, LifeTimeLimit, SuppLimitStatus,
            SuppLimit, SuppLifeTimeLimit, AnnualEffDate, AnnualLimitVersion, AnnualLastUpdatedUser, AnnualLastUpdatedDate, DisabilityLmt)
        VALUES (@plan, @ins, @hlt, @pay, @grp, @annual, @lifetime, @status,
            CASE WHEN @status IN ('B', 'C') THEN @supp END, CASE WHEN @status IN ('B', 'C') THEN @suppLifetime END,
            @effective, @version, @userCode, @today, @disability);
        SELECT 0 AS Result, CAST(SCOPE_IDENTITY() AS int) AS AnnualIndex;
        RETURN;
    END

    UPDATE dbo.AnnualLimit SET PLNCode = @plan, INSCode = @ins, HLTCode = @hlt, PAYCode = @pay, GRPCompany = @grp, AnnLimit = @annual,
        LifeTimeLimit = @lifetime, SuppLimitStatus = @status,
        SuppLimit = CASE WHEN @status IN ('B', 'C') THEN @supp ELSE SuppLimit END,
        SuppLifeTimeLimit = CASE WHEN @status IN ('B', 'C') THEN @suppLifetime ELSE SuppLifeTimeLimit END,
        AnnualEffDate = @effective, AnnualLimitVersion = @version, AnnualLastUpdatedUser = @userCode,
        AnnualLastUpdatedDate = @today, DisabilityLmt = @disability
    WHERE AnnualIndex = @index;
    SELECT 0 AS Result, @index AS AnnualIndex;
END
GO

-- @group is a LIKE prefix already escaped by the caller.
CREATE OR ALTER PROCEDURE genisis.AnnualLimit_Search
    @top int, @plan nvarchar(4000) = NULL, @health nvarchar(4000) = NULL, @insured nvarchar(4000) = NULL,
    @payor nvarchar(4000) = NULL, @group nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@top) AnnualIndex AS [Index], RTRIM(PLNCode) AS PlanCode, RTRIM(INSCode) AS InsuredCode, RTRIM(HLTCode) AS HealthCode,
        RTRIM(PAYCode) AS PayorCode, GRPCompany AS GroupCompany, AnnLimit AS AnnualLimit, SuppLimit, AnnualEffDate AS EffectiveDate,
        SuppLimitStatus, AnnualLimitVersion AS Version, LifeTimeLimit AS LifetimeLimit, SuppLifeTimeLimit AS SuppLifetimeLimit,
        DisabilityLmt AS DisabilityLimit
    FROM dbo.AnnualLimit
    WHERE (@plan IS NULL OR PLNCode = @plan) AND (@health IS NULL OR HLTCode = @health) AND (@insured IS NULL OR INSCode = @insured)
      AND (@payor IS NULL OR PAYCode = @payor) AND (@group IS NULL OR GRPCompany LIKE @group + N'%')
    ORDER BY AnnualIndex DESC
    OPTION (RECOMPILE);
END
GO

-- Desktop DeleteAnnual. Result: 0 deleted, 1 record not found, 2 a member is on the row's plan code.
CREATE OR ALTER PROCEDURE genisis.AnnualLimit_Delete
    @index int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @plan nvarchar(4);
    SELECT @plan = PLNCode FROM dbo.AnnualLimit WITH (UPDLOCK) WHERE AnnualIndex = @index;
    IF @@ROWCOUNT = 0 BEGIN SELECT 1 AS Result; RETURN; END
    IF EXISTS (SELECT 1 FROM dbo.MBM WHERE PLNCode = @plan) BEGIN SELECT 2 AS Result; RETURN; END
    DELETE FROM dbo.AnnualLimit WHERE AnnualIndex = @index;
    SELECT 0 AS Result;
END
GO

-- ===== main/030_premium.sql =====
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

-- ===== main/040_membership.sql =====
-- Membership > Enquiry (read-only). SearchMBMCovPersonsProc, SearchCas and SearchCasHistory are existing procedures and are called as they are.

-- @by: number | name | ic | policy. @prefix is an escaped LIKE prefix ending in %.
CREATE OR ALTER PROCEDURE genisis.Membership_Search
    @max int, @by nvarchar(4000), @q nvarchar(4000), @prefix nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) m.MBMNumber, m.MBMPolicyNo, m.MBMCOVID, m.MBMIcBcPp, m.MBMName,
           m.MBMPayorEffDate, m.MBMPayorExpDate, m.MBMValueDate, m.MBMBordxDate, m.MBMDataStatus,
           CASE WHEN m.MBMStatus = 'C' AND m.MBMPayorEffDate > CAST(GETDATE() AS date) AND t.MBMCancelDate IS NULL
                THEN 'A' ELSE m.MBMStatus END AS MBMStatus,
           t.MBMRegUser, o.MBMGroupCompany, m.HSCLNInd
    FROM dbo.MBM m
    INNER JOIN dbo.MBMOthers o ON m.MBMNumber = o.MBMNumber
    INNER JOIN dbo.MBMTwo t ON m.MBMNumber = t.MBMNumber
    WHERE (@by = N'number' AND m.MBMNumber LIKE @prefix)
       OR (@by = N'name' AND m.MBMName LIKE @prefix)
       OR (@by = N'ic' AND (m.MBMIcBcPp = @q OR REPLACE(m.MBMIcBcPp, '-', '') = REPLACE(@q, '-', '')))
       OR (@by = N'policy' AND m.MBMPolicyNo LIKE @prefix)
    ORDER BY m.MBMNumber
    OPTION (RECOMPILE);
END
GO

-- Result sets: plan, annual limit in force on @effDate, clinic, benefit limits.
CREATE OR ALTER PROCEDURE genisis.Membership_Details
    @mbmNumber nvarchar(4000), @plnCode nvarchar(4000), @insCode nvarchar(4000), @hltCode nvarchar(4000), @effDate datetime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT PLNCode, PLNDescription, ProductName, PLNRBType, MessagePromt, PLNPolicyWording FROM dbo.PLN WHERE PLNCode = @plnCode;
    SELECT TOP (1) AnnLimit, DisabilityLmt, SuppLimitStatus, MajorMedicalLmt, AnnualEffDate
    FROM dbo.AnnualLimit
    WHERE PLNCode = @plnCode AND INSCode = @insCode AND HLTCode = @hltCode AND AnnualEffDate <= @effDate
    ORDER BY AnnualEffDate DESC;
    SELECT PLNType, INSType, MBMPolEffDate, MBMPolExpDate FROM dbo.MBMCLNOthers WHERE MBMNumber = @mbmNumber;
    SELECT MBMCoveredID, MBMCancerAnnLmt, MBMCancerBalLmt, MBMKidneyAnnLmt, MBMKidneyBalLmt, MBMHomeNCAnnLmt, MBMHomeNCBalLmt
    FROM dbo.MBMBnfLmt WHERE MBMNumber = @mbmNumber;
END
GO

CREATE OR ALTER PROCEDURE genisis.Membership_Adjustments
    @mbmNumber nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT MBMAmendID, MBMNumber, MBMCoverID, MBMAmendEdtType, MBMAmendDate, MBMAENDtEffDate,
           MBMAEndtBordxDate, MBMAEndtBatchNo, MBMAmendUser
    FROM dbo.MBMAmendHistory WHERE MBMNumber = @mbmNumber
    ORDER BY MBMAmendDate DESC, MBMAmendID DESC;
END
GO

CREATE OR ALTER PROCEDURE genisis.Membership_GetIc
    @mbmNumber nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT LTRIM(RTRIM(MBMIcBcPp)) AS IcNumber FROM dbo.MBM WHERE MBMNumber = @mbmNumber;
END
GO

-- Result sets: principal and supplementary memberships for the IC (dashes ignored).
CREATE OR ALTER PROCEDURE genisis.Membership_History
    @ic nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT TOP (200) x.MBMNumber, x.MBMPolicyNo, x.MBMIcBcPp, x.MBMName, x.MBMPayorEffDate, x.MBMPayorExpDate,
           x.INSCode, x.PLNCode, x.PAYCode, x.MBMCOVID, x.MBMDOB, m.MBMDataStatus,
           CASE WHEN m.MBMStatus = 'C' AND m.MBMPayorEffDate > CAST(GETDATE() AS date) AND t.MBMCancelDate IS NULL
                THEN 'A' ELSE m.MBMStatus END AS MBMStatus,
           x.MBMBordxDate, p.ProductName
    FROM genisis.Maint_MBMCrossReference x
    INNER JOIN dbo.MBM m ON x.MBMNumber = m.MBMNumber
    INNER JOIN dbo.MBMTwo t ON x.MBMNumber = t.MBMNumber
    LEFT JOIN dbo.PLN p ON p.PLNCode = x.PLNCode
    WHERE REPLACE(x.MBMIcBcPp, '-', '') = REPLACE(@ic, '-', '')
    ORDER BY x.MBMPayorEffDate DESC;

    SELECT DISTINCT TOP (100) c.MBMCName, c.MBMCICBCPPNo, c.MBMCCoverID AS MBMCOVID, c.MBMCDOB AS MBMDOB,
           COALESCE(c.MBMCEffDate, x.MBMPayorEffDate) AS MBMPayorEffDate,
           COALESCE(c.MBMCExpDate, x.MBMPayorExpDate) AS MBMPayorExpDate,
           x.MBMNumber, x.MBMPolicyNo, x.MBMName AS PrincipalName,
           CASE WHEN c.MBMCStatus = 'C' AND c.MBMCEffDate > CAST(GETDATE() AS date) AND c.MBMCCancelDate IS NULL
                THEN 'A' ELSE c.MBMCStatus END AS MBMStatus
    FROM dbo.MBMCoveredPersons c
    INNER JOIN genisis.Maint_MBMCrossReference x ON x.MBMNumber = c.MBMCNumber
    WHERE REPLACE(c.MBMCICBCPPNo, '-', '') = REPLACE(@ic, '-', '')
    ORDER BY MBMPayorEffDate DESC;
END
GO

CREATE OR ALTER PROCEDURE genisis.Membership_Account
    @mbmNumber nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.VIEW_MBMTotal_Summary WHERE MBMNumber = @mbmNumber ORDER BY MBMPatientCovID;
END
GO

-- Result sets: exclusions, remarks.
CREATE OR ALTER PROCEDURE genisis.Membership_Notes
    @ic nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT MBMNumber, MBMName, MBMPolicyNo, INSCode, PAYCode, MBMExclusion FROM dbo.View_Get_MemberExclusion WHERE MBMIcBcPp = @ic;
    SELECT MBMNumber, MBMName, INSCode, PAYCode, MBMTakeover, TakeOvermessageInd, MBMRemarks FROM dbo.View_Get_MemberRemarks WHERE MBMIcBcPp = @ic;
END
GO

-- ===== main/050_registration_numbers.sql =====
-- Membership > Registration: the counters behind membership numbers, check digits and security codes.
-- The API works out the next value (same rules as the desktop); these procedures read under lock and store it.
-- Call them inside the registration transaction so the UPDLOCK/HOLDLOCK read lock lasts until commit.

CREATE OR ALTER PROCEDURE genisis.Seq_PayorGet
    @payorCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT SEQNumber AS Text FROM genisis.Maint_MBMSEQ05 WITH (UPDLOCK, HOLDLOCK) WHERE PAYCode = @payorCode;
END
GO

CREATE OR ALTER PROCEDURE genisis.Seq_PayorSet
    @payorCode nvarchar(4000), @next nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.Maint_MBMSEQ05 SET SEQNumber = @next WHERE PAYCode = @payorCode;
    IF @@ROWCOUNT = 0 INSERT INTO genisis.Maint_MBMSEQ05 (PAYCode, SEQNumber) VALUES (@payorCode, @next);
END
GO

CREATE OR ALTER PROCEDURE genisis.Seq_CheckDigitsGet
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) MBMChkDigits AS Text FROM dbo.MBMCheckDigits WITH (UPDLOCK, HOLDLOCK);
END
GO

CREATE OR ALTER PROCEDURE genisis.Seq_CheckDigitsSet
    @next nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.MBMCheckDigits SET MBMChkDigits = @next;
END
GO

CREATE OR ALTER PROCEDURE genisis.Seq_SecurityGet
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) MBMSecurityCode AS Text FROM dbo.MBMSecurityDigits WITH (UPDLOCK, HOLDLOCK);
END
GO

CREATE OR ALTER PROCEDURE genisis.Seq_SecuritySet
    @next nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.MBMSecurityDigits SET MBMSecurityCode = @next;
END
GO

-- @byPolicy = 1 uses MBMSEQRenewPol (key payor*policy), otherwise MBMSEQRenew05 (key payor*insured*dob*name).
CREATE OR ALTER PROCEDURE genisis.Seq_RenewGet
    @byPolicy bit, @key nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    IF @byPolicy = 1
        SELECT INSCode, RewSeqNo, RewSeqID FROM genisis.Maint_MBMSEQRenewPol WITH (UPDLOCK, HOLDLOCK) WHERE RewMBMSeqKey = @key;
    ELSE
        SELECT INSCode, RewSeqNo, RewSeqID FROM genisis.Maint_MBMSEQRenew05 WITH (UPDLOCK, HOLDLOCK) WHERE RewMBMSeqKey = @key;
END
GO

CREATE OR ALTER PROCEDURE genisis.Seq_RenewInsert
    @byPolicy bit, @key nvarchar(4000), @insCode nvarchar(4000), @sequence nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    IF @byPolicy = 1
        INSERT INTO genisis.Maint_MBMSEQRenewPol (RewMBMSeqKey, INSCode, RewSeqNo, RewSeqID) VALUES (@key, @insCode, @sequence, '01');
    ELSE
        INSERT INTO genisis.Maint_MBMSEQRenew05 (RewMBMSeqKey, INSCode, RewSeqNo, RewSeqID) VALUES (@key, @insCode, @sequence, '01');
END
GO

-- @insCode NULL keeps the stored insured type.
CREATE OR ALTER PROCEDURE genisis.Seq_RenewUpdate
    @byPolicy bit, @key nvarchar(4000), @insCode nvarchar(4000) = NULL, @sequence nvarchar(4000), @seqId nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    IF @byPolicy = 1
        UPDATE genisis.Maint_MBMSEQRenewPol SET RewSeqID = @seqId, RewSeqNo = @sequence, INSCode = COALESCE(@insCode, INSCode) WHERE RewMBMSeqKey = @key;
    ELSE
        UPDATE genisis.Maint_MBMSEQRenew05 SET RewSeqID = @seqId, RewSeqNo = @sequence, INSCode = COALESCE(@insCode, INSCode) WHERE RewMBMSeqKey = @key;
END
GO

CREATE OR ALTER PROCEDURE genisis.Seq_RenewHistoryAppend
    @key nvarchar(4000), @entry nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.Maint_MBMSEQRenewHistory WITH (UPDLOCK, HOLDLOCK) SET SEQNo = SEQNo + '~' + @entry WHERE MBMKey = @key;
    IF @@ROWCOUNT = 0 INSERT INTO genisis.Maint_MBMSEQRenewHistory (MBMKey, SEQNo) VALUES (@key, @entry);
END
GO

-- ===== main/060_registration_lookups.sql =====
-- Membership > Registration: drop-down lists.

-- Result sets in order: health types, payors, insured types, races, nationalities, relationships, states, cities.
CREATE OR ALTER PROCEDURE genisis.Registration_Lookups
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(HLTCode) AS Code, RTRIM(HLTDescription) AS Name FROM dbo.HLT ORDER BY HLTCode;
    SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name, RTRIM(HLTCode) AS HealthCode FROM dbo.PAY ORDER BY PAYCode;
    SELECT RTRIM(INSCode) AS Code, RTRIM(INSDescription) AS Name FROM dbo.INS ORDER BY INSCode;
    SELECT RTRIM(RACCode) AS Code, RTRIM(RACName) AS Name FROM dbo.RAC WHERE RACCode IS NOT NULL ORDER BY RACCode;
    SELECT RTRIM(NATCode) AS Code, RTRIM(NATName) AS Name FROM dbo.NAT WHERE NATCode IS NOT NULL ORDER BY NATName;
    SELECT RTRIM(RELCode) AS Code, RTRIM(RELDescription) AS Name FROM dbo.REL WHERE RELCode IS NOT NULL ORDER BY RELCode;
    SELECT RTRIM(STACode) AS Code, RTRIM(STADescription) AS Name FROM dbo.STA WHERE STACode IS NOT NULL ORDER BY STADescription;
    SELECT RTRIM(CTYCode) AS Code, RTRIM(CTYDescription) AS Name FROM genisis.Maint_CTY WHERE CTYCode IS NOT NULL ORDER BY CTYDescription;
END
GO

-- Health type S plans are shared by all payors.
CREATE OR ALTER PROCEDURE genisis.Registration_Plans
    @healthCode nvarchar(4000), @payorCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(PLNCode) AS Code, MAX(RTRIM(PLNDescription)) AS Name
    FROM dbo.PLN
    WHERE HLTCode = @healthCode AND (@healthCode = 'S' OR PAYCode = @payorCode)
    GROUP BY PLNCode
    ORDER BY PLNCode;
END
GO

-- ===== main/070_registration_quote.sql =====
-- Membership > Registration: reads behind the premium / limit / MCO calculation (used by Quote and Save).

CREATE OR ALTER PROCEDURE genisis.Registration_Payor
    @pay nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(PAYMBMRegStatus) AS RegStatus FROM dbo.PAY WHERE PAYCode = @pay;
END
GO

-- Latest plan row; health type S plans are shared by all payors.
CREATE OR ALTER PROCEDURE genisis.Registration_Plan
    @plan nvarchar(4000), @hlt nvarchar(4000), @pay nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) GRPCompany, PRMInd, MCOInd, AnnLmtIND, PLNLifeTimeStatus, PLNProRate, PLNRoundUpStatus, PLNPremGenderStatus
    FROM dbo.PLN WHERE PLNCode = @plan AND HLTCode = @hlt AND (@hlt = 'S' OR PAYCode = @pay)
    ORDER BY PLNEffDate DESC;
END
GO

-- Plan-specific age bands plus the shared ones (PLNCode NULL).
CREATE OR ALTER PROCEDURE genisis.Registration_AgeBands
    @plan nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(AGECode) AS AgeCode, RTRIM(AGEDescription) AS Description, RTRIM(AGEFrom) AS AgeFrom, RTRIM(AGETo) AS AgeTo, RTRIM(PLNCode) AS PlanCode
    FROM dbo.AGE WHERE PLNCode = @plan OR PLNCode IS NULL;
END
GO

CREATE OR ALTER PROCEDURE genisis.Registration_AnnualLimit
    @ins nvarchar(4000), @plan nvarchar(4000), @hlt nvarchar(4000), @eff datetime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) SuppLimitStatus, AnnLimit, SuppLimit, LifeTimeLimit, SuppLifeTimeLimit FROM dbo.AnnualLimit
    WHERE INSCode = @ins AND PLNCode = @plan AND HLTCode = @hlt AND AnnualEffDate <= @eff ORDER BY AnnualEffDate DESC;
END
GO

-- MCO fee in force on @eff for the plan; falls back to the default row (PLNCode NULL) for the insured/health type.
CREATE OR ALTER PROCEDURE genisis.Registration_Mco
    @ins nvarchar(4000), @hlt nvarchar(4000), @plan nvarchar(4000), @eff datetime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) SuppMcoStatus, CovIDChrg, MCOAmountAdult, MCOAmountChild, MCOSuppAdultAmt, MCOSuppChildAmt FROM dbo.MCO
    WHERE INSCode = @ins AND HLTCode = @hlt AND (PLNCode = @plan OR PLNCode IS NULL) AND MCOEffDate <= @eff
    ORDER BY CASE WHEN PLNCode IS NULL THEN 1 ELSE 0 END, MCOEffDate DESC, MCOCode DESC;
END
GO

CREATE OR ALTER PROCEDURE genisis.Registration_Premium
    @ins nvarchar(4000), @hlt nvarchar(4000), @plan nvarchar(4000), @ageCode nvarchar(4000), @eff datetime, @pay nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) SuppPrmStatus, PRMAmount, PRMAmountFemale, SuppPrmAmt FROM dbo.PRM
    WHERE INSCode = @ins AND HLTCode = @hlt AND PLNCode = @plan AND AGECode = @ageCode AND PRMEffDate <= @eff
      AND (@hlt = 'S' OR PAYCode = @pay)
    ORDER BY PRMEffDate DESC;
END
GO

-- ===== main/080_registration_save.sql =====
-- Membership > Registration: writes for one registration. All run inside the API's transaction.
-- The Upload_* procedures are the desktop's own and are called unchanged, with the same fixed values the desktop passes.

CREATE OR ALTER PROCEDURE genisis.Server_Today
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(GETDATE() AS date) AS Today;
END
GO

CREATE OR ALTER PROCEDURE genisis.Registration_BenefitLimit
    @ins nvarchar(4000), @plan nvarchar(4000), @hlt nvarchar(4000), @eff datetime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(SuppALBStatus) AS SuppBnfStatus, KidDialysisLmt, CancerLmt, HomeNursLmt FROM dbo.BnfAnnLmt
    WHERE INSCode = @ins AND PLNCode = @plan AND HLTCode = @hlt AND ALBEffDate <= @eff ORDER BY ALBEffDate DESC;
END
GO

CREATE OR ALTER PROCEDURE genisis.Registration_InsertBenefitLimit
    @number nvarchar(4000), @coverId nvarchar(4000), @kidney decimal(19, 4), @cancer decimal(19, 4), @homeNursing decimal(19, 4)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.MBMBnfLmt (MBMNumber, MBMCoveredID, MBMKidneyAnnLmt, MBMKidneyBalLmt, MBMCancerAnnLmt, MBMCancerBalLmt,
        MBMHomeNCAnnLmt, MBMHomeNCBalLmt)
    VALUES (@number, @coverId, @kidney, @kidney, @cancer, @cancer, @homeNursing, @homeNursing);
END
GO

-- Latest active membership on the same policy / payor / insured type (lifetime plans carry its balance forward).
CREATE OR ALTER PROCEDURE genisis.Registration_PreviousLifetime
    @policy nvarchar(4000), @pay nvarchar(4000), @ins nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) MBMNumber, MBMTopupNumber, MBMAvailableLimit FROM dbo.MBM
    WHERE MBMPolicyNo = @policy AND MBMStatus = 'A' AND PAYCode = @pay AND INSCode = @ins ORDER BY MBMPayorEffDate DESC;
END
GO

CREATE OR ALTER PROCEDURE genisis.Registration_CarriedLimit
    @previousNo nvarchar(4000), @coverId nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) MBMCAvailableLimit AS Value FROM dbo.MBMCoveredPersons WHERE MBMCNumber = @previousNo AND MBMCCoverID = @coverId;
END
GO

CREATE OR ALTER PROCEDURE genisis.Registration_LifetimeBalance
    @topupNo nvarchar(4000), @coverId nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) MBMLifeTimeLmt AS Value FROM dbo.MBMLifeTime WHERE MBMNumber = @topupNo AND MBMCovID = @coverId;
END
GO

CREATE OR ALTER PROCEDURE genisis.Registration_InsertLifetime
    @topupNo nvarchar(4000), @coverId nvarchar(4000), @balance decimal(19, 4)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.MBMLifeTime (MBMNumber, MBMCovID, MBMLifeTimeLmt) VALUES (@topupNo, @coverId, @balance);
END
GO

CREATE OR ALTER PROCEDURE genisis.Registration_InsertPrincipal
    @pay nvarchar(4000), @hlt nvarchar(4000), @number nvarchar(4000), @policy nvarchar(4000), @ic nvarchar(4000),
    @name nvarchar(4000), @dob datetime, @sex nvarchar(4000), @race nvarchar(4000), @memberType nvarchar(4000),
    @add1 nvarchar(4000), @add2 nvarchar(4000), @add3 nvarchar(4000), @city nvarchar(4000), @postCode nvarchar(4000),
    @state nvarchar(4000), @today datetime, @eff datetime, @exp datetime, @takeOver nvarchar(4000), @renewal nvarchar(4000),
    @bordx datetime, @batch nvarchar(4000), @plan nvarchar(4000), @ins nvarchar(4000), @ageCode nvarchar(4000),
    @adultChild nvarchar(4000), @available decimal(19, 4), @topupNo nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.Upload_MBM_HIS @pay, @hlt, @number, @policy, '00', @ic, @name, @dob, @sex, @race, 'A', 'A', @memberType,
        @add1, @add2, @add3, @city, @postCode, @state, 'N', @today, @eff, @exp, @takeOver, @renewal, @bordx, @batch,
        @plan, @ins, @ageCode, @adultChild, @available, 'H', @topupNo, 'N';
END
GO

CREATE OR ALTER PROCEDURE genisis.Registration_InsertMBMTwo
    @number nvarchar(4000), @salutation nvarchar(4000), @telHome nvarchar(4000), @telMobile nvarchar(4000),
    @telOffice nvarchar(4000), @email nvarchar(4000), @otherIc nvarchar(4000), @nat nvarchar(4000), @user nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.MBMTwo (MBMNumber, MBMSalutation, MBMTelnoH, MBMTelNoM, MBMTelNoO, MBMEmail, MBMIcBcPp2nd,
        NATCode, SRVCodeList, LGNCode, MBMRegUser, MBMLastUpdateUser, MBMUploadStatus)
    VALUES (@number, @salutation, @telHome, @telMobile, @telOffice, @email, @otherIc, @nat, 'ME', '', @user, @user, 'M');
END
GO

CREATE OR ALTER PROCEDURE genisis.Registration_InsertXref
    @number nvarchar(4000), @policy nvarchar(4000), @ic nvarchar(4000), @name nvarchar(4000), @eff datetime, @exp datetime,
    @ageCode nvarchar(4000), @ins nvarchar(4000), @plan nvarchar(4000), @bordx datetime, @topupNo nvarchar(4000),
    @topupInd nvarchar(4000), @dob datetime, @hlt nvarchar(4000), @pay nvarchar(4000), @batch nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.Upload_MBMXref_HIS @number, '00', @policy, @ic, @name, @eff, @exp, @ageCode, @ins, @plan, @bordx,
        @topupNo, @topupInd, 'A', 'A', @dob, @hlt, @pay, @batch;
END
GO

CREATE OR ALTER PROCEDURE genisis.Registration_InsertOthers
    @number nvarchar(4000), @groupCompany nvarchar(4000), @employeeNo nvarchar(4000), @agentCode nvarchar(4000),
    @branch nvarchar(4000), @premium decimal(19, 4), @mco decimal(19, 4), @basicMco decimal(19, 4), @basicPremium decimal(19, 4),
    @department nvarchar(4000), @installment nvarchar(4000), @prevPolicy nvarchar(4000), @prevMember nvarchar(4000),
    @joined datetime = NULL, @guarantee nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.MBMOthers (MBMNumber, RELCode, MBMGroupCompany, MBMEmployeeNo, MBMAgentCode, MBMPOLSubNo1, MBMBranch,
        MBMPremium, MBMMCOFee, MBMBasicMCO, MBMBasicPrem, MBMDepartment, MBMInstallment, MBMUndExcess,
        MBMFirstMemNo, MBMFirstPolNo, MBMPrevPolNo, MBMPrevMemNo, MBMFirstJoinedDate, MBMPolicyDisc, MBMRenewalDisc,
        MBMPolCondition, MBMGuaRenewal, MBMENDtRefNo, MCOVoidStatus, MBMInvoiceNo, MBMPosition, MBMGroupCategory,
        MBMDivision, MBMSubDivision, MBMCostCenter)
    VALUES (@number, 'P', @groupCompany, @employeeNo, @agentCode, '', @branch,
        @premium, @mco, @basicMco, @basicPremium, @department, @installment, 0,
        '', '', @prevPolicy, @prevMember, @joined, 0, 0,
        '', @guarantee, '', '', '', '', '', '', '', '');
END
GO

CREATE OR ALTER PROCEDURE genisis.Registration_InsertOthersTwo
    @number nvarchar(4000), @marital nvarchar(4000), @exclusion nvarchar(4000), @allergic nvarchar(4000), @remarks nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.MBMOthersTwo (MBMNumber, MBMWeight, MBMHeight, MBMOccupation, MBMWorkNature, MBMBlood, MBMSmoking, MBMAlcohol,
        MBMPrevPLNCode, MBMPAYRemarks, MBMPrevInsCom, MBMMaritalStatus, MBMCoPayRB, MBMStaffDec,
        MBMPayorPremGross, MBMPayorPremNet, MBMPayorMCOGross, MBMPayorMCONet, MBMRBAmt, MBMFamDiscAmt, MBMRenewDiscAmt,
        MBMLoadingPrem, MBMExclusion, MBMBankACNo, MBMAllergic, MBMRemarks)
    VALUES (@number, '', '', '', '', '', '', '', '', '', '', @marital, '', '', 0, 0, 0, 0, 0, 0, 0, 0,
        @exclusion, '', @allergic, @remarks);
END
GO

-- Keeps an existing card security code for the base membership number; otherwise stores @generatedCode. Returns the code in use.
CREATE OR ALTER PROCEDURE genisis.Registration_SecurityCode
    @baseNumber nvarchar(4000), @generatedCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.MBMSCSecurity WITH (UPDLOCK, HOLDLOCK) WHERE MBMNumber = @baseNumber)
        INSERT INTO dbo.MBMSCSecurity (MBMNumber, MBMSecurityCode, MBMCardType, MBMClientType) VALUES (@baseNumber, @generatedCode, 'M', 'MED');
    SELECT TOP (1) RTRIM(MBMSecurityCode) AS SecurityCode FROM dbo.MBMSCSecurity WHERE MBMNumber = @baseNumber;
END
GO

-- Upload_MBMCovPersonsNew takes a char(1) relationship, which would store SP (spouse) as S (son), so the full code is written afterwards.
CREATE OR ALTER PROCEDURE genisis.Registration_InsertCoveredPerson
    @rel nvarchar(4000), @number nvarchar(4000), @coverId nvarchar(4000), @dob datetime, @name nvarchar(4000),
    @bordx datetime, @batch nvarchar(4000), @ic nvarchar(4000), @sex nvarchar(4000), @plan nvarchar(4000),
    @ageCode nvarchar(4000), @adultChild nvarchar(4000), @annual decimal(19, 4), @available decimal(19, 4),
    @basicMco decimal(19, 4), @mco decimal(19, 4), @basicPremium decimal(19, 4), @premium decimal(19, 4),
    @today datetime, @eff datetime, @exp datetime
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.Upload_MBMCovPersonsNew @rel, @number, @coverId, @dob, 'C', @name, 'A', @bordx, @batch, @ic, @sex,
        @plan, @ageCode, @adultChild, @annual, @available, @basicMco, @mco, @basicPremium, @premium, @today, @eff, @exp;
    IF LEN(@rel) > 1
        UPDATE dbo.MBMCoveredPersons SET MBMCRELCode = @rel WHERE MBMCNumber = @number AND MBMCCoverID = @coverId;
END
GO

CREATE OR ALTER PROCEDURE genisis.Registration_InsertCoveredPersonTwo
    @number nvarchar(4000), @coverId nvarchar(4000), @salutation nvarchar(4000), @occupation nvarchar(4000),
    @allergic nvarchar(4000), @exclusion nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.Upload_MBMCovPersonsTwo @number, @coverId, @salutation, @occupation, '', '', @allergic, '', '', '', '', '',
        @exclusion, '', '', '', '', 0, 0, 0, 0, '', 0, 0, 0, 0, 0, NULL, NULL, 0, 0;
END
GO

-- Last batch number used for the payor's bordereaux date (new registrations, type N).
CREATE OR ALTER PROCEDURE genisis.Registration_SaveBatch
    @pay nvarchar(4000), @bordx datetime, @batch nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.BAT WITH (UPDLOCK, HOLDLOCK) SET BATSequence = @batch
    WHERE PAYCode = @pay AND BATBordxDate = @bordx AND BordxType = 'N';
    IF @@ROWCOUNT = 0
        INSERT INTO dbo.BAT (PAYCode, BATBordxDate, BATSequence, BordxType) VALUES (@pay, @bordx, @batch, 'N');
END
GO

-- ===== main/090_code_lists.sql =====
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

-- ===== main/100_benefit_mgmt_fee.sql =====
-- Maintenance > Plan, Annual Limit (Benefit) and Case Mgmt. Fee tabs.

CREATE OR ALTER PROCEDURE genisis.BenefitLimit_Search
    @plan nvarchar(4000) = NULL, @health nvarchar(4000) = NULL, @insured nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ALBCode AS [Index], RTRIM(PLNCode) AS PlanCode, RTRIM(HLTCode) AS HealthCode, RTRIM(INSCode) AS InsuredCode,
        ALBEffDate AS EffectiveDate, RTRIM(SuppALBStatus) AS SuppStatus, KidDialysisLmt AS KidneyDialysisLimit,
        CancerLmt AS CancerLimit, HomeNursLmt AS HomeNursingLimit
    FROM dbo.BnfAnnLmt
    WHERE (@plan IS NULL OR PLNCode = @plan) AND (@health IS NULL OR HLTCode = @health) AND (@insured IS NULL OR INSCode = @insured)
    ORDER BY ALBCode DESC
    OPTION (RECOMPILE);
END
GO

-- @index NULL = new row. Result: 0 saved, 1 record not found, 2 another row has the same INS/HLT/PLN.
-- An edit that keeps its own key is allowed, so rows that are already duplicated can still be maintained.
CREATE OR ALTER PROCEDURE genisis.BenefitLimit_Save
    @index int = NULL, @plan nvarchar(4000), @health nvarchar(4000), @insured nvarchar(4000), @status nvarchar(4000),
    @effective datetime, @kidney decimal(19, 4), @cancer decimal(19, 4), @homeNursing decimal(19, 4), @userCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today datetime = CAST(CAST(GETDATE() AS date) AS datetime);
    IF @index IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.BnfAnnLmt WITH (UPDLOCK, HOLDLOCK) WHERE ALBCode = @index)
    BEGIN SELECT 1 AS Result, @index AS [Index]; RETURN; END

    IF EXISTS (
        SELECT 1 FROM dbo.BnfAnnLmt WITH (UPDLOCK, HOLDLOCK)
        WHERE INSCode = @insured AND HLTCode = @health AND PLNCode = @plan
          AND (@index IS NULL OR (ALBCode <> @index AND NOT EXISTS (
              SELECT 1 FROM dbo.BnfAnnLmt c WHERE c.ALBCode = @index AND c.INSCode = @insured AND c.HLTCode = @health AND c.PLNCode = @plan))))
    BEGIN SELECT 2 AS Result, @index AS [Index]; RETURN; END

    IF @index IS NULL
    BEGIN
        INSERT INTO dbo.BnfAnnLmt (PLNCode, INSCode, HLTCode, SuppALBStatus, ALBEffDate, KidDialysisLmt, CancerLmt, HomeNursLmt,
            ALBLastUpdateUser, ALBLastUpdateDate)
        VALUES (@plan, @insured, @health, @status, @effective, @kidney, @cancer, @homeNursing, @userCode, @today);
        SELECT 0 AS Result, CAST(SCOPE_IDENTITY() AS int) AS [Index];
        RETURN;
    END

    UPDATE dbo.BnfAnnLmt SET PLNCode = @plan, INSCode = @insured, HLTCode = @health, SuppALBStatus = @status, ALBEffDate = @effective,
        KidDialysisLmt = @kidney, CancerLmt = @cancer, HomeNursLmt = @homeNursing, ALBLastUpdateUser = @userCode, ALBLastUpdateDate = @today
    WHERE ALBCode = @index;
    SELECT 0 AS Result, @index AS [Index];
END
GO

-- Result: 0 deleted, 1 record not found.
CREATE OR ALTER PROCEDURE genisis.BenefitLimit_Delete
    @index int
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.BnfAnnLmt WHERE ALBCode = @index;
    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END AS Result;
END
GO

CREATE OR ALTER PROCEDURE genisis.MgmtFee_Search
    @plan nvarchar(4000) = NULL, @health nvarchar(4000) = NULL, @insured nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(MgmtPLNCode) AS PlanCode, RTRIM(MgmtHLTCode) AS HealthCode, RTRIM(MgmtINSCode) AS InsuredCode, MgmtFee AS Fee
    FROM genisis.Maint_CaseMgmtFee
    WHERE (@plan IS NULL OR MgmtPLNCode = @plan) AND (@health IS NULL OR MgmtHLTCode = @health) AND (@insured IS NULL OR MgmtINSCode = @insured)
    ORDER BY MgmtPLNCode, MgmtHLTCode, MgmtINSCode
    OPTION (RECOMPILE);
END
GO

-- The plan / health / insured key is the table's primary key; an update only changes the fee.
-- Result: 0 saved, 1 record not found (update), 2 key already exists (add).
CREATE OR ALTER PROCEDURE genisis.MgmtFee_Save
    @plan nvarchar(4000), @health nvarchar(4000), @insured nvarchar(4000), @fee decimal(19, 4), @update bit, @userCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today datetime = CAST(CAST(GETDATE() AS date) AS datetime);
    DECLARE @exists bit = CASE WHEN EXISTS (SELECT 1 FROM genisis.Maint_CaseMgmtFee WITH (UPDLOCK, HOLDLOCK)
        WHERE MgmtPLNCode = @plan AND MgmtHLTCode = @health AND MgmtINSCode = @insured) THEN 1 ELSE 0 END;
    IF @update = 1 AND @exists = 0 BEGIN SELECT 1 AS Result; RETURN; END
    IF @update = 0 AND @exists = 1 BEGIN SELECT 2 AS Result; RETURN; END

    IF @update = 0
        INSERT INTO genisis.Maint_CaseMgmtFee (MgmtPLNCode, MgmtHLTCode, MgmtINSCode, MgmtFee, MgmtLastUpdateUser, MgmtLastUpdateDate)
        VALUES (@plan, @health, @insured, @fee, @userCode, @today);
    ELSE
        UPDATE genisis.Maint_CaseMgmtFee SET MgmtFee = @fee, MgmtLastUpdateUser = @userCode, MgmtLastUpdateDate = @today
        WHERE MgmtPLNCode = @plan AND MgmtHLTCode = @health AND MgmtINSCode = @insured;
    SELECT 0 AS Result;
END
GO

-- Result: 0 deleted, 1 record not found.
CREATE OR ALTER PROCEDURE genisis.MgmtFee_Delete
    @plan nvarchar(4000), @health nvarchar(4000), @insured nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM genisis.Maint_CaseMgmtFee WHERE MgmtPLNCode = @plan AND MgmtHLTCode = @health AND MgmtINSCode = @insured;
    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END AS Result;
END
GO

-- ===== main/110_mco_fee.sql =====
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

-- ===== main/120_benefits.sql =====
-- Maintenance > Benefits (legacy frmMaintenanceBenefits): Category, Sub Category, Benefit Entry and Benefits Listing tabs.
-- dbo.BNF is keyed by sub category (BNFCode) / insured / plan / effective date; the health type is stored but not part of the key.
-- Text filters marked "LIKE" are escaped by the caller.

CREATE OR ALTER PROCEDURE genisis.Benefits_Lookups
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(HLTCode) AS Code, RTRIM(HLTDescription) AS Name FROM dbo.HLT ORDER BY HLTCode;
    SELECT RTRIM(INSCode) AS Code, RTRIM(INSDescription) AS Name FROM dbo.INS ORDER BY INSCode;
    SELECT RTRIM(CATCode) AS Code, RTRIM(CATDescription) AS Name FROM dbo.BNFCategory ORDER BY CATCode;
    SELECT RTRIM(BNFSubCode) AS Code, RTRIM(BNFSUBName) AS Name FROM dbo.BNFSubCat ORDER BY BNFSubCode;
    SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name FROM dbo.PAY WHERE PAYCode IS NOT NULL ORDER BY PAYCode;
    SELECT DISTINCT RTRIM(GRPCompany) AS Name FROM dbo.PLN WHERE NULLIF(RTRIM(GRPCompany), N'') IS NOT NULL ORDER BY Name;
END
GO

-- Category tab. @code is a LIKE prefix, @description a LIKE fragment (desktop SearchCAT).
CREATE OR ALTER PROCEDURE genisis.BenefitCategory_Search
    @code nvarchar(4000) = NULL, @description nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(CATCode) AS Code, RTRIM(CATDescription) AS Description FROM dbo.BNFCategory
    WHERE (@code IS NULL OR CATCode LIKE @code + N'%') AND (@description IS NULL OR CATDescription LIKE N'%' + @description + N'%')
    ORDER BY CATCode
    OPTION (RECOMPILE);
END
GO

-- @update 0 adds, 1 changes the description. Result: 0 saved, 1 record not found, 2 code already exists.
CREATE OR ALTER PROCEDURE genisis.BenefitCategory_Save
    @code nvarchar(4000), @description nvarchar(4000), @update bit, @userCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today datetime = CAST(CAST(GETDATE() AS date) AS datetime);
    DECLARE @exists bit = CASE WHEN EXISTS (SELECT 1 FROM dbo.BNFCategory WITH (UPDLOCK, HOLDLOCK) WHERE CATCode = @code) THEN 1 ELSE 0 END;
    IF @update = 1 AND @exists = 0 BEGIN SELECT 1 AS Result; RETURN; END
    IF @update = 0 AND @exists = 1 BEGIN SELECT 2 AS Result; RETURN; END
    IF @update = 1
        UPDATE dbo.BNFCategory SET CATDescription = @description, CATLastUpdateUser = @userCode, CATLastUpdateDate = @today WHERE CATCode = @code;
    ELSE
        INSERT INTO dbo.BNFCategory (CATCode, CATDescription, CATLastUpdateUser, CATLastUpdateDate) VALUES (@code, @description, @userCode, @today);
    SELECT 0 AS Result;
END
GO

-- Desktop DeleteCAT: refused while a sub category uses the category. Result: 0 deleted, 1 record not found, 2 still in use.
CREATE OR ALTER PROCEDURE genisis.BenefitCategory_Delete
    @code nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.BNFCategory WITH (UPDLOCK, HOLDLOCK) WHERE CATCode = @code)
    BEGIN SELECT 1 AS Result, CAST(NULL AS nvarchar(20)) AS UsedIn; RETURN; END
    IF EXISTS (SELECT 1 FROM dbo.BNFSubCat WHERE BNFCatCode = @code) BEGIN SELECT 2 AS Result, N'Benefit' AS UsedIn; RETURN; END
    DELETE FROM dbo.BNFCategory WHERE CATCode = @code;
    SELECT 0 AS Result, CAST(NULL AS nvarchar(20)) AS UsedIn;
END
GO

-- Sub Category tab (desktop View_BNF_CAT_subcat, which hides 602 Govt. Service Tax). Sub categories whose category no longer
-- exists are listed too (with no category description) so they can be corrected. @code is a LIKE prefix, @name a LIKE fragment.
CREATE OR ALTER PROCEDURE genisis.BenefitSubCategory_Search
    @category nvarchar(4000) = NULL, @code nvarchar(4000) = NULL, @name nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(s.BNFCatCode) AS CategoryCode, RTRIM(c.CATDescription) AS CategoryDescription, RTRIM(s.BNFSubCode) AS Code,
        RTRIM(s.BNFSUBName) AS Name
    FROM dbo.BNFSubCat s LEFT JOIN dbo.BNFCategory c ON c.CATCode = s.BNFCatCode
    WHERE s.BNFSubCode <> N'602' AND (@category IS NULL OR s.BNFCatCode = @category)
      AND (@code IS NULL OR s.BNFSubCode LIKE @code + N'%') AND (@name IS NULL OR s.BNFSUBName LIKE N'%' + @name + N'%')
    ORDER BY s.BNFCatCode, s.BNFSubCode
    OPTION (RECOMPILE);
END
GO

-- @update 0 adds, 1 changes the category and name. Result: 0 saved, 1 not found (Missing: 'Category' or 'Record'), 2 code already exists.
CREATE OR ALTER PROCEDURE genisis.BenefitSubCategory_Save
    @category nvarchar(4000), @code nvarchar(4000), @name nvarchar(4000), @update bit, @userCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today datetime = CAST(CAST(GETDATE() AS date) AS datetime);
    IF NOT EXISTS (SELECT 1 FROM dbo.BNFCategory WHERE CATCode = @category) BEGIN SELECT 1 AS Result, N'Category' AS Missing; RETURN; END
    DECLARE @exists bit = CASE WHEN EXISTS (SELECT 1 FROM dbo.BNFSubCat WITH (UPDLOCK, HOLDLOCK) WHERE BNFSubCode = @code) THEN 1 ELSE 0 END;
    IF @update = 1 AND @exists = 0 BEGIN SELECT 1 AS Result, N'Record' AS Missing; RETURN; END
    IF @update = 0 AND @exists = 1 BEGIN SELECT 2 AS Result, CAST(NULL AS nvarchar(20)) AS Missing; RETURN; END
    IF @update = 1
        UPDATE dbo.BNFSubCat SET BNFCatCode = @category, BNFSUBName = @name, BNFLastUpdateUser = @userCode, BNFLastUpdateDate = @today
        WHERE BNFSubCode = @code;
    ELSE
        INSERT INTO dbo.BNFSubCat (BNFCatCode, BNFSubCode, BNFSUBName, BNFLastUpdateUser, BNFLastUpdateDate)
        VALUES (@category, @code, @name, @userCode, @today);
    SELECT 0 AS Result, CAST(NULL AS nvarchar(20)) AS Missing;
END
GO

-- Desktop Deletesubcat: refused while a benefit row uses the sub category. Result: 0 deleted, 1 record not found, 2 still in use.
CREATE OR ALTER PROCEDURE genisis.BenefitSubCategory_Delete
    @code nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.BNFSubCat WITH (UPDLOCK, HOLDLOCK) WHERE BNFSubCode = @code)
    BEGIN SELECT 1 AS Result, CAST(NULL AS nvarchar(20)) AS UsedIn; RETURN; END
    IF EXISTS (SELECT 1 FROM dbo.BNF WHERE BNFCode = @code) BEGIN SELECT 2 AS Result, N'Benefit' AS UsedIn; RETURN; END
    DELETE FROM dbo.BNFSubCat WHERE BNFSubCode = @code;
    SELECT 0 AS Result, CAST(NULL AS nvarchar(20)) AS UsedIn;
END
GO

-- Benefit Entry tab: every sub category with the values already saved for this plan / insured / health / date (desktop tBNFlisting).
CREATE OR ALTER PROCEDURE genisis.BenefitEntry_Load
    @plan nvarchar(4000), @insured nvarchar(4000), @health nvarchar(4000), @effective datetime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(s.BNFSubCode) AS Code, RTRIM(s.BNFSUBName) AS Name, b.BNFNoOfDays AS Days, RTRIM(b.BNFClaimAbleAmount) AS RatePerDay,
        RTRIM(b.BNFRMPerDis) AS PerDisability
    FROM dbo.BNFSubCat s
    LEFT JOIN dbo.BNF b ON b.BNFCode = s.BNFSubCode AND b.PLNCode = @plan AND b.INSCode = @insured AND b.HLTCode = @health AND b.BNFEffDate = @effective
    ORDER BY s.BNFSubCode;
END
GO

-- Desktop SaveBNF refuses a new set when the plan / insured / date already has any benefit row. Locks the range for the insert that follows.
CREATE OR ALTER PROCEDURE genisis.BenefitEntry_SetExists
    @plan nvarchar(4000), @insured nvarchar(4000), @effective datetime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (
        SELECT 1 FROM dbo.BNF WITH (UPDLOCK, HOLDLOCK) WHERE PLNCode = @plan AND INSCode = @insured AND BNFEffDate = @effective)
        THEN 1 ELSE 0 END AS bit) AS SetExists;
END
GO

-- Adds one benefit row. @version 1 fills BNFEffDateVersion the way the Benefits Listing tab did (rows already on the same
-- sub category / health / insured / plan, plus one); the Benefit Entry tab leaves it empty.
-- Result: 0 saved, 1 a code was not found (Missing names it), 2 the sub category already has a row for this plan / insured / date.
CREATE OR ALTER PROCEDURE genisis.Benefit_Insert
    @subCategory nvarchar(4000), @health nvarchar(4000), @insured nvarchar(4000), @plan nvarchar(4000), @effective datetime,
    @days smallint, @ratePerDay nvarchar(4000), @perDisability nvarchar(4000), @version bit, @userCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @missing nvarchar(20) = CASE
        WHEN NOT EXISTS (SELECT 1 FROM dbo.BNFSubCat WHERE BNFSubCode = @subCategory) THEN N'Sub Category Code'
        WHEN NOT EXISTS (SELECT 1 FROM dbo.HLT WHERE HLTCode = @health) THEN N'Health Type'
        WHEN NOT EXISTS (SELECT 1 FROM dbo.INS WHERE INSCode = @insured) THEN N'Insured Type'
        WHEN NOT EXISTS (SELECT 1 FROM dbo.PLN WHERE PLNCode = @plan) THEN N'Plan Code'
    END;
    IF @missing IS NOT NULL BEGIN SELECT 1 AS Result, @missing AS Missing; RETURN; END
    IF EXISTS (
        SELECT 1 FROM dbo.BNF WITH (UPDLOCK, HOLDLOCK)
        WHERE BNFCode = @subCategory AND INSCode = @insured AND PLNCode = @plan AND BNFEffDate = @effective)
    BEGIN SELECT 2 AS Result, CAST(NULL AS nvarchar(20)) AS Missing; RETURN; END

    INSERT INTO dbo.BNF (BNFCode, HLTCode, INSCode, PLNCode, BNFNoOfDays, BNFClaimAbleAmount, BNFRMPerDis, BNFEffDate, BNFEffDateVersion,
        BNFLastUpdateUser, BNFLastUpdateDate)
    VALUES (@subCategory, @health, @insured, @plan, @days, @ratePerDay, @perDisability, @effective,
        CASE WHEN @version = 1 THEN (SELECT COUNT(*) + 1 FROM dbo.BNF
            WHERE BNFCode = @subCategory AND HLTCode = @health AND INSCode = @insured AND PLNCode = @plan) END,
        @userCode, CAST(CAST(GETDATE() AS date) AS datetime));
    SELECT 0 AS Result, CAST(NULL AS nvarchar(20)) AS Missing;
END
GO

-- Benefits Listing tab (desktop VIEW_BNF_PLN_SubCat). Payor and group company come from the plan; a plan code can have several
-- PLN rows, so filters use EXISTS and the newest plan row is shown. @group is a LIKE prefix.
CREATE OR ALTER PROCEDURE genisis.Benefit_Search
    @top int, @subCategory nvarchar(4000) = NULL, @health nvarchar(4000) = NULL, @insured nvarchar(4000) = NULL,
    @plan nvarchar(4000) = NULL, @effective datetime = NULL, @payor nvarchar(4000) = NULL, @group nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@top) RTRIM(b.BNFCode) AS SubCategoryCode, RTRIM(s.BNFSUBName) AS SubCategoryName, RTRIM(b.HLTCode) AS HealthCode,
        RTRIM(b.INSCode) AS InsuredCode, RTRIM(b.PLNCode) AS PlanCode, b.BNFNoOfDays AS Days, RTRIM(b.BNFClaimAbleAmount) AS RatePerDay,
        RTRIM(b.BNFRMPerDis) AS PerDisability, b.BNFEffDate AS EffectiveDate, RTRIM(p.PAYCode) AS PayorCode, RTRIM(p.GRPCompany) AS GroupCompany
    FROM dbo.BNF b
    LEFT JOIN dbo.BNFSubCat s ON s.BNFSubCode = b.BNFCode
    OUTER APPLY (SELECT TOP 1 PAYCode, GRPCompany FROM dbo.PLN WHERE PLNCode = b.PLNCode ORDER BY PLNIndex DESC) p
    WHERE (@subCategory IS NULL OR b.BNFCode = @subCategory) AND (@health IS NULL OR b.HLTCode = @health)
      AND (@insured IS NULL OR b.INSCode = @insured) AND (@plan IS NULL OR b.PLNCode = @plan)
      AND (@effective IS NULL OR b.BNFEffDate = @effective)
      AND (@payor IS NULL OR EXISTS (SELECT 1 FROM dbo.PLN x WHERE x.PLNCode = b.PLNCode AND x.PAYCode = @payor))
      AND (@group IS NULL OR EXISTS (SELECT 1 FROM dbo.PLN x WHERE x.PLNCode = b.PLNCode AND x.GRPCompany LIKE @group + N'%'))
    ORDER BY b.PLNCode, b.INSCode, b.BNFEffDate DESC, b.BNFCode
    OPTION (RECOMPILE);
END
GO

-- Edits one row's days, RM / day and RM per disability; its key and health type stay fixed. Result: 0 saved, 1 record not found.
CREATE OR ALTER PROCEDURE genisis.Benefit_Update
    @subCategory nvarchar(4000), @insured nvarchar(4000), @plan nvarchar(4000), @effective datetime,
    @days smallint, @ratePerDay nvarchar(4000), @perDisability nvarchar(4000), @userCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.BNF SET BNFNoOfDays = @days, BNFClaimAbleAmount = @ratePerDay, BNFRMPerDis = @perDisability,
        BNFLastUpdateUser = @userCode, BNFLastUpdateDate = CAST(CAST(GETDATE() AS date) AS datetime)
    WHERE BNFCode = @subCategory AND INSCode = @insured AND PLNCode = @plan AND BNFEffDate = @effective;
    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END AS Result;
END
GO

-- Deletes one row. Refused while a member is on the row's plan / health / insured (desktop VIEW_BNF_MBM_CodeChecking).
-- Result: 0 deleted, 1 record not found, 2 still in use.
CREATE OR ALTER PROCEDURE genisis.Benefit_Delete
    @subCategory nvarchar(4000), @insured nvarchar(4000), @plan nvarchar(4000), @effective datetime
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @health char(2);
    SELECT @health = HLTCode FROM dbo.BNF WITH (UPDLOCK, HOLDLOCK)
    WHERE BNFCode = @subCategory AND INSCode = @insured AND PLNCode = @plan AND BNFEffDate = @effective;
    IF @@ROWCOUNT = 0 BEGIN SELECT 1 AS Result; RETURN; END
    IF EXISTS (SELECT 1 FROM dbo.MBM WHERE PLNCode = @plan AND HLTCode = @health AND INSCode = @insured) BEGIN SELECT 2 AS Result; RETURN; END
    DELETE FROM dbo.BNF WHERE BNFCode = @subCategory AND INSCode = @insured AND PLNCode = @plan AND BNFEffDate = @effective;
    SELECT 0 AS Result;
END
GO

-- ===== main/130_mma.sql =====
-- Maintenance > Entity (legacy frmMMA: Procedures and Look Up Table tabs) and Maintenance > Accounts (legacy frmMMA13thSchDesc).
-- The tables live in the maintenance database: genisis.Maint_MMAProcedures, genisis.Maint_MMAFees, genisis.Maint_MMASchedule (view).
-- Text filters are LIKE fragments escaped by the caller. Only the columns the desktop maintains are written.

CREATE OR ALTER PROCEDURE genisis.MmaProcedure_Search
    @top int, @bodyPart nvarchar(4000) = NULL, @code nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@top) ProcIndex AS [Index], RTRIM(ProcMainBPCode) AS BodyPart, RTRIM(OPCSCode) AS Code, OPCSNarrative AS Narrative,
        RTRIM(ProcSurgCat) AS SurgeonCategory, RTRIM(ProcAnaesCat) AS AnaesthetistCategory, RTRIM(ProcPart) AS Part,
        RTRIM(PartACost) AS PartACost, RTRIM(ProcPageNo) AS PageNo
    FROM genisis.Maint_MMAProcedures
    WHERE (@bodyPart IS NULL OR ProcMainBPCode LIKE N'%' + @bodyPart + N'%') AND (@code IS NULL OR OPCSCode LIKE N'%' + @code + N'%')
    ORDER BY ProcMainBPCode, OPCSCode, ProcIndex
    OPTION (RECOMPILE);
END
GO

-- @index NULL adds a procedure. Result: 0 saved, 1 record not found.
CREATE OR ALTER PROCEDURE genisis.MmaProcedure_Save
    @index int = NULL, @bodyPart nvarchar(4000), @code nvarchar(4000), @narrative nvarchar(4000), @surgeonCategory nvarchar(4000),
    @anaesthetistCategory nvarchar(4000), @pageNo nvarchar(4000), @partACost nvarchar(4000), @part nvarchar(4000), @userCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today datetime = CAST(CAST(GETDATE() AS date) AS datetime);
    IF @index IS NULL
    BEGIN
        INSERT INTO genisis.Maint_MMAProcedures (ProcMainBPCode, OPCSCode, OPCSNarrative, ProcSurgCat, ProcAnaesCat, ProcPageNo, PartACost, ProcPart,
            ProcLastUpdateUser, ProcLastUpdateDate)
        OUTPUT 0 AS Result, inserted.ProcIndex AS [Index]
        VALUES (@bodyPart, @code, @narrative, @surgeonCategory, @anaesthetistCategory, @pageNo, @partACost, @part, @userCode, @today);
        RETURN;
    END
    UPDATE genisis.Maint_MMAProcedures SET ProcMainBPCode = @bodyPart, OPCSCode = @code, OPCSNarrative = @narrative, ProcSurgCat = @surgeonCategory,
        ProcAnaesCat = @anaesthetistCategory, ProcPageNo = @pageNo, PartACost = @partACost, ProcPart = @part,
        ProcLastUpdateUser = @userCode, ProcLastUpdateDate = @today
    WHERE ProcIndex = @index;
    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END AS Result, @index AS [Index];
END
GO

-- Result: 0 deleted, 1 record not found.
CREATE OR ALTER PROCEDURE genisis.MmaProcedure_Delete
    @index int
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM genisis.Maint_MMAProcedures WHERE ProcIndex = @index;
    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END AS Result;
END
GO

CREATE OR ALTER PROCEDURE genisis.MmaFee_Search
    @surgeonCategory nvarchar(4000) = NULL, @anaesthetistCategory nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT FeeIndex AS [Index], RTRIM(SurgeonsCat) AS SurgeonCategory, SurgeonsFees AS SurgeonFee, RTRIM(AnaesthetistsCat) AS AnaesthetistCategory,
        AnaesthetistsFees AS AnaesthetistFee
    FROM genisis.Maint_MMAFees
    WHERE (@surgeonCategory IS NULL OR SurgeonsCat LIKE N'%' + @surgeonCategory + N'%')
      AND (@anaesthetistCategory IS NULL OR AnaesthetistsCat LIKE N'%' + @anaesthetistCategory + N'%')
    ORDER BY FeeIndex
    OPTION (RECOMPILE);
END
GO

-- @index NULL adds a fee row. Result: 0 saved, 1 record not found.
CREATE OR ALTER PROCEDURE genisis.MmaFee_Save
    @index int = NULL, @surgeonCategory nvarchar(4000), @surgeonFee decimal(19, 4), @anaesthetistCategory nvarchar(4000),
    @anaesthetistFee decimal(19, 4), @userCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today datetime = CAST(CAST(GETDATE() AS date) AS datetime);
    IF @index IS NULL
    BEGIN
        INSERT INTO genisis.Maint_MMAFees (SurgeonsCat, SurgeonsFees, AnaesthetistsCat, AnaesthetistsFees, FeeLastUpdateUser, FeeLastUpdateDate)
        OUTPUT 0 AS Result, inserted.FeeIndex AS [Index]
        VALUES (@surgeonCategory, @surgeonFee, @anaesthetistCategory, @anaesthetistFee, @userCode, @today);
        RETURN;
    END
    UPDATE genisis.Maint_MMAFees SET SurgeonsCat = @surgeonCategory, SurgeonsFees = @surgeonFee, AnaesthetistsCat = @anaesthetistCategory,
        AnaesthetistsFees = @anaesthetistFee, FeeLastUpdateUser = @userCode, FeeLastUpdateDate = @today
    WHERE FeeIndex = @index;
    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END AS Result, @index AS [Index];
END
GO

-- The desktop deletes without a check; the web refuses while a procedure uses the row's surgeon or anaesthetist category
-- (the fees reach procedures through those categories). Result: 0 deleted, 1 record not found, 2 still in use.
CREATE OR ALTER PROCEDURE genisis.MmaFee_Delete
    @index int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @surgeon nvarchar(10), @anaesthetist nvarchar(50);
    SELECT @surgeon = SurgeonsCat, @anaesthetist = AnaesthetistsCat FROM genisis.Maint_MMAFees WITH (UPDLOCK, HOLDLOCK) WHERE FeeIndex = @index;
    IF @@ROWCOUNT = 0 BEGIN SELECT 1 AS Result; RETURN; END
    IF EXISTS (SELECT 1 FROM genisis.Maint_MMAProcedures WHERE ProcSurgCat = @surgeon OR ProcAnaesCat = @anaesthetist)
    BEGIN SELECT 2 AS Result; RETURN; END
    DELETE FROM genisis.Maint_MMAFees WHERE FeeIndex = @index;
    SELECT 0 AS Result;
END
GO

-- Accounts screen: one OPCS code of VIEW_MMA_Schedule, MMA beside 13th Schedule (desktop cmdNext / cmdPrev).
-- @direction 1 = next code after @code (first when NULL), -1 = previous code before @code (last when NULL), 0 = first code >= @code.
-- No row means there is no such code. The MMA surgeon fee is the MMAFees fee for the procedure's surgeon category (view column [1]);
-- the desktop read the view's SurgeonsFees column, which is the 14th Schedule surgeon fee.
CREATE OR ALTER PROCEDURE genisis.MmaSchedule_Move
    @code nvarchar(4000) = NULL, @direction int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 RTRIM(OPCSCode) AS Code, OPCSNarrative AS MmaDescription, ProcedureDescription AS ScheduleDescription,
        RTRIM(ProcMainBPCode) AS MmaBodyPart, BodyParts AS ScheduleBodyPart, RTRIM(ProcPageNo) AS MmaPage, RTRIM(Page) AS SchedulePage,
        CAST([1] AS decimal(19, 4)) AS MmaSurgeonFee, CAST(AnaesthetistsFees AS decimal(19, 4)) AS MmaAnaesthetistFee,
        CAST(Surgeon13thMin AS decimal(19, 4)) AS ScheduleSurgeonFeeMin, CAST(Surgeon13th AS decimal(19, 4)) AS ScheduleSurgeonFeeMax,
        CAST(Anesthetist13th AS decimal(19, 4)) AS ScheduleAnaesthetistFee
    FROM genisis.Maint_MMASchedule
    WHERE OPCSCode IS NOT NULL
      AND (@code IS NULL
           OR (@direction = 1 AND OPCSCode > @code)
           OR (@direction = -1 AND OPCSCode < @code)
           OR (@direction = 0 AND OPCSCode >= @code))
    ORDER BY CASE WHEN @direction = -1 THEN NULL ELSE OPCSCode END, OPCSCode DESC
    OPTION (RECOMPILE);
END
GO

-- ===== main/140_adjustment.sql =====
-- Membership > Adjustment (legacy frmNewMBMAdjustment + frmNewMBMOthers). Reads for the screen and the writes behind each
-- endorsement type. All writes run inside the API's transaction; main, maintenance and clinical tables are reached on one
-- connection (maintenance through genisis.Maint_*, clinical through genisis.Cis_*).

-- ===== Reads =====

-- Next (@direction 1) or previous (-1) membership number (desktop cmdNext / cmdPrev).
CREATE OR ALTER PROCEDURE genisis.Adjustment_Navigate
    @from nvarchar(4000), @direction int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) MBMNumber FROM dbo.MBM
    WHERE (@direction = 1 AND MBMNumber > @from) OR (@direction = -1 AND MBMNumber < @from)
    ORDER BY CASE WHEN @direction = -1 THEN NULL ELSE MBMNumber END, MBMNumber DESC
    OPTION (RECOMPILE);
END
GO

-- Result sets: principal, covered persons, clinical plan (MBMCLNOthers), annual limit in force on the payor effective date.
-- Status follows SearchMBMCovPersonsProc: a cancellation dated in the future without a cancel date still shows as active.
CREATE OR ALTER PROCEDURE genisis.Adjustment_Load
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(m.MBMNumber) AS MembershipNo, RTRIM(m.HLTCode) AS HealthCode, RTRIM(m.PAYCode) AS PayorCode, RTRIM(m.INSCode) AS InsuredType,
        RTRIM(m.PLNCode) AS PlanCode, RTRIM(m.MBMPolicyNo) AS PolicyNo, RTRIM(m.MBMName) AS Name, RTRIM(m.MBMIcBcPp) AS IcBcPp,
        m.MBMDOB AS DateOfBirth, RTRIM(m.MBMSex) AS Sex, RTRIM(m.RACCode) AS RaceCode,
        RTRIM(m.MBMAddress1) AS Address1, RTRIM(m.MBMAddress2) AS Address2, RTRIM(m.MBMAddress3) AS Address3,
        RTRIM(m.CTYCode) AS City, RTRIM(m.STACode) AS State, RTRIM(m.MBMPostCode) AS PostCode,
        m.MBMPayorEffDate AS PayorEffectiveDate, m.MBMPayorExpDate AS PayorExpiryDate, m.MBMBordxDate AS BordxDate, m.MBMBatchNo AS BatchNo,
        RTRIM(m.AGECode) AS AgeCode, RTRIM(m.MBMAdultChild) AS AdultChild, RTRIM(m.MBMTakeover) AS TakeOver, RTRIM(m.MBMRenewFlag) AS Renewal,
        RTRIM(m.MBMDataStatus) AS DataStatus,
        RTRIM(CASE WHEN m.MBMStatus = 'C' AND m.MBMPayorEffDate > CAST(GETDATE() AS date) AND t.MBMCancelDate IS NULL THEN 'A' ELSE m.MBMStatus END) AS Status,
        RTRIM(m.MBMExpiredStatus) AS ExpiredStatus, m.MBMAvailableLimit AS AvailableLimit, m.MBMValueDate AS ValueDate,
        RTRIM(m.MBMPolicyYear) AS PolicyYear, m.MBMFinalExpiryDate AS FinalExpiryDate, m.MBMNextDueDate AS NextDueDate,
        RTRIM(m.MBMTopupInd) AS TopupInd, RTRIM(m.MBMTopupNumber) AS TopupNumber,
        RTRIM(t.MBMSalutation) AS Salutation, RTRIM(t.MBMEmail) AS Email, RTRIM(t.LGNCode) AS LgnCode, RTRIM(t.NATCode) AS NationalityCode,
        RTRIM(t.MBMTelnoH) AS TelHome, RTRIM(t.MBMTelNoM) AS TelMobile, RTRIM(t.MBMTelNoO) AS TelOffice, t.MBMMmgtNotes AS MgmtNotes,
        RTRIM(t.MBMIcBcPp2nd) AS OtherIc, RTRIM(t.MBMSendingMode) AS SendingMode, t.MBMCardDate AS CardSentDate, t.MBMCancelDate AS CancelDate,
        RTRIM(o.MBMDepartment) AS Department, RTRIM(o.MBMGroupCompany) AS GroupCompany, RTRIM(o.MBMPOLSubNo1) AS PolicySubNo,
        CAST(CASE WHEN o.LabelStatus = 'Y' THEN 1 ELSE 0 END AS bit) AS LabelStatus, RTRIM(o.MBMEmployeeNo) AS EmployeeNo,
        RTRIM(o.MBMAgentCode) AS AgentCode, RTRIM(o.MBMInstallment) AS InstallmentMode, o.MBMMCOFee AS McoFee, o.MBMBasicMCO AS BasicMco,
        RTRIM(o.MBMBranch) AS Branch, o.MBMBasicPrem AS BasicPremium, o.MBMPremium AS Premium, o.MBMFirstJoinedDate AS DateJoined,
        o.MBMClmNonPayFr AS ClaimNonPayFrom, o.MBMClmNonPayTo AS ClaimNonPayTo, RTRIM(o.MBMNewPlanCode) AS NewPlanCode,
        o.MBMNewPolEffDate AS NewPolicyEffectiveDate, o.MBMPolicyDisc AS PolicyDisc, o.MBMRenewalDisc AS RenewalDisc,
        RTRIM(o.MBMPolCondition) AS PolicyCondition, o.MBMUndExcess AS UnderwritingExcess, RTRIM(o.MBMENDtRefNo) AS EndtRefNo,
        RTRIM(o.MBMCostCenter) AS CostCenter, RTRIM(o.MBMPosition) AS Position, RTRIM(o.MBMGroupCategory) AS GroupCategory,
        RTRIM(o.MBMDivision) AS Division, RTRIM(o.MBMSubDivision) AS SubDivision, RTRIM(o.Subsidiary) AS Subsidiary,
        RTRIM(o.MBMGuaRenewal) AS GuaranteeRenewal, RTRIM(o.MBMInvoiceNo) AS InvoiceNo, o.MBMInvoiceDate AS InvoiceDate,
        RTRIM(o.MBMCRNote) AS CreditNote, o.MBMCRNoteDate AS CreditNoteDate,
        w.MBMAllergic AS Allergic, w.MBMRemarks AS Remarks, RTRIM(w.MBMWeight) AS Weight, RTRIM(w.MBMHeight) AS Height,
        RTRIM(w.MBMWorkNature) AS WorkNature, RTRIM(w.MBMBlood) AS Blood, RTRIM(w.MBMSmoking) AS Smoking, RTRIM(w.MBMAlcohol) AS Alcohol,
        RTRIM(w.MBMMaritalStatus) AS MaritalStatus, RTRIM(w.MBMPrevPLNCode) AS PreviousPlanCode, w.MBMRBAmt AS RbAmount,
        RTRIM(w.MBMPrevInsCom) AS PreviousInsurer, RTRIM(w.MBMOccupation) AS Occupation, RTRIM(w.MBMCoPayRB) AS CoPayRb,
        w.MBMGenWP AS GeneralWaitingDays, w.MBMGenEndWP AS GeneralWaitingEnd, w.MBMPreExWP AS PreExistingWaitingDays,
        w.MBMPreExEndWP AS PreExistingWaitingEnd, w.MBMSpeIllWP AS SpecificIllnessWaitingDays, w.MBMSpeIllEndWP AS SpecificIllnessWaitingEnd,
        RTRIM(w.MBMStaffDec) AS StaffDiscount, RTRIM(w.MBMBTBG) AS Btbg, w.MBMPayorPremGross AS PayorPremiumGross,
        w.MBMPayorPremNet AS PayorPremiumNet, w.MBMPayorMCOGross AS PayorMcoGross, w.MBMPayorMCONet AS PayorMcoNet,
        w.MBMFamDiscAmt AS FamilyDiscountAmount, w.MBMRenewDiscAmt AS RenewalDiscountAmount, w.MBMLoadingPrem AS LoadingPremium,
        w.MBMExclusion AS Exclusion, RTRIM(w.MBMBankACNo) AS BankAccountNo, w.MBMTopupJoinDate AS TopupJoinDate,
        RTRIM(p.PAYGRPCode) AS PayorGroupCode
    FROM dbo.MBM m
    INNER JOIN dbo.MBMTwo t ON t.MBMNumber = m.MBMNumber
    LEFT JOIN dbo.MBMOthers o ON o.MBMNumber = m.MBMNumber
    LEFT JOIN dbo.MBMOthersTwo w ON w.MBMNumber = m.MBMNumber
    LEFT JOIN dbo.PAY p ON p.PAYCode = m.PAYCode
    WHERE m.MBMNumber = @number;

    SELECT RTRIM(c.MBMCCoverID) AS CoverId, RTRIM(c2.MBMCSalutation) AS Salutation, RTRIM(c.MBMCName) AS Name,
        RTRIM(c.MBMCICBCPPNo) AS IcBcPp, c.MBMCDOB AS DateOfBirth, RTRIM(c.MBMCSex) AS Sex, RTRIM(c.MBMCAdultChild) AS AdultChild,
        RTRIM(c.MBMCRELCode) AS Relationship, RTRIM(c.MBMCPLNCode) AS PlanCode,
        RTRIM(CASE WHEN c.MBMCStatus = 'C' AND c.MBMCEffDate > CAST(GETDATE() AS date) AND c.MBMCCancelDate IS NULL THEN 'A' ELSE c.MBMCStatus END) AS Status,
        c2.MBMCExclusion AS Exclusion, c2.MBMCAllergic AS Allergic, c.MBMCAnnualLimit AS AnnualLimit, c.MBMCAvailableLimit AS AvailableLimit,
        c.MBMCEffDate AS EffectiveDate, c.MBMCExpDate AS ExpiryDate, c.MBMCCancelDate AS CancelDate, c2.MBMCUndExcess AS UnderwritingExcess,
        c.MBMCMCO AS Mco, c.MBMCBasicMCO AS BasicMco, c.MBMCPremium AS Premium, c.MBMCBasicPremium AS BasicPremium,
        RTRIM(c.MBMInvoiceNo) AS InvoiceNo, c.MBMInvoiceDate AS InvoiceDate, RTRIM(c2.MBMCRNote) AS CreditNote, c2.MBMCRNoteDate AS CreditNoteDate,
        RTRIM(c2.MBMCPrevPLNCode) AS PreviousPlanCode, c2.MBMCRBAmt AS RbAmount, RTRIM(c2.MBMCPAYRemarks) AS PayorRemarks,
        RTRIM(c2.MBMCPrevInsCom) AS PreviousInsurer, c2.MBMCGenWP AS GeneralWaitingDays, c2.MBMCGenEndWP AS GeneralWaitingEnd,
        c2.MBMCPreExWP AS PreExistingWaitingDays, c2.MBMCPreExEndWP AS PreExistingWaitingEnd, c2.MBMCSpeIllWP AS SpecificIllnessWaitingDays,
        c2.MBMSpeIllEndWP AS SpecificIllnessWaitingEnd, RTRIM(c2.MBMCStaffDec) AS StaffDiscount, c2.MBMCClmNonPayFr AS ClaimNonPayFrom,
        c2.MBMCClmNonPayTo AS ClaimNonPayTo, c2.MBMCPayorPremGross AS PayorPremiumGross, c2.MBMCPayorPremNet AS PayorPremiumNet,
        c2.MBMCPayorMCOGross AS PayorMcoGross, c2.MBMCPayorMCONet AS PayorMcoNet, c2.MBMCRenewalDisc AS RenewalDisc,
        c2.MBMCPolicyDisc AS PolicyDisc, c2.MBMCFamDiscAmt AS FamilyDiscountAmount, c2.MBMCRenewDiscAmt AS RenewalDiscountAmount,
        c2.MBMCLoadingPrem AS LoadingPremium, RTRIM(c2.MBMCPAYNo) AS PayorMemberNo, RTRIM(c2.MBMCPAYSeqNo) AS PayorSeqNo,
        RTRIM(c2.MBMCClientNO) AS ClientNo, RTRIM(c2.MBMCClientID) AS ClientId, RTRIM(c2.MBMCIcBcPp2nd) AS OtherIc
    FROM dbo.MBMCoveredPersons c
    LEFT JOIN dbo.MBMCoveredPersonsTwo c2 ON c2.MBMCNumber = c.MBMCNumber AND c2.MBMCCoverID = c.MBMCCoverID
    WHERE c.MBMCNumber = @number
    ORDER BY c.MBMCCoverID;

    SELECT TOP (1) RTRIM(PLNType) AS PlanCode, RTRIM(INSType) AS InsuredType, MBMPolEffDate AS EffectiveDate, MBMPolExpDate AS ExpiryDate
    FROM dbo.MBMCLNOthers WHERE MBMNumber = @number;

    SELECT TOP (1) a.AnnLimit AS AnnualLimit, a.DisabilityLmt AS DisabilityLimit, RTRIM(a.SuppLimitStatus) AS SuppLimitStatus,
        RTRIM(l.LimitDesc) AS SuppLimitDescription
    FROM dbo.MBM m
    INNER JOIN dbo.AnnualLimit a ON a.PLNCode = m.PLNCode AND a.INSCode = m.INSCode AND a.HLTCode = m.HLTCode AND a.AnnualEffDate <= m.MBMPayorEffDate
    LEFT JOIN dbo.AnnualLimitType l ON l.LimitCode = a.SuppLimitStatus
    WHERE m.MBMNumber = @number
    ORDER BY a.AnnualEffDate DESC;
END
GO

-- Fees tab: refund rows (the desktop shows the last one per cover ID) and reinstatement rows.
CREATE OR ALTER PROCEDURE genisis.Adjustment_FeeAdjustments
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(MBMCovID) AS CoverId, MBMMCOEffective AS McoEffective, MBMMCORefund AS McoRefund,
        MBMPremEffective AS PremiumEffective, MBMPremReduction AS PremiumReduction
    FROM dbo.MBMRefund WHERE MBMNumber = @number ORDER BY MIndex;
    SELECT RTRIM(MBMCoverID) AS CoverId, MBMMCOFee AS Mco, MBMPremium AS Premium, RTRIM(MBMInvoiceNo) AS InvoiceNo, MBMInvoiceDate AS InvoiceDate
    FROM dbo.MBMReinstatement WHERE MBMNumber = @number;
END
GO

-- MBM History tab (desktop MBMHISListing / SearchMBMHistory06 "Princ"): by IC (dashes ignored) when it is longer than 4
-- characters, otherwise by name and date of birth.
CREATE OR ALTER PROCEDURE genisis.Adjustment_History
    @ic nvarchar(4000) = NULL, @name nvarchar(4000) = NULL, @dob datetime = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT TOP (200) RTRIM(x.MBMNumber) AS MembershipNo, RTRIM(x.MBMTopupInd) AS TopupInd, RTRIM(x.MBMTopupNumber) AS TopupNumber,
        RTRIM(x.MBMPolicyNo) AS PolicyNo, x.MBMPayorEffDate AS EffectiveDate, x.MBMPayorExpDate AS ExpiryDate,
        CASE WHEN m.MBMStatus = 'C' THEN t.MBMENDtEffDate END AS CancelDate, RTRIM(m.MBMDataStatus) AS DataStatus,
        RTRIM(m.MBMStatus) AS Status, x.MBMBordxDate AS BordxDate, t.MBMENDtBordxDate AS EndtBordxDate, m.MBMValueDate AS ValueDate
    FROM genisis.Maint_MBMCrossReference x
    INNER JOIN dbo.MBM m ON m.MBMNumber = x.MBMNumber
    INNER JOIN dbo.MBMTwo t ON t.MBMNumber = x.MBMNumber
    WHERE (LEN(@ic) > 4 AND REPLACE(x.MBMIcBcPp, '-', '') = REPLACE(@ic, '-', ''))
       OR ((@ic IS NULL OR LEN(@ic) <= 4) AND x.MBMName = @name AND x.MBMDOB = @dob)
    ORDER BY x.MBMPayorEffDate DESC
    OPTION (RECOMPILE);
END
GO

-- Last endorsement batch for the bordereaux date (desktop txtEndtBordxDate_LostFocus); 1 when none.
CREATE OR ALTER PROCEDURE genisis.Adjustment_LastBatch
    @bordx datetime, @pay nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ISNULL((SELECT TOP (1) BATSequence FROM dbo.BAT
        WHERE BATBordxDate = @bordx AND BordxType = 'E' AND (@pay IS NULL OR PAYCode = @pay) ORDER BY BATSequence DESC), 1) AS LastBatch;
END
GO

-- Another membership using the policy number (desktop txtMBMPolNo_LostFocus warning).
CREATE OR ALTER PROCEDURE genisis.Adjustment_PolicyOwner
    @policy nvarchar(4000), @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(MBMNumber) AS MembershipNo FROM genisis.Maint_MBMCrossReference WHERE MBMPolicyNo = @policy AND MBMNumber <> @number;
END
GO

CREATE OR ALTER PROCEDURE genisis.Adjustment_ValidDates
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) MBMDOBStart AS DobStart, MBMDOBEnd AS DobEnd, MBMEffStart AS EffectiveStart, MBMEffEnd AS EffectiveEnd,
        MBMExpStart AS ExpiryStart, MBMExpEnd AS ExpiryEnd
    FROM dbo.MBMValidDateRange ORDER BY ValidID DESC;
END
GO

-- Plan settings for fee recalculation (desktop MemberCallFees): health type N plans are per payor.
CREATE OR ALTER PROCEDURE genisis.Adjustment_PlanRates
    @plan nvarchar(4000), @hlt nvarchar(4000), @pay nvarchar(4000), @eff datetime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(PLNRoundUpStatus) AS RoundUpStatus, RTRIM(PLNProRate) AS ProRate, RTRIM(PLNPremGenderStatus) AS PremGenderStatus,
        RTRIM(PRMInd) AS PremiumInd
    FROM dbo.PLN
    WHERE PLNCode = @plan AND HLTCode = @hlt AND PLNEffDate <= @eff AND (@hlt <> 'N' OR PAYCode = @pay)
    ORDER BY PLNEffDate DESC;
END
GO

-- Lifetime flag and supplementary age limit of a plan code (desktop GetSuppExpDate / AddSupplementaryAdjustment).
CREATE OR ALTER PROCEDURE genisis.Adjustment_PlanSettings
    @plan nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(PLNLifeTimeStatus) AS LifetimeStatus, PlnAgeLimit AS AgeLimit, RTRIM(PLNRoundUpStatus) AS RoundUpStatus
    FROM dbo.PLN WHERE PLNCode = @plan ORDER BY PLNEffDate DESC;
END
GO

CREATE OR ALTER PROCEDURE genisis.Adjustment_DiscPlan
    @plan nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) PolicyDisc, RenewalDisc FROM genisis.Maint_DiscPlan WHERE PLNCode = @plan ORDER BY DISIndex;
END
GO

-- Supplementary limit status of a plan code: latest annual limit row regardless of insured / health type (desktop AddSupplementaryAdjustment).
CREATE OR ALTER PROCEDURE genisis.Adjustment_SuppLimit
    @plan nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(SuppLimitStatus) AS SuppLimitStatus, SuppLimit AS SuppLimitAmount FROM dbo.AnnualLimit WHERE PLNCode = @plan ORDER BY AnnualEffDate DESC;
END
GO

-- Supplementary premium status and base amount for a plan / age band / insured type (desktop AddSupplementaryAdjustment).
CREATE OR ALTER PROCEDURE genisis.Adjustment_SuppPremium
    @plan nvarchar(4000), @ageCode nvarchar(4000), @ins nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(SuppPrmStatus) AS SuppPrmStatus, SuppPrmAmt FROM dbo.PRM
    WHERE PLNCode = @plan AND AGECode = @ageCode AND INSCode = @ins ORDER BY PRMEffDate DESC;
END
GO

CREATE OR ALTER PROCEDURE genisis.Adjustment_NextCoverId
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ISNULL(MAX(TRY_CAST(MBMCCoverID AS int)), 0) + 1 AS NextCoverId FROM dbo.MBMCoveredPersons WITH (UPDLOCK, HOLDLOCK) WHERE MBMCNumber = @number;
END
GO

CREATE OR ALTER PROCEDURE genisis.Adjustment_Now
AS
BEGIN
    SET NOCOUNT ON;
    SELECT GETDATE() AS Now;
END
GO

-- Outstanding HIS / clinical case on a covered person (desktop CmdRemove_Click).
CREATE OR ALTER PROCEDURE genisis.Adjustment_OpenCases
    @number nvarchar(4000), @coverId nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM genisis.Maint_CASCrossReference WHERE MBMNumber = @number AND MBMPatientCovID = @coverId AND CASStatus = 'O')
                THEN 1 ELSE 0 END AS bit) AS HisCase,
           CAST(CASE WHEN EXISTS (SELECT 1 FROM genisis.Cis_CISCas WHERE MBMnumber = @number AND CoverID = @coverId AND CASStatus = 'O')
                THEN 1 ELSE 0 END AS bit) AS ClinicalCase;
END
GO

-- ===== Clinical (CISDB) reads for a new supplementary on a clinical member (desktop SaveCISCovPerson) =====

CREATE OR ALTER PROCEDURE genisis.Adjustment_CisPlan
    @plan nvarchar(4000), @hlt nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(DCStatus) AS DcStatus, RTRIM(PAYCode) AS PayorCode, RTRIM(SDentalStatus) AS SuppDentalStatus,
        RTRIM(SSpecStatus) AS SuppSpecialistStatus, RTRIM(SMEPStatus) AS SuppMepStatus, RTRIM(PLNRoundUpStatus) AS RoundUpStatus,
        RTRIM(PLNProRate) AS ProRate, RTRIM(MCOProcfeeStatus) AS McoProcFeeStatus
    FROM genisis.Cis_PLN WHERE PLNCode = @plan AND HLTCode = @hlt;
END
GO

CREATE OR ALTER PROCEDURE genisis.Adjustment_CisLimit
    @ins nvarchar(4000), @plan nvarchar(4000), @hlt nvarchar(4000), @eff datetime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(SuppLimitStatus) AS SuppLimitStatus, SuppLimit, SDentalAnnLimit AS DentalLimit, SSpecAnnLimit AS SpecialistLimit,
        SMEPAnnLimit AS MepLimit
    FROM genisis.Cis_AnnualLimit WHERE INSCode = @ins AND PLNCode = @plan AND HLTCode = @hlt AND AnnEffDate <= @eff ORDER BY AnnEffDate DESC;
    SELECT TOP (1) VisitLimit FROM genisis.Cis_VisitLimit
    WHERE INSCode = @ins AND PLNCode = @plan AND HLTCode = @hlt AND VisitEffDate <= @eff ORDER BY VisitEffDate DESC;
END
GO

-- Result sets: premium status (payor filter only for health types other than S), supplementary premium for the age band and payor.
CREATE OR ALTER PROCEDURE genisis.Adjustment_CisPremium
    @ins nvarchar(4000), @plan nvarchar(4000), @hlt nvarchar(4000), @pay nvarchar(4000), @ageCode nvarchar(4000), @eff datetime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(SuppPRMStatus) AS SuppPrmStatus FROM genisis.Cis_PRM
    WHERE PLNCode = @plan AND HLTCode = @hlt AND INSCode = @ins AND (@hlt = 'S' OR PAYCode = @pay) AND PRMEffDate <= @eff ORDER BY PRMEffDate DESC;
    SELECT TOP (1) SuppPRMAmt AS SuppPrmAmt FROM genisis.Cis_PRM
    WHERE INSCode = @ins AND HLTCode = @hlt AND PLNCode = @plan AND AGECode = @ageCode AND PAYCode = @pay AND PRMEffDate <= @eff
    ORDER BY PRMEffDate DESC;
END
GO

-- Plan-specific MCO row, else the default row (PLNCode NULL).
CREATE OR ALTER PROCEDURE genisis.Adjustment_CisMco
    @ins nvarchar(4000), @plan nvarchar(4000), @hlt nvarchar(4000), @eff datetime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(SuppMCOStatus) AS SuppMcoStatus, MCOSuppAdultAmt AS SuppAdult, MCOSuppChildAmt AS SuppChild
    FROM genisis.Cis_MCO
    WHERE INSCode = @ins AND HLTCode = @hlt AND (PLNCode = @plan OR PLNCode IS NULL) AND MCOEffDate <= @eff
    ORDER BY CASE WHEN PLNCode IS NULL THEN 1 ELSE 0 END, MCOEffDate DESC;
END
GO

-- ===== Writes =====

-- Membership and cross-reference status; a cancellation also sets the final expiry date (desktop Write2MBMnHistory).
CREATE OR ALTER PROCEDURE genisis.Adjustment_SetStatus
    @number nvarchar(4000), @status nvarchar(4000), @finalExpiry datetime = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.Maint_MBMCrossReference SET MBMStatus = @status, MBMFinalExpiryDate = COALESCE(@finalExpiry, MBMFinalExpiryDate)
    WHERE MBMNumber = @number;
    UPDATE dbo.MBM SET MBMStatus = @status, MBMFinalExpiryDate = COALESCE(@finalExpiry, MBMFinalExpiryDate) WHERE MBMNumber = @number;
END
GO

CREATE OR ALTER PROCEDURE genisis.Adjustment_SetEndorsement
    @number nvarchar(4000), @type nvarchar(4000), @endtEff datetime, @endtBordx datetime, @cancel bit
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.MBMTwo SET MBMLastEndorseStatus = @type, MBMENDtEffDate = @endtEff, MBMENDtBordxDate = @endtBordx,
        MBMCancelDate = CASE WHEN @cancel = 1 THEN @endtEff ELSE MBMCancelDate END
    WHERE MBMNumber = @number;
END
GO

CREATE OR ALTER PROCEDURE genisis.Adjustment_InsertAmendHistory
    @number nvarchar(4000), @policy nvarchar(4000), @ic nvarchar(4000), @name nvarchar(4000), @expiry datetime = NULL,
    @plan nvarchar(4000), @type nvarchar(4000), @endtBordx datetime, @endtEff datetime, @user nvarchar(4000), @today datetime,
    @batch int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.MBMAmendHistory (MBMNumber, MBMPolicyNo, MBMIcBcPp, MBMName, MBMExpiryDate, MBMPlanCode, MBMAmendEdtType,
        MBMAEndtBordxDate, MBMAEndtEffDate, MBMAmendUser, MBMAmendDate, MBMAEndtBatchNo)
    VALUES (@number, @policy, @ic, @name, @expiry, CASE WHEN @expiry IS NULL THEN NULL ELSE @plan END, @type,
        @endtBordx, @endtEff, @user, @today, @batch);
END
GO

-- Every covered person gets @status (desktop Write2MBMnHistory for cancellation, suspension and policy deletion).
CREATE OR ALTER PROCEDURE genisis.Adjustment_SetCoveredStatus
    @number nvarchar(4000), @status nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.MBMCoveredPersons SET MBMCStatus = @status WHERE MBMCNumber = @number;
END
GO

-- Desktop WriteAdjStatus2Cov: covered persons not already cancelled (K / C) take @status; @cancelDate (cancellations) is set on all.
-- @coverId NULL = every covered person.
CREATE OR ALTER PROCEDURE genisis.Adjustment_SetCoveredAdjustment
    @number nvarchar(4000), @status nvarchar(4000), @coverId nvarchar(4000) = NULL, @cancelDate datetime = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.MBMCoveredPersons
    SET MBMCStatus = CASE WHEN RTRIM(ISNULL(MBMCStatus, '')) IN ('K', 'C') THEN MBMCStatus ELSE @status END,
        MBMCCancelDate = COALESCE(@cancelDate, MBMCCancelDate)
    WHERE MBMCNumber = @number AND (@coverId IS NULL OR MBMCCoverID = @coverId);
END
GO

-- MCO / premium refund row (the desktop's own Upload_MBMMcoRefund, called unchanged).
CREATE OR ALTER PROCEDURE genisis.Adjustment_InsertRefund
    @number nvarchar(4000), @coverId nvarchar(4000), @premiumEffective decimal(19, 4), @premiumReduction decimal(19, 4),
    @mcoEffective decimal(19, 4), @mcoRefund decimal(19, 4), @pay nvarchar(4000), @bordx datetime, @batch int = NULL, @hlt nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.Upload_MBMMcoRefund @number, @coverId, @premiumEffective, @premiumReduction, @mcoEffective, @mcoRefund, @pay, @bordx, @batch, @hlt;
END
GO

-- Name / date of birth change on a renewal number moves the renewal sequence key and renames every renewal of the base number
-- (desktop SavePrincipalAdjustment; it matched MBMNumber LIKE '%base%', this matches the base number and its *nn renewals only).
CREATE OR ALTER PROCEDURE genisis.Adjustment_RenameRenewals
    @baseNumber nvarchar(4000), @previousKey nvarchar(4000), @newKey nvarchar(4000), @name nvarchar(4000), @dob datetime
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.Maint_MBMSEQRenew05 SET RewMBMSEQKey = @newKey WHERE RewMBMSEQKey = @previousKey;
    UPDATE dbo.MBM SET MBMName = @name, MBMDOB = @dob
    WHERE MBMNumber = @baseNumber OR MBMNumber LIKE REPLACE(REPLACE(@baseNumber, '[', '[[]'), '_', '[_]') + N'*%';
    UPDATE genisis.Maint_MBMCrossReference SET MBMName = @name, MBMDOB = @dob
    WHERE MBMNumber = @baseNumber OR MBMNumber LIKE REPLACE(REPLACE(@baseNumber, '[', '[[]'), '_', '[_]') + N'*%';
END
GO

-- Principal on MBM and the cross-reference (desktop SavePrincipalAdjustment). @availableLimit NULL keeps the stored limit.
CREATE OR ALTER PROCEDURE genisis.Adjustment_SavePrincipal
    @number nvarchar(4000), @name nvarchar(4000), @policy nvarchar(4000), @sex nvarchar(4000), @add1 nvarchar(4000),
    @add2 nvarchar(4000), @add3 nvarchar(4000), @city nvarchar(4000), @state nvarchar(4000), @postCode nvarchar(4000),
    @ic nvarchar(4000), @dob datetime, @race nvarchar(4000), @ageCode nvarchar(4000), @eff datetime = NULL, @exp datetime = NULL,
    @bordx datetime, @takeOver nvarchar(4000), @renewal nvarchar(4000), @dataStatus nvarchar(4000), @status nvarchar(4000),
    @plan nvarchar(4000), @availableLimit decimal(19, 4) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.MBM SET MBMName = @name, MBMPolicyNo = @policy, MBMSex = @sex, MBMAddress1 = @add1, MBMAddress2 = @add2, MBMAddress3 = @add3,
        CTYCode = @city, STACode = @state, MBMPostCode = @postCode, MBMIcBcPp = @ic, MBMDOB = @dob, RACCode = @race, AGECode = @ageCode,
        MBMPayorEffDate = COALESCE(@eff, MBMPayorEffDate), MBMPayorExpDate = COALESCE(@exp, MBMPayorExpDate), MBMBordxDate = @bordx,
        MBMTakeover = @takeOver, MBMRenewFlag = @renewal, MBMDataStatus = @dataStatus, MBMStatus = @status, PLNCode = @plan,
        MBMAvailableLimit = COALESCE(@availableLimit, MBMAvailableLimit)
    WHERE MBMNumber = @number;
    UPDATE genisis.Maint_MBMCrossReference SET MBMName = @name, MBMPolicyNo = @policy, MBMIcBcPp = @ic, MBMDOB = @dob,
        MBMPayorEffDate = COALESCE(@eff, MBMPayorEffDate), MBMPayorExpDate = COALESCE(@exp, MBMPayorExpDate), MBMBordxDate = @bordx,
        MBMDataStatus = @dataStatus, MBMStatus = @status, PLNCode = @plan
    WHERE MBMNumber = @number;
END
GO

CREATE OR ALTER PROCEDURE genisis.Adjustment_SaveMbmTwo
    @number nvarchar(4000), @salutation nvarchar(4000), @email nvarchar(4000), @lgn nvarchar(4000), @type nvarchar(4000),
    @nat nvarchar(4000) = NULL, @telHome nvarchar(4000), @telMobile nvarchar(4000), @telOffice nvarchar(4000), @now datetime,
    @user nvarchar(4000), @mgmtNotes nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.MBMTwo WHERE MBMNumber = @number) INSERT INTO dbo.MBMTwo (MBMNumber) VALUES (@number);
    UPDATE dbo.MBMTwo SET MBMSalutation = @salutation, MBMEmail = @email, LGNCode = @lgn, MBMLastEndorseStatus = @type,
        NATCode = COALESCE(@nat, NATCode), MBMTelnoH = @telHome, MBMTelNoM = @telMobile, MBMTelNoO = @telOffice,
        MBMLastAdjustmentDate = @now, MBMLastUpdateUser = @user, MBMMmgtNotes = @mgmtNotes
    WHERE MBMNumber = @number;
END
GO

-- MBMOthers and MBMOthersTwo (desktop SavePrincipalAdjustment, including the frmNewMBMOthers fields). Rows are created when missing.
CREATE OR ALTER PROCEDURE genisis.Adjustment_SaveOthers
    @number nvarchar(4000), @department nvarchar(4000), @groupCompany nvarchar(4000), @polSubNo nvarchar(4000), @labelStatus nvarchar(4000) = NULL,
    @employeeNo nvarchar(4000), @agentCode nvarchar(4000), @installment nvarchar(4000), @mco decimal(19, 4) = NULL, @branch nvarchar(4000),
    @basicPremium decimal(19, 4) = NULL, @premium decimal(19, 4) = NULL, @joined datetime = NULL, @clmNonPayFrom datetime = NULL,
    @clmNonPayTo datetime = NULL, @newPlan nvarchar(4000), @newPolicyEff datetime = NULL, @policyDisc decimal(19, 4), @renewalDisc decimal(19, 4),
    @polCondition nvarchar(4000), @undExcess decimal(19, 4), @endtRefNo nvarchar(4000), @costCenter nvarchar(4000), @position nvarchar(4000),
    @groupCategory nvarchar(4000), @division nvarchar(4000), @subDivision nvarchar(4000), @subsidiary nvarchar(4000), @nextDue datetime = NULL,
    @allergic nvarchar(4000), @remarks nvarchar(4000), @weight nvarchar(4000), @height nvarchar(4000), @workNature nvarchar(4000),
    @blood nvarchar(4000), @smoking nvarchar(4000), @alcohol nvarchar(4000), @marital nvarchar(4000) = NULL, @prevPlan nvarchar(4000),
    @rbAmount decimal(19, 4), @prevInsurer nvarchar(4000), @occupation nvarchar(4000), @coPayRb nvarchar(4000),
    @genWp decimal(19, 4) = NULL, @genWpEnd datetime = NULL, @preExWp decimal(19, 4) = NULL, @preExWpEnd datetime = NULL,
    @speIllWp decimal(19, 4) = NULL, @speIllWpEnd datetime = NULL, @staffDisc nvarchar(4000), @btbg nvarchar(4000) = NULL,
    @payorPremGross decimal(19, 4) = NULL, @payorPremNet decimal(19, 4), @payorMcoGross decimal(19, 4), @payorMcoNet decimal(19, 4),
    @familyDiscAmount decimal(19, 4), @renewalDiscAmount decimal(19, 4), @loadingPremium decimal(19, 4), @exclusion nvarchar(4000),
    @bankAccountNo nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.MBMOthers WHERE MBMNumber = @number) INSERT INTO dbo.MBMOthers (MBMNumber) VALUES (@number);
    UPDATE dbo.MBMOthers SET MBMDepartment = @department, MBMGroupCompany = @groupCompany, MBMPOLSubNo1 = @polSubNo, LabelStatus = @labelStatus,
        MBMEmployeeNo = @employeeNo, MBMAgentCode = @agentCode, RELCode = 'P', MBMInstallment = @installment,
        MBMMCOFee = COALESCE(@mco, MBMMCOFee), MBMBranch = @branch, MBMBasicPrem = COALESCE(@basicPremium, MBMBasicPrem),
        MBMPremium = COALESCE(@premium, MBMPremium), MBMFirstJoinedDate = COALESCE(@joined, MBMFirstJoinedDate),
        MBMClmNonPayFr = @clmNonPayFrom, MBMClmNonPayTo = @clmNonPayTo, MBMNewPlanCode = @newPlan, MBMNewPolEffDate = @newPolicyEff,
        MBMPolicyDisc = @policyDisc, MBMRenewalDisc = @renewalDisc, MBMPolCondition = @polCondition, MBMUndExcess = @undExcess,
        MBMENDtRefNo = @endtRefNo, MBMCostCenter = @costCenter, MBMPosition = @position, MBMGroupCategory = @groupCategory,
        MBMDivision = @division, MBMSubDivision = @subDivision, Subsidiary = @subsidiary, MBMNextDueDate = COALESCE(@nextDue, MBMNextDueDate)
    WHERE MBMNumber = @number;

    IF NOT EXISTS (SELECT 1 FROM dbo.MBMOthersTwo WHERE MBMNumber = @number) INSERT INTO dbo.MBMOthersTwo (MBMNumber) VALUES (@number);
    UPDATE dbo.MBMOthersTwo SET MBMAllergic = @allergic, MBMRemarks = @remarks, MBMWeight = @weight, MBMHeight = @height,
        MBMWorkNature = @workNature, MBMBlood = @blood, MBMSmoking = @smoking, MBMAlcohol = @alcohol,
        MBMMaritalStatus = COALESCE(@marital, MBMMaritalStatus), MBMPrevPLNCode = @prevPlan, MBMRBAmt = @rbAmount, MBMPrevInsCom = @prevInsurer,
        MBMOccupation = @occupation, MBMCoPayRB = @coPayRb, MBMGenWP = @genWp, MBMGenEndWP = @genWpEnd, MBMPreExWP = @preExWp,
        MBMPreExEndWP = @preExWpEnd, MBMSpeIllWP = @speIllWp, MBMSpeIllEndWP = @speIllWpEnd, MBMStaffDec = @staffDisc, MBMBTBG = @btbg,
        MBMPayorPremGross = COALESCE(@payorPremGross, MBMPayorPremGross), MBMPayorPremNet = @payorPremNet, MBMPayorMCOGross = @payorMcoGross,
        MBMPayorMCONet = @payorMcoNet, MBMFamDiscAmt = @familyDiscAmount, MBMRenewDiscAmt = @renewalDiscAmount, MBMLoadingPrem = @loadingPremium,
        MBMExclusion = @exclusion, MBMBankACNo = @bankAccountNo
    WHERE MBMNumber = @number;
END
GO

-- One existing covered person (desktop SaveSupplementaryAdjustment). @status NULL keeps the status; null dates keep the stored ones.
CREATE OR ALTER PROCEDURE genisis.Adjustment_UpdateCovered
    @number nvarchar(4000), @coverId nvarchar(4000), @name nvarchar(4000), @ic nvarchar(4000), @dob datetime, @sex nvarchar(4000),
    @adultChild nvarchar(4000), @rel nvarchar(4000), @eff datetime = NULL, @exp datetime = NULL, @premium decimal(19, 4) = NULL,
    @plan nvarchar(4000) = NULL, @status nvarchar(4000) = NULL, @salutation nvarchar(4000), @allergic nvarchar(4000), @exclusion nvarchar(4000),
    @clmNonPayFrom datetime = NULL, @clmNonPayTo datetime = NULL, @policyDisc decimal(19, 4), @renewalDisc decimal(19, 4),
    @prevPlan nvarchar(4000), @rbAmount decimal(19, 4), @payRemarks nvarchar(4000), @prevInsurer nvarchar(4000),
    @genWp decimal(19, 4) = NULL, @genWpEnd datetime = NULL, @preExWp decimal(19, 4) = NULL, @preExWpEnd datetime = NULL,
    @speIllWp decimal(19, 4) = NULL, @speIllWpEnd datetime = NULL, @staffDisc nvarchar(4000) = NULL, @payorPremGross decimal(19, 4) = NULL,
    @payorPremNet decimal(19, 4), @payorMcoGross decimal(19, 4), @payorMcoNet decimal(19, 4), @familyDiscAmount decimal(19, 4),
    @renewalDiscAmount decimal(19, 4), @loadingPremium decimal(19, 4), @undExcess decimal(19, 4), @payNo nvarchar(4000),
    @paySeqNo nvarchar(4000), @clientNo nvarchar(4000), @clientId nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.MBMCoveredPersons SET MBMCName = @name, MBMCICBCPPNo = @ic, MBMCDOB = @dob, MBMCSex = @sex, MBMCAdultChild = @adultChild,
        MBMCRELCode = @rel, MBMCEffDate = COALESCE(@eff, MBMCEffDate), MBMCExpDate = COALESCE(@exp, MBMCExpDate),
        MBMCPremium = COALESCE(@premium, MBMCPremium), MBMCPLNCode = COALESCE(@plan, MBMCPLNCode), MBMCStatus = COALESCE(@status, MBMCStatus)
    WHERE MBMCNumber = @number AND MBMCCoverID = @coverId;
    SELECT @@ROWCOUNT AS Updated;

    IF NOT EXISTS (SELECT 1 FROM dbo.MBMCoveredPersonsTwo WHERE MBMCNumber = @number AND MBMCCoverID = @coverId)
        INSERT INTO dbo.MBMCoveredPersonsTwo (MBMCNumber, MBMCCoverID) VALUES (@number, @coverId);
    UPDATE dbo.MBMCoveredPersonsTwo SET MBMCSalutation = @salutation, MBMCAllergic = @allergic, MBMCExclusion = @exclusion,
        MBMCClmNonPayFr = @clmNonPayFrom, MBMCClmNonPayTo = @clmNonPayTo, MBMCPolicyDisc = @policyDisc, MBMCRenewalDisc = @renewalDisc,
        MBMCPrevPLNCode = @prevPlan, MBMCRBAmt = @rbAmount, MBMCPAYRemarks = @payRemarks, MBMCPrevInsCom = @prevInsurer,
        MBMCGenWP = @genWp, MBMCGenEndWP = @genWpEnd, MBMCPreExWP = @preExWp, MBMCPreExEndWP = @preExWpEnd, MBMCSpeIllWP = @speIllWp,
        MBMSpeIllEndWP = @speIllWpEnd, MBMCStaffDec = COALESCE(@staffDisc, MBMCStaffDec), MBMCPayorPremGross = @payorPremGross,
        MBMCPayorPremNet = @payorPremNet, MBMCPayorMCOGross = @payorMcoGross, MBMCPayorMCONet = @payorMcoNet,
        MBMCFamDiscAmt = @familyDiscAmount, MBMCRenewDiscAmt = @renewalDiscAmount, MBMCLoadingPrem = @loadingPremium,
        MBMCUndExcess = @undExcess, MBMCPAYNo = @payNo, MBMCPAYSeqNo = @paySeqNo, MBMCClientNO = @clientNo, MBMCClientID = @clientId
    WHERE MBMCNumber = @number AND MBMCCoverID = @coverId;
END
GO

-- New covered person (desktop AddSupplementaryAdjustment). Clinical-only members (no HIS plan) get no effective / expiry date.
CREATE OR ALTER PROCEDURE genisis.Adjustment_InsertCovered
    @number nvarchar(4000), @coverId nvarchar(4000), @ageCode nvarchar(4000), @status nvarchar(4000), @bordx datetime, @batch int = NULL,
    @name nvarchar(4000), @ic nvarchar(4000), @dob datetime, @sex nvarchar(4000), @adultChild nvarchar(4000), @rel nvarchar(4000),
    @today datetime, @plan nvarchar(4000), @eff datetime = NULL, @exp datetime = NULL, @annual decimal(19, 4), @basicPremium decimal(19, 4),
    @premium decimal(19, 4), @basicMco decimal(19, 4), @mco decimal(19, 4), @salutation nvarchar(4000), @allergic nvarchar(4000),
    @exclusion nvarchar(4000), @payorPremNet decimal(19, 4), @payorMcoGross decimal(19, 4), @payorMcoNet decimal(19, 4),
    @loadingPremium decimal(19, 4), @familyDiscAmount decimal(19, 4), @renewalDiscAmount decimal(19, 4), @undExcess decimal(19, 4),
    @renewalDisc decimal(19, 4), @policyDisc decimal(19, 4), @clmNonPayFrom datetime = NULL, @clmNonPayTo datetime = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.MBMCoveredPersons (MBMCNumber, MBMCCoverID, MBMCAgeband, MBMCStatus, MBMCBordxDate, MBMCBatchNo, MBMCName, MBMCICBCPPNo,
        MBMCDOB, MBMCSex, MBMCAdultChild, MBMCRELCode, MBMCMemberType, MBMCRegDate, MBMCPLNCode, MBMCEffDate, MBMCExpDate,
        MBMCAnnualLimit, MBMCAvailableLimit, MBMCBasicPremium, MBMCPremium, MBMCBasicMCO, MBMCMCO)
    VALUES (@number, @coverId, @ageCode, @status, @bordx, @batch, @name, @ic, @dob, @sex, @adultChild, @rel, 'C', @today, @plan, @eff, @exp,
        @annual, @annual, @basicPremium, @premium, @basicMco, @mco);
    INSERT INTO dbo.MBMCoveredPersonsTwo (MBMCNumber, MBMCCoverID, MBMCSalutation, MBMCAllergic, MBMCExclusion, MBMCPayorPremNet,
        MBMCPayorMCOGross, MBMCPayorMCONet, MBMCLoadingPrem, MBMCFamDiscAmt, MBMCRenewDiscAmt, MBMCUndExcess, MBMCRenewalDisc,
        MBMCPolicyDisc, MBMCClmNonPayFr, MBMCClmNonPayTo)
    VALUES (@number, @coverId, @salutation, @allergic, @exclusion, @payorPremNet, @payorMcoGross, @payorMcoNet, @loadingPremium,
        @familyDiscAmount, @renewalDiscAmount, @undExcess, @renewalDisc, @policyDisc,
        CASE WHEN @clmNonPayFrom IS NOT NULL AND @clmNonPayTo IS NOT NULL THEN @clmNonPayFrom END,
        CASE WHEN @clmNonPayFrom IS NOT NULL AND @clmNonPayTo IS NOT NULL THEN @clmNonPayTo END);
END
GO

-- Clinical covered person row (the desktop's own Upload_MBMCovPersonsCis, called unchanged).
CREATE OR ALTER PROCEDURE genisis.Adjustment_InsertCoveredClinical
    @number nvarchar(4000), @coverId nvarchar(4000), @mco decimal(19, 4), @basicMco decimal(19, 4), @premium decimal(19, 4),
    @basicPremium decimal(19, 4), @annual decimal(19, 4), @visit decimal(19, 4), @dental decimal(19, 4), @specialist decimal(19, 4),
    @mep decimal(19, 4), @today datetime, @user nvarchar(4000), @expiry datetime = NULL, @plan nvarchar(4000),
    @invoiceNo nvarchar(4000), @invoiceDate datetime = NULL, @invoiceBy nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.Upload_MBMCovPersonsCis @number, @coverId, @mco, @basicMco, @premium, @basicPremium, @annual, @visit, @dental, @specialist, @mep,
        @today, @user, @today, @user, @expiry, '', '', 'A', @plan, @invoiceNo, @invoiceDate, @invoiceBy;
END
GO

-- Removes a covered person and its limits (desktop DeleteSupplementary). @topupNo is set for guarantee renewal 3 (lifetime rows).
CREATE OR ALTER PROCEDURE genisis.Adjustment_DeleteCovered
    @number nvarchar(4000), @coverId nvarchar(4000), @topupNo nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.MBMCoveredPersons WHERE MBMCNumber = @number AND MBMCCoverID = @coverId;
    DELETE FROM dbo.MBMCoveredPersonsTwo WHERE MBMCNumber = @number AND MBMCCoverID = @coverId;
    DELETE FROM dbo.MBMCoveredPersonsCLN WHERE MBMCNumber = @number AND MBMCCoverID = @coverId;
    IF NULLIF(@topupNo, N'') IS NOT NULL DELETE FROM dbo.MBMLifeTime WHERE MBMNumber = @topupNo AND MBMCovID = @coverId;
    DELETE FROM dbo.MBMBnfLmt WHERE MBMNumber = @number AND MBMCoveredID = @coverId;
END
GO

CREATE OR ALTER PROCEDURE genisis.Adjustment_UpdateMcoPending
    @number nvarchar(4000), @bordx datetime
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.Maint_MCOPending SET MBMNewBordxDate = @bordx WHERE MBMNumber = @number;
END
GO

-- ===== main/150_enquiry_tabs.sql =====
-- Membership > Enquiry: the tabs added after the first read-only release (HIS Benefits, Acc History, Fees, Non Disclosure,
-- Policy Terms, LT Limit, Policy Wording, T/O Claims, WS). The desktop built these queries from string fragments; here they take parameters.

-- HIS Benefits (desktop BNFListing / BNFListingSupp): the benefit schedule in force on the payor effective date, latest effective date only.
CREATE OR ALTER PROCEDURE genisis.Enquiry_Benefits
    @plan nvarchar(4000), @hlt nvarchar(4000), @ins nvarchar(4000), @eff datetime
AS
BEGIN
    SET NOCOUNT ON;
    WITH latest AS (
        SELECT MAX(BNFEffDate) AS EffDate FROM dbo.VIEW_BNF
        WHERE PLNCode = @plan AND HLTCode = @hlt AND INSCode = @ins AND BNFEffDate <= @eff)
    SELECT RTRIM(b.BNFCode) AS BenefitCode, RTRIM(b.BNFdescription) AS Description, ISNULL(b.BNFNoOfDays, 0) AS Days,
        RTRIM(b.BNFClaimAbleAmount) AS RatePerDay, RTRIM(b.BNFRMPerDis) AS PerDisability, b.BNFEffDate AS EffectiveDate
    FROM dbo.VIEW_BNF b JOIN latest l ON b.BNFEffDate = l.EffDate
    WHERE b.PLNCode = @plan AND b.HLTCode = @hlt AND b.INSCode = @ins
    ORDER BY b.BNFCode;
END
GO

-- Acc History (desktop GetAccHisListing): claims paid and receipts for the IC, from the worksheet database.
CREATE OR ALTER PROCEDURE genisis.Enquiry_AccountHistory
    @ic nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(MBMNumber) AS MembershipNo, MBMPayorEffDate AS EffectiveDate, CASDateAdmitted AS Admitted, CaseRegDate AS Registered,
        RTRIM(CASNumber) + '-' + CAST(ClaimVersion AS nvarchar(10)) AS CaseNo,
        CASE WHEN ISNULL(CLMPayableByMedix2M, 0) <> 0 THEN RTRIM(DebitCreditRefNo) END AS DebitCreditRefNo,
        CASE WHEN ISNULL(CLMPayableByMedix2M, 0) <> 0 THEN DebitCreditDate END AS DebitCreditDate,
        CLMPayableByMedix2M AS Payable, RTRIM(ReceiptNo) AS ReceiptNo, ReceiptDate, ISNULL(ReceiptAmt, 0) AS ReceiptAmount,
        ISNULL(ReceiptAmt, 0) + ISNULL(CLMPayableByMedix2M, 0) AS Balance
    FROM genisis.WS_AccountHistory WHERE MBMIcBcPp = @ic;
END
GO

-- Fees tab: premium, MCO and access fee with the refund recorded for a cancelled membership (desktop showMBM).
CREATE OR ALTER PROCEDURE genisis.Enquiry_Fees
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT o.MBMPremium AS Premium, o.MBMMCOFee AS McoFee, o.MBMAccessFee AS AccessFee, o.MBMBasicPrem AS BasicPremium, o.MBMBasicMCO AS BasicMco,
        r.MBMPremReduction AS PremiumRefund, r.MBMMCORefund AS McoRefund, r.MBMProviderRef AS AccessFeeRefund, r.MBMProviderEff AS AccessFeeEffective,
        r.MBMMCOEffective AS McoEffective, r.MBMPremEffective AS PremiumEffective
    FROM dbo.MBM m
    LEFT JOIN dbo.MBMOthers o ON o.MBMNumber = m.MBMNumber
    OUTER APPLY (SELECT TOP (1) * FROM dbo.MBMRefund x WHERE x.MBMNumber = m.MBMNumber AND m.MBMStatus = 'C' ORDER BY x.MIndex) r
    WHERE m.MBMNumber = @number;
END
GO

-- Non Disclosure (desktop SearchPrincipalIll / Case_SearchIllness): pre-existing and underlying illnesses recorded on cases for the IC.
CREATE OR ALTER PROCEDURE genisis.Enquiry_NonDisclosure
    @ic nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(MBMNumber) AS MembershipNo, RTRIM(CASNumber) AS CaseNo, RTRIM(MBMPatientCovID) AS CoverId, CASDateAdmitted AS Admitted,
        CASPreIllDate AS PreExistingDate,
        CASE RTRIM(CASPreIllCAT) WHEN '1' THEN 'Illness' WHEN '2' THEN 'Injury' WHEN '3' THEN 'Hospitalisation' WHEN '4' THEN CASPreIllOthers END AS PreExisting,
        RTRIM(CASPreIllInfBy) AS PreExistingInformedBy, CASUndIllDate AS UnderlyingDate,
        CASE RTRIM(CASUndIllCAT) WHEN '1' THEN 'Diabetes' WHEN '2' THEN 'Hypertension' WHEN '3' THEN CASUndIllOthers END AS Underlying,
        RTRIM(CASUndIllInfBy) AS UnderlyingInformedBy
    FROM dbo.VIEW_Cas_SearchIll WHERE MBMIcBcPp = @ic;
END
GO

-- Policy Terms (desktop PolTermsListing / Case_Registration case 6): health type N plans are per payor.
CREATE OR ALTER PROCEDURE genisis.Enquiry_PolicyTerms
    @plan nvarchar(4000), @hlt nvarchar(4000), @pay nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) PLNRefRqdAdmin AS ReferralForAdmission, PLNRefRqdPreSpec AS ReferralForSpecialist, PLNMinDurHosp AS MinimumHospitalStay,
        PLNPhyVstPerDay AS PhysicianVisitsPerDay, PLNSepPeriodPrin AS SeparationPeriodPrincipal, PLNSepPeriodSup AS SeparationPeriodSupplementary,
        PLNLowerAgeLimit AS LowerAgeLimit, PLNUpperAgeLimit AS UpperAgeLimit, PLNStudentAge AS StudentAge, PLNChildUntilAge AS ChildUntilAge
    FROM dbo.PLN WHERE PLNCode = @plan AND (@hlt = 'S' OR (@hlt = 'N' AND PAYCode = @pay));
END
GO

-- LT Limit (desktop LTLmtListing / SP_SearchLTLmt): annual and lifetime balances with current and historical cases, principal then supplementaries.
CREATE OR ALTER PROCEDURE genisis.Enquiry_LifetimeLimits
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(p.MBMNumber) AS MembershipNo, RTRIM(p.MBMCOVID) AS CoverId, RTRIM(p.MBMName) AS Name, ISNULL(p.MBMAvailableLimit, 0) AS AnnualBalance,
        CASE WHEN RTRIM(p.MBMGuaRenewal) = '3' THEN ISNULL((SELECT TOP (1) MBMLifeTimeLmt FROM dbo.View_Search_LTPrinLmt WHERE MBMNumber = p.MBMTopupNumber), 0)
             ELSE ISNULL(p.MBMAvailableLimit, 0) END AS LifetimeBalance,
        ISNULL(c.Cases, 0) AS Cases, ISNULL(h.CasHistory, 0) AS CaseHistory, ISNULL(c.BordxAmt, 0) AS CaseAmount, ISNULL(h.BordxAmt, 0) AS HistoryAmount
    FROM dbo.View_Search_LTPrinLmt p
    OUTER APPLY (SELECT TOP (1) Cases, BordxAmt FROM dbo.View_Search_LTPrinCase WHERE MBMNumber = p.MBMNumber) c
    OUTER APPLY (SELECT TOP (1) CasHistory, BordxAmt FROM dbo.View_Search_LTPrinCaseHistory WHERE MBMTopupNumber = p.MBMTopupNumber) h
    WHERE p.MBMNumber = @number
    UNION ALL
    SELECT RTRIM(s.MBMCNumber), RTRIM(s.MBMCCoverID), RTRIM(s.MBMCName), ISNULL(s.MBMCAvailableLimit, 0),
        CASE WHEN RTRIM(p.MBMGuaRenewal) = '3' THEN ISNULL((SELECT TOP (1) MBMLifeTimeLmt FROM dbo.View_Search_LTSuppLmt
                 WHERE MBMCNumber = p.MBMTopupNumber AND MBMCCoverID = s.MBMCCoverID), 0)
             ELSE ISNULL(s.MBMCAvailableLimit, 0) END,
        ISNULL(c.Cases, 0), ISNULL(h.CasHistory, 0), ISNULL(c.BordxAmt, 0), ISNULL(h.BordxAmt, 0)
    FROM dbo.View_Search_LTPrinLmt p
    JOIN dbo.View_Search_LTSuppLmt s ON s.MBMCNumber = p.MBMNumber
    OUTER APPLY (SELECT TOP (1) Cases, BordxAmt FROM dbo.View_Search_LTSuppCase WHERE MBMCNumber = s.MBMCNumber AND MBMCCoverID = s.MBMCCoverID) c
    OUTER APPLY (SELECT TOP (1) CasHistory, BordxAmt FROM dbo.VIEW_Search_LTSuppCaseHistory WHERE MBMTopupNumber = s.MBMTopupNumber AND MBMCCoverID = s.MBMCCoverID) h
    WHERE p.MBMNumber = @number AND RTRIM(p.INSCode) IN ('F', 'H');
END
GO

-- T/O Claims (desktop TakeoverClaims): previous insurer's claims for the policy number.
CREATE OR ALTER PROCEDURE genisis.Enquiry_TakeoverClaims
    @policy nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CLMEffDate AS EffectiveDate, CLMExpDate AS ExpiryDate, RTRIM(CLMPatientname) AS Patient, RTRIM(CLMPatientIC) AS PatientIc,
        CLMDOA AS Admitted, CLMDOD AS Discharged, RTRIM(CLMHospital) AS Hospital,
        RTRIM(ISNULL(CLMDiagnosis1, '')) + ISNULL('/' + NULLIF(RTRIM(CLMDiagnosis2), ''), '') + ISNULL('/' + NULLIF(RTRIM(CLMDiagnosis3), ''), '') AS Diagnosis,
        RTRIM(ISNULL(CLMDrName1, '')) + ISNULL('/' + NULLIF(RTRIM(CLMDrName2), ''), '') AS Doctor,
        CLMBillAmt AS BillAmount, CLMAppAmt AS ApprovedAmount, CLMExcessAmt AS ExcessAmount
    FROM dbo.CLMMAATakaful WHERE CLMPolNo = @policy;
END
GO

-- Plan policy wording folder name (desktop showMBM: PLN.PLNPolicyWording).
CREATE OR ALTER PROCEDURE genisis.Enquiry_PolicyWording
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(p.PLNPolicyWording) AS PolicyWording
    FROM dbo.MBM m JOIN dbo.PLN p ON p.PLNCode = m.PLNCode AND p.HLTCode = m.HLTCode AND (m.HLTCode = 'S' OR p.PAYCode = m.PAYCode)
    WHERE m.MBMNumber = @number;
END
GO

-- ===== main/160_bordx.sql =====
-- Membership > Upload Bordx (legacy FrmNewMBMEndt10), standard format. The desktop stages new members in UploadDb through its
-- TmpUpload_* procedures and copies them into HISDB with SaveUploadDate; the API does the same, inside one transaction, keyed by a
-- per-run @runKey so concurrent uploads never see each other's staged rows.

-- ===== Lookups =====

CREATE OR ALTER PROCEDURE genisis.Bordx_Payor
    @pay nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(BordxFormatType) AS BordxFormatType, RTRIM(CLNStatus) AS ClinicalStatus, RTRIM(MBMCancelStatus) AS CancelStatus,
        RTRIM(PAYMBMRegStatus) AS RegStatus
    FROM dbo.PAY WHERE PAYCode = @pay;
END
GO

-- Codes the verification checks against: insured types, relationships (data types), renewal types.
CREATE OR ALTER PROCEDURE genisis.Bordx_Codes
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(INSCode) AS Code FROM dbo.INS WHERE INSCode IS NOT NULL;
    SELECT RTRIM(RELCode) AS Code FROM dbo.REL WHERE RELCode IS NOT NULL;
    SELECT RTRIM(RenewalType) AS Code FROM genisis.Maint_Renewal WHERE RenewalType IS NOT NULL;
END
GO

-- Plan by HIS plan code (@plan) or by the payor's own plan code (@payorPlan, ZURICH / TAK format).
CREATE OR ALTER PROCEDURE genisis.Bordx_Plan
    @plan nvarchar(4000) = NULL, @payorPlan nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(PLNCode) AS PlanCode, RTRIM(PLNLifeTimeStatus) AS LifetimeStatus, RTRIM(PLNPayorCode) AS PayorCode, PlnAgeLimit AS AgeLimit
    FROM dbo.PLN
    WHERE (@plan IS NOT NULL AND PLNCode = @plan) OR (@payorPlan IS NOT NULL AND PLNPayorPlan = @payorPlan);
END
GO

-- Active cross-reference rows for an IC (desktop "Same IC ... Existed" check).
CREATE OR ALTER PROCEDURE genisis.Bordx_ActiveByIc
    @ic nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(MBMNumber) AS MembershipNo, RTRIM(MBMIcBcPp) AS Ic, MBMPayorExpDate AS ExpiryDate
    FROM genisis.Maint_MBMCrossReference WHERE MBMIcBcPp = @ic AND MBMStatus = 'A';
END
GO

-- Latest active membership for the IC / payor / insured type (desktop renewal-type top-up check).
CREATE OR ALTER PROCEDURE genisis.Bordx_TopupCheck
    @ic nvarchar(4000), @pay nvarchar(4000), @ins nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(MBMNumber) AS MembershipNo, RTRIM(INSCode) AS InsuredType, RTRIM(MBMTopupNumber) AS TopupNumber
    FROM genisis.Maint_MBMCrossReference
    WHERE MBMIcBcPp = @ic AND MBMStatus = 'A' AND PAYCode = @pay AND INSCode = @ins
    ORDER BY MBMPayorEffDate DESC, MBMNumber;
END
GO

-- Endorsement file checks: open case / already cancelled.
CREATE OR ALTER PROCEDURE genisis.Bordx_EndorsementState
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM genisis.Maint_CASCrossReference WHERE CASStatus = 'O' AND MBMNumber = @number) THEN 1 ELSE 0 END AS bit) AS OpenCase,
           CAST(CASE WHEN EXISTS (SELECT 1 FROM genisis.Maint_MBMCrossReference WHERE MBMStatus = 'C' AND MBMNumber = @number) THEN 1 ELSE 0 END AS bit) AS Cancelled;
END
GO

-- ===== New member calculation reads (desktop SPHISDBTableRecAssign / SPCisDBTableRecAssign and the Get* helpers) =====

CREATE OR ALTER PROCEDURE genisis.Bordx_HisPlan
    @plan nvarchar(4000), @hlt nvarchar(4000), @pay nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(PLNTopUPStatus) AS TopupStatus, RTRIM(PLNProRate) AS ProRate, RTRIM(PRMInd) AS PremiumInd, RTRIM(MCOInd) AS McoInd,
        RTRIM(AnnLmtIND) AS AnnualLimitInd, RTRIM(PLNLifeTimeStatus) AS LifetimeStatus, PlnAgeLimit AS AgeLimit,
        RTRIM(PLNRoundUpStatus) AS RoundUpStatus, RTRIM(PLNPremGenderStatus) AS PremGenderStatus, RTRIM(PLNGuaranteeStatus) AS GuaranteeStatus
    FROM dbo.PLN WHERE PLNCode = @plan AND HLTCode = @hlt AND (@hlt <> 'N' OR PAYCode = @pay)
    ORDER BY PLNEffDate DESC;
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_CisPlan
    @plan nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(DCStatus) AS DcStatus, RTRIM(PRMInd) AS PremiumInd, RTRIM(MCOInd) AS McoInd, RTRIM(ANNLmtInd) AS AnnualLimitInd,
        RTRIM(VISLmtInd) AS VisitLimitInd, RTRIM(PAYCode) AS PayorCode, RTRIM(PDentalStatus) AS DentalStatus, RTRIM(PSpecStatus) AS SpecialistStatus,
        RTRIM(PMEPStatus) AS MepStatus, PLNAgeLimit AS AgeLimit, RTRIM(PLNRoundUpStatus) AS RoundUpStatus, RTRIM(MCOProcfeeStatus) AS McoProcFeeStatus,
        RTRIM(PLNProRate) AS ProRate
    FROM genisis.Cis_PLN WHERE PLNCode = @plan ORDER BY PLNEffDate DESC;
END
GO

-- Clinical rates for one person. Result sets: annual limit row, visit limit, premium row (payor filter except health type S),
-- MCO row (plan-specific, else default).
CREATE OR ALTER PROCEDURE genisis.Bordx_CisRates
    @ins nvarchar(4000), @plan nvarchar(4000), @hlt nvarchar(4000), @eff datetime, @premiumDate datetime, @pay nvarchar(4000), @ageCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(SuppLimitStatus) AS SuppLimitStatus, AnnLimit, SuppLimit, PDentalAnnLimit AS DentalLimit, PSpecAnnLimit AS SpecialistLimit,
        PMEPAnnLimit AS MepLimit
    FROM genisis.Cis_AnnualLimit WHERE INSCode = @ins AND PLNCode = @plan AND HLTCode = @hlt AND AnnEffDate <= @eff ORDER BY AnnEffDate DESC;
    SELECT TOP (1) VisitLimit FROM genisis.Cis_VisitLimit
    WHERE INSCode = @ins AND PLNCode = @plan AND HLTCode = @hlt AND VisitEffDate <= @eff ORDER BY VisitEffDate DESC;
    SELECT TOP (1) RTRIM(SuppPRMStatus) AS SuppPrmStatus, PRMAmount, SuppPRMAmt AS SuppPrmAmt FROM genisis.Cis_PRM
    WHERE INSCode = @ins AND HLTCode = @hlt AND PLNCode = @plan AND AGECode = @ageCode AND PRMEffDate <= @premiumDate AND (@hlt = 'S' OR PAYCode = @pay)
    ORDER BY PRMEffDate DESC;
    SELECT TOP (1) RTRIM(SuppMCOStatus) AS SuppMcoStatus, MCOAmtAdult AS Adult, MCOAmtChild AS Child, MCOSuppAdultAmt AS SuppAdult, MCOSuppChildAmt AS SuppChild
    FROM genisis.Cis_MCO WHERE INSCode = @ins AND HLTCode = @hlt AND (PLNCode = @plan OR PLNCode IS NULL) AND MCOEffDate <= @eff
    ORDER BY CASE WHEN PLNCode IS NULL THEN 1 ELSE 0 END, MCOEffDate DESC;
END
GO

-- Top-up: latest cross-reference row for the IC under the payor and insured type (desktop PLNTopUpStatus = Y).
CREATE OR ALTER PROCEDURE genisis.Bordx_TopupByIc
    @ic nvarchar(4000), @pay nvarchar(4000), @ins nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(MBMNumber) AS MembershipNo FROM genisis.Maint_MBMCrossReference
    WHERE MBMIcBcPp = @ic AND LEFT(MBMNumber, 2) = @pay AND INSCode = @ins ORDER BY MBMPayorEffDate DESC;
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_PreviousCoveredLimit
    @previousNo nvarchar(4000), @name nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) MBMCAvailableLimit AS Value FROM dbo.MBMCoveredPersons WHERE MBMCNumber = @previousNo AND MBMCName = @name;
END
GO

-- Clinical-only membership sequence (desktop GenerateMembershipNo, MBMSEQ by payor + insured type): A00001 .. Z99999.
CREATE OR ALTER PROCEDURE genisis.Bordx_NextClinicalSeq
    @pay nvarchar(4000), @ins nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @code nvarchar(20) = LTRIM(RTRIM(@pay + @ins)), @current nvarchar(10), @next nvarchar(10);
    SELECT @current = RTRIM(SEQNumber) FROM genisis.Maint_MBMSEQ WITH (UPDLOCK, HOLDLOCK) WHERE SEQCode = @code;
    IF @current IS NULL
    BEGIN
        SET @next = N'A00001';
        INSERT INTO genisis.Maint_MBMSEQ (SEQCode, PAYCode, INSCode, SEQNumber) VALUES (@code, @pay, @ins, @next);
    END
    ELSE
    BEGIN
        DECLARE @n int = TRY_CAST(SUBSTRING(@current, 2, 5) AS int), @c nchar(1) = LEFT(@current, 1);
        SET @next = CASE WHEN @n = 99999 THEN NCHAR(UNICODE(@c) + 1) + N'00001' ELSE @c + RIGHT(N'00000' + CAST(@n + 1 AS nvarchar(5)), 5) END;
        UPDATE genisis.Maint_MBMSEQ SET SEQNumber = @next WHERE SEQCode = @code;
    END
    SELECT @next AS Sequence;
END
GO

-- Desktop CheckBatch: next batch number for the payor / bordereaux date / file type.
CREATE OR ALTER PROCEDURE genisis.Bordx_NextBatch
    @pay nvarchar(4000), @bordx datetime, @type nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @batch int;
    SELECT @batch = BATSequence + 1 FROM dbo.BAT WITH (UPDLOCK, HOLDLOCK) WHERE PAYCode = @pay AND BATBordxDate = @bordx AND BordxType = @type;
    IF @batch IS NULL
    BEGIN
        SET @batch = 1;
        INSERT INTO dbo.BAT (PAYCode, BATBordxDate, BATSequence, BordxType) VALUES (@pay, @bordx, 1, @type);
    END
    ELSE UPDATE dbo.BAT SET BATSequence = @batch WHERE PAYCode = @pay AND BATBordxDate = @bordx AND BordxType = @type;
    SELECT @batch AS Batch;
END
GO

-- ===== Staging (the desktop's own procedures, called with named parameters) =====
-- The current TmpUpload_MBM_HIS / _MBMCovPersonsNew / _MBMOthers_2015 have required parameters the desktop never passes
-- (top-up IC, deductible, CMC / agent name); they are sent blank here.

CREATE OR ALTER PROCEDURE genisis.Bordx_ClearStaging
    @runKey nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.DeleteTmpUploadDate @UpdatedBy = @runKey;
END
GO

-- Copies the staged rows into HISDB / HISMaintenance. SaveUploadDate swallows its own errors with a bare ROLLBACK, which inside the
-- API's transaction surfaces as a transaction-count error, so a failed copy can never look like a success.
CREATE OR ALTER PROCEDURE genisis.Bordx_CommitStaging
    @runKey nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.SaveUploadDate @UpdatedBy = @runKey;
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_StagePrincipal
    @runKey nvarchar(4000), @pay nvarchar(4000), @hlt nvarchar(4000), @number nvarchar(4000), @policy nvarchar(4000), @coverId nvarchar(4000),
    @ic nvarchar(4000), @name nvarchar(4000), @dob datetime = NULL, @sex nvarchar(4000), @race nvarchar(4000), @status nvarchar(4000),
    @dataStatus nvarchar(4000), @memberType nvarchar(4000), @add1 nvarchar(4000), @add2 nvarchar(4000), @add3 nvarchar(4000), @city nvarchar(4000),
    @postCode nvarchar(4000), @state nvarchar(4000), @expiredStatus nvarchar(4000), @valueDate datetime, @eff datetime = NULL, @exp datetime = NULL,
    @takeOver nvarchar(4000), @renewal nvarchar(4000), @bordx datetime, @batch int, @plan nvarchar(4000), @ins nvarchar(4000), @ageCode nvarchar(4000),
    @adultChild nvarchar(4000), @available decimal(19, 4), @hsClnInd nvarchar(4000), @topupNo nvarchar(4000), @topupInd nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.TmpUpload_MBM_HIS @PAYCode = @pay, @HLTCode = @hlt, @MBMNumber = @number, @MBMPolicyNo = @policy, @MBMCOVID = @coverId,
        @MBMIcBcPp = @ic, @MBMName = @name, @MBMDOB = @dob, @MBMSex = @sex, @RACCode = @race, @MBMStatus = @status, @MBMDataStatus = @dataStatus,
        @MBMMemberType = @memberType, @MBMAddress1 = @add1, @MBMAddress2 = @add2, @MBMAddress3 = @add3, @CTYCode = @city, @MBMPostCode = @postCode,
        @STACode = @state, @MBMExpiredStatus = @expiredStatus, @MBMValueDate = @valueDate, @MBMPayorEffDate = @eff, @MBMPayorEXPDate = @exp,
        @MBMTakeOver = @takeOver, @MBMRenewFlag = @renewal, @MBMBordxDate = @bordx, @MBMBatchNo = @batch, @PLNCode = @plan, @INSCode = @ins,
        @AgeCode = @ageCode, @MBMAdultChild = @adultChild, @MBMAvailableLimit = @available, @HSClnInd = @hsClnInd, @MBMTopupNumber = @topupNo,
        @MBMTopupInd = @topupInd, @UpdatedBy = @runKey, @MBMTINumber = '', @MBMTIcoverID = '', @DEDUCT_LIMIT = NULL;
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_StageMbmTwo
    @runKey nvarchar(4000), @number nvarchar(4000), @salutation nvarchar(4000), @telHome nvarchar(4000), @telMobile nvarchar(4000),
    @telOffice nvarchar(4000), @nat nvarchar(4000), @language nvarchar(4000), @user nvarchar(4000), @clinical nvarchar(4000), @appName nvarchar(4000),
    @otherIc nvarchar(4000), @fileName nvarchar(4000), @today datetime, @agentName nvarchar(4000), @agentEmail nvarchar(4000), @agentPhone nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.TmpUpload_MBMTwo_Agent @MBMNumber = @number, @MBMSalutation = @salutation, @MBMTelnoH = @telHome, @MBMTelNoM = @telMobile,
        @MBMTelNoO = @telOffice, @NATCode = @nat, @LGNCode = @language, @SRVCodeList = 'ME', @MBMRegUser = @user, @MBMLastUpdateUser = @user,
        @MBMClinical = @clinical, @MBMAppName = @appName, @MBMIcBcPp2nd = @otherIc, @MBMUploadStatus = 'A', @MBMFilename = @fileName,
        @MBMENDtEffDate = @today, @UpdatedBy = @runKey, @MBMAgentName = @agentName, @MBMAgentEmail = @agentEmail, @MBMAgentPhoneNo = @agentPhone;
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_StageXref
    @runKey nvarchar(4000), @number nvarchar(4000), @policy nvarchar(4000), @ic nvarchar(4000), @name nvarchar(4000), @eff datetime = NULL,
    @exp datetime = NULL, @ageCode nvarchar(4000), @ins nvarchar(4000), @plan nvarchar(4000), @bordx datetime, @topupNo nvarchar(4000),
    @topupInd nvarchar(4000), @dataStatus nvarchar(4000), @status nvarchar(4000), @dob datetime = NULL, @hlt nvarchar(4000), @pay nvarchar(4000), @batch int
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.TmpUpload_MBMXref_HIS @MBMNumber = @number, @MBMCOVID = '00', @MBMPolicyNo = @policy, @MBMIcBcPp = @ic, @MBMName = @name,
        @MBMPayorEffDate = @eff, @MBMPayorEXPDate = @exp, @AgeCode = @ageCode, @INSCode = @ins, @PLNCode = @plan, @MBMBordxDate = @bordx,
        @MBMTopupNumber = @topupNo, @MBMTopupInd = @topupInd, @MBMDataStatus = @dataStatus, @MBMStatus = @status, @MBMDOB = @dob, @HLTCode = @hlt,
        @PAYCode = @pay, @MBMBatchNo = @batch, @UpdatedBy = @runKey;
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_StageOthers
    @runKey nvarchar(4000), @number nvarchar(4000), @agentCode nvarchar(4000), @labelStatus nvarchar(4000), @premium decimal(19, 4),
    @basicPremium decimal(19, 4), @basicMco decimal(19, 4), @mco decimal(19, 4), @rel nvarchar(4000), @policyDisc decimal(19, 4),
    @renewalDisc decimal(19, 4), @groupCompany nvarchar(4000), @prevTakeOverPlan nvarchar(4000), @joined datetime = NULL, @prevPolicy nvarchar(4000),
    @prevMember nvarchar(4000), @employeeNo nvarchar(4000), @department nvarchar(4000), @installment nvarchar(4000), @polSubNo1 nvarchar(4000),
    @polSubNo2 nvarchar(4000), @branch nvarchar(4000), @newPolicyEff datetime = NULL, @endtRefNo nvarchar(4000), @polCondition nvarchar(4000),
    @undExcess decimal(19, 4), @guarantee nvarchar(4000), @clmNonPayFrom datetime = NULL, @clmNonPayTo datetime = NULL, @invoiceNo nvarchar(4000),
    @gstAmount decimal(19, 4) = NULL, @gstPercent decimal(19, 4) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.TmpUpload_MBMOthers_2015 @MBMNumber = @number, @MBMAgentCode = @agentCode, @LabelStatus = @labelStatus, @MBMFirstMEMNo = @number,
        @MBMPremium = @premium, @MBMBasicPrem = @basicPremium, @MBMBasicMCO = @basicMco, @MBMMCOFee = @mco, @RELCODE = @rel,
        @MBMPolicyDisc = @policyDisc, @MBMRenewalDisc = @renewalDisc, @MBMGroupCompany = @groupCompany, @MBMPrevTakeOverPlnCode = @prevTakeOverPlan,
        @MBMFirstJoinedDate = @joined, @MBMPrevPolNo = @prevPolicy, @MBMPrevMemNo = @prevMember, @MBMEmployeeNo = @employeeNo,
        @MBMDepartment = @department, @MBMInstallment = @installment, @MBMPolSubNo1 = @polSubNo1, @MBMPOLSubNo2 = @polSubNo2, @MBMBranch = @branch,
        @MBMNewPlanCode = '', @MBMNewPolEffDate = @newPolicyEff, @MBMENDtRefNo = @endtRefNo, @MBMPolCondition = @polCondition,
        @MBMUndExcess = @undExcess, @MBMGuaRenewal = @guarantee, @MBMClmNonPayFr = @clmNonPayFrom, @MBMClmNonPayTo = @clmNonPayTo,
        @MBMInvoiceNo = @invoiceNo, @MBMPaymentDate = NULL, @UpdatedBy = @runKey, @MBMGSTAmt = @gstAmount, @MBMGSTPerc = @gstPercent,
        @CMCName = '', @AgentName = '';
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_StageOthersTwo
    @runKey nvarchar(4000), @number nvarchar(4000), @weight nvarchar(4000), @height nvarchar(4000), @workNature nvarchar(4000), @blood nvarchar(4000),
    @occupation nvarchar(4000), @smoking nvarchar(4000), @alcohol nvarchar(4000), @marital nvarchar(4000), @grpAdd1 nvarchar(4000),
    @grpAdd2 nvarchar(4000), @grpAdd3 nvarchar(4000), @grpAdd4 nvarchar(4000), @riskNo nvarchar(4000), @transNo nvarchar(4000), @payNo nvarchar(4000),
    @paySeqNo nvarchar(4000), @bankAccountNo nvarchar(4000), @clientNo nvarchar(4000), @clientId nvarchar(4000), @rbAmount decimal(19, 4),
    @prevInsurer nvarchar(4000), @prevPlan nvarchar(4000), @payRemarks nvarchar(4000), @coPayRb nvarchar(4000), @exclusion nvarchar(4000),
    @btbg nvarchar(4000), @payorPremNet decimal(19, 4), @payorMcoNet decimal(19, 4), @payorMcoGross decimal(19, 4), @loadingPremium decimal(19, 4),
    @familyDiscAmount decimal(19, 4), @renewalDiscAmount decimal(19, 4), @payorPremGross decimal(19, 4), @policyIssued datetime = NULL,
    @allergic nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.TmpUpload_MBMOthersTwoNew @MBMNumber = @number, @MBMWeight = @weight, @MBMHeight = @height, @MBMWorkNature = @workNature,
        @MBMBlood = @blood, @MBMOccupation = @occupation, @MBMSmoking = @smoking, @MBMAlcohol = @alcohol, @MBMMaritalStatus = @marital,
        @MBMGRPCompanyAdd1 = @grpAdd1, @MBMGRPCompanyAdd2 = @grpAdd2, @MBMGRPCompanyAdd3 = @grpAdd3, @MBMGRPCompanyAdd4 = @grpAdd4,
        @MBMRiskNo = @riskNo, @MBMTransNo = @transNo, @MBMPayNo = @payNo, @MBMPaySeqNo = @paySeqNo, @MBMBankACNo = @bankAccountNo,
        @MBMClientNo = @clientNo, @MBMClientID = @clientId, @MBMRBAmt = @rbAmount, @MBMPrevInsCom = @prevInsurer, @MBMPrevPlnCode = @prevPlan,
        @MBMPayRemarks = @payRemarks, @MBMCoPayRB = @coPayRb, @MBMExclusion = @exclusion, @MBMBTBG = @btbg, @MBMPayorPremNet = @payorPremNet,
        @MBMPayorMcoNet = @payorMcoNet, @MBMPayorMCoGross = @payorMcoGross, @MBMLoadingPrem = @loadingPremium, @MBMFamDiscAmt = @familyDiscAmount,
        @MBMRenewDiscAmt = @renewalDiscAmount, @MBMPayorPremGross = @payorPremGross, @MBMPOLIssuedDate = @policyIssued, @MBMAllergic = @allergic,
        @UpdatedBy = @runKey;
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_StageClinical
    @runKey nvarchar(4000), @memberType nvarchar(4000), @pay nvarchar(4000), @number nvarchar(4000), @policy nvarchar(4000), @plan nvarchar(4000),
    @ins nvarchar(4000), @warranty nvarchar(4000), @basicPremium decimal(19, 4), @premium decimal(19, 4), @basicMco decimal(19, 4), @mco decimal(19, 4),
    @balance decimal(19, 4), @visit decimal(19, 4), @dental decimal(19, 4), @specialist decimal(19, 4), @mep decimal(19, 4), @eff datetime = NULL,
    @exp datetime = NULL, @cardType nvarchar(4000), @invoiced nvarchar(4000), @invoiceNo nvarchar(4000), @invoiceDate datetime = NULL, @invoiceBy nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.TmpUpload_MBMClnOthers @MBMCLNMemberType = @memberType, @PAYCode = @pay, @MBMNumber = @number, @MBMPolNumber = @policy,
        @MBMCLNStatus = 'A', @MBMCLNDataStatus = 'A', @MBMCLNExpiryStatus = 'N', @PLNType = @plan, @InsType = @ins, @MBMCLNWarranty = @warranty,
        @MBMCLNBasicPrem = @basicPremium, @MBMCLNPremium = @premium, @MBMCLNBasicMCO = @basicMco, @MBMCLNMCOFee = @mco, @MBMCLNBalLmt = @balance,
        @MBMCLNVisitLmt = @visit, @MBMDentalLmt = @dental, @MBMSpecLmt = @specialist, @MBMMEPLmt = @mep, @MBMPolEffDate = @eff, @MBMPolExpDate = @exp,
        @MBMCardType = @cardType, @MBMCInvoiced = @invoiced, @MBMCInvoiceNo = @invoiceNo, @MBMCInvoiceDate = @invoiceDate, @MBMCInvoiceBy = @invoiceBy,
        @UpdatedBy = @runKey;
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_StageCovered
    @runKey nvarchar(4000), @rel nvarchar(4000), @number nvarchar(4000), @coverId nvarchar(4000), @dob datetime = NULL, @memberType nvarchar(4000),
    @name nvarchar(4000), @status nvarchar(4000), @bordx datetime, @batch int, @ic nvarchar(4000), @sex nvarchar(4000), @plan nvarchar(4000),
    @ageCode nvarchar(4000), @adultChild nvarchar(4000), @annual decimal(19, 4), @basicMco decimal(19, 4), @mco decimal(19, 4),
    @basicPremium decimal(19, 4), @premium decimal(19, 4), @today datetime, @eff datetime = NULL, @exp datetime = NULL
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.TmpUpload_MBMCovPersonsNew @MBMCRELCode = @rel, @MBMCNumber = @number, @MBMCCoverID = @coverId, @MBMCDOB = @dob,
        @MBMCMemberType = @memberType, @MBMCName = @name, @MBMCStatus = @status, @MBMCBordxDate = @bordx, @MBMCBatchNo = @batch,
        @MBMCICBCPPNo = @ic, @MBMCSex = @sex, @MBMCPLNCode = @plan, @MBMCAgeband = @ageCode, @MBMCAdultChild = @adultChild,
        @MBMCAnnualLimit = @annual, @MBMCAvailableLimit = @annual, @MBMCBasicMCO = @basicMco, @MBMCMCO = @mco, @MBMCBasicPremium = @basicPremium,
        @MBMCPremium = @premium, @MBMCRegDate = @today, @MBMCEffDate = @eff, @MBMCExpDate = @exp, @UpdatedBy = @runKey,
        @MBMTICNumber = '', @MBMTICCoverID = '';
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_StageCoveredTwo
    @runKey nvarchar(4000), @number nvarchar(4000), @coverId nvarchar(4000), @salutation nvarchar(4000), @occupation nvarchar(4000),
    @workNature nvarchar(4000), @blood nvarchar(4000), @allergic nvarchar(4000), @smoking nvarchar(4000), @alcohol nvarchar(4000),
    @prevPlan nvarchar(4000), @prevInsurer nvarchar(4000), @payRemarks nvarchar(4000), @exclusion nvarchar(4000), @payNo nvarchar(4000),
    @paySeqNo nvarchar(4000), @clientNo nvarchar(4000), @clientId nvarchar(4000), @rbAmount decimal(19, 4), @payorPremNet decimal(19, 4),
    @payorMcoNet decimal(19, 4), @payorMcoGross decimal(19, 4), @otherIc nvarchar(4000), @loadingPremium decimal(19, 4),
    @familyDiscAmount decimal(19, 4), @renewalDiscAmount decimal(19, 4), @payorPremGross decimal(19, 4), @undExcess decimal(19, 4),
    @clmNonPayFrom datetime = NULL, @clmNonPayTo datetime = NULL, @policyDisc decimal(19, 4), @renewalDisc decimal(19, 4)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.TmpUpload_MBMCovPersonsTwo @MBMCNumber = @number, @MBMCCoverID = @coverId, @MBMCSalutation = @salutation, @MBMCOccupation = @occupation,
        @MBMCWorkNature = @workNature, @MBMCBloodType = @blood, @MBMCAllergic = @allergic, @MBMCSmoking = @smoking, @MBMCAlcohol = @alcohol,
        @MBMCPrevPlnCode = @prevPlan, @MBMCPrevInsCom = @prevInsurer, @MBMCPayRemarks = @payRemarks, @MBMCExclusion = @exclusion,
        @MBMCPayNo = @payNo, @MBMCPaySeqNo = @paySeqNo, @MBMCClientNo = @clientNo, @MBMCClientID = @clientId, @MBMCRBAmt = @rbAmount,
        @MBMCPayorPremNet = @payorPremNet, @MBMCPayorMcoNet = @payorMcoNet, @MBMCPayorMCoGross = @payorMcoGross, @MBMCIcBcPp2nd = @otherIc,
        @MBMCLoadingPrem = @loadingPremium, @MBMCFamDiscAmt = @familyDiscAmount, @MBMCRenewDiscAmt = @renewalDiscAmount,
        @MBMCPayorPremGross = @payorPremGross, @MBMCUndExcess = @undExcess, @MBMCClmNonPayFr = @clmNonPayFrom, @MBMCClmNonPayTo = @clmNonPayTo,
        @MBMCPolicyDisc = @policyDisc, @MBMCRenewalDisc = @renewalDisc, @UpdatedBy = @runKey;
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_StageCoveredClinical
    @runKey nvarchar(4000), @number nvarchar(4000), @coverId nvarchar(4000), @mco decimal(19, 4), @basicMco decimal(19, 4), @premium decimal(19, 4),
    @basicPremium decimal(19, 4), @balance decimal(19, 4), @visit decimal(19, 4), @dental decimal(19, 4), @specialist decimal(19, 4),
    @mep decimal(19, 4), @today datetime, @user nvarchar(4000), @exp datetime = NULL, @cardType nvarchar(4000), @warranty nvarchar(4000),
    @plan nvarchar(4000), @invoiceNo nvarchar(4000), @invoiceDate datetime = NULL, @invoiceBy nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.TmpUpload_MBMCovPersonsCis @MBMCNumber = @number, @MBMCCoverID = @coverId, @MBMCCLNMCO = @mco, @MBMCCLNBasicMCO = @basicMco,
        @MBMCCLNPrem = @premium, @MBMCCLNBasicPrem = @basicPremium, @MBMCCLNBalLmt = @balance, @MBMCCLNVisitLmt = @visit,
        @MBMCCLNDentalLmt = @dental, @MBMCCLNSpecLmt = @specialist, @MBMCCLNMEPLmt = @mep, @MBMCCLNRegDate = @today, @MBMCCLNRegUser = @user,
        @MBMCCLNLastUpdateDate = @today, @MBMCCLNLastUpdateUser = @user, @MBMCCLNExpDate = @exp, @MBMCCardType = @cardType,
        @MBMCCLNWarranty = @warranty, @MBMCCLNStatus = 'A', @MBMCCLNPLNCode = @plan, @MBMCInvoiceNo = @invoiceNo, @MBMCInvoiceDate = @invoiceDate,
        @MBMCInvoiceBy = @invoiceBy, @UpdatedBy = @runKey;
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_StageRefund
    @runKey nvarchar(4000), @number nvarchar(4000), @coverId nvarchar(4000), @premiumEffective decimal(19, 4), @premiumReduction decimal(19, 4),
    @mcoEffective decimal(19, 4), @mcoRefund decimal(19, 4), @pay nvarchar(4000), @bordx datetime, @batch int, @hlt nvarchar(4000),
    @fileName nvarchar(4000), @endtRefNo nvarchar(4000), @cancelDate datetime, @gstAmount decimal(19, 4) = NULL, @gstPercent decimal(19, 4) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.TmpUpload_MBMMcoRefund_2015 @MBMNumber = @number, @MBMCovID = @coverId, @MBMPREMEffective = @premiumEffective,
        @MBMPremReduction = @premiumReduction, @MBMMCOEffective = @mcoEffective, @MBMMCORefund = @mcoRefund, @PAYCode = @pay,
        @MBMBordxDate = @bordx, @MBMBatchNO = @batch, @HLTCode = @hlt, @MBMFileName = @fileName, @MBMENDtRefNo = @endtRefNo,
        @MBMCancerdate = @cancelDate, @UpdatedBy = @runKey, @MBMGSTAmt = @gstAmount, @MBMGSTPerc = @gstPercent;
END
GO

-- ===== Cancellation and reinstatement (written straight to HISDB, as the desktop does) =====

-- Membership to cancel: policy number + payor expiry date, optionally limited to the payor plan (desktop EndtCancelPrincipalData).
CREATE OR ALTER PROCEDURE genisis.Bordx_FindForCancel
    @policy nvarchar(4000), @exp datetime, @payorPlan nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(m.MBMNumber) AS MembershipNo, RTRIM(m.MBMStatus) AS Status, RTRIM(m.MBMPolicyNo) AS PolicyNo, RTRIM(m.MBMIcBcPp) AS Ic,
        RTRIM(m.MBMName) AS Name, m.MBMPayorExpDate AS ExpiryDate, RTRIM(m.PLNCode) AS PlanCode, m.MBMPayorEffDate AS EffectiveDate,
        o.MBMPremium AS Premium, o.MBMMCOFee AS Mco
    FROM dbo.MBM m
    LEFT JOIN dbo.MBMOthers o ON o.MBMNumber = m.MBMNumber
    WHERE m.MBMPolicyNo = @policy AND m.MBMPayorExpDate = @exp AND m.MBMStatus <> 'D'
      AND (@payorPlan IS NULL OR EXISTS (SELECT 1 FROM dbo.PLN p WHERE p.PLNCode = m.PLNCode AND p.PLNPayorPlan = @payorPlan));
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_Cancel
    @number nvarchar(4000), @today datetime, @user nvarchar(4000), @bordx datetime, @cancelDate datetime, @fileName nvarchar(4000),
    @policy nvarchar(4000), @ic nvarchar(4000), @name nvarchar(4000), @exp datetime, @plan nvarchar(4000), @batch int
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.MBMTwo SET MBMLastAdjustmentDate = @today, MBMLastUpdateUser = @user, MBMLastEndorseStatus = 'C', MBMENDtBordxDate = @bordx,
        MBMCancelDate = @cancelDate, MBMENDtEffDate = @cancelDate, MBMFilename = @fileName
    WHERE MBMNumber = @number;
    UPDATE genisis.Maint_MBMCrossReference SET MBMStatus = 'C' WHERE MBMNumber = @number;
    INSERT INTO dbo.MBMAmendHistory (MBMNumber, MBMPolicyNo, MBMIcBcPp, MBMName, MBMExpiryDate, MBMPlanCode, MBMAmendEdtType, MBMAEndtBordxDate,
        MBMAEndtEffDate, MBMAmendUser, MBMAmendDate, MBMAEndtBatchNo)
    VALUES (@number, @policy, @ic, @name, @exp, @plan, 'C', @bordx, @cancelDate, @user, @today, @batch);
    UPDATE dbo.MBM SET MBMStatus = 'C' WHERE MBMNumber = @number;
END
GO

-- Lapsed membership being reinstated: same policy and payor expiry date (payor ZN excluded, as in the desktop).
CREATE OR ALTER PROCEDURE genisis.Bordx_FindForReinstate
    @policy nvarchar(4000), @exp datetime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(MBMNumber) AS MembershipNo FROM dbo.MBM WHERE PAYCode <> 'ZN' AND MBMPolicyNo = @policy AND MBMPayorExpDate = @exp;
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_Reinstate
    @number nvarchar(4000), @bordx datetime, @mco decimal(19, 4), @premium decimal(19, 4), @reinstateDate datetime, @user nvarchar(4000),
    @today datetime, @batch int, @fileName nvarchar(4000), @endtRefNo nvarchar(4000), @gstAmount decimal(19, 4) = NULL, @gstPercent decimal(19, 4) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.MBMReinstatement (MBMNumber, MBMCoverID, MBMEndtBordxDate, MBMMCOFee, MBMPremium, MBMLapsedDate, MBMReinstateDate,
        LastUpdateUSR, LastUpdateDate, MBMEndtBordxBatch, MBMFileName, MBMENDtRefNo, MBMGSTAmt, MBMGSTPerc)
    VALUES (@number, '00', @bordx, @mco, @premium, NULL, @reinstateDate, @user, @today, @batch, @fileName, @endtRefNo, @gstAmount, @gstPercent);
    UPDATE dbo.MBM SET MBMStatus = 'A' WHERE MBMNumber = @number;
END
GO

-- Reinstatement fees (desktop MemberCallFees): plan settings on the reinstatement date.
CREATE OR ALTER PROCEDURE genisis.Bordx_ReinstatePlan
    @plan nvarchar(4000), @hlt nvarchar(4000), @pay nvarchar(4000), @eff datetime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(PLNRoundUpStatus) AS RoundUpStatus, RTRIM(PLNProRate) AS ProRate, RTRIM(PLNPremGenderStatus) AS PremGenderStatus
    FROM dbo.PLN WHERE PLNCode = @plan AND HLTCode = @hlt AND PLNEffDate <= @eff AND (@hlt <> 'N' OR PAYCode = @pay)
    ORDER BY PLNEffDate DESC;
END
GO

-- ===== Generate ASCII =====

-- Members for the ASCII file: one bordereaux date + batch, or a bordereaux date range optionally limited to a plan-code range.
CREATE OR ALTER PROCEDURE genisis.Bordx_AsciiMembers
    @hlt nvarchar(4000), @pay nvarchar(4000), @from datetime, @to datetime, @batch int = NULL, @planFrom nvarchar(4000) = NULL, @planTo nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(m.MBMNumber) AS MembershipNo, RTRIM(m.MBMMemberType) AS MemberType, RTRIM(m.INSCode) AS InsuredType, RTRIM(m.MBMPolicyNo) AS PolicyNo,
        m.MBMPayorEffDate AS EffectiveDate, RTRIM(m.MBMIcBcPp) AS Ic, RTRIM(m.MBMName) AS Name, RTRIM(m.PLNCode) AS PlanCode,
        RTRIM(o.MBMEmployeeNo) AS EmployeeNo, RTRIM(o.MBMGroupCompany) AS GroupCompany, RTRIM(o.MBMPOLSubNo1) AS PolicySubNo,
        w.MBMPOLIssuedDate AS PolicyIssued, RTRIM(w.MBMTransNo) AS TransNo, RTRIM(w.MBMPayNo) AS PayNo, RTRIM(w.MBMPaySeqNo) AS PaySeqNo,
        RTRIM(w.MBMClientNo) AS ClientNo, RTRIM(w.MBMClientID) AS ClientId
    FROM dbo.MBM m
    LEFT JOIN dbo.MBMOthers o ON o.MBMNumber = m.MBMNumber
    LEFT JOIN dbo.MBMOthersTwo w ON w.MBMNumber = m.MBMNumber
    WHERE m.HLTCode = @hlt AND m.PAYCode = @pay AND m.MBMStatus <> 'D' AND m.MBMDataStatus <> 'D'
      AND m.MBMBordxDate >= @from AND m.MBMBordxDate <= @to AND (@batch IS NULL OR m.MBMBatchNo = @batch)
      AND (@planFrom IS NULL OR (m.PLNCode >= @planFrom AND m.PLNCode <= @planTo))
    ORDER BY m.MBMNumber;
END
GO

CREATE OR ALTER PROCEDURE genisis.Bordx_AsciiCovered
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(c.MBMCRELCode) AS Relationship, RTRIM(c.MBMCName) AS Name, RTRIM(c.MBMCICBCPPNo) AS Ic, RTRIM(c.MBMCNumber) AS MembershipNo,
        RTRIM(c.MBMCCoverID) AS CoverId, RTRIM(c2.MBMCPAYNo) AS PayNo, RTRIM(c2.MBMCPAYSeqNo) AS PaySeqNo, RTRIM(c2.MBMCClientNO) AS ClientNo,
        RTRIM(c2.MBMCClientID) AS ClientId
    FROM dbo.MBMCoveredPersons c
    LEFT JOIN dbo.MBMCoveredPersonsTwo c2 ON c2.MBMCNumber = c.MBMCNumber AND c2.MBMCCoverID = c.MBMCCoverID
    WHERE c.MBMCNumber = @number
    ORDER BY c.MBMCCoverID;
END
GO

-- ===== main/170_tracking.sql =====
-- Membership > Tracking (legacy FrmNewMBMTracking). The desktop passed a concatenated WHERE fragment to dbo.MBMTracking; here every
-- filter is a typed parameter. genisis.Tracking_CaseText (171_tracking_cases.sql, generated) holds each case's fixed query text.

-- Result sets: payors (with health type), states (MBM.STACode holds the state description, as the desktop's list does), product categories.
CREATE OR ALTER PROCEDURE genisis.Tracking_Lookups
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name, RTRIM(HLTCode) AS HealthCode FROM dbo.PAY WHERE PAYCode IS NOT NULL ORDER BY PAYCode;
    SELECT DISTINCT UPPER(RTRIM(STADescription)) AS Code, UPPER(RTRIM(STADescription)) AS Name FROM dbo.STA WHERE STADescription IS NOT NULL ORDER BY Name;
    SELECT RTRIM(ProductCode) AS Code, RTRIM(ProductName) AS Name FROM genisis.Maint_ProductCategory WHERE ProductCode IS NOT NULL ORDER BY ProductCode;
END
GO

-- Runs one MBMTracking case with the Tracking screen's filters (FrmNewMBMTracking.SearchRecord).
--   @insuredGroup  0..7 (desktop insured type list), @status 0..6 (desktop status list), @renewal N/P/R/T
--   @blanks        comma-separated filter keys whose "blank" box is ticked (keys outside the fixed list are ignored)
--   @memberTypeFilter  DETAIL_C / DETAIL_S (detail listings) or NAME_<P|S|B>_<C|S|BOTH> (General Listing (Prin. Name))
--   @extra         insured / adult-child restriction of the per-type count columns: I, F, G, H, I_C, I_A, G_C, G_A
--   @withSuppRegDate  1 for the supplementary detail listings (Supp. registration date filter)
CREATE OR ALTER PROCEDURE genisis.Tracking_Run
    @case int,
    @health nvarchar(10) = NULL, @payor nvarchar(10) = NULL, @insuredGroup nvarchar(1) = NULL, @status nvarchar(1) = NULL,
    @renewal nvarchar(1) = NULL, @adultChild nvarchar(1) = NULL, @race nvarchar(1) = NULL, @sex nvarchar(1) = NULL,
    @relationship nvarchar(2) = NULL, @dataStatus nvarchar(1) = NULL, @expired nvarchar(1) = NULL, @premiumMode nvarchar(1) = NULL,
    @state nvarchar(50) = NULL, @takeOver nvarchar(1) = NULL, @agentCode nvarchar(50) = NULL, @membershipNo nvarchar(30) = NULL,
    @groupCompany nvarchar(200) = NULL, @labelStatus nvarchar(1) = NULL, @department nvarchar(100) = NULL, @productCategory nvarchar(10) = NULL,
    @dayInsuredFrom decimal(19, 4) = NULL, @dayInsuredTo decimal(19, 4) = NULL, @ageDaysFrom decimal(19, 4) = NULL, @ageDaysTo decimal(19, 4) = NULL,
    @delayBordxFrom decimal(19, 4) = NULL, @delayBordxTo decimal(19, 4) = NULL, @delayInvBordxFrom decimal(19, 4) = NULL,
    @delayInvBordxTo decimal(19, 4) = NULL, @delayRegFrom decimal(19, 4) = NULL, @delayRegTo decimal(19, 4) = NULL,
    @delayInvDFrom decimal(19, 4) = NULL, @delayInvDTo decimal(19, 4) = NULL, @delayInvEFrom decimal(19, 4) = NULL, @delayInvETo decimal(19, 4) = NULL,
    @premiumFrom decimal(19, 4) = NULL, @premiumTo decimal(19, 4) = NULL, @mcoFeeFrom decimal(19, 4) = NULL, @mcoFeeTo decimal(19, 4) = NULL,
    @batchFrom decimal(19, 4) = NULL, @batchTo decimal(19, 4) = NULL,
    @employeeNoFrom nvarchar(50) = NULL, @employeeNoTo nvarchar(50) = NULL, @policyNoFrom nvarchar(50) = NULL, @policyNoTo nvarchar(50) = NULL,
    @planFrom nvarchar(10) = NULL, @planTo nvarchar(10) = NULL, @ageBandFrom nvarchar(10) = NULL, @ageBandTo nvarchar(10) = NULL,
    @postCodeFrom nvarchar(10) = NULL, @postCodeTo nvarchar(10) = NULL, @policyYearFrom nvarchar(10) = NULL, @policyYearTo nvarchar(10) = NULL,
    @invoiceNoFrom nvarchar(50) = NULL, @invoiceNoTo nvarchar(50) = NULL, @icFrom nvarchar(30) = NULL, @icTo nvarchar(30) = NULL,
    @effDateFrom date = NULL, @effDateTo date = NULL, @expDateFrom date = NULL, @expDateTo date = NULL,
    @dateJoinedFrom date = NULL, @dateJoinedTo date = NULL, @dorFrom date = NULL, @dorTo date = NULL, @dobFrom date = NULL, @dobTo date = NULL,
    @bordxDateFrom date = NULL, @bordxDateTo date = NULL, @invoiceDateFrom date = NULL, @invoiceDateTo date = NULL,
    @endtEffDateFrom date = NULL, @endtEffDateTo date = NULL, @cancelDateFrom date = NULL, @cancelDateTo date = NULL,
    @dorSuppFrom date = NULL, @dorSuppTo date = NULL,
    @blanks nvarchar(2000) = NULL, @memberTypeFilter nvarchar(20) = NULL, @extra nvarchar(5) = NULL, @withSuppRegDate bit = 0
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @prefix nvarchar(max), @suffix nvarchar(max), @w nvarchar(max) = N'';
    EXEC genisis.Tracking_CaseText @case = @case, @prefix = @prefix OUTPUT, @suffix = @suffix OUTPUT;
    IF @prefix IS NULL THROW 50001, N'Unknown tracking report case.', 1;

    -- Equality filters.
    SET @w += CASE WHEN @health IS NOT NULL THEN N' AND HLTCode = @health' ELSE N'' END
        + CASE WHEN @payor IS NOT NULL THEN N' AND PAYCode = @payor' ELSE N'' END
        + CASE WHEN @adultChild IS NOT NULL THEN N' AND MBMAdultChild = @adultChild' ELSE N'' END
        + CASE WHEN @race IS NOT NULL THEN N' AND RACCode = @race' ELSE N'' END
        + CASE WHEN @sex IS NOT NULL THEN N' AND MBMSex = @sex' ELSE N'' END
        + CASE WHEN @relationship IS NOT NULL THEN N' AND RELCode = @relationship' ELSE N'' END
        + CASE WHEN @dataStatus IS NOT NULL THEN N' AND MBMDataStatus = @dataStatus' ELSE N'' END
        + CASE WHEN @expired IS NOT NULL THEN N' AND MBMExpiredStatus = @expired' ELSE N'' END
        + CASE WHEN @premiumMode IS NOT NULL THEN N' AND MBMInstallment = @premiumMode' ELSE N'' END
        + CASE WHEN @state IS NOT NULL THEN N' AND STACode = @state' ELSE N'' END
        + CASE WHEN @takeOver IS NOT NULL THEN N' AND MBMTakeover = @takeOver' ELSE N'' END
        + CASE WHEN @agentCode IS NOT NULL THEN N' AND MBMAgentCode = @agentCode' ELSE N'' END
        + CASE WHEN @membershipNo IS NOT NULL THEN N' AND MBMNumber = @membershipNo' ELSE N'' END
        + CASE WHEN @groupCompany IS NOT NULL THEN N' AND MBMGroupCompany = @groupCompany' ELSE N'' END
        + CASE WHEN @labelStatus IS NOT NULL THEN N' AND LabelStatus = @labelStatus' ELSE N'' END
        + CASE WHEN @department IS NOT NULL THEN N' AND MBMDepartment = @department' ELSE N'' END
        + CASE WHEN @productCategory IS NOT NULL THEN N' AND PLNProCat = @productCategory' ELSE N'' END;

    -- Coded selections (desktop combo lists).
    SET @w += CASE @insuredGroup
            WHEN N'0' THEN N' AND INSCode = ''I''' WHEN N'1' THEN N' AND INSCode = ''F''' WHEN N'2' THEN N' AND INSCode = ''G'''
            WHEN N'3' THEN N' AND INSCode = ''H''' WHEN N'4' THEN N' AND (INSCode = ''I'' OR INSCode = ''F'')'
            WHEN N'5' THEN N' AND (INSCode = ''I'' OR INSCode = ''G'')' WHEN N'6' THEN N' AND (INSCode = ''F'' OR INSCode = ''H'')'
            WHEN N'7' THEN N' AND (INSCode = ''G'' OR INSCode = ''H'')' ELSE N'' END
        + CASE @status
            WHEN N'0' THEN N' AND MBMStatus = ''A''' WHEN N'1' THEN N' AND MBMStatus = ''C''' WHEN N'2' THEN N' AND MBMStatus = ''D'''
            WHEN N'3' THEN N' AND (MBMStatus = ''A'' OR MBMStatus = ''C'')' WHEN N'4' THEN N' AND (MBMStatus = ''A'' OR MBMStatus = ''S'')'
            WHEN N'5' THEN N' AND (MBMStatus = ''A'' OR MBMStatus = ''C'' OR MBMStatus = ''S'')' WHEN N'6' THEN N' AND MBMStatus = ''S''' ELSE N'' END
        + CASE @renewal
            WHEN N'P' THEN N' AND (MBMRenewFlag = ''P'')' WHEN N'N' THEN N' AND (MBMRenewFlag IS NULL OR MBMRenewFlag = '''' OR MBMRenewFlag = ''N'')'
            WHEN N'R' THEN N' AND (MBMRenewFlag = ''R'')' WHEN N'T' THEN N' AND (MBMRenewFlag = ''T'')' ELSE N'' END;

    -- From / to ranges.
    SELECT @w += CASE WHEN r.FromSet = 1 THEN N' AND ' + r.Col + N' >= @' + r.Param + N'From' ELSE N'' END
               + CASE WHEN r.ToSet = 1 THEN N' AND ' + r.Col + N' <= @' + r.Param + N'To' ELSE N'' END
    FROM (VALUES
        (N'DayInsured', N'dayInsured', IIF(@dayInsuredFrom IS NULL, 0, 1), IIF(@dayInsuredTo IS NULL, 0, 1)),
        (N'AgeDays', N'ageDays', IIF(@ageDaysFrom IS NULL, 0, 1), IIF(@ageDaysTo IS NULL, 0, 1)),
        (N'DelayBordx', N'delayBordx', IIF(@delayBordxFrom IS NULL, 0, 1), IIF(@delayBordxTo IS NULL, 0, 1)),
        (N'DelayInvBordx', N'delayInvBordx', IIF(@delayInvBordxFrom IS NULL, 0, 1), IIF(@delayInvBordxTo IS NULL, 0, 1)),
        (N'DelayReg', N'delayReg', IIF(@delayRegFrom IS NULL, 0, 1), IIF(@delayRegTo IS NULL, 0, 1)),
        (N'DelayInvD', N'delayInvD', IIF(@delayInvDFrom IS NULL, 0, 1), IIF(@delayInvDTo IS NULL, 0, 1)),
        (N'DelayInvE', N'delayInvE', IIF(@delayInvEFrom IS NULL, 0, 1), IIF(@delayInvETo IS NULL, 0, 1)),
        (N'MBMPremium', N'premium', IIF(@premiumFrom IS NULL, 0, 1), IIF(@premiumTo IS NULL, 0, 1)),
        (N'MBMMCOFee', N'mcoFee', IIF(@mcoFeeFrom IS NULL, 0, 1), IIF(@mcoFeeTo IS NULL, 0, 1)),
        (N'MBMBatchNo', N'batch', IIF(@batchFrom IS NULL, 0, 1), IIF(@batchTo IS NULL, 0, 1)),
        (N'MBMEmployeeNo', N'employeeNo', IIF(@employeeNoFrom IS NULL, 0, 1), IIF(@employeeNoTo IS NULL, 0, 1)),
        (N'MBMPolicyNo', N'policyNo', IIF(@policyNoFrom IS NULL, 0, 1), IIF(@policyNoTo IS NULL, 0, 1)),
        (N'PLNCode', N'plan', IIF(@planFrom IS NULL, 0, 1), IIF(@planTo IS NULL, 0, 1)),
        (N'AGECode', N'ageBand', IIF(@ageBandFrom IS NULL, 0, 1), IIF(@ageBandTo IS NULL, 0, 1)),
        (N'MBMPostCode', N'postCode', IIF(@postCodeFrom IS NULL, 0, 1), IIF(@postCodeTo IS NULL, 0, 1)),
        (N'MBMPolicyYear', N'policyYear', IIF(@policyYearFrom IS NULL, 0, 1), IIF(@policyYearTo IS NULL, 0, 1)),
        (N'MBMInvoiceNo', N'invoiceNo', IIF(@invoiceNoFrom IS NULL, 0, 1), IIF(@invoiceNoTo IS NULL, 0, 1)),
        (N'MBMIcBcPp', N'ic', IIF(@icFrom IS NULL, 0, 1), IIF(@icTo IS NULL, 0, 1)),
        (N'MBMPayorEffDate', N'effDate', IIF(@effDateFrom IS NULL, 0, 1), IIF(@effDateTo IS NULL, 0, 1)),
        (N'MBMPayorExpDate', N'expDate', IIF(@expDateFrom IS NULL, 0, 1), IIF(@expDateTo IS NULL, 0, 1)),
        (N'MBMFirstJoinedDate', N'dateJoined', IIF(@dateJoinedFrom IS NULL, 0, 1), IIF(@dateJoinedTo IS NULL, 0, 1)),
        (N'MBMValueDate', N'dor', IIF(@dorFrom IS NULL, 0, 1), IIF(@dorTo IS NULL, 0, 1)),
        (N'MBMDOB', N'dob', IIF(@dobFrom IS NULL, 0, 1), IIF(@dobTo IS NULL, 0, 1)),
        (N'MBMBordxDate', N'bordxDate', IIF(@bordxDateFrom IS NULL, 0, 1), IIF(@bordxDateTo IS NULL, 0, 1)),
        (N'MBMInvoiceDate', N'invoiceDate', IIF(@invoiceDateFrom IS NULL, 0, 1), IIF(@invoiceDateTo IS NULL, 0, 1)),
        (N'MBMEndtEffDate', N'endtEffDate', IIF(@endtEffDateFrom IS NULL, 0, 1), IIF(@endtEffDateTo IS NULL, 0, 1)),
        (N'MBMCancelDate', N'cancelDate', IIF(@cancelDateFrom IS NULL, 0, 1), IIF(@cancelDateTo IS NULL, 0, 1)),
        (N'MBMCRegDate', N'dorSupp', IIF(@withSuppRegDate = 1 AND @dorSuppFrom IS NOT NULL, 1, 0), IIF(@withSuppRegDate = 1 AND @dorSuppTo IS NOT NULL, 1, 0))
    ) r (Col, Param, FromSet, ToSet);

    -- "Blank" check boxes. (HISDB runs at a legacy compatibility level, so STRING_SPLIT is not available; keys are matched with
    -- CHARINDEX against a fixed list and never concatenated into the SQL.)
    DECLARE @blankList nvarchar(2002) = N',' + REPLACE(ISNULL(@blanks, N''), N' ', N'') + N',';
    DECLARE @blankClauses TABLE (BlankKey nvarchar(50) PRIMARY KEY, Clause nvarchar(200));
    INSERT INTO @blankClauses VALUES
        (N'health', N'(HLTCode IS NULL OR HLTCode = '''')'), (N'payor', N'(PAYCode IS NULL OR PAYCode = '''')'),
        (N'insuredGroup', N'(INSCode IS NULL OR INSCode = '''')'), (N'memberTypeBlank', N'(MBMMemberType IS NULL OR MBMMemberType = '''')'),
        (N'adultChild', N'(MBMAdultChild IS NULL OR MBMAdultChild = '''')'), (N'race', N'(RACCode IS NULL OR RACCode = '''')'),
        (N'sex', N'(MBMSex IS NULL OR MBMSex = '''')'), (N'relationship', N'(RELCode IS NULL OR RELCode = '''')'),
        (N'dataStatus', N'(MBMDataStatus IS NULL OR MBMDataStatus = '''')'), (N'status', N'(MBMStatus IS NULL OR MBMStatus = '''')'),
        (N'expired', N'(MBMExpiredStatus IS NULL OR MBMExpiredStatus = '''')'), (N'renewal', N'(MBMRenewFlag IS NULL OR MBMRenewFlag = '''')'),
        (N'premiumMode', N'(MBMInstallment IS NULL OR MBMInstallment = '''')'), (N'state', N'(STACode IS NULL OR STACode = '''')'),
        (N'takeOver', N'(MBMTakeover IS NULL OR MBMTakeover = '''')'), (N'agentCode', N'(MBMAgentCode IS NULL OR MBMAgentCode = '''')'),
        (N'membershipNo', N'(MBMNumber IS NULL OR MBMNumber = '''')'), (N'policyNo', N'(MBMPolicyNo IS NULL OR MBMPolicyNo = '''')'),
        (N'groupCompany', N'(MBMGroupCompany IS NULL OR MBMGroupCompany = '''')'), (N'plan', N'(PLNCode IS NULL OR PLNCode = '''')'),
        (N'ageBand', N'(AGECode IS NULL OR AGECode = '''')'), (N'employeeNo', N'(MBMEmployeeNo IS NULL OR MBMEmployeeNo = '''')'),
        (N'dayInsured', N'(DayInsured IS NULL)'), (N'ageDays', N'(AgeDays IS NULL)'), (N'delayBordx', N'(DelayBordx IS NULL)'),
        (N'delayInvBordx', N'(DelayInvBordx IS NULL)'), (N'delayReg', N'(DelayReg IS NULL)'), (N'delayInvD', N'(DelayInvD IS NULL)'),
        (N'delayInvE', N'(DelayInvE IS NULL)'), (N'premium', N'(MBMPremium IS NULL)'), (N'mcoFee', N'(MBMMCOFee IS NULL)'),
        (N'effDate', N'(MBMPayorEffDate IS NULL OR MBMPayorEffDate = '''')'), (N'expDate', N'(MBMPayorExpDate IS NULL OR MBMPayorExpDate = '''')'),
        (N'dateJoined', N'(MBMFirstJoinedDate IS NULL OR MBMFirstJoinedDate = '''')'), (N'dor', N'(MBMValueDate IS NULL OR MBMValueDate = '''')'),
        (N'dob', N'(MBMDOB IS NULL OR MBMDOB = '''')'), (N'bordxDate', N'(MBMBordxDate IS NULL OR MBMBordxDate = '''')'),
        (N'batch', N'(MBMBatchNo IS NULL OR MBMBatchNo = '''')'), (N'postCode', N'(MBMPostCode IS NULL OR MBMPostCode = '''')'),
        (N'policyYear', N'(MBMPolicyYear IS NULL OR MBMPolicyYear = '''')'), (N'invoiceNo', N'(MBMInvoiceNo IS NULL OR MBMInvoiceNo = '''')'),
        (N'invoiceDate', N'(MBMInvoiceDate IS NULL OR MBMInvoiceDate = '''')'), (N'ic', N'(MBMIcBcPp IS NULL OR MBMIcBcPp = '''')'),
        (N'labelStatus', N'(LabelStatus IS NULL OR LabelStatus = '''')'), (N'department', N'(MBMDepartment IS NULL OR MBMDepartment = '''')'),
        (N'productCategory', N'(PLNProCat IS NULL OR PLNProCat = '''')'), (N'endtEffDate', N'(MBMEndtEffDate IS NULL OR MBMEndtEffDate = '''')'),
        (N'cancelDate', N'(MBMCancelDate IS NULL OR MBMCancelDate = '''')'), (N'dorSupp', N'(MBMCRegDate IS NULL OR MBMCRegDate = '''')');
    SELECT @w += N' AND ' + c.Clause FROM @blankClauses c
    WHERE CHARINDEX(N',' + c.BlankKey + N',', @blankList) > 0 AND (c.BlankKey <> N'dorSupp' OR @withSuppRegDate = 1);

    -- Member type selection of the detail listings and of General Listing (Prin. Name).
    IF @memberTypeFilter IS NOT NULL
    BEGIN
        DECLARE @memberClause nvarchar(200) = CASE @memberTypeFilter
            WHEN N'DETAIL_C' THEN N'(MBMMemberType = ''P'' OR MBMMemberType = ''C'')'
            WHEN N'DETAIL_S' THEN N'(MBMMemberType = ''Q'')'
            WHEN N'NAME_P_C' THEN N'MBMMemberType = ''P'' AND MBMCCoverID = ''00'''
            WHEN N'NAME_P_S' THEN N'MBMMemberType = ''Q'' AND MBMCCoverID = ''00'''
            WHEN N'NAME_P_BOTH' THEN N'(MBMMemberType = ''P'' OR MBMMemberType = ''Q'') AND MBMCCoverID = ''00'''
            WHEN N'NAME_S_C' THEN N'MBMMemberType = ''C'''
            WHEN N'NAME_S_S' THEN N'MBMMemberType = ''S'''
            WHEN N'NAME_S_BOTH' THEN N'(MBMMemberType = ''S'' OR MBMMemberType = ''C'')'
            WHEN N'NAME_B_C' THEN N'(MBMMemberType = ''P'' OR MBMMemberType = ''C'')'
            WHEN N'NAME_B_S' THEN N'(MBMMemberType = ''Q'' OR MBMMemberType = ''S'')'
            WHEN N'NAME_B_BOTH' THEN N'(MBMMemberType = ''P'' OR MBMMemberType = ''Q'')' END;
        IF @memberClause IS NULL THROW 50003, N'Unknown tracking member type filter.', 1;
        SET @w += N' AND ' + @memberClause;
    END

    -- Per insured / adult-child count columns (Bordx and MCO invoice "INS Type & MBM Type", Branch by Agent).
    IF @extra IS NOT NULL
    BEGIN
        DECLARE @extraClause nvarchar(200) = CASE @extra
            WHEN N'I' THEN N'INSCode = ''I''' WHEN N'F' THEN N'INSCode = ''F''' WHEN N'G' THEN N'INSCode = ''G''' WHEN N'H' THEN N'INSCode = ''H'''
            WHEN N'I_C' THEN N'INSCode = ''I'' AND MBMAdultChild = ''C''' WHEN N'I_A' THEN N'INSCode = ''I'' AND MBMAdultChild = ''A'''
            WHEN N'G_C' THEN N'INSCode = ''G'' AND MBMAdultChild = ''C''' WHEN N'G_A' THEN N'INSCode = ''G'' AND MBMAdultChild = ''A''' END;
        IF @extraClause IS NULL THROW 50004, N'Unknown tracking count column.', 1;
        SET @w += N' AND ' + @extraClause;
    END

    -- Payors starting with Z are internal test payors and never reported.
    SET @w += N' AND SUBSTRING(PAYCode, 1, 1) <> ''Z''';

    DECLARE @sql nvarchar(max) = @prefix + @w + N' ' + @suffix;
    EXEC sp_executesql @sql,
        N'@health nvarchar(10), @payor nvarchar(10), @adultChild nvarchar(1), @race nvarchar(1), @sex nvarchar(1), @relationship nvarchar(2),
          @dataStatus nvarchar(1), @expired nvarchar(1), @premiumMode nvarchar(1), @state nvarchar(50), @takeOver nvarchar(1), @agentCode nvarchar(50),
          @membershipNo nvarchar(30), @groupCompany nvarchar(200), @labelStatus nvarchar(1), @department nvarchar(100), @productCategory nvarchar(10),
          @dayInsuredFrom decimal(19, 4), @dayInsuredTo decimal(19, 4), @ageDaysFrom decimal(19, 4), @ageDaysTo decimal(19, 4),
          @delayBordxFrom decimal(19, 4), @delayBordxTo decimal(19, 4), @delayInvBordxFrom decimal(19, 4), @delayInvBordxTo decimal(19, 4),
          @delayRegFrom decimal(19, 4), @delayRegTo decimal(19, 4), @delayInvDFrom decimal(19, 4), @delayInvDTo decimal(19, 4),
          @delayInvEFrom decimal(19, 4), @delayInvETo decimal(19, 4), @premiumFrom decimal(19, 4), @premiumTo decimal(19, 4),
          @mcoFeeFrom decimal(19, 4), @mcoFeeTo decimal(19, 4), @batchFrom decimal(19, 4), @batchTo decimal(19, 4),
          @employeeNoFrom nvarchar(50), @employeeNoTo nvarchar(50), @policyNoFrom nvarchar(50), @policyNoTo nvarchar(50),
          @planFrom nvarchar(10), @planTo nvarchar(10), @ageBandFrom nvarchar(10), @ageBandTo nvarchar(10), @postCodeFrom nvarchar(10),
          @postCodeTo nvarchar(10), @policyYearFrom nvarchar(10), @policyYearTo nvarchar(10), @invoiceNoFrom nvarchar(50), @invoiceNoTo nvarchar(50),
          @icFrom nvarchar(30), @icTo nvarchar(30), @effDateFrom date, @effDateTo date, @expDateFrom date, @expDateTo date,
          @dateJoinedFrom date, @dateJoinedTo date, @dorFrom date, @dorTo date, @dobFrom date, @dobTo date, @bordxDateFrom date, @bordxDateTo date,
          @invoiceDateFrom date, @invoiceDateTo date, @endtEffDateFrom date, @endtEffDateTo date, @cancelDateFrom date, @cancelDateTo date,
          @dorSuppFrom date, @dorSuppTo date',
        @health = @health, @payor = @payor, @adultChild = @adultChild, @race = @race, @sex = @sex, @relationship = @relationship,
        @dataStatus = @dataStatus, @expired = @expired, @premiumMode = @premiumMode, @state = @state, @takeOver = @takeOver, @agentCode = @agentCode,
        @membershipNo = @membershipNo, @groupCompany = @groupCompany, @labelStatus = @labelStatus, @department = @department,
        @productCategory = @productCategory, @dayInsuredFrom = @dayInsuredFrom, @dayInsuredTo = @dayInsuredTo, @ageDaysFrom = @ageDaysFrom,
        @ageDaysTo = @ageDaysTo, @delayBordxFrom = @delayBordxFrom, @delayBordxTo = @delayBordxTo, @delayInvBordxFrom = @delayInvBordxFrom,
        @delayInvBordxTo = @delayInvBordxTo, @delayRegFrom = @delayRegFrom, @delayRegTo = @delayRegTo, @delayInvDFrom = @delayInvDFrom,
        @delayInvDTo = @delayInvDTo, @delayInvEFrom = @delayInvEFrom, @delayInvETo = @delayInvETo, @premiumFrom = @premiumFrom,
        @premiumTo = @premiumTo, @mcoFeeFrom = @mcoFeeFrom, @mcoFeeTo = @mcoFeeTo, @batchFrom = @batchFrom, @batchTo = @batchTo,
        @employeeNoFrom = @employeeNoFrom, @employeeNoTo = @employeeNoTo, @policyNoFrom = @policyNoFrom, @policyNoTo = @policyNoTo,
        @planFrom = @planFrom, @planTo = @planTo, @ageBandFrom = @ageBandFrom, @ageBandTo = @ageBandTo, @postCodeFrom = @postCodeFrom,
        @postCodeTo = @postCodeTo, @policyYearFrom = @policyYearFrom, @policyYearTo = @policyYearTo, @invoiceNoFrom = @invoiceNoFrom,
        @invoiceNoTo = @invoiceNoTo, @icFrom = @icFrom, @icTo = @icTo, @effDateFrom = @effDateFrom, @effDateTo = @effDateTo,
        @expDateFrom = @expDateFrom, @expDateTo = @expDateTo, @dateJoinedFrom = @dateJoinedFrom, @dateJoinedTo = @dateJoinedTo,
        @dorFrom = @dorFrom, @dorTo = @dorTo, @dobFrom = @dobFrom, @dobTo = @dobTo, @bordxDateFrom = @bordxDateFrom, @bordxDateTo = @bordxDateTo,
        @invoiceDateFrom = @invoiceDateFrom, @invoiceDateTo = @invoiceDateTo, @endtEffDateFrom = @endtEffDateFrom, @endtEffDateTo = @endtEffDateTo,
        @cancelDateFrom = @cancelDateFrom, @cancelDateTo = @cancelDateTo, @dorSuppFrom = @dorSuppFrom, @dorSuppTo = @dorSuppTo;
END
GO

-- ===== main/171_tracking_cases.sql =====
-- GENERATED by tools/Generate-TrackingCases.ps1 from dbo.MBMTracking. Do not edit by hand; rerun the script instead.
-- Fixed query text of each MBMTracking case: prefix (SELECT ... FROM view WHERE ...) and suffix (GROUP BY / ORDER BY).
CREATE OR ALTER PROCEDURE genisis.Tracking_CaseText
    @case int, @prefix nvarchar(max) OUTPUT, @suffix nvarchar(max) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @prefix = CASE @case
    WHEN 1 THEN N'SELECT PAYCode, HLTCode, INSCode, AGECode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 2 THEN N'SELECT PAYCode, HLTCode, INSCode, AGECode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 3 THEN N'SELECT PAYCode, HLTCode, AGECode, COUNT(MBMNumber) AS TotalP,SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 4 THEN N'SELECT PAYCode, HLTCode, PrinAGECode, COUNT(MBMNumber) AS TotalS,SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 5 THEN N'SELECT PAYCode, HLTCode, AGECode, COUNT(MBMNumber) AS TotalP,SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 6 THEN N'SELECT PAYCode, HLTCode, AGECode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 7 THEN N'SELECT HLTCode, INSCode, AGECode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 8 THEN N'SELECT HLTCode, INSCode, PrinAGECode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 9 THEN N'SELECT HLTCode, INSCode, AGECode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 10 THEN N'SELECT HLTCode, INSCode, AGECode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 11 THEN N'SELECT PAYCode, HLTCode, INSCode, AGECode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 12 THEN N'SELECT PAYCode, HLTCode, INSCode, PrinAGECode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 13 THEN N'SELECT PAYCode, INSCode, AGECode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 14 THEN N'SELECT PAYCode, INSCode, PrinAGECode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 15 THEN N'SELECT PAYCode, INSCode, AGECode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 16 THEN N'SELECT PAYCode, INSCode, AGECode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 17 THEN N'SELECT PAYCode, MBMAgentCode, HLTCode, INSCode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 18 THEN N'SELECT PAYCode, MBMAgentCode, HLTCode, INSCode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 19 THEN N'SELECT MBMAgentCode, PAYCode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 20 THEN N'SELECT MBMAgentCode, PAYCode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 21 THEN N'SELECT PAYCode, MBMAgentCode, INSCode, PLNCode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 22 THEN N'SELECT PAYCode, MBMAgentCode, INSCode, PLNCode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 23 THEN N'SELECT PAYCode, MBMAgentCode, INSCode, PLNCode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 24 THEN N'SELECT PAYCode, MBMAgentCode, INSCode, PLNCode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 25 THEN N'SELECT PAYCode, MBMAgentCode, RACCode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross, SUM(MBMAccessFee) AS EAFee From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 26 THEN N'SELECT PAYCode, MBMAgentCode, RACCode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 27 THEN N'SELECT PAYCode, MBMAgentCode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 28 THEN N'SELECT PAYCode, MBMAgentCode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 29 THEN N'SELECT TOP 20 PAYCode, MBMAgentCode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 30 THEN N'SELECT TOP 20 PAYCode, MBMAgentCode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 31 THEN N'Select MBMNumber, MBMPolicyNo, MBMName, INSCode, MBMMemberType, PLNCode, MBMCCoverID, HLTCode, MBMAgentCode, MBMStatus, MBMAccessFee, MBMPayorEffDate, MBMPayorExpDate, MBMMCOFee, MBMPremium, DayInsured, PAYCode From View_MBM_Search2 Where 0 = 0'
    WHEN 32 THEN N'Select MBMNumber, MBMPolicyNo, MBMName, INSCode, MBMMemberType, PLNCode, MBMCCoverID, HLTCode, MBMAgentCode, MBMStatus, MBMAccessFee, MBMPayorEffDate, MBMPayorExpDate, MBMMCOFee, MBMPremium, DayInsured, PAYCode, RElCode, MBMCCoverID From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 33 THEN N'SELECT MBMValueDate, MBMBordxDate, MBMBatchNo, COUNT(MBMNumber) AS TotalP, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 34 THEN N'SELECT MBMValueDate, MBMBordxDate, MBMBatchNo, COUNT(MBMNumber) AS TotalS, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 35 THEN N'Select MBMNumber, MBMPolicyNo, MBMName, MBMBordxDate, MBMBatchNo, MBMPremium, MBMMemberType, MBMStatus, MBMCCoverID, MBMPayorEffDate, MBMPayorExpDate, MBMMCOFee, MBMAccessFee From View_MBM_Search2 Where 0 = 0'
    WHEN 36 THEN N'Select MBMNumber, MBMPolicyNo, MBMName, MBMBordxDate, MBMBatchNo, MBMPremium, MBMCCoverID, MBMMemberType, MBMStatus, MBMCCoverID, MBMPayorEffDate, MBMPayorExpDate, MBMMCOFee, MBMAccessFee From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 37 THEN N'SELECT PAYCode, MBMBordxDate From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 38 THEN N'SELECT PAYCode, MBMBordxDate From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 39 THEN N'SELECT PAYCode, MBMBordxDate, COUNT(MBMNumber) AS TotalIC, SUM(MBMMCOFEE) AS MCOFEE From VIEW_MBM_Search2 Where INSCode = ''I'' AND MBMAdultChild = ''C'''
    WHEN 40 THEN N'SELECT PAYCode, MBMBordxDate, COUNT(MBMNumber) AS TotalIA, SUM(MBMMCOFEE) AS MCOFEE From VIEW_MBM_Search2 Where INSCode = ''I'' AND MBMAdultChild = ''A'''
    WHEN 41 THEN N'SELECT PAYCode, MBMBordxDate, COUNT(MBMNumber) AS TotalF, SUM(MBMMCOFEE) AS MCOFEE From VIEW_MBM_Search2 Where INSCode = ''F'''
    WHEN 42 THEN N'SELECT PAYCode, MBMBordxDate, COUNT(MBMNumber) AS TotalGC, SUM(MBMMCOFEE) AS MCOFEE From VIEW_MBM_Search2 Where INSCode = ''G'' AND MBMAdultChild = ''C'''
    WHEN 43 THEN N'SELECT PAYCode, MBMBordxDate, COUNT(MBMNumber) AS TotalGA, SUM(MBMMCOFEE) AS MCOFEE From VIEW_MBM_Search2 Where INSCode = ''G'' AND MBMAdultChild = ''A'''
    WHEN 44 THEN N'SELECT PAYCode, MBMBordxDate, COUNT(MBMNumber) AS TotalH, SUM(MBMMCOFEE) AS MCOFEE From VIEW_MBM_Search2 Where INSCode = ''H'''
    WHEN 45 THEN N'SELECT PAYCode, MBMBordxDate, COUNT(MBMNumber) AS TotalFS, SUM(MBMMCOFee) AS MCOFee From View_MBM_Supplementary2 Where INSCode = ''F'''
    WHEN 46 THEN N'SELECT PAYCode, MBMBordxDate, COUNT(MBMNumber) AS TotalHS, SUM(MBMMCOFee) AS MCOFee From View_MBM_Supplementary2 Where INSCode = ''H'''
    WHEN 47 THEN N'SELECT PAYCode, MBMBordxDate From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 48 THEN N'SELECT PAYCode, MBMBordxDate, COUNT(MBMNumber) AS TotalIC, SUM(MBMPremium) AS Premium From VIEW_MBM_Search2 Where INSCode = ''I'' AND MBMAdultChild = ''C'''
    WHEN 49 THEN N'SELECT PAYCode, MBMBordxDate, COUNT(MBMNumber) AS TotalIA, SUM(MBMPremium) AS Premium From VIEW_MBM_Search2 Where INSCode = ''I'' AND MBMAdultChild = ''A'''
    WHEN 50 THEN N'SELECT PAYCode, MBMBordxDate, COUNT(MBMNumber) AS TotalF, SUM(MBMPremium) AS Premium From VIEW_MBM_Search2 Where INSCode = ''F'''
    WHEN 51 THEN N'SELECT PAYCode, MBMBordxDate, COUNT(MBMNumber) AS TotalFS, SUM(MBMPremium) AS Premium From View_MBM_Supplementary2 Where INSCode = ''F'''
    WHEN 52 THEN N'SELECT PAYCode, MBMBordxDate, COUNT(MBMNumber) AS TotalGC, SUM(MBMPremium) AS Premium From VIEW_MBM_Search2 Where INSCode = ''G'' AND MBMAdultChild = ''C'''
    WHEN 53 THEN N'SELECT PAYCode, MBMBordxDate, COUNT(MBMNumber) AS TotalGA, SUM(MBMPremium) AS Premium From VIEW_MBM_Search2 Where INSCode = ''G'' AND MBMAdultChild = ''A'''
    WHEN 54 THEN N'SELECT PAYCode, MBMBordxDate, COUNT(MBMNumber) AS TotalH, SUM(MBMPremium) AS Premium From VIEW_MBM_Search2 Where INSCode = ''H'''
    WHEN 55 THEN N'SELECT PAYCode, MBMBordxDate, COUNT(MBMNumber) AS TotalHS, SUM(MBMPremium) AS Premium From View_MBM_Supplementary2 Where INSCode = ''H'''
    WHEN 56 THEN N'SELECT PAYCode, MBMBordxDate From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 57 THEN N'SELECT PAYCode, MBMBordxDate, MBMBatchNo, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 58 THEN N'SELECT PAYCode, MBMBordxDate, MBMBatchNo, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 59 THEN N'SELECT HLTCode, BRNCode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 60 THEN N'SELECT HLTCode, BRNCode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 61 THEN N'SELECT BRNCode, MBMAgentCode From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 62 THEN N'SELECT BRNCode, MBMAgentCode From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 63 THEN N'SELECT BRNCode, MBMAgentCode, COUNT(MBMNumber) AS TotalP, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0 AND (BRNCode IS NULL OR BRNCode = '') AND (MBMAgentCode = '' OR MBMAgentCode IS NULL)'
    WHEN 64 THEN N'SELECT BRNCode, MBMAgentCode, COUNT(MBMNumber) AS TotalP, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0 AND (MBMAgentCode = '' OR MBMAgentCode IS NULL)'
    WHEN 65 THEN N'SELECT BRNCode, MBMAgentCode, COUNT(MBMNumber) AS TotalP, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0 AND (BRNCode IS NULL OR BRNCode = '')'
    WHEN 66 THEN N'SELECT BRNCode, MBMAgentCode, COUNT(MBMNumber) AS TotalP, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 67 THEN N'SELECT BRNCode, MBMAgentCode, COUNT(MBMNumber) AS TotalS, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0 AND (BRNCode IS NULL OR BRNCode = '') AND (MBMAgentCode = '' OR MBMAgentCode IS NULL)'
    WHEN 68 THEN N'SELECT BRNCode, MBMAgentCode, COUNT(MBMNumber) AS TotalS, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0 AND (MBMAgentCode = '' OR MBMAgentCode IS NULL)'
    WHEN 69 THEN N'SELECT BRNCode, MBMAgentCode, COUNT(MBMNumber) AS TotalS, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0 AND (BRNCode IS NULL OR BRNCode = '')'
    WHEN 70 THEN N'SELECT BRNCode, MBMAgentCode, COUNT(MBMNumber) AS TotalS, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 71 THEN N'SELECT BRNCode, MBMAgentCode, COUNT(CASNumber) AS TotalClaim, SUM(CLMTotalApproved) AS AppAmt From VIEW_MBMClaim_Search Where 0 = 0 AND (BRNCode IS NULL OR BRNCode = '') AND (MBMAgentCode = '' OR MBMAgentCode IS NULL)'
    WHEN 72 THEN N'SELECT BRNCode, MBMAgentCode, COUNT(CASNumber) AS TotalClaim, SUM(CLMTotalApproved) AS AppAmt From VIEW_MBMClaim_Search Where 0 = 0 AND (MBMAgentCode = '' OR MBMAgentCode IS NULL)'
    WHEN 73 THEN N'SELECT BRNCode, MBMAgentCode, COUNT(CASNumber) AS TotalClaim, SUM(CLMTotalApproved) AS AppAmt From VIEW_MBMClaim_Search Where 0 = 0 AND (BRNCode IS NULL OR BRNCode = '')'
    WHEN 74 THEN N'SELECT BRNCode, MBMAgentCode, COUNT(CASNumber) AS TotalClaim, SUM(CLMTotalApproved) AS AppAmt From VIEW_MBMClaim_Search Where 0 = 0'
    WHEN 75 THEN N'SELECT HLTCode, PAYCode, BRNCode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 76 THEN N'SELECT HLTCode, PAYCode, BRNCode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 77 THEN N'Select MBMNumber, MBMPolicyNo, MBMName, MBMDOB, MBMBordxDate, MBMValueDate, MBMInvoiceDate, MBMMemberType, MBMStatus, MBMPayorEffDate, MBMPayorExpDate, MBMRenewFlag, PAYCode, DayInsured, AgeDays, DelayBordx, DelayReg, DelayInvD, DelayInvE, DelayInvBordx, RELCode, MBMCCoverID From View_MBM_Search2 Where 0 = 0'
    WHEN 78 THEN N'Select MBMNumber, MBMPolicyNo, MBMName, MBMDOB, MBMBordxDate, MBMValueDate, MBMInvoiceDate, MBMMemberType, MBMStatus, MBMPayorEffDate, MBMPayorExpDate, MBMRenewFlag, PAYCode, DayInsured, AgeDays, DelayBordx, DelayReg, DelayInvD, DelayInvE, DelayInvBordx, RELCode, MBMCCoverID From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 79 THEN N'SELECT MBMNumber, MBMPolicyNo, MBMName, MBMPayorEffDate, MBMPayorExpDate, DayInsured, INSCode, PLNCode From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 80 THEN N'SELECT COUNT(MBMNumber) AS TotalP, DayInsured From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 81 THEN N'SELECT DayInsured, COUNT(MBMNumber) AS TotalS From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 82 THEN N'SELECT HLTCode, INSCode, MBMSex, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 83 THEN N'SELECT HLTCode, INSCode, PrinSex, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 84 THEN N'SELECT HLTCode, INSCode, MBMSex, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 85 THEN N'SELECT PAYCode, HLTCode, INSCode, MBMSex, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 86 THEN N'SELECT PAYCode, HLTCode, INSCode, PrinSex, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 87 THEN N'SELECT PAYCode, HLTCode, INSCode, MBMSex, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 88 THEN N'Select MBMNumber, MBMPolicyNo, MBMName, INSCode, AGECode, MBMSex, MBMExpiredStatus, MBMBatchNo, PLNCode, MBMGroupCompany, MBMMemberType, MBMStatus, MBMDataStatus, RElCode, MBMCCoverID, MBMDOB, MBMPayorEffDate, MBMPayorExpDate, MBMMCOFee, MBMBasicMCO, MBMAccessFee, MBMValueDate, MBMPremium, MBMBasicPrem, STACode, MBMBordxDate, RACCode, DayInsured, MBMRenewFlag, PAYCode, MBMIcBcPp, BRNCode,MBMEmployeeNo, MBMDepartment,MBMCostCenter,MBMGroupCategory,MBMSecurityCode,MBMClientType,Subsidiary,MBMRemarks,Remark, MBMInvoiceDate,MBMInvoiceNo From View_MBM_Search2 Where 0 = 0'
    WHEN 89 THEN N'Select MBMNumber, MBMPolicyNo, MBMName, INSCode, AGECode, MBMSex, MBMExpiredStatus, PLNCode, MBMGroupCompany, MBMMemberType, MBMStatus, MBMDataStatus, MBMBatchNo, MBMDOB, MBMPayorEffDate, MBMPayorExpDate, MBMMCOFee, MBMBasicMCO, MBMAccessFee, MBMValueDate, MBMPremium, MBMBasicPrem, STACode, MBMBordxDate, RACCode, DayInsured, MBMRenewFlag, PAYCode, MBMIcBcPp, BRNCode,MBMEmployeeNo, MBMDepartment, RELCode, MBMCCoverID,MBMCostCenter,MBMGroupCategory,MBMSecurityCode,MBMClientType,Subsidiary,MBMRemarks,Remark, MBMInvoiceDate,MBMInvoiceNo From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 90 THEN N'Select MBMNumber, MBMPolicyNo, MBMName, INSCode, AGECode, MBMSex, MBMExpiredStatus, PLNCode, MBMGroupCompany, MBMMemberType, MBMStatus, MBMDataStatus, MBMDOB, MBMPayorEffDate, MBMPayorExpDate, MBMMCOFee, MBMBasicMCO, MBMAccessFee, MBMValueDate, MBMPremium, MBMBasicPrem, STACode, MBMBordxDate, RACCode, DayInsured, MBMRenewFlag, PAYCode, MBMIcBcPp, BRNCode,MBMEmployeeNo, MBMDepartment, RELCode, MBMCCoverID, MBMCRegDate,MBMCostCenter,MBMGroupCategory From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 91 THEN N'Select MBMNumber, MBMPolicyNo, MBMName, INSCode, AGECode, MBMSex, MBMEmployeeNo, PLNCode, MBMGroupCompany, MBMMemberType, MBMStatus, RELCode, MBMCCoverID, MBMDOB, MBMPayorEffDate, MBMPayorExpDate, MBMMCOFee, MBMBasicMCO, MBMAccessFee, MBMPremium, MBMBasicPrem, STACode, MBMBordxDate, RACCode, DayInsured, MBMRenewFlag, PAYCode, MBMDepartment From View_MBM_Search2 Where 0 = 0'
    WHEN 92 THEN N'Select MBMNumber, MBMPolicyNo, MBMName, MBMCName, INSCode, AGECode, MBMSex, MBMEmployeeNo, PLNCode, MBMGroupCompany, MBMMemberType, MBMStatus, MBMDOB, MBMPayorEffDate, MBMPayorExpDate, MBMMCOFee, MBMBasicMCO, MBMAccessFee, MBMPremium, MBMBasicPrem, STACode, MBMBordxDate, RACCode, DayInsured, MBMRenewFlag, PAYCode, MBMDepartment, MBMCSex, MBMCAgeband, MBMCDOB, RELCode, MBMCCoverID From View_MBM_Supplementary3 Where 0 = 0'
    WHEN 93 THEN N'Select MBMNumber, MBMPolicyNo, MBMName, MBMCName, INSCode, AGECode, MBMSex, MBMEmployeeNo, PLNCode, MBMGroupCompany, MBMMemberType, MBMStatus, RELCode, MBMCCoverID, MBMDOB, MBMPayorEffDate, MBMPayorExpDate, MBMMCOFee, MBMBasicMCO, MBMAccessFee, MBMPremium, MBMBasicPrem, STACode, MBMBordxDate, RACCode, DayInsured, MBMRenewFlag, PAYCode, MBMDepartment, MBMCSex, MBMCAgeband, MBMCDOB, MBMCRegDate From View_MBM_Supplementary3 Where 0 = 0'
    WHEN 94 THEN N'Select MBMNumber, MBMName, RElCode, MBMCCoverID, MBMStatus, MBMMemberType, MBMPremium, MBMBasicMCO, MBMMCOFee, MBMBasicPrem, MBMPayorMCONet, MBMPayorMCOGross, MBMPayorPremNet, MBMPayorPremGross From View_MBM_Search2 Where 0 = 0'
    WHEN 95 THEN N'Select MBMNumber, MBMName, RElCode, MBMCCoverID, MBMStatus, MBMMemberType, MBMPremium, MBMBasicMCO, MBMMCOFee, MBMBasicPrem, MBMPayorMCONet, MBMPayorMCOGross, MBMPayorPremNet, MBMPayorPremGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 96 THEN N'Select MBMNumber, MBMPolicyNo, MBMName, MBMAddress1, CTYCode, MBMGroupCompany, MBMMemberType, MBMStatus, MBMCCoverID, MBMPostCode, STACode, MBMRenewFlag, PAYCode, MBMAddress2, MBMAddress3 From View_MBM_Search2 Where 0 = 0'
    WHEN 97 THEN N'Select MBMNumber, MBMPolicyNo, MBMName, MBMAddress1, CTYCode, MBMGroupCompany, MBMMemberType, MBMStatus, MBMCCoverID, MBMPostCode, STACode, MBMRenewFlag, PAYCode, MBMAddress2, MBMAddress3, MBMCCoverID, MBMCRegDate From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 98 THEN N'Select MBMNumber, MBMPolicyNo, MBMName, MBMAddress1, CTYCode, MBMGroupCompany, MBMMemberType, MBMStatus, MBMCCoverID, MBMPostCode, STACode, MBMRenewFlag, PAYCode, MBMAddress2, MBMAddress3, MBMCCoverID From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 99 THEN N'SELECT PAYCode, MBMExpiredStatus, INSCode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 100 THEN N'SELECT PAYCode, MBMExpiredStatus, INSCode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 101 THEN N'Select MBMNumber, MBMName, MBMIcBcPp, MBMInstallment, MBMMemberType, MBMStatus, MBMCCoverID, MBMTakeover, MBMPayorEffDate, MBMPayorExpDate, MBMPolicyYear, MBMRenewFlag, MBMFirstJoinedDate From View_MBM_Search2 Where 0 = 0'
    WHEN 102 THEN N'Select MBMNumber, MBMName, MBMIcBcPp, MBMInstallment, MBMMemberType, MBMStatus, MBMCCoverID, MBMTakeover, MBMPayorEffDate, MBMPayorExpDate, MBMPolicyYear, MBMRenewFlag, MBMFirstJoinedDate, MBMCCoverID From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 103 THEN N'SELECT MBMPolicyYear, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 104 THEN N'SELECT MBMPolicyYear, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 105 THEN N'SELECT PAYCode, MBMInstallment, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 106 THEN N'SELECT PAYCode, MBMInstallment, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 107 THEN N'SELECT PAYCode, MBMRenewFlag, INSCode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 108 THEN N'SELECT PAYCode, MBMRenewFlag, INSCode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 109 THEN N'SELECT PAYCode, MBMTakeover, INSCode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 110 THEN N'SELECT PAYCode, MBMTakeover, INSCode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 111 THEN N'SELECT HLTCode, INSCode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 112 THEN N'SELECT HLTCode, INSCode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 113 THEN N'SELECT PAYCode, HLTCode, INSCode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 114 THEN N'SELECT PAYCode, HLTCode, INSCode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 115 THEN N'SELECT MONTH(MBMPayorEffDate) AS Month, YEAR(MBMPayorEffDate) AS Year, COUNT(MBMNumber) AS TotalP, SUM(MBMPremium) AS PremiumGross, SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee FROM VIEW_MBM_Search2 Where 0 = 0'
    WHEN 116 THEN N'SELECT MONTH(MBMPayorEffDate) AS Month, YEAR(MBMPayorEffDate) AS Year, COUNT(MBMNumber) AS TotalS, SUM(MBMPremium) AS PremiumGross, SUM(MBMMCOFee) AS MCOGross FROM View_MBM_Supplementary2 Where 0 = 0'
    WHEN 117 THEN N'SELECT PAYCode, MONTH(MBMPayorEffDate) AS Month, YEAR(MBMPayorEffDate) AS Year, COUNT(MBMNumber) AS TotalP, SUM(MBMPremium) AS PremiumGross, SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee FROM VIEW_MBM_Search2 Where 0 = 0'
    WHEN 118 THEN N'SELECT PAYCode, MONTH(MBMPayorEffDate) AS Month, YEAR(MBMPayorEffDate) AS Year, COUNT(MBMNumber) AS TotalS, SUM(MBMPremium) AS PremiumGross, SUM(MBMMCOFee) AS MCOGross FROM View_MBM_Supplementary2 Where 0 = 0'
    WHEN 119 THEN N'SELECT MONTH(MBMPayorExpDate) AS Month, YEAR(MBMPayorExpDate) AS Year, COUNT(MBMNumber) AS TotalP, SUM(MBMPremium) AS PremiumGross, SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee FROM VIEW_MBM_Search2 Where 0 = 0'
    WHEN 120 THEN N'SELECT MONTH(MBMPayorExpDate) AS Month, YEAR(MBMPayorExpDate) AS Year, COUNT(MBMNumber) AS TotalS, SUM(MBMPremium) AS PremiumGross, SUM(MBMMCOFee) AS MCOGross FROM View_MBM_Supplementary2 Where 0 = 0'
    WHEN 121 THEN N'SELECT PAYCode, MONTH(MBMPayorExpDate) AS Month, YEAR(MBMPayorExpDate) AS Year, COUNT(MBMNumber) AS TotalP, SUM(MBMPremium) AS PremiumGross, SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee FROM VIEW_MBM_Search2 Where 0 = 0'
    WHEN 122 THEN N'SELECT PAYCode, MONTH(MBMPayorExpDate) AS Month, YEAR(MBMPayorExpDate) AS Year, COUNT(MBMNumber) AS TotalS, SUM(MBMPremium) AS PremiumGross, SUM(MBMMCOFee) AS MCOGross FROM View_MBM_Supplementary2 Where 0 = 0'
    WHEN 123 THEN N'SELECT MONTH(MBMValueDate) AS Month, YEAR(MBMValueDate) AS Year, COUNT(MBMNumber) AS TotalP, SUM(MBMPremium) AS PremiumGross, SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee FROM VIEW_MBM_Search2 Where 0 = 0'
    WHEN 124 THEN N'SELECT MONTH(MBMValueDate) AS Month, YEAR(MBMValueDate) AS Year, COUNT(MBMNumber) AS TotalS, SUM(MBMPremium) AS PremiumGross, SUM(MBMMCOFee) AS MCOGross FROM View_MBM_Supplementary2 Where 0 = 0'
    WHEN 125 THEN N'SELECT PAYCode, MONTH(MBMValueDate) AS Month, YEAR(MBMValueDate) AS Year, COUNT(MBMNumber) AS TotalP, SUM(MBMPremium) AS PremiumGross, SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee FROM VIEW_MBM_Search2 Where 0 = 0'
    WHEN 126 THEN N'SELECT PAYCode, MONTH(MBMValueDate) AS Month, YEAR(MBMValueDate) AS Year, COUNT(MBMNumber) AS TotalS, SUM(MBMPremium) AS PremiumGross, SUM(MBMMCOFee) AS MCOGross FROM View_MBM_Supplementary2 Where 0 = 0'
    WHEN 127 THEN N'Select MBMNumber, MBMPolicyNo, MBMName, MBMMemberType, MBMBordxDate, MBMBatchNo, MBMCCoverID, MBMStatus, MBMInvoiceNo, MBMInvoiceDate, MBMMCOFee, MBMValueDate,MBMGroupCompany From View_MBM_Search2 Where 0 = 0'
    WHEN 128 THEN N'Select MBMNumber, MBMPolicyNo, MBMName, MBMMemberType, MBMBordxDate, MBMBatchNo, MBMCCoverID, MBMStatus, MBMInvoiceNo, MBMInvoiceDate, MBMMCOFee, MBMValueDate, RELCode,MBMGroupCompany From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 129 THEN N'Select MBMNumber, MBMPolicyNo, MBMName, MBMMemberType, MBMBordxDate, MBMBatchNo, MBMCCoverID, MBMStatus, MBMInvoiceNo, MBMInvoiceDate, MBMMCOFee, MBMValueDate, RELCode From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 130 THEN N'SELECT PAYCode, MBMInvoiceNo, MBMInvoiceDate From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 131 THEN N'SELECT PAYCode, MBMInvoiceNo, MBMInvoiceDate From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 132 THEN N'SELECT PAYCode, MBMInvoiceNo, MBMInvoiceDate, COUNT(MBMNumber) AS TotalMBM, SUM(MBMMCOFEE) AS MCOFEE From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 133 THEN N'SELECT PAYCode, MBMInvoiceNo, MBMInvoiceDate, COUNT(MBMNumber) AS TotalMBMS, SUM(MBMMCOFEE) AS MCOFEE From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 134 THEN N'SELECT PAYCode, MBMInvoiceNo, MBMInvoiceDate, COUNT(MBMNumber) AS TotalMBM, SUM(MBMPremium) AS Premium From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 135 THEN N'SELECT PAYCode, MBMInvoiceNo, MBMInvoiceDate, COUNT(MBMNumber) AS TotalMBMS, SUM(MBMPremium) AS Premium From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 136 THEN N'SELECT HLTCode, PAYCode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 137 THEN N'SELECT HLTCode, PAYCode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 138 THEN N'SELECT PAYCode, SUM(MBMMCOFee) AS MBMMCOFee, SUM(MBMBasicMCO) AS MBMBasicMCO, SUM(MBMPayorMCOGross) AS MBMPayorMCOGross, SUM(MBMPayorMCONet) AS MBMPayorMCONet, SUM(MBMPremium) AS MBMPremium, SUM(MBMBasicPrem) AS MBMBasicPrem, SUM(MBMPayorPremGross) AS MBMPayorPremGross, SUM(MBMPayorPremNet) AS MBMPayorPremNet From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 139 THEN N'SELECT PAYCode, SUM(MBMMCOFee) AS MBMMCOFee, SUM(MBMBasicMCO) AS MBMBasicMCO, SUM(MBMPayorMCOGross) AS MBMPayorMCOGross, SUM(MBMPayorMCONet) AS MBMPayorMCONet, SUM(MBMPremium) AS MBMPremium, SUM(MBMBasicPrem) AS MBMBasicPrem, SUM(MBMPayorPremGross) AS MBMPayorPremGross, SUM(MBMPayorPremNet) AS MBMPayorPremNet From VIEW_MBM_Supplementary2 Where 0 = 0'
    WHEN 140 THEN N'SELECT PAYCompanyName, MBMGroupCompany, COUNT(MBMNumber) AS TotalP, SUM(MBMPremium) AS PremiumGross, SUM(CLMTotalApproved) AS CLMTotalApproved From VIEW_MBM_Search4 Where (INSCode = ''G'' OR INSCode = ''H'')'
    WHEN 141 THEN N'SELECT PAYCompanyName, MBMGroupCompany, COUNT(MBMNumber) AS TotalS From View_MBM_Supplementary4 Where (INSCode = ''G'' OR INSCode = ''H'')'
    WHEN 142 THEN N'SELECT HLTCode, INSCode, PLNCode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 143 THEN N'SELECT HLTCode, INSCode, PLNCode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 144 THEN N'SELECT PAYCode, HLTCode, INSCode, PLNCode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 145 THEN N'SELECT PAYCode, HLTCode, INSCode, PLNCode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 146 THEN N'SELECT HLTCode, INSCode, PLNCode, AGECode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 147 THEN N'SELECT HLTCode, INSCode, PLNCode, PrinAGECode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 148 THEN N'SELECT PAYCode, HLTCode, INSCode, PLNCode, AGECode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 149 THEN N'SELECT PAYCode, HLTCode, INSCode, PLNCode, PrinAGECode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 150 THEN N'SELECT HLTCode, INSCode, PLNCode, AGECode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 151 THEN N'SELECT PAYCode, HLTCode, INSCode, PLNCode, AGECode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFee) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 152 THEN N'SELECT PAYCode, MBMPolicyNo, MBMGroupCompany, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 153 THEN N'SELECT PAYCode, MBMPolicyNo, MBMGroupCompany, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 154 THEN N'SELECT HLTCode, INSCode, RACCode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 155 THEN N'SELECT HLTCode, INSCode, RACCode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 156 THEN N'SELECT PAYCode, HLTCode, INSCode, RACCode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 157 THEN N'SELECT PAYCode, HLTCode, INSCode, RACCode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 158 THEN N'SELECT PAYCode, HLTCode, RELCode, AGECode, COUNT(DISTINCT MBMNumber) AS TotalP, COUNT(MBMNumber) AS TotalS From VIEW_MBM_Search3 Where 0 = 0'
    WHEN 159 THEN N'SELECT PAYCode, HLTCode, RELCode, AGECode, COUNT(DISTINCT MBMNumber) AS TotalP, COUNT(MBMNumber) AS TotalS From View_MBM_Supplementary2 Where RELCode <> ''P'''
    WHEN 160 THEN N'SELECT PAYCode, HLTCode, INSCode, RELCode, COUNT(DISTINCT MBMNumber) AS TotalP, COUNT(MBMNumber) AS TotalS From VIEW_MBM_Search3 Where 0 = 0'
    WHEN 161 THEN N'SELECT PAYCode, HLTCode, INSCode, RELCode, COUNT(DISTINCT MBMNumber) AS TotalP, COUNT(MBMNumber) AS TotalS From View_MBM_Supplementary2 Where RELCode <> ''P'''
    WHEN 162 THEN N'SELECT HLTCode, STACode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 163 THEN N'SELECT HLTCode, STACode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 164 THEN N'SELECT PAYCode, HLTCode, STACode, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 165 THEN N'SELECT PAYCode, HLTCode, STACode, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 166 THEN N'SELECT HLTCode, PAYCode, MBMGroupCompany, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 167 THEN N'SELECT HLTCode, PAYCode, MBMGroupCompany, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 168 THEN N'SELECT HLTCode, PAYCode, MBMGroupCompany, MBMDepartment, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 169 THEN N'SELECT HLTCode, PAYCode, MBMGroupCompany, MBMDepartment, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 170 THEN N'SELECT PLNProCat, ProductNameCat, COUNT(MBMNumber) AS TotalP, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMAccessFee) AS EAFee, SUM(MBMPremium) AS PremiumGross From VIEW_MBM_Search2 Where 0 = 0'
    WHEN 171 THEN N'SELECT PLNProCat, ProductName, COUNT(MBMNumber) AS TotalS, SUM(MBMMCOFEE) AS MCOGross, SUM(MBMPremium) AS PremiumGross From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 172 THEN N'SELECT MONTH(MBMPayorEffDate) AS Month, YEAR(MBMPayorEffDate) AS Year, PLNProCat, ProductName, COUNT(MBMNumber) AS TotalP, SUM(MBMPremium) AS PremiumGross, SUM(MBMMCOFee) AS MCOGross FROM VIEW_MBM_Search2 Where 0 = 0'
    WHEN 173 THEN N'SELECT MONTH(MBMPayorEffDate) AS Month, YEAR(MBMPayorEffDate) AS Year, PLNProCat, ProductName, COUNT(MBMNumber) AS TotalS, SUM(MBMPremium) AS PremiumGross, SUM(MBMMCOFee) AS MCOGross FROM View_MBM_Supplementary2 Where 0 = 0'
    WHEN 174 THEN N'SELECT MONTH(MBMPayorExpDate) AS Month, YEAR(MBMPayorExpDate) AS Year, PLNProCat, ProductName, COUNT(MBMNumber) AS TotalP, SUM(MBMPremium) AS PremiumGross, SUM(MBMMCOFee) AS MCOGross FROM VIEW_MBM_Search2 Where 0 = 0'
    WHEN 175 THEN N'SELECT MONTH(MBMPayorExpDate) AS Month, YEAR(MBMPayorExpDate) AS Year, PLNProCat, ProductName, COUNT(MBMNumber) AS TotalS, SUM(MBMPremium) AS PremiumGross, SUM(MBMMCOFee) AS MCOGross FROM View_MBM_Supplementary2 Where 0 = 0'
    WHEN 176 THEN N'SELECT MONTH(MBMValueDate) AS Month, YEAR(MBMValueDate) AS Year, PLNProCat, ProductName, COUNT(MBMNumber) AS TotalP, SUM(MBMPremium) AS PremiumGross, SUM(MBMMCOFee) AS MCOGross FROM VIEW_MBM_Search2 Where 0 = 0'
    WHEN 177 THEN N'SELECT MONTH(MBMValueDate) AS Month, YEAR(MBMValueDate) AS Year, PLNProCat, ProductName, COUNT(MBMNumber) AS TotalS, SUM(MBMPremium) AS PremiumGross, SUM(MBMMCOFee) AS MCOGross FROM View_MBM_Supplementary2 Where 0 = 0'
    WHEN 178 THEN N'SELECT MONTH(MBMInvoiceDate) AS Month, YEAR(MBMInvoiceDate) AS Year, COUNT(MBMNumber) AS TotalP, SUM(MBMPremium) AS PremiumGross, SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee FROM VIEW_MBM_Search2 Where 0 = 0'
    WHEN 179 THEN N'SELECT MONTH(MBMInvoiceDate) AS Month, YEAR(MBMInvoiceDate) AS Year, COUNT(MBMNumber) AS TotalS, SUM(MBMPremium) AS PremiumGross, SUM(MBMMCOFee) AS MCOGross FROM View_MBM_Supplementary2 Where 0 = 0'
    WHEN 180 THEN N'SELECT PAYCode, MONTH(MBMInvoiceDate) AS Month, YEAR(MBMInvoiceDate) AS Year, COUNT(MBMNumber) AS TotalP, SUM(MBMPremium) AS PremiumGross, SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee FROM VIEW_MBM_Search2 Where 0 = 0'
    WHEN 181 THEN N'SELECT PAYCode, MONTH(MBMInvoiceDate) AS Month, YEAR(MBMInvoiceDate) AS Year, COUNT(MBMNumber) AS TotalS, SUM(MBMPremium) AS PremiumGross, SUM(MBMMCOFee) AS MCOGross FROM View_MBM_Supplementary2 Where 0 = 0'
    WHEN 182 THEN N'SELECT * From CLN Where 0 = 0'
    WHEN 183 THEN N'Select MBMNumber, MBMPolicyNo, MBMName, MBMCCoverID, MBMBordxDate, MBMInvoiceDate, MBMCardSentDate, MBMPayorEffDate, BRNCode, MBMAgentCode, MBMStatus, MBMMemberType From View_MBM_Search2 Where 0 = 0'
    WHEN 184 THEN N'Select MBMNumber, MBMPolicyNo, MBMName, MBMCCoverID, MBMBordxDate, MBMInvoiceDate, MBMCardSentDate, MBMPayorEffDate, BRNCode, MBMAgentCode, MBMStatus, MBMMemberType From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 185 THEN N'Select MBMNumber, MBMPolicyNo, MBMName, MBMCCoverID, MBMBordxDate, MBMInvoiceDate, MBMCardSentDate, MBMPayorEffDate, BRNCode, MBMAgentCode, MBMStatus, MBMMemberType From View_MBM_Supplementary2 Where 0 = 0'
    WHEN 186 THEN N'SELECT MONTH(MBMBordxDate) AS Month, YEAR(MBMBordxDate) AS Year, COUNT(MBMNumber) AS TotalP, SUM(MBMPremium) AS PremiumGross, SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee FROM VIEW_MBM_Search2 Where 0 = 0'
    WHEN 187 THEN N'SELECT MONTH(MBMBordxDate) AS Month, YEAR(MBMBordxDate) AS Year, COUNT(MBMNumber) AS TotalS, SUM(MBMPremium) AS PremiumGross, SUM(MBMMCOFee) AS MCOGross FROM View_MBM_Supplementary2 Where 0 = 0'
    WHEN 188 THEN N'SELECT PAYCode, MONTH(MBMBordxDate) AS Month, YEAR(MBMBordxDate) AS Year, COUNT(MBMNumber) AS TotalP, SUM(MBMPremium) AS PremiumGross, SUM(MBMMCOFee) AS MCOGross, SUM(MBMAccessFee) AS EAFee FROM VIEW_MBM_Search2 Where 0 = 0'
    WHEN 189 THEN N'SELECT PAYCode, MONTH(MBMBordxDate) AS Month, YEAR(MBMBordxDate) AS Year, COUNT(MBMNumber) AS TotalS, SUM(MBMPremium) AS PremiumGross, SUM(MBMMCOFee) AS MCOGross FROM View_MBM_Supplementary2 Where 0 = 0'
    WHEN 190 THEN N'SELECT PAYCode, MBMInvoiceNo, MBMInvoiceDate, COUNT(MBMNumber) AS TotalMBM, SUM(MBMMCOFEE) AS MCOFEE From VIEW_MBM_Search2_MX Where 0 = 0'
    WHEN 191 THEN N'SELECT case when coverid=''00'' then ''00'' else ''01'' end as cc,PAYCode, MBMInvoiceNo, MBMInvoiceDate, COUNT(MBMNumber) AS TotalMBM, SUM(MBMMCOFEE) AS MCOFEE From VIEW_MBM_Search_MCO Where 0 = 0'
    WHEN 192 THEN N'SELECT case when coverid=''00'' then ''Principal'' else ''Dependent'' end as cc, PAYCode, MBMInvoiceNo, MBMInvoiceDate, COUNT(MBMNumber) AS TotalMBM,SUM(MBMMCOFEE) AS MCOFEE into #temp From VIEW_MBM_Search_MCO Where 0 = 0'
    END;
    SET @suffix = CASE @case
    WHEN 1 THEN N'GROUP BY PAYCode, HLTCode, INSCode, AGECode'
    WHEN 2 THEN N'GROUP BY PAYCode, HLTCode, INSCode, AGECode'
    WHEN 3 THEN N'GROUP BY PAYCode, HLTCode, AGECode'
    WHEN 4 THEN N'GROUP BY PAYCode, HLTCode, PrinAGECode'
    WHEN 5 THEN N'GROUP BY PAYCode, HLTCode, AGECode'
    WHEN 6 THEN N'GROUP BY PAYCode, HLTCode, AGECode'
    WHEN 7 THEN N'GROUP BY HLTCode, INSCode, AGECode'
    WHEN 8 THEN N'GROUP BY HLTCode, INSCode, PrinAGECode'
    WHEN 9 THEN N'GROUP BY HLTCode, INSCode, AGECode'
    WHEN 10 THEN N'GROUP BY HLTCode, INSCode, AGECode'
    WHEN 11 THEN N'GROUP BY PAYCode, HLTCode, INSCode, AGECode'
    WHEN 12 THEN N'GROUP BY PAYCode, HLTCode, INSCode, PrinAGECode'
    WHEN 13 THEN N'GROUP BY PAYCode, INSCode, AGECode'
    WHEN 14 THEN N'GROUP BY PAYCode, INSCode, PrinAGECode'
    WHEN 15 THEN N'GROUP BY PAYCode, INSCode, AGECode'
    WHEN 16 THEN N'GROUP BY PAYCode, INSCode, AGECode'
    WHEN 17 THEN N'GROUP BY PAYCode, MBMAgentCode, HLTCode, INSCode'
    WHEN 18 THEN N'GROUP BY PAYCode, MBMAgentCode, HLTCode, INSCode'
    WHEN 19 THEN N'GROUP BY MBMAgentCode, PAYCode'
    WHEN 20 THEN N'GROUP BY MBMAgentCode, PAYCode'
    WHEN 21 THEN N'GROUP BY PAYCode, MBMAgentCode, INSCode, PLNCode'
    WHEN 22 THEN N'GROUP BY PAYCode, MBMAgentCode, INSCode, PLNCode'
    WHEN 23 THEN N'GROUP BY PAYCode, MBMAgentCode, INSCode, PLNCode'
    WHEN 24 THEN N'GROUP BY PAYCode, MBMAgentCode, INSCode, PLNCode'
    WHEN 25 THEN N'GROUP BY PAYCode, MBMAgentCode, RACCode'
    WHEN 26 THEN N'GROUP BY PAYCode, MBMAgentCode, RACCode'
    WHEN 27 THEN N'GROUP BY PAYCode, MBMAgentCode'
    WHEN 28 THEN N'GROUP BY PAYCode, MBMAgentCode'
    WHEN 29 THEN N'GROUP BY PAYCode, MBMAgentCode'
    WHEN 30 THEN N'GROUP BY PAYCode, MBMAgentCode ORDER BY PremiumGross Desc'
    WHEN 31 THEN N''
    WHEN 32 THEN N''
    WHEN 33 THEN N'GROUP BY MBMValueDate, MBMBordxDate, MBMBatchNo'
    WHEN 34 THEN N'GROUP BY MBMValueDate, MBMBordxDate, MBMBatchNo'
    WHEN 35 THEN N''
    WHEN 36 THEN N''
    WHEN 37 THEN N'GROUP BY PAYCode, MBMBordxDate'
    WHEN 38 THEN N'GROUP BY PAYCode, MBMBordxDate'
    WHEN 39 THEN N'GROUP BY PAYCode, MBMBordxDate'
    WHEN 40 THEN N'GROUP BY PAYCode, MBMBordxDate'
    WHEN 41 THEN N'GROUP BY PAYCode, MBMBordxDate'
    WHEN 42 THEN N'GROUP BY PAYCode, MBMBordxDate'
    WHEN 43 THEN N'GROUP BY PAYCode, MBMBordxDate'
    WHEN 44 THEN N'GROUP BY PAYCode, MBMBordxDate'
    WHEN 45 THEN N'GROUP BY PAYCode, MBMBordxDate'
    WHEN 46 THEN N'GROUP BY PAYCode, MBMBordxDate'
    WHEN 47 THEN N'GROUP BY PAYCode, MBMBordxDate'
    WHEN 48 THEN N'GROUP BY PAYCode, MBMBordxDate'
    WHEN 49 THEN N'GROUP BY PAYCode, MBMBordxDate'
    WHEN 50 THEN N'GROUP BY PAYCode, MBMBordxDate'
    WHEN 51 THEN N'GROUP BY PAYCode, MBMBordxDate'
    WHEN 52 THEN N'GROUP BY PAYCode, MBMBordxDate'
    WHEN 53 THEN N'GROUP BY PAYCode, MBMBordxDate'
    WHEN 54 THEN N'GROUP BY PAYCode, MBMBordxDate'
    WHEN 55 THEN N'GROUP BY PAYCode, MBMBordxDate'
    WHEN 56 THEN N'GROUP BY PAYCode, MBMBordxDate'
    WHEN 57 THEN N'GROUP BY PAYCode, MBMBordxDate, MBMBatchNo'
    WHEN 58 THEN N'GROUP BY PAYCode, MBMBordxDate, MBMBatchNo'
    WHEN 59 THEN N'GROUP BY HLTCode, BRNCode'
    WHEN 60 THEN N'GROUP BY HLTCode, BRNCode'
    WHEN 61 THEN N'GROUP BY BRNCode, MBMAgentCode'
    WHEN 62 THEN N'GROUP BY BRNCode, MBMAgentCode'
    WHEN 63 THEN N'GROUP BY BRNCode, MBMAgentCode'
    WHEN 64 THEN N'GROUP BY BRNCode, MBMAgentCode'
    WHEN 65 THEN N'GROUP BY BRNCode, MBMAgentCode'
    WHEN 66 THEN N'GROUP BY BRNCode, MBMAgentCode'
    WHEN 67 THEN N'GROUP BY BRNCode, MBMAgentCode'
    WHEN 68 THEN N'GROUP BY BRNCode, MBMAgentCode'
    WHEN 69 THEN N'GROUP BY BRNCode, MBMAgentCode'
    WHEN 70 THEN N'GROUP BY BRNCode, MBMAgentCode'
    WHEN 71 THEN N'GROUP BY BRNCode, MBMAgentCode'
    WHEN 72 THEN N'GROUP BY BRNCode, MBMAgentCode'
    WHEN 73 THEN N'GROUP BY BRNCode, MBMAgentCode'
    WHEN 74 THEN N'GROUP BY BRNCode, MBMAgentCode'
    WHEN 75 THEN N'GROUP BY HLTCode, PAYCode, BRNCode'
    WHEN 76 THEN N'GROUP BY HLTCode, PAYCode, BRNCode'
    WHEN 77 THEN N''
    WHEN 78 THEN N''
    WHEN 79 THEN N''
    WHEN 80 THEN N'GROUP BY DayInsured'
    WHEN 81 THEN N'GROUP BY DayInsured'
    WHEN 82 THEN N'GROUP BY HLTCode, INSCode, MBMSex'
    WHEN 83 THEN N'GROUP BY HLTCode, INSCode, PrinSex'
    WHEN 84 THEN N'GROUP BY HLTCode, INSCode, MBMSex'
    WHEN 85 THEN N'GROUP BY PAYCode, HLTCode, INSCode, MBMSex'
    WHEN 86 THEN N'GROUP BY PAYCode, HLTCode, INSCode, PrinSex'
    WHEN 87 THEN N'GROUP BY PAYCode, HLTCode, INSCode, MBMSex'
    WHEN 88 THEN N''
    WHEN 89 THEN N''
    WHEN 90 THEN N''
    WHEN 91 THEN N''
    WHEN 92 THEN N''
    WHEN 93 THEN N''
    WHEN 94 THEN N''
    WHEN 95 THEN N''
    WHEN 96 THEN N''
    WHEN 97 THEN N''
    WHEN 98 THEN N''
    WHEN 99 THEN N'GROUP BY PAYCode, MBMExpiredStatus, INSCode'
    WHEN 100 THEN N'GROUP BY PAYCode, MBMExpiredStatus, INSCode'
    WHEN 101 THEN N''
    WHEN 102 THEN N''
    WHEN 103 THEN N'GROUP BY MBMPolicyYear'
    WHEN 104 THEN N'GROUP BY MBMPolicyYear'
    WHEN 105 THEN N'GROUP BY PAYCode, MBMInstallment'
    WHEN 106 THEN N'GROUP BY PAYCode, MBMInstallment'
    WHEN 107 THEN N'GROUP BY PAYCode, MBMRenewFlag, INSCode'
    WHEN 108 THEN N'GROUP BY PAYCode, MBMRenewFlag, INSCode'
    WHEN 109 THEN N'GROUP BY PAYCode, MBMTakeover, INSCode'
    WHEN 110 THEN N'GROUP BY PAYCode, MBMTakeover, INSCode'
    WHEN 111 THEN N'GROUP BY HLTCode, INSCode'
    WHEN 112 THEN N'GROUP BY HLTCode, INSCode'
    WHEN 113 THEN N'GROUP BY PAYCode, HLTCode, INSCode'
    WHEN 114 THEN N'GROUP BY PAYCode, HLTCode, INSCode'
    WHEN 115 THEN N'GROUP BY MONTH(MBMPayorEffDate), YEAR(MBMPayorEffDate)'
    WHEN 116 THEN N'GROUP BY MONTH(MBMPayorEffDate), YEAR(MBMPayorEffDate)'
    WHEN 117 THEN N'GROUP BY PAYCode, MONTH(MBMPayorEffDate), YEAR(MBMPayorEffDate)'
    WHEN 118 THEN N'GROUP BY PAYCode, MONTH(MBMPayorEffDate), YEAR(MBMPayorEffDate)'
    WHEN 119 THEN N'GROUP BY MONTH(MBMPayorExpDate), YEAR(MBMPayorExpDate)'
    WHEN 120 THEN N'GROUP BY MONTH(MBMPayorExpDate), YEAR(MBMPayorExpDate)'
    WHEN 121 THEN N'GROUP BY PAYCode, MONTH(MBMPayorExpDate), YEAR(MBMPayorExpDate)'
    WHEN 122 THEN N'GROUP BY PAYCode, MONTH(MBMPayorExpDate), YEAR(MBMPayorExpDate)'
    WHEN 123 THEN N'GROUP BY MONTH(MBMValueDate), YEAR(MBMValueDate)'
    WHEN 124 THEN N'GROUP BY MONTH(MBMValueDate), YEAR(MBMValueDate)'
    WHEN 125 THEN N'GROUP BY PAYCode, MONTH(MBMValueDate), YEAR(MBMValueDate)'
    WHEN 126 THEN N'GROUP BY PAYCode, MONTH(MBMValueDate), YEAR(MBMValueDate)'
    WHEN 127 THEN N''
    WHEN 128 THEN N''
    WHEN 129 THEN N''
    WHEN 130 THEN N'GROUP BY PAYCode, MBMInvoiceNo, MBMInvoiceDate'
    WHEN 131 THEN N'GROUP BY PAYCode, MBMInvoiceNo, MBMInvoiceDate'
    WHEN 132 THEN N'GROUP BY PAYCode, MBMInvoiceNo, MBMInvoiceDate'
    WHEN 133 THEN N'GROUP BY PAYCode, MBMInvoiceNo, MBMInvoiceDate'
    WHEN 134 THEN N'GROUP BY PAYCode, MBMInvoiceNo, MBMInvoiceDate'
    WHEN 135 THEN N'GROUP BY PAYCode, MBMInvoiceNo, MBMInvoiceDate'
    WHEN 136 THEN N'GROUP BY HLTCode, PAYCode'
    WHEN 137 THEN N'GROUP BY HLTCode, PAYCode'
    WHEN 138 THEN N'GROUP BY PAYCode'
    WHEN 139 THEN N'GROUP BY PAYCode'
    WHEN 140 THEN N'GROUP BY PAYCompanyName, MBMGroupCompany'
    WHEN 141 THEN N'GROUP BY PAYCompanyName, MBMGroupCompany'
    WHEN 142 THEN N'GROUP BY HLTCode, INSCode, PLNCode'
    WHEN 143 THEN N'GROUP BY HLTCode, INSCode, PLNCode'
    WHEN 144 THEN N'GROUP BY PAYCode, HLTCode, INSCode, PLNCode'
    WHEN 145 THEN N'GROUP BY PAYCode, HLTCode, INSCode, PLNCode'
    WHEN 146 THEN N'GROUP BY HLTCode, INSCode, PLNCode, AGECode'
    WHEN 147 THEN N'GROUP BY HLTCode, INSCode, PLNCode, PrinAGECode'
    WHEN 148 THEN N'GROUP BY PAYCode, HLTCode, INSCode, PLNCode, AGECode'
    WHEN 149 THEN N'GROUP BY PAYCode, HLTCode, INSCode, PLNCode, PrinAGECode'
    WHEN 150 THEN N'GROUP BY HLTCode, INSCode, PLNCode, AGECode'
    WHEN 151 THEN N'GROUP BY PAYCode, HLTCode, INSCode, PLNCode, AGECode'
    WHEN 152 THEN N'GROUP BY PAYCode, MBMPolicyNo, MBMGroupCompany'
    WHEN 153 THEN N'GROUP BY PAYCode, MBMPolicyNo, MBMGroupCompany'
    WHEN 154 THEN N'GROUP BY HLTCode, INSCode, RACCode'
    WHEN 155 THEN N'GROUP BY HLTCode, INSCode, RACCode'
    WHEN 156 THEN N'GROUP BY PAYCode, HLTCode, INSCode, RACCode'
    WHEN 157 THEN N'GROUP BY PAYCode, HLTCode, INSCode, RACCode'
    WHEN 158 THEN N'GROUP BY PAYCode, HLTCode, RELCode, AGECode'
    WHEN 159 THEN N'GROUP BY PAYCode, HLTCode, RELCode, AGECode'
    WHEN 160 THEN N'GROUP BY PAYCode, HLTCode, INSCode, RELCode'
    WHEN 161 THEN N'GROUP BY PAYCode, HLTCode, INSCode, RELCode'
    WHEN 162 THEN N'GROUP BY HLTCode, STACode'
    WHEN 163 THEN N'GROUP BY HLTCode, STACode'
    WHEN 164 THEN N'GROUP BY PAYCode, HLTCode, STACode'
    WHEN 165 THEN N'GROUP BY PAYCode, HLTCode, STACode'
    WHEN 166 THEN N'GROUP BY HLTCode, PAYCode, MBMGroupCompany'
    WHEN 167 THEN N'GROUP BY HLTCode, PAYCode, MBMGroupCompany'
    WHEN 168 THEN N'GROUP BY HLTCode, PAYCode, MBMGroupCompany, MBMDepartment'
    WHEN 169 THEN N'GROUP BY HLTCode, PAYCode, MBMGroupCompany, MBMDepartment'
    WHEN 170 THEN N'GROUP BY PLNProCat,ProductNameCat'
    WHEN 171 THEN N'GROUP BY PLNProCat, ProductName'
    WHEN 172 THEN N'GROUP BY MONTH(MBMPayorEffDate), YEAR(MBMPayorEffDate), PLNProCat, ProductName'
    WHEN 173 THEN N'GROUP BY MONTH(MBMPayorEffDate), YEAR(MBMPayorEffDate), PLNProCat, ProductName'
    WHEN 174 THEN N'GROUP BY MONTH(MBMPayorExpDate), YEAR(MBMPayorExpDate), PLNProCat, ProductName'
    WHEN 175 THEN N'GROUP BY MONTH(MBMPayorExpDate), YEAR(MBMPayorExpDate), PLNProCat, ProductName'
    WHEN 176 THEN N'GROUP BY MONTH(MBMValueDate), YEAR(MBMValueDate), PLNProCat, ProductName'
    WHEN 177 THEN N'GROUP BY MONTH(MBMValueDate), YEAR(MBMValueDate), PLNProCat, ProductName'
    WHEN 178 THEN N'GROUP BY MONTH(MBMInvoiceDate), YEAR(MBMInvoiceDate)'
    WHEN 179 THEN N'GROUP BY MONTH(MBMInvoiceDate), YEAR(MBMInvoiceDate)'
    WHEN 180 THEN N'GROUP BY PAYCode, MONTH(MBMInvoiceDate), YEAR(MBMInvoiceDate)'
    WHEN 181 THEN N'GROUP BY PAYCode, MONTH(MBMInvoiceDate), YEAR(MBMInvoiceDate)'
    WHEN 182 THEN N''
    WHEN 183 THEN N''
    WHEN 184 THEN N''
    WHEN 185 THEN N''
    WHEN 186 THEN N'GROUP BY MONTH(MBMBordxDate), YEAR(MBMBordxDate)'
    WHEN 187 THEN N'GROUP BY MONTH(MBMBordxDate), YEAR(MBMBordxDate)'
    WHEN 188 THEN N'GROUP BY PAYCode, MONTH(MBMBordxDate), YEAR(MBMBordxDate)'
    WHEN 189 THEN N'GROUP BY PAYCode, MONTH(MBMBordxDate), YEAR(MBMBordxDate)'
    WHEN 190 THEN N'GROUP BY PAYCode, MBMInvoiceNo, MBMInvoiceDate'
    WHEN 191 THEN N'GROUP BY PAYCode, MBMInvoiceNo, MBMInvoiceDate,case when coverid=''00'' then ''00'' else ''01'' end'
    WHEN 192 THEN N'GROUP BY case when coverid=''00'' then ''Principal'' else ''Dependent'' end,PAYCode, MBMInvoiceNo, MBMInvoiceDate Select [PAYCode], Sum(coalesce(Principal_amt,0)) as Principal_amt, Sum(coalesce(Dependent_amt,0)) as Dependent_amt, Sum(coalesce(Principal_mbmcnt,0)) as Principal_mbmcnt, Sum(coalesce(Dependent_mbmcnt,0)) as Dependent_mbmcnt, MBMInvoiceNo, MBMInvoiceDate from ( Select [PAYCode],MBMInvoiceNo, MBMInvoiceDate ,Sum(MCOFEE) as MCOFEE ,Sum(TotalMBM) as TotalMBM ,[MCOFEE_amt]=[CC]+''_amt'' ,[TotalMBM_cnt]=[CC]+''_mbmcnt'' from #temp group by PAYCode,[CC],MBMInvoiceNo, MBMInvoiceDate )NN PIVOT (Sum(MCOFEE) for [MCOFEE_amt] IN (Principal_amt,Dependent_amt)) As PV1 PIVOT (Sum(TotalMBM) for [TotalMBM_cnt] IN (Principal_mbmcnt,Dependent_mbmcnt)) As PV2 group by [PAYCode],MBMInvoiceNo, MBMInvoiceDate Drop table #temp'
    END;
END
GO

PRINT N'Done.';
GO
SET NOEXEC OFF;
GO
