-- Claim Bordx > Storage > Enquiry (legacy frmStorageEnq, "Storage Enquiry"): look up the cartons the claim files are kept in (dbo.Carton, tab "Carton Details") and the files in them
-- (dbo.Storage joined to the claims by dbo.VIEW_Storage_CLM, tab "Storage Details"). The desktop built the WHERE clause as text; here every criterion is a typed parameter. Read only.
-- Like the desktop, the first box of a range of dates means "on or after" and the second "on or before"; a blank box is no criterion; the dates have no time part to compare.

-- ComboListing: the carton numbers for the Carton drop-downs (the desktop's SearchCarton).
CREATE OR ALTER PROCEDURE genisis.StorageEnquiry_Cartons
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT RTRIM(CRTCartonNo) AS CartonNo FROM genisis.WS_Carton ORDER BY RTRIM(CRTCartonNo);
END
GO

-- ComboListing: the users for the Request By drop-down (the desktop's SearchUser, the user names).
CREATE OR ALTER PROCEDURE genisis.StorageEnquiry_Users
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT RTRIM(USRName) AS UserName FROM genisis.Maint_USR WHERE LEN(RTRIM(ISNULL(USRName, N''))) > 0 ORDER BY RTRIM(USRName);
END
GO

-- CartonSearchRecord + CartonListing: the cartons. @status is the first letter of "M - Medix", "S - Store" or "R - Return".
CREATE OR ALTER PROCEDURE genisis.StorageEnquiry_CartonSearch
    @cartonFrom nvarchar(40) = NULL, @cartonTo nvarchar(40) = NULL, @status char(1) = NULL,
    @sendFrom datetime = NULL, @sendTo datetime = NULL, @requestFrom datetime = NULL, @requestTo datetime = NULL,
    @deliveryFrom datetime = NULL, @deliveryTo datetime = NULL, @returnFrom datetime = NULL, @returnTo datetime = NULL,
    @destroyFrom datetime = NULL, @destroyTo datetime = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(CRTCartonNo) AS CartonNo, RTRIM(CRTCartonStatus) AS Status, LastUpdateDate AS RegisterDate, DOSend AS SendDate, DORequest AS RequestDate,
        DODelivery AS DeliveryDate, DOReturn AS ReturnDate, DODestroy AS DestroyDate, RTRIM(CRTRemarks) AS Remarks
    FROM genisis.WS_Carton
    WHERE (@cartonFrom IS NULL OR CRTCartonNo >= @cartonFrom) AND (@cartonTo IS NULL OR CRTCartonNo <= @cartonTo)
      AND (@status IS NULL OR CRTCartonStatus = @status)
      AND (@sendFrom IS NULL OR DOSend >= @sendFrom) AND (@sendTo IS NULL OR DOSend <= @sendTo)
      AND (@requestFrom IS NULL OR DORequest >= @requestFrom) AND (@requestTo IS NULL OR DORequest <= @requestTo)
      AND (@deliveryFrom IS NULL OR DODelivery >= @deliveryFrom) AND (@deliveryTo IS NULL OR DODelivery <= @deliveryTo)
      AND (@returnFrom IS NULL OR DOReturn >= @returnFrom) AND (@returnTo IS NULL OR DOReturn <= @returnTo)
      AND (@destroyFrom IS NULL OR DODestroy >= @destroyFrom) AND (@destroyTo IS NULL OR DODestroy <= @destroyTo)
    ORDER BY CRTCartonNo;
END
GO

-- StorageSearchRecord + StorageListing: the files that are in a carton (the desktop only listed rows with a carton number). @scan = 1: one case number; 0: a range. @fileStatus is the first
-- letter of "I - In" / "O - Out". @registerFrom/To filter the date the file was registered (LastUpdateDate), @sentFrom/To the date the carton was sent; the payor range compares the first two
-- letters of the case number. The view repeats a claim once per invoice payee; the desktop listed every repeat, here a file is one row. Duration = days from delivery to return.
CREATE OR ALTER PROCEDURE genisis.StorageEnquiry_StorageSearch
    @scan bit, @caseFrom nvarchar(30) = NULL, @caseTo nvarchar(30) = NULL, @cartonFrom nvarchar(40) = NULL, @cartonTo nvarchar(40) = NULL, @fileStatus char(1) = NULL,
    @requestBy nvarchar(100) = NULL, @requestFrom datetime = NULL, @requestTo datetime = NULL, @deliveryFrom datetime = NULL, @deliveryTo datetime = NULL,
    @returnFrom datetime = NULL, @returnTo datetime = NULL, @registerFrom datetime = NULL, @registerTo datetime = NULL, @sentFrom datetime = NULL, @sentTo datetime = NULL,
    @payorFrom nvarchar(4) = NULL, @payorTo nvarchar(4) = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@max) RTRIM(CASNumber) AS CaseNo, ClaimVersion, RTRIM(STRCartonNo) AS CartonNo, RTRIM(CRTCartonStatus) AS CartonStatus, RTRIM(STRFileStatus) AS FileStatus,
        MAX(CLMTotalActual) AS BillAmount, DOSend AS SendDate, LastUpdateDate AS RegisterDate, RTRIM(STRRequestBy) AS RequestBy, DORequest AS RequestDate, DODelivery AS DeliveryDate,
        DOReturn AS ReturnDate, CASE WHEN DOReturn IS NOT NULL AND DODelivery IS NOT NULL THEN DATEDIFF(day, DODelivery, DOReturn) END AS Duration, RTRIM(STRRemarks) AS Remarks
    FROM genisis.WS_StorageClaim
    WHERE STRCartonNo IS NOT NULL
      AND (@caseFrom IS NULL OR (@scan = 1 AND CASNumber = @caseFrom) OR (@scan = 0 AND CASNumber >= @caseFrom))
      AND (@scan = 1 OR @caseTo IS NULL OR CASNumber <= @caseTo)
      AND (@cartonFrom IS NULL OR STRCartonNo >= @cartonFrom) AND (@cartonTo IS NULL OR STRCartonNo <= @cartonTo)
      AND (@fileStatus IS NULL OR STRFileStatus = @fileStatus) AND (@requestBy IS NULL OR STRRequestBy = @requestBy)
      AND (@requestFrom IS NULL OR DORequest >= @requestFrom) AND (@requestTo IS NULL OR DORequest <= @requestTo)
      AND (@deliveryFrom IS NULL OR DODelivery >= @deliveryFrom) AND (@deliveryTo IS NULL OR DODelivery <= @deliveryTo)
      AND (@returnFrom IS NULL OR DOReturn >= @returnFrom) AND (@returnTo IS NULL OR DOReturn <= @returnTo)
      AND (@registerFrom IS NULL OR LastUpdateDate >= @registerFrom) AND (@registerTo IS NULL OR LastUpdateDate <= @registerTo)
      AND (@sentFrom IS NULL OR DOSend >= @sentFrom) AND (@sentTo IS NULL OR DOSend <= @sentTo)
      AND (@payorFrom IS NULL OR SUBSTRING(CASNumber, 1, 2) >= @payorFrom) AND (@payorTo IS NULL OR SUBSTRING(CASNumber, 1, 2) <= @payorTo)
    GROUP BY CASNumber, ClaimVersion, STRCartonNo, CRTCartonStatus, STRFileStatus, STRRequestBy, DORequest, DODelivery, DOReturn, STRRemarks, LastUpdateDate, DOSend
    ORDER BY CASNumber, ClaimVersion;
END
GO

-- CmdListing_Click: the check sheet of one carton: its files (case-version) and their file status, as the desktop wrote them into StorageTracking.xlt.
CREATE OR ALTER PROCEDURE genisis.StorageEnquiry_CartonFiles
    @carton nvarchar(40)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(STRCaseNo) AS CaseNo, STRCLMVersion AS ClaimVersion, RTRIM(STRFileStatus) AS FileStatus FROM genisis.WS_Storage WHERE STRCartonNo = @carton ORDER BY STRCaseNo, STRCLMVersion;
END
GO
