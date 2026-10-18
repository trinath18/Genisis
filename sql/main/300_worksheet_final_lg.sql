-- Worksheet > Worksheet Adjustment (legacy frmWorksheetAdj2013), step 3: Final LG (cmdFinalLG_Click). The guarantee letter itself is issued by the same code as Case
-- Adjustment's (CASLG, LG number, notification); what the worksheet adds, in the same transaction, is this: the invoices of the version carry the LG number
-- (UpdateInvoiceLG) and the case is marked "FINAL GL Issued" with its worksheet done (tblRecord IsLGissued, IsWSdone, CaseProgress).
CREATE OR ALTER PROCEDURE genisis.WorksheetFinalLg_Mark
    @cas nvarchar(30), @version int, @lgNumber nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE genisis.WS_ClaimInvoice SET LGReference = @lgNumber
    WHERE CASNumber = @cas AND INVWSversion = CAST(@version AS nvarchar(4));
    UPDATE genisis.Maint_tblRecord SET IsLGissued = '1', IsWSdone = '1', CaseProgress = N'FINAL GL Issued' WHERE CaseNo = @cas;
END
GO
