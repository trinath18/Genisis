-- Claim Bordx > Storage > Claim files (legacy frmStorageEntry, "Storage Entry"): register the cartons the claim files are kept in (dbo.Carton) and put claim files into them (dbo.Storage). The desktop
-- edited through open recordsets and text SQL; here each write is one procedure with typed parameters. Searching the cartons and the files is the Storage Enquiry search
-- (genisis.StorageEnquiry_CartonSearch and _StorageSearch, the latter with its "include claims that are not stored yet" switch). Codes, remarks and names are trimmed and upper-cased by the API (ChkStr).

-- SaveCarton / SaveRecByCarton: adds a carton, or (edit = 1) changes its status, dates and remarks. Result: 0 saved, 1 the carton exists already (add), 2 there is no such carton (edit).
CREATE OR ALTER PROCEDURE genisis.StorageEntry_CartonSave
    @carton nvarchar(40), @status char(1), @sendDate datetime, @requestDate datetime, @deliveryDate datetime, @returnDate datetime, @destroyDate datetime, @remarks nvarchar(max),
    @edit bit, @userCode nvarchar(20), @today datetime
AS
BEGIN
    SET NOCOUNT ON;
    IF @edit = 0
    BEGIN
        IF EXISTS (SELECT 1 FROM genisis.WS_Carton WHERE CRTCartonNo = @carton)
        BEGIN
            SELECT 1 AS Result;
            RETURN;
        END
        INSERT genisis.WS_Carton (CRTCartonNo, DOSend, DORequest, DODelivery, DOReturn, DODestroy, CRTCartonStatus, CRTRemarks, LastUpdateBy, LastUpdateDate)
        VALUES (@carton, @sendDate, @requestDate, @deliveryDate, @returnDate, @destroyDate, @status, @remarks, @userCode, @today);
    END
    ELSE
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM genisis.WS_Carton WHERE CRTCartonNo = @carton)
        BEGIN
            SELECT 2 AS Result;
            RETURN;
        END
        UPDATE genisis.WS_Carton
        SET DOSend = @sendDate, DORequest = @requestDate, DODelivery = @deliveryDate, DOReturn = @returnDate, DODestroy = @destroyDate, CRTCartonStatus = @status, CRTRemarks = @remarks,
            LastUpdateBy = @userCode, LastUpdateDate = @today
        WHERE CRTCartonNo = @carton;
    END
    SELECT 0 AS Result;
END
GO

-- cmdDelete_Click (Carton Details): deletes a carton. The desktop deleted it whatever was stored in it, leaving the files with a carton that no longer exists; a carton with files in it is
-- refused here. Result: 0 deleted, 1 no such carton, 2 files are stored in it.
CREATE OR ALTER PROCEDURE genisis.StorageEntry_CartonDelete
    @carton nvarchar(40)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM genisis.WS_Carton WHERE CRTCartonNo = @carton)
    BEGIN
        SELECT 1 AS Result;
        RETURN;
    END
    IF EXISTS (SELECT 1 FROM genisis.WS_Storage WHERE STRCartonNo = @carton)
    BEGIN
        SELECT 2 AS Result;
        RETURN;
    END
    DELETE genisis.WS_Carton WHERE CRTCartonNo = @carton;
    SELECT 0 AS Result;
END
GO

-- SaveStore: puts a claim file into a carton (add), or (edit = 1) changes its storage row. Without a Request By the three dates are cleared, as the desktop did.
-- Result: 0 saved, 1 the claim is stored already (add), 2 it is not stored (edit), 3 there is no such carton, 4 there is no such claim.
CREATE OR ALTER PROCEDURE genisis.StorageEntry_FileSave
    @caseNo nvarchar(40), @version nvarchar(6), @carton nvarchar(40), @fileStatus char(1), @requestBy nvarchar(100), @requestDate datetime, @deliveryDate datetime, @returnDate datetime,
    @remarks nvarchar(max), @edit bit, @userCode nvarchar(20), @today datetime
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM genisis.WS_Carton WHERE CRTCartonNo = @carton)
    BEGIN
        SELECT 3 AS Result;
        RETURN;
    END
    IF NOT EXISTS (SELECT 1 FROM genisis.WS_Claim WHERE CASNumber = @caseNo AND ClaimVersion = CAST(@version AS int))
    BEGIN
        SELECT 4 AS Result;
        RETURN;
    END
    IF LEN(RTRIM(ISNULL(@requestBy, N''))) = 0
        SELECT @requestDate = NULL, @deliveryDate = NULL, @returnDate = NULL;
    IF @edit = 0
    BEGIN
        IF EXISTS (SELECT 1 FROM genisis.WS_Storage WHERE STRCaseNo = @caseNo AND STRCLMVersion = @version)
        BEGIN
            SELECT 1 AS Result;
            RETURN;
        END
        INSERT genisis.WS_Storage (STRCaseNo, STRCLMVersion, STRCartonNo, STRFileStatus, STRRequestBy, DORequest, DODelivery, DOReturn, STRRemarks, LastUpdateBy, LastUpdateDate)
        VALUES (@caseNo, @version, @carton, @fileStatus, @requestBy, @requestDate, @deliveryDate, @returnDate, @remarks, @userCode, @today);
    END
    ELSE
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM genisis.WS_Storage WHERE STRCaseNo = @caseNo AND STRCLMVersion = @version)
        BEGIN
            SELECT 2 AS Result;
            RETURN;
        END
        UPDATE genisis.WS_Storage
        SET STRCartonNo = @carton, STRFileStatus = @fileStatus, STRRequestBy = @requestBy, DORequest = @requestDate, DODelivery = @deliveryDate, DOReturn = @returnDate, STRRemarks = @remarks,
            LastUpdateBy = @userCode, LastUpdateDate = @today
        WHERE STRCaseNo = @caseNo AND STRCLMVersion = @version;
    END
    SELECT 0 AS Result;
END
GO

-- DeleteRecords (the Remove button): takes one claim file out of storage. Result: 0 removed, 1 it was not stored.
CREATE OR ALTER PROCEDURE genisis.StorageEntry_FileRemove
    @caseNo nvarchar(40), @version nvarchar(6)
AS
BEGIN
    SET NOCOUNT ON;
    DELETE genisis.WS_Storage WHERE STRCaseNo = @caseNo AND STRCLMVersion = @version;
    SELECT CASE WHEN @@ROWCOUNT = 0 THEN 1 ELSE 0 END AS Result;
END
GO
