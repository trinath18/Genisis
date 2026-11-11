-- Claim Bordx > Storage > Document (legacy frmStorageDoc, "Storage / Cartons"): put the documents of the accounts department (bordereaux, payment vouchers and receipts) into cartons.
-- The desktop listed the documents of the chosen type with their storage row (dbo.StorageDocument) when they had one, built its WHERE clause as text and edited through open recordsets;
-- here every criterion is a typed parameter and a save is one procedure call.

-- ComboListing: the departments for the Dept drop-down (the desktop's SearchDPT).
CREATE OR ALTER PROCEDURE genisis.StorageDocument_Departments
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT RTRIM(DPTDescription) AS Department FROM genisis.Maint_DPT WHERE LEN(RTRIM(ISNULL(DPTDescription, N''))) > 0 ORDER BY RTRIM(DPTDescription);
END
GO

-- StorageSearchRecord + StorageListing (the desktop's Storage_Documents with case 1, 2 or 3): @docType B bordereaux, P payment vouchers (to hospitals and to members), R receipts (from MNRB and from
-- payors). The document number range, carton, file status, request by and the three dates apply to the storage row, so a document that is not stored yet only matches when none of those is given.
-- Unlike the other Storage screens the three dates are an exact day, as the desktop had them. @fileStatus NULL = all.
CREATE OR ALTER PROCEDURE genisis.StorageDocument_Search
    @docType char(1), @docFrom nvarchar(40) = NULL, @docTo nvarchar(40) = NULL, @carton nvarchar(20) = NULL, @fileStatus char(1) = NULL, @requestBy nvarchar(100) = NULL,
    @requestDate datetime = NULL, @deliveryDate datetime = NULL, @returnDate datetime = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    IF @docType = 'B'
        SELECT TOP (@max) RTRIM(b.BordxRefNo) AS DocumentNo, RTRIM(s.STRCartonNo) AS CartonNo, s.STRDocType AS DocType, RTRIM(s.STRFileStatus) AS FileStatus, s.LastUpdateDate AS RegisterDate,
            RTRIM(s.STRRequestBy) AS RequestBy, s.DORequest AS RequestDate, s.DODelivery AS DeliveryDate, s.DOReturn AS ReturnDate, RTRIM(s.STRRemarks) AS Remarks, RTRIM(s.STRDept) AS Department
        FROM genisis.WS_Bordx b LEFT JOIN genisis.WS_StorageDocument s ON s.STRDocumentNo = b.BordxRefNo
        WHERE b.BordxRefNo IS NOT NULL AND (@docFrom IS NULL OR b.BordxRefNo >= @docFrom) AND (@docTo IS NULL OR b.BordxRefNo <= @docTo)
          AND (@carton IS NULL OR s.STRCartonNo = @carton) AND (@fileStatus IS NULL OR s.STRFileStatus = @fileStatus) AND (@requestBy IS NULL OR s.STRRequestBy = @requestBy)
          AND (@requestDate IS NULL OR s.DORequest = @requestDate) AND (@deliveryDate IS NULL OR s.DODelivery = @deliveryDate) AND (@returnDate IS NULL OR s.DOReturn = @returnDate)
        ORDER BY b.BordxRefNo OPTION (RECOMPILE);
    ELSE IF @docType = 'P'
        SELECT TOP (@max) RTRIM(SearchPVNo) AS DocumentNo, RTRIM(STRCartonNo) AS CartonNo, STRDocType AS DocType, RTRIM(STRFileStatus) AS FileStatus, LastUpdateDate AS RegisterDate,
            RTRIM(STRRequestBy) AS RequestBy, DORequest AS RequestDate, DODelivery AS DeliveryDate, DOReturn AS ReturnDate, RTRIM(STRRemarks) AS Remarks, RTRIM(STRDept) AS Department
        FROM genisis.WS_StoragePVDoc
        WHERE (@docFrom IS NULL OR SearchPVNo >= @docFrom) AND (@docTo IS NULL OR SearchPVNo <= @docTo)
          AND (@carton IS NULL OR STRCartonNo = @carton) AND (@fileStatus IS NULL OR STRFileStatus = @fileStatus) AND (@requestBy IS NULL OR STRRequestBy = @requestBy)
          AND (@requestDate IS NULL OR DORequest = @requestDate) AND (@deliveryDate IS NULL OR DODelivery = @deliveryDate) AND (@returnDate IS NULL OR DOReturn = @returnDate)
        ORDER BY SearchPVNo OPTION (RECOMPILE);
    ELSE
        SELECT TOP (@max) RTRIM(RecNo) AS DocumentNo, RTRIM(STRCartonNo) AS CartonNo, STRDocType AS DocType, RTRIM(STRFileStatus) AS FileStatus, LastUpdateDate AS RegisterDate,
            RTRIM(STRRequestBy) AS RequestBy, DORequest AS RequestDate, DODelivery AS DeliveryDate, DOReturn AS ReturnDate, RTRIM(STRRemarks) AS Remarks, RTRIM(STRDept) AS Department
        FROM genisis.WS_StorageRecDoc
        WHERE (@docFrom IS NULL OR RecNo >= @docFrom) AND (@docTo IS NULL OR RecNo <= @docTo)
          AND (@carton IS NULL OR STRCartonNo = @carton) AND (@fileStatus IS NULL OR STRFileStatus = @fileStatus) AND (@requestBy IS NULL OR STRRequestBy = @requestBy)
          AND (@requestDate IS NULL OR DORequest = @requestDate) AND (@deliveryDate IS NULL OR DODelivery = @deliveryDate) AND (@returnDate IS NULL OR DOReturn = @returnDate)
        ORDER BY RecNo OPTION (RECOMPILE);
END
GO

-- SaveStore: puts a document into a carton (add), or changes the storage row of a document that is already stored (edit). Without a Request By the three dates are cleared, as the desktop did.
-- Result: 0 saved, 1 the document is stored already (add), 2 it is not stored (edit), 3 there is no such carton.
CREATE OR ALTER PROCEDURE genisis.StorageDocument_Save
    @documentNo nvarchar(40), @carton nvarchar(20), @fileStatus char(1), @requestBy nvarchar(100), @docType char(1), @department nvarchar(40),
    @requestDate datetime, @deliveryDate datetime, @returnDate datetime, @remarks nvarchar(400), @edit bit, @userCode nvarchar(100), @today datetime
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM genisis.WS_Carton WHERE CRTCartonNo = @carton)
    BEGIN
        SELECT 3 AS Result;
        RETURN;
    END
    IF LEN(RTRIM(ISNULL(@requestBy, N''))) = 0
        SELECT @requestDate = NULL, @deliveryDate = NULL, @returnDate = NULL;
    IF @edit = 0
    BEGIN
        IF EXISTS (SELECT 1 FROM genisis.WS_StorageDocument WHERE STRDocumentNo = @documentNo)
        BEGIN
            SELECT 1 AS Result;
            RETURN;
        END
        INSERT genisis.WS_StorageDocument (STRDocumentNo, STRCartonNo, STRDocType, STRDept, STRFileStatus, STRRequestBy, DORequest, DODelivery, DOReturn, STRRemarks, LastUpdateBy, LastUpdateDate)
        VALUES (@documentNo, @carton, @docType, @department, @fileStatus, @requestBy, @requestDate, @deliveryDate, @returnDate, @remarks, @userCode, @today);
    END
    ELSE
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM genisis.WS_StorageDocument WHERE STRDocumentNo = @documentNo)
        BEGIN
            SELECT 2 AS Result;
            RETURN;
        END
        UPDATE genisis.WS_StorageDocument
        SET STRCartonNo = @carton, STRDocType = @docType, STRDept = @department, STRFileStatus = @fileStatus, STRRequestBy = @requestBy, DORequest = @requestDate, DODelivery = @deliveryDate,
            DOReturn = @returnDate, STRRemarks = @remarks, LastUpdateBy = @userCode, LastUpdateDate = @today
        WHERE STRDocumentNo = @documentNo;
    END
    SELECT 0 AS Result;
END
GO
