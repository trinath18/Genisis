-- Authorize Claims > DCU Tracking > Claim Report (legacy FrmDCUReport1 "DCU Reports" and FrmDCUReport2 "DCU Reports (Jupiter)", the same form pointed at the other server). The desktop sent a WHERE fragment built as
-- text (SearchRecords) and a case number to HISWorkSheet.dbo.DCUReport, which held the 120 queries, and then asked more questions for each row of the list (the ICD description, the duration of stay of each case, the
-- case's bordx amounts, the ageing counts of the efficiency reports). Here the criteria are one XML document read with typed values (this database is at compatibility level 100, which has no JSON functions) and quoted into the fragment in one place (genisis.DcuReport_Where), the same
-- queries are run by genisis.DcuReport_Run (the select, group by and view of each case are copied from the legacy procedure; the views are reached through synonyms), the lists are ordered, and the per-row questions are
-- answered once for the whole list (genisis.DcuReport_IcdDescriptions, genisis.DcuReport_CaseBordx, the duration cases and genisis.DcuReport_Efficiency).

-- The legacy VIEW_CLMReportDCUNew selects VIEW_CLMReportCasDCU.MBMAvailableLimit, a column that view does not have in this database, so it cannot be used (binding error); no report reads that column. The same rows,
-- without it:
CREATE OR ALTER VIEW genisis.DcuClaimNew
AS
SELECT DISTINCT
    k.MBMNumber,
    k.MBMPatientCovID,
    k.CASStatus,
    k.MBMIcBcPp,
    k.PatientName,
    k.ICNo,
    k.Sex,
    k.DOB,
    k.Ageband,
    k.RACCode,
    k.CASDischargeDate,
    k.CaseRegDate,
    k.CASDateAdmitted,
    k.ClaimNatureCode,
    k.CASPending,
    k.CASReserveAmount,
    k.CASClaimability,
    k.MBMCMemberType,
    k.MBMCName,
    k.MBMCDOB,
    k.AGECode,
    k.MBMPayorExpDate,
    k.NoofDay,
    k.MBMPayorEffDate,
    k.MBMSex,
    k.MBMDOB,
    k.PAYCode,
    k.MBMName,
    k.MBMPolicyNo,
    k.MBMCAgeband,
    k.MBMCSex,
    k.MBMAgentCode,
    k.MBMGroupCompany,
    k.MBMStatus,
    k.MBMDataStatus,
    k.HLTCode,
    k.MBMMemberType,
    k.PLNCode,
    k.INSCode,
    k.FinalDiag,
    k.FirstDiag,
    k.HOSName,
    k.MBMPremium,
    k.MBMBasicPrem,
    k.MBMFirstJoinedDate,
    c.CASNumber,
    c.ClaimVersion,
    c.BordxRefNo,
    c.BordxDate,
    c.ClaimOpenDate,
    c.CLMTotalApproved,
    c.CLMTotalActual,
    c.BordxClaimAmt,
    c.CLMPending,
    c.RecoveryAmt,
    c.CLMPayableByMedix2M,
    k.CASStayType,
    k.CASPAYType,
    k.ModalityCode,
    k.STACode,
    k.CASOPROCRequired5,
    k.CASOPROCRequired4,
    k.CASOPROCRequired3,
    k.CASOPROCRequired2,
    k.CASCptCode4,
    k.CASCptCode5,
    k.CASOPROCRequired1,
    k.CASCptCode1,
    k.CASCptCode2,
    k.CASCptCode3,
    c.INVEnteredDate,
    c.CLMINVRecDate,
    c.DIRDay,
    c.INVDay,
    c.BordxDay
FROM genisis.WS_ClmReportClaimDcu2 c LEFT OUTER JOIN genisis.WS_ClmReportCasDcu k ON c.CASNumber = k.CASNumber;
GO

-- A text value as an N'...' literal (the quote doubled).
CREATE OR ALTER FUNCTION genisis.DcuQuote (@s nvarchar(4000))
RETURNS nvarchar(max)
AS
BEGIN
    RETURN N'N''' + REPLACE(@s, N'''', N'''''') + N'''';
END
GO

-- SearchRecords: the WHERE fragment of the criteria (XML, <c><name>value</name>...</c>, camelCase element names; a missing or empty element is no criterion). Dates are yyyy-mm-dd and are written as yyyymmdd. Every text goes through genisis.DcuQuote, the
-- version numbers are read as integers.
CREATE OR ALTER FUNCTION genisis.DcuReport_Where (@c xml)
RETURNS nvarchar(max)
AS
BEGIN
    DECLARE @w nvarchar(max) = N'', @v nvarchar(4000), @v2 nvarchar(4000), @d date, @i int;

    SET @v = NULLIF(LTRIM(RTRIM(@c.value('(/c/healthType)[1]', 'nvarchar(4000)'))), N''); IF @v IS NOT NULL SET @w += N' AND HLTCode = ' + genisis.DcuQuote(@v);
    SET @v = NULLIF(LTRIM(RTRIM(@c.value('(/c/insType)[1]', 'nvarchar(4000)'))), N''); IF @v IS NOT NULL SET @w += N' AND INSCode = ' + genisis.DcuQuote(@v);

    -- member type (P principal, S supplementary; none = both) and supplementary type (C combined limit, S separated limit; none = both)
    SET @v = NULLIF(LTRIM(RTRIM(@c.value('(/c/memberType)[1]', 'nvarchar(4000)'))), N''); SET @v2 = NULLIF(LTRIM(RTRIM(@c.value('(/c/supplementaryType)[1]', 'nvarchar(4000)'))), N'');
    IF @v = N'P' AND @v2 IS NULL SET @w += N' AND (MBMMemberType = ''P'' Or MBMMemberType = ''Q'') And MBMPatientCovID = ''00'' ';
    ELSE IF @v = N'P' AND @v2 = N'C' SET @w += N' AND MBMMemberType = ''P''  And MBMPatientCovID = ''00''';
    ELSE IF @v = N'P' AND @v2 = N'S' SET @w += N' AND MBMMemberType = ''Q'' And MBMPatientCovID = ''00''';
    ELSE IF @v = N'S' AND @v2 IS NULL SET @w += N' AND (MBMCMemberType = ''S'' OR MBMCMemberType = ''C'')';
    ELSE IF @v = N'S' AND @v2 = N'C' SET @w += N' AND MBMCMemberType = ''C''';
    ELSE IF @v = N'S' AND @v2 = N'S' SET @w += N' AND MBMCMemberType = ''S''';
    ELSE IF @v IS NULL AND @v2 = N'C' SET @w += N' AND (MBMMemberType = ''P'' OR MBMCMemberType = ''C'')';
    ELSE IF @v IS NULL AND @v2 = N'S' SET @w += N' AND (MBMMemberType = ''Q'' OR MBMCMemberType = ''S'')';

    SET @v = NULLIF(LTRIM(RTRIM(@c.value('(/c/dataStatus)[1]', 'nvarchar(4000)'))), N''); IF @v IS NOT NULL SET @w += N' AND MBMDataStatus = ' + genisis.DcuQuote(@v);
    SET @v = NULLIF(LTRIM(RTRIM(@c.value('(/c/claimability)[1]', 'nvarchar(4000)'))), N''); IF @v IS NOT NULL SET @w += N' AND CASClaimability = ' + genisis.DcuQuote(@v);
    SET @v = NULLIF(LTRIM(RTRIM(@c.value('(/c/policyStatus)[1]', 'nvarchar(4000)'))), N''); IF @v IS NOT NULL SET @w += N' AND MBMStatus = ' + genisis.DcuQuote(@v);

    -- case status: 0 open, 1 open and closed, 2 deleted, 3 all
    SET @i = @c.value('(/c/caseStatus)[1]', 'int');
    IF @i = 0 SET @w += N' AND CASStatus = ''O''';
    ELSE IF @i = 1 SET @w += N' AND (CASStatus = ''O'' OR CASStatus = ''C'')';
    ELSE IF @i = 2 SET @w += N' AND CASStatus = ''D''';

    SET @v = NULLIF(LTRIM(RTRIM(@c.value('(/c/agentCode)[1]', 'nvarchar(4000)'))), N''); IF @v IS NOT NULL SET @w += N' And  MBMAgentCode like ' + genisis.DcuQuote(N'%' + @v + N'%') + N' ';
    SET @v = NULLIF(LTRIM(RTRIM(@c.value('(/c/policyNo)[1]', 'nvarchar(4000)'))), N''); IF @v IS NOT NULL SET @w += N' And  MBMPolicyNo like ' + genisis.DcuQuote(N'%' + @v + N'%') + N' ';
    SET @v = NULLIF(LTRIM(RTRIM(@c.value('(/c/payor)[1]', 'nvarchar(4000)'))), N''); IF @v IS NOT NULL SET @w += N' AND PAYCode = ' + genisis.DcuQuote(@v);
    SET @v = NULLIF(LTRIM(RTRIM(@c.value('(/c/groupCompany)[1]', 'nvarchar(4000)'))), N''); IF @v IS NOT NULL SET @w += N' AND MBMGroupCompany = ' + genisis.DcuQuote(@v);

    -- case pending (N no, Y yes) and claim pending (B bordxed, C cancelled, P pending)
    SET @v = NULLIF(LTRIM(RTRIM(@c.value('(/c/casePending)[1]', 'nvarchar(4000)'))), N'');
    IF @v = N'N' SET @w += N' AND (CASPending = ''N'' OR CASPending = '''' OR CASPending IS NULL)';
    ELSE IF @v = N'Y' SET @w += N' AND CASPending = ''Y''';
    SET @v = NULLIF(LTRIM(RTRIM(@c.value('(/c/claimPending)[1]', 'nvarchar(4000)'))), N''); IF @v IN (N'B', N'C', N'P') SET @w += N' AND CLMPending = ' + genisis.DcuQuote(@v);

    -- dates
    SET @d = @c.value('(/c/doaFrom)[1]', 'date'); IF @d IS NOT NULL SET @w += N' AND CASDateAdmitted >= ' + genisis.DcuQuote(CONVERT(nvarchar(8), @d, 112));
    SET @d = @c.value('(/c/doaTo)[1]', 'date'); IF @d IS NOT NULL SET @w += N' AND CASDateAdmitted <= ' + genisis.DcuQuote(CONVERT(nvarchar(8), @d, 112));
    SET @d = @c.value('(/c/dodFrom)[1]', 'date'); IF @d IS NOT NULL SET @w += N' AND CASDischargeDate >= ' + genisis.DcuQuote(CONVERT(nvarchar(8), @d, 112));
    SET @d = @c.value('(/c/dodTo)[1]', 'date'); IF @d IS NOT NULL SET @w += N' AND CASDischargeDate <= ' + genisis.DcuQuote(CONVERT(nvarchar(8), @d, 112));
    SET @d = @c.value('(/c/dorFrom)[1]', 'date'); IF @d IS NOT NULL SET @w += N' AND CaseRegDate >= ' + genisis.DcuQuote(CONVERT(nvarchar(8), @d, 112));
    SET @d = @c.value('(/c/dorTo)[1]', 'date'); IF @d IS NOT NULL SET @w += N' AND CaseRegDate <= ' + genisis.DcuQuote(CONVERT(nvarchar(8), @d, 112));
    SET @d = @c.value('(/c/payorEffFrom)[1]', 'date'); IF @d IS NOT NULL SET @w += N' AND MBMPayorEffDate >= ' + genisis.DcuQuote(CONVERT(nvarchar(8), @d, 112));
    SET @d = @c.value('(/c/payorEffTo)[1]', 'date'); IF @d IS NOT NULL SET @w += N' AND MBMPayorEffDate <= ' + genisis.DcuQuote(CONVERT(nvarchar(8), @d, 112));
    SET @d = @c.value('(/c/payorExpFrom)[1]', 'date'); IF @d IS NOT NULL SET @w += N' AND MBMPayorExpDate >= ' + genisis.DcuQuote(CONVERT(nvarchar(8), @d, 112));
    SET @d = @c.value('(/c/payorExpTo)[1]', 'date'); IF @d IS NOT NULL SET @w += N' AND MBMPayorExpDate <= ' + genisis.DcuQuote(CONVERT(nvarchar(8), @d, 112));
    SET @d = @c.value('(/c/joinFrom)[1]', 'date'); IF @d IS NOT NULL SET @w += N' AND MBMFirstJoinedDate >= ' + genisis.DcuQuote(CONVERT(nvarchar(8), @d, 112));
    SET @d = @c.value('(/c/joinTo)[1]', 'date'); IF @d IS NOT NULL SET @w += N' AND MBMFirstJoinedDate <= ' + genisis.DcuQuote(CONVERT(nvarchar(8), @d, 112));
    SET @d = @c.value('(/c/bordxDateFrom)[1]', 'date'); IF @d IS NOT NULL SET @w += N' AND BordxDate >= ' + genisis.DcuQuote(CONVERT(nvarchar(8), @d, 112));
    SET @d = @c.value('(/c/bordxDateTo)[1]', 'date'); IF @d IS NOT NULL SET @w += N' AND BordxDate <= ' + genisis.DcuQuote(CONVERT(nvarchar(8), @d, 112));

    -- ranges of text
    SET @v = NULLIF(LTRIM(RTRIM(@c.value('(/c/finalDiagFrom)[1]', 'nvarchar(4000)'))), N''); IF @v IS NOT NULL SET @w += N' AND FinalDiag >= ' + genisis.DcuQuote(@v);
    SET @v = NULLIF(LTRIM(RTRIM(@c.value('(/c/finalDiagTo)[1]', 'nvarchar(4000)'))), N''); IF @v IS NOT NULL SET @w += N' AND FinalDiag <= ' + genisis.DcuQuote(@v);
    SET @v = NULLIF(LTRIM(RTRIM(@c.value('(/c/planFrom)[1]', 'nvarchar(4000)'))), N''); IF @v IS NOT NULL SET @w += N' AND PLNCode >= ' + genisis.DcuQuote(@v);
    SET @v = NULLIF(LTRIM(RTRIM(@c.value('(/c/planTo)[1]', 'nvarchar(4000)'))), N''); IF @v IS NOT NULL SET @w += N' AND PLNCode <= ' + genisis.DcuQuote(@v);
    SET @v = NULLIF(LTRIM(RTRIM(@c.value('(/c/bordxNoFrom)[1]', 'nvarchar(4000)'))), N''); IF @v IS NOT NULL SET @w += N' AND BordxRefNo >= ' + genisis.DcuQuote(@v);
    SET @v = NULLIF(LTRIM(RTRIM(@c.value('(/c/bordxNoTo)[1]', 'nvarchar(4000)'))), N''); IF @v IS NOT NULL SET @w += N' AND BordxRefNo <= ' + genisis.DcuQuote(@v);
    SET @v = NULLIF(LTRIM(RTRIM(@c.value('(/c/caseNoFrom)[1]', 'nvarchar(4000)'))), N''); IF @v IS NOT NULL SET @w += N' AND CASNumber >= ' + genisis.DcuQuote(@v);
    SET @v = NULLIF(LTRIM(RTRIM(@c.value('(/c/caseNoTo)[1]', 'nvarchar(4000)'))), N''); IF @v IS NOT NULL SET @w += N' AND CASNumber <= ' + genisis.DcuQuote(@v);

    SET @i = @c.value('(/c/versionFrom)[1]', 'int'); IF @i IS NOT NULL SET @w += N' AND ClaimVersion >= ' + CONVERT(nvarchar(11), @i);
    SET @i = @c.value('(/c/versionTo)[1]', 'int'); IF @i IS NOT NULL SET @w += N' AND ClaimVersion <= ' + CONVERT(nvarchar(11), @i);

    -- the "empty" boxes: rows where the field has no value
    IF @c.value('(/c/blankHealthType)[1]', 'nvarchar(10)') = N'true' SET @w += N' AND (HLTCode Is null OR HLTCode = '''')';
    IF @c.value('(/c/blankInsType)[1]', 'nvarchar(10)') = N'true' SET @w += N' AND (INSCode Is null OR INSCode = '''')';
    IF @c.value('(/c/blankDataStatus)[1]', 'nvarchar(10)') = N'true' SET @w += N' AND (MBMDataStatus Is null OR MBMDataStatus = '''')';
    IF @c.value('(/c/blankClaimability)[1]', 'nvarchar(10)') = N'true' SET @w += N' AND (CASClaimability Is null OR CASClaimability = '''')';
    IF @c.value('(/c/blankPolicyStatus)[1]', 'nvarchar(10)') = N'true' SET @w += N' AND (MBMStatus Is null OR MBMStatus = '''')';
    IF @c.value('(/c/blankCaseStatus)[1]', 'nvarchar(10)') = N'true' SET @w += N' AND (CASStatus Is null OR CASStatus = '''')';
    IF @c.value('(/c/blankAgentCode)[1]', 'nvarchar(10)') = N'true' SET @w += N' AND (MBMAgentCode Is null OR MBMAgentCode = '''')';
    IF @c.value('(/c/blankPolicyNo)[1]', 'nvarchar(10)') = N'true' SET @w += N' AND (MBMPolicyNo Is null OR MBMPolicyNo = '''')';
    IF @c.value('(/c/blankPayor)[1]', 'nvarchar(10)') = N'true' SET @w += N' AND (PAYCode Is null OR PAYCode = '''')';
    IF @c.value('(/c/blankGroupCompany)[1]', 'nvarchar(10)') = N'true' SET @w += N' AND (MBMGroupCompany Is null OR MBMGroupCompany = '''')';
    IF @c.value('(/c/blankDoa)[1]', 'nvarchar(10)') = N'true' SET @w += N' AND (CASDateAdmitted Is null OR CASDateAdmitted = '''')';
    IF @c.value('(/c/blankDod)[1]', 'nvarchar(10)') = N'true' SET @w += N' AND (CASDischargeDate Is null OR CASDischargeDate = '''')';
    IF @c.value('(/c/blankDor)[1]', 'nvarchar(10)') = N'true' SET @w += N' AND (CaseRegDate Is null OR CaseRegDate = '''')';
    IF @c.value('(/c/blankFinalDiag)[1]', 'nvarchar(10)') = N'true' SET @w += N' AND (FinalDiag Is null OR FinalDiag = '''')';
    IF @c.value('(/c/blankPlan)[1]', 'nvarchar(10)') = N'true' SET @w += N' AND (PLNCode Is null OR PLNCode = '''')';
    IF @c.value('(/c/blankBordxNo)[1]', 'nvarchar(10)') = N'true' SET @w += N' AND (BordxRefNo Is null OR BordxRefNo = '''')';
    IF @c.value('(/c/blankPayorEff)[1]', 'nvarchar(10)') = N'true' SET @w += N' AND (MBMPayorEffDate Is null OR MBMPayorEffDate = '''')';
    IF @c.value('(/c/blankPayorExp)[1]', 'nvarchar(10)') = N'true' SET @w += N' AND (MBMPayorExpDate Is null OR MBMPayorExpDate = '''')';
    IF @c.value('(/c/blankJoin)[1]', 'nvarchar(10)') = N'true' SET @w += N' AND (MBMFirstJoinedDate Is null OR MBMFirstJoinedDate = '''')';
    IF @c.value('(/c/blankBordxDate)[1]', 'nvarchar(10)') = N'true' SET @w += N' AND (BordxDate Is null OR BordxDate = '''')';

    SET @v = NULLIF(LTRIM(RTRIM(@c.value('(/c/stayType)[1]', 'nvarchar(4000)'))), N''); IF @v IS NOT NULL SET @w += N' And CASStayType = ' + genisis.DcuQuote(@v);
    SET @v = NULLIF(LTRIM(RTRIM(@c.value('(/c/cashType)[1]', 'nvarchar(4000)'))), N''); IF @v IS NOT NULL SET @w += N' And CASPayType = ' + genisis.DcuQuote(@v);
    SET @v = NULLIF(LTRIM(RTRIM(@c.value('(/c/modality)[1]', 'nvarchar(4000)'))), N''); IF @v IS NOT NULL SET @w += N' And ModalityCode = ' + genisis.DcuQuote(@v);
    RETURN @w;
END
GO

-- DCUReport: the query of one case, ordered and cut at @max rows. @orderBy picks the measure of a ranked list (Total, TotalApproved or TotalCases; the "Top 20 by Amount" lists use TotalApproved). The queries are the
-- legacy procedure's. The legacy Top N queries had no ORDER BY, so they gave any N groups; a Top N list is the N highest of its measure here, and the other lists are ordered by their groups.
CREATE OR ALTER PROCEDURE genisis.DcuReport_Run
    @case int, @criteria xml, @orderBy nvarchar(20) = NULL, @max int = 5001
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @select nvarchar(max), @group nvarchar(max), @order nvarchar(max), @top int, @rank nvarchar(20);
    SELECT @select = d.sel, @group = d.grp, @order = d.ord, @top = d.topN, @rank = d.rnk
    FROM (VALUES
        (1, N'PAYCode, CASNumber, claimversion, MBMNumber, MBMPolicyNo, MBMPatientCovID, MBMName, MBMDOB, DOB, PatientName, MBMSex, AgeCode, Sex, Ageband, InsCode, PLNCode, HOSName, FirstDiag, FinalDiag, CLMTotalActual, CLMTotalApproved, CASReserveAmount, RecoveryAmt, BordxRefNo, BordxDate, ClaimOpenDate, CASClaimability, CASStatus, MBMStatus, CASDateAdmitted, CASDischargeDate, MBMPayorEffDate, MBMPayorExpDate, MBMPremium, NoofDay, CaseRegDate, MBMIcBcPp, ICNo, CASCptCode1, CASCptCode2, CASCptCode3, CASCptCode4, CASCptCode5, CASOPROCRequired1, CASOPROCRequired2, CASOPROCRequired3, CASOPROCRequired4, CASOPROCRequired5 From genisis.WS_ClmReportDcu Where 0 = 0', N'', N'ORDER BY CASNumber, ClaimVersion', NULL, NULL),
        (2, N'PAYCode, CASNumber, MBMNumber, MBMPolicyNo, MBMPatientCovID, MBMName, MBMDOB, DOB, PatientName, MBMSex, AgeCode, Sex, Ageband, InsCode, PLNCode, HOSName, FirstDiag, FinalDiag, CASClaimability, CASStatus, MBMStatus, SUM(CLMTotalActual) AS CLMTotalActual, SUM(CLMTotalApproved) AS CLMTotalApproved, SUM(CASReserveAmount) AS CASReserveAmount, SUM(RecoveryAmt) AS RecoveryAmt, CASDateAdmitted, CASDischargeDate, MBMPayorEffDate, MBMPayorExpDate, MBMPremium, NoofDay, CaseRegDate, MBMIcBcPp, ICNo, CASCptCode1, CASCptCode2, CASCptCode3, CASCptCode4, CASCptCode5, CASOPROCRequired1, CASOPROCRequired2, CASOPROCRequired3, CASOPROCRequired4, CASOPROCRequired5 From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, CASNumber, MBMNumber, MBMPolicyNo, MBMPatientCovID, MBMName, MBMDOB, DOB, PatientName, MBMSex, AgeCode, Sex, Ageband, InsCode, PLNCode, HOSName, FirstDiag, FinalDiag, CASClaimability, CASStatus, MBMStatus, CASDateAdmitted, CASDischargeDate, MBMPayorEffDate, MBMPayorExpDate, MBMPremium, NoofDay, CaseRegDate, MBMIcBcPp, ICNo, CASCptCode1, CASCptCode2, CASCptCode3, CASCptCode4, CASCptCode5, CASOPROCRequired1, CASOPROCRequired2, CASOPROCRequired3, CASOPROCRequired4, CASOPROCRequired5', N'ORDER BY PAYCode, CASNumber, MBMNumber, MBMPolicyNo, MBMPatientCovID, MBMName, MBMDOB, DOB, PatientName, MBMSex, AgeCode, Sex, Ageband, InsCode, PLNCode, HOSName, FirstDiag, FinalDiag, CASClaimability, CASStatus, MBMStatus, CASDateAdmitted, CASDischargeDate, MBMPayorEffDate, MBMPayorExpDate, MBMPremium, NoofDay, CaseRegDate, MBMIcBcPp, ICNo, CASCptCode1, CASCptCode2, CASCptCode3, CASCptCode4, CASCptCode5, CASOPROCRequired1, CASOPROCRequired2, CASOPROCRequired3, CASOPROCRequired4, CASOPROCRequired5', NULL, NULL),
        (4, N'PAYCode, INSCode, COUNT(CASNumber) as TotalCases, SUM(clmtotalactual) AS totalActual, SUM(clmtotalapproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, INSCode', N'ORDER BY PAYCode, INSCode', NULL, NULL),
        (5, N'PAYCode, INSCode, COUNT(DISTINCT CASNumber) as TotalCases, SUM(clmtotalactual) AS totalActual, SUM(clmtotalapproved) As TotalApproved From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, INSCode', N'ORDER BY PAYCode, INSCode', NULL, NULL),
        (6, N'PAYCode, INSCode, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY PAYCode, INSCode', N'ORDER BY PAYCode, INSCode', NULL, NULL),
        (7, N'PAYCode, PLNCode, INSCode, COUNT(CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActualAmt, SUM(CLMTotalApproved) AS TotalApprovedAmt, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, PLNCode, INSCode', N'ORDER BY PAYCode, PLNCode, INSCode', NULL, NULL),
        (8, N'PAYCode, PLNCode, INSCode, COUNT(DISTINCT CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActualAmt, SUM(CLMTotalApproved) AS TotalApprovedAmt From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, PLNCode, INSCode', N'ORDER BY PAYCode, PLNCode, INSCode', NULL, NULL),
        (9, N'PAYCode, PLNCode, INSCode, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY PAYCode, PLNCode, INSCode', N'ORDER BY PAYCode, PLNCode, INSCode', NULL, NULL),
        (10, N'PAYCode, INSCode, Ageband, COUNT(CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActualAmt, SUM(CLMTotalApproved) AS TotalApprovedAmt, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, INSCode, Ageband', N'ORDER BY PAYCode, INSCode, Ageband', NULL, NULL),
        (11, N'PAYCode, INSCode, Ageband, COUNT(DISTINCT CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActualAmt, SUM(CLMTotalApproved) AS TotalApprovedAmt From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, INSCode, Ageband', N'ORDER BY PAYCode, INSCode, Ageband', NULL, NULL),
        (12, N'PAYCode, INSCode, Ageband, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY PAYCode, INSCode, Ageband', N'ORDER BY PAYCode, INSCode, Ageband', NULL, NULL),
        (13, N'PAYCode, INSCode, Sex, COUNT(CASNumber) AS TotalCases, SUM(CLMTotalActual) AS totalActual, SUM(CLMTotalApproved) AS TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, INSCode, Sex', N'ORDER BY PAYCode, INSCode, Sex', NULL, NULL),
        (14, N'PAYCode, INSCode, Sex, COUNT(DISTINCT CASNumber) AS TotalCases, SUM(CLMTotalActual) AS totalActual, SUM(CLMTotalApproved) AS TotalApproved From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, INSCode, Sex', N'ORDER BY PAYCode, INSCode, Sex', NULL, NULL),
        (15, N'PAYCode, INSCode, Sex, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY PAYCode, INSCode, Sex', N'ORDER BY PAYCode, INSCode, Sex', NULL, NULL),
        (16, N'PAYCode, INSCode, RACCode, COUNT(CASNumber) AS TotalCases, SUM(CLMTotalActual) AS totalActual, SUM(CLMTotalApproved) AS TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, INSCode, RACCode', N'ORDER BY PAYCode, INSCode, RACCode', NULL, NULL),
        (17, N'PAYCode, INSCode, RACCode, COUNT(DISTINCT CASNumber) AS TotalCases, SUM(CLMTotalActual) AS totalActual, SUM(CLMTotalApproved) AS TotalApproved From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, INSCode, RACCode', N'ORDER BY PAYCode, INSCode, RACCode', NULL, NULL),
        (18, N'PAYCode, INSCode, RACCode, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY PAYCode, INSCode, RACCode', N'ORDER BY PAYCode, INSCode, RACCode', NULL, NULL),
        (19, N'PAYCode, PLNCode, COUNT(CASNumber) as TotalCases, SUM(clmtotalactual) AS totalActual, SUM(clmtotalapproved) As TotalApproved, SUM(NoofDay) AS NoofDay, SUM(MBMBasicPrem) AS BasicPremium, SUM(MBMPremium) AS Premium From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, PLNCode', N'ORDER BY PAYCode, PLNCode', NULL, NULL),
        (20, N'PAYCode, PLNCode, COUNT(DISTINCT CASNumber) as TotalCases, SUM(clmtotalactual) AS totalActual, SUM(clmtotalapproved) As TotalApproved, SUM(MBMBasicPrem) AS BasicPremium, SUM(MBMPremium) AS Premium From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, PLNCode', N'ORDER BY PAYCode, PLNCode', NULL, NULL),
        (21, N'PAYCode, PLNCode, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY PAYCode, PLNCode', N'ORDER BY PAYCode, PLNCode', NULL, NULL),
        (22, N'PAYCode, PLNCode, COUNT(CASNumber) as TotalCases, SUM(clmtotalactual) AS totalActual, SUM(clmtotalapproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, PLNCode', N'ORDER BY PAYCode, PLNCode', NULL, NULL),
        (23, N'PAYCode, PLNCode, COUNT(DISTINCT CASNumber) as TotalCases, SUM(clmtotalactual) AS totalActual, SUM(clmtotalapproved) As TotalApproved From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, PLNCode', N'ORDER BY PAYCode, PLNCode', NULL, NULL),
        (24, N'PAYCode, PLNCode, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY PAYCode, PLNCode', N'ORDER BY PAYCode, PLNCode', NULL, NULL),
        (25, N'PAYCode, PLNCode, HOSName, COUNT(CASNumber) as TotalCases, SUM(clmtotalactual) AS totalActual, SUM(clmtotalapproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, PLNCode, HOSName', N'PAYCode, PLNCode, HOSName', 10, N'TotalCases'),
        (26, N'PAYCode, PLNCode, HOSName, COUNT(DISTINCT CASNumber) as TotalCases, SUM(clmtotalactual) AS totalActual, SUM(clmtotalapproved) As TotalApproved From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, PLNCode, HOSName', N'PAYCode, PLNCode, HOSName', 10, N'TotalCases'),
        (27, N'PAYCode, PLNCode, HOSName, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY PAYCode, PLNCode, HOSName', N'ORDER BY PAYCode, PLNCode, HOSName', NULL, NULL),
        (28, N'PAYCode, PLNCode, FinalDiag, COUNT(CASNumber) as TotalCases, SUM(clmtotalactual) AS totalActual, SUM(clmtotalapproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, PLNCode, FinalDiag', N'PAYCode, PLNCode, FinalDiag', 10, N'TotalCases'),
        (29, N'PAYCode, PLNCode, FinalDiag, COUNT(DISTINCT CASNumber) as TotalCases, SUM(clmtotalactual) AS totalActual, SUM(clmtotalapproved) As TotalApproved From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, PLNCode, FinalDiag', N'PAYCode, PLNCode, FinalDiag', 10, N'TotalCases'),
        (30, N'PAYCode, PLNCode, FinalDiag, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY PAYCode, PLNCode, FinalDiag', N'ORDER BY PAYCode, PLNCode, FinalDiag', NULL, NULL),
        (31, N'PAYCode, PLNCode, Ageband, COUNT(CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActualAmt, SUM(CLMTotalApproved) AS TotalApprovedAmt, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, PLNCode, Ageband', N'ORDER BY PAYCode, PLNCode, Ageband', NULL, NULL),
        (32, N'PAYCode, PLNCode, Ageband, COUNT(DISTINCT CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActualAmt, SUM(CLMTotalApproved) AS TotalApprovedAmt From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, PLNCode, Ageband', N'ORDER BY PAYCode, PLNCode, Ageband', NULL, NULL),
        (33, N'PAYCode, PLNCode, AGEBand, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY PAYCode, PLNCode, AGEBand', N'ORDER BY PAYCode, PLNCode, AGEBand', NULL, NULL),
        (34, N'PAYCode, PLNCode, INSCode, Ageband, COUNT(CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActualAmt, SUM(CLMTotalApproved) AS TotalApprovedAmt, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, PLNCode, INSCode, Ageband', N'ORDER BY PAYCode, PLNCode, INSCode, Ageband', NULL, NULL),
        (37, N'Ageband, COUNT(CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActualAmt, SUM(CLMTotalApproved) AS TotalApprovedAmt, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY Ageband', N'ORDER BY Ageband', NULL, NULL),
        (38, N'Ageband, COUNT(DISTINCT CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActualAmt, SUM(CLMTotalApproved) AS TotalApprovedAmt From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY Ageband', N'ORDER BY Ageband', NULL, NULL),
        (39, N'Ageband, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY Ageband', N'ORDER BY Ageband', NULL, NULL),
        (40, N'Sex, Ageband, COUNT(CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActualAmt, SUM(CLMTotalApproved) AS TotalApprovedAmt, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY Sex, Ageband', N'ORDER BY Sex, Ageband', NULL, NULL),
        (41, N'Sex, Ageband, COUNT(DISTINCT CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActualAmt, SUM(CLMTotalApproved) AS TotalApprovedAmt From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY Sex, Ageband', N'ORDER BY Sex, Ageband', NULL, NULL),
        (42, N'Sex, Ageband, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY Sex, Ageband', N'ORDER BY Sex, Ageband', NULL, NULL),
        (43, N'RACCode, Ageband, COUNT(CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActualAmt, SUM(CLMTotalApproved) AS TotalApprovedAmt, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY RACCode, Ageband', N'ORDER BY RACCode, Ageband', NULL, NULL),
        (44, N'RACCode, Ageband, COUNT(DISTINCT CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActualAmt, SUM(CLMTotalApproved) AS TotalApprovedAmt From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY RACCode, Ageband', N'ORDER BY RACCode, Ageband', NULL, NULL),
        (45, N'RACCode, Ageband, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY RACCode, Ageband', N'ORDER BY RACCode, Ageband', NULL, NULL),
        (46, N'INSCode, PLNCode, Ageband, COUNT(CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActualAmt, SUM(CLMTotalApproved) AS TotalApprovedAmt, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY INSCode, PLNCode, Ageband', N'ORDER BY INSCode, PLNCode, Ageband', NULL, NULL),
        (47, N'INSCode, PLNCode, Ageband, COUNT(DISTINCT CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActualAmt, COUNT(CASNumber) AS TotalCases2, SUM(CLMTotalApproved) AS TotalApprovedAmt, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY INSCode, PLNCode, Ageband', N'ORDER BY INSCode, PLNCode, Ageband', NULL, NULL),
        (48, N'INSCode, PLNCode, Ageband, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY INSCode, PLNCode, Ageband', N'ORDER BY INSCode, PLNCode, Ageband', NULL, NULL),
        (49, N'ClaimNatureCode, Ageband, COUNT(CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActualAmt, SUM(CLMTotalApproved) AS TotalApprovedAmt, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY ClaimNatureCode, Ageband', N'ORDER BY ClaimNatureCode, Ageband', NULL, NULL),
        (52, N'PAYCode, COUNT(CASNumber) as TotalCases, SUM(clmtotalactual) AS totalActual, SUM(clmtotalapproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode', N'ORDER BY PAYCode', NULL, NULL),
        (53, N'PAYCode, COUNT(DISTINCT CASNumber) as TotalCases, SUM(clmtotalactual) AS totalActual, SUM(clmtotalapproved) As TotalApproved From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode', N'ORDER BY PAYCode', NULL, NULL),
        (54, N'PAYCode, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY PAYCode', N'ORDER BY PAYCode', NULL, NULL),
        (55, N'PAYCode, HOSName, COUNT(HOSName) AS Total, SUM(NoofDay) AS NoofDay, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) AS TotalApproved From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, HOSName', N'PAYCode, HOSName', 20, N'Total'),
        (56, N'PAYCode, HOSName, COUNT(DISTINCT CASNumber) AS Total, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) AS TotalApproved From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, HOSName', N'PAYCode, HOSName', 20, N'Total'),
        (57, N'HOSName, COUNT(HOSName) AS Total, SUM(NoofDay) AS NoofDay, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) AS TotalApproved From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY HOSName', N'HOSName', 20, N'Total'),
        (58, N'HOSName, COUNT(DISTINCT CASNumber) AS Total, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) AS TotalApproved From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY HOSName', N'HOSName', 20, N'Total'),
        (59, N'HOSName, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY HOSName', N'ORDER BY HOSName', NULL, NULL),
        (60, N'PAYCode, HOSName, COUNT(HOSName) AS Total, SUM(NoofDay) AS NoofDay, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) AS TotalApproved From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, HOSName', N'ORDER BY PAYCode, HOSName', NULL, NULL),
        (61, N'PAYCode, HOSName, COUNT(DISTINCT CASNumber) AS Total, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) AS TotalApproved From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, HOSName', N'ORDER BY PAYCode, HOSName', NULL, NULL),
        (62, N'PAYCode, HOSName, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY PAYCode, HOSName', N'ORDER BY PAYCode, HOSName', NULL, NULL),
        (63, N'HOSName, COUNT(HOSName) AS Total, SUM(NoofDay) AS NoofDay, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) AS TotalApproved From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY HOSName', N'ORDER BY HOSName', NULL, NULL),
        (64, N'HOSName, COUNT(DISTINCT CASNumber) AS Total, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) AS TotalApproved From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY HOSName', N'ORDER BY HOSName', NULL, NULL),
        (65, N'HOSName, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY HOSName', N'ORDER BY HOSName', NULL, NULL),
        (66, N'PAYCode, HOSName, CASNumber, ClaimVersion, MBMNumber, MBMPatientCovID, PatientName, CLMTotalActual, CLMTotalApproved, RecoveryAmt, CASDateAdmitted, CASDischargeDate, CASStatus, CASClaimability, FinalDiag From genisis.WS_ClmReportDcu Where 0 = 0', N'', N'ORDER BY PAYCode, HOSName, CASNumber, ClaimVersion', NULL, NULL),
        (67, N'PAYCode, HOSName, CASNumber, MBMNumber, MBMPatientCovID, PatientName, SUM(CLMTotalActual) AS CLMTotalActual, SUM(CLMTotalApproved) AS CLMTotalApproved, SUM(RecoveryAmt) AS RecoveryAmt, CASDateAdmitted, CASDischargeDate, CASStatus, CASClaimability, FinalDiag From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, HOSName, CASNumber, MBMNumber, MBMPatientCovID, PatientName, CASDateAdmitted, CASDischargeDate, CASStatus, CASClaimability, FinalDiag', N'ORDER BY PAYCode, HOSName, CASNumber, MBMNumber, MBMPatientCovID, PatientName, CASDateAdmitted, CASDischargeDate, CASStatus, CASClaimability, FinalDiag', NULL, NULL),
        (68, N'COUNT(CASNumber) AS Total, FinalDiag, Sum(CLMTotalApproved) As TotalApproved, Sum(CLMTotalActual) As TotalActual, HOSName, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY FinalDiag, HOSName', N'ORDER BY FinalDiag, HOSName', NULL, NULL),
        (71, N'COUNT(CASNumber) AS Total, FinalDiag, Sum(CLMTotalApproved) As TotalApproved, Sum(CLMTotalActual) As TotalActual, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY FinalDiag', N'FinalDiag', 20, N'Total'),
        (72, N'COUNT(DISTINCT CASNumber) AS Total, FinalDiag, Sum(CLMTotalApproved) As TotalApproved, Sum(CLMTotalActual) As TotalActual From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY FinalDiag', N'FinalDiag', 20, N'Total'),
        (73, N'Sum(CLMTotalApproved) As TotalApproved, COUNT(CASNumber) AS Total, FinalDiag, Sum(CLMTotalActual) As TotalActual, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY FinalDiag', N'FinalDiag', 20, N'TotalApproved'),
        (74, N'Sum(CLMTotalApproved) As TotalApproved, COUNT(DISTINCT CASNumber) AS Total, FinalDiag, Sum(CLMTotalActual) As TotalActual From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY FinalDiag', N'FinalDiag', 20, N'TotalApproved'),
        (75, N'COUNT(CASNumber) AS Total, FinalDiag, Sum(CLMTotalApproved) As TotalApproved, Sum(CLMTotalActual) As TotalActual, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY FinalDiag', N'FinalDiag', NULL, N'Total'),
        (76, N'COUNT(DISTINCT CASNumber) AS Total, FinalDiag, Sum(CLMTotalApproved) As TotalApproved, Sum(CLMTotalActual) As TotalActual From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY FinalDiag', N'FinalDiag', NULL, N'Total'),
        (77, N'FinalDiag, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY FinalDiag', N'ORDER BY FinalDiag', NULL, NULL),
        (78, N'COUNT(CASNumber) AS Total, FinalDiag, Sum(CLMTotalApproved) As TotalApproved, Sum(CLMTotalActual) As TotalActual, HOSName, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY FinalDiag, HOSName', N'ORDER BY FinalDiag, HOSName', NULL, NULL),
        (79, N'COUNT(DISTINCT CASNumber) AS Total, FinalDiag, Sum(CLMTotalApproved) As TotalApproved, Sum(CLMTotalActual) As TotalActual, HOSName From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY FinalDiag, HOSName', N'ORDER BY FinalDiag, HOSName', NULL, NULL),
        (80, N'FinalDiag, HOSName, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY FinalDiag, HOSName', N'ORDER BY FinalDiag, HOSName', NULL, NULL),
        (81, N'PAYCode, STACode, COUNT(CASNumber) as TotalCases, SUM(clmtotalactual) AS totalActual, SUM(clmtotalapproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, STACode', N'ORDER BY PAYCode, STACode', NULL, NULL),
        (82, N'PAYCode, STACode, COUNT(DISTINCT CASNumber) as TotalCases, SUM(clmtotalactual) AS totalActual, SUM(clmtotalapproved) As TotalApproved From genisis.WS_ClmReportDcu Where 0 = 0', N'GROUP BY PAYCode, STACode', N'ORDER BY PAYCode, STACode', NULL, NULL),
        (83, N'PAYCode, STACode, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY PAYCode, STACode', N'ORDER BY PAYCode, STACode', NULL, NULL),
        (84, N'MONTH(CaseRegDate) AS MONTH, YEAR(CaseRegDate) AS YEAR, COUNT(CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu where 0 = 0', N'GROUP BY MONTH(CaseRegDate), YEAR(CaseRegDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (85, N'MONTH(CaseRegDate) AS MONTH, YEAR(CaseRegDate) AS YEAR, COUNT(Distinct CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu where 0 = 0', N'GROUP BY MONTH(CaseRegDate), YEAR(CaseRegDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (86, N'MONTH(CaseRegDate) AS MONTH, YEAR(CaseRegDate) AS YEAR, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY MONTH(CaseRegDate), YEAR(CaseRegDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (87, N'PAYCode, MONTH(CaseRegDate) AS MONTH, YEAR(CaseRegDate) AS YEAR, COUNT(CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu where 0 = 0', N'GROUP BY PAYCode, MONTH(CaseRegDate), YEAR(CaseRegDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (88, N'PAYCode, MONTH(CaseRegDate) AS MONTH, YEAR(CaseRegDate) AS YEAR, COUNT(Distinct CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu where 0 = 0', N'GROUP BY PAYCode, MONTH(CaseRegDate), YEAR(CaseRegDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (89, N'PAYCode, MONTH(CaseRegDate) AS MONTH, YEAR(CaseRegDate) AS YEAR, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 =0', N'GROUP BY PAYCode, MONTH(CaseRegDate), YEAR(CaseRegDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (90, N'MONTH(MBMPayorEffDate) AS MONTH, YEAR(MBMPayorEffDate) AS YEAR, COUNT(CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu where 0 = 0', N'GROUP BY MONTH(MBMPayorEffDate), YEAR(MBMPayorEffDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (91, N'MONTH(MBMPayorEffDate) AS MONTH, YEAR(MBMPayorEffDate) AS YEAR, COUNT(Distinct CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu where 0 = 0', N'GROUP BY MONTH(MBMPayorEffDate), YEAR(MBMPayorEffDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (92, N'MONTH(MBMPayorEffDate) AS MONTH, YEAR(MBMPayorEffDate) AS YEAR, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY MONTH(MBMPayorEffDate), YEAR(MBMPayorEffDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (93, N'PAYCode, MONTH(MBMPayorEffDate) AS MONTH, YEAR(MBMPayorEffDate) AS YEAR, COUNT(CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu where 0 = 0', N'GROUP BY PAYCode, MONTH(MBMPayorEffDate), YEAR(MBMPayorEffDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (94, N'PAYCode, MONTH(MBMPayorEffDate) AS MONTH, YEAR(MBMPayorEffDate) AS YEAR, COUNT(Distinct CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu where 0 = 0', N'GROUP BY PAYCode, MONTH(MBMPayorEffDate), YEAR(MBMPayorEffDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (95, N'PAYCode, MONTH(MBMPayorEffDate) AS MONTH, YEAR(MBMPayorEffDate) AS YEAR, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY PAYCode, MONTH(MBMPayorEffDate), YEAR(MBMPayorEffDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (96, N'MONTH(MBMPayorExpDate) AS MONTH, YEAR(MBMPayorExpDate) AS YEAR, COUNT(CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu where 0 = 0', N'GROUP BY MONTH(MBMPayorExpDate), YEAR(MBMPayorExpDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (97, N'MONTH(MBMPayorExpDate) AS MONTH, YEAR(MBMPayorExpDate) AS YEAR, COUNT(Distinct CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu where 0 = 0', N'GROUP BY MONTH(MBMPayorExpDate), YEAR(MBMPayorExpDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (98, N'MONTH(MBMPayorExpDate) AS MONTH, YEAR(MBMPayorExpDate) AS YEAR, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY MONTH(MBMPayorExpDate), YEAR(MBMPayorExpDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (99, N'PAYCode, MONTH(MBMPayorExpDate) AS MONTH, YEAR(MBMPayorExpDate) AS YEAR, COUNT(CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu where 0 = 0', N'GROUP BY PAYCode, MONTH(MBMPayorExpDate), YEAR(MBMPayorExpDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (100, N'PAYCode, MONTH(MBMPayorExpDate) AS MONTH, YEAR(MBMPayorExpDate) AS YEAR, COUNT(Distinct CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu where 0 = 0', N'GROUP BY PAYCode, MONTH(MBMPayorExpDate), YEAR(MBMPayorExpDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (101, N'PAYCode, MONTH(MBMPayorExpDate) AS MONTH, YEAR(MBMPayorExpDate) AS YEAR, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY PAYCode, MONTH(MBMPayorExpDate), YEAR(MBMPayorExpDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (102, N'MONTH(CASDateAdmitted) AS MONTH, YEAR(CASDateAdmitted) AS YEAR, COUNT(CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu where 0 = 0', N'GROUP BY MONTH(CASDateAdmitted), YEAR(CASDateAdmitted)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (103, N'MONTH(CASDateAdmitted) AS MONTH, YEAR(CASDateAdmitted) AS YEAR, COUNT(Distinct CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu where 0 = 0', N'GROUP BY MONTH(CASDateAdmitted), YEAR(CASDateAdmitted)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (104, N'MONTH(CASDateAdmitted) AS MONTH, YEAR(CASDateAdmitted) AS YEAR, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY MONTH(CASDateAdmitted), YEAR(CASDateAdmitted)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (105, N'PAYCode, MONTH(CASDateAdmitted) AS MONTH, YEAR(CASDateAdmitted) AS YEAR, COUNT(CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu where 0 = 0', N'GROUP BY PAYCode, MONTH(CASDateAdmitted), YEAR(CASDateAdmitted)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (106, N'PAYCode, MONTH(CASDateAdmitted) AS MONTH, YEAR(CASDateAdmitted) AS YEAR, COUNT(Distinct CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu where 0 = 0', N'GROUP BY PAYCode, MONTH(CASDateAdmitted), YEAR(CASDateAdmitted)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (107, N'PAYCode, MONTH(CASDateAdmitted) AS MONTH, YEAR(CASDateAdmitted) AS YEAR, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY PAYCode, MONTH(CASDateAdmitted), YEAR(CASDateAdmitted)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (108, N'MONTH(CASDischargeDate) AS MONTH, YEAR(CASDischargeDate) AS YEAR, COUNT(CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu where 0 = 0', N'GROUP BY MONTH(CASDischargeDate), YEAR(CASDischargeDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (109, N'MONTH(CASDischargeDate) AS MONTH, YEAR(CASDischargeDate) AS YEAR, COUNT(Distinct CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu where 0 = 0', N'GROUP BY MONTH(CASDischargeDate), YEAR(CASDischargeDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (110, N'MONTH(CASDischargeDate) AS MONTH, YEAR(CASDischargeDate) AS YEAR, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY MONTH(CASDischargeDate), YEAR(CASDischargeDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (111, N'PAYCode, MONTH(CASDischargeDate) AS MONTH, YEAR(CASDischargeDate) AS YEAR, COUNT(CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu where 0 = 0', N'GROUP BY PAYCode, MONTH(CASDischargeDate), YEAR(CASDischargeDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (112, N'PAYCode, MONTH(CASDischargeDate) AS MONTH, YEAR(CASDischargeDate) AS YEAR, COUNT(Distinct CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu where 0 = 0', N'GROUP BY PAYCode, MONTH(CASDischargeDate), YEAR(CASDischargeDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (113, N'PAYCode, MONTH(CASDischargeDate) AS MONTH, YEAR(CASDischargeDate) AS YEAR, SUM(DurationStay) AS NoofDay From genisis.WS_ClmReportDcu2 Where 0 = 0', N'GROUP BY PAYCode, MONTH(CASDischargeDate), YEAR(CASDischargeDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (114, N'MONTH(BordxDate) AS MONTH, YEAR(BordxDate) AS YEAR, COUNT(CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu where 0 = 0', N'GROUP BY MONTH(BordxDate), YEAR(BordxDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (115, N'PAYCode, MONTH(BordxDate) AS MONTH, YEAR(BordxDate) AS YEAR, COUNT(CASNumber) AS TotalCases, SUM(CLMTotalActual) AS TotalActual, SUM(CLMTotalApproved) As TotalApproved, SUM(NoofDay) AS NoofDay From genisis.WS_ClmReportDcu where 0 = 0', N'GROUP BY PAYCode, MONTH(BordxDate), YEAR(BordxDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL),
        (116, N'MONTH(CLMINVRecDate) AS MONTH, Count(CLMINVRecDate) as Total From genisis.DcuClaimNew where CLMINVRecDate IS NOT NULL', N'GROUP BY MONTH(CLMINVRecDate)', N'ORDER BY [MONTH]', NULL, NULL),
        (117, N'MONTH(BordxDate) AS MONTH, Count(BordxDate) as Total From genisis.WS_ClmReportDcu where BordxDate IS NOT NULL', N'GROUP BY MONTH(BordxDate)', N'ORDER BY [MONTH]', NULL, NULL),
        (118, N'MONTH(INVEnteredDate) AS MONTH, Count(INVEnteredDate) as Total From genisis.DcuClaimNew where INVEnteredDate IS NOT NULL', N'GROUP BY MONTH(INVEnteredDate)', N'ORDER BY [MONTH]', NULL, NULL),
        (119, N'MONTH(BordxDate) AS MONTH, Count(BordxDate) as Total From genisis.DcuClaimNew where BordxDate IS NOT NULL', N'GROUP BY MONTH(BordxDate)', N'ORDER BY [MONTH]', NULL, NULL),
        (120, N'YEAR(ClaimOpenDate) AS Year, MONTH(ClaimOpenDate) AS MONTH, Count(CASNumber) as Total From genisis.WS_ClmReportDcu where ClaimOpenDate IS NOT NULL', N'GROUP BY YEAR(ClaimOpenDate), MONTH(ClaimOpenDate)', N'ORDER BY [YEAR], [MONTH]', NULL, NULL)
    ) d (caseNo, sel, grp, ord, topN, rnk)
    WHERE d.caseNo = @case;
    IF @select IS NULL
    BEGIN
        RAISERROR (N'Unknown DCU report case %d.', 16, 1, @case);
        RETURN;
    END
    IF @rank IS NOT NULL
    BEGIN
        IF @orderBy NOT IN (N'Total', N'TotalApproved', N'TotalCases') SET @orderBy = NULL;
        SET @order = N'ORDER BY ' + QUOTENAME(ISNULL(@orderBy, @rank)) + N' DESC, ' + @order;
    END
    DECLARE @sql nvarchar(max) = N'SELECT TOP (' + CASE WHEN @top IS NULL THEN N'@max' ELSE CONVERT(nvarchar(11), @top) END + N') ' + @select + genisis.DcuReport_Where(@criteria) + N' ' + @group + N' ' + @order;
    EXEC sp_executesql @sql, N'@max int', @max = @max;
END
GO

-- The ageing counts of the efficiency reports (CountDIR0to15 ... CountBordxInv90), in one query per report. @kind: DIR (claims by the month the invoice was received, DIRDay), BORDX (by bordx month, BordxDay),
-- INV (by the month the invoice was entered, InvDay), BORDXINV (by bordx month over the invoice view, BordxDay). The age bands are the desktop's (0-15, 15-30, 31-60, 61-90, over 90; the first two share day 15).
-- Unlike the desktop, which ran these counts without the criteria of the screen, they use them. OSBORDX counts the claims by the year and month of the bordx (CountOSBordx).
CREATE OR ALTER PROCEDURE genisis.DcuReport_Efficiency
    @kind nvarchar(10), @criteria xml
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @src nvarchar(100), @date nvarchar(30), @day nvarchar(30), @sql nvarchar(max);
    IF @kind = N'DIR' SELECT @src = N'genisis.DcuClaimNew', @date = N'CLMINVRecDate', @day = N'DIRDay';
    ELSE IF @kind = N'BORDX' SELECT @src = N'genisis.WS_ClmReportDcu', @date = N'BordxDate', @day = N'BordxDay';
    ELSE IF @kind = N'INV' SELECT @src = N'genisis.DcuClaimNew', @date = N'INVEnteredDate', @day = N'InvDay';
    ELSE IF @kind = N'BORDXINV' SELECT @src = N'genisis.DcuClaimNew', @date = N'BordxDate', @day = N'BordxDay';
    IF @kind = N'OSBORDX'
        SET @sql = N'SELECT YEAR(BordxDate) AS [YEAR], MONTH(BordxDate) AS [MONTH], COUNT(CASNumber) AS Total FROM genisis.WS_ClmReportDcu WHERE BordxDate IS NOT NULL' + genisis.DcuReport_Where(@criteria) + N' GROUP BY YEAR(BordxDate), MONTH(BordxDate)';
    ELSE IF @src IS NOT NULL
        SET @sql = N'SELECT MONTH(' + @date + N') AS [MONTH], COUNT(' + @date + N') AS Total, SUM(CASE WHEN ' + @day + N' BETWEEN 0 AND 15 THEN 1 ELSE 0 END) AS D0to15, SUM(CASE WHEN ' + @day + N' BETWEEN 15 AND 30 THEN 1 ELSE 0 END) AS D15to30, '
            + N'SUM(CASE WHEN ' + @day + N' BETWEEN 31 AND 60 THEN 1 ELSE 0 END) AS D31to60, SUM(CASE WHEN ' + @day + N' BETWEEN 61 AND 90 THEN 1 ELSE 0 END) AS D61to90, SUM(CASE WHEN ' + @day + N' > 90 THEN 1 ELSE 0 END) AS D90 FROM '
            + @src + N' WHERE ' + @date + N' IS NOT NULL' + genisis.DcuReport_Where(@criteria) + N' GROUP BY MONTH(' + @date + N') ORDER BY MONTH(' + @date + N')';
    IF @sql IS NULL
    BEGIN
        RAISERROR (N'Unknown efficiency report.', 16, 1);
        RETURN;
    END
    EXEC sp_executesql @sql;
END
GO

-- ComboListing: insured types, payors (with their health type, which narrows the list as LoadPayor did) and policy statuses.
CREATE OR ALTER PROCEDURE genisis.DcuReport_Lookups
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(INSCode) AS Code, RTRIM(INSDescription) AS Name FROM dbo.INS ORDER BY INSCode;
    SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name, RTRIM(HLTCode) AS HealthType FROM dbo.PAY ORDER BY PAYCode;
    SELECT RTRIM(pstatuscode) AS Code, RTRIM(pstatusDesc) AS Name FROM dbo.PolicyStatus ORDER BY pstatuscode;
END
GO

-- ChkGrpCom2_Click / cboPayorCode_Click (SearchGrpCompanyPay): the group companies of a payor's members. The first form listed those of every payor; the Jupiter form's payor filter is what is meant.
CREATE OR ALTER PROCEDURE genisis.DcuReport_GroupCompanies
    @payor nvarchar(4)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT RTRIM(o.MBMGroupCompany) AS GroupCompany
    FROM dbo.MBMOthers o INNER JOIN genisis.Maint_MBMCrossReference x ON x.MBMNumber = o.MBMNumber
    WHERE x.PAYCode = @payor AND RTRIM(ISNULL(o.MBMGroupCompany, N'')) <> N''
    ORDER BY GroupCompany;
END
GO

-- GenFirstICD / GenFinalICD / DiagFinalICD / DiagHOSFinalICD / HOSFinalICD / PLNFinalICD: the descriptions of a list of ICD codes (as <l><i>code</i>...</l>), once for the whole list instead of one query per row.
CREATE OR ALTER PROCEDURE genisis.DcuReport_IcdDescriptions
    @codes xml
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(i.ICDCode) AS Code, RTRIM(MIN(i.ICDDescription)) AS Description
    FROM dbo.ICDOriginal i
    WHERE RTRIM(i.ICDCode) IN (SELECT RTRIM(x.value('.', 'nvarchar(100)')) FROM @codes.nodes('/l/i') t (x))
    GROUP BY RTRIM(i.ICDCode);
END
GO

-- GenBordxRefNo1 / GenBordxRefNo2: of the cases listed (separated by |), the approved amount of the claims not yet in a bordx and of those in one. The desktop showed the case's whole approved amount in both columns
-- whenever the case had a claim of that kind; the amounts of those claims are summed here.
CREATE OR ALTER PROCEDURE genisis.DcuReport_CaseBordx
    @cases xml
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(CASNumber) AS CaseNo, SUM(CASE WHEN ISNULL(BordxRefNo, N'') = N'' THEN CLMTotalApproved END) AS NotYetBordx, SUM(CASE WHEN ISNULL(BordxRefNo, N'') <> N'' THEN CLMTotalApproved END) AS Bordxed
    FROM genisis.WS_Claim
    WHERE RTRIM(CASNumber) IN (SELECT RTRIM(x.value('.', 'nvarchar(100)')) FROM @cases.nodes('/l/i') t (x))
    GROUP BY RTRIM(CASNumber);
END
GO
