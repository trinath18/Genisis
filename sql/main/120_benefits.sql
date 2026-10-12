-- Maintenance > Benefits (legacy frmMaintenanceBenefits): Category, Sub Category, Benefit Entry and Benefits Listing tabs.
-- dbo.BNF is keyed by sub category (BNFCode) / insured / plan / effective date; the health type is stored but not part of the key.
-- Text filters marked "LIKE" are escaped by the caller.

CREATE OR ALTER PROCEDURE genisis.Benefits_Lookups
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(HLTCode) AS Code, RTRIM(HLTDescription) AS Name FROM dbo.HLT ORDER BY HLTCode;
    SELECT RTRIM(INSCode) AS Code, RTRIM(INSDescription) AS Name FROM dbo.INS ORDER BY INSCode;
    SELECT RTRIM(CATCode) AS Code, RTRIM(CATDescription) AS Name FROM dbo.BNFCategory ORDER BY CATCode;
    SELECT RTRIM(BNFSubCode) AS Code, RTRIM(BNFSUBName) AS Name FROM dbo.BNFSubCat ORDER BY BNFSubCode;
    SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name FROM dbo.PAY WHERE PAYCode IS NOT NULL ORDER BY PAYCode;
    SELECT DISTINCT RTRIM(GRPCompany) AS Name FROM dbo.PLN WHERE NULLIF(RTRIM(GRPCompany), N'') IS NOT NULL ORDER BY Name;
END
GO

-- Category tab. @code is a LIKE prefix, @description a LIKE fragment (desktop SearchCAT).
CREATE OR ALTER PROCEDURE genisis.BenefitCategory_Search
    @code nvarchar(4000) = NULL, @description nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(CATCode) AS Code, RTRIM(CATDescription) AS Description FROM dbo.BNFCategory
    WHERE (@code IS NULL OR CATCode LIKE @code + N'%') AND (@description IS NULL OR CATDescription LIKE N'%' + @description + N'%')
    ORDER BY CATCode
    OPTION (RECOMPILE);
END
GO

-- @update 0 adds, 1 changes the description. Result: 0 saved, 1 record not found, 2 code already exists.
CREATE OR ALTER PROCEDURE genisis.BenefitCategory_Save
    @code nvarchar(4000), @description nvarchar(4000), @update bit, @userCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today datetime = CAST(CAST(GETDATE() AS date) AS datetime);
    DECLARE @exists bit = CASE WHEN EXISTS (SELECT 1 FROM dbo.BNFCategory WITH (UPDLOCK, HOLDLOCK) WHERE CATCode = @code) THEN 1 ELSE 0 END;
    IF @update = 1 AND @exists = 0 BEGIN SELECT 1 AS Result; RETURN; END
    IF @update = 0 AND @exists = 1 BEGIN SELECT 2 AS Result; RETURN; END
    IF @update = 1
        UPDATE dbo.BNFCategory SET CATDescription = @description, CATLastUpdateUser = @userCode, CATLastUpdateDate = @today WHERE CATCode = @code;
    ELSE
        INSERT INTO dbo.BNFCategory (CATCode, CATDescription, CATLastUpdateUser, CATLastUpdateDate) VALUES (@code, @description, @userCode, @today);
    SELECT 0 AS Result;
END
GO

-- Desktop DeleteCAT: refused while a sub category uses the category. Result: 0 deleted, 1 record not found, 2 still in use.
CREATE OR ALTER PROCEDURE genisis.BenefitCategory_Delete
    @code nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.BNFCategory WITH (UPDLOCK, HOLDLOCK) WHERE CATCode = @code)
    BEGIN SELECT 1 AS Result, CAST(NULL AS nvarchar(20)) AS UsedIn; RETURN; END
    IF EXISTS (SELECT 1 FROM dbo.BNFSubCat WHERE BNFCatCode = @code) BEGIN SELECT 2 AS Result, N'Benefit' AS UsedIn; RETURN; END
    DELETE FROM dbo.BNFCategory WHERE CATCode = @code;
    SELECT 0 AS Result, CAST(NULL AS nvarchar(20)) AS UsedIn;
END
GO

-- Sub Category tab (desktop View_BNF_CAT_subcat, which hides 602 Govt. Service Tax). Sub categories whose category no longer
-- exists are listed too (with no category description) so they can be corrected. @code is a LIKE prefix, @name a LIKE fragment.
CREATE OR ALTER PROCEDURE genisis.BenefitSubCategory_Search
    @category nvarchar(4000) = NULL, @code nvarchar(4000) = NULL, @name nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(s.BNFCatCode) AS CategoryCode, RTRIM(c.CATDescription) AS CategoryDescription, RTRIM(s.BNFSubCode) AS Code,
        RTRIM(s.BNFSUBName) AS Name
    FROM dbo.BNFSubCat s LEFT JOIN dbo.BNFCategory c ON c.CATCode = s.BNFCatCode
    WHERE s.BNFSubCode <> N'602' AND (@category IS NULL OR s.BNFCatCode = @category)
      AND (@code IS NULL OR s.BNFSubCode LIKE @code + N'%') AND (@name IS NULL OR s.BNFSUBName LIKE N'%' + @name + N'%')
    ORDER BY s.BNFCatCode, s.BNFSubCode
    OPTION (RECOMPILE);
END
GO

-- @update 0 adds, 1 changes the category and name. Result: 0 saved, 1 not found (Missing: 'Category' or 'Record'), 2 code already exists.
CREATE OR ALTER PROCEDURE genisis.BenefitSubCategory_Save
    @category nvarchar(4000), @code nvarchar(4000), @name nvarchar(4000), @update bit, @userCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today datetime = CAST(CAST(GETDATE() AS date) AS datetime);
    IF NOT EXISTS (SELECT 1 FROM dbo.BNFCategory WHERE CATCode = @category) BEGIN SELECT 1 AS Result, N'Category' AS Missing; RETURN; END
    DECLARE @exists bit = CASE WHEN EXISTS (SELECT 1 FROM dbo.BNFSubCat WITH (UPDLOCK, HOLDLOCK) WHERE BNFSubCode = @code) THEN 1 ELSE 0 END;
    IF @update = 1 AND @exists = 0 BEGIN SELECT 1 AS Result, N'Record' AS Missing; RETURN; END
    IF @update = 0 AND @exists = 1 BEGIN SELECT 2 AS Result, CAST(NULL AS nvarchar(20)) AS Missing; RETURN; END
    IF @update = 1
        UPDATE dbo.BNFSubCat SET BNFCatCode = @category, BNFSUBName = @name, BNFLastUpdateUser = @userCode, BNFLastUpdateDate = @today
        WHERE BNFSubCode = @code;
    ELSE
        INSERT INTO dbo.BNFSubCat (BNFCatCode, BNFSubCode, BNFSUBName, BNFLastUpdateUser, BNFLastUpdateDate)
        VALUES (@category, @code, @name, @userCode, @today);
    SELECT 0 AS Result, CAST(NULL AS nvarchar(20)) AS Missing;
END
GO

-- Desktop Deletesubcat: refused while a benefit row uses the sub category. Result: 0 deleted, 1 record not found, 2 still in use.
CREATE OR ALTER PROCEDURE genisis.BenefitSubCategory_Delete
    @code nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.BNFSubCat WITH (UPDLOCK, HOLDLOCK) WHERE BNFSubCode = @code)
    BEGIN SELECT 1 AS Result, CAST(NULL AS nvarchar(20)) AS UsedIn; RETURN; END
    IF EXISTS (SELECT 1 FROM dbo.BNF WHERE BNFCode = @code) BEGIN SELECT 2 AS Result, N'Benefit' AS UsedIn; RETURN; END
    DELETE FROM dbo.BNFSubCat WHERE BNFSubCode = @code;
    SELECT 0 AS Result, CAST(NULL AS nvarchar(20)) AS UsedIn;
END
GO

-- Benefit Entry tab: every sub category with the values already saved for this plan / insured / health / date (desktop tBNFlisting).
CREATE OR ALTER PROCEDURE genisis.BenefitEntry_Load
    @plan nvarchar(4000), @insured nvarchar(4000), @health nvarchar(4000), @effective datetime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(s.BNFSubCode) AS Code, RTRIM(s.BNFSUBName) AS Name, b.BNFNoOfDays AS Days, RTRIM(b.BNFClaimAbleAmount) AS RatePerDay,
        RTRIM(b.BNFRMPerDis) AS PerDisability
    FROM dbo.BNFSubCat s
    LEFT JOIN dbo.BNF b ON b.BNFCode = s.BNFSubCode AND b.PLNCode = @plan AND b.INSCode = @insured AND b.HLTCode = @health AND b.BNFEffDate = @effective
    ORDER BY s.BNFSubCode;
END
GO

-- Desktop SaveBNF refuses a new set when the plan / insured / date already has any benefit row. Locks the range for the insert that follows.
CREATE OR ALTER PROCEDURE genisis.BenefitEntry_SetExists
    @plan nvarchar(4000), @insured nvarchar(4000), @effective datetime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (
        SELECT 1 FROM dbo.BNF WITH (UPDLOCK, HOLDLOCK) WHERE PLNCode = @plan AND INSCode = @insured AND BNFEffDate = @effective)
        THEN 1 ELSE 0 END AS bit) AS SetExists;
END
GO

-- Adds one benefit row. @version 1 fills BNFEffDateVersion the way the Benefits Listing tab did (rows already on the same
-- sub category / health / insured / plan, plus one); the Benefit Entry tab leaves it empty.
-- Result: 0 saved, 1 a code was not found (Missing names it), 2 the sub category already has a row for this plan / insured / date.
CREATE OR ALTER PROCEDURE genisis.Benefit_Insert
    @subCategory nvarchar(4000), @health nvarchar(4000), @insured nvarchar(4000), @plan nvarchar(4000), @effective datetime,
    @days smallint, @ratePerDay nvarchar(4000), @perDisability nvarchar(4000), @version bit, @userCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @missing nvarchar(20) = CASE
        WHEN NOT EXISTS (SELECT 1 FROM dbo.BNFSubCat WHERE BNFSubCode = @subCategory) THEN N'Sub Category Code'
        WHEN NOT EXISTS (SELECT 1 FROM dbo.HLT WHERE HLTCode = @health) THEN N'Health Type'
        WHEN NOT EXISTS (SELECT 1 FROM dbo.INS WHERE INSCode = @insured) THEN N'Insured Type'
        WHEN NOT EXISTS (SELECT 1 FROM dbo.PLN WHERE PLNCode = @plan) THEN N'Plan Code'
    END;
    IF @missing IS NOT NULL BEGIN SELECT 1 AS Result, @missing AS Missing; RETURN; END
    IF EXISTS (
        SELECT 1 FROM dbo.BNF WITH (UPDLOCK, HOLDLOCK)
        WHERE BNFCode = @subCategory AND INSCode = @insured AND PLNCode = @plan AND BNFEffDate = @effective)
    BEGIN SELECT 2 AS Result, CAST(NULL AS nvarchar(20)) AS Missing; RETURN; END

    INSERT INTO dbo.BNF (BNFCode, HLTCode, INSCode, PLNCode, BNFNoOfDays, BNFClaimAbleAmount, BNFRMPerDis, BNFEffDate, BNFEffDateVersion,
        BNFLastUpdateUser, BNFLastUpdateDate)
    VALUES (@subCategory, @health, @insured, @plan, @days, @ratePerDay, @perDisability, @effective,
        CASE WHEN @version = 1 THEN (SELECT COUNT(*) + 1 FROM dbo.BNF
            WHERE BNFCode = @subCategory AND HLTCode = @health AND INSCode = @insured AND PLNCode = @plan) END,
        @userCode, CAST(CAST(GETDATE() AS date) AS datetime));
    SELECT 0 AS Result, CAST(NULL AS nvarchar(20)) AS Missing;
END
GO

-- Benefits Listing tab (desktop VIEW_BNF_PLN_SubCat). Payor and group company come from the plan; a plan code can have several
-- PLN rows, so filters use EXISTS and the newest plan row is shown. @group is a LIKE prefix.
CREATE OR ALTER PROCEDURE genisis.Benefit_Search
    @top int, @subCategory nvarchar(4000) = NULL, @health nvarchar(4000) = NULL, @insured nvarchar(4000) = NULL,
    @plan nvarchar(4000) = NULL, @effective datetime = NULL, @payor nvarchar(4000) = NULL, @group nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@top) RTRIM(b.BNFCode) AS SubCategoryCode, RTRIM(s.BNFSUBName) AS SubCategoryName, RTRIM(b.HLTCode) AS HealthCode,
        RTRIM(b.INSCode) AS InsuredCode, RTRIM(b.PLNCode) AS PlanCode, b.BNFNoOfDays AS Days, RTRIM(b.BNFClaimAbleAmount) AS RatePerDay,
        RTRIM(b.BNFRMPerDis) AS PerDisability, b.BNFEffDate AS EffectiveDate, RTRIM(p.PAYCode) AS PayorCode, RTRIM(p.GRPCompany) AS GroupCompany
    FROM dbo.BNF b
    LEFT JOIN dbo.BNFSubCat s ON s.BNFSubCode = b.BNFCode
    OUTER APPLY (SELECT TOP 1 PAYCode, GRPCompany FROM dbo.PLN WHERE PLNCode = b.PLNCode ORDER BY PLNIndex DESC) p
    WHERE (@subCategory IS NULL OR b.BNFCode = @subCategory) AND (@health IS NULL OR b.HLTCode = @health)
      AND (@insured IS NULL OR b.INSCode = @insured) AND (@plan IS NULL OR b.PLNCode = @plan)
      AND (@effective IS NULL OR b.BNFEffDate = @effective)
      AND (@payor IS NULL OR EXISTS (SELECT 1 FROM dbo.PLN x WHERE x.PLNCode = b.PLNCode AND x.PAYCode = @payor))
      AND (@group IS NULL OR EXISTS (SELECT 1 FROM dbo.PLN x WHERE x.PLNCode = b.PLNCode AND x.GRPCompany LIKE @group + N'%'))
    ORDER BY b.PLNCode, b.INSCode, b.BNFEffDate DESC, b.BNFCode
    OPTION (RECOMPILE);
END
GO

-- Edits one row's days, RM / day and RM per disability; its key and health type stay fixed. Result: 0 saved, 1 record not found.
CREATE OR ALTER PROCEDURE genisis.Benefit_Update
    @subCategory nvarchar(4000), @insured nvarchar(4000), @plan nvarchar(4000), @effective datetime,
    @days smallint, @ratePerDay nvarchar(4000), @perDisability nvarchar(4000), @userCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.BNF SET BNFNoOfDays = @days, BNFClaimAbleAmount = @ratePerDay, BNFRMPerDis = @perDisability,
        BNFLastUpdateUser = @userCode, BNFLastUpdateDate = CAST(CAST(GETDATE() AS date) AS datetime)
    WHERE BNFCode = @subCategory AND INSCode = @insured AND PLNCode = @plan AND BNFEffDate = @effective;
    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END AS Result;
END
GO

-- Deletes one row. Refused while a member is on the row's plan / health / insured (desktop VIEW_BNF_MBM_CodeChecking).
-- Result: 0 deleted, 1 record not found, 2 still in use.
CREATE OR ALTER PROCEDURE genisis.Benefit_Delete
    @subCategory nvarchar(4000), @insured nvarchar(4000), @plan nvarchar(4000), @effective datetime
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @health char(2);
    SELECT @health = HLTCode FROM dbo.BNF WITH (UPDLOCK, HOLDLOCK)
    WHERE BNFCode = @subCategory AND INSCode = @insured AND PLNCode = @plan AND BNFEffDate = @effective;
    IF @@ROWCOUNT = 0 BEGIN SELECT 1 AS Result; RETURN; END
    IF EXISTS (SELECT 1 FROM dbo.MBM WHERE PLNCode = @plan AND HLTCode = @health AND INSCode = @insured) BEGIN SELECT 2 AS Result; RETURN; END
    DELETE FROM dbo.BNF WHERE BNFCode = @subCategory AND INSCode = @insured AND PLNCode = @plan AND BNFEffDate = @effective;
    SELECT 0 AS Result;
END
GO
