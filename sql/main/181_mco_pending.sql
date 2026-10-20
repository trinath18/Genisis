-- Membership > MCO Pending (legacy frmMBMMCOPending). MCOPending lives in the maintenance database (genisis.Maint_MCOPending).
-- Status codes: P pending, R release (the row is removed), B billed (set by Contribution Billing; cannot be changed here).

-- Desktop Listing on VIEW_MCOPending: every cross-referenced membership with its pending row, if any. The bordx date shown is the
-- pending row's, else the membership's; the bordx date filter applies to the membership's date as in the desktop.
-- Rows of MCOPending without a cross-reference are listed under their own number (the desktop fails on the view's empty MBMNumber).
CREATE OR ALTER PROCEDURE genisis.McoPending_Search
    @numberFrom nvarchar(4000) = NULL, @numberTo nvarchar(4000) = NULL, @bordxFrom datetime = NULL, @bordxTo datetime = NULL,
    @status nvarchar(4000) = NULL, @limit int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@limit) RTRIM(COALESCE(x.MBMNumber, p.MBMNumber)) AS MembershipNo, COALESCE(p.MBMBordxDate, x.MBMBordxDate) AS BordxDate,
        p.MBMNewBordxDate AS NewBordxDate, RTRIM(p.Status) AS Status
    FROM genisis.Maint_MBMCrossReference x
    FULL OUTER JOIN genisis.Maint_MCOPending p ON p.MBMNumber = x.MBMNumber
    WHERE (@numberFrom IS NULL OR COALESCE(x.MBMNumber, p.MBMNumber) >= @numberFrom)
      AND (@numberTo IS NULL OR COALESCE(x.MBMNumber, p.MBMNumber) <= @numberTo)
      AND (@bordxFrom IS NULL OR x.MBMBordxDate >= @bordxFrom)
      AND (@bordxTo IS NULL OR x.MBMBordxDate <= @bordxTo)
      AND (@status IS NULL OR p.Status = @status)
    ORDER BY COALESCE(x.MBMNumber, p.MBMNumber);
END
GO

-- One membership for cmdUpdate_Click: Exists says whether MCOPending already has its row.
CREATE OR ALTER PROCEDURE genisis.McoPending_Get
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(COALESCE(x.MBMNumber, p.MBMNumber)) AS MembershipNo, COALESCE(p.MBMBordxDate, x.MBMBordxDate) AS BordxDate,
        RTRIM(p.Status) AS Status, CAST(CASE WHEN p.MBMNumber IS NULL THEN 0 ELSE 1 END AS bit) AS PendingExists
    FROM genisis.Maint_MBMCrossReference x
    FULL OUTER JOIN genisis.Maint_MCOPending p ON p.MBMNumber = x.MBMNumber
    WHERE x.MBMNumber = @number OR p.MBMNumber = @number;
END
GO

CREATE OR ALTER PROCEDURE genisis.McoPending_Insert
    @number nvarchar(4000), @bordxDate datetime, @status nvarchar(4000), @user nvarchar(4000), @today datetime
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO genisis.Maint_MCOPending (MBMNumber, MBMBordxDate, Status, LastUpdateUser, LastUpdateDate)
    VALUES (@number, @bordxDate, @status, @user, @today);
END
GO

CREATE OR ALTER PROCEDURE genisis.McoPending_Delete
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM genisis.Maint_MCOPending WHERE MBMNumber = @number;
END
GO
