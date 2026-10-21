-- Maintenance > Account Transaction (legacy frmMMASchedule, "MMA Schedule of Fees"): a read-only search of
-- HISMaintenance.dbo.VIEW_MMA_Schedule through genisis.Maint_MMASchedule. Every criterion is optional; text criteria are
-- "contains" matches as in the desktop, the part and CPT code exact, the page range compares the page text (page + part).
-- The surgeon's and anaesthetist's categories are matched against the category as listed (without its first character).
CREATE OR ALTER PROCEDURE genisis.AccountTransaction_Search
    @bodyPart nvarchar(4000) = NULL, @opcs nvarchar(4000) = NULL, @procedure nvarchar(4000) = NULL,
    @surgeonCategory nvarchar(4000) = NULL, @anaesthetistCategory nvarchar(4000) = NULL, @part nvarchar(4000) = NULL,
    @pageFrom nvarchar(4000) = NULL, @pageTo nvarchar(4000) = NULL, @cptCode nvarchar(4000) = NULL, @cptDescription nvarchar(4000) = NULL,
    @limit int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@limit) RTRIM(ProcMainBPCode) AS BodyPart, RTRIM(OPCSCode) AS Opcs, RTRIM(OPCSNarrative) AS ProcedureDescription,
        SUBSTRING(RTRIM(ProcSurgCat), 2, 8) AS SurgeonCategory, SUBSTRING(RTRIM(ProcAnaesCat), 2, 8) AS AnaesthetistCategory,
        RTRIM(ProcPart) AS Part, RTRIM(ProcPageNo) AS Page, CAST(SurgeonsFees AS money) AS SurgeonFee, AnaesthetistsFees AS AnaesthetistFee,
        RTRIM(PartACost) AS PartACost, RTRIM(CPTCode) AS CptCode, RTRIM(CPTDesc) AS CptDescription
    FROM genisis.Maint_MMASchedule
    WHERE (@bodyPart IS NULL OR ProcMainBPCode LIKE N'%' + @bodyPart + N'%')
      AND (@opcs IS NULL OR OPCSCode LIKE N'%' + @opcs + N'%')
      AND (@procedure IS NULL OR OPCSNarrative LIKE N'%' + @procedure + N'%')
      AND (@surgeonCategory IS NULL OR SUBSTRING(ProcSurgCat, 2, 8) LIKE N'%' + @surgeonCategory + N'%')
      AND (@anaesthetistCategory IS NULL OR SUBSTRING(ProcAnaesCat, 2, 8) LIKE N'%' + @anaesthetistCategory + N'%')
      AND (@part IS NULL OR ProcPart = @part)
      AND (@pageFrom IS NULL OR ProcPageNo >= @pageFrom)
      AND (@pageTo IS NULL OR ProcPageNo <= @pageTo)
      AND (@cptCode IS NULL OR CPTCode = @cptCode)
      AND (@cptDescription IS NULL OR CPTDesc LIKE N'%' + @cptDescription + N'%')
    ORDER BY ProcMainBPCode, OPCSCode;
END
GO
