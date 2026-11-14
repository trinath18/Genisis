-- Maintenance > Surgical Schedule (menu mnuSurgicalOperation). The desktop's menu item has no click handler and no form for it is in the source tree (nor in vbcode.zip); the table it names is
-- dbo.PLNSurgOperation, the surgical schedule of a payor's plan that Case Registration, Adjustment and Enquiry list on their "Surgical Schedule" tab (Medix.bas, genisis.CaseReg_SurgicalSchedule).
-- The screen is rebuilt on that table: the rows of one payor and plan, added, changed and deleted. The table has no rows in the development databases.

-- The payors (dbo.PAY).
CREATE OR ALTER PROCEDURE genisis.SurgSchedule_Payors
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(PAYCode) AS Code, RTRIM(ISNULL(PAYCompanyName, N'')) AS Name FROM dbo.PAY WHERE NULLIF(RTRIM(PAYCode), N'') IS NOT NULL ORDER BY PAYCode;
END
GO

-- The plan of a payor (its description), or no row when the payor has no such plan.
CREATE OR ALTER PROCEDURE genisis.SurgSchedule_Plan
    @pay nvarchar(20), @plan nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(PAYCode) AS Payor, RTRIM(PLNCode) AS PlanCode, RTRIM(PLNDescription) AS Description FROM dbo.PLN WHERE PAYCode = @pay AND PLNCode = @plan ORDER BY PLNDescription;
END
GO

-- The surgical schedule of a payor's plan.
CREATE OR ALTER PROCEDURE genisis.SurgSchedule_List
    @pay nvarchar(20), @plan nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT SURIndex AS [Index], RTRIM(SurgMainCat) AS MainCategory, RTRIM(SurgSubCat) AS SubCategory, SurgPerc AS Percentage
    FROM dbo.PLNSurgOperation WHERE PAYCode = @pay AND PLNCode = @plan ORDER BY SurgMainCat, SurgSubCat, SURIndex;
END
GO

-- Adds a row (@index NULL) or changes one. Result: the row's index when saved; -1 the row is not found; -2 the payor has no such plan; -3 the plan already has this main and sub category.
CREATE OR ALTER PROCEDURE genisis.SurgSchedule_Save
    @index int = NULL, @pay nvarchar(20), @plan nvarchar(20), @main varchar(50), @sub varchar(200), @percentage int
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.PLN WHERE PAYCode = @pay AND PLNCode = @plan)
    BEGIN
        SELECT -2 AS Result;
        RETURN;
    END
    IF @index IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.PLNSurgOperation WHERE SURIndex = @index AND PAYCode = @pay AND PLNCode = @plan)
    BEGIN
        SELECT -1 AS Result;
        RETURN;
    END
    IF EXISTS (SELECT 1 FROM dbo.PLNSurgOperation WHERE PAYCode = @pay AND PLNCode = @plan AND SurgMainCat = @main AND ISNULL(SurgSubCat, '') = ISNULL(@sub, '') AND (@index IS NULL OR SURIndex <> @index))
    BEGIN
        SELECT -3 AS Result;
        RETURN;
    END
    IF @index IS NULL
    BEGIN
        INSERT dbo.PLNSurgOperation (PAYCode, PLNCode, SurgMainCat, SurgSubCat, SurgPerc) VALUES (@pay, @plan, @main, @sub, @percentage);
        SELECT CAST(SCOPE_IDENTITY() AS int) AS Result;
        RETURN;
    END
    UPDATE dbo.PLNSurgOperation SET SurgMainCat = @main, SurgSubCat = @sub, SurgPerc = @percentage WHERE SURIndex = @index;
    SELECT @index AS Result;
END
GO

-- Deletes a row of a payor's plan. Result: 0 deleted, 1 not found.
CREATE OR ALTER PROCEDURE genisis.SurgSchedule_Delete
    @index int, @pay nvarchar(20), @plan nvarchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    DELETE dbo.PLNSurgOperation WHERE SURIndex = @index AND PAYCode = @pay AND PLNCode = @plan;
    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END AS Result;
END
GO
