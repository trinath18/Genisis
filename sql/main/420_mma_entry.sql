-- Maintenance > MMA (legacy frmMMAEntry, "Procedures/OPCS"): the MMA and 13th Schedule rates of up to six OPCS codes
-- (SearchMMARate / SearchMMARateCal / SearchMMADesc on VIEW_MMA_Schedule). @codes is a comma-separated list; a code the
-- schedule does not have is not returned (the desktop showed 0).
CREATE OR ALTER PROCEDURE genisis.MmaEntry_Rates
    @c1 nvarchar(4000) = NULL, @c2 nvarchar(4000) = NULL, @c3 nvarchar(4000) = NULL, @c4 nvarchar(4000) = NULL,
    @c5 nvarchar(4000) = NULL, @c6 nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    WITH wanted AS (SELECT DISTINCT Code FROM (VALUES (@c1), (@c2), (@c3), (@c4), (@c5), (@c6)) v (Code) WHERE Code IS NOT NULL AND Code <> N''),
    ranked AS (
        SELECT RTRIM(s.OPCSCode) AS Code, RTRIM(s.OPCSNarrative) AS MmaDescription, RTRIM(s.ProcedureDescription) AS ScheduleDescription,
            RTRIM(s.ProcMainBPCode) AS MmaBodyPart, RTRIM(s.BodyParts) AS ScheduleBodyPart, RTRIM(s.ProcPageNo) AS MmaPage, RTRIM(s.Page) AS SchedulePage,
            CAST(s.SurgeonsFees AS money) AS MmaSurgeonFee, s.AnaesthetistsFees AS MmaAnaesthetistFee,
            CAST(s.Surgeon13th AS money) AS ScheduleSurgeonMax, CAST(s.Surgeon13thMin AS money) AS ScheduleSurgeonMin,
            CAST(s.Anesthetist13th AS money) AS ScheduleAnaesthetist,
            ROW_NUMBER() OVER (PARTITION BY s.OPCSCode ORDER BY s.ProcMainBPCode) AS n
        FROM genisis.Maint_MMASchedule s
        JOIN wanted w ON w.Code = s.OPCSCode)
    SELECT Code, MmaDescription, ScheduleDescription, MmaBodyPart, ScheduleBodyPart, MmaPage, SchedulePage, MmaSurgeonFee, MmaAnaesthetistFee,
        ScheduleSurgeonMax, ScheduleSurgeonMin, ScheduleAnaesthetist
    FROM ranked WHERE n = 1;
END
GO
