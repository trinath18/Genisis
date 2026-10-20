-- Worksheet calculation engine, step 7: the days of the stay. The invoices of a worksheet version carry the days they charge for (CLMBoardDay, CLMICUDay; fractional days such
-- as 7.5 exist), and the days of the worksheet are their sum: the saved room days (ClaimLimit.CLMBoardDays) equal it for 98.6 per cent of the versions, the ICU days for 99.4 per cent.
CREATE OR ALTER PROCEDURE genisis.WorksheetEngine_StayDays
    @cas nvarchar(15), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ISNULL(SUM(i.CLMBoardDay), 0) AS RoomDays, ISNULL(SUM(i.CLMICUDay), 0) AS IcuDays
    FROM genisis.WS_CASInvoice i
    WHERE i.CASNumber = @cas AND i.INVWSversion = CAST(@version AS nvarchar(10));
END
GO
