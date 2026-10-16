-- Maintenance > Entity (legacy frmMMA: Procedures and Look Up Table tabs) and Maintenance > Accounts (legacy frmMMA13thSchDesc).
-- The tables live in the maintenance database: genisis.Maint_MMAProcedures, genisis.Maint_MMAFees, genisis.Maint_MMASchedule (view).
-- Text filters are LIKE fragments escaped by the caller. Only the columns the desktop maintains are written.

CREATE OR ALTER PROCEDURE genisis.MmaProcedure_Search
    @top int, @bodyPart nvarchar(4000) = NULL, @code nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@top) ProcIndex AS [Index], RTRIM(ProcMainBPCode) AS BodyPart, RTRIM(OPCSCode) AS Code, OPCSNarrative AS Narrative,
        RTRIM(ProcSurgCat) AS SurgeonCategory, RTRIM(ProcAnaesCat) AS AnaesthetistCategory, RTRIM(ProcPart) AS Part,
        RTRIM(PartACost) AS PartACost, RTRIM(ProcPageNo) AS PageNo
    FROM genisis.Maint_MMAProcedures
    WHERE (@bodyPart IS NULL OR ProcMainBPCode LIKE N'%' + @bodyPart + N'%') AND (@code IS NULL OR OPCSCode LIKE N'%' + @code + N'%')
    ORDER BY ProcMainBPCode, OPCSCode, ProcIndex
    OPTION (RECOMPILE);
END
GO

-- @index NULL adds a procedure. Result: 0 saved, 1 record not found.
CREATE OR ALTER PROCEDURE genisis.MmaProcedure_Save
    @index int = NULL, @bodyPart nvarchar(4000), @code nvarchar(4000), @narrative nvarchar(4000), @surgeonCategory nvarchar(4000),
    @anaesthetistCategory nvarchar(4000), @pageNo nvarchar(4000), @partACost nvarchar(4000), @part nvarchar(4000), @userCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today datetime = CAST(CAST(GETDATE() AS date) AS datetime);
    IF @index IS NULL
    BEGIN
        INSERT INTO genisis.Maint_MMAProcedures (ProcMainBPCode, OPCSCode, OPCSNarrative, ProcSurgCat, ProcAnaesCat, ProcPageNo, PartACost, ProcPart,
            ProcLastUpdateUser, ProcLastUpdateDate)
        OUTPUT 0 AS Result, inserted.ProcIndex AS [Index]
        VALUES (@bodyPart, @code, @narrative, @surgeonCategory, @anaesthetistCategory, @pageNo, @partACost, @part, @userCode, @today);
        RETURN;
    END
    UPDATE genisis.Maint_MMAProcedures SET ProcMainBPCode = @bodyPart, OPCSCode = @code, OPCSNarrative = @narrative, ProcSurgCat = @surgeonCategory,
        ProcAnaesCat = @anaesthetistCategory, ProcPageNo = @pageNo, PartACost = @partACost, ProcPart = @part,
        ProcLastUpdateUser = @userCode, ProcLastUpdateDate = @today
    WHERE ProcIndex = @index;
    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END AS Result, @index AS [Index];
END
GO

-- Result: 0 deleted, 1 record not found.
CREATE OR ALTER PROCEDURE genisis.MmaProcedure_Delete
    @index int
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM genisis.Maint_MMAProcedures WHERE ProcIndex = @index;
    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END AS Result;
END
GO

CREATE OR ALTER PROCEDURE genisis.MmaFee_Search
    @surgeonCategory nvarchar(4000) = NULL, @anaesthetistCategory nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT FeeIndex AS [Index], RTRIM(SurgeonsCat) AS SurgeonCategory, SurgeonsFees AS SurgeonFee, RTRIM(AnaesthetistsCat) AS AnaesthetistCategory,
        AnaesthetistsFees AS AnaesthetistFee
    FROM genisis.Maint_MMAFees
    WHERE (@surgeonCategory IS NULL OR SurgeonsCat LIKE N'%' + @surgeonCategory + N'%')
      AND (@anaesthetistCategory IS NULL OR AnaesthetistsCat LIKE N'%' + @anaesthetistCategory + N'%')
    ORDER BY FeeIndex
    OPTION (RECOMPILE);
END
GO

-- @index NULL adds a fee row. Result: 0 saved, 1 record not found.
CREATE OR ALTER PROCEDURE genisis.MmaFee_Save
    @index int = NULL, @surgeonCategory nvarchar(4000), @surgeonFee decimal(19, 4), @anaesthetistCategory nvarchar(4000),
    @anaesthetistFee decimal(19, 4), @userCode nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today datetime = CAST(CAST(GETDATE() AS date) AS datetime);
    IF @index IS NULL
    BEGIN
        INSERT INTO genisis.Maint_MMAFees (SurgeonsCat, SurgeonsFees, AnaesthetistsCat, AnaesthetistsFees, FeeLastUpdateUser, FeeLastUpdateDate)
        OUTPUT 0 AS Result, inserted.FeeIndex AS [Index]
        VALUES (@surgeonCategory, @surgeonFee, @anaesthetistCategory, @anaesthetistFee, @userCode, @today);
        RETURN;
    END
    UPDATE genisis.Maint_MMAFees SET SurgeonsCat = @surgeonCategory, SurgeonsFees = @surgeonFee, AnaesthetistsCat = @anaesthetistCategory,
        AnaesthetistsFees = @anaesthetistFee, FeeLastUpdateUser = @userCode, FeeLastUpdateDate = @today
    WHERE FeeIndex = @index;
    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END AS Result, @index AS [Index];
END
GO

-- The desktop deletes without a check; the web refuses while a procedure uses the row's surgeon or anaesthetist category
-- (the fees reach procedures through those categories). Result: 0 deleted, 1 record not found, 2 still in use.
CREATE OR ALTER PROCEDURE genisis.MmaFee_Delete
    @index int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @surgeon nvarchar(10), @anaesthetist nvarchar(50);
    SELECT @surgeon = SurgeonsCat, @anaesthetist = AnaesthetistsCat FROM genisis.Maint_MMAFees WITH (UPDLOCK, HOLDLOCK) WHERE FeeIndex = @index;
    IF @@ROWCOUNT = 0 BEGIN SELECT 1 AS Result; RETURN; END
    IF EXISTS (SELECT 1 FROM genisis.Maint_MMAProcedures WHERE ProcSurgCat = @surgeon OR ProcAnaesCat = @anaesthetist)
    BEGIN SELECT 2 AS Result; RETURN; END
    DELETE FROM genisis.Maint_MMAFees WHERE FeeIndex = @index;
    SELECT 0 AS Result;
END
GO

-- Accounts screen: one OPCS code of VIEW_MMA_Schedule, MMA beside 13th Schedule (desktop cmdNext / cmdPrev).
-- @direction 1 = next code after @code (first when NULL), -1 = previous code before @code (last when NULL), 0 = first code >= @code.
-- No row means there is no such code. The MMA surgeon fee is the MMAFees fee for the procedure's surgeon category (view column [1]);
-- the desktop read the view's SurgeonsFees column, which is the 14th Schedule surgeon fee.
CREATE OR ALTER PROCEDURE genisis.MmaSchedule_Move
    @code nvarchar(4000) = NULL, @direction int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 RTRIM(OPCSCode) AS Code, OPCSNarrative AS MmaDescription, ProcedureDescription AS ScheduleDescription,
        RTRIM(ProcMainBPCode) AS MmaBodyPart, BodyParts AS ScheduleBodyPart, RTRIM(ProcPageNo) AS MmaPage, RTRIM(Page) AS SchedulePage,
        CAST([1] AS decimal(19, 4)) AS MmaSurgeonFee, CAST(AnaesthetistsFees AS decimal(19, 4)) AS MmaAnaesthetistFee,
        CAST(Surgeon13thMin AS decimal(19, 4)) AS ScheduleSurgeonFeeMin, CAST(Surgeon13th AS decimal(19, 4)) AS ScheduleSurgeonFeeMax,
        CAST(Anesthetist13th AS decimal(19, 4)) AS ScheduleAnaesthetistFee
    FROM genisis.Maint_MMASchedule
    WHERE OPCSCode IS NOT NULL
      AND (@code IS NULL
           OR (@direction = 1 AND OPCSCode > @code)
           OR (@direction = -1 AND OPCSCode < @code)
           OR (@direction = 0 AND OPCSCode >= @code))
    ORDER BY CASE WHEN @direction = -1 THEN NULL ELSE OPCSCode END, OPCSCode DESC
    OPTION (RECOMPILE);
END
GO
