-- Worksheet > Worksheet Send to A/R (legacy FrmARRecFrCLM, "Date Send to Audit Review"). The desktop form also read CASOthers.CASDTC
-- and copied the invoice number, date and amount into CASOthers.CASINVNo / InvDate / CASInvAmt; those four columns do not exist
-- in the database, so only the three that do (CASDRC, CASRecBy, CASRecStatus) are read and written here.

-- SearchRecord + WksDeliveryListing(2). @scan = 1: one case number; 0: a range. @status: '0' both (status 0, 1 or not set),
-- '1' passed, '2' not yet passed, NULL no filter. The sent dates are inclusive.
CREATE OR ALTER PROCEDURE genisis.WorksheetAr_Search
    @scan bit, @wksFrom nvarchar(30) = NULL, @wksTo nvarchar(30) = NULL,
    @sentFrom datetime = NULL, @sentTo datetime = NULL, @status char(1) = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(CASNumber) AS CaseNo, CASDRC AS DateSent, RTRIM(CASRecBy) AS PassedBy, RTRIM(CASRecStatus) AS Status
    FROM dbo.CASOthers
    WHERE (@scan = 0 OR @wksFrom IS NULL OR CASNumber = @wksFrom)
      AND (@scan = 1 OR @wksFrom IS NULL OR CASNumber >= @wksFrom)
      AND (@scan = 1 OR @wksTo IS NULL OR CASNumber <= @wksTo)
      AND (@sentFrom IS NULL OR CASDRC >= @sentFrom) AND (@sentTo IS NULL OR CASDRC <= @sentTo)
      AND (@status IS NULL
           OR (@status = '0' AND (CASRecStatus IN ('0', '1') OR CASRecStatus IS NULL))
           OR (@status = '1' AND CASRecStatus = '1')
           OR (@status = '2' AND CASRecStatus = '0'))
    ORDER BY CASNumber;
END
GO

-- UpdateSentCLM: marks one case as sent to audit review today and by whom. Result: 0 done, 1 case not found.
CREATE OR ALTER PROCEDURE genisis.WorksheetAr_Send
    @cas nvarchar(30), @userCode nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.CASOthers SET CASDRC = CAST(CAST(GETDATE() AS date) AS datetime), CASRecStatus = '1', CASRecBy = @userCode
    WHERE CASNumber = @cas;
    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END AS Result;
END
GO
