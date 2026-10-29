-- Maintenance > Member (legacy frmMaintenanceMembers, "Members Maintenance", tab "Valid Date Range"): the one row of dbo.MBMValidDateRange holds the date ranges a member's
-- effective date, expiry date and date of birth must fall in. The desktop opened the whole table, edited its first row and added a row when the table was empty.

-- LoadDate: the first row. Found = 1 when the table has a row ("Record not found" otherwise).
CREATE OR ALTER PROCEDURE genisis.MemberDates_Get
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) CAST(1 AS bit) AS Found, MBMEffStart AS EffectiveStart, MBMEffEnd AS EffectiveEnd, MBMExpStart AS ExpiryStart, MBMExpEnd AS ExpiryEnd,
        MBMDOBStart AS BirthStart, MBMDOBEnd AS BirthEnd, RTRIM(MBMValidDateLastUpdateUser) AS UpdatedBy, MBMValidDateLastUpdateDate AS UpdatedOn
    FROM dbo.MBMValidDateRange
    ORDER BY ValidID;
END
GO

-- cmdUpdate_Click: updates the first row, or adds the row when the table is empty, and stamps the user and the day. Result: 0 updated, 1 added.
CREATE OR ALTER PROCEDURE genisis.MemberDates_Save
    @effStart datetime, @effEnd datetime, @expStart datetime, @expEnd datetime, @birthStart datetime, @birthEnd datetime, @userCode nvarchar(20), @today datetime
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @id int = (SELECT MIN(ValidID) FROM dbo.MBMValidDateRange);
    IF @id IS NULL
    BEGIN
        INSERT dbo.MBMValidDateRange (MBMEffStart, MBMEffEnd, MBMExpStart, MBMExpEnd, MBMDOBStart, MBMDOBEnd, MBMValidDateLastUpdateUser, MBMValidDateLastUpdateDate)
        VALUES (@effStart, @effEnd, @expStart, @expEnd, @birthStart, @birthEnd, @userCode, @today);
        SELECT 1 AS Result;
        RETURN;
    END
    UPDATE dbo.MBMValidDateRange
    SET MBMEffStart = @effStart, MBMEffEnd = @effEnd, MBMExpStart = @expStart, MBMExpEnd = @expEnd, MBMDOBStart = @birthStart, MBMDOBEnd = @birthEnd,
        MBMValidDateLastUpdateUser = @userCode, MBMValidDateLastUpdateDate = @today
    WHERE ValidID = @id;
    SELECT 0 AS Result;
END
GO
