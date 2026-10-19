-- Membership > Card Sending Mode (legacy frmMembershipCardControl): when and how membership cards were sent.
-- Principals: MBMTwo.MBMSendingMode / MBMCardDate / MBMDOCRef. Supplementaries: the same columns of MBMCoveredPersonsTwo.

-- Desktop MBMInquiryList on Search_MBM_CardSent (VIEW_CardSent_Principal). Result 1: the principals matching the filters;
-- result 2: the covered persons of the family principals among them (only those with an MBMCoveredPersonsTwo row, as in the
-- desktop's inner join). The sending-mode and sent-date filters apply to the principal only, as in the desktop.
CREATE OR ALTER PROCEDURE genisis.CardControl_Search
    @payor nvarchar(4000) = NULL, @numberFrom nvarchar(4000) = NULL, @numberTo nvarchar(4000) = NULL,
    @bordxFrom datetime = NULL, @bordxTo datetime = NULL, @batchFrom int = NULL, @batchTo int = NULL,
    @sentFrom datetime = NULL, @sentTo datetime = NULL, @mode nvarchar(4000) = NULL, @limit int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@limit) RTRIM(m.MBMNumber) AS MembershipNo, RTRIM(m.MBMName) AS Name, m.MBMBordxDate AS BordxDate, m.MBMBatchNo AS BatchNo,
        RTRIM(t.MBMSendingMode) AS SendingMode, t.MBMCardDate AS SentDate, RTRIM(m.MBMPolicyNo) AS PolicyNo, RTRIM(t.MBMDOCRef) AS DocReference,
        RTRIM(o.MBMGroupCompany) AS GroupCompany, RTRIM(o.MBMCostCenter) AS CostCenter, RTRIM(o.MBMBranch) AS Branch,
        RTRIM(o.MBMDepartment) AS Department, RTRIM(m.INSCode) AS InsuredType
    INTO #principals
    FROM dbo.MBM m
    JOIN dbo.MBMTwo t ON t.MBMNumber = m.MBMNumber
    LEFT JOIN dbo.MBMOthers o ON o.MBMNumber = m.MBMNumber
    WHERE (@payor IS NULL OR m.PAYCode = @payor)
      AND (@numberFrom IS NULL OR m.MBMNumber >= @numberFrom)
      AND (@numberTo IS NULL OR m.MBMNumber <= @numberTo)
      AND (@bordxFrom IS NULL OR m.MBMBordxDate >= @bordxFrom)
      AND (@bordxTo IS NULL OR m.MBMBordxDate <= @bordxTo)
      AND (@batchFrom IS NULL OR m.MBMBatchNo >= @batchFrom)
      AND (@batchTo IS NULL OR m.MBMBatchNo <= @batchTo)
      AND (@sentFrom IS NULL OR t.MBMCardDate >= @sentFrom)
      AND (@sentTo IS NULL OR t.MBMCardDate <= @sentTo)
      AND (@mode IS NULL OR t.MBMSendingMode = @mode)
    ORDER BY m.MBMNumber;

    SELECT MembershipNo, Name, BordxDate, BatchNo, SendingMode, SentDate, PolicyNo, DocReference, GroupCompany, CostCenter, Branch,
        Department, InsuredType
    FROM #principals ORDER BY MembershipNo;

    SELECT RTRIM(c.MBMCNumber) AS MembershipNo, RTRIM(c.MBMCCoverID) AS CoverId, RTRIM(c.MBMCName) AS Name,
        RTRIM(c2.MBMSendingMode) AS SendingMode, c2.MBMCardDate AS SentDate, RTRIM(c2.MBMDOCRef) AS DocReference
    FROM #principals p
    JOIN dbo.MBMCoveredPersons c ON c.MBMCNumber = p.MembershipNo
    JOIN dbo.MBMCoveredPersonsTwo c2 ON c2.MBMCNumber = c.MBMCNumber AND c2.MBMCCoverID = c.MBMCCoverID
    WHERE p.InsuredType IN ('F', 'H')
    ORDER BY c.MBMCNumber, c.MBMCCoverID;
END
GO

-- CmdUpdate_Click: cover 00 updates the principal (MBMTwo), any other cover the covered person (MBMCoveredPersonsTwo).
-- Result: the number of rows changed.
CREATE OR ALTER PROCEDURE genisis.CardControl_Update
    @number nvarchar(4000), @coverId nvarchar(4000), @mode nvarchar(4000), @sentDate datetime, @docRef nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    IF @coverId = N'00'
        UPDATE dbo.MBMTwo SET MBMSendingMode = @mode, MBMCardDate = @sentDate, MBMDOCRef = @docRef WHERE MBMNumber = @number;
    ELSE
        UPDATE dbo.MBMCoveredPersonsTwo SET MBMSendingMode = @mode, MBMCardDate = @sentDate, MBMDOCRef = @docRef
        WHERE MBMCNumber = @number AND MBMCCoverID = @coverId;
    SELECT @@ROWCOUNT AS Result;
END
GO
