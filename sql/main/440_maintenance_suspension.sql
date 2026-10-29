-- Maintenance > Suspension (legacy frmSuspensionDetail, "Suspension Detail"): the suspension categories (dbo.SuspensionCat) and the members and agents that are suspended (dbo.SuspensionDetail).
-- The desktop built its SQL as text and edited through open recordsets; here every criterion is a typed parameter and each save, edit and delete is one statement or one procedure.
-- Codes and descriptions are trimmed and upper-cased, as the desktop's ChkStr did.

-- SearchCAT + CATListing: the categories, or the one whose code starts the way the box does (the desktop compared the first two characters).
CREATE OR ALTER PROCEDURE genisis.SuspensionCat_Search
    @code nvarchar(4) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(SuspensionCode) AS Code, RTRIM(SuspensionDesc) AS Description
    FROM dbo.SuspensionCat
    WHERE @code IS NULL OR SuspensionCode = LEFT(@code, 2)
    ORDER BY SuspensionCode;
END
GO

-- SaveCAT: adds a category, or (edit = 1) changes its description. The desktop's save ran under On Error Resume Next and wrote CATLastUpdateUser / CATLastUpdateDate, columns that do not exist
-- (the table has LastUpdateUser / LastUpdateDate), so it never stamped the row; here the user and the day are stamped.
-- Result: 0 saved, 1 the code already exists (add), 2 no such category (edit).
CREATE OR ALTER PROCEDURE genisis.SuspensionCat_Save
    @code nvarchar(4), @description nvarchar(100), @edit bit, @userCode nvarchar(20), @today datetime
AS
BEGIN
    SET NOCOUNT ON;
    IF @edit = 0
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.SuspensionCat WHERE SuspensionCode = @code)
        BEGIN
            SELECT 1 AS Result;
            RETURN;
        END
        INSERT dbo.SuspensionCat (SuspensionCode, SuspensionDesc, LastUpdateUser, LastUpdateDate) VALUES (@code, @description, @userCode, @today);
    END
    ELSE
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.SuspensionCat WHERE SuspensionCode = @code)
        BEGIN
            SELECT 2 AS Result;
            RETURN;
        END
        UPDATE dbo.SuspensionCat SET SuspensionDesc = @description, LastUpdateUser = @userCode, LastUpdateDate = @today WHERE SuspensionCode = @code;
    END
    SELECT 0 AS Result;
END
GO

-- DeleteCat: a category that a suspension uses cannot be deleted. Result: 0 deleted, 1 no such category, 2 used in Suspension Detail.
CREATE OR ALTER PROCEDURE genisis.SuspensionCat_Delete
    @code nvarchar(4)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.SuspensionCat WHERE SuspensionCode = @code)
    BEGIN
        SELECT 1 AS Result;
        RETURN;
    END
    IF EXISTS (SELECT 1 FROM dbo.SuspensionDetail WHERE SuspensionCode = @code)
    BEGIN
        SELECT 2 AS Result;
        RETURN;
    END
    DELETE dbo.SuspensionCat WHERE SuspensionCode = @code;
    SELECT 0 AS Result;
END
GO

-- SearchDetail + AGTListing: the suspended members (selection 01, MembershipNo) and agents (02, AgentCode), by selection, member number or agent code and payor, with the payor's name.
-- The desktop filtered the key only when a selection was chosen, and read the payor name one row at a time.
CREATE OR ALTER PROCEDURE genisis.SuspensionDetail_Search
    @selection nvarchar(4) = NULL, @key nvarchar(100) = NULL, @payor nvarchar(4) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(d.SuspensionCode) AS Selection,
        CASE WHEN LEN(LTRIM(RTRIM(ISNULL(d.MembershipNo, N'')))) > 0 THEN RTRIM(d.MembershipNo) ELSE RTRIM(d.AgentCode) END AS KeyCode,
        RTRIM(d.Remarks) AS Description, RTRIM(d.PayorCode) AS PayorCode, RTRIM(p.PAYCompanyName) AS PayorName
    FROM dbo.SuspensionDetail d
    LEFT JOIN dbo.PAY p ON p.PAYCode = d.PayorCode
    WHERE (@selection IS NULL OR d.SuspensionCode = @selection)
      AND (@key IS NULL OR @selection IS NULL OR (@selection = N'02' AND d.AgentCode = @key) OR (@selection = N'01' AND d.MembershipNo = @key))
      AND (@payor IS NULL OR d.PayorCode = @payor)
    ORDER BY d.SUSIndex;
END
GO

-- SaveDetail: adds a suspension (the key is the member number for 01 and the agent code for 02), or (edit = 1) changes the description and payor of the one saved. The desktop refused a key
-- that already had a row whatever its selection. Result: 0 saved, 1 the member number / agent code already has a row (add), 2 no such row (edit).
CREATE OR ALTER PROCEDURE genisis.SuspensionDetail_Save
    @selection nvarchar(4), @key nvarchar(100), @remarks nvarchar(200), @payor nvarchar(4), @edit bit, @userCode nvarchar(20), @today datetime
AS
BEGIN
    SET NOCOUNT ON;
    IF @edit = 0
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.SuspensionDetail WHERE (@selection = N'01' AND MembershipNo = @key) OR (@selection <> N'01' AND AgentCode = @key))
        BEGIN
            SELECT 1 AS Result;
            RETURN;
        END
        INSERT dbo.SuspensionDetail (MembershipNo, AgentCode, Remarks, PayorCode, SuspensionCode, LastUpdateUser, LastUpdateDate)
        VALUES (CASE WHEN @selection = N'01' THEN @key END, CASE WHEN @selection <> N'01' THEN @key END, @remarks, @payor, @selection, @userCode, @today);
    END
    ELSE
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.SuspensionDetail WHERE SuspensionCode = @selection AND ((@selection = N'02' AND AgentCode = @key) OR (@selection <> N'02' AND MembershipNo = @key)))
        BEGIN
            SELECT 2 AS Result;
            RETURN;
        END
        UPDATE dbo.SuspensionDetail SET Remarks = @remarks, PayorCode = @payor, LastUpdateUser = @userCode, LastUpdateDate = @today
        WHERE SuspensionCode = @selection AND ((@selection = N'02' AND AgentCode = @key) OR (@selection <> N'02' AND MembershipNo = @key));
    END
    SELECT 0 AS Result;
END
GO

-- DeleteDETAIL: removes the suspension of a member (01) or an agent (02). Result: 0 deleted, 1 no such row.
CREATE OR ALTER PROCEDURE genisis.SuspensionDetail_Delete
    @selection nvarchar(4), @key nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;
    DELETE dbo.SuspensionDetail WHERE (@selection = N'01' AND MembershipNo = @key) OR (@selection <> N'01' AND AgentCode = @key);
    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END AS Result;
END
GO
