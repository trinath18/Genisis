-- Setup for the Genesis stored procedures. Run on the main database (the one that has dbo.PLN and dbo.MBM).
-- Creates the genisis schema and the synonyms the procedures use to read the maintenance database.
-- If your maintenance database has another name, change @MaintenanceDb below before running.
SET NOEXEC OFF;
SET NOCOUNT ON;
DECLARE @MaintenanceDb sysname = N'HISMaintenance';
-- The worksheet and payment databases are optional: the screens that read them need them, the synonyms are skipped when absent.
DECLARE @WorkSheetDb sysname = N'HISWorkSheet';
DECLARE @PaymentDb sysname = N'HISPayment';

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
    (N'Maint_CAQ2013', N'CAQ2013'), (N'Maint_CASCrossReference', N'CASCrossReference'), (N'Maint_SpecApprove', N'SpecApprove'),
    (N'Maint_REPSelection', N'REPSelection'), (N'Maint_MMASchedule', N'VIEW_MMA_Schedule'),
    (N'Maint_MMAProcedures', N'MMAProcedures'),
    (N'Maint_Case_RegInsertCASRef', N'Case_RegInsertCASRef'), (N'Maint_Case_RegInsertCASRef1', N'Case_RegInsertCASRef1'),
    (N'Maint_CASLG', N'CASLG'), (N'Maint_LGSeq', N'LGSeq'), (N'Maint_tblRecord', N'tblRecord'),
    (N'Maint_Case_AdjInsertCASRef', N'Case_AdjInsertCASRef'), (N'Maint_Case_AdjUpdateCASRef', N'Case_AdjUpdateCASRef'),
    (N'Maint_Case_AdjInsertCASLG_2011', N'Case_AdjInsertCASLG_2011'), (N'Maint_Case_AdjUpdateCASLG', N'Case_AdjUpdateCASLG'),
    (N'Maint_SearchPendingReason', N'SearchPendingReason'),
    (N'Maint_HosPanelCode', N'HosPanelCode'), (N'Maint_MMAFees', N'MMAFees')) v (s, t);
EXEC (@sql);

SET @sql = N'';
SELECT @sql += N'IF OBJECT_ID(N''genisis.' + s + N''', N''SN'') IS NOT NULL DROP SYNONYM genisis.' + s + N';
CREATE SYNONYM genisis.' + s + N' FOR ' + QUOTENAME(d) + N'.dbo.' + t + N';
'
FROM (VALUES (N'WS_Claim', @WorkSheetDb, N'Claim'), (N'WS_ClaimInvoice', @WorkSheetDb, N'ClaimInvoice'),
    (N'WS_ClaimApprove', @WorkSheetDb, N'ClaimApprove'), (N'WS_Bordx', @WorkSheetDb, N'Bordx'),
    (N'WS_BordxSEQ', @WorkSheetDb, N'Claim_BordxSEQ2013'), (N'WS_CLMAsciiSEQ', @WorkSheetDb, N'CLMAsciiSEQ'),
    (N'WS_PendingBordx', @WorkSheetDb, N'VIEW_WS_PendingBordx2'), (N'WS_PendingBordxIB', @WorkSheetDb, N'VIEW_WS_PendingBordxIB'),
    (N'WS_CLMReport', @WorkSheetDb, N'View_CLMReport5_New'), (N'WS_CLMReportCas', @WorkSheetDb, N'VIEW_CLMReportCasNew'),
    (N'WS_WKSTracking', @WorkSheetDb, N'VIEW_WKSTrackingNew'), (N'WS_WKS2AC', @WorkSheetDb, N'VIEW_WKS2AC'),
    (N'WS_ClaimActual', @WorkSheetDb, N'ClaimActual'), (N'WS_ClaimHos', @WorkSheetDb, N'ClaimHos'), (N'WS_ClaimMember', @WorkSheetDb, N'ClaimMember'),
    (N'WS_ClaimLimit', @WorkSheetDb, N'ClaimLimit'), (N'WS_ClaimOthers', @WorkSheetDb, N'ClaimOthers'), (N'WS_CASInvoice', @WorkSheetDb, N'View_CAS_Invoice'),
    (N'Pay_Payment2HOS', @PaymentDb, N'Payment2HOS')) v (s, d, t)
WHERE DB_ID(d) IS NOT NULL;
EXEC (@sql);
PRINT N'genisis schema and synonyms ready (maintenance database: ' + @MaintenanceDb + N').';
GO
