-- Claim Bordx > Bor Delivery > Send to Payor (legacy FrmSendtoPayor, "Bordx Delivery"), Send to A/C (legacy FrmSendtoAC, "Bordx Sent to A/C") Returned From Payor (legacy FrmReturnPayor) and Bordx Approval (legacy FrmBordxApproval).
-- The desktop built the WHERE clause as text and ran it through dbo.Bordx_Sent2Payor / dbo.Bordx_Sent2AC (dynamic SQL on a raw string); here every criterion is a typed parameter and the
-- table is read through genisis.WS_Bordx (HISWorkSheet.dbo.Bordx).
-- Returned From Payor listed what dbo.Bordx_ReturnPayor returned: the bordx of dbo.VIEW_BordxReturn_Pay, whose HAVING clause keeps only the bordx already sent to accounts (BordxSendtoACStatus = 1). The
-- view repeats a bordx once per claim state (CLMAppStatus, payor approval...) and shows the date received from the claim rows; here a bordx is one row and shows its own BordxPAYRecDocDate, the column the update sets.

-- SearchRecord + BordxDeliveryListing(2). @kind: 'P' send to payor, 'A' send to accounts, 'R' returned from payor, 'V' bordx approval. @scan = 1: one bordx number; 0: a range. Dates, amounts and numbers are inclusive.
-- @status: '0' not sent (not returned), '1' sent (returned), '2' or NULL both. The A/C period of the payor screens is compared as text, as the desktop did (the columns are varchar: '10' is below '3').
-- Sent / SentOn / SentBy are the status, date and user of the screen's own step (to payor, to A/C or received from the payor); the four dates of the delivery chain are returned for the A/C list.
CREATE OR ALTER PROCEDURE genisis.BordxDelivery_Search
    @kind char(1), @scan bit, @payor nvarchar(4) = NULL, @bordxFrom nvarchar(40) = NULL, @bordxTo nvarchar(40) = NULL,
    @dateFrom datetime = NULL, @dateTo datetime = NULL, @amountFrom money = NULL, @amountTo money = NULL,
    @monthFrom varchar(9) = NULL, @monthTo varchar(9) = NULL, @yearFrom varchar(9) = NULL, @yearTo varchar(9) = NULL,
    @sentFrom datetime = NULL, @sentTo datetime = NULL, @status char(1) = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max)
        RTRIM(BordxPAYCode) AS Payor, RTRIM(BordxRefNo) AS BordxNo, BordxDate, BordxClaimAmt AS Amount, RTRIM(BordxStatus) AS BordxStatus,
        CAST(CASE @kind WHEN 'P' THEN CASE WHEN BordxPAYToStatus = '1' THEN 1 ELSE 0 END
                        WHEN 'R' THEN CASE WHEN BordxPAYDOCFRStatus = '1' THEN 1 ELSE 0 END
                        WHEN 'V' THEN ISNULL(BordxAppStatus, 0)
                        ELSE ISNULL(BordxSendtoACStatus, 0) END AS bit) AS Sent,
        CASE @kind WHEN 'P' THEN BordxPAYSentDate WHEN 'R' THEN BordxPAYRecDocDate WHEN 'V' THEN BordxAppDate ELSE BordxSendACDate END AS SentOn,
        RTRIM(CASE @kind WHEN 'P' THEN BordxPAYSentBy WHEN 'R' THEN BordxPAYRecDocBy WHEN 'V' THEN BordxAppBy ELSE BordxSendACBy END) AS SentBy,
        BordxPAYSentDate AS ToPayorDate, BordxPAYRecDocDate AS FromPayorDate, BordxMNRBSentDate AS ToMnrbDate, BordxMNRBRecDocDate AS FromMnrbDate,
        RTRIM(PAYToACPeriodMth) AS PeriodMonth, RTRIM(PAYToACPeriodYR) AS PeriodYear
    FROM genisis.WS_Bordx
    WHERE (@payor IS NULL OR BordxPAYCode = @payor)
      AND (@scan = 0 OR @bordxFrom IS NULL OR BordxRefNo = @bordxFrom)
      AND (@scan = 1 OR @bordxFrom IS NULL OR BordxRefNo >= @bordxFrom)
      AND (@scan = 1 OR @bordxTo IS NULL OR BordxRefNo <= @bordxTo)
      AND (@dateFrom IS NULL OR BordxDate >= @dateFrom) AND (@dateTo IS NULL OR BordxDate <= @dateTo)
      AND (@amountFrom IS NULL OR BordxClaimAmt >= @amountFrom) AND (@amountTo IS NULL OR BordxClaimAmt <= @amountTo)
      AND (@kind <> 'R' OR ISNULL(BordxSendtoACStatus, 0) = 1)
      AND (@kind IN ('A', 'V') OR ((@monthFrom IS NULL OR PAYToACPeriodMth >= @monthFrom) AND (@monthTo IS NULL OR PAYToACPeriodMth <= @monthTo)
                            AND (@yearFrom IS NULL OR PAYToACPeriodYR >= @yearFrom) AND (@yearTo IS NULL OR PAYToACPeriodYR <= @yearTo)))
      AND (@kind <> 'A' OR ((@sentFrom IS NULL OR BordxSendACDate >= @sentFrom) AND (@sentTo IS NULL OR BordxSendACDate <= @sentTo)))
      AND (@status IS NULL OR @status = '2'
           OR (@kind = 'P' AND BordxPAYToStatus = @status)
           OR (@kind = 'R' AND BordxPAYDOCFRStatus = @status)
           OR (@kind = 'V' AND ISNULL(BordxAppStatus, 0) = CAST(@status AS bit))
           OR (@kind = 'A' AND ISNULL(BordxSendtoACStatus, 0) = CAST(@status AS bit)))
    ORDER BY BordxRefNo;
END
GO

-- UpdateSentPayor / UpdateSentAC / UpdateReturnPayor: marks one bordx as sent on @date by @userCode (to the payor: BordxPAYSentDate, BordxPAYToStatus = '1', BordxPAYSentBy; to accounts: BordxSendACDate,
-- BordxSendtoACStatus = 1, BordxSendACBy; received from the payor: BordxPAYRecDocDate, BordxPAYDOCFRStatus = '1', BordxPAYRecDocBy, and the claims of the bordx that are not approved yet
-- (CLMAppStatus 'N', blank or NULL) become 'Y'; approval: BordxAppDate, BordxAppStatus = 1, BordxAppBy). The desktop only stopped a bordx that was already sent from being ticked; here the procedure
-- refuses it too.
-- Result: 0 done, 1 bordx not found, 2 refused (already sent).
CREATE OR ALTER PROCEDURE genisis.BordxDelivery_Send
    @kind char(1), @bordxNo nvarchar(40), @date datetime, @userCode nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @sent bit;
    SELECT @sent = CASE @kind WHEN 'P' THEN CASE WHEN BordxPAYToStatus = '1' THEN 1 ELSE 0 END
                              WHEN 'R' THEN CASE WHEN BordxPAYDOCFRStatus = '1' THEN 1 ELSE 0 END
                              WHEN 'V' THEN ISNULL(BordxAppStatus, 0)
                              ELSE ISNULL(BordxSendtoACStatus, 0) END
    FROM genisis.WS_Bordx WHERE BordxRefNo = @bordxNo;
    IF @@ROWCOUNT = 0
    BEGIN
        SELECT 1 AS Result;
        RETURN;
    END
    IF @sent = 1
    BEGIN
        SELECT 2 AS Result;
        RETURN;
    END
    IF @kind = 'P'
        UPDATE genisis.WS_Bordx SET BordxPAYSentDate = @date, BordxPAYToStatus = '1', BordxPAYSentBy = @userCode WHERE BordxRefNo = @bordxNo;
    ELSE IF @kind = 'V'
        UPDATE genisis.WS_Bordx SET BordxAppDate = @date, BordxAppStatus = 1, BordxAppBy = @userCode WHERE BordxRefNo = @bordxNo;
    ELSE IF @kind = 'R'
    BEGIN
        UPDATE genisis.WS_Bordx SET BordxPAYRecDocDate = @date, BordxPAYDOCFRStatus = '1', BordxPAYRecDocBy = @userCode WHERE BordxRefNo = @bordxNo;
        UPDATE genisis.WS_Claim SET CLMAppStatus = 'Y' WHERE BordxRefNo = @bordxNo AND (CLMAppStatus = 'N' OR CLMAppStatus = '' OR CLMAppStatus IS NULL);
    END
    ELSE
        UPDATE genisis.WS_Bordx SET BordxSendACDate = @date, BordxSendtoACStatus = 1, BordxSendACBy = @userCode WHERE BordxRefNo = @bordxNo;
    SELECT 0 AS Result;
END
GO
