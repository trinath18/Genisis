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
