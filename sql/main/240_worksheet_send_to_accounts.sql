-- Worksheet > Worksheet Send to A/C (legacy FrmWksSendToAC, "Claims Send To A/C"). The desktop built the WHERE clause as text
-- and ran it on dbo.VIEW_WKS2AC; here every criterion is a typed parameter. Reads genisis.WS_WKS2AC (HISWorkSheet.dbo.VIEW_WKS2AC).

-- SearchRecord + WksDeliveryListing(2). @scan = 1: one case number; 0: a range. @tokenStatus: '0' both (the desktop's
-- "Both" keeps status 0 and 1 only), '1' passed, '2' not yet passed, NULL no filter. Dates and amounts are inclusive.
-- The view repeats a worksheet once per hospital / member payment; the desktop listed (and added up) every repeat and
-- sent a ticked worksheet twice. Here a worksheet is one row, showing its latest cheque dates.
CREATE OR ALTER PROCEDURE genisis.WorksheetAccounts_Search
    @scan bit, @wksFrom nvarchar(30) = NULL, @wksTo nvarchar(30) = NULL, @payor nvarchar(4) = NULL,
    @openFrom datetime = NULL, @openTo datetime = NULL, @amountFrom money = NULL, @amountTo money = NULL,
    @sentFrom datetime = NULL, @sentTo datetime = NULL, @tokenStatus char(1) = NULL,
    @bordxFrom nvarchar(40) = NULL, @bordxTo nvarchar(40) = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(PAYCode) AS Payor, RTRIM(CASNumber) AS CaseNo, ClaimVersion, ClaimOpenDate AS WksDate,
        CLMTotalApproved AS WksAmount, CLMTokenDate AS DateToAc, RTRIM(CLMUserToken) AS PassedBy,
        CAST(ISNULL(CLMTokenStatus, 0) AS bit) AS Sent, MAX(PVHOSChequeDate) AS HosChequeDate, MAX(PVChequeDate) AS MbmChequeDate,
        RTRIM(BordxRefNo) AS BordxNo
    FROM genisis.WS_WKS2AC
    WHERE (@payor IS NULL OR PAYCode = @payor)
      AND (@scan = 0 OR @wksFrom IS NULL OR CASNumber = @wksFrom)
      AND (@scan = 1 OR @wksFrom IS NULL OR CASNumber >= @wksFrom)
      AND (@scan = 1 OR @wksTo IS NULL OR CASNumber <= @wksTo)
      AND (@openFrom IS NULL OR ClaimOpenDate >= @openFrom) AND (@openTo IS NULL OR ClaimOpenDate <= @openTo)
      AND (@amountFrom IS NULL OR CLMTotalApproved >= @amountFrom) AND (@amountTo IS NULL OR CLMTotalApproved <= @amountTo)
      AND (@sentFrom IS NULL OR CLMTokenDate >= @sentFrom) AND (@sentTo IS NULL OR CLMTokenDate <= @sentTo)
      AND (@tokenStatus IS NULL
           OR (@tokenStatus = '0' AND CLMTokenStatus IN (0, 1))
           OR (@tokenStatus = '1' AND CLMTokenStatus = 1)
           OR (@tokenStatus = '2' AND CLMTokenStatus = 0))
      AND (@bordxFrom IS NULL OR BordxRefNo >= @bordxFrom) AND (@bordxTo IS NULL OR BordxRefNo <= @bordxTo)
    GROUP BY PAYCode, CASNumber, ClaimVersion, ClaimOpenDate, CLMTotalApproved, CLMTokenDate, CLMUserToken, CLMTokenStatus, BordxRefNo
    ORDER BY CASNumber, ClaimVersion;
END
GO

-- UpdateSentAC: marks one worksheet as sent to accounts on @date and appends the "SENT TO ACCOUNT" line to the case remarks.
-- Like the desktop (ChkStr), the remark line and the whole remarks page it is added to are trimmed and upper-cased, and the page
-- is chosen from the lengths of the six remark pages exactly as that form did (it never measured the last page, so the
-- first rule only looks at page 5). The desktop only stopped a worksheet already sent from being ticked; here the procedure
-- refuses it too. Result: 0 done, 1 worksheet not found, 2 refused (already sent).
CREATE OR ALTER PROCEDURE genisis.WorksheetAccounts_Send
    @cas nvarchar(30), @version int, @date datetime, @userCode nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @sent bit;
    SELECT @sent = CLMTokenStatus FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @version;
    IF @@ROWCOUNT = 0
    BEGIN
        SELECT 1 AS Result;
        RETURN;
    END
    IF ISNULL(@sent, 0) = 1
    BEGIN
        SELECT 2 AS Result;
        RETURN;
    END

    UPDATE genisis.WS_Claim SET CLMTokenDate = @date, CLMTokenStatus = 1, CLMUserToken = @userCode
    WHERE CASNumber = @cas AND ClaimVersion = @version;

    DECLARE @add nvarchar(max) = UPPER(LTRIM(RTRIM(N'~' + NCHAR(10) + CONVERT(nvarchar(10), GETDATE(), 103) + N'         FILE  -  '
        + RTRIM(@cas) + N'-' + CAST(@version AS nvarchar(10)) + N' SENT TO ACCOUNT ON ' + CONVERT(nvarchar(10), @date, 103)
        + REPLICATE(N' ', 24) + @userCode + N' ')));

    DECLARE @p1 nvarchar(max), @p2 nvarchar(max), @p3 nvarchar(max), @p4 nvarchar(max), @p5 nvarchar(max), @p6 nvarchar(max);
    SELECT @p1 = CASORemarks, @p2 = CASORemarksTwo FROM dbo.CASRemarks WHERE CASNumber = @cas;
    SELECT @p3 = CASORemarks, @p4 = CASORemarksTwo FROM dbo.CASRemarksTwo WHERE CASNumber = @cas;
    SELECT @p5 = CASRemarks, @p6 = CASRemarksFour FROM dbo.CASRemarkThree WHERE CASNumber = @cas;

    -- Len() of a page that holds more than spaces; 0 for a missing or blank page.
    DECLARE @l1 int = CASE WHEN LTRIM(RTRIM(ISNULL(@p1, N''))) <> N'' THEN LEN(@p1 + N'x') - 1 ELSE 0 END;
    DECLARE @l2 int = CASE WHEN LTRIM(RTRIM(ISNULL(@p2, N''))) <> N'' THEN LEN(@p2 + N'x') - 1 ELSE 0 END;
    DECLARE @l3 int = CASE WHEN LTRIM(RTRIM(ISNULL(@p3, N''))) <> N'' THEN LEN(@p3 + N'x') - 1 ELSE 0 END;
    DECLARE @l4 int = CASE WHEN LTRIM(RTRIM(ISNULL(@p4, N''))) <> N'' THEN LEN(@p4 + N'x') - 1 ELSE 0 END;
    DECLARE @l5 int = CASE WHEN LTRIM(RTRIM(ISNULL(@p5, N''))) <> N'' THEN LEN(@p5 + N'x') - 1 ELSE 0 END;

    IF @l5 > 1800
        UPDATE dbo.CASRemarkThree SET CASRemarksFour = UPPER(LTRIM(RTRIM(ISNULL(@p6, N'') + @add))) WHERE CASNumber = @cas;
    ELSE IF (@l5 <> 0 AND @l5 < 1800) OR @l4 > 1800
        UPDATE dbo.CASRemarkThree SET CASRemarks = UPPER(LTRIM(RTRIM(ISNULL(@p5, N'') + @add))) WHERE CASNumber = @cas;
    ELSE IF (@l4 <> 0 AND @l4 < 1800) OR @l3 > 1800
        UPDATE dbo.CASRemarksTwo SET CASORemarksTwo = UPPER(LTRIM(RTRIM(ISNULL(@p4, N'') + @add))) WHERE CASNumber = @cas;
    ELSE IF (@l3 <> 0 AND @l3 < 1800) OR @l2 > 1800
        UPDATE dbo.CASRemarksTwo SET CASORemarks = UPPER(LTRIM(RTRIM(ISNULL(@p3, N'') + @add))) WHERE CASNumber = @cas;
    ELSE IF (@l2 <> 0 AND @l2 < 1800) OR @l1 > 1800
        UPDATE dbo.CASRemarks SET CASORemarksTwo = UPPER(LTRIM(RTRIM(ISNULL(@p2, N'') + @add))) WHERE CASNumber = @cas;
    ELSE IF @l1 <> 0 AND @l1 < 1800
        UPDATE dbo.CASRemarks SET CASORemarks = UPPER(LTRIM(RTRIM(ISNULL(@p1, N'') + @add))) WHERE CASNumber = @cas;
    ELSE IF @l1 = 0
        UPDATE dbo.CASRemarks SET CASORemarks = UPPER(LTRIM(RTRIM(ISNULL(@p1, N'') + @add))) WHERE CASNumber = @cas;

    SELECT 0 AS Result;
END
GO
