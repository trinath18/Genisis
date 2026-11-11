-- Claim Bordx > Bor Delivery > Returned Fr Payor (Not App) (legacy FrmReturnPayorCase, "Bordx Delivery"): look up one worksheet of a bordx that was sent to the payor and set the
-- worksheet's approved status (CLMAppStatus) to N or Y. The desktop ran dbo.Bordx_ReturnPayorCase (dynamic SQL on VIEW_BORDX_ClaimApp) and updated Claim by text; here the criteria
-- are typed parameters and the update checks the bordx has been sent to the payor.

-- Bordx_ReturnPayorCase, case 2 (TxtCaseNo_LostFocus): every claim version of a case, for the Version drop-down.
CREATE OR ALTER PROCEDURE genisis.BordxReturnCase_Versions
    @cas nvarchar(30)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ClaimVersion AS Version FROM genisis.WS_Claim WHERE CASNumber = @cas ORDER BY ClaimVersion;
END
GO

-- Bordx_ReturnPayorCase, case 1: the worksheet and its bordx, when the bordx has been sent to the payor (BordxPAYToStatus = '1'); nothing otherwise ("Bordx not send to Payor!").
CREATE OR ALTER PROCEDURE genisis.BordxReturnCase_Find
    @cas nvarchar(30), @version int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) RTRIM(b.BordxPAYCode) AS Payor, RTRIM(c.CASNumber) AS CaseNo, c.ClaimVersion, c.CLMTotalApproved AS ApprovedAmount, RTRIM(c.CLMAppStatus) AS AppStatus,
        RTRIM(b.BordxRefNo) AS BordxNo, b.BordxDate, b.BordxClaimAmt AS BordxAmount, RTRIM(b.BordxStatus) AS BordxStatus
    FROM genisis.WS_Claim c
    JOIN genisis.WS_Bordx b ON b.BordxRefNo = c.BordxRefNo
    WHERE c.CASNumber = @cas AND c.ClaimVersion = @version AND b.BordxPAYToStatus = '1';
END
GO

-- UpdateCLM: sets the worksheet's approved status ('N' or 'Y'). Result: 0 done, 1 the worksheet is not in a bordx that was sent to the payor.
CREATE OR ALTER PROCEDURE genisis.BordxReturnCase_Set
    @cas nvarchar(30), @version int, @status char(1)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM genisis.WS_Claim c JOIN genisis.WS_Bordx b ON b.BordxRefNo = c.BordxRefNo
                   WHERE c.CASNumber = @cas AND c.ClaimVersion = @version AND b.BordxPAYToStatus = '1')
    BEGIN
        SELECT 1 AS Result;
        RETURN;
    END
    UPDATE genisis.WS_Claim SET CLMAppStatus = @status WHERE CASNumber = @cas AND ClaimVersion = @version;
    SELECT 0 AS Result;
END
GO
