-- Case > Panel Hospitals (legacy frmPanelHospital). Replaces the raw @strAppend dbo.Search_PanelHospitals2.

-- PayorComboListing / SearchPayor: the payors of the payor drop-downs (Inpatient Case Listing, Claim ASCII, Case ASCII).
CREATE OR ALTER PROCEDURE genisis.Case_Payors
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name FROM dbo.PAY ORDER BY PAYCode;
END
GO

-- Form_Load: the hospital panel codes (HosPanelCode on the maintenance database).
CREATE OR ALTER PROCEDURE genisis.PanelHospitals_Codes
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(HosPanelCode) AS Code, RTRIM(HosPanelDesc) AS Description
    FROM genisis.Maint_HosPanelCode ORDER BY HosPanelCode;
END
GO

-- PanelListing: the hospitals of one panel code.
CREATE OR ALTER PROCEDURE genisis.PanelHospitals_List
    @code nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(h.HOSCode) AS Code, RTRIM(h.HOSName) AS Name, RTRIM(h.HOSTelNo) AS Tel, RTRIM(h.HOSFaxNo) AS Fax,
        RTRIM(h.HOSAdd1) AS Add1, RTRIM(h.HOSAdd2) AS Add2, RTRIM(h.HOSAdd3) AS Add3,
        RTRIM(h.HOSState) AS State, RTRIM(h.HOSTown) AS Town
    FROM dbo.HOS h
    INNER JOIN dbo.PanelHospitals p ON h.HOSCode = p.HOSCode
    WHERE p.HosPanelCode = @code
    ORDER BY h.HOSName, h.HOSCode;
END
GO
