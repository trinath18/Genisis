-- Membership > Temp MBM Adj (legacy FrmTempMBMAdj): an older copy of Adjustment that only opens memberships of temporary payors
-- (PAY.tempPayIND = 'Y', checked in FindMBM). The adjustment itself is saved by the Adjustment procedures.
CREATE OR ALTER PROCEDURE genisis.TempAdjustment_Payor
    @number nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(m.MBMNumber) AS MembershipNo, RTRIM(m.PAYCode) AS PayorCode,
        CAST(CASE WHEN RTRIM(ISNULL(p.tempPayIND, '')) = 'Y' THEN 1 ELSE 0 END AS bit) AS Temporary
    FROM dbo.MBM m
    LEFT JOIN dbo.PAY p ON p.PAYCode = m.PAYCode
    WHERE m.MBMNumber = @number;
END
GO
