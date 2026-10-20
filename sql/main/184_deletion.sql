-- Membership > Deletion (legacy frmDeleteMembers). Memberships of one health type and payor, chosen by bordx date range (D),
-- bordx date + batch (B) or membership number range (M), are copied to the archive database (genisis.Arch_*) and deleted from
-- MBM, MBMOthers and, for family plans, MBMCoveredPersons. Other tables (MBMTwo, MBMCrossReference, ...) are left as the desktop
-- left them.

-- Health types and payors for the drop-downs (desktop ComboListing: tables HLT and PAY).
CREATE OR ALTER PROCEDURE genisis.Deletion_Lookups
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(HLTCode) AS Code, RTRIM(HLTDescription) AS Name FROM dbo.HLT ORDER BY HLTCode;
    SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name FROM dbo.PAY ORDER BY PAYCode;
END
GO

-- The principals a delete option selects (desktop Exportsql / DELsqlList).
CREATE OR ALTER FUNCTION genisis.Deletion_Selection
    (@hlt nvarchar(4000), @pay nvarchar(4000), @option nvarchar(4000), @bordxFrom datetime, @bordxTo datetime, @receiptDate datetime,
     @batch int, @numberFrom nvarchar(4000), @numberTo nvarchar(4000))
RETURNS TABLE
AS
RETURN
    SELECT m.MBMNumber, m.INSCode, m.MBMMemberType
    FROM dbo.MBM m
    WHERE m.HLTCode = @hlt AND m.PAYCode = @pay
      AND ((@option = N'D' AND m.MBMBordxDate >= @bordxFrom AND m.MBMBordxDate <= @bordxTo)
        OR (@option = N'B' AND m.MBMBordxDate = @receiptDate AND m.MBMBatchNo = @batch)
        OR (@option = N'M' AND m.MBMNumber >= @numberFrom AND m.MBMNumber <= @numberTo));
GO

-- Desktop DELMBMListing / DELSuppListing. Result 1: the principals; result 2: covered persons of the family principals that have a
-- bordx date (the desktop lists only those, but deletes all).
CREATE OR ALTER PROCEDURE genisis.Deletion_List
    @hlt nvarchar(4000), @pay nvarchar(4000), @option nvarchar(4000), @bordxFrom datetime = NULL, @bordxTo datetime = NULL,
    @receiptDate datetime = NULL, @batch int = NULL, @numberFrom nvarchar(4000) = NULL, @numberTo nvarchar(4000) = NULL, @limit int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@limit) RTRIM(m.MBMNumber) AS MembershipNo, m.MBMBordxDate AS BordxDate, m.MBMBatchNo AS BatchNo, RTRIM(m.MBMName) AS Name,
        RTRIM(m.MBMMemberType) AS MemberType, RTRIM(m.INSCode) AS InsuredType
    INTO #principals
    FROM genisis.Deletion_Selection(@hlt, @pay, @option, @bordxFrom, @bordxTo, @receiptDate, @batch, @numberFrom, @numberTo) s
    JOIN dbo.MBM m ON m.MBMNumber = s.MBMNumber
    ORDER BY m.MBMNumber;

    SELECT MembershipNo, BordxDate, BatchNo, Name, MemberType, InsuredType FROM #principals ORDER BY MembershipNo;

    SELECT RTRIM(c.MBMCNumber) AS MembershipNo, RTRIM(c.MBMCCoverID) AS CoverId, RTRIM(c.MBMCName) AS Name, c.MBMCBordxDate AS BordxDate,
        c.MBMCBatchNo AS BatchNo, RTRIM(c.MBMCMemberType) AS MemberType
    FROM #principals p JOIN dbo.MBMCoveredPersons c ON c.MBMCNumber = p.MembershipNo
    WHERE p.InsuredType IN ('F', 'H') AND c.MBMCBordxDate IS NOT NULL
    ORDER BY c.MBMCNumber, c.MBMCCoverID;
END
GO

-- Desktop DeletePrincipals / DeleteMBMOthers / DeleteSupplementary: archive then delete. The caller owns the transaction.
-- Columns the desktop read from MBM / MBMOthers / MBMCoveredPersons that have since moved to MBMTwo / MBMOthersTwo /
-- MBMCoveredPersonsTwo are read from there.
-- Result: Principals (member type P, as the desktop counts them), Supplementaries (covered persons not of type P), Memberships.
CREATE OR ALTER PROCEDURE genisis.Deletion_Delete
    @hlt nvarchar(4000), @pay nvarchar(4000), @option nvarchar(4000), @bordxFrom datetime = NULL, @bordxTo datetime = NULL,
    @receiptDate datetime = NULL, @batch int = NULL, @numberFrom nvarchar(4000) = NULL, @numberTo nvarchar(4000) = NULL, @today datetime
AS
BEGIN
    SET NOCOUNT ON;
    SELECT MBMNumber, INSCode, MBMMemberType INTO #selected
    FROM genisis.Deletion_Selection(@hlt, @pay, @option, @bordxFrom, @bordxTo, @receiptDate, @batch, @numberFrom, @numberTo);

    INSERT INTO genisis.Arch_MBM (MBMNumber, MBMPolicyNo, MBMIcBcPp, MBMName, MBMSalutation, HLTCode, PAYCode, MBMAddress1, MBMAddress2,
        MBMAddress3, CTYCode, MBMPostCode, MBMTelnoH, MBMTelNoM, MBMTelNoO, MBMEmail, MBMDOB, NATCode, RACCode, MBMSex, MBMPayorEffDate,
        MBMPayorExpDate, AGECode, INSCode, PLNCode, MBMAdultChild, MBMAvailableLimit, SRVCodeList, STACode, LGNCode, MBMBordxDate,
        MBMTakeover, MBMRenewFlag, BRNCode, MBMStatus, MBMValueDate, MBMPolicyYear, MBMMemberType, MBMBatchNo, MBMDataStatus)
    SELECT m.MBMNumber, m.MBMPolicyNo, m.MBMIcBcPp, m.MBMName, t.MBMSalutation, m.HLTCode, m.PAYCode, m.MBMAddress1, m.MBMAddress2,
        m.MBMAddress3, m.CTYCode, m.MBMPostCode, t.MBMTelnoH, t.MBMTelNoM, t.MBMTelNoO, t.MBMEmail, m.MBMDOB, t.NATCode, m.RACCode, m.MBMSex,
        m.MBMPayorEffDate, m.MBMPayorExpDate, m.AGECode, m.INSCode, m.PLNCode, m.MBMAdultChild, m.MBMAvailableLimit, t.SRVCodeList, m.STACode,
        t.LGNCode, m.MBMBordxDate, m.MBMTakeover, m.MBMRenewFlag, m.BRNCode, 'D', @today, m.MBMPolicyYear, m.MBMMemberType, m.MBMBatchNo, 'D'
    FROM dbo.MBM m JOIN #selected s ON s.MBMNumber = m.MBMNumber
    LEFT JOIN dbo.MBMTwo t ON t.MBMNumber = m.MBMNumber;

    INSERT INTO genisis.Arch_MBMOthers (MBMNumber, MBMAgentCode, MBMWeight, MBMHeight, MBMWorkNature, MBMOccupation, MBMBlood, MBMAllergic,
        MBMFirstMemNo, MBMGroupCompany, MBMSmoking, MBMAlcohol, MBMMaritalStatus, MBMPrevPolNo, MBMPrevMemNo, MBMEmployeeNo)
    SELECT o.MBMNumber, o.MBMAgentCode, w.MBMWeight, w.MBMHeight, w.MBMWorkNature, w.MBMOccupation, w.MBMBlood, w.MBMAllergic,
        o.MBMFirstMemNo, o.MBMGroupCompany, w.MBMSmoking, w.MBMAlcohol, w.MBMMaritalStatus, o.MBMPrevPolNo, o.MBMPrevMemNo, o.MBMEmployeeNo
    FROM dbo.MBMOthers o JOIN #selected s ON s.MBMNumber = o.MBMNumber
    LEFT JOIN dbo.MBMOthersTwo w ON w.MBMNumber = o.MBMNumber;

    INSERT INTO genisis.Arch_MBMCoveredPersons (MBMCNumber, MBMCCoverID, MBMCSalutation, MBMCName, MBMCICBCPPNo, MBMCDOB, MBMCAgeband,
        MBMCSex, MBMCAdultChild, MBMCRELCode, MBMCBloodType, MBMCAllergic, MBMCMemberType, MBMCAnnualLimit)
    SELECT c.MBMCNumber, c.MBMCCoverID, c2.MBMCSalutation, c.MBMCName, c.MBMCICBCPPNo, c.MBMCDOB, c.MBMCAgeband, c.MBMCSex, c.MBMCAdultChild,
        c.MBMCRELCode, c2.MBMCBloodType, c2.MBMCAllergic, c.MBMCMemberType, c.MBMCAnnualLimit
    FROM dbo.MBMCoveredPersons c JOIN #selected s ON s.MBMNumber = c.MBMCNumber
    LEFT JOIN dbo.MBMCoveredPersonsTwo c2 ON c2.MBMCNumber = c.MBMCNumber AND c2.MBMCCoverID = c.MBMCCoverID
    WHERE s.INSCode IN ('F', 'H');

    DECLARE @supplementaries int = (SELECT COUNT(*) FROM dbo.MBMCoveredPersons c JOIN #selected s ON s.MBMNumber = c.MBMCNumber
        WHERE s.INSCode IN ('F', 'H') AND ISNULL(c.MBMCMemberType, '') <> 'P');

    DELETE o FROM dbo.MBMOthers o JOIN #selected s ON s.MBMNumber = o.MBMNumber;
    DELETE c FROM dbo.MBMCoveredPersons c JOIN #selected s ON s.MBMNumber = c.MBMCNumber WHERE s.INSCode IN ('F', 'H');
    DELETE m FROM dbo.MBM m JOIN #selected s ON s.MBMNumber = m.MBMNumber;

    SELECT (SELECT COUNT(*) FROM #selected WHERE MBMMemberType = 'P') AS Principals, @supplementaries AS Supplementaries,
        (SELECT COUNT(*) FROM #selected) AS Memberships;
END
GO
