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
    (N'Maint_MMAProcedures', N'MMAProcedures'), (N'Maint_MMAFees', N'MMAFees'), (N'Maint_MMASchedule', N'VIEW_MMA_Schedule')) v (s, t);
    (N'Maint_CAQ2013', N'CAQ2013'), (N'Maint_CASCrossReference', N'CASCrossReference'), (N'Maint_SpecApprove', N'SpecApprove'),
    (N'Maint_REPSelection', N'REPSelection'), (N'Maint_MMASchedule', N'VIEW_MMA_Schedule'),
    (N'Maint_MMAProcedures', N'MMAProcedures'),
    (N'Maint_Case_RegInsertCASRef', N'Case_RegInsertCASRef'), (N'Maint_Case_RegInsertCASRef1', N'Case_RegInsertCASRef1'),
    (N'Maint_CASLG', N'CASLG'), (N'Maint_LGSeq', N'LGSeq'), (N'Maint_tblRecord', N'tblRecord'),
    (N'Maint_Case_AdjInsertCASRef', N'Case_AdjInsertCASRef'), (N'Maint_Case_AdjUpdateCASRef', N'Case_AdjUpdateCASRef'),
    (N'Maint_Case_AdjInsertCASLG_2011', N'Case_AdjInsertCASLG_2011'), (N'Maint_Case_AdjUpdateCASLG', N'Case_AdjUpdateCASLG'),
    (N'Maint_SearchPendingReason', N'SearchPendingReason')) v (s, t);
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
-- ===== main/130_case_enquiry.sql =====
-- Case > Enquiry and Case > Tracking (read-only).
-- Replaces the legacy Case_Inquiry / CaseTracking dispatchers, which appended a raw @strAppend
-- string into dynamic SQL; these procs take typed parameters instead.
-- SearchCas, SearchCasAdjustment, SearchCasHistory, SearchHOS and SearchPayor are existing
-- procedures and are called by the API as they are.
-- dbo.CaseEnquiryNotes lives on the worksheet database (VIEW_WS_Listing) and is called on the
-- WorkSheet connection as it is.

-- @by: number | member | patient | ic | policy. @prefix is an escaped LIKE prefix ending in %,
-- @contains is an escaped LIKE fragment wrapped in % by the caller.
CREATE OR ALTER PROCEDURE genisis.CaseEnquiry_Search
    @max int, @by nvarchar(4000), @q nvarchar(4000), @prefix nvarchar(4000), @contains nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) c.CASNumber, c.MBMNumber, c.MBMPatientCovID,
           COALESCE(cp.MBMCName, x.MBMName) AS PatientName,
           c.CASStatus, c.CASClaimability, c.CASDateAdmitted, c.CASDischargeDate,
           c.CaseRegDate, c.CaseOpenBy, c.CASClaimAdmin, c.CASUserToken, c.CASTokenBatch,
           c.CASStayType, c.CASPAYType, c.ClaimNatureCode, c.CASDocType,
           h.HOSCode, h.HOSName,
           x.MBMPolicyNo, x.MBMIcBcPp, x.MBMName, x.PAYCode,
           f.DIAPrelimDiag1 AS FirstDiag, d.DIAFinalDiag1 AS FinalDiag
    FROM dbo.CAS c WITH (NOLOCK)
    LEFT JOIN genisis.Maint_MBMCrossReference x ON c.MBMNumber = x.MBMNumber
    LEFT JOIN dbo.MBMCoveredPersons cp WITH (NOLOCK)
        ON c.MBMNumber = cp.MBMCNumber AND c.MBMPatientCovID = cp.MBMCCoverID
    LEFT JOIN dbo.HOS h WITH (NOLOCK) ON c.CASHOSCode = h.HOSCode
    LEFT JOIN dbo.DIAFirst f WITH (NOLOCK) ON c.CASNumber = f.CASNumber
    LEFT JOIN dbo.DIAFinal d WITH (NOLOCK) ON c.CASNumber = d.CASNumber
    WHERE (@by = N'number' AND c.CASNumber LIKE @prefix)
       OR (@by = N'member' AND c.MBMNumber LIKE @prefix)
       OR (@by = N'patient' AND (cp.MBMCName LIKE @contains OR x.MBMName LIKE @contains))
       OR (@by = N'ic' AND (x.MBMIcBcPp = @q OR REPLACE(x.MBMIcBcPp, '-', '') = REPLACE(@q, '-', '')))
       OR (@by = N'policy' AND x.MBMPolicyNo LIKE @prefix)
    ORDER BY c.CaseRegDate DESC, c.CASNumber
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
-- Diagnosis / repudiation block of frmCaseInquiry (legacy Case_Inquiry @strAppendCase 2, 5 and 6).
-- Result sets: first diagnosis, final diagnosis, repudiation codes with descriptions.
CREATE OR ALTER PROCEDURE genisis.CaseEnquiry_Diagnoses
    @casNumber nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ICDCode1, ICDCode2, ICDCode3, DIAPrelimDiag1, DIAPrelimDiag2, DIAPrelimDiag3,
           DIADOS1, DIADOS2, DIADOS3, DIALastAdjustmentUser, DIALastAdjustmentDate
    FROM dbo.DIAFirst WITH (NOLOCK) WHERE CASNumber = @casNumber;
    SELECT ICDCode1, ICDCode2, ICDCode3, DIAFinalDiag1, DIAFinalDiag2, DIAFinalDiag3,
           DIADOS1, DIADOS2, DIADOS3, DIALastAdjustmentUser, DIALastAdjustmentDate
    FROM dbo.DIAFinal WITH (NOLOCK) WHERE CASNumber = @casNumber;
    SELECT r.CASREPCode1, r.CASREPCode2, r.CASREPCode3,
           r.CASREPReasons1, r.CASREPReasons2, r.CASREPReasons3,
           r.CASREPIssuedBy, r.CASREPDate, r.CASPersonnel, r.CASAttention,
           r.CASUnderlying, r.CASUnderlyingRemark, r.CASLetter, r.Remarks,
           r1.RepDescription AS RepDesc1, r2.RepDescription AS RepDesc2, r3.RepDescription AS RepDesc3
    FROM dbo.CASRepudiation r WITH (NOLOCK)
    LEFT JOIN dbo.RepCode r1 ON r1.RepCode = r.CASREPCode1
    LEFT JOIN dbo.RepCode r2 ON r2.RepCode = r.CASREPCode2
    LEFT JOIN dbo.RepCode r3 ON r3.RepCode = r.CASREPCode3
    WHERE r.CASNumber = @casNumber;
END
GO

-- Lookup lists for the enquiry / tracking filter dropdowns.
-- Result sets: hospitals, payors, repudiation codes, claim nature codes, health types, insured types.
CREATE OR ALTER PROCEDURE genisis.Case_Lookups
AS
BEGIN
    SET NOCOUNT ON;
    SELECT HOSCode, HOSName FROM dbo.HOS ORDER BY HOSName;
    SELECT PAYCode, PAYCompanyName FROM dbo.PAY ORDER BY PAYCompanyName;
    SELECT RepCode, RepDescription FROM dbo.RepCode ORDER BY RepCode;
    SELECT ClaimNatureCode, ClaimNatureName FROM dbo.ClaimNature ORDER BY ClaimNatureCode;
    SELECT HLTCode, HLTDescription FROM dbo.HLT ORDER BY HLTCode;
    SELECT INSCode, INSDescription FROM dbo.INS ORDER BY INSCode;
END
GO

-- FrmCaseTracking main list (legacy CaseTracking @strAppendCase = 1).
-- Same column set and GROUP BY over VIEW_CAS_TRACKING2; every filter is optional.
-- @status: 'O', 'D', or 'OC' for open-or-closed. @claimability: 'A' approved set, 'R' repudiated set,
-- or an exact 2-letter code. @pending: 'Y' or 'N' (N also matches blank/NULL). @memberType:
-- 'principal' or 'supplementary'. LIKE filters take already-escaped text; the proc wraps it in %.
CREATE OR ALTER PROCEDURE genisis.CaseTracking_List
    @max int,
    @payor nvarchar(4000) = NULL,
    @caseFrom nvarchar(4000) = NULL, @caseTo nvarchar(4000) = NULL,
    @mbmFrom nvarchar(4000) = NULL, @mbmTo nvarchar(4000) = NULL,
    @regFrom datetime = NULL, @regTo datetime = NULL,
    @batch nvarchar(4000) = NULL,
    @patient nvarchar(4000) = NULL,
    @grp nvarchar(4000) = NULL,
    @status nvarchar(4000) = NULL,
    @claimability nvarchar(4000) = NULL,
    @pending nvarchar(4000) = NULL,
    @regBy nvarchar(4000) = NULL,
    @payType nvarchar(4000) = NULL,
    @userToken nvarchar(4000) = NULL,
    @sex nvarchar(4000) = NULL,
    @memberType nvarchar(4000) = NULL,
    @hltCode nvarchar(4000) = NULL,
    @insCode nvarchar(4000) = NULL,
    @renewFlag nvarchar(4000) = NULL,
    @takeover nvarchar(4000) = NULL,
    @agentCode nvarchar(4000) = NULL,
    @policyNo nvarchar(4000) = NULL,
    @doctor nvarchar(4000) = NULL,
    @repFrom nvarchar(4000) = NULL, @repTo nvarchar(4000) = NULL,
    @claimNatureFrom nvarchar(4000) = NULL, @claimNatureTo nvarchar(4000) = NULL,
    @hosName nvarchar(4000) = NULL,
    @stayType nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) CASNumber, MBMPolicyNo, MBMNumber, PatientName, CaseOpenBy,
           CASDateAdmitted, CASStayType, CASPAYType, CASClaimAdmin,
           CASDischargeDate, CaseRegDate, CASStatus, HOSName,
           FirstDiag, FinalDiag, CASUserToken, CASTokenDate, CASQuery,
           CASClaimability, CASAdmissionReason, ActualAmt, CASDRD, CLMBTBGAmt,
           NoofDay, DurationStay, ApprovedAmt, BordxRefNo, CLMBordxDate,
           ClaimOpenDate, ClaimOpenBy, CLMTotalActualGST, PatientIC
    FROM dbo.VIEW_CAS_TRACKING2
    WHERE (@payor IS NULL OR PAYCode = @payor)
      AND (@caseFrom IS NULL OR CASNumber >= @caseFrom)
      AND (@caseTo IS NULL OR CASNumber <= @caseTo)
      AND (@mbmFrom IS NULL OR MBMNumber >= @mbmFrom)
      AND (@mbmTo IS NULL OR MBMNumber <= @mbmTo)
      AND (@regFrom IS NULL OR CaseRegDate >= @regFrom)
      AND (@regTo IS NULL OR CaseRegDate < DATEADD(day, 1, @regTo))
      AND (@batch IS NULL OR CASTokenBatch = @batch)
      AND (@patient IS NULL OR PatientName LIKE '%' + @patient + '%')
      AND (@grp IS NULL OR MBMGroupCompany = @grp)
      AND (@status IS NULL OR (@status = N'OC' AND CASStatus IN (N'O', N'C')) OR CASStatus = @status)
      AND (@claimability IS NULL
           OR (@claimability = N'A' AND CASClaimability IN (N'AA', N'SA', N'NA', N'PA', N'RA'))
           OR (@claimability = N'R' AND CASClaimability IN (N'RR', N'NR', N'PR', N'AR', N'BR'))
           OR (@claimability NOT IN (N'A', N'R') AND CASClaimability = @claimability))
      AND (@pending IS NULL
           OR (@pending = N'N' AND (CASPending = N'N' OR CASPending = '' OR CASPending IS NULL))
           OR (@pending = N'Y' AND CASPending = N'Y'))
      AND (@regBy IS NULL OR CaseOpenBy = @regBy)
      AND (@payType IS NULL OR CASPAYType = @payType)
      AND (@userToken IS NULL OR CASUserToken = @userToken)
      AND (@sex IS NULL OR Sex = @sex)
      AND (@memberType IS NULL
           OR (@memberType = N'principal' AND MBMPatientCovID = '00')
           OR (@memberType = N'supplementary' AND MBMPatientCovID <> '00'))
      AND (@hltCode IS NULL OR HLTCode = @hltCode)
      AND (@insCode IS NULL OR INSCode = @insCode)
      AND (@renewFlag IS NULL
           OR (@renewFlag = N'N' AND (MBMRenewFlag IS NULL OR MBMRenewFlag = '' OR MBMRenewFlag = 'N'))
           OR (@renewFlag <> N'N' AND MBMRenewFlag = @renewFlag))
      AND (@takeover IS NULL OR MBMTakeover = @takeover)
      AND (@agentCode IS NULL OR MBMAgentCode LIKE '%' + @agentCode + '%')
      AND (@policyNo IS NULL OR MBMPolicyNo LIKE '%' + @policyNo + '%')
      AND (@doctor IS NULL OR CASAttendDoctor1 LIKE '%' + @doctor + '%')
      AND (@repFrom IS NULL OR CASREPCode1 >= @repFrom)
      AND (@repTo IS NULL OR CASREPCode1 <= @repTo)
      AND (@claimNatureFrom IS NULL OR ClaimNatureCode >= @claimNatureFrom)
      AND (@claimNatureTo IS NULL OR ClaimNatureCode <= @claimNatureTo)
      AND (@hosName IS NULL OR HOSName = @hosName)
      AND (@stayType IS NULL OR CASStayType = @stayType)
    GROUP BY CASNumber, MBMPolicyNo, MBMNumber, PatientName, CaseOpenBy,
             CASDateAdmitted, CASStayType, CASPAYType, CASClaimAdmin,
             CASDischargeDate, CaseRegDate, CASStatus, HOSName,
             FirstDiag, FinalDiag, CASUserToken, CASTokenDate, CASQuery,
             CASClaimability, CASAdmissionReason, ActualAmt, CASDRD, CLMBTBGAmt,
             NoofDay, DurationStay, ApprovedAmt, BordxRefNo, CLMBordxDate,
             ClaimOpenDate, ClaimOpenBy, CLMTotalActualGST, PatientIC
    ORDER BY MAX(CASNumber), CASNumber
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
-- FrmCaseTracking totals (legacy CaseTracking summary modes).
-- Same filter set over VIEW_CAS_TRACKINGSUM, plus @claimPending: 'P' pending, 'C' closed, 'B' both-side.
CREATE OR ALTER PROCEDURE genisis.CaseTracking_Summary
    @payor nvarchar(4000) = NULL,
    @caseFrom nvarchar(4000) = NULL, @caseTo nvarchar(4000) = NULL,
    @mbmFrom nvarchar(4000) = NULL, @mbmTo nvarchar(4000) = NULL,
    @regFrom datetime = NULL, @regTo datetime = NULL,
    @batch nvarchar(4000) = NULL,
    @patient nvarchar(4000) = NULL,
    @grp nvarchar(4000) = NULL,
    @status nvarchar(4000) = NULL,
    @claimability nvarchar(4000) = NULL,
    @pending nvarchar(4000) = NULL,
    @claimPending nvarchar(4000) = NULL,
    @regBy nvarchar(4000) = NULL,
    @payType nvarchar(4000) = NULL,
    @userToken nvarchar(4000) = NULL,
    @sex nvarchar(4000) = NULL,
    @memberType nvarchar(4000) = NULL,
    @hltCode nvarchar(4000) = NULL,
    @insCode nvarchar(4000) = NULL,
    @renewFlag nvarchar(4000) = NULL,
    @takeover nvarchar(4000) = NULL,
    @agentCode nvarchar(4000) = NULL,
    @policyNo nvarchar(4000) = NULL,
    @doctor nvarchar(4000) = NULL,
    @repFrom nvarchar(4000) = NULL, @repTo nvarchar(4000) = NULL,
    @claimNatureFrom nvarchar(4000) = NULL, @claimNatureTo nvarchar(4000) = NULL,
    @hosName nvarchar(4000) = NULL,
    @stayType nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(DISTINCT CASNumber) AS CaseCount,
           SUM(CLMTotalActual) AS ActualAmt,
           SUM(CLMTotalApproved) AS ApprovedAmt
    FROM dbo.VIEW_CAS_TRACKINGSUM
    WHERE (@payor IS NULL OR PAYCode = @payor)
      AND (@caseFrom IS NULL OR CASNumber >= @caseFrom)
      AND (@caseTo IS NULL OR CASNumber <= @caseTo)
      AND (@mbmFrom IS NULL OR MBMNumber >= @mbmFrom)
      AND (@mbmTo IS NULL OR MBMNumber <= @mbmTo)
      AND (@regFrom IS NULL OR CaseRegDate >= @regFrom)
      AND (@regTo IS NULL OR CaseRegDate < DATEADD(day, 1, @regTo))
      AND (@patient IS NULL OR PatientName LIKE '%' + @patient + '%')
      AND (@grp IS NULL OR MBMGroupCompany = @grp)
      AND (@status IS NULL OR (@status = N'OC' AND CASStatus IN (N'O', N'C')) OR CASStatus = @status)
      AND (@claimability IS NULL
           OR (@claimability = N'A' AND CASClaimability IN (N'AA', N'SA', N'NA', N'PA', N'RA'))
           OR (@claimability = N'R' AND CASClaimability IN (N'RR', N'NR', N'PR', N'AR', N'BR'))
           OR (@claimability NOT IN (N'A', N'R') AND CASClaimability = @claimability))
      AND (@claimPending IS NULL OR CLMPending = @claimPending)
      AND (@regBy IS NULL OR CaseOpenBy = @regBy)
      AND (@payType IS NULL OR CASPAYType = @payType)
      AND (@sex IS NULL OR Sex = @sex)
      AND (@memberType IS NULL
           OR (@memberType = N'principal' AND MBMPatientCovID = '00')
           OR (@memberType = N'supplementary' AND MBMPatientCovID <> '00'))
      AND (@hltCode IS NULL OR HLTCode = @hltCode)
      AND (@insCode IS NULL OR INSCode = @insCode)
      AND (@renewFlag IS NULL
           OR (@renewFlag = N'N' AND (MBMRenewFlag IS NULL OR MBMRenewFlag = '' OR MBMRenewFlag = 'N'))
           OR (@renewFlag <> N'N' AND MBMRenewFlag = @renewFlag))
      AND (@takeover IS NULL OR MBMTakeover = @takeover)
      AND (@agentCode IS NULL OR MBMAgentCode LIKE '%' + @agentCode + '%')
      AND (@policyNo IS NULL OR MBMPolicyNo LIKE '%' + @policyNo + '%')
      AND (@repFrom IS NULL OR CASREPCode1 >= @repFrom)
      AND (@repTo IS NULL OR CASREPCode1 <= @repTo)
      AND (@claimNatureFrom IS NULL OR ClaimNatureCode >= @claimNatureFrom)
      AND (@claimNatureTo IS NULL OR ClaimNatureCode <= @claimNatureTo)
      AND (@hosName IS NULL OR HOSName = @hosName)
      AND (@stayType IS NULL OR CASStayType = @stayType)
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
-- ===== main/140_case_registration.sql =====
-- Case > Registration (frmCaseAdmission).
-- Reads replace the legacy Case_Registration dispatchers (main and maintenance), which appended a raw
-- @strAppend string into dynamic SQL. The inserts reuse the existing parameterized procedures
-- (Case_RegInsertCAS2013 / CAS32013, Case_RegInsertCASRemark / 2 / 3, Case_RegInsertDIAFirst / DIAFinal,
-- Case_RegInsertUD, Case_RegInsertCASOthers / 2, Case_RegInsertCASRep, and the maintenance
-- Case_RegInsertCASRef / Ref1 through synonyms); the API calls them inside one transaction.
-- SearchMBMCas is an existing parameterized procedure and is called as it is.

-- Combo lists filled by ComboListing: special approval, claim nature, cash type, stay type, treatment mode,
-- hospitals, and the repudiation letter selections (frmRepudiationSelection: D = document, P = personnel, A = attention).
CREATE OR ALTER PROCEDURE genisis.CaseReg_Lookups
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(SpecAppCode) AS Code, RTRIM(SpecAppDesc) AS Name FROM genisis.Maint_SpecApprove;
    SELECT RTRIM(ClaimNatureCode) AS Code, RTRIM(ClaimNatureName) AS Name FROM dbo.ClaimNature;
    SELECT RTRIM(PayTypeCode) AS Code, RTRIM(PayTypeName) AS Name FROM dbo.PayType
    WHERE SUBSTRING(PayTypeCode, 1, 1) IN (N'C', N'R');
    SELECT RTRIM(PayTypeCode) AS Code, RTRIM(PayTypeName) AS Name FROM dbo.PayType
    WHERE SUBSTRING(PayTypeCode, 1, 1) NOT IN (N'C', N'R');
    SELECT RTRIM(ModalityCode) AS Code, RTRIM(ModalityName) AS Name FROM dbo.TreatmentModality;
    SELECT RTRIM(HOSCode) AS Code, RTRIM(HOSName) AS Name FROM dbo.HOS WHERE HOSName IS NOT NULL ORDER BY HOSName;
    SELECT RTRIM(REPCode) AS Code, RTRIM(REPDescription) AS Name FROM genisis.Maint_REPSelection ORDER BY REPCode;
END
GO

-- Everything cmdShow_Click / showMBM / showMBMCov read besides SearchMBMCas.
-- Result sets: plan prompts, annual limit, agent, principal (View_MBM_MBMOthers), principal exclusion,
-- covered persons with their exclusion, server date.
CREATE OR ALTER PROCEDURE genisis.CaseReg_MemberExtras
    @mbm nvarchar(4000), @plan nvarchar(4000), @pay nvarchar(4000), @hlt nvarchar(4000),
    @ins nvarchar(4000), @eff datetime, @agent nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT AnnLmtIND, MessagePromt, PLNStartAge, PLNEndAge, PLNPolicyWording
    FROM dbo.PLN WHERE PLNCode = @plan AND (@hlt = N'S' OR PAYCode = @pay);

    SELECT TOP (1) AnnLimit FROM dbo.AnnualLimit
    WHERE INSCode = @ins AND PLNCode = @plan AND HLTCode = @hlt AND AnnualEffDate <= @eff
    ORDER BY AnnualEffDate DESC;

    SELECT TOP (1) AgentName FROM dbo.Agent WHERE @agent IS NOT NULL AND AgentCode = @agent;

    SELECT TOP (1) MBMName, MBMIcBcPp, MBMMemberType, MBMDOB, MBMTopupInd, PAYChkHosp
    FROM dbo.View_MBM_MBMOthers WHERE MBMNumber = @mbm;

    SELECT TOP (1) MBMExclusion FROM dbo.MBMOthersTwo WHERE MBMNumber = @mbm;

    SELECT c.MBMCCoverID, c.MBMCName, c.MBMCICBCPPNo, c.MBMCRELCode, c.MBMCDOB, c.MBMCAnnualLimit, t.MBMCExclusion
    FROM dbo.MBMCoveredPersons c
    LEFT JOIN dbo.MBMCoveredPersonsTwo t ON t.MBMCNumber = c.MBMCNumber AND t.MBMCCoverID = c.MBMCCoverID
    WHERE c.MBMCNumber = @mbm
    ORDER BY c.MBMCCoverID;

    SELECT GETDATE() AS Today;
END
GO

-- cboPatientList_Click: patient IC / relationship / DOB / plan and the pre-existing, specific illness and
-- waiting period dates (principal from MBM + MBMOthersTwo, covered person from MBMCoveredPersons + Two).
CREATE OR ALTER PROCEDURE genisis.CaseReg_Patient
    @mbm nvarchar(4000), @coverId nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) m.MBMIcBcPp AS PatientIC, CAST(N'P' AS nvarchar(5)) AS Relationship, m.MBMDOB AS DOB,
           CAST(NULL AS nvarchar(20)) AS PlanCode,
           o.MBMPreExistIllDate AS PreExistIllDate, o.MBMSpecificIllDate AS SpecificIllDate,
           o.MBMWaitingPeriodDate AS WaitingPeriodDate, o.MBMBDayStatus AS BDayStatus
    FROM dbo.MBM m LEFT JOIN dbo.MBMOthersTwo o ON o.MBMNumber = m.MBMNumber
    WHERE m.MBMNumber = @mbm AND @coverId = N'00';

    SELECT TOP (1) c.MBMCICBCPPNo AS PatientIC, c.MBMCRELCode AS Relationship, c.MBMCDOB AS DOB,
           c.MBMCPLNCode AS PlanCode,
           t.MBMCPreExistIllDate AS PreExistIllDate, t.MBMCSpecificIllDate AS SpecificIllDate,
           t.MBMCWaitingPeriodDate AS WaitingPeriodDate, t.MBMCBDayStatus AS BDayStatus
    FROM dbo.MBMCoveredPersons c
    LEFT JOIN dbo.MBMCoveredPersonsTwo t ON t.MBMCNumber = c.MBMCNumber AND t.MBMCCoverID = c.MBMCCoverID
    WHERE c.MBMCNumber = @mbm AND c.MBMCCoverID = @coverId;
END
GO

-- BNFListing / NewBNFListing (legacy Case_Registration modes 5 and 20).
CREATE OR ALTER PROCEDURE genisis.CaseReg_Benefits
    @plan nvarchar(4000), @hlt nvarchar(4000), @ins nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT BNFCode, BNFDescription, BNFNoOfDays, BNFClaimAbleAmount, BNFRMPerDis
    FROM dbo.VIEW_BNF WHERE PLNCode = @plan AND HLTCode = @hlt AND INSCode = @ins;
END
GO

-- MBMHISListing (maintenance Case_Registration mode 3): memberships held under the same IC.
CREATE OR ALTER PROCEDURE genisis.CaseReg_MemberHistory
    @ic nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT MBMNumber, MBMPolicyNo, MBMPayorEffDate, MBMPayorExpDate, INSCode, PLNCode
    FROM genisis.Maint_MBMCrossReference WHERE MBMIcBcPp = @ic;
END
GO

-- SearchPrincipalIll (legacy Case_SearchIllness): pre-existing / underlying illness recorded on earlier cases.
CREATE OR ALTER PROCEDURE genisis.CaseReg_NonDisclosure
    @ic nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT MBMNumber, CASNumber, MBMPatientCovID, CASDateAdmitted, CASPreIllDate, CASPreIllCAT, CASPreIllOthers,
           CASPreIllInfBy, CASUndIllDate, CASUndIllCAT, CASUndIllOthers, CASUndIllInfBy
    FROM dbo.VIEW_Cas_SearchIll WHERE MBMIcBcPp = @ic;
END
GO

-- PolTermsListing (legacy Case_Registration mode 6): health type S reads by plan, N by plan and payor.
CREATE OR ALTER PROCEDURE genisis.CaseReg_PolicyTerms
    @plan nvarchar(4000), @pay nvarchar(4000), @hlt nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) PLNRefRqdAdmin, PLNRefRqdPreSpec, PLNMinDurHosp, PLNPhyVstPerDay, PLNSepPeriodPrin,
           PLNSepPeriodSup, PLNLowerAgeLimit, PLNUpperAgeLimit, PLNStudentAge, PLNChildUntilAge
    FROM dbo.PLN
    WHERE PLNCode = @plan AND (@hlt = N'S' OR (@hlt = N'N' AND PAYCode = @pay));
END
GO

-- ScheduleSurg: surgical schedule for the payor / plan.
CREATE OR ALTER PROCEDURE genisis.CaseReg_SurgicalSchedule
    @pay nvarchar(4000), @plan nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT PAYCode, PLNCode, SurgMainCat, SurgSubCat, SurgPerc
    FROM dbo.PLNSurgOperation WHERE PAYCode = @pay AND PLNCode = @plan;
END
GO

-- frmSearchICD: exact code and/or description fragments (already LIKE-escaped by the caller).
CREATE OR ALTER PROCEDURE genisis.CaseReg_IcdSearch
    @max int, @code nvarchar(4000) = NULL, @desc nvarchar(4000) = NULL, @desc2 nvarchar(4000) = NULL, @matchAny bit = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(ICDCode) AS ICDCode, RTRIM(ICDDescription) AS ICDDescription
    FROM dbo.ICD
    WHERE (@code IS NULL OR ICDCode = @code)
      AND ((@desc IS NULL AND @desc2 IS NULL)
           OR (@matchAny = 1 AND (ICDDescription LIKE N'%' + @desc + N'%' OR ICDDescription LIKE N'%' + @desc2 + N'%'))
           OR (@matchAny = 0 AND (@desc IS NULL OR ICDDescription LIKE N'%' + @desc + N'%')
                             AND (@desc2 IS NULL OR ICDDescription LIKE N'%' + @desc2 + N'%')))
    ORDER BY ICDCode
    OPTION (RECOMPILE);
END
GO

-- FrmSearchRepudiation: code prefix and/or description fragments (already LIKE-escaped by the caller).
CREATE OR ALTER PROCEDURE genisis.CaseReg_RepCodeSearch
    @max int, @code nvarchar(4000) = NULL, @desc nvarchar(4000) = NULL, @desc2 nvarchar(4000) = NULL, @matchAny bit = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(RepCode) AS RepCode, RTRIM(RepDescription) AS RepDescription, RepNonDisc
    FROM dbo.RepCode
    WHERE (@code IS NULL OR RepCode LIKE @code + N'%')
      AND ((@desc IS NULL AND @desc2 IS NULL)
           OR (@matchAny = 1 AND (RepDescription LIKE N'%' + @desc + N'%' OR RepDescription LIKE N'%' + @desc2 + N'%'))
           OR (@matchAny = 0 AND (@desc IS NULL OR RepDescription LIKE N'%' + @desc + N'%')
                             AND (@desc2 IS NULL OR RepDescription LIKE N'%' + @desc2 + N'%')))
    ORDER BY RepCode
    OPTION (RECOMPILE);
END
GO

-- SearchMMARate: surgeon / anaesthetist MMA fees for an OPCS code (maintenance VIEW_MMA_Schedule).
CREATE OR ALTER PROCEDURE genisis.CaseReg_MmaRate
    @code nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) SurgeonsFees, AnaesthetistsFees FROM genisis.Maint_MMASchedule WHERE OPCSCode = @code;
END
GO

-- frmSearchCPT / frmSearchInv: OPCS code list (maintenance MMAProcedures). Code is a prefix match and the
-- narrative a contains match, like the desktop's strCriteria. Investigations are ProcPart 'A'.
CREATE OR ALTER PROCEDURE genisis.CaseReg_OpcsSearch
    @max int, @code nvarchar(4000) = NULL, @desc nvarchar(4000) = NULL, @part nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(OPCSCode) AS OPCSCode, RTRIM(OPCSNarrative) AS OPCSNarrative
    FROM genisis.Maint_MMAProcedures
    WHERE OPCSCode IS NOT NULL
      AND (@code IS NULL OR OPCSCode LIKE @code + N'%')
      AND (@desc IS NULL OR OPCSNarrative LIKE N'%' + @desc + N'%')
      AND (@part IS NULL OR ProcPart = @part)
    ORDER BY OPCSCode
    OPTION (RECOMPILE);
END
GO

-- txtHOSCode_LostFocus panel check: hospital status, payor check flag, and whether the hospital is on the
-- payor's panel. AX uses the panel code chosen by policy number exactly as the desktop does.
CREATE OR ALTER PROCEDURE genisis.CaseReg_HospitalCheck
    @hosCode nvarchar(4000), @pay nvarchar(4000), @policyNo nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @panelCode nvarchar(100) = CASE
        WHEN @policyNo = N'03215666' THEN N'AX - AXA AFFIN STAFF (NON-EXECUTIVE)'
        WHEN @policyNo IN (N'03215814', N'03215886') THEN N'AX - SPNB'
        WHEN @policyNo = N'03312049' THEN N'AX - EMBASSY OF LIBYA (STAFF&MT)'
        ELSE N'AX - AXA AFFIN' END;
    SELECT RTRIM(h.HOSName) AS HOSName, h.HOSStatus,
           (SELECT TOP (1) PAYChkHosp FROM dbo.PAY WHERE PAYCode = @pay) AS PAYChkHosp,
           CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.PanelHospitals p
                                  WHERE p.HOSCode = @hosCode AND p.PayCode = @pay
                                    AND (@pay <> N'AX' OR p.HosPanelCode = @panelCode))
                THEN 1 ELSE 0 END AS bit) AS OnPanel
    FROM dbo.HOS h WHERE h.HOSCode = @hosCode;
END
GO

-- NewCaseAddmission pre-save checks (maintenance Case_Registration modes 1 and 2):
-- policy not yet active, activated within 30 days, and an existing case for the patient on the same admission date.
CREATE OR ALTER PROCEDURE genisis.CaseReg_PreChecks
    @mbm nvarchar(4000), @coverId nvarchar(4000), @doa date
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        CAST(CASE WHEN EXISTS (SELECT 1 FROM genisis.Maint_MBMCrossReference
                               WHERE MBMNumber = @mbm AND MBMPayorEffDate >= GETDATE()) THEN 1 ELSE 0 END AS bit) AS NotActivated,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM genisis.Maint_MBMCrossReference
                               WHERE MBMNumber = @mbm AND MBMPayorEffDate >= DATEADD(day, -30, GETDATE())
                                 AND MBMPayorEffDate <= GETDATE()) THEN 1 ELSE 0 END AS bit) AS RecentlyActivated,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM genisis.Maint_CASCrossReference
                               WHERE MBMNumber = @mbm AND MBMPatientCovID = @coverId
                                 AND CAST(CASDateAdmitted AS date) = @doa) THEN 1 ELSE 0 END AS bit) AS SameDayAdmission;
END
GO

-- GenerateCaseNo: next number from the maintenance CAQ2013 counter for the payor (payor code + 8 digits).
-- Call inside the registration transaction so the UPDLOCK/HOLDLOCK read lock lasts until commit.
CREATE OR ALTER PROCEDURE genisis.CaseReg_NextCaseNo
    @payor nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @code int, @n bigint;
    SELECT TOP (1) @code = CAQCode, @n = TRY_CAST(CAQNumber AS bigint)
    FROM genisis.Maint_CAQ2013 WITH (UPDLOCK, HOLDLOCK) WHERE PAYCode = @payor ORDER BY CAQCode;

    DECLARE @text nvarchar(20);
    IF @code IS NULL
    BEGIN
        SET @text = N'00000001';
        INSERT INTO genisis.Maint_CAQ2013 (PAYCode, CAQNumber) VALUES (@payor, @text);
    END
    ELSE
    BEGIN
        SET @n = ISNULL(@n, 0) + 1;
        SET @text = CAST(@n AS nvarchar(20));
        IF LEN(@text) < 8 SET @text = REPLICATE(N'0', 8 - LEN(@text)) + @text;
        UPDATE genisis.Maint_CAQ2013 SET CAQNumber = @text WHERE CAQCode = @code;
    END
    SELECT @payor + @text AS CaseNumber;
END
GO

-- UpdateBalLmt: reduce the member's available limit by the reduction amount, never below zero.
CREATE OR ALTER PROCEDURE genisis.CaseReg_ReduceLimit
    @mbm nvarchar(4000), @amount money
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.MBM
    SET MBMAvailableLimit = CASE WHEN MBMAvailableLimit > @amount THEN MBMAvailableLimit - @amount ELSE 0 END
    WHERE MBMNumber = @mbm;
END
GO

-- cmdLG_Click + funcword: the saved case with the member / plan / covered-person fields the LG balance
-- check, the CASLG insert and the letters read. Parameterized subset of SearchCasMBMClaim mode 3.
CREATE OR ALTER PROCEDURE genisis.CaseReg_LGContext
    @cas nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1)
        c.CASNumber, c.MBMNumber, c.MBMPatientCovID, c.CASDateAdmitted, c.CASDischargeDate,
        c.CASTopupLmt, c.CASHOSCode, RTRIM(h.HOSName) AS HOSName, c.CASClaimability,
        m.MBMPolicyNo, RTRIM(m.MBMName) AS MBMName, m.MBMIcBcPp, m.INSCode, m.PAYCode, m.HLTCode,
        m.PLNCode, m.MBMPayorEffDate, m.MBMPayorExpDate, m.MBMTopupInd, m.MBMTopupNumber, m.MBMPolEffDate,
        CASE WHEN c.MBMPatientCovID = '00' THEN m.MBMMemberType ELSE cp.MBMCMemberType END AS MBMMemberType,
        RTRIM(COALESCE(cp.MBMCName, m.MBMName)) AS PatientName, cp.MBMCICBCPPNo AS PatientIC, cp.MBMCPLNCode,
        mo.MBMGroupCompany,
        p.AnnLmtIND, p.PLNLifeTimeStatus, p.PLNSOF, p.PLNBTBG, p.PLNPerVisit, p.PLNRBType, p.PLNLGType,
        p.CoPaymentInd, p.PLNMeal, p.PLNNursing, p.PLNTax, p.PLNMRI
    FROM dbo.CAS c
    INNER JOIN dbo.MBM m ON m.MBMNumber = c.MBMNumber
    LEFT JOIN dbo.MBMCoveredPersons cp
        ON cp.MBMCNumber = c.MBMNumber AND cp.MBMCCoverID = c.MBMPatientCovID
    LEFT JOIN dbo.MBMOthers mo ON mo.MBMNumber = m.MBMNumber
    LEFT JOIN dbo.HOS h ON h.HOSCode = c.CASHOSCode
    LEFT JOIN dbo.PLN p
        ON p.PAYCode = m.PAYCode AND p.HLTCode = m.HLTCode AND p.PLNCode = m.PLNCode
    WHERE c.CASNumber = @cas;
END
GO

-- CheckLimit's AnnualLimit row: the limit effective at the admission date for the plan / insured type.
CREATE OR ALTER PROCEDURE genisis.CaseReg_LGAnnualLimit
    @plan nvarchar(50), @ins nvarchar(50), @doa datetime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) DisabilityLmt, SuppLimitStatus, AnnLimit, SuppLimit, LifeTimeLimit, PLNCode
    FROM dbo.AnnualLimit
    WHERE PLNCode = @plan AND INSCode = @ins AND AnnualEffDate <= @doa
    ORDER BY AnnualEffDate DESC;
END
GO

-- CheckLimit's top-up reads: the cross reference row for the top-up membership, the top-up member itself
-- and the lifetime-guarantee renewal flag (maintenance + main tables the desktop queries separately).
CREATE OR ALTER PROCEDURE genisis.CaseReg_TopupContext
    @topup nvarchar(30), @cov nvarchar(10)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) MBMNumber, INSCode, PLNCode, MBMPayorEffDate, MBMPayorExpDate, MBMPolicyNo, MBMTopupNumber
    FROM genisis.Maint_MBMCrossReference WHERE MBMTopupNumber = @topup;

    SELECT TOP (1) MBMNumber, INSCode, PLNCode, MBMPayorEffDate, MBMPayorExpDate, MBMPolicyNo,
           MBMAvailableLimit, MBMTopupNumber
    FROM dbo.MBM WHERE MBMNumber = @topup;

    SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.MBMLifeTime WHERE MBMNumber = @topup AND MBMCovID = @cov)
                     THEN 1 ELSE 0 END AS bit) AS LTGuaranteedRenewal;
END
GO

-- funcword's bookmark lookups: the payor letterhead, the hospital contact and the plan's co-pay / MRI
-- flags plus city / state names (Case_Registration modes 15, 16 and 17).
CREATE OR ALTER PROCEDURE genisis.CaseReg_LetterContext
    @pay nvarchar(10), @hosName nvarchar(200), @plan nvarchar(50), @hlt nvarchar(10),
    @cty nvarchar(20) = NULL, @sta nvarchar(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(PAYCompanyName) AS PAYCompanyName, PAYAddress1, PAYAddress2, PAYAddress3,
           PAYMainContact, PAYAdmissionContact
    FROM dbo.PAY WHERE PAYCode = @pay;

    SELECT TOP (1) HOSContact, HOSFaxNo FROM dbo.HOS WHERE HOSName = @hosName;

    SELECT TOP (1) CoPaymentInd, PLNMeal, PLNNursing, PLNTax, PLNMRI
    FROM dbo.PLN WHERE PLNCode = @plan AND (@hlt = N'S' OR PAYCode = @pay);

    SELECT TOP (1) RTRIM(CTYDescription) AS City FROM genisis.Maint_CTY WHERE CTYCode = @cty;
    SELECT TOP (1) RTRIM(STADescription) AS [State] FROM dbo.STA WHERE STACode = @sta;
END
GO

-- Repudiation letter detail: the reason description plus its category name and text
-- (Case_Registration modes 18 and 19).
CREATE OR ALTER PROCEDURE genisis.CaseReg_RepLetterDetail
    @code nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(r.RepDescription) AS RepDescription,
           RTRIM(rc.RepCatName) AS RepCatName, RTRIM(rc.REPCatDescription) AS REPCatDescription
    FROM dbo.RepCode r
    LEFT JOIN dbo.RepCodeCategory rc ON rc.REPCatCode = r.REPCatCode
    WHERE r.RepCode = @code;
END
GO

-- ===== main/150_case_adjustment.sql =====
-- Case > Adjustment (legacy frmCaseAdjustmentNew / frmGLMenu).
-- The case itself is read with the existing parameterized dbo.SearchCasAdjustment; these procedures replace
-- the raw @strAppend modes of dbo.Case_Adjustment the form used for everything else.

-- cmdPrev_Click / CmdNext_Click: the neighbouring case number on the maintenance cross reference.
CREATE OR ALTER PROCEDURE genisis.CaseAdj_Navigate
    @cas nvarchar(15), @next bit
AS
BEGIN
    SET NOCOUNT ON;
    IF @next = 1
        SELECT TOP (1) RTRIM(CASNumber) FROM genisis.Maint_CASCrossReference WHERE CASNumber > @cas ORDER BY CASNumber;
    ELSE
        SELECT TOP (1) RTRIM(CASNumber) FROM genisis.Maint_CASCrossReference WHERE CASNumber < @cas ORDER BY CASNumber DESC;
END
GO

-- showCAS side reads: ShowFirstDiag, ShowFinalDiag, ShowRepudiation (codes with their descriptions),
-- the pending reason (tblRecord.DocRemark), the plan's policy wording and whether the case has a GL.
CREATE OR ALTER PROCEDURE genisis.CaseAdj_Extras
    @cas nvarchar(15), @plan nvarchar(20), @pay nvarchar(10), @hlt nvarchar(10)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(ICDCode1) AS ICDCode1, RTRIM(ICDCode2) AS ICDCode2, RTRIM(ICDCode3) AS ICDCode3,
        DIAPrelimDiag1, DIAPrelimDiag2, DIAPrelimDiag3, DIADOS1, DIADOS2, DIADOS3
    FROM dbo.DIAFirst WHERE CASNumber = @cas;

    SELECT RTRIM(ICDCode1) AS ICDCode1, RTRIM(ICDCode2) AS ICDCode2, RTRIM(ICDCode3) AS ICDCode3,
        DIAFinalDiag1, DIAFinalDiag2, DIAFinalDiag3, DIADOS1, DIADOS2, DIADOS3
    FROM dbo.DIAFinal WHERE CASNumber = @cas;

    SELECT RTRIM(r.CASREPCode1) + ISNULL(N' - ' + RTRIM(c1.RepDescription), N'') AS Rep1,
        RTRIM(r.CASREPCode2) + ISNULL(N' - ' + RTRIM(c2.RepDescription), N'') AS Rep2,
        RTRIM(r.CASREPCode3) + ISNULL(N' - ' + RTRIM(c3.RepDescription), N'') AS Rep3,
        RTRIM(r.CASDocument) AS CASDocument, RTRIM(r.CASPersonnel) AS CASPersonnel,
        RTRIM(r.CASAttention) AS CASAttention, RTRIM(r.CASUnderlying) AS CASUnderlying
    FROM dbo.CASRepudiation r
    LEFT JOIN dbo.REPCode c1 ON c1.RepCode = NULLIF(RTRIM(r.CASREPCode1), N'')
    LEFT JOIN dbo.REPCode c2 ON c2.RepCode = NULLIF(RTRIM(r.CASREPCode2), N'')
    LEFT JOIN dbo.REPCode c3 ON c3.RepCode = NULLIF(RTRIM(r.CASREPCode3), N'')
    WHERE r.CASNumber = @cas;

    SELECT TOP (1) DocRemark FROM genisis.Maint_tblRecord WHERE CaseNo = @cas ORDER BY RecId DESC;

    SELECT TOP (1) RTRIM(PLNPolicyWording) FROM dbo.PLN
    WHERE PLNCode = @plan AND (@hlt = N'S' OR PAYCode = @pay);

    SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM genisis.Maint_CASLG WHERE CASENo = @cas) THEN 1 ELSE 0 END AS bit);
END
GO

-- cmdUpdate1_Click: which child rows already exist (insert vs update), the values the update reuses from CAS
-- and the balance-limit inputs for UpdateBalLmt. The CAS row is locked for the rest of the transaction.
CREATE OR ALTER PROCEDURE genisis.CaseAdj_State
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(c.MBMNumber) AS MBMNumber, RTRIM(c.MBMPatientCovID) AS MBMPatientCovID, RTRIM(c.CASStatus) AS CASStatus,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.CASRemarks WHERE CASNumber = @cas) THEN 1 ELSE 0 END AS bit) AS HasRemark,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.CASRemarksTwo WHERE CASNumber = @cas) THEN 1 ELSE 0 END AS bit) AS HasRemark2,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.CASRemarkThree WHERE CASNumber = @cas) THEN 1 ELSE 0 END AS bit) AS HasRemark3,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM genisis.Maint_CASCrossReference WHERE CASNumber = @cas) THEN 1 ELSE 0 END AS bit) AS HasXref,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.DIAFirst WHERE CASNumber = @cas) THEN 1 ELSE 0 END AS bit) AS HasDiaFirst,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.DIAFinal WHERE CASNumber = @cas) THEN 1 ELSE 0 END AS bit) AS HasDiaFinal,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.CAS_UD WHERE CASNumber = @cas) THEN 1 ELSE 0 END AS bit) AS HasUD,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.CASOthers WHERE CASNumber = @cas) THEN 1 ELSE 0 END AS bit) AS HasOthers,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.CASOthersTwo WHERE CASNumber = @cas) THEN 1 ELSE 0 END AS bit) AS HasOthers2,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.CASRepudiation WHERE CASNumber = @cas) THEN 1 ELSE 0 END AS bit) AS HasRep,
        ISNULL((SELECT TOP (1) CASRedAmt FROM dbo.CASOthers WHERE CASNumber = @cas), 0) AS PrevRedAmt,
        (SELECT TOP (1) MBMAvailableLimit FROM dbo.MBM WHERE MBMNumber = c.MBMNumber) AS AvailableLimit
    FROM dbo.CAS c WITH (UPDLOCK, ROWLOCK)
    WHERE c.CASNumber = @cas;
END
GO

-- The LG tab (LGListing over View_GL_Listing on the maintenance database).
CREATE OR ALTER PROCEDURE genisis.CaseAdj_LGList
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(CASENo) AS CaseNo, LGNo, LGDate, LGAmt, RTRIM(LGIssuedBy) AS LGIssuedBy, RTRIM(LGRemark) AS LGRemark,
        RTRIM(LGStatus) AS LGStatus, RTRIM(LGUserTime) AS LGUserTime, RTRIM(LGSystemTime) AS LGSystemTime,
        RTRIM(LGPurpose) AS LGPurpose, RTRIM(LGIndicator) AS LGIndicator
    FROM genisis.Maint_CASLG WHERE CASENo = @cas ORDER BY LGNo;
END
GO

-- cmdLG_Click / frmGLMenu inputs: the earlier GLs (purpose rules, same-purpose re-issue, open GL) and the
-- request date / time recorded on tblRecord.
CREATE OR ALTER PROCEDURE genisis.CaseAdj_GLContext
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT LGNo, RTRIM(LGPurpose) AS LGPurpose, RTRIM(LGIndicator) AS LGIndicator
    FROM genisis.Maint_CASLG WHERE CASENo = @cas ORDER BY LGNo;

    SELECT TOP (1) CaseDate, RTRIM(CaseTime) AS CaseTime FROM genisis.Maint_tblRecord WHERE CaseNo = @cas ORDER BY RecId DESC;
END
GO

-- The case progress flags the desktop sets on tblRecord (NULL leaves a flag unchanged).
CREATE OR ALTER PROCEDURE genisis.CaseAdj_SetProgress
    @cas nvarchar(15), @isRejected char(1) = NULL, @isLGIssued char(1) = NULL, @progress nvarchar(100) = NULL,
    @docRemark nvarchar(200) = NULL, @setDocRemark bit = 0
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.Maint_tblRecord
    SET IsRejected = ISNULL(@isRejected, IsRejected),
        IsLGissued = ISNULL(@isLGIssued, IsLGissued),
        CaseProgress = ISNULL(@progress, CaseProgress),
        DocRemark = CASE WHEN @setDocRemark = 1 THEN @docRemark ELSE DocRemark END
    WHERE CaseNo = @cas;
END
GO

-- cmdDocReqAdm_Click ("Delete" on the LG tab): remove one GL of the case.
CREATE OR ALTER PROCEDURE genisis.CaseAdj_DeleteLG
    @cas nvarchar(15), @lgNo int
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM genisis.Maint_CASLG WHERE CASENo = @cas AND LGNo = @lgNo;
    SELECT @@ROWCOUNT;
END
GO

-- cmdLG_Click: close the open GL when a GL for a different purpose is issued.
CREATE OR ALTER PROCEDURE genisis.CaseAdj_CloseLG
    @cas nvarchar(15), @lgNo int
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.Maint_CASLG SET LGIndicator = 'C' WHERE CASENo = @cas AND LGNo = @lgNo;
END
GO

-- GetLGCode: the next LG sequence of the case (LGSeq), locked until the caller's transaction ends.
CREATE OR ALTER PROCEDURE genisis.CaseAdj_NextLGSeq
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM genisis.Maint_LGSeq WITH (UPDLOCK, HOLDLOCK) WHERE CASNumber = @cas)
        UPDATE genisis.Maint_LGSeq SET LGSequence = LGSequence + 1 WHERE CASNumber = @cas;
    ELSE
        INSERT INTO genisis.Maint_LGSeq (CASNumber, LGSequence) VALUES (@cas, 1);
    SELECT CAST(LGSequence AS int) FROM genisis.Maint_LGSeq WHERE CASNumber = @cas;
END
GO

-- Case_Adjustment mode 19 + mode 3: the plan's LG settings and whether the member's plan is back-to-back.
CREATE OR ALTER PROCEDURE genisis.CaseAdj_GLPlan
    @mbm nvarchar(15), @plan nvarchar(20), @pay nvarchar(10)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(PLNPerVisit) AS PLNPerVisit, RTRIM(PLNRBType) AS PLNRBType, RTRIM(PLNLGType) AS PLNLGType,
        RTRIM(PLNLGPrivate) AS PLNLGPrivate, RTRIM(PLNLGGov) AS PLNLGGov
    FROM dbo.PLN WHERE PLNCode = @plan AND (@pay = N'ZN' OR PAYCode = @pay);

    SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.MBM m INNER JOIN dbo.PLN p ON p.PLNCode = m.PLNCode
        WHERE m.MBMNumber = @mbm AND p.PLNBTBG = 'Y') THEN 1 ELSE 0 END AS bit);
END
GO

-- frmCASList (Source = intCaseAdjustment): the case pick-list over View_Case_Search.
CREATE OR ALTER PROCEDURE genisis.CaseAdj_CaseSearch
    @by nvarchar(10), @q nvarchar(100), @plan nvarchar(20) = NULL, @max int = 200
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(CASNumber) AS CASNumber, RTRIM(MBMNumber) AS MBMNumber,
        RTRIM(MBMPatientCovID) AS MBMPatientCovID,
        CASE WHEN RTRIM(MBMPatientCovID) = '00' THEN RTRIM(MBMName) ELSE RTRIM(MBMCName) END AS PatientName,
        RTRIM(MBMPolicyNo) AS MBMPolicyNo, CASDateAdmitted, CASDischargeDate,
        RTRIM(CASStatus) AS CASStatus, RTRIM(CASHOSCode) AS CASHOSCode, CASReserveAmount,
        RTRIM(CASClaimability) AS CASClaimability
    FROM dbo.View_Case_Search
    WHERE MBMName IS NOT NULL AND (@plan IS NULL OR PLNCode = @plan) AND
        (@by = 'case' AND CASNumber LIKE @q + '%'
        OR @by = 'member' AND (MBMNumber LIKE @q + '%' OR MBMCNumber LIKE @q + '%')
        OR @by = 'policy' AND MBMPolicyNo = @q
        OR @by = 'name' AND MBMName LIKE '%' + @q + '%'
        OR @by = 'ic' AND MBMIcBcPp = @q
        OR @by = 'employee' AND MBMEmployeeNo LIKE @q + '%'
        OR @by = 'polsub' AND MBMPOLSubNo1 LIKE @q + '%')
    ORDER BY CASNumber DESC;
END
GO

-- Form_Load's cboPendingReason fill (SearchPendingReason on the maintenance database).
CREATE OR ALTER PROCEDURE genisis.CaseAdj_PendingReasons
AS
BEGIN
    SET NOCOUNT ON;
    EXEC genisis.Maint_SearchPendingReason;
END
GO

-- MBMHistory("Princ", True): the member's policies by IC for the case management note template.
CREATE OR ALTER PROCEDURE genisis.CaseAdj_PolicyHistory
    @ic nvarchar(30)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT RTRIM(MBMNumber) AS MBMNumber, RTRIM(MBMPolicyNo) AS MBMPolicyNo, MBMPayorEffDate, MBMPayorExpDate
    FROM genisis.Maint_MBMCrossReference
    WHERE MBMIcBcPp = @ic AND MBMStatus <> 'D'
    ORDER BY MBMPayorEffDate;
END
GO

PRINT N'Done.';
GO
SET NOEXEC OFF;
GO
