-- Maintenance > Company Info (legacy FrmMaintenanceComInfo, "Company Information"): the one row of dbo.Company with the company's code, name, address, phone, fax, e-mail, homepage and current year.
-- The desktop opened the whole table, edited its first row and added a row when the table was empty. The table is not in the development databases (the desktop's menu item was also disabled),
-- so these procedures say so instead of failing when it is missing; the column types are those the form implies (text for everything but the current year, which is a number).

-- LoadCOMInfo: the first row. TableExists = 0 when dbo.Company is not in this database; Found = 0 when it has no row ("Record not found").
CREATE OR ALTER PROCEDURE genisis.CompanyInfo_Get
AS
BEGIN
    SET NOCOUNT ON;
    IF OBJECT_ID(N'dbo.Company', N'U') IS NULL
    BEGIN
        SELECT CAST(0 AS bit) AS TableExists, CAST(0 AS bit) AS Found;
        RETURN;
    END
    IF NOT EXISTS (SELECT 1 FROM dbo.Company)
    BEGIN
        SELECT CAST(1 AS bit) AS TableExists, CAST(0 AS bit) AS Found;
        RETURN;
    END
    SELECT TOP (1) CAST(1 AS bit) AS TableExists, CAST(1 AS bit) AS Found, RTRIM(COMCode) AS Code, RTRIM(COMName) AS Name, RTRIM(COMAddress) AS Address, RTRIM(COMPhone) AS Phone,
        RTRIM(COMFax) AS Fax, RTRIM(COMEmail) AS Email, RTRIM(COMHomepage) AS Homepage, RTRIM(CAST(COMCurrentYear AS nvarchar(20))) AS CurrentYear
    FROM dbo.Company
    ORDER BY COMCode;
END
GO

-- cmdUpdate_Click: updates the first row, or adds the row when the table is empty, and stamps the user and the day. Result: 0 updated, 1 added, -1 the table is not in this database.
CREATE OR ALTER PROCEDURE genisis.CompanyInfo_Save
    @code nvarchar(50), @name nvarchar(150), @address nvarchar(200), @phone nvarchar(15), @fax nvarchar(15), @email nvarchar(150), @homepage nvarchar(150), @currentYear nvarchar(4),
    @userCode nvarchar(20), @today datetime
AS
BEGIN
    SET NOCOUNT ON;
    IF OBJECT_ID(N'dbo.Company', N'U') IS NULL
    BEGIN
        SELECT -1 AS Result;
        RETURN;
    END
    IF NOT EXISTS (SELECT 1 FROM dbo.Company)
    BEGIN
        INSERT dbo.Company (COMCode, COMName, COMAddress, COMPhone, COMFax, COMEmail, COMHomepage, COMCurrentYear, COMLastUpdateUser, COMLastUpdateDate)
        VALUES (@code, @name, @address, @phone, @fax, @email, @homepage, @currentYear, @userCode, @today);
        SELECT 1 AS Result;
        RETURN;
    END
    ;WITH first_row AS (SELECT TOP (1) * FROM dbo.Company ORDER BY COMCode)
    UPDATE first_row
    SET COMCode = @code, COMName = @name, COMAddress = @address, COMPhone = @phone, COMFax = @fax, COMEmail = @email, COMHomepage = @homepage, COMCurrentYear = @currentYear,
        COMLastUpdateUser = @userCode, COMLastUpdateDate = @today;
    SELECT 0 AS Result;
END
GO
