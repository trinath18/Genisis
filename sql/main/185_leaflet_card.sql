-- Membership > Leaflet & Card (legacy frmMainReport, Leaflet and Card Printing tabs; Crystal Reports Leaflet.rpt and
-- MBMCardPrinting.rpt). The desktop's printed / reprinted flags lived in dbo.MBMPrinting, which this database does not have,
-- so they are not tracked.

-- RecordSearching: memberships of one payor, health type and batch within a bordx date range.
CREATE OR ALTER PROCEDURE genisis.LeafletCard_Search
    @from datetime, @to datetime, @pay nvarchar(4000), @hlt nvarchar(4000), @batch int, @limit int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@limit) RTRIM(PAYCode) AS PayorCode, RTRIM(MBMNumber) AS MembershipNo, MBMBordxDate AS BordxDate, RTRIM(MBMPolicyNo) AS PolicyNo,
        RTRIM(HLTCode) AS HealthCode, MBMBatchNo AS BatchNo, RTRIM(MBMName) AS Name
    FROM dbo.MBM
    WHERE MBMBordxDate >= @from AND MBMBordxDate <= @to AND PAYCode = @pay AND HLTCode = @hlt AND MBMBatchNo = @batch
    ORDER BY PAYCode, MBMBordxDate, HLTCode, MBMBatchNo, MBMNumber;
END
GO

-- Leaflet.rpt reads VIEW_LEAFLET (active memberships only) one membership at a time. Result 1: one row per membership;
-- result 2: its covered persons.
CREATE OR ALTER PROCEDURE genisis.LeafletCard_Leaflets
    @from datetime, @to datetime, @pay nvarchar(4000), @hlt nvarchar(4000), @batch int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT RTRIM(v.MBMNumber) AS MembershipNo, RTRIM(v.MBMPolicyNo) AS PolicyNo, RTRIM(v.MBMAgentCode) AS AgentCode,
        RTRIM(v.MBMName) AS Name, RTRIM(v.MBMAddress1) AS Address1, RTRIM(v.MBMAddress2) AS Address2, RTRIM(v.MBMAddress3) AS Address3,
        RTRIM(v.MBMPostCode) AS PostCode, RTRIM(v.CTYCode) AS City, RTRIM(v.STACode) AS State, RTRIM(v.PAYCompanyName) AS PayorName,
        RTRIM(v.MBMGroupCompany) AS GroupCompany, RTRIM(v.MBMEmployeeNo) AS EmployeeNo, RTRIM(v.MBMDepartment) AS Department,
        RTRIM(v.INSCode) AS InsuredType, RTRIM(v.PLNDescription) AS PlanDescription
    INTO #leaflets
    FROM dbo.VIEW_LEAFLET v
    JOIN dbo.MBM m ON m.MBMNumber = v.MBMNumber
    WHERE m.MBMBordxDate >= @from AND m.MBMBordxDate <= @to AND m.PAYCode = @pay AND m.HLTCode = @hlt AND m.MBMBatchNo = @batch;

    SELECT * FROM #leaflets ORDER BY MembershipNo;

    SELECT RTRIM(c.MBMCNumber) AS MembershipNo, RTRIM(c.MBMCCoverID) AS CoverId, LTRIM(RTRIM(c.MBMCName)) AS Name, RTRIM(c.MBMCRELCode) AS Relationship
    FROM dbo.MBMCoveredPersons c
    WHERE c.MBMCNumber IN (SELECT MembershipNo FROM #leaflets)
    ORDER BY c.MBMCNumber, c.MBMCCoverID;
END
GO

-- MBMCardPrinting.rpt / ExpMBMPrintingToExcel read VIEW_CardPrinting: one card per covered person, the principal as cover 00.
-- The desktop's card selection does not filter on health type.
CREATE OR ALTER PROCEDURE genisis.LeafletCard_Cards
    @from datetime, @to datetime, @pay nvarchar(4000), @batch int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT RTRIM(PAYCode) AS PayorCode, MBMBordxDate AS BordxDate, RTRIM(HLTCode) AS HealthCode, MBMBatchNo AS BatchNo,
        RTRIM(MBMNumber) AS MembershipNo, RTRIM(MBMCCoverID) AS CoverId, RTRIM(MBMCName) AS Name, RTRIM(MBMCICBCPPNo) AS IcBcPp,
        RTRIM(MBMCRELCode) AS Relationship, RTRIM(MBMPolicyNo) AS PolicyNo, RTRIM(PLNCode) AS PlanCode, MBMPayorExpDate AS ExpiryDate,
        RTRIM(MBMGroupCompany) AS GroupCompany, RTRIM(MBMEmployeeNo) AS EmployeeNo
    FROM dbo.VIEW_CardPrinting
    WHERE MBMNumber IS NOT NULL AND MBMCNumber IS NOT NULL
      AND MBMBordxDate >= @from AND MBMBordxDate <= @to AND PAYCode = @pay AND MBMBatchNo = @batch
    ORDER BY PayorCode, BordxDate, HealthCode, BatchNo, MembershipNo, CoverId;
END
GO

-- Payor names for the heading of the card export (desktop ComboListing on PAY).
CREATE OR ALTER PROCEDURE genisis.LeafletCard_Payor
    @pay nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(PAYCompanyName) AS Name FROM dbo.PAY WHERE PAYCode = @pay;
END
GO
