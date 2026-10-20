-- Case > MMA/CPT (legacy frmMMA, "Malaysian Medical Association"): the MMA procedure schedule (MMAProcedures) and the
-- surgeon / anaesthetist fee look-up table (MMAFees), both on the maintenance database. The search patterns are LIKE
-- patterns built (and escaped) by the API; NULL means "not filtered".

-- ProcListing / SearchProc: ProcMainBPCode and OPCSCode are the searchable fields.
CREATE OR ALTER PROCEDURE genisis.Mma_ProcedureSearch
    @mainBodyPart nvarchar(4000) = NULL, @opcsCode nvarchar(4000) = NULL, @max int = 5000
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) ProcIndex AS [Index], RTRIM(ProcMainBPCode) AS MainBodyPart, RTRIM(OPCSCode) AS OpcsCode,
        RTRIM(OPCSNarrative) AS Narrative, RTRIM(ProcSurgCat) AS SurgeonCategory, RTRIM(ProcAnaesCat) AS AnaesthetistCategory,
        RTRIM(ProcPart) AS Part, RTRIM(PartACost) AS PartACost, RTRIM(ProcPageNo) AS PageNo
    FROM genisis.Maint_MMAProcedures
    WHERE (@mainBodyPart IS NULL OR ProcMainBPCode LIKE @mainBodyPart)
      AND (@opcsCode IS NULL OR OPCSCode LIKE @opcsCode)
    ORDER BY ProcIndex;
END
GO

-- SaveProc: @index NULL inserts (returns the new index), otherwise updates that row (Result 1 when it does not exist).
CREATE OR ALTER PROCEDURE genisis.Mma_ProcedureSave
    @index int = NULL, @mainBodyPart nvarchar(4000), @opcsCode nvarchar(4000), @narrative nvarchar(4000),
    @surgeonCategory nvarchar(4000), @anaesthetistCategory nvarchar(4000), @part nvarchar(4000),
    @partACost nvarchar(4000), @pageNo nvarchar(4000), @userCode nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today datetime = CAST(CAST(GETDATE() AS date) AS datetime), @user nvarchar(10) = LEFT(@userCode, 10);
    IF @index IS NULL
    BEGIN
        INSERT genisis.Maint_MMAProcedures (ProcMainBPCode, OPCSCode, OPCSNarrative, ProcSurgCat, ProcAnaesCat, ProcPageNo,
            PartACost, ProcPart, ProcLastUpdateUser, ProcLastUpdateDate)
        OUTPUT 0 AS Result, inserted.ProcIndex AS [Index]
        VALUES (@mainBodyPart, NULLIF(@opcsCode, N''), @narrative, NULLIF(@surgeonCategory, N''), NULLIF(@anaesthetistCategory, N''),
            NULLIF(@pageNo, N''), NULLIF(@partACost, N''), @part, @user, @today);
        RETURN;
    END

    UPDATE genisis.Maint_MMAProcedures
    SET ProcMainBPCode = @mainBodyPart, OPCSCode = NULLIF(@opcsCode, N''), OPCSNarrative = @narrative,
        ProcSurgCat = NULLIF(@surgeonCategory, N''), ProcAnaesCat = NULLIF(@anaesthetistCategory, N''),
        ProcPageNo = NULLIF(@pageNo, N''), PartACost = NULLIF(@partACost, N''), ProcPart = @part,
        ProcLastUpdateUser = @user, ProcLastUpdateDate = @today
    WHERE ProcIndex = @index;
    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END AS Result, @index AS [Index];
END
GO

-- DeleteProc. Result 1 when the row does not exist.
CREATE OR ALTER PROCEDURE genisis.Mma_ProcedureDelete
    @index int
AS
BEGIN
    SET NOCOUNT ON;
    DELETE genisis.Maint_MMAProcedures WHERE ProcIndex = @index;
    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END AS Result;
END
GO

-- LookListing / SearchLook: the surgeon's and anaesthetist's categories are the searchable fields.
CREATE OR ALTER PROCEDURE genisis.Mma_FeeSearch
    @surgeonCategory nvarchar(4000) = NULL, @anaesthetistCategory nvarchar(4000) = NULL, @max int = 5000
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) FeeIndex AS [Index], RTRIM(SurgeonsCat) AS SurgeonCategory, SurgeonsFees AS SurgeonFees,
        RTRIM(AnaesthetistsCat) AS AnaesthetistCategory, AnaesthetistsFees AS AnaesthetistFees
    FROM genisis.Maint_MMAFees
    WHERE (@surgeonCategory IS NULL OR SurgeonsCat LIKE @surgeonCategory)
      AND (@anaesthetistCategory IS NULL OR AnaesthetistsCat LIKE @anaesthetistCategory)
    ORDER BY FeeIndex;
END
GO

-- SaveLook: @index NULL inserts (returns the new index), otherwise updates that row (Result 1 when it does not exist).
CREATE OR ALTER PROCEDURE genisis.Mma_FeeSave
    @index int = NULL, @surgeonCategory nvarchar(4000), @surgeonFees money, @anaesthetistCategory nvarchar(4000),
    @anaesthetistFees money, @userCode nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today datetime = CAST(CAST(GETDATE() AS date) AS datetime), @user nvarchar(10) = LEFT(@userCode, 10);
    IF @index IS NULL
    BEGIN
        INSERT genisis.Maint_MMAFees (SurgeonsCat, SurgeonsFees, AnaesthetistsCat, AnaesthetistsFees, FeeLastUpdateUser, FeeLastUpdateDate)
        OUTPUT 0 AS Result, inserted.FeeIndex AS [Index]
        VALUES (NULLIF(@surgeonCategory, N''), @surgeonFees, NULLIF(@anaesthetistCategory, N''), @anaesthetistFees, @user, @today);
        RETURN;
    END

    UPDATE genisis.Maint_MMAFees
    SET SurgeonsCat = NULLIF(@surgeonCategory, N''), SurgeonsFees = @surgeonFees,
        AnaesthetistsCat = NULLIF(@anaesthetistCategory, N''), AnaesthetistsFees = @anaesthetistFees,
        FeeLastUpdateUser = @user, FeeLastUpdateDate = @today
    WHERE FeeIndex = @index;
    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END AS Result, @index AS [Index];
END
GO

-- DeleteLook. Result 1 when the row does not exist.
CREATE OR ALTER PROCEDURE genisis.Mma_FeeDelete
    @index int
AS
BEGIN
    SET NOCOUNT ON;
    DELETE genisis.Maint_MMAFees WHERE FeeIndex = @index;
    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END AS Result;
END
GO
