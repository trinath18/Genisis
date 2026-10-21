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
