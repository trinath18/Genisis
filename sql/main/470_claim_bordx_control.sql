-- Claim Bordx > Monitoring > Bordx Control (legacy FrmBordxControl, "Bordx Control"): follow the bordereaux from Medix to the payor (and, for the Sihat payors, on to MNRB) and back: when each
-- was sent, when the documents and the money came back, and how many days that took. Read only. The desktop built the WHERE clause as text and called BordxControl with a case number for
-- each layout; here each layout is a procedure with typed parameters. The rows come from the same two views the desktop used (VIEW_Bordx_ControlS2 for the payors whose HLTCode is S,
-- VIEW_Bordx_ControlN2 for N); they hold one row per bordx and per distinct claim amount, so a bordx whose claims differ in amount appears more than once, as it did on the desktop.
-- The status criteria: @payTo / @payDocFr / @mnrbTo are the flags '1' (done) and '0' (not yet); @mnrbTo = 'B' is both ('0' or '1'); @mnrbFr / @payFr are F (paid), P (partial paid) and
-- U (unpaid: blank or NULL). A NULL criterion is not applied.

-- PayorComboListing + cboPayor_Click: the payors with the layout each uses (N non-Sihat, S Sihat).
CREATE OR ALTER PROCEDURE genisis.BordxControl_Payors
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name, RTRIM(HLTCode) AS HealthCode FROM dbo.PAY WHERE PAYCode IS NOT NULL ORDER BY PAYCode;
END
GO

-- SihatBordxControlListing and SihatBordxControlListing2: one payor, or all the Sihat payors (@payor NULL).
CREATE OR ALTER PROCEDURE genisis.BordxControl_Sihat
    @payor nvarchar(4) = NULL, @bordxFrom nvarchar(40) = NULL, @bordxTo nvarchar(40) = NULL, @dateFrom datetime = NULL, @dateTo datetime = NULL,
    @payTo char(1) = NULL, @payDocFr char(1) = NULL, @mnrbTo char(1) = NULL, @mnrbFr char(1) = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(BordxPAYCode) AS PayorCode, RTRIM(BordxRefNo) AS BordxNo, BordxDate, BordxNoClaims AS Claims, BordxClaimAmt AS Amount, RTRIM(BordxStatus) AS Status,
        BordxPAYSentDate AS SentToPayor, BordxPAYRecDocDate AS DocReceived, DATEDIFF(day, BordxPAYSentDate, BordxPAYRecDocDate) AS Days,
        BordxMNRBSentDate AS SentToMnrb, BordxMNRBRecDate AS MoneyFromMnrb, DATEDIFF(day, BordxMNRBSentDate, BordxMNRBRecDate) AS Days2
    FROM genisis.WS_BordxControlS
    WHERE (@payor IS NULL OR BordxPAYCode = @payor)
      AND (@bordxFrom IS NULL OR BordxRefNo >= @bordxFrom) AND (@bordxTo IS NULL OR BordxRefNo <= @bordxTo)
      AND (@dateFrom IS NULL OR BordxDate >= @dateFrom) AND (@dateTo IS NULL OR BordxDate <= @dateTo)
      AND (@payTo IS NULL OR BordxPAYToStatus = @payTo) AND (@payDocFr IS NULL OR BordxPAYDOCFrStatus = @payDocFr)
      AND (@mnrbTo IS NULL OR (@mnrbTo = 'B' AND BordxMNRBToStatus IN ('0', '1')) OR BordxMNRBToStatus = @mnrbTo)
      AND (@mnrbFr IS NULL OR (@mnrbFr = 'U' AND (BordxMNRBFrStatus = '' OR BordxMNRBFrStatus IS NULL)) OR BordxMNRBFrStatus = @mnrbFr)
    ORDER BY BordxPAYCode, BordxRefNo, BordxClaimAmt
    OPTION (RECOMPILE);
END
GO

-- NonSihatBordxControlListing and NonSihatBordxControlListing2: one payor, or all the non-Sihat payors (@payor NULL).
CREATE OR ALTER PROCEDURE genisis.BordxControl_NonSihat
    @payor nvarchar(4) = NULL, @bordxFrom nvarchar(40) = NULL, @bordxTo nvarchar(40) = NULL, @dateFrom datetime = NULL, @dateTo datetime = NULL,
    @payTo char(1) = NULL, @payDocFr char(1) = NULL, @payFr char(1) = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(BordxPAYCode) AS PayorCode, RTRIM(BordxRefNo) AS BordxNo, BordxDate, BordxNoClaims AS Claims, BordxClaimAmt AS Amount, RTRIM(BordxStatus) AS Status,
        BordxPAYSentDate AS SentToPayor, BordxPAYRecDocDate AS DocReceived, DATEDIFF(day, BordxPAYSentDate, BordxPAYRecDocDate) AS Days,
        BordxPAYRecDate AS MoneyFromPayor, DATEDIFF(day, BordxPAYSentDate, BordxPAYRecDate) AS Days2
    FROM genisis.WS_BordxControlN
    WHERE (@payor IS NULL OR BordxPAYCode = @payor)
      AND (@bordxFrom IS NULL OR BordxRefNo >= @bordxFrom) AND (@bordxTo IS NULL OR BordxRefNo <= @bordxTo)
      AND (@dateFrom IS NULL OR BordxDate >= @dateFrom) AND (@dateTo IS NULL OR BordxDate <= @dateTo)
      AND (@payTo IS NULL OR BordxPAYToStatus = @payTo) AND (@payDocFr IS NULL OR BordxPAYDOCFrStatus = @payDocFr)
      AND (@payFr IS NULL OR (@payFr = 'U' AND (BordxPAYFrStatus = '' OR BordxPAYFrStatus IS NULL)) OR BordxPAYFrStatus = @payFr)
    ORDER BY BordxPAYCode, BordxRefNo, BordxClaimAmt
    OPTION (RECOMPILE);
END
GO
