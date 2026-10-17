-- Case > Non-Disclosure Info (legacy frmCASNonDisclosure): the pre-existing / underlying illness the member did not
-- disclose, stored on dbo.CASOthersTwo.

-- CasDetails: nothing is returned when the case does not exist; HasRecord tells whether CASOthersTwo has a row yet.
CREATE OR ALTER PROCEDURE genisis.NonDisclosure_Get
    @cas nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(c.CASNumber) AS CaseNumber,
        CAST(CASE WHEN o.CASNumber IS NULL THEN 0 ELSE 1 END AS bit) AS HasRecord,
        RTRIM(o.CASPreIllCAT) AS PreCategory, o.CASPreIllDate AS PreDate,
        RTRIM(o.CASPreIllOthers) AS PreOthers, RTRIM(o.CASPreIllInfBy) AS PreInformedBy,
        RTRIM(o.CASUndIllCAT) AS UndCategory, o.CASUndIllDate AS UndDate,
        RTRIM(o.CASUndIllOthers) AS UndOthers, RTRIM(o.CASUndIllInfBy) AS UndInformedBy
    FROM dbo.CAS c
    LEFT JOIN dbo.CASOthersTwo o ON o.CASNumber = c.CASNumber
    WHERE c.CASNumber = @cas;
END
GO

-- CmdUpdate_Click. The desktop only updated an existing CASOthersTwo row and otherwise reported success without
-- saving; this inserts the row when it is missing. Result: 0 saved, 1 case not found.
CREATE OR ALTER PROCEDURE genisis.NonDisclosure_Save
    @cas nvarchar(15),
    @preCategory nvarchar(4000), @preDate datetime, @preOthers nvarchar(4000), @preInformedBy nvarchar(4000),
    @undCategory nvarchar(4000), @undDate datetime, @undOthers nvarchar(4000), @undInformedBy nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.CAS WHERE CASNumber = @cas)
    BEGIN
        SELECT 1 AS Result;
        RETURN;
    END

    IF EXISTS (SELECT 1 FROM dbo.CASOthersTwo WITH (UPDLOCK, ROWLOCK) WHERE CASNumber = @cas)
        UPDATE dbo.CASOthersTwo
        SET CASPreIllCAT = @preCategory, CASPreIllDate = @preDate, CASPreIllOthers = @preOthers, CASPreIllInfBy = @preInformedBy,
            CASUndIllCAT = @undCategory, CASUndIllDate = @undDate, CASUndIllOthers = @undOthers, CASUndIllInfBy = @undInformedBy
        WHERE CASNumber = @cas;
    ELSE
        INSERT dbo.CASOthersTwo (CASNumber, CASPreIllCAT, CASPreIllDate, CASPreIllOthers, CASPreIllInfBy,
            CASUndIllCAT, CASUndIllDate, CASUndIllOthers, CASUndIllInfBy)
        VALUES (@cas, @preCategory, @preDate, @preOthers, @preInformedBy, @undCategory, @undDate, @undOthers, @undInformedBy);

    SELECT 0 AS Result;
END
GO
