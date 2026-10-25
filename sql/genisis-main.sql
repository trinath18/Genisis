-- GENERATED FILE: built from sql/main/*.sql. Edit those files, then rebuild this one with
--     GENISIS_WRITE_SQL=1 dotnet test --filter Combined_scripts_are_up_to_date
-- How to run: open this file in SSMS, pick the main database in the "Available Databases" box,
-- check @MaintenanceDb in the first section, then press F5 (Execute).
-- Safe to run again: it only creates or replaces genisis.* procedures and synonyms; tables and data are not changed.

-- ===== main/000_setup.sql =====
-- Setup for the Genesis stored procedures. Run on the main database (the one that has dbo.PLN and dbo.MBM).
-- Creates the genisis schema and the synonyms the procedures use to read the maintenance database.
-- If your maintenance, clinical, worksheet, payment or archive database has another name, change @MaintenanceDb / @CisDb / @WorksheetDb /
-- @PaymentDb / @ArchiveDb below before running.
SET NOEXEC OFF;
SET NOCOUNT ON;
DECLARE @MaintenanceDb sysname = N'HISMaintenance';
DECLARE @CisDb sysname = N'CISDB';
DECLARE @WorksheetDb sysname = N'HISWorkSheet';
-- The clinical (CIS) and payment databases are optional: the screens that read them need them, their synonyms are skipped when absent.
DECLARE @PaymentDb sysname = N'HISPayment';
-- The archive database (the desktop's mediconnectA) is optional too: only Membership > Deletion writes to it.
-- It is HISDMArchive on one server and HISDBArchive on another: the first that exists is used.
DECLARE @ArchiveDb sysname = CASE WHEN DB_ID(N'HISDMArchive') IS NOT NULL THEN N'HISDMArchive' ELSE N'HISDBArchive' END;

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
FROM (VALUES
    (N'Maint_ProductCategory', N'ProductCategory'), (N'Maint_PolicyCategory', N'PolicyCategory'), (N'Maint_MBMCrossReference', N'MBMCrossReference'),
    (N'Maint_MBMSEQ05', N'MBMSEQ05'), (N'Maint_MBMSEQRenew05', N'MBMSEQRenew05'), (N'Maint_MBMSEQRenewPol', N'MBMSEQRenewPol'),
    (N'Maint_MBMSEQRenewHistory', N'MBMSEQRenewHistory'), (N'Maint_CTY', N'CTY'), (N'Maint_SuppMCOStatus', N'SuppMCOStatus'),
    (N'Maint_MBMSEQ', N'MBMSEQ'), (N'Maint_CaseMgmtFee', N'CaseMgmtFee'), (N'Maint_MMAProcedures', N'MMAProcedures'),
    (N'Maint_MMAFees', N'MMAFees'), (N'Maint_MMASchedule', N'VIEW_MMA_Schedule'), (N'Maint_DiscPlan', N'DiscPlan'),
    (N'Maint_MCOPending', N'MCOPending'), (N'Maint_CASCrossReference', N'CASCrossReference'), (N'Maint_Renewal', N'Renewal'),
    (N'Maint_CAQ2013', N'CAQ2013'), (N'Maint_SpecApprove', N'SpecApprove'), (N'Maint_REPSelection', N'REPSelection'),
    (N'Maint_Case_RegInsertCASRef', N'Case_RegInsertCASRef'), (N'Maint_Case_RegInsertCASRef1', N'Case_RegInsertCASRef1'), (N'Maint_CASLG', N'CASLG'),
    (N'Maint_LGSeq', N'LGSeq'), (N'Maint_tblRecord', N'tblRecord'), (N'Maint_Case_AdjInsertCASRef', N'Case_AdjInsertCASRef'),
    (N'Maint_Case_AdjUpdateCASRef', N'Case_AdjUpdateCASRef'), (N'Maint_Case_AdjInsertCASLG_2011', N'Case_AdjInsertCASLG_2011'), (N'Maint_Case_AdjUpdateCASLG', N'Case_AdjUpdateCASLG'),
    (N'Maint_SearchPendingReason', N'SearchPendingReason'), (N'Maint_HosPanelCode', N'HosPanelCode'), (N'Maint_BankAccounts', N'BankAccounts'), (N'Maint_InvRecMode', N'InvRecMode'), (N'Maint_HOSRoomType', N'HOSRoomType'),
    (N'Maint_USR', N'USR'), (N'Maint_GRP', N'GRP'), (N'Maint_AccPeriod', N'AccPeriod'), (N'Maint_TimeMain', N'TimeMain')) v (s, t);
SELECT @sql += N'IF OBJECT_ID(N''genisis.' + s + N''', N''SN'') IS NOT NULL DROP SYNONYM genisis.' + s + N';
CREATE SYNONYM genisis.' + s + N' FOR ' + QUOTENAME(@CisDb) + N'.dbo.' + t + N';
'
FROM (VALUES (N'Cis_PLN', N'PLN'), (N'Cis_AnnualLimit', N'AnnualLimit'), (N'Cis_PRM', N'PRM'), (N'Cis_MCO', N'MCO'),
    (N'Cis_VisitLimit', N'VisitLimit'), (N'Cis_CISCas', N'CISCas')) v (s, t)
WHERE DB_ID(@CisDb) IS NOT NULL AND OBJECT_ID(QUOTENAME(@CisDb) + N'.dbo.CISCas', N'U') IS NOT NULL;
SELECT @sql += N'IF OBJECT_ID(N''genisis.' + s + N''', N''SN'') IS NOT NULL DROP SYNONYM genisis.' + s + N';
CREATE SYNONYM genisis.' + s + N' FOR ' + QUOTENAME(@ArchiveDb) + N'.dbo.' + t + N';
'
FROM (VALUES (N'Arch_MBM', N'MBMArchive'), (N'Arch_MBMOthers', N'MBMOthersArchive'), (N'Arch_MBMCoveredPersons', N'MBMCoveredPersonsArchive'), (N'Arch_USRArchive', N'USRArchive')) v (s, t)
WHERE DB_ID(@ArchiveDb) IS NOT NULL AND OBJECT_ID(QUOTENAME(@ArchiveDb) + N'.dbo.MBMArchive', N'U') IS NOT NULL;
SELECT @sql += N'IF OBJECT_ID(N''genisis.WS_AccountHistory'', N''SN'') IS NOT NULL DROP SYNONYM genisis.WS_AccountHistory;
CREATE SYNONYM genisis.WS_AccountHistory FOR ' + QUOTENAME(@WorksheetDb) + N'.dbo.VIEW_Account_History;
';
EXEC (@sql);

SET @sql = N'';
SELECT @sql += N'IF OBJECT_ID(N''genisis.' + s + N''', N''SN'') IS NOT NULL DROP SYNONYM genisis.' + s + N';
CREATE SYNONYM genisis.' + s + N' FOR ' + QUOTENAME(d) + N'.dbo.' + t + N';
'
FROM (VALUES (N'WS_Claim', @WorksheetDb, N'Claim'), (N'WS_ClaimInvoice', @WorksheetDb, N'ClaimInvoice'),
    (N'WS_ClaimApprove', @WorksheetDb, N'ClaimApprove'), (N'WS_Bordx', @WorksheetDb, N'Bordx'),
    (N'WS_WKSTracking', @WorksheetDb, N'VIEW_WKSTrackingNew'), (N'WS_WKS2AC', @WorksheetDb, N'VIEW_WKS2AC'),
    (N'WS_ClaimActual', @WorksheetDb, N'ClaimActual'), (N'WS_ClaimHos', @WorksheetDb, N'ClaimHos'), (N'WS_ClaimMember', @WorksheetDb, N'ClaimMember'),
    (N'WS_DRSeq', @WorksheetDb, N'DRSeq'), (N'WS_ExcessLetter', @WorksheetDb, N'CAS_Excess'),
    (N'WS_ClaimLimit', @WorksheetDb, N'ClaimLimit'), (N'WS_ClaimOthers', @WorksheetDb, N'ClaimOthers'), (N'WS_CASInvoice', @WorksheetDb, N'View_CAS_Invoice'),
    (N'WS_BordxSEQ', @WorksheetDb, N'Claim_BordxSEQ2013'), (N'WS_CLMAsciiSEQ', @WorksheetDb, N'CLMAsciiSEQ'),
    (N'WS_PendingBordx', @WorksheetDb, N'VIEW_WS_PendingBordx2'), (N'WS_PendingBordxIB', @WorksheetDb, N'VIEW_WS_PendingBordxIB'),
    (N'WS_CLMReport', @WorksheetDb, N'View_CLMReport5_New'), (N'WS_CLMReportCas', @WorksheetDb, N'VIEW_CLMReportCasNew'),
    (N'WS_ClaimInvoiceActual', @WorksheetDb, N'ClaimInvoiceActual'), (N'WS_ClaimInvoiceActualGST', @WorksheetDb, N'ClaimInvoiceActualGST'),
    (N'WS_ClaimInvoiceActualOthers', @WorksheetDb, N'ClaimInvoiceActualOthers'), (N'WS_ClaimInvoiceNotCover', @WorksheetDb, N'ClaimInvoiceNotCover'),
    (N'WS_ClaimInvoiceRemarks', @WorksheetDb, N'ClaimInvoiceRemarks'),
    -- Invoice HOS / MBM Accounting and Invoice Tracking: their views and the daily batch counters
    (N'WS_HOSInvoice', @WorksheetDb, N'VIEW_HOSInvoice'), (N'WS_MBMInvoice1', @WorksheetDb, N'VIEW_MBMInvoice1'), (N'WS_InvoiceTracking', @WorksheetDb, N'VIEW_Invoice_Tracking'),
    (N'WS_INVACHSEQ', @WorksheetDb, N'INVACHSEQ'), (N'WS_INVACMSEQ', @WorksheetDb, N'INVACMSEQ'),
    -- Invoice Processing writes through the legacy parameterized procedures of the worksheet database
    (N'WS_Insert_ClaimInvoice_2013', @WorksheetDb, N'Insert_ClaimInvoice_2013'), (N'WS_Insert_ClaimInvoice_WODIR', @WorksheetDb, N'Insert_ClaimInvoice_WODIR'),
    (N'WS_Insert_ClaimInvoice_WOFolDate', @WorksheetDb, N'Insert_ClaimInvoice_WOFolDate'), (N'WS_Insert_ClaimInvoice_WO2013', @WorksheetDb, N'Insert_ClaimInvoice_WO2013'),
    (N'WS_Update_ClaimInvoice2013', @WorksheetDb, N'Update_ClaimInvoice2013'), (N'WS_Update_ClaimInvoice_WODIR2013', @WorksheetDb, N'Update_ClaimInvoice_WODIR2013'),
    (N'WS_Update_ClaimInvoice_WOFolDate2013', @WorksheetDb, N'Update_ClaimInvoice_WOFolDate2013'), (N'WS_Update_ClaimInvoice_WO2013', @WorksheetDb, N'Update_ClaimInvoice_WO2013'),
    (N'WS_Insert_ClaimInvActual2010', @WorksheetDb, N'Insert_ClaimInvActual2010'), (N'WS_Update_ClaimInvActual2010', @WorksheetDb, N'Update_ClaimInvActual2010'),
    (N'WS_Insert_ClaimInvActualGST', @WorksheetDb, N'Insert_ClaimInvActualGST'), (N'WS_Update_ClaimInvActualGST', @WorksheetDb, N'Update_ClaimInvActualGST'),
    (N'WS_Insert_ClaimInvoiceActOthers_New_2019', @WorksheetDb, N'Insert_ClaimInvoiceActOthers_New_2019'), (N'WS_Update_ClaimInvoiceActOthers_New_2019', @WorksheetDb, N'Update_ClaimInvoiceActOthers_New_2019'),
    (N'WS_Insert_ClaimInvoiceNotCov08', @WorksheetDb, N'Insert_ClaimInvoiceNotCov08'), (N'WS_Update_ClaimInvoiceNotCov08', @WorksheetDb, N'Update_ClaimInvoiceNotCov08'),
    -- Worksheet Registration writes the rows of a version through the desktop's own parameterised procedures
    (N'WS_Insert_WS_Claim_2012', @WorksheetDb, N'Insert_WS_Claim_2012'), (N'WS_Insert_WS_ClaimOthers_Jul08', @WorksheetDb, N'Insert_WS_ClaimOthers_Jul08'),
    (N'WS_Insert_WS_ClaimLimit', @WorksheetDb, N'Insert_WS_ClaimLimit'), (N'WS_Insert_WS_ClaimActual', @WorksheetDb, N'Insert_WS_ClaimActual'),
    (N'WS_ExcessSequence', @WorksheetDb, N'EXC_SEQ'), (N'WS_ClaimPayMBMStatus', @WorksheetDb, N'ClaimPayMBMStatus'), (N'WS_ClaimActualGST', @WorksheetDb, N'ClaimActualGST'), (N'WS_Insert_WS_ClaimActualGST', @WorksheetDb, N'Insert_WS_ClaimActualGST'), (N'WS_Insert_WS_ClaimApprove', @WorksheetDb, N'Insert_WS_ClaimApprove'),
    (N'WS_Insert_WS_ClaimHOS', @WorksheetDb, N'Insert_WS_ClaimHOS'), (N'WS_Insert_WS_ClaimMBM', @WorksheetDb, N'Insert_WS_ClaimMBM'),
    (N'WS_SUNPosting', @WorksheetDb, N'SUNPosting'), (N'WS_SunPostBatch', @WorksheetDb, N'SunPostBatch'),
    (N'WS_SunPostingMbmInv', @WorksheetDb, N'VIEW_SUN_Posting_MBMInv'),
    (N'Pay_Payment2MBM', @PaymentDb, N'Payment2MBM'), (N'Pay_PVNSeq2013', @PaymentDb, N'PVNSeq2013'),
    (N'Pay_PVMedicaSeq2013', @PaymentDb, N'PVMedicaSeq2013'), (N'Pay_PVMedicaMXSeq', @PaymentDb, N'PVMedicaMXSeq'),
    (N'Pay_ReceiptSummary', @PaymentDb, N'ReceiptSummary'), (N'Pay_ReceiptfrPAY', @PaymentDb, N'ReceiptfrPAY'), (N'Pay_RecSeq', @PaymentDb, N'RecSeq'),
    (N'Pay_Payment2HOS', @PaymentDb, N'Payment2HOS')) v (s, d, t)
WHERE DB_ID(d) IS NOT NULL;
EXEC (@sql);
PRINT N'genisis schema and synonyms ready (maintenance database: ' + @MaintenanceDb + N', clinical database: ' + CASE WHEN DB_ID(@CisDb) IS NULL THEN N'none' ELSE @CisDb END
    + N', worksheet database: ' + @WorksheetDb + N', archive database: ' + CASE WHEN DB_ID(@ArchiveDb) IS NULL THEN N'none' ELSE @ArchiveDb END + N').';
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
    @batch int = NULL, @coverId nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.MBMAmendHistory (MBMNumber, MBMPolicyNo, MBMIcBcPp, MBMName, MBMExpiryDate, MBMPlanCode, MBMAmendEdtType,
        MBMAEndtBordxDate, MBMAEndtEffDate, MBMAmendUser, MBMAmendDate, MBMAEndtBatchNo, MBMCoverID)
    VALUES (@number, @policy, @ic, @name, @expiry, CASE WHEN @expiry IS NULL THEN NULL ELSE @plan END, @type,
        @endtBordx, @endtEff, @user, @today, @batch, @coverId);
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
    SELECT RTRIM(CASENo) AS CaseNo, CAST(LGNo AS int) AS LGNo, LGDate, LGAmt, RTRIM(LGIssuedBy) AS LGIssuedBy, RTRIM(LGRemark) AS LGRemark,
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
    SELECT CAST(LGNo AS int) AS LGNo, RTRIM(LGPurpose) AS LGPurpose, RTRIM(LGIndicator) AS LGIndicator
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
    OUTER APPLY (SELECT TOP (1) * FROM dbo.MBMRefund x WHERE x.MBMNumber = m.MBMNumber AND x.MBMCovID = '00' AND m.MBMStatus = 'C' ORDER BY x.MIndex DESC) r
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

-- ===== main/160_panel_hospitals.sql =====
-- Case > Panel Hospitals (legacy frmPanelHospital). Replaces the raw @strAppend dbo.Search_PanelHospitals2.

-- PayorComboListing / SearchPayor: the payors of the payor drop-downs (Inpatient Case Listing, Claim ASCII, Case ASCII).
CREATE OR ALTER PROCEDURE genisis.Case_Payors
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name FROM dbo.PAY ORDER BY PAYCode;
END
GO

-- Form_Load: the hospital panel codes (HosPanelCode on the maintenance database).
CREATE OR ALTER PROCEDURE genisis.PanelHospitals_Codes
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(HosPanelCode) AS Code, RTRIM(HosPanelDesc) AS Description
    FROM genisis.Maint_HosPanelCode ORDER BY HosPanelCode;
END
GO

-- PanelListing: the hospitals of one panel code.
CREATE OR ALTER PROCEDURE genisis.PanelHospitals_List
    @code nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(h.HOSCode) AS Code, RTRIM(h.HOSName) AS Name, RTRIM(h.HOSTelNo) AS Tel, RTRIM(h.HOSFaxNo) AS Fax,
        RTRIM(h.HOSAdd1) AS Add1, RTRIM(h.HOSAdd2) AS Add2, RTRIM(h.HOSAdd3) AS Add3,
        RTRIM(h.HOSState) AS State, RTRIM(h.HOSTown) AS Town
    FROM dbo.HOS h
    INNER JOIN dbo.PanelHospitals p ON h.HOSCode = p.HOSCode
    WHERE p.HosPanelCode = @code
    ORDER BY h.HOSName, h.HOSCode;
END
GO

-- ===== main/165_case_non_disclosure.sql =====
-- Case > Non-Disclosure Info (legacy frmCASNonDisclosure): the pre-existing / underlying illness the member did not
-- disclose, stored on dbo.CASOthersTwo.

-- CasDetails: nothing is returned when the case does not exist; HasRecord tells whether CASOthersTwo has a row yet.
CREATE OR ALTER PROCEDURE genisis.NonDisclosure_Get
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(c.CASNumber) AS CaseNumber,
        CAST(CASE WHEN o.CASNumber IS NULL THEN 0 ELSE 1 END AS bit) AS HasRecord,
        RTRIM(o.CASPreIllCAT) AS PreCategory, o.CASPreIllDate AS PreDate,
        RTRIM(o.CASPreIllOthers) AS PreOthers, RTRIM(o.CASPreIllInfBy) AS PreInformedBy,
        RTRIM(o.CASUndIllCAT) AS UndCategory, o.CASUndIllDate AS UndDate,
        RTRIM(o.CASUndIllOthers) AS UndOthers, RTRIM(o.CASUndIllInfBy) AS UndInformedBy
    FROM dbo.CAS c
    LEFT JOIN dbo.CASOthersTwo o ON o.CASNumber = c.CASNumber
    WHERE c.CASNumber = @cas;
END
GO

-- CmdUpdate_Click. The desktop only updated an existing CASOthersTwo row and otherwise reported success without
-- saving; this inserts the row when it is missing. Result: 0 saved, 1 case not found.
CREATE OR ALTER PROCEDURE genisis.NonDisclosure_Save
    @cas nvarchar(15),
    @preCategory nvarchar(4000), @preDate datetime, @preOthers nvarchar(4000), @preInformedBy nvarchar(4000),
    @undCategory nvarchar(4000), @undDate datetime, @undOthers nvarchar(4000), @undInformedBy nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.CAS WHERE CASNumber = @cas)
    BEGIN
        SELECT 1 AS Result;
        RETURN;
    END

    IF EXISTS (SELECT 1 FROM dbo.CASOthersTwo WITH (UPDLOCK, ROWLOCK) WHERE CASNumber = @cas)
        UPDATE dbo.CASOthersTwo
        SET CASPreIllCAT = @preCategory, CASPreIllDate = @preDate, CASPreIllOthers = @preOthers, CASPreIllInfBy = @preInformedBy,
            CASUndIllCAT = @undCategory, CASUndIllDate = @undDate, CASUndIllOthers = @undOthers, CASUndIllInfBy = @undInformedBy
        WHERE CASNumber = @cas;
    ELSE
        INSERT dbo.CASOthersTwo (CASNumber, CASPreIllCAT, CASPreIllDate, CASPreIllOthers, CASPreIllInfBy,
            CASUndIllCAT, CASUndIllDate, CASUndIllOthers, CASUndIllInfBy)
        VALUES (@cas, @preCategory, @preDate, @preOthers, @preInformedBy, @undCategory, @undDate, @undOthers, @undInformedBy);

    SELECT 0 AS Result;
END
GO

-- ===== main/170_case_pay_claim.sql =====
-- Case > Pay & Claim (legacy frmCASPayClaim). Reads replace dbo.Case_CASPayClaim / dbo.Search_CAS_PayClaim; the save
-- replaces dbo.Case_SavePayClaim / dbo.Case_UpdatePayClaim and leaves the AgentName / CasRemarks columns untouched.

-- showMBM: the principal and, for family / group insured types (F, H), the covered persons.
CREATE OR ALTER PROCEDURE genisis.PayClaim_Members
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(m.MBMNumber) AS MemberNo, N'00' AS CoverId, RTRIM(m.MBMName) AS PatientName, RTRIM(m.MBMPolicyNo) AS PolicyNo,
        m.MBMPayorEffDate AS PolicyEffective, m.MBMPayorExpDate AS PolicyExpiry,
        RTRIM(m.MBMAddress1) AS Add1, RTRIM(m.MBMAddress2) AS Add2, RTRIM(m.MBMAddress3) AS Add3,
        RTRIM(m.MBMPostCode) AS PostCode, RTRIM(m.STACode) AS State, RTRIM(p.PAYCompanyName) AS InsuranceCompany, 0 AS Sort
    FROM dbo.CAS c
    INNER JOIN dbo.MBM m ON m.MBMNumber = c.MBMNumber
    LEFT JOIN dbo.PAY p ON p.PAYCode = m.PAYCode
    WHERE c.CASNumber = @cas
    UNION ALL
    SELECT RTRIM(cp.MBMCNumber), RTRIM(cp.MBMCCoverID), RTRIM(cp.MBMCName), RTRIM(m.MBMPolicyNo),
        m.MBMPayorEffDate, m.MBMPayorExpDate, NULL, NULL, NULL, NULL, NULL, NULL, 1
    FROM dbo.CAS c
    INNER JOIN dbo.MBM m ON m.MBMNumber = c.MBMNumber
    INNER JOIN dbo.MBMCoveredPersons cp ON cp.MBMCNumber = m.MBMNumber
    WHERE c.CASNumber = @cas AND RTRIM(m.INSCode) IN (N'F', N'H')
    ORDER BY Sort, CoverId;
END
GO

-- ShowMBMDetails: the case header the second tab shows (the patient is the principal for cover ID 00).
CREATE OR ALTER PROCEDURE genisis.PayClaim_Case
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(c.CASNumber) AS CaseNumber, RTRIM(c.MBMNumber) AS MembershipNo, RTRIM(m.MBMPolicyNo) AS PolicyNo,
        RTRIM(m.MBMName) AS PrincipalName,
        RTRIM(CASE WHEN c.MBMPatientCovID = N'00' THEN m.MBMName ELSE ISNULL(cp.MBMCName, m.MBMName) END) AS PatientName,
        c.CASDateAdmitted AS AdmissionDate, c.CASDischargeDate AS DischargeDate, RTRIM(h.HOSName) AS HospitalName
    FROM dbo.CAS c
    LEFT JOIN dbo.MBM m ON m.MBMNumber = c.MBMNumber
    LEFT JOIN dbo.MBMCoveredPersons cp ON cp.MBMCNumber = c.MBMNumber AND cp.MBMCCoverID = c.MBMPatientCovID
    LEFT JOIN dbo.HOS h ON h.HOSCode = c.CASHOSCode
    WHERE c.CASNumber = @cas;
END
GO

-- showPayClaim: the case's Pay & Claim record (at most one per case).
CREATE OR ALTER PROCEDURE genisis.PayClaim_Get
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(CASNumber) AS CaseNo, CASPayCLMDate AS PayClaimDate,
        RTRIM(CASLab) AS LabTest, RTRIM(CASConfInfo) AS ConflictingInfo, RTRIM(CASInfoInv) AS Investigation,
        RTRIM(CASCovgSub) AS Radiology, RTRIM(CASNonPan) AS NonPanel, RTRIM(CASNonCash) AS NoCashless,
        RTRIM(CASCOthers) AS Others, RTRIM(CASOthers) AS Remarks
    FROM dbo.CASPayClaim WHERE CASNumber = @cas;
END
GO

-- SavePayClaim. @isNew = 1 inserts (Result 2 when the case already has a record), 0 updates (Result 1 when it has none).
CREATE OR ALTER PROCEDURE genisis.PayClaim_Save
    @cas nvarchar(15), @isNew bit, @payClaimDate datetime,
    @lab char(1), @confInfo char(1), @infoInv char(1), @covgSub char(1), @nonPan char(1), @nonCash char(1),
    @cOthers char(1), @others nvarchar(4000), @userCode nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.CAS WHERE CASNumber = @cas)
    BEGIN
        SELECT 1 AS Result;
        RETURN;
    END

    DECLARE @exists bit = CASE WHEN EXISTS (SELECT 1 FROM dbo.CASPayClaim WITH (UPDLOCK, ROWLOCK) WHERE CASNumber = @cas) THEN 1 ELSE 0 END;
    IF @isNew = 1 AND @exists = 1 BEGIN SELECT 2 AS Result; RETURN; END
    IF @isNew = 0 AND @exists = 0 BEGIN SELECT 1 AS Result; RETURN; END

    DECLARE @now datetime = GETDATE();
    IF @isNew = 1
        INSERT dbo.CASPayClaim (CASNumber, CASPayCLMDate, CASLab, CASConfInfo, CASInfoInv, CASCovgSub, CASNonPan, CASNonCash,
            CASCOthers, CASOthers, CASRegUser, CASRegDate, LastUpdateUser, LastUpdateDate)
        VALUES (@cas, @payClaimDate, @lab, @confInfo, @infoInv, @covgSub, @nonPan, @nonCash,
            @cOthers, @others, @userCode, @now, @userCode, @now);
    ELSE
        UPDATE dbo.CASPayClaim
        SET CASPayCLMDate = @payClaimDate, CASLab = @lab, CASConfInfo = @confInfo, CASInfoInv = @infoInv, CASCovgSub = @covgSub,
            CASNonPan = @nonPan, CASNonCash = @nonCash, CASCOthers = @cOthers, CASOthers = @others,
            LastUpdateUser = @userCode, LastUpdateDate = @now
        WHERE CASNumber = @cas;

    SELECT 0 AS Result;
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

-- ===== main/180_mma.sql =====
-- Case > MMA/CPT (legacy frmMMA, "Malaysian Medical Association"): the MMA procedure schedule (MMAProcedures) and the
-- surgeon / anaesthetist fee look-up table (MMAFees), both on the maintenance database. The search patterns are LIKE
-- patterns built (and escaped) by the API; NULL means "not filtered".

-- ProcListing / SearchProc: ProcMainBPCode and OPCSCode are the searchable fields.
CREATE OR ALTER PROCEDURE genisis.Mma_ProcedureSearch
    @mainBodyPart nvarchar(4000) = NULL, @opcsCode nvarchar(4000) = NULL, @max int = 5000
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) ProcIndex AS [Index], RTRIM(ProcMainBPCode) AS MainBodyPart, RTRIM(OPCSCode) AS OpcsCode,
        RTRIM(OPCSNarrative) AS Narrative, RTRIM(ProcSurgCat) AS SurgeonCategory, RTRIM(ProcAnaesCat) AS AnaesthetistCategory,
        RTRIM(ProcPart) AS Part, RTRIM(PartACost) AS PartACost, RTRIM(ProcPageNo) AS PageNo
    FROM genisis.Maint_MMAProcedures
    WHERE (@mainBodyPart IS NULL OR ProcMainBPCode LIKE @mainBodyPart)
      AND (@opcsCode IS NULL OR OPCSCode LIKE @opcsCode)
    ORDER BY ProcIndex;
END
GO

-- SaveProc: @index NULL inserts (returns the new index), otherwise updates that row (Result 1 when it does not exist).
CREATE OR ALTER PROCEDURE genisis.Mma_ProcedureSave
    @index int = NULL, @mainBodyPart nvarchar(4000), @opcsCode nvarchar(4000), @narrative nvarchar(4000),
    @surgeonCategory nvarchar(4000), @anaesthetistCategory nvarchar(4000), @part nvarchar(4000),
    @partACost nvarchar(4000), @pageNo nvarchar(4000), @userCode nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today datetime = CAST(CAST(GETDATE() AS date) AS datetime), @user nvarchar(10) = LEFT(@userCode, 10);
    IF @index IS NULL
    BEGIN
        INSERT genisis.Maint_MMAProcedures (ProcMainBPCode, OPCSCode, OPCSNarrative, ProcSurgCat, ProcAnaesCat, ProcPageNo,
            PartACost, ProcPart, ProcLastUpdateUser, ProcLastUpdateDate)
        OUTPUT 0 AS Result, inserted.ProcIndex AS [Index]
        VALUES (@mainBodyPart, NULLIF(@opcsCode, N''), @narrative, NULLIF(@surgeonCategory, N''), NULLIF(@anaesthetistCategory, N''),
            NULLIF(@pageNo, N''), NULLIF(@partACost, N''), @part, @user, @today);
        RETURN;
    END

    UPDATE genisis.Maint_MMAProcedures
    SET ProcMainBPCode = @mainBodyPart, OPCSCode = NULLIF(@opcsCode, N''), OPCSNarrative = @narrative,
        ProcSurgCat = NULLIF(@surgeonCategory, N''), ProcAnaesCat = NULLIF(@anaesthetistCategory, N''),
        ProcPageNo = NULLIF(@pageNo, N''), PartACost = NULLIF(@partACost, N''), ProcPart = @part,
        ProcLastUpdateUser = @user, ProcLastUpdateDate = @today
    WHERE ProcIndex = @index;
    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END AS Result, @index AS [Index];
END
GO

-- DeleteProc. Result 1 when the row does not exist.
CREATE OR ALTER PROCEDURE genisis.Mma_ProcedureDelete
    @index int
AS
BEGIN
    SET NOCOUNT ON;
    DELETE genisis.Maint_MMAProcedures WHERE ProcIndex = @index;
    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END AS Result;
END
GO

-- LookListing / SearchLook: the surgeon's and anaesthetist's categories are the searchable fields.
CREATE OR ALTER PROCEDURE genisis.Mma_FeeSearch
    @surgeonCategory nvarchar(4000) = NULL, @anaesthetistCategory nvarchar(4000) = NULL, @max int = 5000
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) FeeIndex AS [Index], RTRIM(SurgeonsCat) AS SurgeonCategory, SurgeonsFees AS SurgeonFees,
        RTRIM(AnaesthetistsCat) AS AnaesthetistCategory, AnaesthetistsFees AS AnaesthetistFees
    FROM genisis.Maint_MMAFees
    WHERE (@surgeonCategory IS NULL OR SurgeonsCat LIKE @surgeonCategory)
      AND (@anaesthetistCategory IS NULL OR AnaesthetistsCat LIKE @anaesthetistCategory)
    ORDER BY FeeIndex;
END
GO

-- SaveLook: @index NULL inserts (returns the new index), otherwise updates that row (Result 1 when it does not exist).
CREATE OR ALTER PROCEDURE genisis.Mma_FeeSave
    @index int = NULL, @surgeonCategory nvarchar(4000), @surgeonFees money, @anaesthetistCategory nvarchar(4000),
    @anaesthetistFees money, @userCode nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today datetime = CAST(CAST(GETDATE() AS date) AS datetime), @user nvarchar(10) = LEFT(@userCode, 10);
    IF @index IS NULL
    BEGIN
        INSERT genisis.Maint_MMAFees (SurgeonsCat, SurgeonsFees, AnaesthetistsCat, AnaesthetistsFees, FeeLastUpdateUser, FeeLastUpdateDate)
        OUTPUT 0 AS Result, inserted.FeeIndex AS [Index]
        VALUES (NULLIF(@surgeonCategory, N''), @surgeonFees, NULLIF(@anaesthetistCategory, N''), @anaesthetistFees, @user, @today);
        RETURN;
    END

    UPDATE genisis.Maint_MMAFees
    SET SurgeonsCat = NULLIF(@surgeonCategory, N''), SurgeonsFees = @surgeonFees,
        AnaesthetistsCat = NULLIF(@anaesthetistCategory, N''), AnaesthetistsFees = @anaesthetistFees,
        FeeLastUpdateUser = @user, FeeLastUpdateDate = @today
    WHERE FeeIndex = @index;
    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END AS Result, @index AS [Index];
END
GO

-- DeleteLook. Result 1 when the row does not exist.
CREATE OR ALTER PROCEDURE genisis.Mma_FeeDelete
    @index int
AS
BEGIN
    SET NOCOUNT ON;
    DELETE genisis.Maint_MMAFees WHERE FeeIndex = @index;
    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END AS Result;
END
GO

-- ===== main/180_reinstatement.sql =====
-- Membership > Member Reinstatement (legacy frmMBMReintstatement).
-- Status, final expiry, covered-person status and the amendment history reuse the Adjustment_* writers;
-- the fees reuse Adjustment_PlanRates / Registration_Mco / Registration_Premium (desktop MemberCallFees).

-- The membership as cmdSearch_Click shows it (Search_MBM / VIEW_Search) plus the lapse date (MBMTwo.MBMCancelDate).
CREATE OR ALTER PROCEDURE genisis.Reinstatement_Load
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(m.MBMNumber) AS MembershipNo, RTRIM(m.MBMName) AS Name, RTRIM(m.MBMPolicyNo) AS PolicyNo,
        RTRIM(m.MBMIcBcPp) AS IcBcPp, RTRIM(m.MBMStatus) AS Status, m.MBMPayorEffDate AS PayorEffectiveDate,
        m.MBMPayorExpDate AS PayorExpiryDate, m.MBMFinalExpiryDate AS FinalExpiryDate, RTRIM(m.INSCode) AS InsuredType,
        RTRIM(m.PLNCode) AS PlanCode, RTRIM(m.MBMAdultChild) AS AdultChild, RTRIM(m.PAYCode) AS PayorCode,
        RTRIM(m.HLTCode) AS HealthCode, RTRIM(m.AGECode) AS AgeCode, RTRIM(m.MBMSex) AS Sex,
        o.MBMMCOFee AS McoFee, o.MBMPremium AS Premium, t.MBMCancelDate AS LapsedDate
    FROM dbo.MBM m
    LEFT JOIN dbo.MBMOthers o ON o.MBMNumber = m.MBMNumber
    LEFT JOIN dbo.MBMTwo t ON t.MBMNumber = m.MBMNumber
    WHERE m.MBMNumber = @number;
END
GO

-- Every covered person, whatever its status: the desktop reinstates all of them.
CREATE OR ALTER PROCEDURE genisis.Reinstatement_Covered
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(MBMCCoverID) AS CoverId, RTRIM(MBMCAdultChild) AS AdultChild, RTRIM(MBMCAgeband) AS AgeCode,
        RTRIM(MBMCSex) AS Sex, MBMCMCO AS McoFee, MBMCPremium AS Premium
    FROM dbo.MBMCoveredPersons WHERE MBMCNumber = @number ORDER BY MBMCCoverID;
END
GO

-- One MBMReinstatement row per reinstated cover (00 = principal); invoiced later by Contribution Billing > MCO Reinstatement.
CREATE OR ALTER PROCEDURE genisis.Reinstatement_Insert
    @number nvarchar(4000), @coverId nvarchar(4000), @endtBordx datetime, @batch nvarchar(4000), @mco money, @premium money,
    @lapsed datetime, @reinstate datetime, @user nvarchar(4000), @today datetime
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.MBMReinstatement (MBMNumber, MBMCoverID, MBMEndtBordxDate, MBMMCOFee, MBMPremium, MBMLapsedDate,
        MBMReinstateDate, LastUpdateUSR, LastUpdateDate, MBMEndtBordxBatch)
    VALUES (@number, @coverId, @endtBordx, @mco, @premium, @lapsed, @reinstate, @user, @today, @batch);
END
GO

-- ===== main/181_mco_pending.sql =====
-- Membership > MCO Pending (legacy frmMBMMCOPending). MCOPending lives in the maintenance database (genisis.Maint_MCOPending).
-- Status codes: P pending, R release (the row is removed), B billed (set by Contribution Billing; cannot be changed here).

-- Desktop Listing on VIEW_MCOPending: every cross-referenced membership with its pending row, if any. The bordx date shown is the
-- pending row's, else the membership's; the bordx date filter applies to the membership's date as in the desktop.
-- Rows of MCOPending without a cross-reference are listed under their own number (the desktop fails on the view's empty MBMNumber).
CREATE OR ALTER PROCEDURE genisis.McoPending_Search
    @numberFrom nvarchar(4000) = NULL, @numberTo nvarchar(4000) = NULL, @bordxFrom datetime = NULL, @bordxTo datetime = NULL,
    @status nvarchar(4000) = NULL, @limit int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@limit) RTRIM(COALESCE(x.MBMNumber, p.MBMNumber)) AS MembershipNo, COALESCE(p.MBMBordxDate, x.MBMBordxDate) AS BordxDate,
        p.MBMNewBordxDate AS NewBordxDate, RTRIM(p.Status) AS Status
    FROM genisis.Maint_MBMCrossReference x
    FULL OUTER JOIN genisis.Maint_MCOPending p ON p.MBMNumber = x.MBMNumber
    WHERE (@numberFrom IS NULL OR COALESCE(x.MBMNumber, p.MBMNumber) >= @numberFrom)
      AND (@numberTo IS NULL OR COALESCE(x.MBMNumber, p.MBMNumber) <= @numberTo)
      AND (@bordxFrom IS NULL OR x.MBMBordxDate >= @bordxFrom)
      AND (@bordxTo IS NULL OR x.MBMBordxDate <= @bordxTo)
      AND (@status IS NULL OR p.Status = @status)
    ORDER BY COALESCE(x.MBMNumber, p.MBMNumber);
END
GO

-- One membership for cmdUpdate_Click: Exists says whether MCOPending already has its row.
CREATE OR ALTER PROCEDURE genisis.McoPending_Get
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(COALESCE(x.MBMNumber, p.MBMNumber)) AS MembershipNo, COALESCE(p.MBMBordxDate, x.MBMBordxDate) AS BordxDate,
        RTRIM(p.Status) AS Status, CAST(CASE WHEN p.MBMNumber IS NULL THEN 0 ELSE 1 END AS bit) AS PendingExists
    FROM genisis.Maint_MBMCrossReference x
    FULL OUTER JOIN genisis.Maint_MCOPending p ON p.MBMNumber = x.MBMNumber
    WHERE x.MBMNumber = @number OR p.MBMNumber = @number;
END
GO

CREATE OR ALTER PROCEDURE genisis.McoPending_Insert
    @number nvarchar(4000), @bordxDate datetime, @status nvarchar(4000), @user nvarchar(4000), @today datetime
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO genisis.Maint_MCOPending (MBMNumber, MBMBordxDate, Status, LastUpdateUser, LastUpdateDate)
    VALUES (@number, @bordxDate, @status, @user, @today);
END
GO

CREATE OR ALTER PROCEDURE genisis.McoPending_Delete
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM genisis.Maint_MCOPending WHERE MBMNumber = @number;
END
GO

-- ===== main/182_pluto.sql =====
-- Membership > Upload Membership Excel (Pluto) and Generate Membership ASCII (Pluto)
-- (legacy frmMembershipExcelPluto / frmMembershipASCIIPluto). The desktop used the Pluto connection (MediConnectPluto.UDL),
-- which points at the main database; the staging table is dbo.MBMExcellUploadShelly.

-- One worksheet row (cmdReturnDate_Click). Empty dates are stored as NULL (the desktop's '' became 01/01/1900).
CREATE OR ALTER PROCEDURE genisis.Pluto_InsertUpload
    @No decimal(18, 0), @RELCode nvarchar(4000), @MBMPolicyNo nvarchar(4000), @MBMName nvarchar(4000), @MBMAddress1 nvarchar(4000),
    @MBMAddress2 nvarchar(4000), @MBMAddress3 nvarchar(4000), @CTYCode nvarchar(4000), @STACode nvarchar(4000),
    @MBMPostCode nvarchar(4000), @MBMGroupCompany nvarchar(4000), @MBMDepartment nvarchar(4000), @BRNCode nvarchar(4000),
    @MBMSex nvarchar(4000), @MBMRenewFlag nvarchar(4000), @MBMIcBcPp nvarchar(4000), @MBMDOB datetime, @PLNCode nvarchar(4000),
    @INSCode nvarchar(4000), @MBMPayorEffDate datetime, @MBMPayorExpDate datetime, @MBMExclusion nvarchar(4000),
    @MBMCLNPlanCode nvarchar(4000), @MBMCLNInsType nvarchar(4000), @MBMCLNEffDate datetime, @MBMCLNExpDate datetime,
    @MBMCLNExclusion nvarchar(4000), @PAYCode nvarchar(4000), @MBMBordxDate datetime
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.MBMExcellUploadShelly (No, RELCode, MBMPolicyNo, MBMName, MBMAddress1, MBMAddress2, MBMAddress3, CTYCode, STACode,
        MBMPostCode, MBMGroupCompany, MBMDepartment, BRNCode, MBMSex, MBMRenewFlag, MBMIcBcPp, MBMDOB, PLNCode, INSCode,
        MBMPayorEffDate, MBMPayorExpDate, MBMExclusion, MBMCLNPlanCode, MBMCLNInsType, MBMCLNEffDate, MBMCLNExpDate,
        MBMCLNExclusion, PAYCode, MBMBordxDate)
    VALUES (@No, @RELCode, @MBMPolicyNo, @MBMName, @MBMAddress1, @MBMAddress2, @MBMAddress3, @CTYCode, @STACode,
        @MBMPostCode, @MBMGroupCompany, @MBMDepartment, @BRNCode, @MBMSex, @MBMRenewFlag, @MBMIcBcPp, @MBMDOB, @PLNCode, @INSCode,
        @MBMPayorEffDate, @MBMPayorExpDate, @MBMExclusion, @MBMCLNPlanCode, @MBMCLNInsType, @MBMCLNEffDate, @MBMCLNExpDate,
        @MBMCLNExclusion, @PAYCode, @MBMBordxDate);
END
GO

-- The staged rows of one payor and bordx date, in worksheet order (CmdGenerate_Click). Health type only filters the payor list.
CREATE OR ALTER PROCEDURE genisis.Pluto_AsciiRows
    @bordxDate datetime, @pay nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RELCode, MBMPolicyNo, MBMAgentCode, MBMSalutation, MBMName, MBMAddress1, MBMAddress2, MBMAddress3, CTYCode, MBMPostCode,
        STACode, MBMTelNoH, MBMTelNoO, MBMTelNoM, MBMIcBcPp, MBMDOB, MBMSex, NATCode, MBMWeight, MBMHeight, LGNCode, RACCode,
        MBMOccupation, MBMWorkNature, MBMBlood, MBMAllergic, MBMPayorEffDate, MBMPayorExpDate, PLNCode, INSCode, MBMGroupCompany,
        MBMRenewFlag, MBMExclusion, MBMSmoking, MBMAlcohol, MBMMaritalStatus, BRNCode, MBMPrevPolNo, MBMPrevMemNo, MBMEmployeeNo,
        MBMJoinedDate, Clinical, ClinicName1, ClinicName2, ClinicName3, ClinicName4, MBMDepartment, MBMGRPCompanyAdd1,
        MBMGRPCompanyAdd2, MBMGRPCompanyAdd3, MBMGRPCompanyAdd4, MBMPrevTakeOverPlnCode, MBMInstallment, MBMPOLSubNo1, MBMPOLSubNo2,
        MBMPOLIssuedDate, MBMRiskNo, MBMTransNo, MBMPAYNo, MBMPAYSeqNo, MBMClientNo, MBMClientID, MBMPrevPLNCode, MBMRBAmt,
        MBMPrevInsCom, MBMPAYRemarks, MBMCLMNonPayFr, MBMCLMNonPayTo, MBMUpgradedPLN, MBMNewEffDate, MBMEndorsementNo, MBMAppName,
        MBMPayorPremNet, MBMPayorPremGross, MBMPayorMCONet, MBMPayorMCOGross, MBMFamPolicyDisc, MBMRenewDisc, MBMIcBcPp2nd,
        MBMRnBStatus, MBMLoadPrem, MBMFamDiscAmt, MBMRenewDiscAmt, MBMBlackListed, MBMBankACNo, MBMUnderwritingExcess,
        MBMCLNPlanCode, MBMCLNInsType, MBMCLNEffDate, MBMCLNExpDate, MBMCLNExclusion
    FROM dbo.MBMExcellUploadShelly
    WHERE MBMBordxDate = @bordxDate AND PAYCode = @pay
    ORDER BY No;
END
GO

-- Payors of one health type for the ASCII screen (desktop cboHLTCode_Click / SearchPayorHLTType).
CREATE OR ALTER PROCEDURE genisis.Pluto_Payors
    @hlt nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name FROM dbo.PAY WHERE HLTCode = @hlt ORDER BY PAYCode;
END
GO

-- ===== main/183_card_control.sql =====
-- Membership > Card Sending Mode (legacy frmMembershipCardControl): when and how membership cards were sent.
-- Principals: MBMTwo.MBMSendingMode / MBMCardDate / MBMDOCRef. Supplementaries: the same columns of MBMCoveredPersonsTwo.

-- Desktop MBMInquiryList on Search_MBM_CardSent (VIEW_CardSent_Principal). Result 1: the principals matching the filters;
-- result 2: the covered persons of the family principals among them (only those with an MBMCoveredPersonsTwo row, as in the
-- desktop's inner join). The sending-mode and sent-date filters apply to the principal only, as in the desktop.
CREATE OR ALTER PROCEDURE genisis.CardControl_Search
    @payor nvarchar(4000) = NULL, @numberFrom nvarchar(4000) = NULL, @numberTo nvarchar(4000) = NULL,
    @bordxFrom datetime = NULL, @bordxTo datetime = NULL, @batchFrom int = NULL, @batchTo int = NULL,
    @sentFrom datetime = NULL, @sentTo datetime = NULL, @mode nvarchar(4000) = NULL, @limit int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@limit) RTRIM(m.MBMNumber) AS MembershipNo, RTRIM(m.MBMName) AS Name, m.MBMBordxDate AS BordxDate, m.MBMBatchNo AS BatchNo,
        RTRIM(t.MBMSendingMode) AS SendingMode, t.MBMCardDate AS SentDate, RTRIM(m.MBMPolicyNo) AS PolicyNo, RTRIM(t.MBMDOCRef) AS DocReference,
        RTRIM(o.MBMGroupCompany) AS GroupCompany, RTRIM(o.MBMCostCenter) AS CostCenter, RTRIM(o.MBMBranch) AS Branch,
        RTRIM(o.MBMDepartment) AS Department, RTRIM(m.INSCode) AS InsuredType
    INTO #principals
    FROM dbo.MBM m
    JOIN dbo.MBMTwo t ON t.MBMNumber = m.MBMNumber
    LEFT JOIN dbo.MBMOthers o ON o.MBMNumber = m.MBMNumber
    WHERE (@payor IS NULL OR m.PAYCode = @payor)
      AND (@numberFrom IS NULL OR m.MBMNumber >= @numberFrom)
      AND (@numberTo IS NULL OR m.MBMNumber <= @numberTo)
      AND (@bordxFrom IS NULL OR m.MBMBordxDate >= @bordxFrom)
      AND (@bordxTo IS NULL OR m.MBMBordxDate <= @bordxTo)
      AND (@batchFrom IS NULL OR m.MBMBatchNo >= @batchFrom)
      AND (@batchTo IS NULL OR m.MBMBatchNo <= @batchTo)
      AND (@sentFrom IS NULL OR t.MBMCardDate >= @sentFrom)
      AND (@sentTo IS NULL OR t.MBMCardDate <= @sentTo)
      AND (@mode IS NULL OR t.MBMSendingMode = @mode)
    ORDER BY m.MBMNumber;

    SELECT MembershipNo, Name, BordxDate, BatchNo, SendingMode, SentDate, PolicyNo, DocReference, GroupCompany, CostCenter, Branch,
        Department, InsuredType
    FROM #principals ORDER BY MembershipNo;

    SELECT RTRIM(c.MBMCNumber) AS MembershipNo, RTRIM(c.MBMCCoverID) AS CoverId, RTRIM(c.MBMCName) AS Name,
        RTRIM(c2.MBMSendingMode) AS SendingMode, c2.MBMCardDate AS SentDate, RTRIM(c2.MBMDOCRef) AS DocReference
    FROM #principals p
    JOIN dbo.MBMCoveredPersons c ON c.MBMCNumber = p.MembershipNo
    JOIN dbo.MBMCoveredPersonsTwo c2 ON c2.MBMCNumber = c.MBMCNumber AND c2.MBMCCoverID = c.MBMCCoverID
    WHERE p.InsuredType IN ('F', 'H')
    ORDER BY c.MBMCNumber, c.MBMCCoverID;
END
GO

-- CmdUpdate_Click: cover 00 updates the principal (MBMTwo), any other cover the covered person (MBMCoveredPersonsTwo).
-- Result: the number of rows changed.
CREATE OR ALTER PROCEDURE genisis.CardControl_Update
    @number nvarchar(4000), @coverId nvarchar(4000), @mode nvarchar(4000), @sentDate datetime, @docRef nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    IF @coverId = N'00'
        UPDATE dbo.MBMTwo SET MBMSendingMode = @mode, MBMCardDate = @sentDate, MBMDOCRef = @docRef WHERE MBMNumber = @number;
    ELSE
        UPDATE dbo.MBMCoveredPersonsTwo SET MBMSendingMode = @mode, MBMCardDate = @sentDate, MBMDOCRef = @docRef
        WHERE MBMCNumber = @number AND MBMCCoverID = @coverId;
    SELECT @@ROWCOUNT AS Result;
END
GO

-- ===== main/184_deletion.sql =====
-- Membership > Deletion (legacy frmDeleteMembers). Memberships of one health type and payor, chosen by bordx date range (D),
-- bordx date + batch (B) or membership number range (M), are copied to the archive database (genisis.Arch_*) and deleted from
-- MBM, MBMOthers and, for family plans, MBMCoveredPersons. Other tables (MBMTwo, MBMCrossReference, ...) are left as the desktop
-- left them.

-- Health types and payors for the drop-downs (desktop ComboListing: tables HLT and PAY).
CREATE OR ALTER PROCEDURE genisis.Deletion_Lookups
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(HLTCode) AS Code, RTRIM(HLTDescription) AS Name FROM dbo.HLT ORDER BY HLTCode;
    SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name FROM dbo.PAY ORDER BY PAYCode;
END
GO

-- The principals a delete option selects (desktop Exportsql / DELsqlList).
CREATE OR ALTER FUNCTION genisis.Deletion_Selection
    (@hlt nvarchar(4000), @pay nvarchar(4000), @option nvarchar(4000), @bordxFrom datetime, @bordxTo datetime, @receiptDate datetime,
     @batch int, @numberFrom nvarchar(4000), @numberTo nvarchar(4000))
RETURNS TABLE
AS
RETURN
    SELECT m.MBMNumber, m.INSCode, m.MBMMemberType
    FROM dbo.MBM m
    WHERE m.HLTCode = @hlt AND m.PAYCode = @pay
      AND ((@option = N'D' AND m.MBMBordxDate >= @bordxFrom AND m.MBMBordxDate <= @bordxTo)
        OR (@option = N'B' AND m.MBMBordxDate = @receiptDate AND m.MBMBatchNo = @batch)
        OR (@option = N'M' AND m.MBMNumber >= @numberFrom AND m.MBMNumber <= @numberTo));
GO

-- Desktop DELMBMListing / DELSuppListing. Result 1: the principals; result 2: covered persons of the family principals that have a
-- bordx date (the desktop lists only those, but deletes all).
CREATE OR ALTER PROCEDURE genisis.Deletion_List
    @hlt nvarchar(4000), @pay nvarchar(4000), @option nvarchar(4000), @bordxFrom datetime = NULL, @bordxTo datetime = NULL,
    @receiptDate datetime = NULL, @batch int = NULL, @numberFrom nvarchar(4000) = NULL, @numberTo nvarchar(4000) = NULL, @limit int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@limit) RTRIM(m.MBMNumber) AS MembershipNo, m.MBMBordxDate AS BordxDate, m.MBMBatchNo AS BatchNo, RTRIM(m.MBMName) AS Name,
        RTRIM(m.MBMMemberType) AS MemberType, RTRIM(m.INSCode) AS InsuredType
    INTO #principals
    FROM genisis.Deletion_Selection(@hlt, @pay, @option, @bordxFrom, @bordxTo, @receiptDate, @batch, @numberFrom, @numberTo) s
    JOIN dbo.MBM m ON m.MBMNumber = s.MBMNumber
    ORDER BY m.MBMNumber;

    SELECT MembershipNo, BordxDate, BatchNo, Name, MemberType, InsuredType FROM #principals ORDER BY MembershipNo;

    SELECT RTRIM(c.MBMCNumber) AS MembershipNo, RTRIM(c.MBMCCoverID) AS CoverId, RTRIM(c.MBMCName) AS Name, c.MBMCBordxDate AS BordxDate,
        c.MBMCBatchNo AS BatchNo, RTRIM(c.MBMCMemberType) AS MemberType
    FROM #principals p JOIN dbo.MBMCoveredPersons c ON c.MBMCNumber = p.MembershipNo
    WHERE p.InsuredType IN ('F', 'H') AND c.MBMCBordxDate IS NOT NULL
    ORDER BY c.MBMCNumber, c.MBMCCoverID;
END
GO

-- Desktop DeletePrincipals / DeleteMBMOthers / DeleteSupplementary: archive then delete. The caller owns the transaction.
-- Columns the desktop read from MBM / MBMOthers / MBMCoveredPersons that have since moved to MBMTwo / MBMOthersTwo /
-- MBMCoveredPersonsTwo are read from there.
-- Result: Principals (member type P, as the desktop counts them), Supplementaries (covered persons not of type P), Memberships.
CREATE OR ALTER PROCEDURE genisis.Deletion_Delete
    @hlt nvarchar(4000), @pay nvarchar(4000), @option nvarchar(4000), @bordxFrom datetime = NULL, @bordxTo datetime = NULL,
    @receiptDate datetime = NULL, @batch int = NULL, @numberFrom nvarchar(4000) = NULL, @numberTo nvarchar(4000) = NULL, @today datetime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT MBMNumber, INSCode, MBMMemberType INTO #selected
    FROM genisis.Deletion_Selection(@hlt, @pay, @option, @bordxFrom, @bordxTo, @receiptDate, @batch, @numberFrom, @numberTo);

    INSERT INTO genisis.Arch_MBM (MBMNumber, MBMPolicyNo, MBMIcBcPp, MBMName, MBMSalutation, HLTCode, PAYCode, MBMAddress1, MBMAddress2,
        MBMAddress3, CTYCode, MBMPostCode, MBMTelnoH, MBMTelNoM, MBMTelNoO, MBMEmail, MBMDOB, NATCode, RACCode, MBMSex, MBMPayorEffDate,
        MBMPayorExpDate, AGECode, INSCode, PLNCode, MBMAdultChild, MBMAvailableLimit, SRVCodeList, STACode, LGNCode, MBMBordxDate,
        MBMTakeover, MBMRenewFlag, BRNCode, MBMStatus, MBMValueDate, MBMPolicyYear, MBMMemberType, MBMBatchNo, MBMDataStatus)
    SELECT m.MBMNumber, m.MBMPolicyNo, m.MBMIcBcPp, m.MBMName, t.MBMSalutation, m.HLTCode, m.PAYCode, m.MBMAddress1, m.MBMAddress2,
        m.MBMAddress3, m.CTYCode, m.MBMPostCode, t.MBMTelnoH, t.MBMTelNoM, t.MBMTelNoO, t.MBMEmail, m.MBMDOB, t.NATCode, m.RACCode, m.MBMSex,
        m.MBMPayorEffDate, m.MBMPayorExpDate, m.AGECode, m.INSCode, m.PLNCode, m.MBMAdultChild, m.MBMAvailableLimit, t.SRVCodeList, m.STACode,
        t.LGNCode, m.MBMBordxDate, m.MBMTakeover, m.MBMRenewFlag, m.BRNCode, 'D', @today, m.MBMPolicyYear, m.MBMMemberType, m.MBMBatchNo, 'D'
    FROM dbo.MBM m JOIN #selected s ON s.MBMNumber = m.MBMNumber
    LEFT JOIN dbo.MBMTwo t ON t.MBMNumber = m.MBMNumber;

    INSERT INTO genisis.Arch_MBMOthers (MBMNumber, MBMAgentCode, MBMWeight, MBMHeight, MBMWorkNature, MBMOccupation, MBMBlood, MBMAllergic,
        MBMFirstMemNo, MBMGroupCompany, MBMSmoking, MBMAlcohol, MBMMaritalStatus, MBMPrevPolNo, MBMPrevMemNo, MBMEmployeeNo)
    SELECT o.MBMNumber, o.MBMAgentCode, w.MBMWeight, w.MBMHeight, w.MBMWorkNature, w.MBMOccupation, w.MBMBlood, w.MBMAllergic,
        o.MBMFirstMemNo, o.MBMGroupCompany, w.MBMSmoking, w.MBMAlcohol, w.MBMMaritalStatus, o.MBMPrevPolNo, o.MBMPrevMemNo, o.MBMEmployeeNo
    FROM dbo.MBMOthers o JOIN #selected s ON s.MBMNumber = o.MBMNumber
    LEFT JOIN dbo.MBMOthersTwo w ON w.MBMNumber = o.MBMNumber;

    INSERT INTO genisis.Arch_MBMCoveredPersons (MBMCNumber, MBMCCoverID, MBMCSalutation, MBMCName, MBMCICBCPPNo, MBMCDOB, MBMCAgeband,
        MBMCSex, MBMCAdultChild, MBMCRELCode, MBMCBloodType, MBMCAllergic, MBMCMemberType, MBMCAnnualLimit)
    SELECT c.MBMCNumber, c.MBMCCoverID, c2.MBMCSalutation, c.MBMCName, c.MBMCICBCPPNo, c.MBMCDOB, c.MBMCAgeband, c.MBMCSex, c.MBMCAdultChild,
        c.MBMCRELCode, c2.MBMCBloodType, c2.MBMCAllergic, c.MBMCMemberType, c.MBMCAnnualLimit
    FROM dbo.MBMCoveredPersons c JOIN #selected s ON s.MBMNumber = c.MBMCNumber
    LEFT JOIN dbo.MBMCoveredPersonsTwo c2 ON c2.MBMCNumber = c.MBMCNumber AND c2.MBMCCoverID = c.MBMCCoverID
    WHERE s.INSCode IN ('F', 'H');

    DECLARE @supplementaries int = (SELECT COUNT(*) FROM dbo.MBMCoveredPersons c JOIN #selected s ON s.MBMNumber = c.MBMCNumber
        WHERE s.INSCode IN ('F', 'H') AND ISNULL(c.MBMCMemberType, '') <> 'P');

    DELETE o FROM dbo.MBMOthers o JOIN #selected s ON s.MBMNumber = o.MBMNumber;
    DELETE c FROM dbo.MBMCoveredPersons c JOIN #selected s ON s.MBMNumber = c.MBMCNumber WHERE s.INSCode IN ('F', 'H');
    DELETE m FROM dbo.MBM m JOIN #selected s ON s.MBMNumber = m.MBMNumber;

    SELECT (SELECT COUNT(*) FROM #selected WHERE MBMMemberType = 'P') AS Principals, @supplementaries AS Supplementaries,
        (SELECT COUNT(*) FROM #selected) AS Memberships;
END
GO

-- ===== main/185_leaflet_card.sql =====
-- Membership > Leaflet & Card (legacy frmMainReport, Leaflet and Card Printing tabs; Crystal Reports Leaflet.rpt and
-- MBMCardPrinting.rpt). The desktop's printed / reprinted flags lived in dbo.MBMPrinting, which this database does not have,
-- so they are not tracked.

-- RecordSearching: memberships of one payor, health type and batch within a bordx date range.
CREATE OR ALTER PROCEDURE genisis.LeafletCard_Search
    @from datetime, @to datetime, @pay nvarchar(4000), @hlt nvarchar(4000), @batch int, @limit int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@limit) RTRIM(PAYCode) AS PayorCode, RTRIM(MBMNumber) AS MembershipNo, MBMBordxDate AS BordxDate, RTRIM(MBMPolicyNo) AS PolicyNo,
        RTRIM(HLTCode) AS HealthCode, MBMBatchNo AS BatchNo, RTRIM(MBMName) AS Name
    FROM dbo.MBM
    WHERE MBMBordxDate >= @from AND MBMBordxDate <= @to AND PAYCode = @pay AND HLTCode = @hlt AND MBMBatchNo = @batch
    ORDER BY PAYCode, MBMBordxDate, HLTCode, MBMBatchNo, MBMNumber;
END
GO

-- Leaflet.rpt reads VIEW_LEAFLET (active memberships only) one membership at a time. Result 1: one row per membership;
-- result 2: its covered persons.
CREATE OR ALTER PROCEDURE genisis.LeafletCard_Leaflets
    @from datetime, @to datetime, @pay nvarchar(4000), @hlt nvarchar(4000), @batch int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT RTRIM(v.MBMNumber) AS MembershipNo, RTRIM(v.MBMPolicyNo) AS PolicyNo, RTRIM(v.MBMAgentCode) AS AgentCode,
        RTRIM(v.MBMName) AS Name, RTRIM(v.MBMAddress1) AS Address1, RTRIM(v.MBMAddress2) AS Address2, RTRIM(v.MBMAddress3) AS Address3,
        RTRIM(v.MBMPostCode) AS PostCode, RTRIM(v.CTYCode) AS City, RTRIM(v.STACode) AS State, RTRIM(v.PAYCompanyName) AS PayorName,
        RTRIM(v.MBMGroupCompany) AS GroupCompany, RTRIM(v.MBMEmployeeNo) AS EmployeeNo, RTRIM(v.MBMDepartment) AS Department,
        RTRIM(v.INSCode) AS InsuredType, RTRIM(v.PLNDescription) AS PlanDescription
    INTO #leaflets
    FROM dbo.VIEW_LEAFLET v
    JOIN dbo.MBM m ON m.MBMNumber = v.MBMNumber
    WHERE m.MBMBordxDate >= @from AND m.MBMBordxDate <= @to AND m.PAYCode = @pay AND m.HLTCode = @hlt AND m.MBMBatchNo = @batch;

    SELECT * FROM #leaflets ORDER BY MembershipNo;

    SELECT RTRIM(c.MBMCNumber) AS MembershipNo, RTRIM(c.MBMCCoverID) AS CoverId, LTRIM(RTRIM(c.MBMCName)) AS Name, RTRIM(c.MBMCRELCode) AS Relationship
    FROM dbo.MBMCoveredPersons c
    WHERE c.MBMCNumber IN (SELECT MembershipNo FROM #leaflets)
    ORDER BY c.MBMCNumber, c.MBMCCoverID;
END
GO

-- MBMCardPrinting.rpt / ExpMBMPrintingToExcel read VIEW_CardPrinting: one card per covered person, the principal as cover 00.
-- The desktop's card selection does not filter on health type.
CREATE OR ALTER PROCEDURE genisis.LeafletCard_Cards
    @from datetime, @to datetime, @pay nvarchar(4000), @batch int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT RTRIM(PAYCode) AS PayorCode, MBMBordxDate AS BordxDate, RTRIM(HLTCode) AS HealthCode, MBMBatchNo AS BatchNo,
        RTRIM(MBMNumber) AS MembershipNo, RTRIM(MBMCCoverID) AS CoverId, RTRIM(MBMCName) AS Name, RTRIM(MBMCICBCPPNo) AS IcBcPp,
        RTRIM(MBMCRELCode) AS Relationship, RTRIM(MBMPolicyNo) AS PolicyNo, RTRIM(PLNCode) AS PlanCode, MBMPayorExpDate AS ExpiryDate,
        RTRIM(MBMGroupCompany) AS GroupCompany, RTRIM(MBMEmployeeNo) AS EmployeeNo
    FROM dbo.VIEW_CardPrinting
    WHERE MBMNumber IS NOT NULL AND MBMCNumber IS NOT NULL
      AND MBMBordxDate >= @from AND MBMBordxDate <= @to AND PAYCode = @pay AND MBMBatchNo = @batch
    ORDER BY PayorCode, BordxDate, HealthCode, BatchNo, MembershipNo, CoverId;
END
GO

-- Payor names for the heading of the card export (desktop ComboListing on PAY).
CREATE OR ALTER PROCEDURE genisis.LeafletCard_Payor
    @pay nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(PAYCompanyName) AS Name FROM dbo.PAY WHERE PAYCode = @pay;
END
GO

-- ===== main/186_temp_registration.sql =====
-- Membership > Temp MBM Reg (legacy FrmTempMBMReg): a registration pre-filled from a previous membership (cmdShow_Click on
-- SearchMBMCovPersonsProc, with showMBMTwo / showMBMOthers / showMBMOthersTwo / showMBMCov). The new membership is saved by the
-- Registration procedures.

-- Result 1: the previous membership; result 2: its covered persons (family plans, cover 00 excluded).
CREATE OR ALTER PROCEDURE genisis.TempRegistration_Source
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(m.MBMNumber) AS MembershipNo, RTRIM(m.HLTCode) AS HealthCode, RTRIM(m.PAYCode) AS PayorCode,
        RTRIM(m.MBMPolicyNo) AS PolicyNo, m.MBMPolicyYear AS PolicyYear, RTRIM(t.MBMSalutation) AS Salutation, RTRIM(m.MBMName) AS Name,
        RTRIM(m.MBMIcBcPp) AS IcBcPp, RTRIM(t.MBMIcBcPp2nd) AS OtherIc, m.MBMDOB AS DateOfBirth, RTRIM(m.MBMSex) AS Sex,
        RTRIM(m.RACCode) AS RaceCode, RTRIM(t.NATCode) AS NationalityCode, RTRIM(w.MBMMaritalStatus) AS MaritalStatus,
        RTRIM(m.MBMAddress1) AS Address1, RTRIM(m.MBMAddress2) AS Address2, RTRIM(m.MBMAddress3) AS Address3, RTRIM(m.CTYCode) AS City,
        RTRIM(m.MBMPostCode) AS PostCode, RTRIM(m.STACode) AS State, RTRIM(t.MBMTelnoH) AS TelHome, RTRIM(t.MBMTelNoM) AS TelMobile,
        RTRIM(t.MBMTelNoO) AS TelOffice, RTRIM(t.MBMEmail) AS Email, RTRIM(m.INSCode) AS InsuredType, RTRIM(m.PLNCode) AS PlanCode,
        o.MBMFirstJoinedDate AS DateJoined, RTRIM(m.MBMTakeover) AS TakeOver, RTRIM(o.MBMGuaRenewal) AS GuaranteeRenewal,
        RTRIM(o.MBMInstallment) AS InstallmentMode, RTRIM(o.MBMGroupCompany) AS GroupCompany, RTRIM(o.MBMEmployeeNo) AS EmployeeNo,
        RTRIM(o.MBMDepartment) AS Department, RTRIM(o.MBMBranch) AS Branch, RTRIM(o.MBMAgentCode) AS AgentCode,
        RTRIM(w.MBMExclusion) AS Exclusion, RTRIM(w.MBMAllergic) AS Allergic, RTRIM(w.MBMRemarks) AS Remarks
    FROM dbo.MBM m
    LEFT JOIN dbo.MBMTwo t ON t.MBMNumber = m.MBMNumber
    LEFT JOIN dbo.MBMOthers o ON o.MBMNumber = m.MBMNumber
    LEFT JOIN dbo.MBMOthersTwo w ON w.MBMNumber = m.MBMNumber
    WHERE m.MBMNumber = @number;

    SELECT RTRIM(c.MBMCCoverID) AS CoverId, RTRIM(c.MBMCRELCode) AS Relationship, RTRIM(c2.MBMCSalutation) AS Salutation,
        RTRIM(c.MBMCName) AS Name, RTRIM(c.MBMCICBCPPNo) AS IcBcPp, c.MBMCDOB AS DateOfBirth, RTRIM(c.MBMCSex) AS Sex,
        RTRIM(c2.MBMCOccupation) AS Occupation, RTRIM(c2.MBMCExclusion) AS Exclusion, RTRIM(c2.MBMCAllergic) AS Allergic
    FROM dbo.MBM m
    JOIN dbo.MBMCoveredPersons c ON c.MBMCNumber = m.MBMNumber
    LEFT JOIN dbo.MBMCoveredPersonsTwo c2 ON c2.MBMCNumber = c.MBMCNumber AND c2.MBMCCoverID = c.MBMCCoverID
    WHERE m.MBMNumber = @number AND m.INSCode IN ('F', 'H') AND RTRIM(c.MBMCCoverID) <> '00'
    ORDER BY c.MBMCCoverID;
END
GO

-- ===== main/187_temp_adjustment.sql =====
-- Membership > Temp MBM Adj (legacy FrmTempMBMAdj): an older copy of Adjustment that only opens memberships of temporary payors
-- (PAY.tempPayIND = 'Y', checked in FindMBM). The adjustment itself is saved by the Adjustment procedures.
CREATE OR ALTER PROCEDURE genisis.TempAdjustment_Payor
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(m.MBMNumber) AS MembershipNo, RTRIM(m.PAYCode) AS PayorCode,
        CAST(CASE WHEN RTRIM(ISNULL(p.tempPayIND, '')) = 'Y' THEN 1 ELSE 0 END AS bit) AS Temporary
    FROM dbo.MBM m
    LEFT JOIN dbo.PAY p ON p.PAYCode = m.PAYCode
    WHERE m.MBMNumber = @number;
END
GO

-- ===== main/190_case_audit_review.sql =====
-- Case > Case Audit Review (legacy frmCaseAuditReview). The case, member, diagnoses and repudiation are read with the
-- procedures Case Adjustment already uses; these cover what is specific to the audit review: its own record
-- (dbo.CASAuditReview, shown through dbo.VIEW_CAS_AuditReview) and the hospital payments (HISPayment.Payment2HOS).
-- They replace the raw @strAppend dbo.Case_AuditReview modes and dbo.Case_AuditInsert / dbo.Case_AuditUpdate.

-- showOther (audit part) + showPaymentRecord + the member. The visit date is stored by the desktop as an mm/dd/yyyy string.
CREATE OR ALTER PROCEDURE genisis.CaseAudit_Get
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SET DATEFORMAT mdy;
    SELECT RTRIM(CASInvNo) AS InvoiceNo, INVDate AS InvoiceDate, CASInvAmt AS InvoiceAmount, CASDisRec AS DiscountReceived,
        RTRIM(CASDiscType) AS DiscountType, CASLetterSent AS LetterSent, CASHospReply AS LetterReply,
        RTRIM(CASARStatus) AS Status, CASAROpenDate AS DateOpen, CASDTC AS DateToClaims,
        CASReviewAudit AS Query, CASARAnswer AS Answer, CASARRemarks AS Remarks,
        CASE WHEN ISDATE(NULLIF(RTRIM(CASDOV), N'')) = 1 THEN CONVERT(datetime, RTRIM(CASDOV), 101) END AS VisitDate, RTRIM(CASDOVTime) AS VisitTime,
        CASVRemarks AS VisitAction, RTRIM(Officer) AS Officer
    FROM dbo.CASAuditReview WHERE CASNumber = @cas;

    IF OBJECT_ID(N'genisis.Pay_Payment2HOS', N'SN') IS NOT NULL
        EXEC sp_executesql N'SELECT RTRIM(PVHOSNo) AS PvNo, PVHOSDate AS PvDate, HOSPaidAmt AS AmountPaid
            FROM genisis.Pay_Payment2HOS WHERE CASNumber = @cas ORDER BY PVHOSDate, PVHOSIndex',
            N'@cas nvarchar(15)', @cas;
    ELSE
        SELECT CAST(NULL AS nvarchar(30)) AS PvNo, CAST(NULL AS datetime) AS PvDate, CAST(NULL AS money) AS AmountPaid WHERE 1 = 0;

    -- The member straight from dbo.MBM: SearchMBMCas (used for the case's member panel) returns nothing for a member
    -- that has no MBMOthers / MBMOthersTwo / MBMTwo row, and the audit review still has to show who the member is.
    SELECT RTRIM(m.MBMNumber) AS MemberNo, RTRIM(m.MBMName) AS Name, RTRIM(m.MBMIcBcPp) AS Ic, RTRIM(m.PAYCode) AS Payor,
        RTRIM(m.MBMPolicyNo) AS PolicyNo, RTRIM(m.MBMRenewFlag) AS Renewal, RTRIM(m.MBMTakeover) AS TakeOver,
        m.MBMPayorEffDate AS EffectiveDate, m.MBMPayorExpDate AS ExpiryDate, RTRIM(m.MBMAddress1) AS Address1,
        RTRIM(m.MBMAddress2) AS Address2, RTRIM(m.MBMAddress3) AS Address3, RTRIM(m.MBMPostCode) AS PostCode,
        RTRIM(m.CTYCode) AS Town, RTRIM(m.STACode) AS State, m.MBMDOB AS Dob, m.MBMAvailableLimit AS AvailableLimit,
        RTRIM(m.MBMStatus) AS Status, RTRIM(CAST(o.MBMInstallment AS nvarchar(50))) AS Installment
    FROM dbo.CAS c
    INNER JOIN dbo.MBM m ON m.MBMNumber = c.MBMNumber
    LEFT JOIN dbo.MBMOthers o ON o.MBMNumber = m.MBMNumber
    WHERE c.CASNumber = @cas;
END
GO

-- cmdUpdate1_Click: Case_AuditInsert / Case_AuditUpdate on the case's audit row plus Case_AuditUpdate on the maintenance
-- cross reference (the audit status). Blank amounts are stored as 0, blank dates as NULL, like the desktop.
-- Result: 0 saved, 1 case not found.
CREATE OR ALTER PROCEDURE genisis.CaseAudit_Save
    @cas nvarchar(15), @invoiceNo nvarchar(4000), @invoiceDate datetime, @invoiceAmount money, @discountReceived money,
    @discountType nvarchar(4000), @letterSent datetime, @letterReply datetime, @status nvarchar(4000),
    @dateOpen datetime, @dateToClaims datetime, @query nvarchar(4000), @answer nvarchar(4000), @remarks nvarchar(4000),
    @visitDate datetime, @visitTime nvarchar(4000), @visitAction nvarchar(4000), @officer nvarchar(4000), @userCode nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @mbm nvarchar(30) = (SELECT MBMNumber FROM dbo.CAS WHERE CASNumber = @cas);
    IF @mbm IS NULL
    BEGIN
        SELECT 1 AS Result;
        RETURN;
    END

    DECLARE @today datetime = CAST(CAST(GETDATE() AS date) AS datetime), @user nvarchar(10) = LEFT(@userCode, 10);
    DECLARE @visit nvarchar(10) = CONVERT(nvarchar(10), @visitDate, 101);
    DECLARE @amount money = ISNULL(@invoiceAmount, 0), @discount money = ISNULL(@discountReceived, 0);

    IF EXISTS (SELECT 1 FROM dbo.CASAuditReview WITH (UPDLOCK, ROWLOCK) WHERE CASNumber = @cas)
        UPDATE dbo.CASAuditReview
        SET CASInvNo = @invoiceNo, INVDate = @invoiceDate, CASInvAmt = @amount, CASLetterSent = @letterSent, CASHospReply = @letterReply,
            CASDOV = @visit, CASDOVTime = @visitTime, Officer = @officer, CASVRemarks = @visitAction, CASDisRec = @discount,
            CASDTC = @dateToClaims, CASAROpenDate = @dateOpen, CASARAnswer = @answer, CASARRemarks = @remarks,
            CASReviewAudit = @query, CASARStatus = @status, CASLastUpdateUser = @user, CASLastUpdateDate = @today,
            CASDiscType = @discountType
        WHERE CASNumber = @cas;
    ELSE
        INSERT dbo.CASAuditReview (CASNumber, CASInvNo, INVDate, CASInvAmt, CASLetterSent, CASHospReply, CASDOV, CASDOVTime,
            Officer, CASVRemarks, CASDisRec, CASDTC, CASAROpenDate, CASARAnswer, CASARRemarks, CASReviewAudit, CASARStatus,
            CASLastUpdateUser, CASLastUpdateDate, CASDiscType)
        VALUES (@cas, @invoiceNo, @invoiceDate, @amount, @letterSent, @letterReply, @visit, @visitTime,
            @officer, @visitAction, @discount, @dateToClaims, @dateOpen, @answer, @remarks, @query, @status,
            @user, @today, @discountType);

    UPDATE genisis.Maint_CASCrossReference SET CASARStatus = @status WHERE CASNumber = @cas AND MBMNumber = @mbm;

    SELECT 0 AS Result;
END
GO

-- ===== main/200_case_transfer.sql =====
-- Case > Case Transfer (legacy frmCaseTransferMBMNo, "Case - Transfer of Membership"). Replaces the raw @strAppend
-- dbo.Case_Transfer modes and the inline recordset writes of TransferProcess / ReCalculatePrevMBMLmt.
-- The worksheet totals are read through the genisis.WS_* synonyms, so the whole transfer is one MainDb transaction
-- (the desktop ran its writes on four connections with no working transaction).

-- Text amounts (ClaimApprove stores them as nvarchar) as a number; anything that is not a plain number is 0.
-- ISNUMERIC is not enough: it accepts a lone minus sign, a lone dot and a dollar sign, none of which can be cast.
CREATE OR ALTER FUNCTION genisis.Util_SafeDecimal (@s nvarchar(60))
RETURNS decimal(18, 4)
AS
BEGIN
    DECLARE @t nvarchar(60) = LTRIM(RTRIM(ISNULL(@s, N'')));
    RETURN CASE
        WHEN @t = N'' OR LEN(@t) > 15 OR @t LIKE N'%[^0-9.-]%' OR @t NOT LIKE N'%[0-9]%'
          OR @t LIKE N'%.%.%' OR @t LIKE N'%-%-%' OR (@t LIKE N'%-%' AND LEFT(@t, 1) <> N'-') THEN 0
        ELSE CAST(@t AS decimal(18, 4))
    END;
END
GO

-- showCAS: the member / cover the case is on now.
CREATE OR ALTER PROCEDURE genisis.CaseTransfer_Case
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(CASNumber) AS CaseNumber, RTRIM(MBMNumber) AS MemberNo, RTRIM(MBMPatientCovID) AS CoverId, RTRIM(CASStatus) AS CaseStatus
    FROM dbo.CAS WHERE CASNumber = @cas;
END
GO

-- MBMInfo: member / patient details. No row when the member does not exist; CoverFound = 0 for an unknown supplementary.
CREATE OR ALTER PROCEDURE genisis.CaseTransfer_Member
    @mbm nvarchar(30), @cover nvarchar(10)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(m.MBMNumber) AS MemberNo, @cover AS CoverId,
        RTRIM(CASE WHEN @cover = N'00' THEN m.MBMName ELSE cp.MBMCName END) AS PatientName,
        RTRIM(m.MBMPolicyNo) AS PolicyNo,
        RTRIM(CASE WHEN @cover = N'00' THEN m.MBMIcBcPp ELSE cp.MBMCICBCPPNo END) AS IcNo,
        RTRIM(m.MBMStatus) AS Status, m.MBMPayorEffDate AS EffDate, m.MBMPayorExpDate AS ExpDate,
        CAST(CASE WHEN @cover = N'00' OR cp.MBMCNumber IS NOT NULL THEN 1 ELSE 0 END AS bit) AS CoverFound
    FROM dbo.MBM m
    LEFT JOIN dbo.MBMCoveredPersons cp ON cp.MBMCNumber = m.MBMNumber AND cp.MBMCCoverID = @cover
    WHERE m.MBMNumber = @mbm;
END
GO

-- ReCalculatePrevMBMLmt / the limit part of TransferProcess for one member. @sign = +1 gives the case's approved amount
-- back to the previous member (@giveBack = 1: always applied, the covered person's own plan code is used for the benefit
-- limit lookup), -1 charges the new member (only applied when the amount is not zero).
-- Limit holder: the principal's MBM.MBMAvailableLimit when the plan shares the limit (SuppLimitStatus A) or the patient is
-- the principal; otherwise the covered person's own MBMCAvailableLimit (status B uses cover 01). The benefit balances
-- (cancer / kidney / home nursing) follow the plan's benefit status the same way. The annual limit row is the one in
-- force on the member's payor effective date (the desktop took an arbitrary row of the plan).
CREATE OR ALTER PROCEDURE genisis.CaseTransfer_MoveLimit
    @mbm nvarchar(30), @cov nvarchar(10), @sign int, @total money, @cancer money, @kidney money, @home money, @giveBack bit
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @hlt nvarchar(4), @ins nvarchar(4), @plan nvarchar(8), @eff datetime;
    SELECT @hlt = HLTCode, @ins = INSCode, @plan = PLNCode, @eff = MBMPayorEffDate FROM dbo.MBM WHERE MBMNumber = @mbm;

    DECLARE @status varchar(1);
    SELECT TOP (1) @status = SuppLimitStatus FROM dbo.AnnualLimit
    WHERE PLNCode = @plan AND INSCode = @ins AND HLTCode = @hlt AND AnnualEffDate <= ISNULL(@eff, '19000101')
    ORDER BY AnnualEffDate DESC;
    IF @status IS NULL
        SELECT TOP (1) @status = SuppLimitStatus FROM dbo.AnnualLimit WHERE PLNCode = @plan ORDER BY AnnualEffDate DESC;
    SET @status = CASE WHEN RTRIM(ISNULL(@status, '')) = '' THEN 'A' ELSE RTRIM(@status) END;

    IF @giveBack = 1 OR @total <> 0
    BEGIN
        IF @status = 'A' OR @cov = N'00'
            UPDATE dbo.MBM SET MBMAvailableLimit = ISNULL(MBMAvailableLimit, 0) + @sign * @total WHERE MBMNumber = @mbm;
        ELSE
        BEGIN
            DECLARE @holder nvarchar(10) = CASE WHEN @status = 'B' THEN N'01' ELSE @cov END;
            UPDATE dbo.MBMCoveredPersons SET MBMCAvailableLimit = ISNULL(MBMCAvailableLimit, 0) + @sign * @total
            WHERE MBMCNumber = @mbm AND MBMCCoverID = @holder;
            IF @giveBack = 1
            BEGIN
                DECLARE @coveredPlan nvarchar(8) = (SELECT TOP (1) RTRIM(MBMCPLNCode) FROM dbo.MBMCoveredPersons WHERE MBMCNumber = @mbm AND MBMCCoverID = @holder);
                IF ISNULL(@coveredPlan, N'') <> N'' SET @plan = @coveredPlan;
            END
        END
    END

    DECLARE @benefitStatus varchar(1);
    SELECT TOP (1) @benefitStatus = SuppALBStatus FROM dbo.BnfAnnLmt
    WHERE INSCode = @ins AND PLNCode = @plan AND HLTCode = @hlt AND ALBEffDate <= ISNULL(@eff, '19000101')
    ORDER BY ALBEffDate DESC;
    DECLARE @benefitHolder nvarchar(6) = CASE WHEN RTRIM(ISNULL(@benefitStatus, '')) = 'B' THEN N'01' ELSE @cov END;

    UPDATE dbo.MBMBnfLmt
    SET MBMCancerBalLmt = ISNULL(MBMCancerBalLmt, 0) + @sign * @cancer,
        MBMKidneyBalLmt = ISNULL(MBMKidneyBalLmt, 0) + @sign * @kidney,
        MBMHomeNCBalLmt = ISNULL(MBMHomeNCBalLmt, 0) + @sign * @home
    WHERE MBMNumber = @mbm AND MBMCoveredID = @benefitHolder;
END
GO

-- TransferProcess. The API has already checked the target member / cover and status; the existence checks here only
-- protect the data. Result: 0 transferred, 1 case not found, 2 target member / cover not found, 3 the case is already on
-- that member / cover, 4 the previous member is not on the member table (nothing to give the limit back to).
-- Returns the approved total that moved.
CREATE OR ALTER PROCEDURE genisis.CaseTransfer_Apply
    @cas nvarchar(15), @newMbm nvarchar(30), @newCov nvarchar(10), @userCode nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @oldMbm nvarchar(30), @oldCov nvarchar(10);
    SELECT @oldMbm = RTRIM(MBMNumber), @oldCov = RTRIM(MBMPatientCovID) FROM dbo.CAS WITH (UPDLOCK, ROWLOCK) WHERE CASNumber = @cas;
    IF @oldMbm IS NULL BEGIN SELECT 1 AS Result, CAST(0 AS money) AS Moved; RETURN; END
    IF NOT EXISTS (SELECT 1 FROM dbo.MBM WHERE MBMNumber = @newMbm)
       OR (@newCov <> N'00' AND NOT EXISTS (SELECT 1 FROM dbo.MBMCoveredPersons WHERE MBMCNumber = @newMbm AND MBMCCoverID = @newCov))
    BEGIN SELECT 2 AS Result, CAST(0 AS money) AS Moved; RETURN; END
    IF @oldMbm = @newMbm AND @oldCov = @newCov BEGIN SELECT 3 AS Result, CAST(0 AS money) AS Moved; RETURN; END
    IF NOT EXISTS (SELECT 1 FROM dbo.MBM WHERE MBMNumber = @oldMbm) BEGIN SELECT 4 AS Result, CAST(0 AS money) AS Moved; RETURN; END

    -- What the case has been approved for, summed over every claim version (CLMTotalApproved) and the
    -- cancer / kidney / home nursing benefit amounts (stored as text on ClaimApprove).
    DECLARE @total money = ISNULL((SELECT SUM(CLMTotalApproved) FROM genisis.WS_Claim WHERE CASNumber = @cas), 0);
    DECLARE @cancer money, @kidney money, @home money;
    SELECT @cancer = ISNULL(SUM(genisis.Util_SafeDecimal(CLMIndCancerTreatment)), 0),
        @kidney = ISNULL(SUM(genisis.Util_SafeDecimal(CLMIndKidneyDial)), 0),
        @home = ISNULL(SUM(genisis.Util_SafeDecimal(CLMHomeNCAmt)), 0)
    FROM genisis.WS_ClaimApprove WHERE CASNumber = @cas;

    DECLARE @today datetime = CAST(CAST(GETDATE() AS date) AS datetime);
    DECLARE @note nvarchar(500) = N'~TransMBM by : ' + @userCode + N' Date : ' + CONVERT(nvarchar(8), GETDATE(), 112) + N','
        + @oldMbm + N'-' + @oldCov + N'->' + @newMbm + N'-' + @newCov;

    UPDATE genisis.Maint_CASCrossReference
    SET TransRemarks = RIGHT(ISNULL(TransRemarks, N'') + @note, 2000), MBMNumber = @newMbm, MBMPatientCovID = @newCov
    WHERE CASNumber = @cas;
    UPDATE dbo.CAS
    SET MBMNumber = @newMbm, MBMPatientCovID = @newCov, CASLastUpdateUser = @userCode, CASLastUpdateDate = @today
    WHERE CASNumber = @cas;

    EXEC genisis.CaseTransfer_MoveLimit @oldMbm, @oldCov, 1, @total, @cancer, @kidney, @home, 1;
    EXEC genisis.CaseTransfer_MoveLimit @newMbm, @newCov, -1, @total, @cancer, @kidney, @home, 0;

    SELECT 0 AS Result, @total AS Moved;
END
GO

-- ===== main/210_case_listing.sql =====
-- Case > Inpatient Case Listing (legacy mnuAppCase -> FrmWksBordx, "Select Worksheets for Bordx"): search the worksheets
-- that are waiting for a bordx (HISWorkSheet views, through the genisis.WS_* synonyms) and generate the bordx.
-- Replaces the raw SQL text FrmWksBordx built for WorksheetTrackingListing / WorksheetTrackingListing2 / UpdateRecord /
-- SaveCLMBordx and the Medix.bas GenerateCLMBordxNo sequence read-then-update (which could hand out a number twice).

-- SearchRecord + WorksheetTrackingListing(2). @scan = 1: one worksheet (case number, optionally a claim version);
-- 0: a case number range. @ib = 1 reads the IB payor view. Dates are inclusive; @doaEmpty / @dodEmpty keep only
-- worksheets with no admission / discharge date; @mhBordx = 1 keeps CLMMHBordx = 'Y' (Bordx Status "Yes").
CREATE OR ALTER PROCEDURE genisis.CaseListing_Search
    @scan bit, @wksFrom nvarchar(30) = NULL, @wksTo nvarchar(30) = NULL, @version int = NULL, @ib bit = 0,
    @doaFrom datetime = NULL, @doaTo datetime = NULL, @doaEmpty bit = 0,
    @dodFrom datetime = NULL, @dodTo datetime = NULL, @dodEmpty bit = 0, @mhBordx bit = 0, @max int = 5000
AS
BEGIN
    SET NOCOUNT ON;
    IF @ib = 1
        SELECT TOP (@max) RTRIM(CASNumber) AS CaseNo, ClaimVersion, RTRIM(PAYCode) AS Payor, CLMTotalApproved AS AppAmt,
            CASDateAdmitted AS Doa, CASDischargeDate AS Dod, RTRIM(INVPayTo) AS CashType, RTRIM(MBMPatientCovID) AS CoverId,
            RTRIM(MBMCName) AS CoveredName, RTRIM(HOSName) AS Hospital, CLMPayableByMedix2H AS HosAmt, RTRIM(MBMName) AS MemberName,
            RTRIM(MBMGroupCompany) AS GroupCompany, RTRIM(PLNCode) AS PlanCode, RTRIM(ICDCode1) AS IcdCode, RTRIM(MBMBTBG) AS Btbg,
            RTRIM(INSCode) AS InsuredType, RTRIM(CASClaimability) AS Claimability, RTRIM(HLTCode) AS HltType
        FROM genisis.WS_PendingBordxIB
        WHERE ClaimStatus <> N'R'
          AND (@scan = 0 OR @wksFrom IS NULL OR CASNumber = @wksFrom)
          AND (@scan = 0 OR @version IS NULL OR ClaimVersion = @version)
          AND (@scan = 1 OR @wksFrom IS NULL OR CASNumber >= @wksFrom)
          AND (@scan = 1 OR @wksTo IS NULL OR CASNumber <= @wksTo)
          AND (@doaFrom IS NULL OR CASDateAdmitted >= @doaFrom) AND (@doaTo IS NULL OR CASDateAdmitted <= @doaTo)
          AND (@dodFrom IS NULL OR CASDischargeDate >= @dodFrom) AND (@dodTo IS NULL OR CASDischargeDate <= @dodTo)
          AND (@doaEmpty = 0 OR CASDateAdmitted IS NULL) AND (@dodEmpty = 0 OR CASDischargeDate IS NULL)
          AND (@mhBordx = 0 OR CLMMHBordx = 'Y')
        ORDER BY CASNumber, ClaimVersion;
    ELSE
        SELECT TOP (@max) RTRIM(CASNumber) AS CaseNo, ClaimVersion, RTRIM(PAYCode) AS Payor, CLMTotalApproved AS AppAmt,
            CASDateAdmitted AS Doa, CASDischargeDate AS Dod, RTRIM(INVPayTo) AS CashType, RTRIM(MBMPatientCovID) AS CoverId,
            RTRIM(MBMCName) AS CoveredName, RTRIM(HOSName) AS Hospital, CLMPayableByMedix2H AS HosAmt, RTRIM(MBMName) AS MemberName,
            RTRIM(MBMGroupCompany) AS GroupCompany, RTRIM(PLNCode) AS PlanCode, RTRIM(ICDCode1) AS IcdCode, RTRIM(MBMBTBG) AS Btbg,
            RTRIM(INSCode) AS InsuredType, RTRIM(CASClaimability) AS Claimability, RTRIM(HLTCode) AS HltType
        FROM genisis.WS_PendingBordx
        WHERE ClaimStatus <> N'R'
          AND (@scan = 0 OR @wksFrom IS NULL OR CASNumber = @wksFrom)
          AND (@scan = 0 OR @version IS NULL OR ClaimVersion = @version)
          AND (@scan = 1 OR @wksFrom IS NULL OR CASNumber >= @wksFrom)
          AND (@scan = 1 OR @wksTo IS NULL OR CASNumber <= @wksTo)
          AND (@doaFrom IS NULL OR CASDateAdmitted >= @doaFrom) AND (@doaTo IS NULL OR CASDateAdmitted <= @doaTo)
          AND (@dodFrom IS NULL OR CASDischargeDate >= @dodFrom) AND (@dodTo IS NULL OR CASDischargeDate <= @dodTo)
          AND (@doaEmpty = 0 OR CASDateAdmitted IS NULL) AND (@dodEmpty = 0 OR CASDischargeDate IS NULL)
          AND (@mhBordx = 0 OR CLMMHBordx = 'Y')
        ORDER BY CASNumber, ClaimVersion;
END
GO

-- One pending worksheet as the bordx needs it: the values the selection rules check, who is paid (INVPayTo, BTBG) and the
-- three amounts a bordx can sum (CmdBordx_Click picks one by BTBG / INVPayTo). No row when it is not (or no longer) pending.
CREATE OR ALTER PROCEDURE genisis.CaseListing_Item
    @cas nvarchar(30), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) p.CaseNo, p.ClaimVersion, p.Payor, p.Dod, p.Hospital, p.GroupCompany, p.InsuredType, p.Claimability, p.HltType,
        p.CashType, p.Btbg, c.CLMPayableByMedix2H AS Payable2H, c.CLMPayableByMedix2M AS Payable2M,
        c.CLMTotalApproved AS TotalApproved, ISNULL(c.CLMCaseMgmtFees, 0) AS CaseMgmtFees
    FROM (
        SELECT RTRIM(CASNumber) AS CaseNo, ClaimVersion, RTRIM(PAYCode) AS Payor, CASDischargeDate AS Dod, RTRIM(HOSName) AS Hospital,
            RTRIM(MBMGroupCompany) AS GroupCompany, RTRIM(INSCode) AS InsuredType, RTRIM(CASClaimability) AS Claimability,
            RTRIM(HLTCode) AS HltType, RTRIM(INVPayTo) AS CashType, RTRIM(MBMBTBG) AS Btbg
        FROM genisis.WS_PendingBordx WHERE CASNumber = @cas AND ClaimVersion = @version AND ClaimStatus <> N'R'
        UNION ALL
        SELECT RTRIM(CASNumber), ClaimVersion, RTRIM(PAYCode), CASDischargeDate, RTRIM(HOSName), RTRIM(MBMGroupCompany),
            RTRIM(INSCode), RTRIM(CASClaimability), RTRIM(HLTCode), RTRIM(INVPayTo), RTRIM(MBMBTBG)
        FROM genisis.WS_PendingBordxIB WHERE CASNumber = @cas AND ClaimVersion = @version AND ClaimStatus <> N'R'
    ) p
    INNER JOIN genisis.WS_Claim c ON c.CASNumber = p.CaseNo AND c.ClaimVersion = p.ClaimVersion;
END
GO

-- GenerateCLMBordxNo: <payor health type><payor code><7-digit sequence per payor code>. The number is taken atomically.
CREATE OR ALTER PROCEDURE genisis.CaseListing_NextBordxNo
    @pay nvarchar(8)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @hlt nvarchar(4) = (SELECT RTRIM(HLTCode) FROM dbo.PAY WHERE PAYCode = @pay);
    DECLARE @seq nvarchar(20);
    UPDATE genisis.WS_BordxSEQ WITH (UPDLOCK, ROWLOCK)
    SET @seq = CLMBordxNo = RIGHT(N'0000000' + CAST(CAST(CLMBordxNo AS int) + 1 AS nvarchar(10)), 7)
    WHERE PAYCode = @pay;
    IF @@ROWCOUNT = 0
    BEGIN
        SET @seq = N'0000001';
        INSERT genisis.WS_BordxSEQ (PAYCode, CLMBordxNo, HLTCode) VALUES (@pay, @seq, @hlt);
    END
    SELECT ISNULL(@hlt, N'') + @pay + @seq AS BordxNo;
END
GO

-- UpdateRecord: mark one worksheet as bordxed (claim, invoice, case pending flags) and append the "BORDX DONE" line to the
-- case remarks. The remark goes into the first remarks page that has room, following the desktop's page order
-- (CASRemarks.CASORemarks, CASORemarksTwo, CASRemarksTwo.CASORemarks, CASORemarksTwo, CASRemarkThree.CASRemarks, CASRemarksFive);
-- the lengths decide the page exactly as UpdateRecord did.
CREATE OR ALTER PROCEDURE genisis.CaseListing_Apply
    @cas nvarchar(30), @version int, @bordxNo nvarchar(40), @userCode nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today datetime = CAST(CAST(GETDATE() AS date) AS datetime);

    UPDATE genisis.WS_Claim SET BordxRefNo = @bordxNo, CLMBordxDate = @today, ClaimBordxStatus = 1, CLMPending = 'N'
    WHERE CASNumber = @cas AND ClaimVersion = @version;
    UPDATE genisis.WS_ClaimInvoice SET INVBordxRefNo = @bordxNo, INVBordxed = 1
    WHERE CASNumber = @cas AND INVWSversion = CAST(@version AS nvarchar(4));
    UPDATE dbo.CAS SET CASPending = 'N' WHERE CASNumber = @cas;
    UPDATE genisis.Maint_CASCrossReference SET CASPending = 'N' WHERE CASNumber = @cas;

    DECLARE @add nvarchar(max) = N'~' + NCHAR(10) + CONVERT(nvarchar(10), GETDATE(), 103) + N'         BORDX DONE BY ' + @userCode
        + N' -  ' + @bordxNo + REPLICATE(N' ', 44) + @userCode + N' ';

    DECLARE @l1 int = 0, @l2 int = 0, @l3 int = 0, @l4 int = 0, @l5 int = 0;
    SELECT @l1 = LEN(ISNULL(CASORemarks, N'') + N'x') - 1, @l2 = LEN(ISNULL(CASORemarksTwo, N'') + N'x') - 1 FROM dbo.CASRemarks WHERE CASNumber = @cas;
    SELECT @l3 = LEN(ISNULL(CASORemarks, N'') + N'x') - 1, @l4 = LEN(ISNULL(CASORemarksTwo, N'') + N'x') - 1 FROM dbo.CASRemarksTwo WHERE CASNumber = @cas;
    SELECT @l5 = LEN(ISNULL(CASRemarks, N'') + N'x') - 1 FROM dbo.CASRemarkThree WHERE CASNumber = @cas;

    IF @l5 >= 1800 AND @l5 <= 3500
        UPDATE dbo.CASRemarkThree SET CASRemarksFive = ISNULL(CASRemarksFive, N'') + @add WHERE CASNumber = @cas;
    ELSE IF (@l5 <> 0 AND @l5 <= 1800) OR @l4 >= 1800
        UPDATE dbo.CASRemarkThree SET CASRemarks = ISNULL(CASRemarks, N'') + @add WHERE CASNumber = @cas;
    ELSE IF (@l4 <> 0 AND @l4 <= 1800) OR @l3 >= 1800
        UPDATE dbo.CASRemarksTwo SET CASORemarksTwo = ISNULL(CASORemarksTwo, N'') + @add WHERE CASNumber = @cas;
    ELSE IF (@l3 <> 0 AND @l3 <= 1800) OR @l2 >= 1800
        UPDATE dbo.CASRemarksTwo SET CASORemarks = ISNULL(CASORemarks, N'') + @add WHERE CASNumber = @cas;
    ELSE IF (@l2 <> 0 AND @l2 <= 1800) OR @l1 >= 1800
        UPDATE dbo.CASRemarks SET CASORemarksTwo = ISNULL(CASORemarksTwo, N'') + @add WHERE CASNumber = @cas;
    ELSE IF @l1 <> 0 AND @l1 < 1800
        UPDATE dbo.CASRemarks SET CASORemarks = ISNULL(CASORemarks, N'') + @add WHERE CASNumber = @cas;
END
GO

-- SaveCLMBordx: the Bordx row. Status C when the claims are paid to the hospital (INVPayTo H), R otherwise.
CREATE OR ALTER PROCEDURE genisis.CaseListing_SaveBordx
    @bordxNo nvarchar(40), @amount money, @claims int, @status char(1), @userCode nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT genisis.WS_Bordx (BordxRefNo, BordxPAYCode, BordxDate, BordxBy, BordxClaimAmt, BordxNoClaims, BordxStatus)
    VALUES (@bordxNo, SUBSTRING(@bordxNo, 2, 2), CAST(CAST(GETDATE() AS date) AS datetime), @userCode, @amount, @claims, @status);
END
GO

-- ===== main/220_case_ascii.sql =====
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

-- ===== main/230_worksheet_tracking.sql =====
-- Worksheet > Worksheet Tracking (legacy FrmWorksheetTracking). The desktop built a WHERE clause as text (strAppend) and
-- sent it to dbo.WorksheetTracking in HISWorkSheet, which ran it as dynamic SQL; here every criterion is a typed parameter.
-- Reads the worksheet tracking view through genisis.WS_WKSTracking (HISWorkSheet.dbo.VIEW_WKSTrackingNew).

-- SearchRecord + WorksheetTrackingListing(2). @scan = 1: one case number; 0: a case number range. Every "from"/"to" pair is
-- inclusive and ignored when NULL; an "empty" flag keeps only rows where the column is NULL or blank (a date column is
-- blank at 1900-01-01, as the desktop's  = ''  test saw it). The desktop's Ins Type filter read controls that do not exist
-- on the form, so it never filtered; here @insFrom / @insTo really filter.
CREATE OR ALTER PROCEDURE genisis.WorksheetTracking_Search
    @scan bit, @wksFrom nvarchar(30) = NULL, @wksTo nvarchar(30) = NULL, @versionFrom int = NULL, @versionTo int = NULL,
    @payor nvarchar(4) = NULL, @icFrom nvarchar(60) = NULL, @icTo nvarchar(60) = NULL, @icEmpty bit = 0,
    @groupCompany nvarchar(300) = NULL, @claimability char(1) = NULL, @pending char(1) = NULL, @pendingEmpty bit = 0,
    @planFrom nvarchar(8) = NULL, @planTo nvarchar(8) = NULL,
    @approvedFrom money = NULL, @approvedTo money = NULL, @payMFrom money = NULL, @payMTo money = NULL,
    @payHFrom money = NULL, @payHTo money = NULL,
    @policyFrom nvarchar(50) = NULL, @policyTo nvarchar(50) = NULL, @memberFrom nvarchar(30) = NULL, @memberTo nvarchar(30) = NULL,
    @insFrom nvarchar(4) = NULL, @insTo nvarchar(4) = NULL,
    @bordxFrom nvarchar(40) = NULL, @bordxTo nvarchar(40) = NULL, @bordxEmpty bit = 0,
    @dpoFrom datetime = NULL, @dpoTo datetime = NULL, @dpoEmpty bit = 0,
    @doaFrom datetime = NULL, @doaTo datetime = NULL, @doaEmpty bit = 0,
    @dodFrom datetime = NULL, @dodTo datetime = NULL, @dodEmpty bit = 0,
    @firstFrom nvarchar(20) = NULL, @firstTo nvarchar(20) = NULL, @firstEmpty bit = 0,
    @finalFrom nvarchar(20) = NULL, @finalTo nvarchar(20) = NULL, @finalEmpty bit = 0,
    @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(PAYCode) AS Payor, RTRIM(CASNumber) AS CaseNo, ClaimVersion, RTRIM(ClaimStatus) AS WksStatus,
        RTRIM(CLMPending) AS Pending, CLMTotalApproved AS ApprovedAmt, CLMPayableByMedix2H AS PayableToHospital,
        CLMPayableByMedix2M AS PayableToMember, CaseRegDate AS Dor, CASDateAdmitted AS Doa, CASDischargeDate AS Dod,
        ClaimCloseDate AS DClose, CASTokenDate AS Dpo, CLMTokenDate AS DtaC, RTRIM(PatientName) AS Patient, RTRIM(HOSName) AS Hospital,
        NoofDay AS DaysOpen, RTRIM(BordxRefNo) AS BordxNo, BordxClaimAmt AS BordxAmt, BordxDate, RTRIM(DebitCreditRefNo) AS DebitCreditRefNo,
        RTRIM(FirstDiag) AS FirstDiag, RTRIM(FinalDiag) AS FinalDiag, RTRIM(CASAdmissionReason) AS AdmissionReason,
        CAST(ISNULL(ClaimBordxStatus, 0) AS bit) AS Bordxed
    FROM genisis.WS_WKSTracking
    WHERE (@payor IS NULL OR PAYCode = @payor)
      AND (@scan = 0 OR @wksFrom IS NULL OR CASNumber = @wksFrom)
      AND (@scan = 1 OR @wksFrom IS NULL OR CASNumber >= @wksFrom)
      AND (@scan = 1 OR @wksTo IS NULL OR CASNumber <= @wksTo)
      AND (@versionFrom IS NULL OR ClaimVersion >= @versionFrom) AND (@versionTo IS NULL OR ClaimVersion <= @versionTo)
      AND (@icFrom IS NULL OR ICno >= @icFrom) AND (@icTo IS NULL OR ICno <= @icTo)
      AND (@icEmpty = 0 OR ICno IS NULL)
      AND (@groupCompany IS NULL OR MBMGroupCompany = @groupCompany)
      AND (@claimability IS NULL OR CASClaimability = @claimability)
      AND (@pending IS NULL OR CLMPending = @pending)
      AND (@pendingEmpty = 0 OR CLMPending IS NULL OR CLMPending = '')
      AND (@planFrom IS NULL OR PLNCode >= @planFrom) AND (@planTo IS NULL OR PLNCode <= @planTo)
      AND (@approvedFrom IS NULL OR CLMTotalApproved >= @approvedFrom) AND (@approvedTo IS NULL OR CLMTotalApproved <= @approvedTo)
      AND (@payMFrom IS NULL OR CLMPayableByMedix2M >= @payMFrom) AND (@payMTo IS NULL OR CLMPayableByMedix2M <= @payMTo)
      AND (@payHFrom IS NULL OR CLMPayableByMedix2H >= @payHFrom) AND (@payHTo IS NULL OR CLMPayableByMedix2H <= @payHTo)
      AND (@policyFrom IS NULL OR MBMPolicyNo >= @policyFrom) AND (@policyTo IS NULL OR MBMPolicyNo <= @policyTo)
      AND (@memberFrom IS NULL OR MBMNumber >= @memberFrom) AND (@memberTo IS NULL OR MBMNumber <= @memberTo)
      AND (@insFrom IS NULL OR INSCode >= @insFrom) AND (@insTo IS NULL OR INSCode <= @insTo)
      AND (@bordxFrom IS NULL OR BordxRefNo >= @bordxFrom) AND (@bordxTo IS NULL OR BordxRefNo <= @bordxTo)
      AND (@bordxEmpty = 0 OR BordxRefNo IS NULL OR BordxRefNo = '')
      AND (@dpoFrom IS NULL OR CASTokenDate >= @dpoFrom) AND (@dpoTo IS NULL OR CASTokenDate <= @dpoTo)
      AND (@doaFrom IS NULL OR CASDateAdmitted >= @doaFrom) AND (@doaTo IS NULL OR CASDateAdmitted <= @doaTo)
      AND (@dodFrom IS NULL OR CASDischargeDate >= @dodFrom) AND (@dodTo IS NULL OR CASDischargeDate <= @dodTo)
      AND (@dpoEmpty = 0 OR CASTokenDate IS NULL OR CASTokenDate = '19000101')
      AND (@doaEmpty = 0 OR CASDateAdmitted IS NULL OR CASDateAdmitted = '19000101')
      AND (@dodEmpty = 0 OR CASDischargeDate IS NULL OR CASDischargeDate = '19000101')
      AND (@firstFrom IS NULL OR FirstDiag >= @firstFrom) AND (@firstTo IS NULL OR FirstDiag <= @firstTo)
      AND (@firstEmpty = 0 OR FirstDiag IS NULL OR FirstDiag = '')
      AND (@finalFrom IS NULL OR FinalDiag >= @finalFrom) AND (@finalTo IS NULL OR FinalDiag <= @finalTo)
      AND (@finalEmpty = 0 OR FinalDiag IS NULL OR FinalDiag = '')
    ORDER BY CASNumber, ClaimVersion;
END
GO

-- ChkGrpCom_Click: the group companies that have worksheets for a payor (the desktop listed one entry per worksheet).
CREATE OR ALTER PROCEDURE genisis.WorksheetTracking_GroupCompanies
    @payor nvarchar(4)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT RTRIM(MBMGroupCompany) AS GroupCompany
    FROM genisis.WS_WKSTracking
    WHERE PAYCode = @payor AND MBMGroupCompany IS NOT NULL AND RTRIM(MBMGroupCompany) <> N''
    ORDER BY RTRIM(MBMGroupCompany);
END
GO

-- UpdateCLM: sets the pending status of one worksheet and clears the pending flag of its case, in the four places the
-- desktop wrote. The desktop only stopped a bordxed worksheet from being ticked; here the procedure refuses it too.
-- Result: 0 done, 1 worksheet not found, 2 refused (already bordxed).
CREATE OR ALTER PROCEDURE genisis.WorksheetTracking_Update
    @cas nvarchar(30), @version int, @date datetime, @status char(1), @userCode nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @bordxed bit;
    SELECT @bordxed = ClaimBordxStatus FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @version;
    IF @@ROWCOUNT = 0
    BEGIN
        SELECT 1 AS Result;
        RETURN;
    END
    IF ISNULL(@bordxed, 0) = 1
    BEGIN
        SELECT 2 AS Result;
        RETURN;
    END

    UPDATE genisis.WS_Claim SET CLMPendingDate = @date, CLMPending = @status, CLMPendingUser = @userCode
    WHERE CASNumber = @cas AND ClaimVersion = @version;
    UPDATE genisis.WS_ClaimInvoice SET CLMInvPending = @status
    WHERE CASNumber = @cas AND INVWSversion = CAST(@version AS nvarchar(4));
    UPDATE dbo.CAS SET CASPending = 'N' WHERE CASNumber = @cas;
    UPDATE genisis.Maint_CASCrossReference SET CASPending = 'N' WHERE CASNumber = @cas;
    SELECT 0 AS Result;
END
GO

-- ComboListing (SearchINS): the insured types of the Ins Type drop-downs.
CREATE OR ALTER PROCEDURE genisis.Worksheet_InsuredTypes
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(INSCode) AS Code, RTRIM(INSDescription) AS Name FROM dbo.INS ORDER BY INSCode;
END
GO

-- ===== main/240_worksheet_send_to_accounts.sql =====
-- Worksheet > Worksheet Send to A/C (legacy FrmWksSendToAC, "Claims Send To A/C"). The desktop built the WHERE clause as text
-- and ran it on dbo.VIEW_WKS2AC; here every criterion is a typed parameter. Reads genisis.WS_WKS2AC (HISWorkSheet.dbo.VIEW_WKS2AC).

-- SearchRecord + WksDeliveryListing(2). @scan = 1: one case number; 0: a range. @tokenStatus: '0' both (the desktop's
-- "Both" keeps status 0 and 1 only), '1' passed, '2' not yet passed, NULL no filter. Dates and amounts are inclusive.
-- The view repeats a worksheet once per hospital / member payment; the desktop listed (and added up) every repeat and
-- sent a ticked worksheet twice. Here a worksheet is one row, showing its latest cheque dates.
CREATE OR ALTER PROCEDURE genisis.WorksheetAccounts_Search
    @scan bit, @wksFrom nvarchar(30) = NULL, @wksTo nvarchar(30) = NULL, @payor nvarchar(4) = NULL,
    @openFrom datetime = NULL, @openTo datetime = NULL, @amountFrom money = NULL, @amountTo money = NULL,
    @sentFrom datetime = NULL, @sentTo datetime = NULL, @tokenStatus char(1) = NULL,
    @bordxFrom nvarchar(40) = NULL, @bordxTo nvarchar(40) = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(PAYCode) AS Payor, RTRIM(CASNumber) AS CaseNo, ClaimVersion, ClaimOpenDate AS WksDate,
        CLMTotalApproved AS WksAmount, CLMTokenDate AS DateToAc, RTRIM(CLMUserToken) AS PassedBy,
        CAST(ISNULL(CLMTokenStatus, 0) AS bit) AS Sent, MAX(PVHOSChequeDate) AS HosChequeDate, MAX(PVChequeDate) AS MbmChequeDate,
        RTRIM(BordxRefNo) AS BordxNo
    FROM genisis.WS_WKS2AC
    WHERE (@payor IS NULL OR PAYCode = @payor)
      AND (@scan = 0 OR @wksFrom IS NULL OR CASNumber = @wksFrom)
      AND (@scan = 1 OR @wksFrom IS NULL OR CASNumber >= @wksFrom)
      AND (@scan = 1 OR @wksTo IS NULL OR CASNumber <= @wksTo)
      AND (@openFrom IS NULL OR ClaimOpenDate >= @openFrom) AND (@openTo IS NULL OR ClaimOpenDate <= @openTo)
      AND (@amountFrom IS NULL OR CLMTotalApproved >= @amountFrom) AND (@amountTo IS NULL OR CLMTotalApproved <= @amountTo)
      AND (@sentFrom IS NULL OR CLMTokenDate >= @sentFrom) AND (@sentTo IS NULL OR CLMTokenDate <= @sentTo)
      AND (@tokenStatus IS NULL
           OR (@tokenStatus = '0' AND CLMTokenStatus IN (0, 1))
           OR (@tokenStatus = '1' AND CLMTokenStatus = 1)
           OR (@tokenStatus = '2' AND CLMTokenStatus = 0))
      AND (@bordxFrom IS NULL OR BordxRefNo >= @bordxFrom) AND (@bordxTo IS NULL OR BordxRefNo <= @bordxTo)
    GROUP BY PAYCode, CASNumber, ClaimVersion, ClaimOpenDate, CLMTotalApproved, CLMTokenDate, CLMUserToken, CLMTokenStatus, BordxRefNo
    ORDER BY CASNumber, ClaimVersion;
END
GO

-- UpdateSentAC: marks one worksheet as sent to accounts on @date and appends the "SENT TO ACCOUNT" line to the case remarks.
-- Like the desktop (ChkStr), the remark line and the whole remarks page it is added to are trimmed and upper-cased, and the page
-- is chosen from the lengths of the six remark pages exactly as that form did (it never measured the last page, so the
-- first rule only looks at page 5). The desktop only stopped a worksheet already sent from being ticked; here the procedure
-- refuses it too. Result: 0 done, 1 worksheet not found, 2 refused (already sent).
CREATE OR ALTER PROCEDURE genisis.WorksheetAccounts_Send
    @cas nvarchar(30), @version int, @date datetime, @userCode nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @sent bit;
    SELECT @sent = CLMTokenStatus FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @version;
    IF @@ROWCOUNT = 0
    BEGIN
        SELECT 1 AS Result;
        RETURN;
    END
    IF ISNULL(@sent, 0) = 1
    BEGIN
        SELECT 2 AS Result;
        RETURN;
    END

    UPDATE genisis.WS_Claim SET CLMTokenDate = @date, CLMTokenStatus = 1, CLMUserToken = @userCode
    WHERE CASNumber = @cas AND ClaimVersion = @version;

    DECLARE @add nvarchar(max) = UPPER(LTRIM(RTRIM(N'~' + NCHAR(10) + CONVERT(nvarchar(10), GETDATE(), 103) + N'         FILE  -  '
        + RTRIM(@cas) + N'-' + CAST(@version AS nvarchar(10)) + N' SENT TO ACCOUNT ON ' + CONVERT(nvarchar(10), @date, 103)
        + REPLICATE(N' ', 24) + @userCode + N' ')));

    DECLARE @p1 nvarchar(max), @p2 nvarchar(max), @p3 nvarchar(max), @p4 nvarchar(max), @p5 nvarchar(max), @p6 nvarchar(max);
    SELECT @p1 = CASORemarks, @p2 = CASORemarksTwo FROM dbo.CASRemarks WHERE CASNumber = @cas;
    SELECT @p3 = CASORemarks, @p4 = CASORemarksTwo FROM dbo.CASRemarksTwo WHERE CASNumber = @cas;
    SELECT @p5 = CASRemarks, @p6 = CASRemarksFour FROM dbo.CASRemarkThree WHERE CASNumber = @cas;

    -- Len() of a page that holds more than spaces; 0 for a missing or blank page.
    DECLARE @l1 int = CASE WHEN LTRIM(RTRIM(ISNULL(@p1, N''))) <> N'' THEN LEN(@p1 + N'x') - 1 ELSE 0 END;
    DECLARE @l2 int = CASE WHEN LTRIM(RTRIM(ISNULL(@p2, N''))) <> N'' THEN LEN(@p2 + N'x') - 1 ELSE 0 END;
    DECLARE @l3 int = CASE WHEN LTRIM(RTRIM(ISNULL(@p3, N''))) <> N'' THEN LEN(@p3 + N'x') - 1 ELSE 0 END;
    DECLARE @l4 int = CASE WHEN LTRIM(RTRIM(ISNULL(@p4, N''))) <> N'' THEN LEN(@p4 + N'x') - 1 ELSE 0 END;
    DECLARE @l5 int = CASE WHEN LTRIM(RTRIM(ISNULL(@p5, N''))) <> N'' THEN LEN(@p5 + N'x') - 1 ELSE 0 END;

    IF @l5 > 1800
        UPDATE dbo.CASRemarkThree SET CASRemarksFour = UPPER(LTRIM(RTRIM(ISNULL(@p6, N'') + @add))) WHERE CASNumber = @cas;
    ELSE IF (@l5 <> 0 AND @l5 < 1800) OR @l4 > 1800
        UPDATE dbo.CASRemarkThree SET CASRemarks = UPPER(LTRIM(RTRIM(ISNULL(@p5, N'') + @add))) WHERE CASNumber = @cas;
    ELSE IF (@l4 <> 0 AND @l4 < 1800) OR @l3 > 1800
        UPDATE dbo.CASRemarksTwo SET CASORemarksTwo = UPPER(LTRIM(RTRIM(ISNULL(@p4, N'') + @add))) WHERE CASNumber = @cas;
    ELSE IF (@l3 <> 0 AND @l3 < 1800) OR @l2 > 1800
        UPDATE dbo.CASRemarksTwo SET CASORemarks = UPPER(LTRIM(RTRIM(ISNULL(@p3, N'') + @add))) WHERE CASNumber = @cas;
    ELSE IF (@l2 <> 0 AND @l2 < 1800) OR @l1 > 1800
        UPDATE dbo.CASRemarks SET CASORemarksTwo = UPPER(LTRIM(RTRIM(ISNULL(@p2, N'') + @add))) WHERE CASNumber = @cas;
    ELSE IF @l1 <> 0 AND @l1 < 1800
        UPDATE dbo.CASRemarks SET CASORemarks = UPPER(LTRIM(RTRIM(ISNULL(@p1, N'') + @add))) WHERE CASNumber = @cas;
    ELSE IF @l1 = 0
        UPDATE dbo.CASRemarks SET CASORemarks = UPPER(LTRIM(RTRIM(ISNULL(@p1, N'') + @add))) WHERE CASNumber = @cas;

    SELECT 0 AS Result;
END
GO

-- ===== main/250_worksheet_send_to_ar.sql =====
-- Worksheet > Worksheet Send to A/R (legacy FrmARRecFrCLM, "Date Send to Audit Review"). The desktop form also read CASOthers.CASDTC
-- and copied the invoice number, date and amount into CASOthers.CASINVNo / InvDate / CASInvAmt; those four columns do not exist
-- in the database, so only the three that do (CASDRC, CASRecBy, CASRecStatus) are read and written here.

-- SearchRecord + WksDeliveryListing(2). @scan = 1: one case number; 0: a range. @status: '0' both (status 0, 1 or not set),
-- '1' passed, '2' not yet passed, NULL no filter. The sent dates are inclusive.
CREATE OR ALTER PROCEDURE genisis.WorksheetAr_Search
    @scan bit, @wksFrom nvarchar(30) = NULL, @wksTo nvarchar(30) = NULL,
    @sentFrom datetime = NULL, @sentTo datetime = NULL, @status char(1) = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(CASNumber) AS CaseNo, CASDRC AS DateSent, RTRIM(CASRecBy) AS PassedBy, RTRIM(CASRecStatus) AS Status
    FROM dbo.CASOthers
    WHERE (@scan = 0 OR @wksFrom IS NULL OR CASNumber = @wksFrom)
      AND (@scan = 1 OR @wksFrom IS NULL OR CASNumber >= @wksFrom)
      AND (@scan = 1 OR @wksTo IS NULL OR CASNumber <= @wksTo)
      AND (@sentFrom IS NULL OR CASDRC >= @sentFrom) AND (@sentTo IS NULL OR CASDRC <= @sentTo)
      AND (@status IS NULL
           OR (@status = '0' AND (CASRecStatus IN ('0', '1') OR CASRecStatus IS NULL))
           OR (@status = '1' AND CASRecStatus = '1')
           OR (@status = '2' AND CASRecStatus = '0'))
    ORDER BY CASNumber;
END
GO

-- UpdateSentCLM: marks one case as sent to audit review today and by whom. Result: 0 done, 1 case not found.
CREATE OR ALTER PROCEDURE genisis.WorksheetAr_Send
    @cas nvarchar(30), @userCode nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.CASOthers SET CASDRC = CAST(CAST(GETDATE() AS date) AS datetime), CASRecStatus = '1', CASRecBy = @userCode
    WHERE CASNumber = @cas;
    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END AS Result;
END
GO

-- ===== main/260_worksheet_enquiry.sql =====
-- Worksheet > Worksheet Enquiry (frmWorksheetEnq): read-only enquiry on a saved worksheet. The 29 benefit slots of ClaimActual /
-- ClaimApprove / ClaimHos / ClaimMember / ClaimLimit use the row -> column map the desktop's save routines (saveActual, saveApprove,
-- saveHos, saveMember, saveLimit) write: row 1 Room & Board ... row 29 Service Tax - Lodger. The amounts are stored as the text the
-- grid showed ("1,200.00", "-", "(50.00)"), hence Worksheet_Amount.

CREATE OR ALTER FUNCTION genisis.Worksheet_Amount (@s nvarchar(60))
RETURNS decimal(18, 2)
AS
BEGIN
    DECLARE @t nvarchar(60) = REPLACE(REPLACE(LTRIM(RTRIM(ISNULL(@s, N''))), N',', N''), N' ', N'');
    DECLARE @neg bit = CASE WHEN LEFT(@t, 1) = N'(' AND RIGHT(@t, 1) = N')' THEN 1 ELSE 0 END;
    IF @neg = 1 SET @t = SUBSTRING(@t, 2, LEN(@t) - 2);
    RETURN CASE
        WHEN @t = N'' OR LEN(@t) > 15 OR @t LIKE N'%[^0-9.-]%' OR @t NOT LIKE N'%[0-9]%'
          OR @t LIKE N'%.%.%' OR @t LIKE N'%-%-%' OR (@t LIKE N'%-%' AND LEFT(@t, 1) <> N'-') THEN 0
        WHEN @neg = 1 THEN -ABS(CAST(@t AS decimal(18, 2)))
        ELSE CAST(@t AS decimal(18, 2))
    END;
END
GO

-- cmdSearchCase_Click (frmCASList): the cases to pick from, found by case / member number prefix, patient name, I/C or policy number.
-- Versions is how many worksheet versions the case has (0: nothing to enquire on yet).
CREATE OR ALTER PROCEDURE genisis.WorksheetEnquiry_Cases
    @max int, @by nvarchar(20), @q nvarchar(100), @prefix nvarchar(200), @contains nvarchar(200)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(c.CASNumber) AS CaseNo, RTRIM(c.MBMNumber) AS MemberNo, RTRIM(COALESCE(cp.MBMCName, x.MBMName)) AS PatientName,
        RTRIM(h.HOSName) AS Hospital, c.CASDateAdmitted AS Admitted, c.CASDischargeDate AS Discharged, RTRIM(x.MBMPolicyNo) AS PolicyNo,
        RTRIM(x.PAYCode) AS Payor, RTRIM(c.CASStatus) AS CaseStatus, RTRIM(c.CASClaimability) AS Claimability,
        (SELECT COUNT(*) FROM genisis.WS_Claim w WHERE w.CASNumber = c.CASNumber) AS Versions
    FROM dbo.CAS c WITH (NOLOCK)
    LEFT JOIN genisis.Maint_MBMCrossReference x ON c.MBMNumber = x.MBMNumber
    LEFT JOIN dbo.MBMCoveredPersons cp WITH (NOLOCK) ON c.MBMNumber = cp.MBMCNumber AND c.MBMPatientCovID = cp.MBMCCoverID
    LEFT JOIN dbo.HOS h WITH (NOLOCK) ON c.CASHOSCode = h.HOSCode
    WHERE (@by = N'number' AND c.CASNumber LIKE @prefix)
       OR (@by = N'member' AND c.MBMNumber LIKE @prefix)
       OR (@by = N'patient' AND (cp.MBMCName LIKE @contains OR x.MBMName LIKE @contains))
       OR (@by = N'ic' AND (x.MBMIcBcPp = @q OR REPLACE(x.MBMIcBcPp, '-', '') = REPLACE(@q, '-', '')))
       OR (@by = N'policy' AND x.MBMPolicyNo LIKE @prefix)
    ORDER BY c.CaseRegDate DESC, c.CASNumber
    OPTION (RECOMPILE);
END
GO

-- The versions of a case's worksheet (cboVersion lists each as "n" and "Mn", then "Master").
CREATE OR ALTER PROCEDURE genisis.WorksheetEnquiry_Versions
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ClaimVersion, RTRIM(ClaimStatus) AS ClaimStatus, RTRIM(CLMWSStatus) AS WsStatus, CAST(ISNULL(ClaimBordxStatus, 0) AS bit) AS Bordxed
    FROM genisis.WS_Claim WHERE CASNumber = @cas ORDER BY ClaimVersion;
END
GO

-- cmdDisplay_Click: the case, member, policy and worksheet header of one version (SearchCasMBMClaim), the policy status text and the
-- live limits. No row when the case has no such worksheet or its case / member is missing (the desktop query joins them: "Record not found!").
-- Balance limit (txtBalLimit), for members without a top-up: the member's available limit; a "Q" member of a supplementary plan with
-- limit status B / C and a dependant patient uses the covered person's limit instead (the principal's "01" row for B, the patient's for C).
-- Top-up policies add their own balance in the desktop; that figure is shown from the saved worksheet on the Topup tab instead.
CREATE OR ALTER PROCEDURE genisis.WorksheetEnquiry_Header
    @cas nvarchar(15), @version int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @mbm nvarchar(20), @cov nvarchar(4), @plan nvarchar(10), @ins nvarchar(4), @admit datetime, @type nvarchar(4),
        @lifeStatus nvarchar(2), @guaRenewal nvarchar(2), @supp nvarchar(2), @available money, @lifetime money, @found int;

    SELECT TOP (1) @mbm = RTRIM(MBMNumber), @cov = RTRIM(MBMPatientCovID), @ins = RTRIM(INSCode), @admit = CASDateAdmitted, @type = RTRIM(MBMMemberType),
        @lifeStatus = RTRIM(PLNLifeTimeStatus), @guaRenewal = RTRIM(MBMGuaRenewal),
        @plan = RTRIM(CASE WHEN RTRIM(MBMPatientCovID) <> N'00' AND ISNULL(RTRIM(MBMCPLNCode), N'') <> N'' THEN MBMCPLNCode ELSE PLNCode END)
    FROM dbo.VIEW_Claim_MBM_Case_worksheetAdjustment WHERE CASNumber = @cas AND ClaimVersion = @version AND MBMNumber IS NOT NULL;
    SET @found = @@ROWCOUNT;
    IF @found = 0 RETURN;

    SELECT TOP (1) @supp = RTRIM(SuppLimitStatus) FROM dbo.AnnualLimit
    WHERE PLNCode = @plan AND INSCode = @ins AND AnnualEffDate <= @admit ORDER BY AnnualEffDate DESC;

    SELECT TOP (1) @available = MBMAvailableLimit FROM dbo.MBM WHERE MBMNumber = @mbm;
    IF @type = N'Q' AND @cov <> N'00' AND @supp IN (N'B', N'C')
        SELECT TOP (1) @available = MBMCAvailableLimit FROM dbo.MBMCoveredPersons
        WHERE MBMCNumber = @mbm AND MBMCCoverID = CASE WHEN @supp = N'B' THEN N'01' ELSE @cov END;

    IF @lifeStatus = N'Y' AND @guaRenewal = N'3'
        SELECT TOP (1) @lifetime = MBMLifeTimeLmt FROM dbo.MBMLifeTime
        WHERE MBMNumber = @mbm AND MBMCovID = CASE WHEN @type = N'Q' AND @supp = N'B' AND @cov <> N'00' THEN N'01'
                                                   WHEN @type = N'Q' AND @supp = N'C' AND @cov <> N'00' THEN @cov ELSE N'00' END;

    SELECT TOP (1)
        RTRIM(v.CASNumber) AS CaseNo, v.ClaimVersion, RTRIM(v.MBMNumber) AS MemberNo, RTRIM(v.MBMPatientCovID) AS PatientCovId,
        RTRIM(v.MBMName) AS MemberName,
        CASE WHEN RTRIM(v.MBMPatientCovID) <> N'00' AND ISNULL(RTRIM(v.MBMCName), N'') <> N'' THEN RTRIM(v.MBMCName) ELSE RTRIM(v.MBMName) END AS PatientName,
        RTRIM(v.MBMPolicyNo) AS PolicyNo, RTRIM(v.PAYCode) AS Payor, RTRIM(v.PAYCompanyName) AS PayorName,
        @plan AS PlanCode, RTRIM(v.INSCode) AS InsCode, RTRIM(v.HLTCode) AS HltCode, RTRIM(v.GRPCompany) AS GroupCompany,
        v.MBMPayorEffDate AS PayorEffDate, v.MBMPayorExpDate AS PayorExpDate, v.MBMPolEffDate AS PolicyEffDate,
        RTRIM(v.MBMStatus) AS PolicyStatusCode, RTRIM(ps.PStatusDesc) AS PolicyStatus, RTRIM(v.MBMExpiredStatus) AS ExpiredStatus,
        RTRIM(v.CASStatus) AS CaseStatus, RTRIM(v.CASClaimability) AS Claimability, RTRIM(v.PayTypeCode) AS PayType,
        RTRIM(v.CASHOSCode) AS HosCode, RTRIM(v.HOSName) AS Hospital, v.CASDateAdmitted AS Admitted, v.CASDischargeDate AS Discharged,
        RTRIM(v.ClaimStatus) AS ClaimStatus, RTRIM(v.CLMWSStatus) AS WsStatus, v.ClaimOpenDate AS WorksheetOpened,
        CAST(ISNULL(v.ClaimBordxStatus, 0) AS bit) AS Bordxed, RTRIM(v.BordxRefNo) AS BordxRefNo, RTRIM(v.CLMExGratia) AS ExGratia,
        RTRIM(v.CLMMHBordx) AS BordxInd, RTRIM(v.CLMLinkCase) AS LinkCase, v.CLMLinkVersion AS LinkVersion, RTRIM(v.MBMGLIssuance) AS GlIssuance,
        RTRIM(v.MBMTopupNumber) AS TopupNumber, RTRIM(v.MBMTopupInd) AS TopupInd,
        @available AS AvailableLimit, @lifetime AS LifetimeLimit, v.CASTakeOverAmt AS TakeOverLimit,
        v.CLMLimitExceeded AS LimitExceeded, v.CLMPreviousBalance AS PreviousBalance, v.CLMBalanceLimit AS BalanceAfterClaim,
        v.CLMLifetimeBal AS LifetimeBalance, v.CLMTotalApproved AS TotalApproved, v.CLMFinalApprove AS FinalApprove,
        RTRIM(v.LimitCollectedBy) AS LimitCollectedBy, RTRIM(v.ExcessCollectedBy) AS ExcessCollectedBy,
        CASE WHEN RTRIM(v.CLMNonPayment) = N'Y' THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS NonPayment,
        v.CLMExGratiaAmt AS ExGratiaAmount, v.CLMCaseMgmtFees AS ProcessingFee, v.CLMMajorMedFee AS MajorMedicalFee, v.CLMMainTUAmt AS MainTopupAmount,
        v.CLMComApp AS CompanyApproved, v.CLMTopUpApp AS TopupApproved, v.CLMPreviousNewLimit AS TopupPreviousBalance, v.CLMPreviousUndExcess AS UnderwritingExcess,
        RTRIM(v.DebitCreditRefNo) AS DebitCreditRefNo, v.DebitCreditDate, RTRIM(v.DebitCreditHRefNo) AS HosDebitCreditRefNo, v.DebitCreditHAmt AS HosDebitCreditAmount,
        v.CLMLastDocDate AS LastDocumentDate, RTRIM(v.CLMPayTo) AS PayTo, RTRIM(v.CLMBank) AS Bank, RTRIM(v.CLMAccountNo) AS AccountNo, RTRIM(v.CLMGPName) AS GpName
    FROM dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v
    LEFT JOIN dbo.PolicyStatus ps ON ps.PStatusCode = v.MBMStatus
    WHERE v.CASNumber = @cas AND v.ClaimVersion = @version AND v.MBMNumber IS NOT NULL;
END
GO

-- Display_Diagnosis: the first (preliminary) and final diagnoses of the case, up to three each.
CREATE OR ALTER PROCEDURE genisis.WorksheetEnquiry_Diagnoses
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT d.Source, d.Slot, RTRIM(d.Diagnosis) AS Diagnosis, RTRIM(d.OnsetDate) AS OnsetDate, RTRIM(d.IcdCode) AS IcdCode
    FROM (
        SELECT 1 AS SourceOrder, N'First' AS Source, s.Slot, s.Diagnosis, s.OnsetDate, s.IcdCode
        FROM dbo.DIAFirst f CROSS APPLY (VALUES
            (1, f.DIAPrelimDiag1, f.DIADOS1, f.ICDCode1), (2, f.DIAPrelimDiag2, f.DIADOS2, f.ICDCode2), (3, f.DIAPrelimDiag3, f.DIADOS3, f.ICDCode3)
        ) s(Slot, Diagnosis, OnsetDate, IcdCode) WHERE f.CASNumber = @cas
        UNION ALL
        SELECT 2, N'Final', s.Slot, s.Diagnosis, s.OnsetDate, s.IcdCode
        FROM dbo.DIAFinal f CROSS APPLY (VALUES
            (1, f.DIAFinalDiag1, f.DIADOS1, f.ICDCode1), (2, f.DIAFinalDiag2, f.DIADOS2, f.ICDCode2), (3, f.DIAFinalDiag3, f.DIADOS3, f.ICDCode3)
        ) s(Slot, Diagnosis, OnsetDate, IcdCode) WHERE f.CASNumber = @cas
    ) d
    WHERE LTRIM(RTRIM(ISNULL(d.Diagnosis, N''))) <> N'' OR LTRIM(RTRIM(ISNULL(d.OnsetDate, N''))) <> N'' OR LTRIM(RTRIM(ISNULL(d.IcdCode, N''))) <> N''
    ORDER BY d.SourceOrder, d.Slot;
END
GO

-- ShowInvoice: the invoices of one version (a numeric version) or every accepted invoice of the case (Mn / Master), optionally only the
-- cashless (pay to hospital, 'H') or reimbursement (pay to member, 'M') ones. The last eight columns feed the Reimbursement Not Cover tab.
CREATE OR ALTER PROCEDURE genisis.WorksheetEnquiry_Invoices
    @cas nvarchar(15), @version int = NULL, @payTo char(1) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT INVIndex AS InvIndex, CAST(ISNULL(INVBordxed, 0) AS bit) AS Bordxed, CAST(ISNULL(INVWSed, 0) AS bit) AS WsDone, RTRIM(INVWSversion) AS WsVersion,
        RTRIM(INVNo) AS InvNo, InvDate, INVAmount AS InvAmount, RTRIM(InvPayTo) AS PayTo, RTRIM(INVPayee) AS Payee, RTRIM(ClaimType) AS ClaimType,
        INVFolUpDate AS FollowUpDate, CLMBoardDay AS DailyRb, CLMRB AS RbRate, CLMICUDay AS IcuDays, CLMICU AS IcuRate, CLMServices AS HospitalServices,
        CLMTheatre AS OperatingTheatre, CLMPreHSAmt AS PreHospitalService, CLMPhysician AS PhysicianVisit, CLMPost AS PostHospitalTreatment,
        PreH60 AS PreHospitalOver60, PreGP AS PreHospitalGpFee, preMedic AS PreHospitalMedicine, PreMR AS PreHospitalMedicalReport, PreSpec AS PreHospitalSpecialistFee,
        PostHosp AS PostHospitalOver31Days, PostNotSame AS PostHospitalDifferentPhysician, Others AS NotCoveredOthers
    FROM genisis.WS_CASInvoice
    WHERE CASNumber = @cas
      AND ((@version IS NOT NULL AND INVWSversion = CAST(@version AS nvarchar(10))) OR (@version IS NULL AND ClaimStatus = N'C'))
      AND (@payTo IS NULL OR InvPayTo = @payTo)
    ORDER BY INVIndex;
END
GO

-- The benefit rows of the worksheet grid (25 benefits, then Others - No Co-Pay and the three service-tax rows) for a range of versions (one version, "Mn" = 1..n, "Master" = all): actual, approved,
-- payable to hospital and due to / (from) member are summed over the range; limit and days are those of the last version in the range.
CREATE OR ALTER PROCEDURE genisis.WorksheetEnquiry_Benefits
    @cas nvarchar(15), @fromVersion int, @toVersion int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @last int = (SELECT MAX(CLMVersion) FROM genisis.WS_ClaimLimit WHERE CASNumber = @cas AND CLMVersion BETWEEN @fromVersion AND @toVersion);
    DECLARE @days TABLE (Slot int PRIMARY KEY, Days nvarchar(30));
    INSERT @days SELECT s.Slot, s.Days FROM genisis.WS_ClaimLimit l CROSS APPLY (VALUES
        (1, l.CLMBoardDays), (2, l.CLMICUDays), (10, l.CLMINHOSPhyDays), (16, l.CLMKIDDCDays), (18, l.CLMCashDays), (19, l.CLMGurdianDays)
    ) s(Slot, Days) WHERE l.CASNumber = @cas AND l.CLMVersion = @last;

    WITH act AS (
        SELECT v.Slot, SUM(genisis.Worksheet_Amount(v.Amt)) AS Amt
        FROM genisis.WS_ClaimActual a
        CROSS APPLY (VALUES
            (1, a.CLMBoardAmt), (2, a.CLMICUAmt), (3, a.CLMServices), (4, a.CLMTheatre), (5, a.CLMMRI),
            (6, a.CLMPreHSAmt), (7, a.CLMPreHosp), (8, a.CLMSurgicalAmt), (9, a.CLMAna), (10, a.CLMNSPreHos),
            (11, a.CLMPhysician), (12, a.CLMPost), (13, a.CLMMPreHSAmt), (14, a.CLMAccPostAmt), (15, a.CLMAmbulance),
            (16, a.CLMOutpatient), (17, a.CLMMonthly), (18, a.CLMOrgan), (19, a.CLMTax), (20, a.CLMGuardianAmt),
            (21, a.CLMMedical), (22, a.CLMPhone), (23, a.CLMAdmin), (24, a.CLMHomeNCAmt), (25, a.CLMOthers1),
            (26, a.CLMOthersNotCoPay), (27, a.CLMBoardTaxAmt), (28, a.CLMICUTaxAmt), (29, a.CLMLodgerTaxAmt)
        ) v(Slot, Amt)
        WHERE a.CASNumber = @cas AND a.CLMVersion BETWEEN @fromVersion AND @toVersion
        GROUP BY v.Slot
    ), app AS (
        SELECT v.Slot, SUM(genisis.Worksheet_Amount(v.Amt)) AS Amt
        FROM genisis.WS_ClaimApprove p
        CROSS APPLY (VALUES
            (1, p.CLMBoardAmt), (2, p.CLMICUAmt), (3, p.CLMServices), (4, p.CLMTheatre), (5, p.CLMMRI),
            (6, p.CLMPreHSAmt), (7, p.CLMPreHosp), (8, p.CLMSurgicalAmt), (9, p.CLMAna), (10, p.CLMNSPreHos),
            (11, p.CLMPhysician), (12, p.CLMPost), (13, p.CLMMPreHSAmt), (14, p.CLMAccPostAmt), (15, p.CLMAmbulance),
            (16, p.CLMOutpatient), (17, p.CLMMonthly), (18, p.CLMOrgan), (19, p.CLMTax), (20, p.CLMGuardianAmt),
            (21, p.CLMMedical), (22, p.CLMPhone), (23, p.CLMAdmin), (24, p.CLMHomeNCAmt), (25, p.CLMOthers1),
            (26, p.CLMOthersNotCoPay), (27, p.CLMBoardTaxAmt), (28, p.CLMICUTaxAmt), (29, p.CLMLodgerTaxAmt)
        ) v(Slot, Amt)
        WHERE p.CASNumber = @cas AND p.CLMVersion BETWEEN @fromVersion AND @toVersion
        GROUP BY v.Slot
    ), hos AS (
        SELECT v.Slot, SUM(genisis.Worksheet_Amount(v.Amt)) AS Amt
        FROM genisis.WS_ClaimHos h
        CROSS APPLY (VALUES
            (1, h.CLMBoardAmt), (2, h.CLMICUAmt), (3, h.CLMServices), (4, h.CLMTheatre), (5, h.CLMMRI),
            (6, h.CLMPreHSAmt), (7, h.CLMPreHosp), (8, h.CLMSurgicalAmt), (9, h.CLMAna), (10, h.CLMNSPreHos),
            (11, h.CLMPhysician), (12, h.CLMPost), (13, h.CLMMPreHSAmt), (14, h.CLMAccPostAmt), (15, h.CLMAmbulance),
            (16, h.CLMOutpatient), (17, h.CLMMonthly), (18, h.CLMOrgan), (19, h.CLMTax), (20, h.CLMGuardianAmt),
            (21, h.CLMMedical), (22, h.CLMPhone), (23, h.CLMAdmin), (24, h.CLMHomeNCAmt), (25, h.CLMOthers1),
            (26, h.CLMOthersNotCoPay), (27, h.CLMBoardTaxAmt), (28, h.CLMICUTaxAmt), (29, h.CLMLodgerTaxAmt)
        ) v(Slot, Amt)
        WHERE h.CASNumber = @cas AND h.CLMVersion BETWEEN @fromVersion AND @toVersion
        GROUP BY v.Slot
    ), mem AS (
        SELECT v.Slot, SUM(genisis.Worksheet_Amount(v.Amt)) AS Amt
        FROM genisis.WS_ClaimMember m
        CROSS APPLY (VALUES
            (1, m.CLMBoardAmt), (2, m.CLMICUAmt), (3, m.CLMServices), (4, m.CLMTheatre), (5, m.CLMMRI),
            (6, m.CLMPreHSAmt), (7, m.CLMPreHosp), (8, m.CLMSurgicalAmt), (9, m.CLMAna), (10, m.CLMNSPreHos),
            (11, m.CLMPhysician), (12, m.CLMPost), (13, m.CLMMPreHSAmt), (14, m.CLMAccPostAmt), (15, m.CLMAmbulance),
            (16, m.CLMOutpatient), (17, m.CLMMonthly), (18, m.CLMOrgan), (19, m.CLMTax), (20, m.CLMGuardianAmt),
            (21, m.CLMMedical), (22, m.CLMPhone), (23, m.CLMAdmin), (24, m.CLMHomeNCAmt), (25, m.CLMOthers1),
            (26, m.CLMOthersNotCoPay), (27, m.CLMBoardTaxAmt), (28, m.CLMICUTaxAmt), (29, m.CLMLodgerTaxAmt)
        ) v(Slot, Amt)
        WHERE m.CASNumber = @cas AND m.CLMVersion BETWEEN @fromVersion AND @toVersion
        GROUP BY v.Slot
    ), lim AS (
        SELECT v.Slot, genisis.Worksheet_Amount(v.Amt) AS Amt
        FROM genisis.WS_ClaimLimit l
        CROSS APPLY (VALUES
            (1, l.CLMBoardAmt), (2, l.CLMICUAmt), (3, l.CLMServices), (4, l.CLMTheatre), (5, l.CLMMRI),
            (6, l.CLMPreHSAmt), (7, l.CLMPreHosp), (8, l.CLMSurgicalAmt), (9, l.CLMAna), (10, l.CLMNSPreHos),
            (11, l.CLMPhysician), (12, l.CLMPost), (13, l.CLMMPreHSAmt), (14, l.CLMAccPostAmt), (15, l.CLMAmbulance),
            (16, l.CLMOutpatient), (17, l.CLMMonthly), (18, l.CLMOrgan), (19, l.CLMTax), (20, l.CLMGuardianAmt),
            (21, l.CLMMedical), (22, l.CLMPhone), (23, l.CLMAdmin), (24, l.CLMHomeNCAmt), (25, l.CLMOthers1),
            (26, l.CLMOthersNotCoPay), (27, l.CLMBoardTaxAmt), (28, l.CLMICUTaxAmt), (29, l.CLMLodgerTaxAmt)
        ) v(Slot, Amt)
        WHERE l.CASNumber = @cas AND l.CLMVersion = @last
    )
    SELECT n.Slot, n.BenefitCode, n.Category, n.Benefit, d.Days, lim.Amt AS Limit, act.Amt AS Actual, app.Amt AS Approved, hos.Amt AS Hospital, mem.Amt AS Member
    FROM (
        SELECT CAST(ROW_NUMBER() OVER (ORDER BY BNFSubCode) AS int) AS Slot, RTRIM(BNFSubCode) AS BenefitCode, RTRIM(CATDescription) AS Category, RTRIM(BNFSubName) AS Benefit
        FROM dbo.view_bnf_cat_subcat WHERE BNFSubCode NOT IN (N'GenSurgBen', N'ine001')
        UNION ALL SELECT 26, N'', N'Others', N'Others - No Co-Pay'
        UNION ALL SELECT 27, N'', N'Service Tax', N'Service Tax - R&B'
        UNION ALL SELECT 28, N'', N'Service Tax', N'Service Tax - ICU'
        UNION ALL SELECT 29, N'', N'Service Tax', N'Service Tax - Lodger'
    ) n
    LEFT JOIN act ON act.Slot = n.Slot LEFT JOIN app ON app.Slot = n.Slot LEFT JOIN hos ON hos.Slot = n.Slot LEFT JOIN mem ON mem.Slot = n.Slot
    LEFT JOIN lim ON lim.Slot = n.Slot LEFT JOIN @days d ON d.Slot = n.Slot
    ORDER BY n.Slot;
END
GO

-- The saved summary figures of the worksheet (the rows under the benefit grid: co-payment, excess, limit exceeded, ex-gratia, fees, final
-- totals and the bordx amount, as getFinalTotal worked them out and SaveClaim stored them), added up over a range of versions.
CREATE OR ALTER PROCEDURE genisis.WorksheetEnquiry_Summary
    @cas nvarchar(15), @fromVersion int, @toVersion int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT SUM(ISNULL(CLMTotalActual, 0)) AS TotalActual, SUM(ISNULL(CLMTotalApproved, 0)) AS TotalApproved, SUM(ISNULL(CLMCoPayRB, 0)) AS CoPayRb,
        SUM(ISNULL(CLMExcess, 0)) AS TotalExcess, SUM(ISNULL(CLMLimitExceeded, 0)) AS LimitExceeded, SUM(ISNULL(CLMExGratiaAmt, 0)) AS ExGratia,
        SUM(ISNULL(CLMBTBGAmt, 0)) AS Btbg, SUM(ISNULL(CLMCaseMgmtFees, 0)) AS ProcessingFee, SUM(ISNULL(CLMMajorMedFee, 0)) AS MajorMedicalFee,
        SUM(ISNULL(CLMMainTUAmt, 0)) AS MainTopup, SUM(ISNULL(CLMHosPayColl, 0)) AS HospitalPayorToCollect,
        SUM(ISNULL(CLMPayableByMedix2H, 0)) AS PayableToHospital, SUM(ISNULL(CLMPayableByMedix2M, 0)) AS PayableToMember,
        SUM(ISNULL(CLMFinalApprove, 0)) AS FinalApprove, SUM(ISNULL(CLMBordxAmt, 0)) AS BordxAmount
    FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion BETWEEN @fromVersion AND @toVersion;
END
GO

-- ClaimOthers: payee address, note, attention, payment mode and the two remarks of one version.
CREATE OR ALTER PROCEDURE genisis.WorksheetEnquiry_Others
    @cas nvarchar(15), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(CLMWSRemarks) AS WorksheetRemarks, RTRIM(CLMDCNoteRemarks) AS CreditDebitRemarks, RTRIM(MBMAdd1) AS Address1, RTRIM(MBMAdd2) AS Address2,
        RTRIM(MBMAdd3) AS Address3, RTRIM(CLMNote) AS Note, RTRIM(MBMPostCode) AS PostCode, RTRIM(MBMCity) AS City, RTRIM(MBMState) AS State,
        RTRIM(CLMAttention) AS Attention, RTRIM(CLMPayMode) AS PaymentMode
    FROM genisis.WS_ClaimOthers WHERE CASNumber = @cas AND CLaimVersion = @version;
END
GO

-- ===== main/270_worksheet_engine.sql =====
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

-- ===== main/280_worksheet_adjustment.sql =====
-- Worksheet > Worksheet Adjustment (legacy frmWorksheetAdj2013), step 1: the Update button (cmdUpdate_Click / SaveOtherInfo), which saves the payment details
-- and the remarks of one worksheet version. The rest of the form (Approve, credit / debit notes, letters) follows in later steps.

-- The pay-to of a version's invoices as the form worked it out: MBMHosStatus is the pay-to of the last invoice listed (H hospital, M member).
CREATE OR ALTER PROCEDURE genisis.WorksheetAdjustment_PayTo
    @cas nvarchar(30), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(InvPayTo) AS PayTo
    FROM genisis.WS_CASInvoice
    WHERE CASNumber = @cas AND INVWSversion = CAST(@version AS nvarchar(10))
    ORDER BY INVIndex DESC;
END
GO

-- cboBank: the banks that have accounts (Bank, from BankAccounts of the maintenance database).
CREATE OR ALTER PROCEDURE genisis.WorksheetAdjustment_Banks
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(Bank) AS Name FROM genisis.Maint_BankAccounts WHERE Bank IS NOT NULL AND RTRIM(Bank) <> N'' GROUP BY RTRIM(Bank) ORDER BY RTRIM(Bank);
END
GO

-- cboCLMState: the states (SearchState).
CREATE OR ALTER PROCEDURE genisis.WorksheetAdjustment_States
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(STADescription) AS Name FROM dbo.STA WHERE STADescription IS NOT NULL AND RTRIM(STADescription) <> N'' ORDER BY RTRIM(STADescription);
END
GO

-- SaveOtherInfo: Claim gets the last document date (only when one was given), pay-to, bank, account number and GP; ClaimOthers (when the version has a row) gets the
-- remarks, the member's address, the note, the payment mode and the attention. Empty text is stored as empty text, as the desktop did.
-- Result: 0 saved, 1 the worksheet version does not exist.
CREATE OR ALTER PROCEDURE genisis.WorksheetAdjustment_UpdatePayment
    @cas nvarchar(30), @version int, @lastDocDate datetime,
    @payTo nvarchar(50), @gpName nvarchar(50), @bank nvarchar(40), @accountNo nvarchar(30),
    @paymentMode nvarchar(50), @attention nvarchar(60),
    @address1 nvarchar(50), @address2 nvarchar(50), @address3 nvarchar(50), @postCode nvarchar(6), @city nvarchar(50), @state nvarchar(50),
    @note nvarchar(100), @wsRemarks nvarchar(200), @cdRemarks nvarchar(200)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF NOT EXISTS (SELECT 1 FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @version)
    BEGIN
        SELECT 1 AS Result;
        RETURN;
    END

    UPDATE genisis.WS_Claim
    SET CLMLastDocDate = ISNULL(@lastDocDate, CLMLastDocDate), CLMPayTo = @payTo, CLMAccountNo = @accountNo, CLMBank = @bank, CLMGPName = @gpName
    WHERE CASNumber = @cas AND ClaimVersion = @version;

    UPDATE genisis.WS_ClaimOthers
    SET CLMWSRemarks = @wsRemarks, CLMDCNoteRemarks = @cdRemarks, MBMAdd1 = @address1, MBMAdd2 = @address2, MBMAdd3 = @address3, MBMPostCode = @postCode,
        MBMCity = @city, MBMState = @state, CLMNote = @note, CLMPayMode = @paymentMode, CLMAttention = @attention
    WHERE CASNumber = @cas AND CLaimVersion = @version;
    SELECT 0 AS Result;
END
GO

-- ===== main/290_worksheet_documents.sql =====
-- Worksheet > Worksheet Adjustment (legacy frmWorksheetAdj2013), step 2: the credit / debit note and the Excess letter. The desktop filled Excel and Word templates
-- (CreditNote.xlt, DebitNote.xlt, DebitNoteCompany.xlt, EXCLetterMAS.DOT, ExcessLetterEtiqa.DOT, MedixExcLetter.DOT) that are not in the source package; these procedures
-- return the data the templates were filled with.

-- The member's address as the notes printed it (the member master, not the address typed on the worksheet) and whether the plan is billed to the company.
CREATE OR ALTER PROCEDURE genisis.WorksheetNote_Member
    @cas nvarchar(30), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(v.MBMAddress1) AS Address1, RTRIM(v.MBMAddress2) AS Address2, RTRIM(v.MBMAddress3) AS Address3, RTRIM(v.MBMPostCode) AS PostCode,
        CAST(CASE WHEN RTRIM(v.PLNCompanyDN) = N'C' THEN 1 ELSE 0 END AS bit) AS CompanyPlan
    FROM dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v
    WHERE v.CASNumber = @cas AND v.ClaimVersion = @version AND v.MBMNumber IS NOT NULL;
END
GO

-- cmdDebit_Click: a debit note that has no number yet gets the next one of its payor: D + payor + five digits (DRSeq, GenerateDebitNo), saved with today's date on the
-- worksheet (UpdateCreditDebit). A worksheet that has a number keeps it. Result: 0 ok, 1 the worksheet version does not exist.
CREATE OR ALTER PROCEDURE genisis.WorksheetNote_AssignDebitNo
    @cas nvarchar(30), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @number nvarchar(30);
    SELECT @number = RTRIM(DebitCreditRefNo) FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @version;
    IF @@ROWCOUNT = 0
    BEGIN
        SELECT 1 AS Result, CAST(NULL AS nvarchar(30)) AS Number;
        RETURN;
    END
    IF ISNULL(@number, N'') <> N''
    BEGIN
        SELECT 0 AS Result, @number AS Number;
        RETURN;
    END

    DECLARE @payor nvarchar(10) = LEFT(@cas, 2), @seq int;
    IF NOT EXISTS (SELECT 1 FROM genisis.WS_DRSeq WITH (UPDLOCK, HOLDLOCK) WHERE Payor = @payor)
    BEGIN
        INSERT genisis.WS_DRSeq (DRSeq, Payor) VALUES (1, @payor);
        SET @seq = 1;
    END
    ELSE
        UPDATE genisis.WS_DRSeq SET @seq = DRSeq = DRSeq + 1 WHERE Payor = @payor;

    SET @number = N'D' + @payor + CASE WHEN @seq > 99999 THEN CAST(@seq AS nvarchar(10)) ELSE RIGHT(N'00000' + CAST(@seq AS nvarchar(10)), 5) END;
    UPDATE genisis.WS_Claim SET DebitCreditRefNo = @number, DebitCreditDate = CAST(CAST(GETDATE() AS date) AS datetime)
    WHERE CASNumber = @cas AND ClaimVersion = @version;
    SELECT 0 AS Result, @number AS Number;
END
GO

-- cmdExcLetter_Click / bookmark: the excess record of the case (CAS_Excess), the one saved for the version when there is one, else the latest, with the amounts as numbers
-- (they are text in the table: "-" or an amount), the hospital's contact and fax, and the member details the letter prints.
CREATE OR ALTER PROCEDURE genisis.WorksheetExcess_Get
    @cas nvarchar(30), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1)
        RTRIM(e.EXCNumber) AS ExcessNo, e.EXCDate AS ExcessDate, RTRIM(e.INVNo) AS InvoiceNo, genisis.Worksheet_Amount(e.INVAmt) AS InvoiceAmount,
        genisis.Worksheet_Amount(e.EXCBoardAmt) AS RoomAndBoard, genisis.Worksheet_Amount(e.EXCICUAmt) AS Icu, genisis.Worksheet_Amount(e.EXCServices) AS Services,
        genisis.Worksheet_Amount(e.EXCTheatre) AS Theatre, genisis.Worksheet_Amount(e.EXCSurgicalAmt) AS Surgical, genisis.Worksheet_Amount(e.EXCAna) AS Anaesthetist,
        genisis.Worksheet_Amount(e.EXCPhysician) AS Physician, genisis.Worksheet_Amount(e.EXCAmbulance) AS Ambulance, genisis.Worksheet_Amount(e.EXCTax) AS Tax,
        genisis.Worksheet_Amount(e.EXCGuardianAmt) AS Guardian, genisis.Worksheet_Amount(e.EXCMedical) AS MedicalReport, genisis.Worksheet_Amount(e.EXCAdmin) AS Admin,
        genisis.Worksheet_Amount(e.EXCBoardTaxAmt) AS RoomAndBoardTax, genisis.Worksheet_Amount(e.EXCICUTaxAmt) AS IcuTax, genisis.Worksheet_Amount(e.EXCLodgerTaxAmt) AS LodgerTax,
        genisis.Worksheet_Amount(e.EXCOthers1) AS Others, RTRIM(e.EXCOthersDesc) AS OthersDescription,
        genisis.Worksheet_Amount(e.EXCTotalExceed) AS LimitExceeded, genisis.Worksheet_Amount(e.EXCCoPayment) AS CoPayment, genisis.Worksheet_Amount(e.EXCUndExcess) AS UndertakenExcess,
        RTRIM(h.HOSContact) AS HospitalContact, RTRIM(h.HOSFaxNo) AS HospitalFax,
        RTRIM(v.MBMEmployeeNo) AS StaffNo, RTRIM(v.MBMCostCenter) AS CostCenter, RTRIM(v.MBMDepartment) AS Department,
        RTRIM(CASE WHEN RTRIM(v.MBMPatientCovID) <> N'00' AND ISNULL(RTRIM(cp.MBMCICBCPPNo), N'') <> N'' THEN cp.MBMCICBCPPNo ELSE v.MBMIcBcPp END) AS PatientIc,
        RTRIM(v.PLNExcessPrivate) AS ExcessTemplate
    FROM genisis.WS_ExcessLetter e
    LEFT JOIN dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v ON v.CASNumber = e.CASNumber AND v.ClaimVersion = @version AND v.MBMNumber IS NOT NULL
    LEFT JOIN dbo.MBMCoveredPersons cp WITH (NOLOCK) ON cp.MBMCNumber = v.MBMNumber AND cp.MBMCCoverID = v.MBMPatientCovID
    LEFT JOIN dbo.HOS h WITH (NOLOCK) ON h.HOSCode = v.CASHOSCode
    WHERE e.CASNumber = @cas
    ORDER BY CASE WHEN e.ClaimVersion = @version THEN 0 ELSE 1 END, e.EXCDate DESC;
END
GO

-- cmdExcLetter_Click, after the letter is made: the case is marked as having had its excess letter (tblRecord.ExcessLetter = 'Y').
CREATE OR ALTER PROCEDURE genisis.WorksheetExcess_MarkIssued
    @cas nvarchar(30)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.Maint_tblRecord SET ExcessLetter = 'Y' WHERE CaseNo = @cas;
    SELECT @@ROWCOUNT AS Marked;
END
GO

-- ===== main/300_worksheet_final_lg.sql =====
-- Worksheet > Worksheet Adjustment (legacy frmWorksheetAdj2013), step 3: Final LG (cmdFinalLG_Click). The guarantee letter itself is issued by the same code as Case
-- Adjustment's (CASLG, LG number, notification); what the worksheet adds, in the same transaction, is this: the invoices of the version carry the LG number
-- (UpdateInvoiceLG) and the case is marked "FINAL GL Issued" with its worksheet done (tblRecord IsLGissued, IsWSdone, CaseProgress).
CREATE OR ALTER PROCEDURE genisis.WorksheetFinalLg_Mark
    @cas nvarchar(30), @version int, @lgNumber nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.WS_ClaimInvoice SET LGReference = @lgNumber
    WHERE CASNumber = @cas AND INVWSversion = CAST(@version AS nvarchar(4));
    UPDATE genisis.Maint_tblRecord SET IsLGissued = '1', IsWSdone = '1', CaseProgress = N'FINAL GL Issued' WHERE CaseNo = @cas;
END
GO

-- ===== main/310_worksheet_plan_limits.sql =====
-- Worksheet calculation engine, step 4: the limits the plan fixes (display_benefit of the Registration form). The benefit rows (BNF) of the plan the desktop reads, the row with the latest
-- effective date up to its bound for each benefit code. Which plan, in the desktop's order:
--   1. a case with a take-over plan (CAS.CASTakeOverPlan): that plan and the insured type, whatever the health type and date;
--   2. a member with a new plan after renewal (MBMNewPlanCode): the current plan with rows up to the payor effective date when the patient was admitted on or before it, otherwise
--      the new plan with rows up to the new policy effective date;
--   3. a dependant with a plan of their own (MBMCPLNCode): that plan, any date;
--   4. otherwise the member's plan, any date.
-- Not ported: the top-up policy's own plan.
CREATE OR ALTER PROCEDURE genisis.WorksheetEngine_PlanBenefits
    @cas nvarchar(15), @version int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @plancode nvarchar(20), @ins nvarchar(5), @hlt nvarchar(5), @bound datetime, @anyHealthType bit = 0;
    DECLARE @own nvarchar(20), @dependant nvarchar(20), @newPlan nvarchar(20), @takeOver nvarchar(20), @patient nvarchar(2), @adm datetime, @payorEff datetime, @newEff datetime;
    SELECT TOP (1)
        @own = RTRIM(v.PLNCode), @dependant = RTRIM(v.MBMCPLNCode), @newPlan = RTRIM(v.MBMNewPlanCode), @patient = v.MBMPatientCovID,
        @ins = RTRIM(v.INSCode), @hlt = RTRIM(v.HLTCode), @adm = v.CASDateAdmitted, @payorEff = v.MBMPayorEffDate, @newEff = v.MBMNewPolEffDate,
        @takeOver = RTRIM(c.CASTakeOverPlan)
    FROM dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v
    JOIN dbo.CAS c ON c.CASNumber = v.CASNumber
    WHERE v.CASNumber = @cas AND v.ClaimVersion = @version AND v.MBMNumber IS NOT NULL;

    IF ISNULL(@takeOver, N'') <> N'' BEGIN SET @plancode = @takeOver; SET @anyHealthType = 1; END
    ELSE IF ISNULL(@newPlan, N'') <> N''
    BEGIN
        IF @adm <= @payorEff BEGIN SET @plancode = @own; SET @bound = @payorEff; END
        ELSE BEGIN SET @plancode = @newPlan; SET @bound = @newEff; END
    END
    ELSE IF @patient <> N'00' AND ISNULL(@dependant, N'') <> N'' SET @plancode = @dependant;
    ELSE SET @plancode = @own;

    SELECT x.Code, x.Days, x.Claimable, x.[Limit]
    FROM (SELECT RTRIM(b.BNFCode) AS Code, RTRIM(b.BNFNoOfDays) AS Days, RTRIM(b.BNFClaimAbleAmount) AS Claimable, LTRIM(RTRIM(b.BNFRMPerDis)) AS [Limit],
                 ROW_NUMBER() OVER (PARTITION BY b.BNFCode ORDER BY b.BNFEffDate DESC) AS rn
          FROM dbo.BNF b
          WHERE b.PLNCode = @plancode AND b.INSCode = @ins AND (@anyHealthType = 1 OR b.HLTCode = @hlt)
            AND (@bound IS NULL OR b.BNFEffDate <= @bound)) x
    WHERE x.rn = 1
    ORDER BY x.Code;
END
GO

-- ===== main/320_worksheet_plan_terms.sql =====
-- Worksheet calculation engine, step 5: the plan's co-payment terms. The plan row (PLN) of the patient's plan, payor and health type: the co-insurance percentage and its sign
-- (PLNCoINSPerc, PLNCoInsSign: a share of the approved total the member pays), and the Room & Board co-payment percentage with its indicator (PLNCoPayPerc, CoPaymentInd).
-- The plan is the covered person's own when the patient is a dependant and has one, otherwise the member's.
-- Also the member's own co-payment percentage and its indicator (MBM.MBMTopupCopay, MBMTopupCopayInd): payor TK members carry 20 there, and the desktop's major-medical formula uses it.
CREATE OR ALTER PROCEDURE genisis.WorksheetEngine_PlanTerms
    @cas nvarchar(15), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1)
        ISNULL(p.PLNCoINSPerc, 0) AS CoInsurancePercent, RTRIM(p.PLNCoInsSign) AS CoInsuranceSign,
        ISNULL(p.PLNCoPayPerc, 0) AS RoomCoPayPercent, RTRIM(p.CoPaymentInd) AS CoPaymentInd,
        ISNULL(m.MBMTopupCopay, 0) AS MemberCoPayPercent, RTRIM(m.MBMTopupCopayInd) AS MemberCoPayInd,
        -- the Room & Board co-payment (frmWorksheetRegGST2015, cmdDisplay): the plan family, the new SM flag and its MRI share, the major-medical flag, whether the payor (the first two letters of the case number)
        -- carries the Room & Board tax, and what the version's invoices say about the room: the new Room & Board tax, the gross Room & Board and the latest invoice date (the cent rounding applies from 1 April 2008)
        LEFT(RTRIM(p.PLNCode), 1) AS PlanFamily, RTRIM(p.NewSMPlan) AS NewSmPlan, CASE WHEN RTRIM(p.NewSMPlan) = N'Y' THEN ISNULL(p.PLNMRIPercentage, 0) ELSE 0 END AS MriPercent,
        RTRIM(p.PLNMajorMedInd) AS MajorMedInd, LEFT(@cas, 2) AS PayorCode, RTRIM(c.CASHOSCode) AS HospitalCode, (SELECT TOP (1) RTRIM(y.PAYRBTax) FROM dbo.PAY y WHERE y.PAYCode = LEFT(@cas, 2)) AS PayorRoomTax,
        ISNULL(inv.RoomTaxNew, 0) AS RoomTaxNew, ISNULL(inv.GrossRoomBoard, 0) AS GrossRoomBoard, inv.LastInvoiceDate
    FROM dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v
    JOIN dbo.MBM m ON m.MBMNumber = v.MBMNumber
    JOIN dbo.CAS c ON c.CASNumber = v.CASNumber
    OUTER APPLY (SELECT SUM(genisis.Worksheet_Amount(a.CLMNewRBTax)) AS RoomTaxNew, SUM(genisis.Worksheet_Amount(a.CLMGrossRB)) AS GrossRoomBoard, MAX(i.InvDate) AS LastInvoiceDate
                 FROM genisis.WS_ClaimInvoice i JOIN genisis.WS_ClaimInvoiceActual a ON a.INVIndex = i.INVIndex
                 WHERE i.CASNumber = @cas AND i.INVWSversion = CAST(@version AS nvarchar(10))) inv
    JOIN dbo.PLN p ON p.PLNCode = RTRIM(CASE WHEN v.MBMPatientCovID <> N'00' AND ISNULL(RTRIM(v.MBMCPLNCode), N'') <> N'' THEN v.MBMCPLNCode ELSE v.PLNCode END)
                  AND p.PAYCode = v.PAYCode AND p.HLTCode = v.HLTCode
    WHERE v.CASNumber = @cas AND v.ClaimVersion = @version AND v.MBMNumber IS NOT NULL;
END
GO

-- ===== main/330_worksheet_balance_limit.sql =====
-- Worksheet calculation engine, step 5b: the member's available limit (the balance limit of the Registration screen). Whose limit it is depends on the plan's annual limit type
-- (AnnualLimit.SuppLimitStatus, the latest row up to the admission date): for the principal, or a patient with cover 00, it is the member's (MBM.MBMAvailableLimit); for a dependant
-- with type B it is the first covered person's (cover 01), with type C the patient's own (MBMCoveredPersons.MBMCAvailableLimit), and with any other type (A, or no row) the member's.
CREATE OR ALTER PROCEDURE genisis.WorksheetEngine_BalanceLimit
    @cas nvarchar(15), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1)
        CASE WHEN RTRIM(v.MBMMemberType) = N'P' OR v.MBMPatientCovID = N'00' THEN m.MBMAvailableLimit
             WHEN a.SuppLimitStatus = N'B' THEN (SELECT TOP (1) c.MBMCAvailableLimit FROM dbo.MBMCoveredPersons c WHERE c.MBMCNumber = v.MBMNumber AND c.MBMCCoverID = N'01')
             WHEN a.SuppLimitStatus = N'C' THEN (SELECT TOP (1) c.MBMCAvailableLimit FROM dbo.MBMCoveredPersons c WHERE c.MBMCNumber = v.MBMNumber AND c.MBMCCoverID = v.MBMPatientCovID)
             ELSE m.MBMAvailableLimit END AS Balance,
        RTRIM(a.SuppLimitStatus) AS LimitType
    FROM dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v
    JOIN dbo.MBM m ON m.MBMNumber = v.MBMNumber
    OUTER APPLY (SELECT TOP (1) al.SuppLimitStatus FROM dbo.AnnualLimit al
                 WHERE al.PLNCode = RTRIM(v.PLNCode) AND al.INSCode = RTRIM(v.INSCode) AND al.AnnualEffDate <= v.CASDateAdmitted ORDER BY al.AnnualEffDate DESC) a
    WHERE v.CASNumber = @cas AND v.ClaimVersion = @version AND v.MBMNumber IS NOT NULL;
END
GO

-- ===== main/340_worksheet_stay_days.sql =====
-- Worksheet calculation engine, step 7: the days of the stay. The invoices of a worksheet version carry the days they charge for (CLMBoardDay, CLMICUDay; fractional days such
-- as 7.5 exist), and the days of the worksheet are their sum: the saved room days (ClaimLimit.CLMBoardDays) equal it for 98.6 per cent of the versions, the ICU days for 99.4 per cent.
CREATE OR ALTER PROCEDURE genisis.WorksheetEngine_StayDays
    @cas nvarchar(15), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ISNULL(SUM(i.CLMBoardDay), 0) AS RoomDays, ISNULL(SUM(i.CLMICUDay), 0) AS IcuDays, ISNULL(SUM(i.CLMInHosPhyVisitDay), 0) AS PhysicianVisitDays, ISNULL(SUM(i.CLMKidneyMnth), 0) AS KidneyMonths,
           ISNULL(SUM(i.CLMCashDay), 0) AS CashDays, ISNULL(SUM(i.CLMLodgerDay), 0) AS GuardianDays
    FROM genisis.WS_CASInvoice i
    WHERE i.CASNumber = @cas AND i.INVWSversion = CAST(@version AS nvarchar(10));
END
GO

-- ===== main/350_worksheet_calculation.sql =====
-- Worksheet calculation screen: what the page needs besides the engine. The captions of the 29 grid rows (the same list the Enquiry grid uses), the figures the desktop saved for a
-- version (to show beside the calculated ones), and the versions a case has.

CREATE OR ALTER PROCEDURE genisis.WorksheetCalc_Captions
AS
BEGIN
    SET NOCOUNT ON;
    SELECT n.Slot, n.BenefitCode, n.Category, n.Benefit
    FROM (
        SELECT CAST(ROW_NUMBER() OVER (ORDER BY BNFSubCode) AS int) AS Slot, RTRIM(BNFSubCode) AS BenefitCode, RTRIM(CATDescription) AS Category, RTRIM(BNFSubName) AS Benefit
        FROM dbo.view_bnf_cat_subcat WHERE BNFSubCode NOT IN (N'GenSurgBen', N'ine001')
        UNION ALL SELECT 26, N'', N'Others', N'Others - No Co-Pay'
        UNION ALL SELECT 27, N'', N'Service Tax', N'Service Tax - R&B'
        UNION ALL SELECT 28, N'', N'Service Tax', N'Service Tax - ICU'
        UNION ALL SELECT 29, N'', N'Service Tax', N'Service Tax - Lodger'
    ) n
    ORDER BY n.Slot;
END
GO

CREATE OR ALTER PROCEDURE genisis.WorksheetCalc_Saved
    @cas nvarchar(15), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) ISNULL(CLMTotalApproved, 0) AS TotalApproved, ISNULL(CLMLimitExceeded, 0) AS LimitExceeded, ISNULL(CLMCoPayRB, 0) AS CoPayment, CLMPreviousBalance AS PreviousBalance
    FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @version;
END
GO

CREATE OR ALTER PROCEDURE genisis.WorksheetCalc_Versions
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ClaimVersion AS Version FROM genisis.WS_Claim WHERE CASNumber = @cas ORDER BY ClaimVersion;
END
GO

-- ===== main/360_finance_receipt_enquiry.sql =====
-- Finance > Receipt Enquiry (legacy frmReceiptEnquiry; its source is not available). Rebuilt from the production data: the receipt
-- header is HISPayment.dbo.ReceiptSummary (receipt no. from RecSeq, type IPCLM / OPCLM / IPFEE / OPFEE, SUN batch and posted flag);
-- claim receipts have their allocation lines in HISPayment.dbo.ReceiptfrPAY (same ReceiptNo, one line per case / version / bordx,
-- CheckAmt = amount allocated); fee receipts name the member MCO invoice (HISDB.dbo.MBMMCOInvoice.MCOInvNo) in ReceiptDesc.
-- Read only: nothing is written.

-- The receipt types in use and the payors.
CREATE OR ALTER PROCEDURE genisis.ReceiptEnquiry_Lookups
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT RTRIM(ReceiptType) AS Code, RTRIM(ReceiptType) AS Name
    FROM genisis.Pay_ReceiptSummary WHERE RTRIM(ISNULL(ReceiptType, N'')) <> N'' ORDER BY Code;
    SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name FROM dbo.PAY ORDER BY PAYCode;
END
GO

-- The receipts that match; NULL = no criterion, ranges inclusive. @posted '1': posted to SUN (RecPayStatus 1), '0': not posted.
-- @caseNo: receipts with an allocation line for that case. Each row carries its number of allocation lines and their total, and
-- ReversedBy: the receipt that reversed it (Reverse Receipt writes the reversal's description as "REV <receipt no>-...").
CREATE OR ALTER PROCEDURE genisis.ReceiptEnquiry_Search
    @receiptFrom nvarchar(40) = NULL, @receiptTo nvarchar(40) = NULL, @dateFrom date = NULL, @dateTo date = NULL,
    @type nvarchar(30) = NULL, @payor nvarchar(4) = NULL, @chequeNo nvarchar(40) = NULL, @batchNo nvarchar(30) = NULL,
    @posted char(1) = NULL, @description nvarchar(100) = NULL, @caseNo nvarchar(30) = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CASE WHEN CHARINDEX(N'-', ReceiptDesc, 5) > 5 THEN SUBSTRING(ReceiptDesc, 5, CHARINDEX(N'-', ReceiptDesc, 5) - 5) END AS OriginalNo,
        RTRIM(ReceiptNo) AS ReversalNo
    INTO #rev FROM genisis.Pay_ReceiptSummary WHERE ReceiptDesc LIKE N'REV %-%';

    SELECT TOP (@max)
        s.ReceiptIndex AS Id, RTRIM(s.ReceiptNo) AS ReceiptNo, s.ReceiptDate, s.ReceiptAmt AS Amount, RTRIM(s.ReceiptType) AS ReceiptType,
        RTRIM(s.ReceiptDesc) AS Description, RTRIM(s.RecChqNo) AS ChequeNo, RTRIM(s.RecPayCode) AS Payor, RTRIM(s.RecBatchNo) AS BatchNo,
        CAST(CASE WHEN s.RecPayStatus = N'1' THEN 1 ELSE 0 END AS bit) AS Posted, s.RecPostDate AS PostDate, RTRIM(s.RecPostUser) AS PostUser,
        ISNULL(a.Lines, 0) AS AllocationLines, a.Allocated, rv.ReversalNo AS ReversedBy
    FROM genisis.Pay_ReceiptSummary s
    OUTER APPLY (SELECT COUNT(*) AS Lines, SUM(p.CheckAmt) AS Allocated FROM genisis.Pay_ReceiptfrPAY p
                 WHERE p.ReceiptNo = s.ReceiptNo AND RTRIM(ISNULL(s.ReceiptNo, N'')) <> N'') a
    OUTER APPLY (SELECT TOP 1 r.ReversalNo FROM #rev r WHERE r.OriginalNo = RTRIM(s.ReceiptNo) AND RTRIM(ISNULL(s.ReceiptNo, N'')) <> N'') rv
    WHERE (@receiptFrom IS NULL OR s.ReceiptNo >= @receiptFrom) AND (@receiptTo IS NULL OR s.ReceiptNo <= @receiptTo)
      AND (@dateFrom IS NULL OR s.ReceiptDate >= @dateFrom) AND (@dateTo IS NULL OR s.ReceiptDate <= @dateTo)
      AND (@type IS NULL OR s.ReceiptType = @type)
      AND (@payor IS NULL OR s.RecPayCode = @payor)
      AND (@chequeNo IS NULL OR s.RecChqNo = @chequeNo)
      AND (@batchNo IS NULL OR s.RecBatchNo = @batchNo)
      AND (@posted IS NULL OR (@posted = '1' AND s.RecPayStatus = N'1') OR (@posted = '0' AND ISNULL(s.RecPayStatus, N'') <> N'1'))
      AND (@description IS NULL OR s.ReceiptDesc LIKE N'%' + @description + N'%')
      AND (@caseNo IS NULL OR EXISTS (SELECT 1 FROM genisis.Pay_ReceiptfrPAY p WHERE p.ReceiptNo = s.ReceiptNo AND p.CASNumber = @caseNo))
    ORDER BY s.ReceiptDate DESC, s.ReceiptNo DESC, s.ReceiptIndex DESC
    OPTION (RECOMPILE);
END
GO

-- One receipt: the header, its claim allocation lines, the fee invoice it names (ReceiptDesc = MCOInvNo, or the part after the last
-- '-', e.g. "IC-S33942") and its SUN posting rows (SUNPosting.TransRef = receipt no.). Four result sets; the first is empty when the
-- receipt does not exist.
CREATE OR ALTER PROCEDURE genisis.ReceiptEnquiry_Get
    @id int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @no nvarchar(40), @desc nvarchar(100);
    SELECT @no = RTRIM(ReceiptNo), @desc = RTRIM(ReceiptDesc) FROM genisis.Pay_ReceiptSummary WHERE ReceiptIndex = @id;

    SELECT s.ReceiptIndex AS Id, RTRIM(s.ReceiptNo) AS ReceiptNo, s.ReceiptDate, s.ReceiptAmt AS Amount, RTRIM(s.ReceiptType) AS ReceiptType,
        RTRIM(s.ReceiptDesc) AS Description, RTRIM(s.RecChqNo) AS ChequeNo, RTRIM(s.RecPayCode) AS Payor, RTRIM(s.RecBatchNo) AS BatchNo,
        CAST(CASE WHEN s.RecPayStatus = N'1' THEN 1 ELSE 0 END AS bit) AS Posted, s.RecPostDate AS PostDate, RTRIM(s.RecPostUser) AS PostUser,
        0 AS AllocationLines, CAST(NULL AS money) AS Allocated,
        (SELECT TOP 1 RTRIM(r.ReceiptNo) FROM genisis.Pay_ReceiptSummary r
         WHERE @no IS NOT NULL AND @no <> N'' AND r.ReceiptDesc LIKE N'REV ' + @no + N'-%' ORDER BY r.ReceiptIndex) AS ReversedBy
    FROM genisis.Pay_ReceiptSummary s WHERE s.ReceiptIndex = @id;

    SELECT p.RecPAYIndex AS Id, RTRIM(p.CASNumber) AS CaseNo, RTRIM(p.ClaimVersion) AS ClaimVersion, RTRIM(p.BordxNo) AS BordxNo,
        p.CheckAmt AS Allocated, p.RecPAYAmt AS ReceiptAmount, p.PAYBankCharge AS BankCharge, RTRIM(p.ChequeNo) AS ChequeNo,
        CASE WHEN p.RECAccPeriodMth IS NULL THEN NULL
             ELSE RIGHT(N'0' + CAST(CAST(p.RECAccPeriodMth AS int) AS nvarchar(2)), 2) + N'/' + CAST(CAST(p.RECAccPeriodYR AS int) AS nvarchar(4)) END AS AccPeriod,
        RTRIM(p.Remark) AS Remark, RTRIM(p.PAYLastUpdateUser) AS UpdatedBy, p.PAYLastUpdatedate AS UpdatedOn
    FROM genisis.Pay_ReceiptfrPAY p
    WHERE @no IS NOT NULL AND @no <> N'' AND p.ReceiptNo = @no
    ORDER BY p.CASNumber, p.ClaimVersion, p.RecPAYIndex;

    DECLARE @invoice nvarchar(40) = CASE WHEN CHARINDEX(N'-', ISNULL(@desc, N'')) > 0 THEN RIGHT(@desc, CHARINDEX(N'-', REVERSE(@desc)) - 1) ELSE @desc END;
    SELECT RTRIM(i.MCOInvNo) AS InvoiceNo, MIN(i.MCOInvDate) AS InvoiceDate, COUNT(*) AS Members, SUM(i.BillMCOAmt) AS BillAmount,
        SUM(ISNULL(i.GSTAmt, 0)) AS GstAmount
    FROM dbo.MBMMCOInvoice i
    WHERE @desc IS NOT NULL AND @desc <> N'' AND (i.MCOInvNo = @desc OR i.MCOInvNo = @invoice)
    GROUP BY i.MCOInvNo;

    SELECT RTRIM(PostType) AS PostType, RTRIM(BatchNo) AS BatchNo, PostDate, RTRIM(PostBy) AS PostBy,
        RTRIM(AccMonth) + N'/' + RTRIM(AccYear) AS AccPeriod, COUNT(*) AS Rows
    FROM genisis.WS_SUNPosting
    WHERE @no IS NOT NULL AND @no <> N'' AND TransRef = @no
    GROUP BY PostType, BatchNo, PostDate, PostBy, AccMonth, AccYear
    ORDER BY PostDate;
END
GO

-- ===== main/360_invoice_processing.sql =====
-- Invoice Processing (legacy frmInvoice, menu Invoice > Invoice Processing), read side: the case an invoice belongs to, the case's invoices, and one invoice's amounts by worksheet row.
-- The row mapping is the one the worksheet engine uses (WorksheetEngine_Actual), so an invoice's rows add up to the worksheet's actual amounts.

CREATE OR ALTER PROCEDURE genisis.Invoice_Case
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(c.CASNumber) AS CaseNo, RTRIM(c.MBMNumber) AS MemberNo, RTRIM(c.MBMPatientCovID) AS CoverId, RTRIM(m.MBMName) AS MemberName, RTRIM(cp.MBMCName) AS PatientName,
        RTRIM(m.PAYCode) AS Payor, RTRIM(m.PLNCode) AS PlanCode, RTRIM(m.INSCode) AS InsuredType, RTRIM(m.HLTCode) AS HealthType, RTRIM(c.CASHOSCode) AS HospitalCode, RTRIM(h.HOSName) AS Hospital,
        c.CASDateAdmitted AS Admitted, c.CASDischargeDate AS Discharged, RTRIM(c.CASStayType) AS StayType, RTRIM(c.CASPAYType) AS PayType, RTRIM(c.CASClaimability) AS Claimability,
        (SELECT MAX(w.ClaimVersion) FROM genisis.WS_Claim w WHERE w.CASNumber = c.CASNumber) AS LastWorksheetVersion,
        (SELECT ISNULL(MAX(w.ClaimVersion), 0) + 1 FROM genisis.WS_Claim w WHERE w.CASNumber = c.CASNumber AND RTRIM(w.ClaimStatus) = N'C') AS NextWorksheetVersion, RTRIM(c.ModalityCode) AS Modality,
        (SELECT TOP (1) r.Remarks FROM genisis.WS_ClaimInvoiceRemarks r WHERE r.CASNumber = c.CASNumber) AS Remarks
    FROM dbo.CAS c
    JOIN dbo.MBM m ON m.MBMNumber = c.MBMNumber
    LEFT JOIN dbo.HOS h ON h.HOSCode = c.CASHOSCode
    LEFT JOIN dbo.MBMCoveredPersons cp ON cp.MBMCNumber = c.MBMNumber AND cp.MBMCCoverID = c.MBMPatientCovID
    WHERE c.CASNumber = @cas;
END
GO

CREATE OR ALTER PROCEDURE genisis.Invoice_List
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT i.INVIndex AS [Index], RTRIM(i.InvNo) AS InvoiceNo, i.InvDate AS InvoiceDate, RTRIM(i.INVPayTo) AS PayTo, RTRIM(i.INVPayee) AS Payee,
        CASE WHEN RTRIM(i.INVPayTo) = N'H' THEN RTRIM(h.HOSName) ELSE RTRIM(i.INVPayee) END AS PayeeName,
        i.INVAmount AS Amount, CAST(CASE WHEN ISNULL(i.INVWSed, 0) = 0 THEN 0 ELSE 1 END AS bit) AS InWorksheet, RTRIM(i.INVWSversion) AS WorksheetVersion,
        RTRIM(i.INVBordxRefNo) AS BordxNo, RTRIM(i.INVStayType) AS StayType, RTRIM(i.INVPAYType) AS PayType, RTRIM(i.CLMINVRecMode) AS ReceiptMode,
        i.CLMINVRecDate AS ReceiptDate, i.INVFolUpDate AS FollowUpDate, RTRIM(i.INVRoomType) AS RoomType, i.InvExcessAmt AS ExcessAmount, i.InvPayAmt AS PayAmount,
        i.INVInitialActAmt AS InitialAmount, RTRIM(i.CLMUHCNo) AS UhcNo, RTRIM(i.INVEnteredBy) AS EnteredBy, i.INVEnteredDate AS EnteredDate
    FROM genisis.WS_ClaimInvoice i
    LEFT JOIN dbo.HOS h ON h.HOSCode = RTRIM(i.INVPayee) AND RTRIM(i.INVPayTo) = N'H'
    WHERE i.CASNumber = @cas
    ORDER BY i.INVIndex;
END
GO

CREATE OR ALTER PROCEDURE genisis.Invoice_Rows
    @invoice int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT n.Slot, ISNULL(SUM(inv.Amt), 0) AS Amount
    FROM (VALUES (1), (2), (3), (4), (5), (6), (7), (8), (9), (10), (11), (12), (13), (14), (15), (16), (17), (18), (19), (20), (21), (22), (23), (24), (25), (26), (27), (28), (29)) n(Slot)
    LEFT JOIN (
        SELECT v.Slot, v.Amt
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
        WHERE i.INVIndex = @invoice
    ) inv ON inv.Slot = n.Slot
    GROUP BY n.Slot
    ORDER BY n.Slot;
END
GO

-- ===== main/361_finance_invoice_receipt.sql =====
-- Finance > Invoice Receipt (Outstanding) and Notification / Suspension (legacy FrmRecMCO / frmRecMCOManual). The desktop forms in the
-- source write RECMCO / RECOthers, which have no rows since 2010; the receipts entered since then are in HISPayment.dbo.ReceiptSummary
-- (fee receipts: ReceiptType IPFEE / OPFEE, ReceiptDesc = the member MCO invoice number, sometimes "<payor>-<invoice>", RecPayCode =
-- the invoice's payor, receipt number from RecSeq exactly like the desktop's GenerateSRecNo). The screens are rebuilt on those
-- tables; nothing existing is altered.

-- The receipt number prefixes in use (RecSeq.RCInd).
CREATE OR ALTER PROCEDURE genisis.InvoiceReceipt_Lookups
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(RCInd) AS Code, RTRIM(RCInd) AS Name FROM genisis.Pay_RecSeq WHERE RTRIM(ISNULL(RCInd, N'')) <> N'' ORDER BY RCInd;
    SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name FROM dbo.PAY ORDER BY PAYCode;
END
GO

-- The member MCO invoices with what has been received against them. Received = fee receipts whose description is the invoice
-- number or ends with "-<invoice number>". @payor: invoices with a member of that payor (membership numbers start with the payor
-- code). @outstandingOnly 1: invoices with nothing received or a balance above zero. @partPaidOnly 1 (Short Payment Receipt):
-- invoices with at least one receipt and a balance still above zero.
CREATE OR ALTER PROCEDURE genisis.InvoiceReceipt_SearchInvoices
    @invoiceFrom nvarchar(40) = NULL, @invoiceTo nvarchar(40) = NULL, @dateFrom datetime = NULL, @dateTo datetime = NULL,
    @payor nvarchar(4) = NULL, @outstandingOnly bit = 1, @partPaidOnly bit = 0, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(ReceiptDesc) AS ReceiptDesc, ReceiptAmt INTO #fees
    FROM genisis.Pay_ReceiptSummary WHERE ReceiptType IN (N'IPFEE', N'OPFEE') AND RTRIM(ISNULL(ReceiptDesc, N'')) <> N'';

    SELECT RTRIM(i.MCOInvNo) AS InvoiceNo, MIN(i.MCOInvDate) AS InvoiceDate, COUNT(*) AS Members, SUM(ISNULL(i.BillMCOAmt, 0)) AS BillAmount,
        MIN(LEFT(i.MBMNumber, 2)) AS Payor
    INTO #inv
    FROM dbo.MBMMCOInvoice i
    WHERE RTRIM(ISNULL(i.MCOInvNo, N'')) <> N''
      AND (@invoiceFrom IS NULL OR i.MCOInvNo >= @invoiceFrom) AND (@invoiceTo IS NULL OR i.MCOInvNo <= @invoiceTo)
      AND (@dateFrom IS NULL OR i.MCOInvDate >= @dateFrom) AND (@dateTo IS NULL OR i.MCOInvDate <= @dateTo)
    GROUP BY i.MCOInvNo
    HAVING @payor IS NULL OR SUM(CASE WHEN LEFT(i.MBMNumber, 2) = @payor THEN 1 ELSE 0 END) > 0
    OPTION (RECOMPILE);

    SELECT TOP (@max) v.InvoiceNo, v.InvoiceDate, v.Members, v.BillAmount, v.Payor, ISNULL(r.Receipts, 0) AS Receipts,
        ISNULL(r.Received, 0) AS Received, v.BillAmount - ISNULL(r.Received, 0) AS Outstanding
    FROM #inv v
    OUTER APPLY (SELECT COUNT(*) AS Receipts, SUM(f.ReceiptAmt) AS Received FROM #fees f
                 WHERE f.ReceiptDesc = v.InvoiceNo OR f.ReceiptDesc LIKE N'%-' + v.InvoiceNo) r
    WHERE (@outstandingOnly = 0 OR r.Receipts IS NULL OR r.Receipts = 0 OR v.BillAmount - ISNULL(r.Received, 0) > 0)
      AND (@partPaidOnly = 0 OR (r.Receipts > 0 AND v.BillAmount - ISNULL(r.Received, 0) > 0))
    ORDER BY v.InvoiceDate DESC, v.InvoiceNo DESC
    OPTION (RECOMPILE);
END
GO

-- The fee receipts recorded against one invoice (same matching as above), newest first.
CREATE OR ALTER PROCEDURE genisis.InvoiceReceipt_Receipts
    @invoiceNo nvarchar(40)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ReceiptIndex AS Id, RTRIM(ReceiptNo) AS ReceiptNo, ReceiptDate, ReceiptAmt AS Amount, RTRIM(ReceiptType) AS ReceiptType,
        RTRIM(ReceiptDesc) AS Description, RTRIM(RecChqNo) AS ChequeNo, RTRIM(RecPayCode) AS Payor, RTRIM(RecBatchNo) AS BatchNo,
        CAST(CASE WHEN RecPayStatus = N'1' THEN 1 ELSE 0 END AS bit) AS Posted
    FROM genisis.Pay_ReceiptSummary
    WHERE ReceiptType IN (N'IPFEE', N'OPFEE') AND (RTRIM(ReceiptDesc) = @invoiceNo OR RTRIM(ReceiptDesc) LIKE N'%-' + @invoiceNo)
    ORDER BY ReceiptDate DESC, ReceiptIndex DESC;
END
GO

-- GenerateSRecNo(prefix): the next receipt number <prefix><5 digits> from RecSeq, locked so two users cannot take the same one.
-- Numbers already used in ReceiptSummary / ReceiptfrPAY are skipped. A prefix without a RecSeq row starts at 10001 (the desktop
-- returned "10001" without the prefix there; here the prefix is kept). Runs inside the caller's transaction.
CREATE OR ALTER PROCEDURE genisis.FinanceReceipt_NextNumber
    @prefix nvarchar(2)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @seq int, @no nvarchar(40);
    SELECT @seq = CAST(RecSeqNo AS int) FROM genisis.Pay_RecSeq WITH (UPDLOCK, HOLDLOCK) WHERE RCInd = @prefix;
    IF @seq IS NULL
    BEGIN
        SET @seq = 10001;
        INSERT INTO genisis.Pay_RecSeq (RecSeqNo, RCInd) VALUES (N'10001', @prefix);
    END
    ELSE
        SET @seq = @seq + 1;
    SET @no = @prefix + RIGHT(N'00000' + CAST(@seq AS nvarchar(10)), 5);
    WHILE EXISTS (SELECT 1 FROM genisis.Pay_ReceiptSummary WHERE ReceiptNo = @no) OR EXISTS (SELECT 1 FROM genisis.Pay_ReceiptfrPAY WHERE ReceiptNo = @no)
    BEGIN
        SET @seq = @seq + 1;
        SET @no = @prefix + RIGHT(N'00000' + CAST(@seq AS nvarchar(10)), 5);
    END
    UPDATE genisis.Pay_RecSeq SET RecSeqNo = RIGHT(N'00000' + CAST(@seq AS nvarchar(10)), 5) WHERE RCInd = @prefix;
    SELECT @no AS ReceiptNo;
END
GO

-- Saves one receipt header (unposted: no SUN batch). Refused when the receipt number is already used. Result: 0 done (with the new
-- ReceiptIndex), 2 refused.
CREATE OR ALTER PROCEDURE genisis.FinanceReceipt_Insert
    @receiptNo nvarchar(40), @receiptDate date, @amount money, @receiptType nvarchar(30), @description nvarchar(100),
    @chequeNo nvarchar(40), @payor nvarchar(4)
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM genisis.Pay_ReceiptSummary WITH (UPDLOCK, HOLDLOCK) WHERE ReceiptNo = @receiptNo)
       OR EXISTS (SELECT 1 FROM genisis.Pay_ReceiptfrPAY WHERE ReceiptNo = @receiptNo)
    BEGIN
        SELECT 2 AS Result, CAST(NULL AS int) AS Id;
        RETURN;
    END
    INSERT INTO genisis.Pay_ReceiptSummary (ReceiptNo, ReceiptDate, ReceiptAmt, ReceiptType, ReceiptDesc, RecChqNo, RecPayCode)
    VALUES (@receiptNo, @receiptDate, @amount, @receiptType, @description, @chequeNo, @payor);
    SELECT 0 AS Result, CAST(SCOPE_IDENTITY() AS int) AS Id;
END
GO

-- ===== main/363_finance_reverse_receipt.sql =====
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

-- ===== main/364_finance_suspend_active.sql =====
-- Finance > Suspend / Active (legacy frmPaymentMBMReissue; its source is not available). Rebuilt on the member payment vouchers in
-- HISPayment.dbo.Payment2MBM and the status codes frmPostingPay2MBM lists: A Active, C Cancel, E Expired, R Reissue. The data shows:
--   * stopping a payment (cancel / expired cheque) adds a row with the SAME PV no., the negative amount, the original cheque no. and
--     cheque date, the action date as PV date, status C or E and cheque status Y; the original row stays A;
--   * re-issuing adds a row with a NEW PV no. of the same prefix (CB: PVNSeq2013, CC: PVMedicaSeq2013, CG: PVMedicaMXSeq), the positive
--     amount, the new cheque no. / transfer reference, the action date and status R.
-- Nothing is updated or deleted; SUN posting (frmPostingPay2MBM) picks the C / E / R rows up as before.

-- The vouchers that match; NULL = no criterion. Each row says whether it has been stopped (the C / E row of its PV no.) and the
-- reissue that replaced it (an R row of the same case / version added after the stop).
CREATE OR ALTER PROCEDURE genisis.SuspendActive_Search
    @pvFrom nvarchar(30) = NULL, @pvTo nvarchar(30) = NULL, @dateFrom datetime = NULL, @dateTo datetime = NULL,
    @caseNo nvarchar(30) = NULL, @chequeNo nvarchar(40) = NULL, @payee nvarchar(120) = NULL, @status char(1) = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max)
        p.PVMBMIndex AS Id, RTRIM(p.PVNo) AS PvNo, p.PVDate, p.PVChequeAmt AS Amount, RTRIM(p.PVChequeNo) AS ChequeNo, p.PVChequeDate AS ChequeDate,
        RTRIM(p.PVStatus) AS Status, RTRIM(p.PVChequeStatus) AS ChequeStatus, RTRIM(p.PVPymtMode) AS PaymentMode, RTRIM(p.PVPayto) AS PayTo,
        RTRIM(p.PVPayeeBank) AS PayeeBank,
        CASE WHEN LEN(RTRIM(ISNULL(p.PVPayeeACNo, N''))) > 4 THEN N'****' + RIGHT(RTRIM(p.PVPayeeACNo), 4) ELSE RTRIM(p.PVPayeeACNo) END AS PayeeAccount,
        RTRIM(p.CASNumber) AS CaseNo, RTRIM(p.ClaimVersion) AS ClaimVersion, RTRIM(p.Remark) AS Remark,
        RTRIM(p.Pay2MBMLastUpdateUser) AS UpdatedBy, p.Pay2MBMLastUpdateDate AS UpdatedOn,
        st.StopStatus AS StoppedAs, st.StopDate AS StoppedOn, ri.PvNo AS ReissuedAs
    FROM genisis.Pay_Payment2MBM p
    OUTER APPLY (SELECT TOP 1 RTRIM(s.PVStatus) AS StopStatus, s.PVDate AS StopDate, s.PVMBMIndex AS StopId FROM genisis.Pay_Payment2MBM s
                 WHERE RTRIM(s.PVNo) = RTRIM(p.PVNo) AND s.PVStatus IN ('C', 'E') AND s.PVMBMIndex <> p.PVMBMIndex
                 ORDER BY s.PVMBMIndex) st
    OUTER APPLY (SELECT TOP 1 RTRIM(r.PVNo) AS PvNo FROM genisis.Pay_Payment2MBM r
                 WHERE st.StopId IS NOT NULL AND r.CASNumber = p.CASNumber AND r.ClaimVersion = p.ClaimVersion AND r.PVStatus = 'R'
                   AND r.PVMBMIndex > st.StopId
                 ORDER BY r.PVMBMIndex) ri
    WHERE LEFT(p.PVNo, 2) IN (N'CB', N'CC', N'CG')
      AND (@pvFrom IS NULL OR p.PVNo >= @pvFrom) AND (@pvTo IS NULL OR p.PVNo <= @pvTo)
      AND (@dateFrom IS NULL OR p.PVDate >= @dateFrom) AND (@dateTo IS NULL OR p.PVDate <= @dateTo)
      AND (@caseNo IS NULL OR p.CASNumber = @caseNo)
      AND (@chequeNo IS NULL OR p.PVChequeNo = @chequeNo)
      AND (@payee IS NULL OR p.PVPayto LIKE N'%' + @payee + N'%')
      AND (@status IS NULL OR p.PVStatus = @status)
    ORDER BY p.PVDate DESC, p.PVNo DESC, p.PVMBMIndex DESC
    OPTION (RECOMPILE);
END
GO

-- Cancel (C) or expire (E) voucher @id. Only an active (A) or re-issued (R) voucher with a positive amount that has not been stopped
-- yet. Result: 0 done, 1 not found, 2 refused.
CREATE OR ALTER PROCEDURE genisis.SuspendActive_Stop
    @id int, @status char(1), @actionDate datetime, @remark nvarchar(400), @userCode nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @pv nvarchar(30), @amount money, @current char(1);
    SELECT @pv = RTRIM(PVNo), @amount = PVChequeAmt, @current = PVStatus FROM genisis.Pay_Payment2MBM WITH (UPDLOCK, HOLDLOCK) WHERE PVMBMIndex = @id;
    IF @pv IS NULL
    BEGIN
        SELECT 1 AS Result;
        RETURN;
    END
    IF @status NOT IN ('C', 'E') OR ISNULL(@current, '') NOT IN ('A', 'R') OR ISNULL(@amount, 0) <= 0
       OR EXISTS (SELECT 1 FROM genisis.Pay_Payment2MBM WITH (UPDLOCK, HOLDLOCK) WHERE RTRIM(PVNo) = @pv AND PVStatus IN ('C', 'E'))
    BEGIN
        SELECT 2 AS Result;
        RETURN;
    END
    INSERT INTO genisis.Pay_Payment2MBM (PVNo, PVDate, PVChequeAmt, PVChequeDate, PVChequeNo, PVPayto, PVPayeeBank, PVPayeeACNo, InvNo, CASNumber,
        ClaimVersion, Remark, Pay2MBMLastUpdateDate, Pay2MBMLastUpdateUser, MBMPaidAmt, PAYAccPeriodMth, PAYAccPeriodYr, PVPayMBMStatus, PVStatus,
        PVChequeStatus, PVCaseRemark)
    SELECT PVNo, CONVERT(datetime, CONVERT(char(8), @actionDate, 112)), -PVChequeAmt, PVChequeDate, PVChequeNo, PVPayto, PVPayeeBank, PVPayeeACNo,
        InvNo, CASNumber, ClaimVersion, ISNULL(NULLIF(@remark, N''), Remark), CONVERT(datetime, CONVERT(char(8), GETDATE(), 112)), @userCode,
        -MBMPaidAmt, RIGHT(N'0' + CAST(MONTH(@actionDate) AS nvarchar(2)), 2), CAST(YEAR(@actionDate) AS nvarchar(4)), PVPayMBMStatus, @status,
        'Y', PVCaseRemark
    FROM genisis.Pay_Payment2MBM WHERE PVMBMIndex = @id;
    SELECT 0 AS Result;
END
GO

-- Re-issue stopped voucher @id under a new PV no. of its prefix. Only a voucher that has been stopped and not re-issued yet.
-- Result: 0 done (with the new PV no.), 1 not found, 2 refused.
CREATE OR ALTER PROCEDURE genisis.SuspendActive_Reissue
    @id int, @actionDate datetime, @chequeNo nvarchar(40), @paymentMode nvarchar(20), @payeeBank nvarchar(200), @payeeAccount nvarchar(60),
    @remark nvarchar(400), @userCode nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @pv nvarchar(30), @prefix nvarchar(2), @cas nvarchar(30), @ver nvarchar(100), @stopId int, @seq int, @newPv nvarchar(30);
    SELECT @pv = RTRIM(PVNo), @prefix = LEFT(PVNo, 2), @cas = CASNumber, @ver = ClaimVersion
    FROM genisis.Pay_Payment2MBM WITH (UPDLOCK, HOLDLOCK) WHERE PVMBMIndex = @id AND ISNULL(PVChequeAmt, 0) > 0;
    IF @pv IS NULL
    BEGIN
        SELECT 1 AS Result, CAST(NULL AS nvarchar(30)) AS PvNo;
        RETURN;
    END
    SELECT TOP 1 @stopId = PVMBMIndex FROM genisis.Pay_Payment2MBM WHERE RTRIM(PVNo) = @pv AND PVStatus IN ('C', 'E') ORDER BY PVMBMIndex;
    IF @stopId IS NULL OR @prefix NOT IN (N'CB', N'CC', N'CG')
       OR EXISTS (SELECT 1 FROM genisis.Pay_Payment2MBM WITH (UPDLOCK, HOLDLOCK)
                  WHERE CASNumber = @cas AND ClaimVersion = @ver AND PVStatus = 'R' AND PVMBMIndex > @stopId)
    BEGIN
        SELECT 2 AS Result, CAST(NULL AS nvarchar(30)) AS PvNo;
        RETURN;
    END

    IF @prefix = N'CB'
    BEGIN
        SELECT @seq = CAST(PVSeqNo AS int) FROM genisis.Pay_PVNSeq2013 WITH (UPDLOCK, HOLDLOCK);
        SET @seq = @seq + 1;
        WHILE EXISTS (SELECT 1 FROM genisis.Pay_Payment2MBM WHERE RTRIM(PVNo) = N'CB' + RIGHT(N'00000000' + CAST(@seq AS nvarchar(10)), 8))
           OR EXISTS (SELECT 1 FROM genisis.Pay_Payment2HOS WHERE RTRIM(PVHOSNo) = N'CB' + RIGHT(N'00000000' + CAST(@seq AS nvarchar(10)), 8))
            SET @seq = @seq + 1;
        UPDATE genisis.Pay_PVNSeq2013 SET PVSeqNo = RIGHT(N'00000000' + CAST(@seq AS nvarchar(10)), 8);
    END
    ELSE IF @prefix = N'CC'
    BEGIN
        SELECT @seq = CAST(PVSeqNo AS int) FROM genisis.Pay_PVMedicaSeq2013 WITH (UPDLOCK, HOLDLOCK);
        SET @seq = @seq + 1;
        WHILE EXISTS (SELECT 1 FROM genisis.Pay_Payment2MBM WHERE RTRIM(PVNo) = N'CC' + RIGHT(N'00000000' + CAST(@seq AS nvarchar(10)), 8))
           OR EXISTS (SELECT 1 FROM genisis.Pay_Payment2HOS WHERE RTRIM(PVHOSNo) = N'CC' + RIGHT(N'00000000' + CAST(@seq AS nvarchar(10)), 8))
            SET @seq = @seq + 1;
        UPDATE genisis.Pay_PVMedicaSeq2013 SET PVSeqNo = RIGHT(N'00000000' + CAST(@seq AS nvarchar(10)), 8);
    END
    ELSE
    BEGIN
        SELECT @seq = CAST(PVSeqNo AS int) FROM genisis.Pay_PVMedicaMXSeq WITH (UPDLOCK, HOLDLOCK);
        SET @seq = @seq + 1;
        WHILE EXISTS (SELECT 1 FROM genisis.Pay_Payment2MBM WHERE RTRIM(PVNo) = N'CG' + RIGHT(N'00000000' + CAST(@seq AS nvarchar(10)), 8))
           OR EXISTS (SELECT 1 FROM genisis.Pay_Payment2HOS WHERE RTRIM(PVHOSNo) = N'CG' + RIGHT(N'00000000' + CAST(@seq AS nvarchar(10)), 8))
            SET @seq = @seq + 1;
        UPDATE genisis.Pay_PVMedicaMXSeq SET PVSeqNo = RIGHT(N'00000000' + CAST(@seq AS nvarchar(10)), 8);
    END
    SET @newPv = @prefix + RIGHT(N'00000000' + CAST(@seq AS nvarchar(10)), 8);

    INSERT INTO genisis.Pay_Payment2MBM (PVNo, PVDate, PVChequeAmt, PVChequeDate, PVChequeNo, PVPayto, PVPayeeBank, PVPayeeACNo, InvNo, CASNumber,
        ClaimVersion, Remark, Pay2MBMLastUpdateDate, Pay2MBMLastUpdateUser, MBMPaidAmt, PAYAccPeriodMth, PAYAccPeriodYr, PVPayMBMStatus, PVStatus,
        PVPymtMode, PVCaseRemark)
    SELECT @newPv, CONVERT(datetime, CONVERT(char(8), @actionDate, 112)), PVChequeAmt, CONVERT(datetime, CONVERT(char(8), @actionDate, 112)), @chequeNo,
        PVPayto, ISNULL(NULLIF(@payeeBank, N''), PVPayeeBank), ISNULL(NULLIF(@payeeAccount, N''), PVPayeeACNo), InvNo, CASNumber, ClaimVersion,
        @remark, CONVERT(datetime, CONVERT(char(8), GETDATE(), 112)), @userCode, MBMPaidAmt, RIGHT(N'0' + CAST(MONTH(@actionDate) AS nvarchar(2)), 2),
        CAST(YEAR(@actionDate) AS nvarchar(4)), PVPayMBMStatus, 'R', NULLIF(@paymentMode, N''), PVCaseRemark
    FROM genisis.Pay_Payment2MBM WHERE PVMBMIndex = @id;
    SELECT 0 AS Result, @newPv AS PvNo;
END
GO

-- ===== main/365_finance_electronic_banking.sql =====
-- Finance > Electronic Banking (legacy frmFileExportBankESP; its source is not available, and no bank file layout exists in the code or
-- databases). Rebuilt from the data: electronic payments are the payment vouchers with PVPymtMode 'ONLINE' in HISPayment.dbo.Payment2MBM
-- (members: payee, bank, account on the voucher) and Payment2HOS (hospitals: one PV no. per payment, one row per case; no bank details
-- on the voucher). PVChequeNo holds the crediting date (ddmmyyyy) on ONLINE vouchers. The account length each bank expects is in
-- HISMaintenance.dbo.BankAccounts. Read only: the API builds the file; nothing is marked as exported.

-- One row per ONLINE payment (PV no.). @payeeType 'M' members, 'H' hospitals, NULL both. Member payments that were stopped (a C / E row
-- with the same PV no.) and the stop rows themselves are left out; so are hospital vouchers that are not active.
CREATE OR ALTER PROCEDURE genisis.ElectronicBanking_Search
    @payeeType char(1) = NULL, @dateFrom datetime = NULL, @dateTo datetime = NULL, @creditRef nvarchar(40) = NULL,
    @pvFrom nvarchar(30) = NULL, @pvTo nvarchar(30) = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) x.PayeeType, x.PvNo, x.PvDate, x.CreditRef, x.Payee, x.Bank, x.Account, x.Amount, x.Cases,
        d.AccountDigits, d.AccountDigitsTwo
    FROM (
        SELECT 'M' AS PayeeType, RTRIM(m.PVNo) AS PvNo, MIN(m.PVDate) AS PvDate, MIN(RTRIM(m.PVChequeNo)) AS CreditRef, MIN(RTRIM(m.PVPayto)) AS Payee,
            MIN(RTRIM(m.PVPayeeBank)) AS Bank, MIN(RTRIM(m.PVPayeeACNo)) AS Account, SUM(m.PVChequeAmt) AS Amount, COUNT(*) AS Cases
        FROM genisis.Pay_Payment2MBM m
        WHERE (@payeeType IS NULL OR @payeeType = 'M') AND m.PVPymtMode = N'ONLINE' AND m.PVChequeAmt > 0 AND ISNULL(m.PVStatus, 'A') IN ('A', 'R')
          AND NOT EXISTS (SELECT 1 FROM genisis.Pay_Payment2MBM s WHERE RTRIM(s.PVNo) = RTRIM(m.PVNo) AND s.PVStatus IN ('C', 'E'))
          AND (@dateFrom IS NULL OR m.PVDate >= @dateFrom) AND (@dateTo IS NULL OR m.PVDate <= @dateTo)
          AND (@creditRef IS NULL OR m.PVChequeNo = @creditRef)
          AND (@pvFrom IS NULL OR m.PVNo >= @pvFrom) AND (@pvTo IS NULL OR m.PVNo <= @pvTo)
        GROUP BY RTRIM(m.PVNo)
        UNION ALL
        SELECT 'H', RTRIM(h.PVHOSNo), MIN(h.PVHOSDate), MIN(RTRIM(h.PVHOSChequeNo)), MIN(RTRIM(h.PVPayee)), MIN(RTRIM(h.PVBank)), MIN(RTRIM(h.PVAccountNo)),
            MAX(h.PVHOSChequeAmt), COUNT(*)
        FROM genisis.Pay_Payment2HOS h
        WHERE (@payeeType IS NULL OR @payeeType = 'H') AND h.PVPymtMode = N'ONLINE' AND h.PVHOSChequeAmt > 0 AND ISNULL(h.PVStatus, 'A') = 'A'
          AND (@dateFrom IS NULL OR h.PVHOSDate >= @dateFrom) AND (@dateTo IS NULL OR h.PVHOSDate <= @dateTo)
          AND (@creditRef IS NULL OR h.PVHOSChequeNo = @creditRef)
          AND (@pvFrom IS NULL OR h.PVHOSNo >= @pvFrom) AND (@pvTo IS NULL OR h.PVHOSNo <= @pvTo)
        GROUP BY RTRIM(h.PVHOSNo)
    ) x
    OUTER APPLY (SELECT TOP 1 b.AccountDigits, b.AccountDigitsTwo FROM genisis.Maint_BankAccounts b WHERE RTRIM(b.Bank) = x.Bank) d
    ORDER BY x.PvDate DESC, x.PvNo DESC
    OPTION (RECOMPILE);
END
GO

-- ===== main/370_finance_sun_posting.sql =====
-- Finance > SUN Posting (legacy frmPostingInvoiceMBM, "Member Invoice Posting", PostType 'InvMBM').
-- The desktop sent a WHERE fragment to the legacy dbo.SUN_HIS_Posting (dynamic SQL over HISWorkSheet.dbo.VIEW_SUN_Posting_MBMInv);
-- here every criterion is a typed parameter. Reads genisis.WS_SunPostingMbmInv, writes genisis.WS_SUNPosting / genisis.WS_SunPostBatch.

-- ComboListing: health types, payors and accounting periods (the post / bordx status and post type lists are fixed in the API).
CREATE OR ALTER PROCEDURE genisis.SunPostingMbm_Lookups
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(HLTCode) AS Code, RTRIM(HLTDescription) AS Name FROM dbo.HLT ORDER BY HLTCode;
    SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name FROM dbo.PAY ORDER BY PAYCode;
    SELECT RTRIM(AccMonth) + N'/' + RTRIM(AccYear) AS Code, RTRIM(AccMonth) + N'/' + RTRIM(AccYear) AS Name
    FROM genisis.Maint_AccPeriod
    ORDER BY AccYear DESC, AccMonth DESC;
END
GO

-- SearchRecords + MBMInvListing / MBMInvCRListing / MBMInvDRListing: the invoices of the view with the desktop's filters.
-- Inclusive ranges; NULL = no criterion. Dates compare with the stored datetime exactly as the desktop's 'yyyy/mm/dd' text did
-- (a "to" date matches rows stamped at midnight of that day). @postStatus '0': not posted (InvMStatus and PostStatus blank or 0),
-- '1': posted. @bordxStatus '0': no bordx reference, '1': has one. @amountSign 1: INVAmount > 0 (post type P), -1: < 0 (N),
-- NULL: either (the credit / debit lists). The patient name (covered person when the cover id is not 00) and the product
-- category (PLN.PLNProCat for the health type / payor; the API uses 'SM' for health type S) are joined here instead of one
-- query per row.
CREATE OR ALTER PROCEDURE genisis.SunPostingMbm_Search
    @healthType nvarchar(4) = NULL, @payor nvarchar(4) = NULL,
    @invoiceFrom datetime = NULL, @invoiceTo datetime = NULL,
    @caseFrom nvarchar(30) = NULL, @caseTo nvarchar(30) = NULL,
    @postStatus char(1) = NULL, @postFrom datetime = NULL, @postTo datetime = NULL,
    @bordxStatus char(1) = NULL, @batchFrom nvarchar(5) = NULL, @batchTo nvarchar(5) = NULL,
    @bordxFrom datetime = NULL, @bordxTo datetime = NULL,
    @amountSign smallint = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max)
        RTRIM(v.CASNumber) AS CaseNo, RTRIM(v.INVWSversion) AS ClaimVersion, RTRIM(v.MBMNumber) AS MembershipNo,
        RTRIM(v.MBMPatientCovID) AS PatientCoverId, RTRIM(v.MBMName) AS MemberName,
        CASE WHEN RTRIM(ISNULL(v.MBMPatientCovID, N'')) <> N'00' THEN RTRIM(cp.MBMCName) ELSE RTRIM(v.MBMName) END AS PatientName,
        RTRIM(v.InvMStatus) AS InvMStatus, RTRIM(v.PostStatus) AS PostStatus, v.PostDate, RTRIM(v.AccMonth) AS AccMonth, RTRIM(v.AccYear) AS AccYear,
        RTRIM(v.HLTCode) AS HealthType, RTRIM(v.PAYCode) AS Payor, RTRIM(v.MBMGroupCompany) AS GroupCompany,
        v.INVEnteredDate AS InvoiceDate, v.INVAmount AS Amount, RTRIM(v.BatchNo) AS BatchNo, v.BordxDate,
        RTRIM(v.INVBordxRefNo) AS BordxRefNo, RTRIM(v.CLMBordxRevInd) AS BordxReversal, RTRIM(pl.PLNProCat) AS ProductCategory
    FROM genisis.WS_SunPostingMbmInv v
    OUTER APPLY (SELECT TOP 1 c.MBMCName FROM dbo.MBMCoveredPersons c
                 WHERE c.MBMCNumber = RTRIM(v.MBMNumber) AND c.MBMCCoverID = RTRIM(v.MBMPatientCovID)) cp
    OUTER APPLY (SELECT TOP 1 p.PLNProCat FROM dbo.PLN p WHERE p.HLTCode = RTRIM(v.HLTCode) AND p.PAYCode = RTRIM(v.PAYCode)) pl
    WHERE (@healthType IS NULL OR v.HLTCode = @healthType)
      AND (@payor IS NULL OR v.PAYCode = @payor)
      AND (@invoiceFrom IS NULL OR v.INVEnteredDate >= @invoiceFrom) AND (@invoiceTo IS NULL OR v.INVEnteredDate <= @invoiceTo)
      AND (@caseFrom IS NULL OR v.CASNumber >= @caseFrom) AND (@caseTo IS NULL OR v.CASNumber <= @caseTo)
      AND (@postStatus IS NULL
           OR (@postStatus = '0' AND (v.InvMStatus IS NULL OR v.InvMStatus = '0') AND (v.PostStatus IS NULL OR v.PostStatus = '0'))
           OR (@postStatus = '1' AND v.InvMStatus = '1' AND v.PostStatus = '1'))
      AND (@postFrom IS NULL OR v.PostDate >= @postFrom) AND (@postTo IS NULL OR v.PostDate <= @postTo)
      AND (@bordxStatus IS NULL
           OR (@bordxStatus = '0' AND (v.INVBordxRefNo IS NULL OR v.INVBordxRefNo = N''))
           OR (@bordxStatus = '1' AND v.INVBordxRefNo <> N''))
      AND (@batchFrom IS NULL OR v.BatchNo >= @batchFrom) AND (@batchTo IS NULL OR v.BatchNo <= @batchTo)
      AND (@bordxFrom IS NULL OR v.BordxDate >= @bordxFrom) AND (@bordxTo IS NULL OR v.BordxDate <= @bordxTo)
      AND (@amountSign IS NULL OR (@amountSign = 1 AND v.INVAmount > 0) OR (@amountSign = -1 AND v.INVAmount < 0))
    ORDER BY v.CASNumber, v.INVWSversion
    OPTION (RECOMPILE);
END
GO

-- CheckTime("POSTING") + SpcAccess: the user's special access flag and the end time of the posting window. The API decides
-- with its clock (the desktop closed the form while the time of day was not after TimeMain.EndTime, unless SpecialAccess = 'Y').
CREATE OR ALTER PROCEDURE genisis.SunPosting_Window
    @userCode nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT (SELECT TOP 1 RTRIM(SpecialAccess) FROM genisis.Maint_USR WHERE USRCode = @userCode) AS SpecialAccess,
           (SELECT TOP 1 EndTime FROM genisis.Maint_TimeMain WHERE Process = N'POSTING') AS EndTime;
END
GO

-- GenSUNPostingBatchNo: the next batch number (the last one + 1, first batch 1001). Locked so two users cannot get the same
-- number; runs inside the caller's transaction, so a failed post gives the number back.
CREATE OR ALTER PROCEDURE genisis.SunPosting_NextBatch
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @next int;
    SELECT @next = MAX(CAST(BatchNo AS int)) + 1 FROM genisis.WS_SunPostBatch WITH (UPDLOCK, HOLDLOCK);
    IF @next IS NULL
    BEGIN
        SET @next = 1001;
        INSERT INTO genisis.WS_SunPostBatch (BatchNo) VALUES (CAST(@next AS nvarchar(20)));
    END
    ELSE
        UPDATE genisis.WS_SunPostBatch SET BatchNo = CAST(@next AS nvarchar(20));
    SELECT CAST(@next AS nvarchar(20)) AS BatchNo;
END
GO

-- CmdPost_Click: one SUNPosting row per invoice (InvMStatus 1, PostStatus 1, PostType InvMBM, today's date, the user, the
-- accounting month / year and the batch). The desktop only stopped a posted list in the screen; here an invoice already posted
-- as InvMBM is refused. Result: 0 done, 1 invoice not found, 2 refused (already posted).
CREATE OR ALTER PROCEDURE genisis.SunPostingMbm_Insert
    @cas nvarchar(30), @version nvarchar(4), @accMonth char(2), @accYear char(4), @batchNo nvarchar(5), @userCode nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM genisis.WS_ClaimInvoice WHERE CASNumber = @cas AND INVWSversion = @version)
    BEGIN
        SELECT 1 AS Result;
        RETURN;
    END
    IF EXISTS (SELECT 1 FROM genisis.WS_SUNPosting WITH (UPDLOCK, HOLDLOCK)
               WHERE CASNumber = @cas AND ClmVersion = @version AND PostType = N'InvMBM' AND InvMStatus = '1')
    BEGIN
        SELECT 2 AS Result;
        RETURN;
    END
    INSERT INTO genisis.WS_SUNPosting (CASNumber, ClmVersion, InvMStatus, PostStatus, PostType, PostDate, PostBy, AccMonth, AccYear, BatchNo)
    VALUES (@cas, @version, '1', '1', N'InvMBM', CONVERT(datetime, CONVERT(char(8), GETDATE(), 112)), @userCode, @accMonth, @accYear, @batchNo);
    SELECT 0 AS Result;
END
GO

-- ===== main/370_invoice_save.sql =====
-- Invoice Processing (legacy frmInvoice), write side. The save itself calls the legacy parameterized procedures of HISWorkSheet through the genisis.WS_* synonyms (000_setup.sql);
-- what is here are the reads the form needs and the two writes that have no legacy procedure (case remarks, remove).

-- getVersion: the worksheet version a new invoice is attached to: the case's last completed version plus one, or 1.
CREATE OR ALTER PROCEDURE genisis.Invoice_NextVersion
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ISNULL(MAX(w.ClaimVersion), 0) + 1 AS NextVersion FROM genisis.WS_Claim w WHERE w.CASNumber = @cas AND RTRIM(w.ClaimStatus) = N'C';
END
GO

-- Form_Load: the lists of the form. Stay types are PayType without the C (claim) and R (reimbursement) ones, as SearchStayType has them.
CREATE OR ALTER PROCEDURE genisis.Invoice_StayTypes
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(PayTypeCode) AS Code, RTRIM(PayTypeName) AS Name FROM dbo.PayType
    WHERE SUBSTRING(PayTypeCode, 1, 1) <> N'C' AND SUBSTRING(PayTypeCode, 1, 1) <> N'R' ORDER BY PayTypeCode;
END
GO

CREATE OR ALTER PROCEDURE genisis.Invoice_ReceiptModes
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(RecModeCode) AS Code, RTRIM(RecModeDesc) AS Name FROM genisis.Maint_InvRecMode ORDER BY RecModeCode;
END
GO

CREATE OR ALTER PROCEDURE genisis.Invoice_RoomTypes
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(RoomType) AS Code FROM genisis.Maint_HOSRoomType ORDER BY RoomType;
END
GO

CREATE OR ALTER PROCEDURE genisis.Invoice_Hospitals
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(HOSCode) AS Code, RTRIM(HOSName) AS Name FROM dbo.HOS ORDER BY HOSName;
END
GO

-- lstInvoice_Click: one invoice with every amount, as column name and value, so the form can be filled and saved again without the web page knowing the 106 columns.
CREATE OR ALTER PROCEDURE genisis.Invoice_Form
    @invoice int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM genisis.WS_ClaimInvoice WHERE INVIndex = @invoice;
    SELECT * FROM genisis.WS_ClaimInvoiceActual WHERE INVIndex = @invoice;
    SELECT * FROM genisis.WS_ClaimInvoiceActualOthers WHERE INVIndex = @invoice;
    SELECT * FROM genisis.WS_ClaimInvoiceNotCover WHERE INVIndex = @invoice;
END
GO

CREATE OR ALTER PROCEDURE genisis.Invoice_Remarks
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Remarks FROM genisis.WS_ClaimInvoiceRemarks WHERE CASNumber = @cas;
END
GO

-- SaveINVRemarks: one remark per case, added or replaced. An empty remark is not added for a case that has none (the desktop adds a blank row; it means nothing and every invoice save would leave one).
CREATE OR ALTER PROCEDURE genisis.Invoice_SaveRemarks
    @cas nvarchar(15),
    @remarks nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM genisis.WS_ClaimInvoiceRemarks WHERE CASNumber = @cas) AND ISNULL(@remarks, N'') = N''
        RETURN;
    IF EXISTS (SELECT 1 FROM genisis.WS_ClaimInvoiceRemarks WHERE CASNumber = @cas)
        UPDATE genisis.WS_ClaimInvoiceRemarks SET Remarks = ISNULL(@remarks, N'') WHERE CASNumber = @cas;
    ELSE
        INSERT INTO genisis.WS_ClaimInvoiceRemarks (CASNumber, Remarks) VALUES (@cas, ISNULL(@remarks, N''));
END
GO

-- CmdRemove_Click: the desktop deletes ClaimInvoice, ClaimInvoiceActual and ClaimInvoiceNotCover; the other two rows of the invoice (the entries and the GST amounts) are deleted too so none is left behind.
CREATE OR ALTER PROCEDURE genisis.Invoice_Remove
    @invoice int
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM genisis.WS_ClaimInvoiceActualGST WHERE INVIndex = @invoice;
    DELETE FROM genisis.WS_ClaimInvoiceActualOthers WHERE INVIndex = @invoice;
    DELETE FROM genisis.WS_ClaimInvoiceNotCover WHERE INVIndex = @invoice;
    DELETE FROM genisis.WS_ClaimInvoiceActual WHERE INVIndex = @invoice;
    DELETE FROM genisis.WS_ClaimInvoice WHERE INVIndex = @invoice;
END
GO

-- The new invoice's key: the legacy insert does not return it, so it is read back by case and number (the desktop takes the highest key of the table).
CREATE OR ALTER PROCEDURE genisis.Invoice_Key
    @cas nvarchar(15),
    @invoiceNo nvarchar(30)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) INVIndex AS InvoiceKey FROM genisis.WS_ClaimInvoice WHERE CASNumber = @cas AND RTRIM(InvNo) = RTRIM(@invoiceNo) ORDER BY INVIndex DESC;
END
GO

-- The payee hospital: its name (the form tests the generic provider names) and whether a discount is due (AX).
CREATE OR ALTER PROCEDURE genisis.Invoice_Hospital
    @code nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(HOSCode) AS Code, RTRIM(HOSName) AS Name, RTRIM(HOSDiscInd) AS DiscountIndicator FROM dbo.HOS WHERE RTRIM(HOSCode) = RTRIM(@code);
END
GO

-- saveActual: the hospital sums are kept for plans 2 to 5 and for new SM plans (PLN.NewSMPlan = Y).
CREATE OR ALTER PROCEDURE genisis.Invoice_PlanIsNewSm
    @plan nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.PLN WHERE RTRIM(PLNCode) = RTRIM(@plan) AND RTRIM(NewSMPlan) = N'Y') THEN 1 ELSE 0 END AS bit) AS NewSm;
END
GO

-- ===== main/380_invoice_original_update.sql =====
-- Invoice > Original Invoice Update (legacy frmUpdateInvoice, "Update Invoice"). The desktop opened a recordset on the first ClaimInvoice row of a
-- case and worksheet version and edited it in place; here a version's invoices are listed (most versions have one, 4,899 of 75,859 have more, and the
-- desktop could only ever reach the first of those) and one is updated by its key, with typed parameters.

-- cmdShow_Click / ShowInvoice: the invoices of a case and worksheet version, with the hospital the invoice is paid to. The hospital is found by its code;
-- the desktop found it by name, which is not unique.
CREATE OR ALTER PROCEDURE genisis.InvoiceOriginal_Find
    @cas nvarchar(30), @version nvarchar(4)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(i.INVIndex AS int) AS Id, RTRIM(i.CASNumber) AS CaseNo, RTRIM(i.INVWSversion) AS Version, RTRIM(i.InvNo) AS InvoiceNo,
           NULLIF(i.InvDate, '19000101') AS InvoiceDate, NULLIF(i.INVOrigRecDate, '19000101') AS OriginalReceivedDate, RTRIM(i.INVOrigRecStatus) AS OriginalStatus,
           RTRIM(i.INVPayTo) AS PayTo, RTRIM(i.INVPayee) AS HospitalCode, RTRIM(h.HOSAccCode) AS HospitalAccCode, RTRIM(h.HOSName) AS HospitalName
    FROM genisis.WS_ClaimInvoice i
    LEFT JOIN dbo.HOS h ON h.HOSCode = i.INVPayee
    WHERE i.CASNumber = @cas AND i.INVWSversion = @version
    ORDER BY i.INVIndex;
END
GO

-- cmdUpdate_Click: the invoice number, the date the original invoice was received, the invoice date and the hospital, and the original-receipt mark ('Y').
-- The hospital changes only when the code is a hospital (the desktop changed it only when the name it looked up was found); a blank invoice date leaves the
-- date as it was (the desktop assigned the mask text to the date column and failed). Returns the number of invoices changed.
CREATE OR ALTER PROCEDURE genisis.InvoiceOriginal_Update
    @id int, @invoiceNo nvarchar(100), @invoiceDate datetime = NULL, @originalDate datetime, @hospital nvarchar(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE i
    SET InvNo = @invoiceNo, INVOrigRecDate = @originalDate, InvDate = ISNULL(@invoiceDate, i.InvDate), INVOrigRecStatus = 'Y',
        INVPayee = CASE WHEN @hospital IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.HOS WHERE HOSCode = @hospital) THEN @hospital ELSE i.INVPayee END
    FROM genisis.WS_ClaimInvoice i
    WHERE i.INVIndex = @id;
    SELECT @@ROWCOUNT;
END
GO

-- ===== main/381_invoice_accounting.sql =====
-- Invoice > Invoice HOS Accounting (legacy FrmInvoiceHOSAC) and Invoice MBM Accounting (legacy FrmInvoiceMBMAC). Both list invoices that were not yet handed to
-- accounts (print status 0) or were (1, "reprint"), and "Print to File" gives the ones listed a batch (H or M, the day, a number of the day) and marks them printed.
-- The desktop built the WHERE clause as text; here every criterion is a typed parameter. Differences, all of which were read out of the desktop's code:
--  * The batch date criteria compared the text column INVBatchDate ("H23102026": H, ddmmyyyy) with a typed date as text, which cannot work; the date part of the
--    batch is compared as a date here.
--  * The hospital criterion compared the hospital's name with its hyphens turned into blanks, so a hospital with a hyphen was never found; the hospital's code is used.
--  * The hospital print marked every invoice of the worksheet version, whoever it was paid to (the member print only the member's); only the hospital's here.
--  * The hospital print kept the first digit of the batch number (Mid(batch, 11, 1)); the whole number is kept (the member print kept three digits).

-- The day a batch label stands for: "H23102026" or "M23102026" is 23 October 2026 (the desktop's batches have always been named this way); NULL for anything else.
-- HISDB runs at compatibility level 100, so no TRY_CONVERT: the date is built as yyyymmdd and tested with ISDATE.
CREATE OR ALTER FUNCTION genisis.InvoiceBatchDay (@batchDate nvarchar(100))
RETURNS date
AS
BEGIN
    DECLARE @iso varchar(8) = SUBSTRING(@batchDate, 6, 4) + SUBSTRING(@batchDate, 4, 2) + SUBSTRING(@batchDate, 2, 2);
    RETURN CASE WHEN LEN(@batchDate) = 9 AND @iso NOT LIKE '%[^0-9]%' AND ISDATE(@iso) = 1 THEN CONVERT(date, @iso, 112) END;
END
GO

-- GenerateINVHSeqNo / GenerateINVMSeqNo: the next batch of a kind for a day, "H" or "M" then ddmmyyyy, a hyphen and the number of the day (the first batch of a day is 1).
CREATE OR ALTER PROCEDURE genisis.InvoiceAccounting_NextBatch
    @kind char(1), @day date
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @n int;
    IF @kind = 'H'
    BEGIN
        UPDATE genisis.WS_INVACHSEQ WITH (UPDLOCK, HOLDLOCK) SET @n = InvACBatch = InvACBatch + 1 WHERE CAST(InvACDate AS date) = @day;
        IF @@ROWCOUNT = 0
        BEGIN
            INSERT genisis.WS_INVACHSEQ (InvACDate, InvACBatch) VALUES (@day, 1);
            SET @n = 1;
        END
    END
    ELSE
    BEGIN
        UPDATE genisis.WS_INVACMSEQ WITH (UPDLOCK, HOLDLOCK) SET @n = InvACBatch = InvACBatch + 1 WHERE CAST(InvACDate AS date) = @day;
        IF @@ROWCOUNT = 0
        BEGIN
            INSERT genisis.WS_INVACMSEQ (InvACDate, InvACBatch) VALUES (@day, 1);
            SET @n = 1;
        END
    END
    SELECT CAST(@kind AS varchar(1)) + REPLACE(CONVERT(varchar(10), @day, 103), '/', '') + '-' + CAST(@n AS varchar(10));
END
GO

-- SearchRecord + InvoiceAccountListing of FrmInvoiceHOSAC: the invoices paid to a hospital. @reprint = 0: not printed yet; 1: printed. Every "from"/"to" pair
-- is inclusive and ignored when NULL.
CREATE OR ALTER PROCEDURE genisis.InvoiceHospitalAccounting_Search
    @reprint bit, @invFrom datetime = NULL, @invTo datetime = NULL, @regFrom datetime = NULL, @regTo datetime = NULL, @hospital nvarchar(100) = NULL,
    @batchDateFrom date = NULL, @batchDateTo date = NULL, @batchNoFrom int = NULL, @batchNoTo int = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(INVPayee) AS HospitalCode, RTRIM(HOSName) AS Hospital, RTRIM(InvNo) AS InvoiceNo, NULLIF(InvDate, '19000101') AS InvoiceDate,
           NULLIF(INVEnteredDate, '19000101') AS RegisteredDate, RTRIM(CASNumber) AS CaseNo, RTRIM(INVWSversion) AS Version, RTRIM(BordxRefNo) AS BordxNo,
           RTRIM(MBMNumber) AS MemberNo, RTRIM(MBMName) AS MemberName, RTRIM(PatientName) AS PatientName, CLMPayableByMedix2H AS Payable, InvPreviousAmt AS PreviousAmount,
           RTRIM(DebitCreditRefNo) AS DebitCreditRefNo, RTRIM(INVBatchDate) AS BatchDate, INVBatchNo AS BatchNo
    FROM genisis.WS_HOSInvoice
    WHERE INVPayTo = 'H'
      AND InvPrintStatus = CASE WHEN @reprint = 1 THEN '1' ELSE '0' END
      AND (@invFrom IS NULL OR InvDate >= @invFrom) AND (@invTo IS NULL OR InvDate < DATEADD(DAY, 1, @invTo))
      AND (@regFrom IS NULL OR INVEnteredDate >= @regFrom) AND (@regTo IS NULL OR INVEnteredDate < DATEADD(DAY, 1, @regTo))
      AND (@hospital IS NULL OR INVPayee = @hospital)
      AND (@batchDateFrom IS NULL OR genisis.InvoiceBatchDay(INVBatchDate) >= @batchDateFrom)
      AND (@batchDateTo IS NULL OR genisis.InvoiceBatchDay(INVBatchDate) <= @batchDateTo)
      AND (@batchNoFrom IS NULL OR INVBatchNo >= @batchNoFrom) AND (@batchNoTo IS NULL OR INVBatchNo <= @batchNoTo)
    ORDER BY INVPayee, InvDate, InvNo, CASNumber, INVWSversion;
END
GO

-- UpdatePrintRecord of FrmInvoiceHOSAC: the batch, the user, the printed mark and the amount payable (as the previous amount) on the hospital's invoices of every
-- worksheet version the criteria list. Returns how many invoices were marked.
CREATE OR ALTER PROCEDURE genisis.InvoiceHospitalAccounting_Print
    @batch nvarchar(20), @user nvarchar(20), @invFrom datetime = NULL, @invTo datetime = NULL, @regFrom datetime = NULL, @regTo datetime = NULL, @hospital nvarchar(100) = NULL,
    @batchDateFrom date = NULL, @batchDateTo date = NULL, @batchNoFrom int = NULL, @batchNoTo int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE i
    SET INVBatchDate = LEFT(@batch, 9), INVBatchNo = CASE WHEN SUBSTRING(@batch, 11, 10) NOT LIKE '%[^0-9]%' AND LEN(SUBSTRING(@batch, 11, 10)) > 0 THEN CAST(SUBSTRING(@batch, 11, 10) AS int) END, InvPrintLogon = @user, InvPrintStatus = '1', InvPreviousAmt = ISNULL(v.CLMPayableByMedix2H, 0)
    FROM genisis.WS_ClaimInvoice i
    JOIN (SELECT DISTINCT CASNumber, INVWSversion, CLMPayableByMedix2H FROM genisis.WS_HOSInvoice
          WHERE INVPayTo = 'H' AND InvPrintStatus = '0'
            AND (@invFrom IS NULL OR InvDate >= @invFrom) AND (@invTo IS NULL OR InvDate < DATEADD(DAY, 1, @invTo))
            AND (@regFrom IS NULL OR INVEnteredDate >= @regFrom) AND (@regTo IS NULL OR INVEnteredDate < DATEADD(DAY, 1, @regTo))
            AND (@hospital IS NULL OR INVPayee = @hospital)
            AND (@batchDateFrom IS NULL OR genisis.InvoiceBatchDay(INVBatchDate) >= @batchDateFrom)
            AND (@batchDateTo IS NULL OR genisis.InvoiceBatchDay(INVBatchDate) <= @batchDateTo)
            AND (@batchNoFrom IS NULL OR INVBatchNo >= @batchNoFrom) AND (@batchNoTo IS NULL OR INVBatchNo <= @batchNoTo)) v
      ON v.CASNumber = i.CASNumber AND v.INVWSversion = i.INVWSversion
    WHERE i.INVPayTo = 'H';
    SELECT @@ROWCOUNT;
END
GO

-- SearchRecord + InvoiceAccountListing of FrmInvoiceMBMAC: the worksheets with an amount payable to the member, by invoice, by the worksheet's close date, the payor (the first
-- two letters of the case number), the print status, the batch and the bordx date. Like the desktop it also lists the hospital-paid invoices of such worksheets (94 of
-- 14,681 rows; its first column says who each invoice was paid to), although its print marks only the member-paid ones.
CREATE OR ALTER PROCEDURE genisis.InvoiceMemberAccounting_Search
    @reprint bit, @closeFrom datetime = NULL, @closeTo datetime = NULL, @payor nvarchar(2) = NULL, @batchDateFrom date = NULL, @batchDateTo date = NULL,
    @batchNoFrom int = NULL, @batchNoTo int = NULL, @bordxFrom datetime = NULL, @bordxTo datetime = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(INVPayee) AS PayTo, RTRIM(INVPayTo) AS PayToCode, RTRIM(CASNumber) AS CaseNo, RTRIM(INVWSversion) AS Version, NULLIF(ClaimCloseDate, '19000101') AS ClaimCloseDate,
           RTRIM(DebitCreditRefNo) AS DebitCreditRefNo, RTRIM(BordxRefNo) AS BordxNo, RTRIM(MBMNumber) AS MemberNo, RTRIM(MBMName) AS MemberName,
           RTRIM(PatientName) AS PatientName, CLMPayableByMedix2M AS Payable, InvPreviousAmt AS PreviousAmount, NULLIF(BordxDate, '19000101') AS BordxDate,
           RTRIM(INVBatchDate) AS BatchDate, INVBatchNo AS BatchNo, RTRIM(PLNCode) AS PlanCode, CLMTotalActual AS TotalActual
    FROM genisis.WS_MBMInvoice1
    WHERE InvPrintStatus = CASE WHEN @reprint = 1 THEN '1' ELSE '0' END
      AND (@closeFrom IS NULL OR ClaimCloseDate >= @closeFrom) AND (@closeTo IS NULL OR ClaimCloseDate < DATEADD(DAY, 1, @closeTo))
      AND (@payor IS NULL OR LEFT(CASNumber, 2) = @payor)
      AND (@batchDateFrom IS NULL OR genisis.InvoiceBatchDay(INVBatchDate) >= @batchDateFrom)
      AND (@batchDateTo IS NULL OR genisis.InvoiceBatchDay(INVBatchDate) <= @batchDateTo)
      AND (@batchNoFrom IS NULL OR INVBatchNo >= @batchNoFrom) AND (@batchNoTo IS NULL OR INVBatchNo <= @batchNoTo)
      AND (@bordxFrom IS NULL OR BordxDate >= @bordxFrom) AND (@bordxTo IS NULL OR BordxDate < DATEADD(DAY, 1, @bordxTo))
    ORDER BY CASNumber, INVWSversion;
END
GO

-- UpdatePrintRecord of FrmInvoiceMBMAC: the batch, the user, the printed mark and the amount payable to the member on the member's invoices of every worksheet
-- version the criteria list. Returns how many invoices were marked.
CREATE OR ALTER PROCEDURE genisis.InvoiceMemberAccounting_Print
    @batch nvarchar(20), @user nvarchar(20), @closeFrom datetime = NULL, @closeTo datetime = NULL, @payor nvarchar(2) = NULL, @batchDateFrom date = NULL, @batchDateTo date = NULL,
    @batchNoFrom int = NULL, @batchNoTo int = NULL, @bordxFrom datetime = NULL, @bordxTo datetime = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE i
    SET INVBatchDate = LEFT(@batch, 9), INVBatchNo = CASE WHEN SUBSTRING(@batch, 11, 3) NOT LIKE '%[^0-9]%' AND LEN(SUBSTRING(@batch, 11, 3)) > 0 THEN CAST(SUBSTRING(@batch, 11, 3) AS int) END, InvPrintLogon = @user, InvPrintStatus = '1', InvPreviousAmt = ISNULL(v.CLMPayableByMedix2M, 0)
    FROM genisis.WS_ClaimInvoice i
    JOIN (SELECT DISTINCT CASNumber, INVWSversion, CLMPayableByMedix2M FROM genisis.WS_MBMInvoice1
          WHERE InvPrintStatus = '0'
            AND (@closeFrom IS NULL OR ClaimCloseDate >= @closeFrom) AND (@closeTo IS NULL OR ClaimCloseDate < DATEADD(DAY, 1, @closeTo))
            AND (@payor IS NULL OR LEFT(CASNumber, 2) = @payor)
            AND (@batchDateFrom IS NULL OR genisis.InvoiceBatchDay(INVBatchDate) >= @batchDateFrom)
            AND (@batchDateTo IS NULL OR genisis.InvoiceBatchDay(INVBatchDate) <= @batchDateTo)
            AND (@batchNoFrom IS NULL OR INVBatchNo >= @batchNoFrom) AND (@batchNoTo IS NULL OR INVBatchNo <= @batchNoTo)
            AND (@bordxFrom IS NULL OR BordxDate >= @bordxFrom) AND (@bordxTo IS NULL OR BordxDate < DATEADD(DAY, 1, @bordxTo))) v
      ON v.CASNumber = i.CASNumber AND v.INVWSversion = i.INVWSversion
    WHERE i.INVPayTo = 'M';
    SELECT @@ROWCOUNT;
END
GO

-- UpdateRecord of FrmInvoiceMBMAC (cmdUpdate_Click, "For Update A/C Period"): the accounting month and year of the member-paid invoices of a worksheet version, and who
-- set them and when. The list the ticks come from has no invoice number (its view has none), and the desktop built the ticked rows' keys from the wrong list columns, so its
-- update could not find them; like the print, the update works on the worksheet version. Returns how many invoices were changed.
CREATE OR ALTER PROCEDURE genisis.InvoiceMemberAccounting_SetPeriod
    @cas nvarchar(30), @version nvarchar(4), @month int, @year int, @user nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.WS_ClaimInvoice
    SET AccPeriodMonth = @month, AccPeriodYear = @year, LastAccPeriodUser = @user, LastAccPeriodDate = CAST(GETDATE() AS date)
    WHERE CASNumber = @cas AND INVWSversion = @version AND INVPayTo = 'M';
    SELECT @@ROWCOUNT;
END
GO

-- ===== main/382_invoice_tracking.sql =====
-- Invoice > Invoice Tracking (legacy FrmInvoiceTracking2). The desktop built the WHERE clause as text (StrAppend) and sent it either to the view VIEW_Invoice_Tracking (the
-- detail listing) or to dbo.InvoiceTracking in HISWorkSheet, which ran it as dynamic SQL (the four summaries); here every criterion is a typed parameter. Read out of the desktop:
--  * The detail listing reads VIEW_Invoice_Tracking as it is, FULL OUTER JOIN and all: it also lists the cases that have no invoice (5,945 of 97,711 rows), which is what the
--    "... is empty" ticks of the form are for.
--  * The four summaries joined worksheets to invoices by case number only, so every invoice was counted with every worksheet version of its case: January 2021 gave an invoice
--    amount of 27,309,937 where the invoices add up to 2,028,764 (and 15,177 "cases" for 2,337 worksheets). They are worked out per worksheet version here: an invoice is counted once,
--    and a worksheet's payable amounts once however many invoices it has. Their criteria also named columns more than one table has (CASNumber, PAYCode), which failed in the desktop.
--  * "Summary (By Hospitals)" asked the procedure for its case 4 and read month columns that case does not return; it gives the layout the form declares for it (hospital, payable
--    to the hospital, payable to the member, invoice amount; the number of worksheets is added).
--  * Stay type "4 - Follow-up & Pre-Hospitalisation" built "AND (INVStayType = 'P' OR INVStayType = 'F'" without its closing bracket and failed; it works here (@stayType = 'PF').
--  * The criteria of the combo boxes the form shows but never read (claimability, insured type, A/C period) are not offered.

-- The invoices and the worksheet each belongs to, with the criteria applied. "From"/"to" pairs are inclusive; an "empty" flag keeps only rows where the column is NULL or blank
-- (a date column is blank at 1900-01-01, as the desktop's  = ''  test saw it).
CREATE OR ALTER FUNCTION genisis.InvoiceTracking_Filtered (
    @invNoFrom nvarchar(100), @invNoTo nvarchar(100), @invNoEmpty bit, @stayType nvarchar(2), @payType char(1),
    @versionFrom int, @versionTo int, @versionEmpty bit, @payTo char(1), @payToEmpty bit,
    @bordxFrom nvarchar(40), @bordxTo nvarchar(40), @bordxEmpty bit,
    @invDateFrom datetime, @invDateTo datetime, @invDateEmpty bit, @regFrom datetime, @regTo datetime, @regEmpty bit,
    @hospital nvarchar(20), @groupCompany nvarchar(300), @groupCompanyEmpty bit,
    @amountFrom money, @amountTo money, @amountEmpty bit, @payor nvarchar(4), @payorEmpty bit,
    @wksFrom nvarchar(30), @wksTo nvarchar(30), @wksEmpty bit,
    @doaFrom datetime, @doaTo datetime, @doaEmpty bit, @dodFrom datetime, @dodTo datetime, @dodEmpty bit,
    @bordxDateFrom datetime, @bordxDateTo datetime, @bordxDateEmpty bit)
RETURNS TABLE
AS RETURN
(
    SELECT v.CASNumber, v.INVWSversion, v.InvNo, v.InvDate, v.INVAmount, v.INVPayTo, v.INVPayee, v.INVStayType, v.INVPAYType, v.INVEnteredDate, v.INVEnteredBy,
           v.INVBordxRefNo, v.BordxDate, v.MBMGroupCompany, v.PAYCode, v.HOSName, v.CASDateAdmitted, v.CASDischargeDate, v.CaseRegDate,
           cl.CLMPayableByMedix2H, cl.CLMPayableByMedix2M, cs.CASHOSCode
    FROM genisis.WS_InvoiceTracking v
    LEFT JOIN genisis.WS_Claim cl ON cl.CASNumber = v.CASNumber AND CAST(cl.ClaimVersion AS nvarchar(10)) = v.INVWSversion
    LEFT JOIN dbo.CAS cs ON cs.CASNumber = v.CASNumber
    WHERE (@invNoFrom IS NULL OR v.InvNo >= @invNoFrom) AND (@invNoTo IS NULL OR v.InvNo <= @invNoTo)
      AND (@invNoEmpty = 0 OR v.InvNo IS NULL OR v.InvNo = '')
      AND (@stayType IS NULL OR (@stayType = 'PF' AND v.INVStayType IN ('P', 'F')) OR v.INVStayType = @stayType)
      AND (@payType IS NULL OR v.INVPAYType = @payType)
      AND (@versionFrom IS NULL OR (v.INVWSversion NOT LIKE '%[^0-9]%' AND v.INVWSversion <> '' AND CAST(v.INVWSversion AS int) >= @versionFrom))
      AND (@versionTo IS NULL OR (v.INVWSversion NOT LIKE '%[^0-9]%' AND v.INVWSversion <> '' AND CAST(v.INVWSversion AS int) <= @versionTo))
      AND (@versionEmpty = 0 OR v.INVWSversion IS NULL OR v.INVWSversion = '')
      AND (@payTo IS NULL OR v.INVPayTo = @payTo) AND (@payToEmpty = 0 OR v.INVPayTo IS NULL OR v.INVPayTo = '')
      AND (@bordxFrom IS NULL OR v.INVBordxRefNo >= @bordxFrom) AND (@bordxTo IS NULL OR v.INVBordxRefNo <= @bordxTo)
      AND (@bordxEmpty = 0 OR v.INVBordxRefNo IS NULL OR v.INVBordxRefNo = '')
      AND (@invDateFrom IS NULL OR v.InvDate >= @invDateFrom) AND (@invDateTo IS NULL OR v.InvDate < DATEADD(DAY, 1, @invDateTo))
      AND (@invDateEmpty = 0 OR v.InvDate IS NULL OR v.InvDate = '19000101')
      AND (@regFrom IS NULL OR v.INVEnteredDate >= @regFrom) AND (@regTo IS NULL OR v.INVEnteredDate < DATEADD(DAY, 1, @regTo))
      AND (@regEmpty = 0 OR v.INVEnteredDate IS NULL OR v.INVEnteredDate = '19000101')
      AND (@hospital IS NULL OR cs.CASHOSCode = @hospital)
      AND (@groupCompany IS NULL OR v.MBMGroupCompany = @groupCompany) AND (@groupCompanyEmpty = 0 OR v.MBMGroupCompany IS NULL OR v.MBMGroupCompany = '')
      AND (@amountFrom IS NULL OR v.INVAmount >= @amountFrom) AND (@amountTo IS NULL OR v.INVAmount <= @amountTo) AND (@amountEmpty = 0 OR v.INVAmount IS NULL)
      AND (@payor IS NULL OR v.PAYCode = @payor) AND (@payorEmpty = 0 OR v.PAYCode IS NULL OR v.PAYCode = '')
      AND (@wksFrom IS NULL OR v.CASNumber >= @wksFrom) AND (@wksTo IS NULL OR v.CASNumber <= @wksTo) AND (@wksEmpty = 0 OR v.CASNumber IS NULL OR v.CASNumber = '')
      AND (@doaFrom IS NULL OR v.CASDateAdmitted >= @doaFrom) AND (@doaTo IS NULL OR v.CASDateAdmitted < DATEADD(DAY, 1, @doaTo))
      AND (@doaEmpty = 0 OR v.CASDateAdmitted IS NULL OR v.CASDateAdmitted = '19000101')
      AND (@dodFrom IS NULL OR v.CASDischargeDate >= @dodFrom) AND (@dodTo IS NULL OR v.CASDischargeDate < DATEADD(DAY, 1, @dodTo))
      AND (@dodEmpty = 0 OR v.CASDischargeDate IS NULL OR v.CASDischargeDate = '19000101')
      AND (@bordxDateFrom IS NULL OR v.BordxDate >= @bordxDateFrom) AND (@bordxDateTo IS NULL OR v.BordxDate < DATEADD(DAY, 1, @bordxDateTo))
      AND (@bordxDateEmpty = 0 OR v.BordxDate IS NULL OR v.BordxDate = '19000101')
)
GO

-- InvTrackListingCLM, report "3 - General Listing(Detail)": one row per invoice (and per case without one).
CREATE OR ALTER PROCEDURE genisis.InvoiceTracking_Detail
    @invNoFrom nvarchar(100) = NULL, @invNoTo nvarchar(100) = NULL, @invNoEmpty bit = 0, @stayType nvarchar(2) = NULL, @payType char(1) = NULL,
    @versionFrom int = NULL, @versionTo int = NULL, @versionEmpty bit = 0, @payTo char(1) = NULL, @payToEmpty bit = 0,
    @bordxFrom nvarchar(40) = NULL, @bordxTo nvarchar(40) = NULL, @bordxEmpty bit = 0,
    @invDateFrom datetime = NULL, @invDateTo datetime = NULL, @invDateEmpty bit = 0, @regFrom datetime = NULL, @regTo datetime = NULL, @regEmpty bit = 0,
    @hospital nvarchar(20) = NULL, @groupCompany nvarchar(300) = NULL, @groupCompanyEmpty bit = 0,
    @amountFrom money = NULL, @amountTo money = NULL, @amountEmpty bit = 0, @payor nvarchar(4) = NULL, @payorEmpty bit = 0,
    @wksFrom nvarchar(30) = NULL, @wksTo nvarchar(30) = NULL, @wksEmpty bit = 0,
    @doaFrom datetime = NULL, @doaTo datetime = NULL, @doaEmpty bit = 0, @dodFrom datetime = NULL, @dodTo datetime = NULL, @dodEmpty bit = 0,
    @bordxDateFrom datetime = NULL, @bordxDateTo datetime = NULL, @bordxDateEmpty bit = 0, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(f.CASNumber) AS CaseNo, RTRIM(f.INVWSversion) AS Version, RTRIM(f.INVBordxRefNo) AS BordxNo, NULLIF(f.BordxDate, '19000101') AS BordxDate,
           RTRIM(f.InvNo) AS InvoiceNo, NULLIF(f.InvDate, '19000101') AS InvoiceDate, f.INVAmount AS InvoiceAmount, NULLIF(f.CaseRegDate, '19000101') AS RegDate,
           NULLIF(f.CASDateAdmitted, '19000101') AS Doa, NULLIF(f.CASDischargeDate, '19000101') AS Dod, RTRIM(f.INVPayee) AS Payee, RTRIM(f.MBMGroupCompany) AS GroupCompany,
           NULLIF(f.INVEnteredDate, '19000101') AS InvoiceRegistered, RTRIM(f.INVEnteredBy) AS EnteredBy, RTRIM(f.INVStayType) AS StayType, RTRIM(f.INVPAYType) AS PayType
    FROM genisis.InvoiceTracking_Filtered(@invNoFrom, @invNoTo, @invNoEmpty, @stayType, @payType, @versionFrom, @versionTo, @versionEmpty, @payTo, @payToEmpty, @bordxFrom, @bordxTo, @bordxEmpty,
        @invDateFrom, @invDateTo, @invDateEmpty, @regFrom, @regTo, @regEmpty, @hospital, @groupCompany, @groupCompanyEmpty, @amountFrom, @amountTo, @amountEmpty, @payor, @payorEmpty,
        @wksFrom, @wksTo, @wksEmpty, @doaFrom, @doaTo, @doaEmpty, @dodFrom, @dodTo, @dodEmpty, @bordxDateFrom, @bordxDateTo, @bordxDateEmpty) f
    ORDER BY f.CASNumber, f.INVWSversion, f.InvNo;
END
GO

-- InvByMthCLM (@report 0), InvByMthHospCLM (1), InvGenListingCLM (2) and InvByHosp (4): the invoices, counted once each and per worksheet version. The columns a report does not
-- have are NULL. 0: by invoice month; 1: by hospital and invoice month; 2: by hospital and invoice; 4: by hospital. Only invoices are counted (not the cases without one).
CREATE OR ALTER PROCEDURE genisis.InvoiceTracking_Summary
    @report int,
    @invNoFrom nvarchar(100) = NULL, @invNoTo nvarchar(100) = NULL, @invNoEmpty bit = 0, @stayType nvarchar(2) = NULL, @payType char(1) = NULL,
    @versionFrom int = NULL, @versionTo int = NULL, @versionEmpty bit = 0, @payTo char(1) = NULL, @payToEmpty bit = 0,
    @bordxFrom nvarchar(40) = NULL, @bordxTo nvarchar(40) = NULL, @bordxEmpty bit = 0,
    @invDateFrom datetime = NULL, @invDateTo datetime = NULL, @invDateEmpty bit = 0, @regFrom datetime = NULL, @regTo datetime = NULL, @regEmpty bit = 0,
    @hospital nvarchar(20) = NULL, @groupCompany nvarchar(300) = NULL, @groupCompanyEmpty bit = 0,
    @amountFrom money = NULL, @amountTo money = NULL, @amountEmpty bit = 0, @payor nvarchar(4) = NULL, @payorEmpty bit = 0,
    @wksFrom nvarchar(30) = NULL, @wksTo nvarchar(30) = NULL, @wksEmpty bit = 0,
    @doaFrom datetime = NULL, @doaTo datetime = NULL, @doaEmpty bit = 0, @dodFrom datetime = NULL, @dodTo datetime = NULL, @dodEmpty bit = 0,
    @bordxDateFrom datetime = NULL, @bordxDateTo datetime = NULL, @bordxDateEmpty bit = 0, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    WITH invoices AS (
        SELECT f.CASNumber, f.INVWSversion, f.InvNo, f.InvDate, f.INVAmount, f.CLMPayableByMedix2H, f.CLMPayableByMedix2M, RTRIM(f.CASHOSCode) AS HospitalCode, RTRIM(f.HOSName) AS Hospital,
               YEAR(f.InvDate) AS InvYear, MONTH(f.InvDate) AS InvMonth,
               CASE WHEN @report = 0 THEN 0 ELSE 1 END AS WithHospital,
               ROW_NUMBER() OVER (PARTITION BY
                   CASE WHEN @report IN (0, 1) THEN YEAR(f.InvDate) END, CASE WHEN @report IN (0, 1) THEN MONTH(f.InvDate) END,
                   CASE WHEN @report <> 0 THEN f.CASHOSCode END, CASE WHEN @report <> 0 THEN f.HOSName END, CASE WHEN @report = 2 THEN f.InvNo END, CASE WHEN @report = 2 THEN f.InvDate END,
                   f.CASNumber, f.INVWSversion ORDER BY f.InvNo) AS FirstOfWorksheet
        FROM genisis.InvoiceTracking_Filtered(@invNoFrom, @invNoTo, @invNoEmpty, @stayType, @payType, @versionFrom, @versionTo, @versionEmpty, @payTo, @payToEmpty, @bordxFrom, @bordxTo, @bordxEmpty,
            @invDateFrom, @invDateTo, @invDateEmpty, @regFrom, @regTo, @regEmpty, @hospital, @groupCompany, @groupCompanyEmpty, @amountFrom, @amountTo, @amountEmpty, @payor, @payorEmpty,
            @wksFrom, @wksTo, @wksEmpty, @doaFrom, @doaTo, @doaEmpty, @dodFrom, @dodTo, @dodEmpty, @bordxDateFrom, @bordxDateTo, @bordxDateEmpty) f
        WHERE f.InvNo IS NOT NULL AND f.CASNumber IS NOT NULL
    )
    SELECT TOP (@max)
           CASE WHEN @report <> 0 THEN HospitalCode END AS HospitalCode, CASE WHEN @report <> 0 THEN Hospital END AS Hospital,
           CASE WHEN @report = 2 THEN RTRIM(InvNo) END AS InvoiceNo, CASE WHEN @report = 2 THEN InvDate END AS InvoiceDate,
           CASE WHEN @report IN (0, 1) THEN InvYear END AS InvYear, CASE WHEN @report IN (0, 1) THEN InvMonth END AS InvMonth,
           COUNT(DISTINCT CASNumber + '-' + INVWSversion) AS TotalCases,
           SUM(CASE WHEN FirstOfWorksheet = 1 THEN CLMPayableByMedix2H END) AS PayableToHospital, SUM(CASE WHEN FirstOfWorksheet = 1 THEN CLMPayableByMedix2M END) AS PayableToMember,
           SUM(INVAmount) AS InvoiceAmount
    FROM invoices
    GROUP BY CASE WHEN @report <> 0 THEN HospitalCode END, CASE WHEN @report <> 0 THEN Hospital END, CASE WHEN @report = 2 THEN RTRIM(InvNo) END, CASE WHEN @report = 2 THEN InvDate END,
             CASE WHEN @report IN (0, 1) THEN InvYear END, CASE WHEN @report IN (0, 1) THEN InvMonth END
    ORDER BY CASE WHEN @report IN (0, 1) THEN InvYear END, CASE WHEN @report IN (0, 1) THEN InvMonth END, CASE WHEN @report <> 0 THEN HospitalCode END, CASE WHEN @report = 2 THEN InvDate END;
END
GO

-- ===== main/390_worksheet_registration_save.sql =====
-- Worksheet > Worksheet Registration, the save (legacy frmWorksheetRegGST2015, cmdApprove_Click). The rows of the version are written by the desktop's own parameterised procedures
-- (reached through the genisis.WS_Insert_WS_* synonyms of 000_setup.sql); the three routines below are the ones the desktop wrote as recordset edits or text.

-- SaveClaim: after the version row of Claim, the case is no longer pending (CAS.CASPending = 'N'). Returns how many cases were changed.
CREATE OR ALTER PROCEDURE genisis.WorksheetSave_ClosePending
    @cas nvarchar(30)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.CAS SET CASPending = 'N' WHERE CASNumber = @cas;
    SELECT @@ROWCOUNT;
END
GO

-- The desktop's Insert_WS_Claim_2012 takes the bank account number as nvarchar(15) although Claim.CLMAccountNo holds 30 characters, so a longer number (16 digits is usual for some
-- banks; 375 of the numbers saved since July 2020 are 16 to 19 long, after being corrected through Adjustment's Update) is cut to 15 characters without a word. The save writes the
-- whole number right after the procedure. Returns how many versions were changed.
CREATE OR ALTER PROCEDURE genisis.WorksheetSave_SetAccountNo
    @cas nvarchar(30), @version int, @accountNo nvarchar(30)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.WS_Claim SET CLMAccountNo = @accountNo WHERE CASNumber = @cas AND ClaimVersion = @version;
    SELECT @@ROWCOUNT;
END
GO

-- UpdateInvoice: the invoices of the case that belong to the version are marked as worksheeted; a void worksheet (status XR) also marks them bordxed under the reference VOID.
-- Returns how many invoices were changed.
CREATE OR ALTER PROCEDURE genisis.WorksheetSave_MarkInvoices
    @cas nvarchar(30), @version int, @void bit
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.WS_ClaimInvoice
    SET INVWSed = 1,
        INVBordxRefNo = CASE WHEN @void = 1 THEN 'VOID' ELSE INVBordxRefNo END,
        INVBordxed = CASE WHEN @void = 1 THEN '1' ELSE INVBordxed END
    WHERE CASNumber = @cas AND INVWSversion = CAST(@version AS nvarchar(4));
    SELECT @@ROWCOUNT;
END
GO

-- UpdateCasReserve: the case's reserve amount is cleared once a worksheet is saved. Returns how many cases were changed.
CREATE OR ALTER PROCEDURE genisis.WorksheetSave_ClearReserve
    @cas nvarchar(30)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.CAS SET CASReserveAmount = 0 WHERE CASNumber = @cas;
    SELECT @@ROWCOUNT;
END
GO

-- savePayMBMStatus: the Y/N flag of each worksheet row of a version (ClaimPayMBMStatus has one row per version). The desktop edited a recordset: a row is added when the version has none, otherwise
-- its columns are set. A column given as NULL (the ones the plan layout does not write) is left as it is. Returns 1 when the row was added and 0 when it was changed.
CREATE OR ALTER PROCEDURE genisis.WorksheetSave_PayMbmStatus
    @cas nvarchar(30), @version smallint,
    @CLMBoardAmt char(1) = NULL,
    @CLMICUAmt char(1) = NULL,
    @CLMServices char(1) = NULL,
    @CLMTheatre char(1) = NULL,
    @CLMMRI char(1) = NULL,
    @CLMAccPostAmt char(1) = NULL,
    @CLMAmbulance char(1) = NULL,
    @CLMOutpatient char(1) = NULL,
    @CLMMonthly char(1) = NULL,
    @CLMOrgan char(1) = NULL,
    @CLMCashDays char(1) = NULL,
    @CLMGuardianAmt char(1) = NULL,
    @CLMMedical char(1) = NULL,
    @CLMPhone char(1) = NULL,
    @CLMAdmin char(1) = NULL,
    @CLMOthers1 char(1) = NULL,
    @CLMOthersNotCoPay char(1) = NULL,
    @CLMBoardTaxAmt char(1) = NULL,
    @CLMICUTaxAmt char(1) = NULL,
    @CLMLodgerTaxAmt char(1) = NULL,
    @CLMPreHSAmt char(1) = NULL,
    @CLMPreHosp char(1) = NULL,
    @CLMSurgicalAmt char(1) = NULL,
    @CLMAna char(1) = NULL,
    @CLMNSPreHos char(1) = NULL,
    @CLMPhysician char(1) = NULL,
    @CLMPost char(1) = NULL,
    @CLMMPreHSAmt char(1) = NULL,
    @CLMCOPreHSSum char(1) = NULL,
    @CLMCOMPreHSSum char(1) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM genisis.WS_ClaimPayMBMStatus WITH (UPDLOCK, HOLDLOCK) WHERE CASNumber = @cas AND CLMVersion = @version)
    BEGIN
        UPDATE genisis.WS_ClaimPayMBMStatus SET
        CLMBoardAmt = COALESCE(@CLMBoardAmt, CLMBoardAmt),
        CLMICUAmt = COALESCE(@CLMICUAmt, CLMICUAmt),
        CLMServices = COALESCE(@CLMServices, CLMServices),
        CLMTheatre = COALESCE(@CLMTheatre, CLMTheatre),
        CLMMRI = COALESCE(@CLMMRI, CLMMRI),
        CLMAccPostAmt = COALESCE(@CLMAccPostAmt, CLMAccPostAmt),
        CLMAmbulance = COALESCE(@CLMAmbulance, CLMAmbulance),
        CLMOutpatient = COALESCE(@CLMOutpatient, CLMOutpatient),
        CLMMonthly = COALESCE(@CLMMonthly, CLMMonthly),
        CLMOrgan = COALESCE(@CLMOrgan, CLMOrgan),
        CLMCashDays = COALESCE(@CLMCashDays, CLMCashDays),
        CLMGuardianAmt = COALESCE(@CLMGuardianAmt, CLMGuardianAmt),
        CLMMedical = COALESCE(@CLMMedical, CLMMedical),
        CLMPhone = COALESCE(@CLMPhone, CLMPhone),
        CLMAdmin = COALESCE(@CLMAdmin, CLMAdmin),
        CLMOthers1 = COALESCE(@CLMOthers1, CLMOthers1),
        CLMOthersNotCoPay = COALESCE(@CLMOthersNotCoPay, CLMOthersNotCoPay),
        CLMBoardTaxAmt = COALESCE(@CLMBoardTaxAmt, CLMBoardTaxAmt),
        CLMICUTaxAmt = COALESCE(@CLMICUTaxAmt, CLMICUTaxAmt),
        CLMLodgerTaxAmt = COALESCE(@CLMLodgerTaxAmt, CLMLodgerTaxAmt),
        CLMPreHSAmt = COALESCE(@CLMPreHSAmt, CLMPreHSAmt),
        CLMPreHosp = COALESCE(@CLMPreHosp, CLMPreHosp),
        CLMSurgicalAmt = COALESCE(@CLMSurgicalAmt, CLMSurgicalAmt),
        CLMAna = COALESCE(@CLMAna, CLMAna),
        CLMNSPreHos = COALESCE(@CLMNSPreHos, CLMNSPreHos),
        CLMPhysician = COALESCE(@CLMPhysician, CLMPhysician),
        CLMPost = COALESCE(@CLMPost, CLMPost),
        CLMMPreHSAmt = COALESCE(@CLMMPreHSAmt, CLMMPreHSAmt),
        CLMCOPreHSSum = COALESCE(@CLMCOPreHSSum, CLMCOPreHSSum),
        CLMCOMPreHSSum = COALESCE(@CLMCOMPreHSSum, CLMCOMPreHSSum)
        WHERE CASNumber = @cas AND CLMVersion = @version;
        SELECT 0;
    END
    ELSE
    BEGIN
        INSERT genisis.WS_ClaimPayMBMStatus (CASNumber, CLMVersion, CLMBoardAmt, CLMICUAmt, CLMServices, CLMTheatre, CLMMRI, CLMAccPostAmt, CLMAmbulance, CLMOutpatient, CLMMonthly, CLMOrgan, CLMCashDays, CLMGuardianAmt, CLMMedical, CLMPhone, CLMAdmin, CLMOthers1, CLMOthersNotCoPay, CLMBoardTaxAmt, CLMICUTaxAmt, CLMLodgerTaxAmt, CLMPreHSAmt, CLMPreHosp, CLMSurgicalAmt, CLMAna, CLMNSPreHos, CLMPhysician, CLMPost, CLMMPreHSAmt, CLMCOPreHSSum, CLMCOMPreHSSum)
        VALUES (@cas, @version, @CLMBoardAmt, @CLMICUAmt, @CLMServices, @CLMTheatre, @CLMMRI, @CLMAccPostAmt, @CLMAmbulance, @CLMOutpatient, @CLMMonthly, @CLMOrgan, @CLMCashDays, @CLMGuardianAmt, @CLMMedical, @CLMPhone, @CLMAdmin, @CLMOthers1, @CLMOthersNotCoPay, @CLMBoardTaxAmt, @CLMICUTaxAmt, @CLMLodgerTaxAmt, @CLMPreHSAmt, @CLMPreHosp, @CLMSurgicalAmt, @CLMAna, @CLMNSPreHos, @CLMPhysician, @CLMPost, @CLMMPreHSAmt, @CLMCOPreHSSum, @CLMCOMPreHSSum);
        SELECT 1;
    END
END
GO

-- GetExcessCode: the next sequence of the case's excess letters (EXC_SEQ: one row per case, 1 the first time) and the letter number E + case + - + sequence. The desktop calls it at every save
-- that leaves the member an excess or a limit exceeded, before it looks whether the case has a letter, so the sequence moves on at each such save (773 of the 805 cases with two or more
-- such versions have a sequence above 1) while the letter keeps the number it was first given.
CREATE OR ALTER PROCEDURE genisis.WorksheetSave_NextExcessNumber
    @cas varchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @seq int;
    IF EXISTS (SELECT 1 FROM genisis.WS_ExcessSequence WITH (UPDLOCK, HOLDLOCK) WHERE CASNumber = @cas)
    BEGIN
        UPDATE genisis.WS_ExcessSequence SET @seq = EXCSequence = EXCSequence + 1 WHERE CASNumber = @cas;
    END
    ELSE
    BEGIN
        INSERT genisis.WS_ExcessSequence (CASNumber, EXCSequence) VALUES (@cas, 1);
        SET @seq = 1;
    END
    SELECT 'E' + RTRIM(@cas) + '-' + CAST(@seq AS varchar(10));
END
GO

-- saveExcessLetter: the case's letter is added when it has none (CAS_Excess holds one row per case). Returns 1 when the letter was added and 0 when the case already had one.
CREATE OR ALTER PROCEDURE genisis.WorksheetSave_ExcessLetter
    @cas varchar(15),
    @number varchar(15),
    @date datetime,
    @invoiceNo varchar(1000) = NULL,
    @invoiceAmount money = NULL,
    @excessAmount money = NULL,
    @issuedBy nvarchar(50) = NULL,
    @EXCBoardAmt nvarchar(15) = NULL,
    @EXCICUAmt nvarchar(15) = NULL,
    @EXCServices nvarchar(15) = NULL,
    @EXCTheatre nvarchar(15) = NULL,
    @EXCPreHSAmt nvarchar(15) = NULL,
    @EXCSurgicalAmt nvarchar(15) = NULL,
    @EXCAna nvarchar(15) = NULL,
    @EXCNSPreHOS nvarchar(15) = NULL,
    @EXCPhysician nvarchar(15) = NULL,
    @EXCPost nvarchar(15) = NULL,
    @EXCEmergency nvarchar(15) = NULL,
    @EXCAmbulance nvarchar(15) = NULL,
    @EXCOutpatient nvarchar(15) = NULL,
    @EXCMonthly nvarchar(15) = NULL,
    @EXCOrgan nvarchar(15) = NULL,
    @EXCTax nvarchar(15) = NULL,
    @EXCGuardianAMT nvarchar(15) = NULL,
    @EXCMedical nvarchar(15) = NULL,
    @EXCPhone nvarchar(15) = NULL,
    @EXCAdmin nvarchar(15) = NULL,
    @EXCOthers1 nvarchar(15) = NULL,
    @EXCBoardTaxAmt nvarchar(15) = NULL,
    @EXCICUTaxAmt nvarchar(15) = NULL,
    @EXCLodgerTaxAmt nvarchar(15) = NULL,
    @EXCTotalExceed nvarchar(15) = NULL,
    @EXCMRI nvarchar(15) = NULL,
    @EXCMPreHSAmt nvarchar(15) = NULL,
    @EXCCoPayment nvarchar(15) = NULL,
    @EXCPreHosp nvarchar(15) = NULL,
    @EXCUndExcess nvarchar(15) = NULL,
    @othersDesc nvarchar(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM genisis.WS_ExcessLetter WITH (UPDLOCK, HOLDLOCK) WHERE CASNumber = @cas)
    BEGIN
        SELECT 0;
        RETURN;
    END
    INSERT genisis.WS_ExcessLetter (EXCNumber, CASNumber, EXCDate, INVNo, INVAmt, ExcessAmt, EXCIssuedBy, EXCIssuedDate, EXCBoardAmt, EXCICUAmt, EXCServices, EXCTheatre, EXCPreHSAmt, EXCSurgicalAmt, EXCAna, EXCNSPreHOS, EXCPhysician, EXCPost, EXCEmergency, EXCAmbulance, EXCOutpatient, EXCMonthly, EXCOrgan, EXCTax, EXCGuardianAMT, EXCMedical, EXCPhone, EXCAdmin, EXCOthers1, EXCBoardTaxAmt, EXCICUTaxAmt, EXCLodgerTaxAmt, EXCTotalExceed, EXCMRI, EXCMPreHSAmt, EXCCoPayment, EXCPreHosp, EXCUndExcess, EXCOthersDesc)
    VALUES (@number, @cas, @date, @invoiceNo, @invoiceAmount, @excessAmount, @issuedBy, @date, @EXCBoardAmt, @EXCICUAmt, @EXCServices, @EXCTheatre, @EXCPreHSAmt, @EXCSurgicalAmt, @EXCAna, @EXCNSPreHOS, @EXCPhysician, @EXCPost, @EXCEmergency, @EXCAmbulance, @EXCOutpatient, @EXCMonthly, @EXCOrgan, @EXCTax, @EXCGuardianAMT, @EXCMedical, @EXCPhone, @EXCAdmin, @EXCOthers1, @EXCBoardTaxAmt, @EXCICUTaxAmt, @EXCLodgerTaxAmt, @EXCTotalExceed, @EXCMRI, @EXCMPreHSAmt, @EXCCoPayment, @EXCPreHosp, @EXCUndExcess, @othersDesc);
    SELECT 1;
END
GO

-- ===== main/400_account_transaction.sql =====
-- Maintenance > Account Transaction (legacy frmMMASchedule, "MMA Schedule of Fees"): a read-only search of
-- HISMaintenance.dbo.VIEW_MMA_Schedule through genisis.Maint_MMASchedule. Every criterion is optional; text criteria are
-- "contains" matches as in the desktop, the part and CPT code exact, the page range compares the page text (page + part).
-- The surgeon's and anaesthetist's categories are matched against the category as listed (without its first character).
CREATE OR ALTER PROCEDURE genisis.AccountTransaction_Search
    @bodyPart nvarchar(4000) = NULL, @opcs nvarchar(4000) = NULL, @procedure nvarchar(4000) = NULL,
    @surgeonCategory nvarchar(4000) = NULL, @anaesthetistCategory nvarchar(4000) = NULL, @part nvarchar(4000) = NULL,
    @pageFrom nvarchar(4000) = NULL, @pageTo nvarchar(4000) = NULL, @cptCode nvarchar(4000) = NULL, @cptDescription nvarchar(4000) = NULL,
    @limit int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@limit) RTRIM(ProcMainBPCode) AS BodyPart, RTRIM(OPCSCode) AS Opcs, RTRIM(OPCSNarrative) AS ProcedureDescription,
        SUBSTRING(RTRIM(ProcSurgCat), 2, 8) AS SurgeonCategory, SUBSTRING(RTRIM(ProcAnaesCat), 2, 8) AS AnaesthetistCategory,
        RTRIM(ProcPart) AS Part, RTRIM(ProcPageNo) AS Page, CAST(SurgeonsFees AS money) AS SurgeonFee, AnaesthetistsFees AS AnaesthetistFee,
        RTRIM(PartACost) AS PartACost, RTRIM(CPTCode) AS CptCode, RTRIM(CPTDesc) AS CptDescription
    FROM genisis.Maint_MMASchedule
    WHERE (@bodyPart IS NULL OR ProcMainBPCode LIKE N'%' + @bodyPart + N'%')
      AND (@opcs IS NULL OR OPCSCode LIKE N'%' + @opcs + N'%')
      AND (@procedure IS NULL OR OPCSNarrative LIKE N'%' + @procedure + N'%')
      AND (@surgeonCategory IS NULL OR SUBSTRING(ProcSurgCat, 2, 8) LIKE N'%' + @surgeonCategory + N'%')
      AND (@anaesthetistCategory IS NULL OR SUBSTRING(ProcAnaesCat, 2, 8) LIKE N'%' + @anaesthetistCategory + N'%')
      AND (@part IS NULL OR ProcPart = @part)
      AND (@pageFrom IS NULL OR ProcPageNo >= @pageFrom)
      AND (@pageTo IS NULL OR ProcPageNo <= @pageTo)
      AND (@cptCode IS NULL OR CPTCode = @cptCode)
      AND (@cptDescription IS NULL OR CPTDesc LIKE N'%' + @cptDescription + N'%')
    ORDER BY ProcMainBPCode, OPCSCode;
END
GO

-- ===== main/410_security.sql =====
-- Maintenance > Security: Department (frmGroupMaintenance, table GRP), Role Management (frmUserMaintenance) and User Permission
-- (frmUserPermissionMaintenance), both on table USR. GRP and USR live in the maintenance database (genisis.Maint_GRP /
-- genisis.Maint_USR); the service providers in the main database (dbo.SRV); deleted users are recorded in the archive database
-- (genisis.Arch_USRArchive) when it exists. Passwords arrive already scrambled by the API (the desktop's Crypt) and are never
-- returned.

-- GRPListing, with the number of users in each department.
CREATE OR ALTER PROCEDURE genisis.Security_Departments
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(g.GRPCode) AS Code, RTRIM(g.GRPDescription) AS Description,
        (SELECT COUNT(*) FROM genisis.Maint_USR u WHERE RTRIM(u.GRPCode) = RTRIM(g.GRPCode)) AS Users
    FROM genisis.Maint_GRP g
    ORDER BY g.GRPCode;
END
GO

-- cmdSave_Click (Add): 0 when the code exists ("Record Duplicated!").
CREATE OR ALTER PROCEDURE genisis.Security_DepartmentInsert
    @code nvarchar(4000), @description nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM genisis.Maint_GRP WITH (UPDLOCK, HOLDLOCK) WHERE GRPCode = @code) BEGIN SELECT 0; RETURN; END
    INSERT INTO genisis.Maint_GRP (GRPCode, GRPDescription) VALUES (@code, @description);
    SELECT 1;
END
GO

-- cmdSave_Click (Edit): the number of rows changed (0 when the department no longer exists).
CREATE OR ALTER PROCEDURE genisis.Security_DepartmentUpdate
    @code nvarchar(4000), @description nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.Maint_GRP SET GRPDescription = @description WHERE GRPCode = @code;
    SELECT @@ROWCOUNT;
END
GO

-- cmdDelete_Click: -1 when users still belong to the department (nothing deleted), otherwise the number of rows deleted.
CREATE OR ALTER PROCEDURE genisis.Security_DepartmentDelete
    @code nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM genisis.Maint_USR WHERE RTRIM(GRPCode) = @code) BEGIN SELECT -1; RETURN; END
    DELETE FROM genisis.Maint_GRP WHERE GRPCode = @code;
    SELECT @@ROWCOUNT;
END
GO

-- USRListing / cmdSearch_Click: the users, optionally those whose code contains @search.
CREATE OR ALTER PROCEDURE genisis.Security_Users
    @search nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(USRCode) AS Code, RTRIM(USRName) AS Name, RTRIM(USRLGCode) AS LgCode, RTRIM(GRPCode) AS Department,
        RTRIM(SRVCode) AS ServiceProvider, RTRIM(USRStatus) AS Status, RTRIM(AuthorisedStatus) AS AuthorisedStatus
    FROM genisis.Maint_USR
    WHERE @search IS NULL OR USRCode LIKE N'%' + @search + N'%'
    ORDER BY USRCode;
END
GO

-- lstUsersList_Click: one user without the password.
CREATE OR ALTER PROCEDURE genisis.Security_User
    @code nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(USRCode) AS Code, RTRIM(USRName) AS Name, RTRIM(USRLGCode) AS LgCode, RTRIM(GRPCode) AS Department,
        RTRIM(SRVCode) AS ServiceProvider, RTRIM(USRStatus) AS Status, RTRIM(AuthorisedStatus) AS AuthorisedStatus,
        USRDateCreated AS DateCreated, RTRIM(USRAccess) AS Access
    FROM genisis.Maint_USR
    WHERE USRCode = @code;
END
GO

-- ComboListing: departments (GRP) and service providers (SRV).
CREATE OR ALTER PROCEDURE genisis.Security_Lookups
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(GRPCode) AS Code, RTRIM(GRPDescription) AS Name FROM genisis.Maint_GRP ORDER BY GRPCode;
    SELECT RTRIM(SRVCode) AS Code, RTRIM(SRVName) AS Name FROM dbo.SRV ORDER BY SRVCode;
END
GO

-- The access string the caller's account has now (to stop someone removing their own Security right).
CREATE OR ALTER PROCEDURE genisis.Security_UserAccess
    @code nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(USRAccess) FROM genisis.Maint_USR WHERE USRCode = @code;
END
GO

-- cmdSave_Click (Add) of both screens: 0 when the code exists. A new user must change the password at the first sign-in
-- (USRLogonCHGStatus = '1', the column's default).
CREATE OR ALTER PROCEDURE genisis.Security_UserInsert
    @code nvarchar(4000), @name nvarchar(4000), @password nvarchar(4000), @department nvarchar(4000), @status nvarchar(4000),
    @dateCreated datetime, @lgCode nvarchar(4000) = NULL, @serviceProvider nvarchar(4000) = NULL,
    @authorisedStatus nvarchar(4000) = NULL, @access nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM genisis.Maint_USR WITH (UPDLOCK, HOLDLOCK) WHERE USRCode = @code) BEGIN SELECT 0; RETURN; END
    INSERT INTO genisis.Maint_USR (USRCode, USRName, USRPassword, GRPCode, USRStatus, USRDateCreated, USRLGCode, SRVCode,
        AuthorisedStatus, USRAccess, USRLogonCHGStatus)
    VALUES (@code, @name, @password, @department, @status, @dateCreated, @lgCode, @serviceProvider, @authorisedStatus, @access, '1');
    SELECT 1;
END
GO

-- Role Management, Edit: name, department, status, date created and, when given, a new password (which must then be changed
-- at the next sign-in). Returns the number of rows changed.
CREATE OR ALTER PROCEDURE genisis.Security_UserUpdateRole
    @code nvarchar(4000), @name nvarchar(4000), @department nvarchar(4000), @status nvarchar(4000), @dateCreated datetime,
    @password nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.Maint_USR
    SET USRName = @name, GRPCode = @department, USRStatus = @status, USRDateCreated = @dateCreated,
        USRPassword = COALESCE(@password, USRPassword), USRLogonCHGStatus = CASE WHEN @password IS NULL THEN USRLogonCHGStatus ELSE '1' END
    WHERE USRCode = @code;
    SELECT @@ROWCOUNT;
END
GO

-- User Permission, Edit: every field of the screen including the access string and, when given, a new password.
CREATE OR ALTER PROCEDURE genisis.Security_UserUpdatePermission
    @code nvarchar(4000), @name nvarchar(4000), @department nvarchar(4000), @status nvarchar(4000), @dateCreated datetime,
    @lgCode nvarchar(4000), @serviceProvider nvarchar(4000), @authorisedStatus nvarchar(4000), @access nvarchar(4000),
    @password nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.Maint_USR
    SET USRName = @name, GRPCode = @department, USRStatus = @status, USRDateCreated = @dateCreated, USRLGCode = @lgCode,
        SRVCode = @serviceProvider, AuthorisedStatus = @authorisedStatus, USRAccess = @access,
        USRPassword = COALESCE(@password, USRPassword), USRLogonCHGStatus = CASE WHEN @password IS NULL THEN USRLogonCHGStatus ELSE '1' END
    WHERE USRCode = @code;
    SELECT @@ROWCOUNT;
END
GO

-- cmdDelete_Click (User Permission): record the user in USRArchive (code, name, date, by whom) when the archive database
-- exists, then delete. USRArchive is keyed on the user code, so a code deleted before (and created again since) updates its
-- archive row instead of failing on the key. Returns the number of users deleted.
CREATE OR ALTER PROCEDURE genisis.Security_UserDelete
    @code nvarchar(4000), @by nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @name nvarchar(120) = (SELECT RTRIM(USRName) FROM genisis.Maint_USR WHERE USRCode = @code);
    IF @name IS NULL AND NOT EXISTS (SELECT 1 FROM genisis.Maint_USR WHERE USRCode = @code) BEGIN SELECT 0; RETURN; END
    IF OBJECT_ID(N'genisis.Arch_USRArchive', N'SN') IS NOT NULL
    BEGIN
        UPDATE genisis.Arch_USRArchive SET USRName = @name, USRLastUpdateDate = CAST(GETDATE() AS date), USRLastUpdateUser = @by
        WHERE USRCode = @code;
        IF @@ROWCOUNT = 0
            INSERT INTO genisis.Arch_USRArchive (USRCode, USRName, USRLastUpdateDate, USRLastUpdateUser)
            VALUES (@code, @name, CAST(GETDATE() AS date), @by);
    END
    DELETE FROM genisis.Maint_USR WHERE USRCode = @code;
    SELECT @@ROWCOUNT;
END
GO

-- ===== main/420_mma_entry.sql =====
-- Maintenance > MMA (legacy frmMMAEntry, "Procedures/OPCS"): the MMA and 13th Schedule rates of up to six OPCS codes
-- (SearchMMARate / SearchMMARateCal / SearchMMADesc on VIEW_MMA_Schedule). @codes is a comma-separated list; a code the
-- schedule does not have is not returned (the desktop showed 0).
CREATE OR ALTER PROCEDURE genisis.MmaEntry_Rates
    @c1 nvarchar(4000) = NULL, @c2 nvarchar(4000) = NULL, @c3 nvarchar(4000) = NULL, @c4 nvarchar(4000) = NULL,
    @c5 nvarchar(4000) = NULL, @c6 nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    WITH wanted AS (SELECT DISTINCT Code FROM (VALUES (@c1), (@c2), (@c3), (@c4), (@c5), (@c6)) v (Code) WHERE Code IS NOT NULL AND Code <> N''),
    ranked AS (
        SELECT RTRIM(s.OPCSCode) AS Code, RTRIM(s.OPCSNarrative) AS MmaDescription, RTRIM(s.ProcedureDescription) AS ScheduleDescription,
            RTRIM(s.ProcMainBPCode) AS MmaBodyPart, RTRIM(s.BodyParts) AS ScheduleBodyPart, RTRIM(s.ProcPageNo) AS MmaPage, RTRIM(s.Page) AS SchedulePage,
            CAST(s.SurgeonsFees AS money) AS MmaSurgeonFee, s.AnaesthetistsFees AS MmaAnaesthetistFee,
            CAST(s.Surgeon13th AS money) AS ScheduleSurgeonMax, CAST(s.Surgeon13thMin AS money) AS ScheduleSurgeonMin,
            CAST(s.Anesthetist13th AS money) AS ScheduleAnaesthetist,
            ROW_NUMBER() OVER (PARTITION BY s.OPCSCode ORDER BY s.ProcMainBPCode) AS n
        FROM genisis.Maint_MMASchedule s
        JOIN wanted w ON w.Code = s.OPCSCode)
    SELECT Code, MmaDescription, ScheduleDescription, MmaBodyPart, ScheduleBodyPart, MmaPage, SchedulePage, MmaSurgeonFee, MmaAnaesthetistFee,
        ScheduleSurgeonMax, ScheduleSurgeonMin, ScheduleAnaesthetist
    FROM ranked WHERE n = 1;
END
GO

PRINT N'Done.';
GO
SET NOEXEC OFF;
GO
