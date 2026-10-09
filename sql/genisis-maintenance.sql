-- GENERATED FILE: built from sql/maintenance/*.sql. Edit those files, then rebuild this one with
--     GENISIS_WRITE_SQL=1 dotnet test --filter Combined_scripts_are_up_to_date
-- How to run: open this file in SSMS, pick the maintenance database in the "Available Databases" box,
-- press F5 (Execute).
-- Safe to run again: it only creates or replaces genisis.* procedures and synonyms; tables and data are not changed.

-- ===== maintenance/000_setup.sql =====
-- Setup for the Genesis stored procedures in the maintenance database (the one that has dbo.USR).
SET NOEXEC OFF;
SET NOCOUNT ON;

DECLARE @major int = ISNULL(CAST(SERVERPROPERTY('ProductMajorVersion') AS int), 0);
DECLARE @build int = ISNULL(CAST(SERVERPROPERTY('ProductBuild') AS int), 0);
DECLARE @version nvarchar(128) = CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128));
IF @major < 13 OR (@major = 13 AND @build < 4001)
BEGIN
    RAISERROR(N'STOPPED: this script needs SQL Server 2016 SP1 or later, but this server is version %s. Run it on a newer SQL Server.', 16, 1, @version);
    SET NOEXEC ON;
END
ELSE IF OBJECT_ID(N'dbo.USR', N'U') IS NULL
BEGIN
    DECLARE @current sysname = DB_NAME();
    RAISERROR(N'STOPPED: database "%s" is not the maintenance database (dbo.USR not found). In SSMS, pick the maintenance database in the "Available Databases" box on the toolbar and run this script again.', 16, 1, @current);
    SET NOEXEC ON;
END

IF SCHEMA_ID(N'genisis') IS NULL EXEC (N'CREATE SCHEMA genisis AUTHORIZATION dbo');
PRINT N'genisis schema ready.';
GO

-- ===== maintenance/010_auth.sql =====
-- Login and password change (legacy frmLogin).

CREATE OR ALTER PROCEDURE genisis.User_GetActive
    @code nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT USRCode, USRName, USRPassword, USRAccess, USRLogonCHGStatus FROM dbo.USR WHERE USRCode = @code AND USRStatus = 'A';
END
GO

-- @pwd is already scrambled by the API (same Crypt as the desktop).
CREATE OR ALTER PROCEDURE genisis.User_ChangePassword
    @code nvarchar(4000), @pwd nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.USR SET USRPassword = @pwd, USRLogonCHGStatus = '0' WHERE USRCode = @code;
END
GO

PRINT N'Done.';
GO
SET NOEXEC OFF;
GO
