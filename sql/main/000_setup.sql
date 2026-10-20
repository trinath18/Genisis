-- Setup for the Genesis stored procedures. Run on the main database (the one that has dbo.PLN and dbo.MBM).
-- Creates the genisis schema and the synonyms the procedures use to read the maintenance database.
-- If your maintenance, clinical, worksheet or archive database has another name, change @MaintenanceDb / @CisDb / @WorksheetDb /
-- @ArchiveDb below before running.
SET NOEXEC OFF;
SET NOCOUNT ON;
DECLARE @MaintenanceDb sysname = N'HISMaintenance';
DECLARE @CisDb sysname = N'CISDB';
DECLARE @WorksheetDb sysname = N'HISWorkSheet';
DECLARE @ArchiveDb sysname = N'HISDMArchive';

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
ELSE IF DB_ID(@ArchiveDb) IS NULL OR OBJECT_ID(QUOTENAME(@ArchiveDb) + N'.dbo.MBMArchive', N'U') IS NULL
BEGIN
    RAISERROR(N'STOPPED: archive database "%s" was not found on this server (or it has no dbo.MBMArchive). Change @ArchiveDb at the top of this script to your archive database name (the desktop''s mediconnectA connection) and run it again.', 16, 1, @ArchiveDb);
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
    (N'Maint_Renewal', N'Renewal'),
    (N'Maint_CAQ2013', N'CAQ2013'), (N'Maint_SpecApprove', N'SpecApprove'), (N'Maint_REPSelection', N'REPSelection'),
    (N'Maint_Case_RegInsertCASRef', N'Case_RegInsertCASRef'), (N'Maint_Case_RegInsertCASRef1', N'Case_RegInsertCASRef1'),
    (N'Maint_CASLG', N'CASLG'), (N'Maint_LGSeq', N'LGSeq'), (N'Maint_tblRecord', N'tblRecord'),
    (N'Maint_Case_AdjInsertCASRef', N'Case_AdjInsertCASRef'), (N'Maint_Case_AdjUpdateCASRef', N'Case_AdjUpdateCASRef'),
    (N'Maint_Case_AdjInsertCASLG_2011', N'Case_AdjInsertCASLG_2011'), (N'Maint_Case_AdjUpdateCASLG', N'Case_AdjUpdateCASLG'),
    (N'Maint_SearchPendingReason', N'SearchPendingReason')) v (s, t);
SELECT @sql += N'IF OBJECT_ID(N''genisis.' + s + N''', N''SN'') IS NOT NULL DROP SYNONYM genisis.' + s + N';
CREATE SYNONYM genisis.' + s + N' FOR ' + QUOTENAME(@CisDb) + N'.dbo.' + t + N';
'
FROM (VALUES (N'Cis_PLN', N'PLN'), (N'Cis_AnnualLimit', N'AnnualLimit'), (N'Cis_PRM', N'PRM'), (N'Cis_MCO', N'MCO'),
    (N'Cis_VisitLimit', N'VisitLimit'), (N'Cis_CISCas', N'CISCas')) v (s, t);
SELECT @sql += N'IF OBJECT_ID(N''genisis.' + s + N''', N''SN'') IS NOT NULL DROP SYNONYM genisis.' + s + N';
CREATE SYNONYM genisis.' + s + N' FOR ' + QUOTENAME(@ArchiveDb) + N'.dbo.' + t + N';
'
FROM (VALUES (N'Arch_MBM', N'MBMArchive'), (N'Arch_MBMOthers', N'MBMOthersArchive'), (N'Arch_MBMCoveredPersons', N'MBMCoveredPersonsArchive')) v (s, t);
SELECT @sql += N'IF OBJECT_ID(N''genisis.WS_AccountHistory'', N''SN'') IS NOT NULL DROP SYNONYM genisis.WS_AccountHistory;
CREATE SYNONYM genisis.WS_AccountHistory FOR ' + QUOTENAME(@WorksheetDb) + N'.dbo.VIEW_Account_History;
';
EXEC (@sql);
PRINT N'genisis schema and synonyms ready (maintenance database: ' + @MaintenanceDb + N', clinical database: ' + @CisDb
    + N', worksheet database: ' + @WorksheetDb + N', archive database: ' + @ArchiveDb + N').';
GO
