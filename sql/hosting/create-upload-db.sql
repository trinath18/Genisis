-- Membership > Upload Bordx: the UploadDb staging database. Run once per server, as a login that can create databases (sqlcmd -S <server> -d master -E -i create-upload-db.sql).
--
-- The desktop (FrmNewMBMEndt10) and the API stage the members of an uploaded bordereaux in UploadDb through the TmpUpload_* procedures of HISDB, each run under its own key
-- (the TmpUpdatedBy column), then SaveUploadDate copies the staged rows into HISDB / HISMaintenance and DeleteTmpUploadDate clears them. Between uploads the tables are empty, so
-- only their structure is needed, and a database restored from a backup of HISDB does not contain it (it holds the procedures, not this database).
--
-- Each staging table has the columns of the live table of the same name, with its default values, plus TmpUpdatedBy nvarchar(50). The script is safe to run again: it creates only what is
-- missing. (A structure copy alone is not enough: the staging procedures leave columns out that the live tables fill with a default, for example MBMRefund.MBMMCORefListPrinted.)
SET NOCOUNT ON;

-- same collation as HISDB, so comparisons between the two databases behave
DECLARE @collation sysname = CAST(DATABASEPROPERTYEX(N'HISDB', 'Collation') AS sysname);
IF DB_ID(N'UploadDb') IS NULL
BEGIN
    EXEC (N'CREATE DATABASE UploadDb COLLATE ' + @collation);
    EXEC (N'ALTER DATABASE UploadDb SET RECOVERY SIMPLE');
    PRINT 'UploadDb created';
END

DECLARE @tables TABLE (src sysname, tbl sysname);
INSERT @tables VALUES
    (N'HISDB', N'MBM'), (N'HISDB', N'MBMTwo'), (N'HISDB', N'MBMOthers'), (N'HISDB', N'MBMOthersTwo'), (N'HISDB', N'MBMCoveredPersons'), (N'HISDB', N'MBMCoveredPersonsTwo'),
    (N'HISDB', N'MBMCoveredPersonsCLN'), (N'HISDB', N'MBMCLNOthers'), (N'HISDB', N'MBMAmendHistory'), (N'HISDB', N'MBMRefund'), (N'HISDB', N'MCOFileUpload'),
    (N'HISMaintenance', N'MBMCrossReference');

DECLARE @src sysname, @tbl sysname, @sql nvarchar(max);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT src, tbl FROM @tables;
OPEN c;
FETCH NEXT FROM c INTO @src, @tbl;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(QUOTENAME(@src) + N'.dbo.' + QUOTENAME(@tbl)) IS NULL
        PRINT 'skipped ' + @tbl + ': there is no ' + @src + '.dbo.' + @tbl;
    ELSE IF OBJECT_ID(N'UploadDb.dbo.' + QUOTENAME(@tbl)) IS NULL
    BEGIN
        SET @sql = N'SELECT TOP (0) * INTO UploadDb.dbo.' + QUOTENAME(@tbl) + N' FROM ' + QUOTENAME(@src) + N'.dbo.' + QUOTENAME(@tbl) + N'; '
                 + N'ALTER TABLE UploadDb.dbo.' + QUOTENAME(@tbl) + N' ADD TmpUpdatedBy nvarchar(50) NULL;';
        EXEC (@sql);
        PRINT 'created UploadDb.dbo.' + @tbl;
    END
    FETCH NEXT FROM c INTO @src, @tbl;
END
CLOSE c;
DEALLOCATE c;

-- the default values of the live tables, which a structure copy does not carry
DECLARE @defaults TABLE (tbl sysname, col sysname, def nvarchar(max));
DECLARE d CURSOR LOCAL FAST_FORWARD FOR SELECT src, tbl FROM @tables;
OPEN d;
FETCH NEXT FROM d INTO @src, @tbl;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(QUOTENAME(@src) + N'.dbo.' + QUOTENAME(@tbl)) IS NOT NULL
    BEGIN
        SET @sql = N'SELECT N''' + @tbl + N''', c.name, dc.definition FROM ' + QUOTENAME(@src) + N'.sys.default_constraints dc JOIN ' + QUOTENAME(@src) + N'.sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id WHERE dc.parent_object_id = OBJECT_ID(N''' + QUOTENAME(@src) + N'.dbo.' + QUOTENAME(@tbl) + N''')';
        INSERT @defaults EXEC (@sql);
    END
    FETCH NEXT FROM d INTO @src, @tbl;
END
CLOSE d;
DEALLOCATE d;

DECLARE @col sysname, @def nvarchar(max);
DECLARE e CURSOR LOCAL FAST_FORWARD FOR SELECT tbl, col, def FROM @defaults;
OPEN e;
FETCH NEXT FROM e INTO @tbl, @col, @def;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'UploadDb.dbo.' + QUOTENAME(@tbl)) IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM UploadDb.sys.default_constraints dc JOIN UploadDb.sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
                       WHERE dc.parent_object_id = OBJECT_ID(N'UploadDb.dbo.' + QUOTENAME(@tbl)) AND c.name = @col)
    BEGIN
        SET @sql = N'ALTER TABLE UploadDb.dbo.' + QUOTENAME(@tbl) + N' ADD DEFAULT ' + @def + N' FOR ' + QUOTENAME(@col);
        EXEC (@sql);
        PRINT 'default on ' + @tbl + '.' + @col;
    END
    FETCH NEXT FROM e INTO @tbl, @col, @def;
END
CLOSE e;
DEALLOCATE e;

SELECT t.name AS staging_table, (SELECT COUNT(*) FROM UploadDb.sys.columns c WHERE c.object_id = t.object_id) AS columns_,
       (SELECT COUNT(*) FROM UploadDb.sys.default_constraints dc WHERE dc.parent_object_id = t.object_id) AS defaults_
FROM UploadDb.sys.tables t ORDER BY t.name;
