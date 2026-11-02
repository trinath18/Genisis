-- Contribution Billing > Refund Listing (legacy frmMCORefundListing2, "MCO Refund Listing"): the MCO fees refunded to a payor's members who were cancelled or endorsed. Read only. The desktop called
-- MBMRefundListing with a case number (1 and 2: the principals, which differ only in how the screen showed DayUtilised; 3: the supplementary members of one principal) and a text WHERE
-- fragment, once per principal for the supplementary members; here two procedures with typed parameters, the second for every principal at once. "Print CR Note", which writes a credit note
-- number to the members, is not part of this migration (see docs/remaining-work.md).
--   @dateBy 'effective' filters the endorsement effective date (MBMENDtEffDate), 'bordx' the endorsement bordx date (MBMENDtBordxDate); a NULL date is not applied.
--   @printed 'N' keeps the members without a credit note, 'Y' the ones that have one (the desktop's Print Status; anything else it treated as Yes).

CREATE OR ALTER PROCEDURE genisis.RefundListing_Principals
    @payor nvarchar(4), @dateBy varchar(10), @dateFrom datetime = NULL, @dateTo datetime = NULL, @printed char(1) = 'N', @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT TOP (@max) RTRIM(MBMNumber) AS MemberNo, RTRIM(MBMCOVID) AS CoverId, RTRIM(MBMPolicyNo) AS PolicyNo, RTRIM(MBMName) AS MemberName, RTRIM(MBMStatus) AS Status,
        RTRIM(MBMIcBcPp) AS IcNo, MBMBordxDate AS BordxDate, MBMPayorEffDate AS PayorEffDate, MBMPayorExpDate AS PayorExpDate, DayInsured AS DaysInsured, MBMENDtEffDate AS EndtEffDate,
        MBMENDtBordxDate AS EndtBordxDate, DayUtilised AS DaysUtilised, MonthUtilised AS MonthsUtilised, MBMMCOFee AS McoFee, MBMMCOUtilised AS McoUtilised, MBMMCORefund AS McoRefund,
        RTRIM(MBMInvoiceNo) AS InvoiceNo, MBMInvoiceDate AS InvoiceDate, TotalCase AS TotalCases, RTRIM(MBMCRNote) AS CreditNote, MBMCRNoteDate AS CreditDate, RTRIM(INSCode) AS InsCode
    FROM dbo.VIEW_MBM_Refund_Search
    WHERE PAYCode = @payor
      AND ((@dateBy = 'effective' AND (@dateFrom IS NULL OR MBMENDtEffDate >= @dateFrom) AND (@dateTo IS NULL OR MBMENDtEffDate <= @dateTo))
        OR (@dateBy = 'bordx' AND (@dateFrom IS NULL OR MBMENDtBordxDate >= @dateFrom) AND (@dateTo IS NULL OR MBMENDtBordxDate <= @dateTo)))
      AND ((@printed = 'N' AND (MBMCRNote IS NULL OR MBMCRNote = '')) OR (@printed <> 'N' AND MBMCRNote <> ''))
    ORDER BY MemberNo
    OPTION (RECOMPILE);
END
GO

-- The supplementary members (dependants) of the principals the criteria find, in the same columns. The desktop looked them up one principal at a time, with the payor and date criteria but not the
-- print status.
CREATE OR ALTER PROCEDURE genisis.RefundListing_Supplementary
    @payor nvarchar(4), @dateBy varchar(10), @dateFrom datetime = NULL, @dateTo datetime = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT TOP (@max) RTRIM(MBMNumber) AS MemberNo, RTRIM(MBMCOVID) AS CoverId, RTRIM(MBMPolicyNo) AS PolicyNo, RTRIM(MBMName) AS MemberName, RTRIM(MBMStatus) AS Status,
        RTRIM(MBMIcBcPp) AS IcNo, MBMBordxDate AS BordxDate, MBMPayorEffDate AS PayorEffDate, MBMPayorExpDate AS PayorExpDate, DayInsured AS DaysInsured, MBMENDtEffDate AS EndtEffDate,
        MBMENDtBordxDate AS EndtBordxDate, DayUtilised AS DaysUtilised, MonthUtilised AS MonthsUtilised, MBMMCOFee AS McoFee, MBMMCOUtilised AS McoUtilised, MBMMCORefund AS McoRefund,
        RTRIM(MBMInvoiceNo) AS InvoiceNo, MBMInvoiceDate AS InvoiceDate, TotalCase AS TotalCases, RTRIM(MBMCRNote) AS CreditNote, MBMCRNoteDate AS CreditDate
    FROM dbo.View_MBM_Supplementary_Refund
    WHERE PAYCode = @payor
      AND ((@dateBy = 'effective' AND (@dateFrom IS NULL OR MBMENDtEffDate >= @dateFrom) AND (@dateTo IS NULL OR MBMENDtEffDate <= @dateTo))
        OR (@dateBy = 'bordx' AND (@dateFrom IS NULL OR MBMENDtBordxDate >= @dateFrom) AND (@dateTo IS NULL OR MBMENDtBordxDate <= @dateTo)))
    ORDER BY MemberNo, CoverId
    OPTION (RECOMPILE);
END
GO

-- cboPayor_Click: whether the payor's cancelled members are listed with MBMCancelStatus = Y; the desktop then showed DayUtilised below one as 0 (the other payors' raw value).
CREATE OR ALTER PROCEDURE genisis.RefundListing_PayorStatus
    @payor nvarchar(4)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name, CASE WHEN MBMCancelStatus = 'Y' THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS CancelStatus FROM dbo.PAY WHERE PAYCode = @payor;
END
GO
