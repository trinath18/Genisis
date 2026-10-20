-- Worksheet calculation screen: what the page needs besides the engine. The captions of the 29 grid rows (the same list the Enquiry grid uses), the figures the desktop saved for a
-- version (to show beside the calculated ones), and the versions a case has.

CREATE OR ALTER PROCEDURE genisis.WorksheetCalc_Captions
AS
BEGIN
    SET NOCOUNT ON;
    SELECT n.Slot, n.BenefitCode, n.Category, n.Benefit
    FROM (
        SELECT CAST(ROW_NUMBER() OVER (ORDER BY BNFSubCode) AS int) AS Slot, RTRIM(BNFSubCode) AS BenefitCode, RTRIM(CATDescription) AS Category, RTRIM(BNFSubName) AS Benefit
        FROM dbo.view_bnf_cat_subcat WHERE BNFSubCode NOT IN (N'GenSurgBen', N'ine001')
        UNION ALL SELECT 26, N'', N'Others', N'Others - No Co-Pay'
        UNION ALL SELECT 27, N'', N'Service Tax', N'Service Tax - R&B'
        UNION ALL SELECT 28, N'', N'Service Tax', N'Service Tax - ICU'
        UNION ALL SELECT 29, N'', N'Service Tax', N'Service Tax - Lodger'
    ) n
    ORDER BY n.Slot;
END
GO

CREATE OR ALTER PROCEDURE genisis.WorksheetCalc_Saved
    @cas nvarchar(15), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) ISNULL(CLMTotalApproved, 0) AS TotalApproved, ISNULL(CLMLimitExceeded, 0) AS LimitExceeded, ISNULL(CLMCoPayRB, 0) AS CoPayment, CLMPreviousBalance AS PreviousBalance
    FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @version;
END
GO

CREATE OR ALTER PROCEDURE genisis.WorksheetCalc_Versions
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ClaimVersion AS Version FROM genisis.WS_Claim WHERE CASNumber = @cas ORDER BY ClaimVersion;
END
GO
