-- Claim Bordx > Bor Delivery > Bordx Send Date. The desktop's menu item has no click handler; its form is frmUploadApproval ("Upload Send/Approval/Return"): frmUploadBordxSentDate, the form whose name
-- matches, is a copy of it with the code removed. It reads an Excel sheet from row 2 until column A is empty, checks it ("Verify ...") and then writes it in one transaction:
--   Send Date   (cmdUploadSend, UpdateSent):       the bordx's payor sent date, its send to A/C date (and its MNRB sent date when the number starts with S), and the web flag of its cases' remarks;
--   App Date    (cmdUpload):                       the payor approval code and date on the bordx's claims;
--   Return Date (cmdReturnDate, UpdateReturnPayor): the date the claims came back from the payor (CLMAppStatus 'A') and a remark on the case.
-- The desktop wrote text SQL with the values pasted in; here each step is a procedure with typed parameters.

-- The bordx numbers of the file that exist (verify). @numbers: the numbers separated by '|' (letters and digits only; the service sends nothing else).
CREATE OR ALTER PROCEDURE genisis.BordxSendDate_Known
    @numbers nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @x xml = CAST(N'<n>' + REPLACE(@numbers, N'|', N'</n><n>') + N'</n>' AS xml);
    SELECT DISTINCT RTRIM(b.BordxRefNo) AS BordxNo
    FROM (SELECT LTRIM(RTRIM(t.n.value(N'.', N'nvarchar(40)'))) AS No FROM @x.nodes(N'/n') AS t(n)) AS f
    JOIN genisis.WS_Bordx b ON b.BordxRefNo = f.No;
END
GO

-- UpdateSent: a bordx sent on @date. A number starting with S (an MNRB bordx) is also sent to MNRB. Every case with a claim in the bordx gets CASWebStatus 'Y' on its three remark rows. The desktop did not
-- check that the bordx was sent already; an upload overwrites the dates, as it did. Result: 0 done, 1 bordx not found.
CREATE OR ALTER PROCEDURE genisis.BordxSendDate_Send
    @bordxNo nvarchar(40), @date datetime, @userCode nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM genisis.WS_Bordx WHERE BordxRefNo = @bordxNo)
    BEGIN
        SELECT 1 AS Result;
        RETURN;
    END
    UPDATE genisis.WS_Bordx
    SET BordxMNRBSentDate = CASE WHEN LEFT(@bordxNo, 1) = N'S' THEN @date ELSE BordxMNRBSentDate END,
        BordxMNRBToStatus = CASE WHEN LEFT(@bordxNo, 1) = N'S' THEN '1' ELSE BordxMNRBToStatus END,
        BordxMNRBSentBy = CASE WHEN LEFT(@bordxNo, 1) = N'S' THEN @userCode ELSE BordxMNRBSentBy END,
        BordxPAYSentDate = @date, BordxPAYToStatus = '1', BordxPAYSentBy = @userCode,
        BordxSendACDate = @date, BordxSendtoACStatus = 1, BordxSendACBy = @userCode
    WHERE BordxRefNo = @bordxNo;
    UPDATE r SET CASWebStatus = 'Y' FROM dbo.CASRemarks r WHERE r.CASNumber IN (SELECT c.CASNumber FROM genisis.WS_Claim c WHERE c.BordxRefNo = @bordxNo);
    UPDATE r SET CASWebStatus = 'Y' FROM dbo.CASRemarksTwo r WHERE r.CASNumber IN (SELECT c.CASNumber FROM genisis.WS_Claim c WHERE c.BordxRefNo = @bordxNo);
    UPDATE r SET CASWebStatus = 'Y' FROM dbo.CASRemarkThree r WHERE r.CASNumber IN (SELECT c.CASNumber FROM genisis.WS_Claim c WHERE c.BordxRefNo = @bordxNo);
    SELECT 0 AS Result;
END
GO

-- cmdUpload: the payor's approval code and date on every claim of the bordx (CLMPayorAppStatus 'Y'). Result: 0 done, 1 bordx not found.
CREATE OR ALTER PROCEDURE genisis.BordxSendDate_Approve
    @bordxNo nvarchar(40), @appCode nvarchar(15), @appDate datetime
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM genisis.WS_Bordx WHERE BordxRefNo = @bordxNo)
    BEGIN
        SELECT 1 AS Result;
        RETURN;
    END
    UPDATE genisis.WS_Claim SET CLMPayorAppCode = @appCode, CLMPayorAppDate = @appDate, CLMPayorAppStatus = 'Y' WHERE BordxRefNo = @bordxNo;
    SELECT 0 AS Result;
END
GO

-- UpdateReturnPayor: the claims of the bordx came back from the payor on @date (BordxPAYRecDocDate on the claim rows, CLMAppStatus 'A'), and the case gets the line
-- "<today>   Received Returned Bordx  -  <bordx> on <date>   <user>". The desktop wrote the line on the first case of the bordx only (it read one CASNumber); here every case of the bordx gets it.
-- Result: 0 done, 1 bordx not found.
CREATE OR ALTER PROCEDURE genisis.BordxSendDate_Return
    @bordxNo nvarchar(40), @date datetime, @userCode nvarchar(50), @today datetime
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM genisis.WS_Bordx WHERE BordxRefNo = @bordxNo)
    BEGIN
        SELECT 1 AS Result;
        RETURN;
    END
    UPDATE genisis.WS_Claim SET BordxPAYRecDocDate = @date, CLMAppStatus = 'A' WHERE BordxRefNo = @bordxNo;
    DECLARE @line nvarchar(max) = N'~' + NCHAR(10) + CONVERT(nvarchar(10), @today, 103) + N'   Received Returned Bordx  -  ' + @bordxNo + N' on ' + CONVERT(nvarchar(10), @date, 103)
        + REPLICATE(N' ', 27) + @userCode + N' ';
    DECLARE @cas nvarchar(30);
    DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT RTRIM(CASNumber) FROM genisis.WS_Claim WHERE BordxRefNo = @bordxNo AND NULLIF(CASNumber, N'') IS NOT NULL ORDER BY 1;
    OPEN c;
    FETCH NEXT FROM c INTO @cas;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        EXEC genisis.Case_AppendRemark @cas, @line;
        FETCH NEXT FROM c INTO @cas;
    END
    CLOSE c;
    DEALLOCATE c;
    SELECT 0 AS Result;
END
GO
