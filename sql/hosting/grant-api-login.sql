-- Least-privilege permissions for the SQL login the hosted API connects with (Railway, or any machine that cannot use Windows authentication).
-- The API only calls stored procedures, so the login needs EXECUTE on:
--   * every procedure of the genisis schema (the API's own procedures: sql/genisis-main.sql, sql/genisis-maintenance.sql)
--   * the older dbo procedures the desktop application owns and the API calls as they are (listed below; found by scanning the API source)
-- Direct reads/writes done inside those procedures and through the cross-database synonyms need db_datareader / db_datawriter on HISDB,
-- HISMaintenance, HISWorkSheet and HISPayment (grant them separately).
--
-- Run as a sysadmin, once per environment. Idempotent. Replace N'apiuser' (one DECLARE per database block) with the name of your login.
-- If the API starts calling another dbo procedure, add it here.

-- ===== HISDB
GO
USE HISDB;
GO
DECLARE @login sysname = N'apiuser', @q sysname, @s nvarchar(max) = N'';
SET @q = QUOTENAME(@login);
IF USER_ID(@login) IS NULL
BEGIN
    SET @s = N'CREATE USER ' + @q + N' FOR LOGIN ' + @q;
    EXEC (@s);
    SET @s = N'';
END
SET @s += N'GRANT EXECUTE ON SCHEMA::genisis TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjInsertCASOthers TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjInsertCASOthers2 TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjInsertCASRemark TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjInsertCASRemark2 TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjInsertCASRemark3 TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjInsertCASRep TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjInsertDIAFinal TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjInsertDIAFirst TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjInsertUD TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjUpdateBalLmt TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjUpdateCAS TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjUpdateCAS1 TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjUpdateCASOthers TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjUpdateCASOthers2 TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjUpdateCASRemark TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjUpdateCASRemark2 TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjUpdateCASRemark3 TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjUpdateCASRep TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjUpdateDIAFirst TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjUpdateHOS TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjUpdateUD TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjupdateDIAFinal TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_RegInsertCAS2013 TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_RegInsertCAS32013 TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_RegInsertCASOthers TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_RegInsertCASOthers2 TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_RegInsertCASRemark TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_RegInsertCASRemark2 TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_RegInsertCASRemark3 TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_RegInsertCASRep TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_RegInsertDIAFinal TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_RegInsertDIAFirst TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_RegInsertUD TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.SearchCas TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.SearchCasAdjustment TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.SearchCasHistory TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.SearchCasHistorySuppmgmt TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.SearchCasHistorymgmt TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.SearchMBMCas TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.SearchMBMCovPersonsProc TO ' + @q + N';';
-- called through genisis synonyms or from inside genisis procedures (cross-database writes)
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Upload_MBM_HIS TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Upload_MBMCovPersonsNew TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Upload_MBMCovPersonsTwo TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Upload_MBMXref_HIS TO ' + @q + N';';
EXEC (@s);
GO

-- ===== HISMaintenance
GO
USE HISMaintenance;
GO
DECLARE @login sysname = N'apiuser', @q sysname, @s nvarchar(max) = N'';
SET @q = QUOTENAME(@login);
IF USER_ID(@login) IS NULL
BEGIN
    SET @s = N'CREATE USER ' + @q + N' FOR LOGIN ' + @q;
    EXEC (@s);
    SET @s = N'';
END
SET @s += N'GRANT EXECUTE ON SCHEMA::genisis TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjInsertCASLG TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Sp_AndroidNotification TO ' + @q + N';';
-- called through genisis synonyms or from inside genisis procedures (cross-database writes)
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjInsertCASLG_2011 TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjInsertCASRef TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjUpdateCASLG TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_AdjUpdateCASRef TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_RegInsertCASRef TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.Case_RegInsertCASRef1 TO ' + @q + N';';
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.SearchPendingReason TO ' + @q + N';';
EXEC (@s);
GO

-- ===== HISWorkSheet
GO
USE HISWorkSheet;
GO
DECLARE @login sysname = N'apiuser', @q sysname, @s nvarchar(max) = N'';
SET @q = QUOTENAME(@login);
IF USER_ID(@login) IS NULL
BEGIN
    SET @s = N'CREATE USER ' + @q + N' FOR LOGIN ' + @q;
    EXEC (@s);
    SET @s = N'';
END
SET @s += N'GRANT EXECUTE ON OBJECT::dbo.CaseEnquiryNotes TO ' + @q + N';';
EXEC (@s);
GO
