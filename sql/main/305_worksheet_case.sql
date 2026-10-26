-- The case's member, patient and plan as the worksheet engine needs them. It is what VIEW_Claim_MBM_Case_worksheetAdjustment gives for the case's columns, joined the same way (the member must have an
-- MBMOthersTwo row), but starting from the case and not from a saved worksheet version, so a worksheet that is not saved yet can be worked out. The columns are the ones the engine procedures read.
CREATE OR ALTER FUNCTION genisis.WorksheetCase (@cas nvarchar(15))
RETURNS TABLE
AS
RETURN
(
    SELECT cs.CASNumber, cs.MBMNumber, cs.MBMPatientCovID, cs.CASDateAdmitted, cs.CASClaimability, cs.CASHOSCode,
           m.PLNCode, m.INSCode, m.HLTCode, m.PAYCode, m.MBMMemberType, m.MBMPayorEffDate, m.MBMPayorExpDate,
           cp.MBMCPLNCode, o.MBMNewPlanCode, o.MBMNewPolEffDate
    FROM dbo.CAS cs
    JOIN dbo.MBM m ON m.MBMNumber = cs.MBMNumber
    JOIN dbo.MBMOthersTwo o2 ON o2.MBMNumber = m.MBMNumber
    LEFT JOIN dbo.MBMOthers o ON o.MBMNumber = m.MBMNumber
    LEFT JOIN dbo.MBMCoveredPersons cp ON cp.MBMCNumber = cs.MBMNumber AND cp.MBMCCoverID = cs.MBMPatientCovID
    WHERE cs.CASNumber = @cas
)
GO
