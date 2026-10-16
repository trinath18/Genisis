-- GENERATED FILE: built from sql/main/*.sql. Edit those files, then rebuild this one with
--     GENISIS_WRITE_SQL=1 dotnet test --filter Combined_scripts_are_up_to_date
-- How to run: open this file in SSMS, pick the main database in the "Available Databases" box,
-- check @MaintenanceDb in the first section, then press F5 (Execute).
-- Safe to run again: it only creates or replaces genisis.* procedures and synonyms; tables and data are not changed.

-- ===== main/000_setup.sql =====
-- Setup for the Genesis stored procedures. Run on the main database (the one that has dbo.PLN and dbo.MBM).
-- Creates the genisis schema and the synonyms the procedures use to read the maintenance database.
-- If your maintenance database has another name, change @MaintenanceDb below before running.
SET NOEXEC OFF;
SET NOCOUNT ON;
DECLARE @MaintenanceDb sysname = N'HISMaintenance';

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

IF SCHEMA_ID(N'genisis') IS NULL EXEC (N'CREATE SCHEMA genisis AUTHORIZATION dbo');

DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'IF OBJECT_ID(N''genisis.' + s + N''', N''SN'') IS NOT NULL DROP SYNONYM genisis.' + s + N';
CREATE SYNONYM genisis.' + s + N' FOR ' + QUOTENAME(@MaintenanceDb) + N'.dbo.' + t + N';
'
FROM (VALUES (N'Maint_ProductCategory', N'ProductCategory'), (N'Maint_PolicyCategory', N'PolicyCategory'),
    (N'Maint_MBMCrossReference', N'MBMCrossReference'), (N'Maint_MBMSEQ05', N'MBMSEQ05'),
    (N'Maint_MBMSEQRenew05', N'MBMSEQRenew05'), (N'Maint_MBMSEQRenewPol', N'MBMSEQRenewPol'),
    (N'Maint_MBMSEQRenewHistory', N'MBMSEQRenewHistory'), (N'Maint_CTY', N'CTY'), (N'Maint_SuppMCOStatus', N'SuppMCOStatus'), (N'Maint_MBMSEQ', N'MBMSEQ'), (N'Maint_CaseMgmtFee', N'CaseMgmtFee'),
    (N'Maint_MMAProcedures', N'MMAProcedures'), (N'Maint_MMAFees', N'MMAFees'), (N'Maint_MMASchedule', N'VIEW_MMA_Schedule')) v (s, t);
EXEC (@sql);
PRINT N'genisis schema and synonyms ready (maintenance database: ' + @MaintenanceDb + N').';
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

PRINT N'Done.';
GO
SET NOEXEC OFF;
GO
