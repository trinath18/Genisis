-- Claim Bordx > Monitoring > Outstanding Bordx (legacy FrmOSBordx, "Outstanding Bordx"): the bordereaux that were sent and have not come back, and for how long. Read only. The desktop called
-- OSBordx with a case number (1 to 9) and a WHERE fragment; here there are two procedures with typed parameters over the same view (VIEW_OS_Bordx, one row per bordx):
--   section 'payor'     Bordx with Payor      sent to the payor (BordxPAYToStatus = 1) and the documents not back (BordxPAYDOCFRStatus = 0)         [cases 1, 2, 3]
--   section 'mnrb'      Not Paid by MNRB      sent to MNRB (BordxMNRBToStatus = 1) and no MNRB payment status yet (BordxMNRBFRStatus IS NULL)       [cases 4, 5, 6]
--   section 'nonSihat'  Non-Sihat             sent to the payor and not fully paid (BordxPAYFRStatus is P or NULL)                                  [cases 7, 8, 9]
-- @health is the payors' HLTCode the "All Sihat" (S) or "All Non-Sihat" (N) boxes and the summaries restrict to; a single payor listing has none. @payor NULL is every payor.

-- The listing: one line per bordx with the date it was sent (to the payor, or to MNRB for 'mnrb'). The days outstanding are worked out from the "as at" date by the API.
CREATE OR ALTER PROCEDURE genisis.OutstandingBordx_Detail
    @section varchar(10), @payor nvarchar(4) = NULL, @health char(1) = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(BordxRefNo) AS BordxNo, RTRIM(PAYCode) AS PayorCode, BordxClaimAmt AS Amount,
        CASE WHEN @section = 'mnrb' THEN BordxMNRBSentDate ELSE BordxPAYSentDate END AS SentDate
    FROM genisis.WS_OutstandingBordx
    WHERE (@payor IS NULL OR PAYCode = @payor) AND (@health IS NULL OR HLTCode = @health)
      AND ((@section = 'payor' AND BordxPAYToStatus = 1 AND BordxPAYDOCFRStatus = 0)
        OR (@section = 'mnrb' AND BordxMNRBToStatus = 1 AND BordxMNRBFRStatus IS NULL)
        OR (@section = 'nonSihat' AND BordxPAYToStatus = 1 AND (BordxPAYFRStatus = 'P' OR BordxPAYFRStatus IS NULL)))
    ORDER BY PAYCode, BordxRefNo
    OPTION (RECOMPILE);
END
GO

-- The general summary (OSBordxPAYSummary, OSBordxMNRBSummary, NONOSBordxPAYSummary): the number of claims and the amount outstanding for each payor.
CREATE OR ALTER PROCEDURE genisis.OutstandingBordx_Summary
    @section varchar(10), @payor nvarchar(4) = NULL, @health char(1) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(PAYCode) AS PayorCode, SUM(BordxNoClaims) AS Claims, SUM(BordxClaimAmt) AS Amount
    FROM genisis.WS_OutstandingBordx
    WHERE (@payor IS NULL OR PAYCode = @payor) AND (@health IS NULL OR HLTCode = @health)
      AND ((@section = 'payor' AND BordxPAYToStatus = 1 AND BordxPAYDOCFRStatus = 0)
        OR (@section = 'mnrb' AND BordxMNRBToStatus = 1 AND BordxMNRBFRStatus IS NULL)
        OR (@section = 'nonSihat' AND BordxPAYToStatus = 1 AND (BordxPAYFRStatus = 'P' OR BordxPAYFRStatus IS NULL)))
    GROUP BY PAYCode
    ORDER BY PAYCode
    OPTION (RECOMPILE);
END
GO
