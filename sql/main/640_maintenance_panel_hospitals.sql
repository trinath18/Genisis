-- Maintenance > Panel Hospital Maintenance (legacy FrmMaintenancePanelHos, "Panel Hospital Maintenance"): which hospitals belong to a hospital panel code (dbo.PanelHospitals: HosPanelCode, HOSCode, LastUpdateUSR,
-- LastUpdateDate). The panel codes and the hospitals of a panel are the ones Case > Panel Hospitals lists (genisis.PanelHospitals_Codes, genisis.PanelHospitals_List). The desktop ran its changes with
-- recordsets and DELETE statements built from the list rows; here one hospital at a time is added or removed with typed parameters, inside one transaction for the whole update.

-- HOSListing / SearchHOS2: the hospitals that are not yet in the panel (what Add lists).
CREATE OR ALTER PROCEDURE genisis.PanelHospitalMaint_Available
    @code nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(h.HOSCode) AS Code, RTRIM(h.HOSName) AS Name, RTRIM(h.HOSTelNo) AS Tel, RTRIM(h.HOSFaxNo) AS Fax
    FROM dbo.HOS h
    WHERE h.HOSCode NOT IN (SELECT p.HOSCode FROM dbo.PanelHospitals p WHERE p.HosPanelCode = @code)
    ORDER BY h.HOSName, h.HOSCode;
END
GO

-- cmdUpdate_Click, Add: puts a hospital in the panel and stamps the user and the day. The desktop opened the row, added it when it was not there and stamped it either way. Result: 0 added, 1 the hospital
-- does not exist, 2 the panel code does not exist, 3 it was in the panel already (stamped again).
CREATE OR ALTER PROCEDURE genisis.PanelHospitalMaint_Add
    @code nvarchar(50), @hospital nvarchar(10), @userCode nvarchar(30), @today datetime
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM genisis.Maint_HosPanelCode WHERE HosPanelCode = @code) BEGIN SELECT 2 AS Result; RETURN; END
    IF NOT EXISTS (SELECT 1 FROM dbo.HOS WHERE HOSCode = @hospital) BEGIN SELECT 1 AS Result; RETURN; END
    IF EXISTS (SELECT 1 FROM dbo.PanelHospitals WHERE HosPanelCode = @code AND HOSCode = @hospital)
    BEGIN
        UPDATE dbo.PanelHospitals SET LastUpdateUSR = @userCode, LastUpdateDate = @today WHERE HosPanelCode = @code AND HOSCode = @hospital;
        SELECT 3 AS Result;
        RETURN;
    END
    INSERT dbo.PanelHospitals (HosPanelCode, HOSCode, LastUpdateUSR, LastUpdateDate) VALUES (@code, @hospital, @userCode, @today);
    SELECT 0 AS Result;
END
GO

-- cmdUpdate_Click, Edit: takes a hospital out of the panel. Returns the number of rows removed.
CREATE OR ALTER PROCEDURE genisis.PanelHospitalMaint_Remove
    @code nvarchar(50), @hospital nvarchar(10)
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.PanelHospitals WHERE HosPanelCode = @code AND HOSCode = @hospital;
    SELECT @@ROWCOUNT AS Removed;
END
GO
