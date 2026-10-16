-- Membership > Tracking (legacy FrmNewMBMTracking). The desktop passed a concatenated WHERE fragment to dbo.MBMTracking; here every
-- filter is a typed parameter. genisis.Tracking_CaseText (171_tracking_cases.sql, generated) holds each case's fixed query text.

-- Result sets: payors (with health type), states (MBM.STACode holds the state description, as the desktop's list does), product categories.
CREATE OR ALTER PROCEDURE genisis.Tracking_Lookups
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name, RTRIM(HLTCode) AS HealthCode FROM dbo.PAY WHERE PAYCode IS NOT NULL ORDER BY PAYCode;
    SELECT DISTINCT UPPER(RTRIM(STADescription)) AS Code, UPPER(RTRIM(STADescription)) AS Name FROM dbo.STA WHERE STADescription IS NOT NULL ORDER BY Name;
    SELECT RTRIM(ProductCode) AS Code, RTRIM(ProductName) AS Name FROM genisis.Maint_ProductCategory WHERE ProductCode IS NOT NULL ORDER BY ProductCode;
END
GO

-- Runs one MBMTracking case with the Tracking screen's filters (FrmNewMBMTracking.SearchRecord).
--   @insuredGroup  0..7 (desktop insured type list), @status 0..6 (desktop status list), @renewal N/P/R/T
--   @blanks        comma-separated filter keys whose "blank" box is ticked (keys outside the fixed list are ignored)
--   @memberTypeFilter  DETAIL_C / DETAIL_S (detail listings) or NAME_<P|S|B>_<C|S|BOTH> (General Listing (Prin. Name))
--   @extra         insured / adult-child restriction of the per-type count columns: I, F, G, H, I_C, I_A, G_C, G_A
--   @withSuppRegDate  1 for the supplementary detail listings (Supp. registration date filter)
CREATE OR ALTER PROCEDURE genisis.Tracking_Run
    @case int,
    @health nvarchar(10) = NULL, @payor nvarchar(10) = NULL, @insuredGroup nvarchar(1) = NULL, @status nvarchar(1) = NULL,
    @renewal nvarchar(1) = NULL, @adultChild nvarchar(1) = NULL, @race nvarchar(1) = NULL, @sex nvarchar(1) = NULL,
    @relationship nvarchar(2) = NULL, @dataStatus nvarchar(1) = NULL, @expired nvarchar(1) = NULL, @premiumMode nvarchar(1) = NULL,
    @state nvarchar(50) = NULL, @takeOver nvarchar(1) = NULL, @agentCode nvarchar(50) = NULL, @membershipNo nvarchar(30) = NULL,
    @groupCompany nvarchar(200) = NULL, @labelStatus nvarchar(1) = NULL, @department nvarchar(100) = NULL, @productCategory nvarchar(10) = NULL,
    @dayInsuredFrom decimal(19, 4) = NULL, @dayInsuredTo decimal(19, 4) = NULL, @ageDaysFrom decimal(19, 4) = NULL, @ageDaysTo decimal(19, 4) = NULL,
    @delayBordxFrom decimal(19, 4) = NULL, @delayBordxTo decimal(19, 4) = NULL, @delayInvBordxFrom decimal(19, 4) = NULL,
    @delayInvBordxTo decimal(19, 4) = NULL, @delayRegFrom decimal(19, 4) = NULL, @delayRegTo decimal(19, 4) = NULL,
    @delayInvDFrom decimal(19, 4) = NULL, @delayInvDTo decimal(19, 4) = NULL, @delayInvEFrom decimal(19, 4) = NULL, @delayInvETo decimal(19, 4) = NULL,
    @premiumFrom decimal(19, 4) = NULL, @premiumTo decimal(19, 4) = NULL, @mcoFeeFrom decimal(19, 4) = NULL, @mcoFeeTo decimal(19, 4) = NULL,
    @batchFrom decimal(19, 4) = NULL, @batchTo decimal(19, 4) = NULL,
    @employeeNoFrom nvarchar(50) = NULL, @employeeNoTo nvarchar(50) = NULL, @policyNoFrom nvarchar(50) = NULL, @policyNoTo nvarchar(50) = NULL,
    @planFrom nvarchar(10) = NULL, @planTo nvarchar(10) = NULL, @ageBandFrom nvarchar(10) = NULL, @ageBandTo nvarchar(10) = NULL,
    @postCodeFrom nvarchar(10) = NULL, @postCodeTo nvarchar(10) = NULL, @policyYearFrom nvarchar(10) = NULL, @policyYearTo nvarchar(10) = NULL,
    @invoiceNoFrom nvarchar(50) = NULL, @invoiceNoTo nvarchar(50) = NULL, @icFrom nvarchar(30) = NULL, @icTo nvarchar(30) = NULL,
    @effDateFrom date = NULL, @effDateTo date = NULL, @expDateFrom date = NULL, @expDateTo date = NULL,
    @dateJoinedFrom date = NULL, @dateJoinedTo date = NULL, @dorFrom date = NULL, @dorTo date = NULL, @dobFrom date = NULL, @dobTo date = NULL,
    @bordxDateFrom date = NULL, @bordxDateTo date = NULL, @invoiceDateFrom date = NULL, @invoiceDateTo date = NULL,
    @endtEffDateFrom date = NULL, @endtEffDateTo date = NULL, @cancelDateFrom date = NULL, @cancelDateTo date = NULL,
    @dorSuppFrom date = NULL, @dorSuppTo date = NULL,
    @blanks nvarchar(2000) = NULL, @memberTypeFilter nvarchar(20) = NULL, @extra nvarchar(5) = NULL, @withSuppRegDate bit = 0
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @prefix nvarchar(max), @suffix nvarchar(max), @w nvarchar(max) = N'';
    EXEC genisis.Tracking_CaseText @case = @case, @prefix = @prefix OUTPUT, @suffix = @suffix OUTPUT;
    IF @prefix IS NULL THROW 50001, N'Unknown tracking report case.', 1;

    -- Equality filters.
    SET @w += CASE WHEN @health IS NOT NULL THEN N' AND HLTCode = @health' ELSE N'' END
        + CASE WHEN @payor IS NOT NULL THEN N' AND PAYCode = @payor' ELSE N'' END
        + CASE WHEN @adultChild IS NOT NULL THEN N' AND MBMAdultChild = @adultChild' ELSE N'' END
        + CASE WHEN @race IS NOT NULL THEN N' AND RACCode = @race' ELSE N'' END
        + CASE WHEN @sex IS NOT NULL THEN N' AND MBMSex = @sex' ELSE N'' END
        + CASE WHEN @relationship IS NOT NULL THEN N' AND RELCode = @relationship' ELSE N'' END
        + CASE WHEN @dataStatus IS NOT NULL THEN N' AND MBMDataStatus = @dataStatus' ELSE N'' END
        + CASE WHEN @expired IS NOT NULL THEN N' AND MBMExpiredStatus = @expired' ELSE N'' END
        + CASE WHEN @premiumMode IS NOT NULL THEN N' AND MBMInstallment = @premiumMode' ELSE N'' END
        + CASE WHEN @state IS NOT NULL THEN N' AND STACode = @state' ELSE N'' END
        + CASE WHEN @takeOver IS NOT NULL THEN N' AND MBMTakeover = @takeOver' ELSE N'' END
        + CASE WHEN @agentCode IS NOT NULL THEN N' AND MBMAgentCode = @agentCode' ELSE N'' END
        + CASE WHEN @membershipNo IS NOT NULL THEN N' AND MBMNumber = @membershipNo' ELSE N'' END
        + CASE WHEN @groupCompany IS NOT NULL THEN N' AND MBMGroupCompany = @groupCompany' ELSE N'' END
        + CASE WHEN @labelStatus IS NOT NULL THEN N' AND LabelStatus = @labelStatus' ELSE N'' END
        + CASE WHEN @department IS NOT NULL THEN N' AND MBMDepartment = @department' ELSE N'' END
        + CASE WHEN @productCategory IS NOT NULL THEN N' AND PLNProCat = @productCategory' ELSE N'' END;

    -- Coded selections (desktop combo lists).
    SET @w += CASE @insuredGroup
            WHEN N'0' THEN N' AND INSCode = ''I''' WHEN N'1' THEN N' AND INSCode = ''F''' WHEN N'2' THEN N' AND INSCode = ''G'''
            WHEN N'3' THEN N' AND INSCode = ''H''' WHEN N'4' THEN N' AND (INSCode = ''I'' OR INSCode = ''F'')'
            WHEN N'5' THEN N' AND (INSCode = ''I'' OR INSCode = ''G'')' WHEN N'6' THEN N' AND (INSCode = ''F'' OR INSCode = ''H'')'
            WHEN N'7' THEN N' AND (INSCode = ''G'' OR INSCode = ''H'')' ELSE N'' END
        + CASE @status
            WHEN N'0' THEN N' AND MBMStatus = ''A''' WHEN N'1' THEN N' AND MBMStatus = ''C''' WHEN N'2' THEN N' AND MBMStatus = ''D'''
            WHEN N'3' THEN N' AND (MBMStatus = ''A'' OR MBMStatus = ''C'')' WHEN N'4' THEN N' AND (MBMStatus = ''A'' OR MBMStatus = ''S'')'
            WHEN N'5' THEN N' AND (MBMStatus = ''A'' OR MBMStatus = ''C'' OR MBMStatus = ''S'')' WHEN N'6' THEN N' AND MBMStatus = ''S''' ELSE N'' END
        + CASE @renewal
            WHEN N'P' THEN N' AND (MBMRenewFlag = ''P'')' WHEN N'N' THEN N' AND (MBMRenewFlag IS NULL OR MBMRenewFlag = '''' OR MBMRenewFlag = ''N'')'
            WHEN N'R' THEN N' AND (MBMRenewFlag = ''R'')' WHEN N'T' THEN N' AND (MBMRenewFlag = ''T'')' ELSE N'' END;

    -- From / to ranges.
    SELECT @w += CASE WHEN r.FromSet = 1 THEN N' AND ' + r.Col + N' >= @' + r.Param + N'From' ELSE N'' END
               + CASE WHEN r.ToSet = 1 THEN N' AND ' + r.Col + N' <= @' + r.Param + N'To' ELSE N'' END
    FROM (VALUES
        (N'DayInsured', N'dayInsured', IIF(@dayInsuredFrom IS NULL, 0, 1), IIF(@dayInsuredTo IS NULL, 0, 1)),
        (N'AgeDays', N'ageDays', IIF(@ageDaysFrom IS NULL, 0, 1), IIF(@ageDaysTo IS NULL, 0, 1)),
        (N'DelayBordx', N'delayBordx', IIF(@delayBordxFrom IS NULL, 0, 1), IIF(@delayBordxTo IS NULL, 0, 1)),
        (N'DelayInvBordx', N'delayInvBordx', IIF(@delayInvBordxFrom IS NULL, 0, 1), IIF(@delayInvBordxTo IS NULL, 0, 1)),
        (N'DelayReg', N'delayReg', IIF(@delayRegFrom IS NULL, 0, 1), IIF(@delayRegTo IS NULL, 0, 1)),
        (N'DelayInvD', N'delayInvD', IIF(@delayInvDFrom IS NULL, 0, 1), IIF(@delayInvDTo IS NULL, 0, 1)),
        (N'DelayInvE', N'delayInvE', IIF(@delayInvEFrom IS NULL, 0, 1), IIF(@delayInvETo IS NULL, 0, 1)),
        (N'MBMPremium', N'premium', IIF(@premiumFrom IS NULL, 0, 1), IIF(@premiumTo IS NULL, 0, 1)),
        (N'MBMMCOFee', N'mcoFee', IIF(@mcoFeeFrom IS NULL, 0, 1), IIF(@mcoFeeTo IS NULL, 0, 1)),
        (N'MBMBatchNo', N'batch', IIF(@batchFrom IS NULL, 0, 1), IIF(@batchTo IS NULL, 0, 1)),
        (N'MBMEmployeeNo', N'employeeNo', IIF(@employeeNoFrom IS NULL, 0, 1), IIF(@employeeNoTo IS NULL, 0, 1)),
        (N'MBMPolicyNo', N'policyNo', IIF(@policyNoFrom IS NULL, 0, 1), IIF(@policyNoTo IS NULL, 0, 1)),
        (N'PLNCode', N'plan', IIF(@planFrom IS NULL, 0, 1), IIF(@planTo IS NULL, 0, 1)),
        (N'AGECode', N'ageBand', IIF(@ageBandFrom IS NULL, 0, 1), IIF(@ageBandTo IS NULL, 0, 1)),
        (N'MBMPostCode', N'postCode', IIF(@postCodeFrom IS NULL, 0, 1), IIF(@postCodeTo IS NULL, 0, 1)),
        (N'MBMPolicyYear', N'policyYear', IIF(@policyYearFrom IS NULL, 0, 1), IIF(@policyYearTo IS NULL, 0, 1)),
        (N'MBMInvoiceNo', N'invoiceNo', IIF(@invoiceNoFrom IS NULL, 0, 1), IIF(@invoiceNoTo IS NULL, 0, 1)),
        (N'MBMIcBcPp', N'ic', IIF(@icFrom IS NULL, 0, 1), IIF(@icTo IS NULL, 0, 1)),
        (N'MBMPayorEffDate', N'effDate', IIF(@effDateFrom IS NULL, 0, 1), IIF(@effDateTo IS NULL, 0, 1)),
        (N'MBMPayorExpDate', N'expDate', IIF(@expDateFrom IS NULL, 0, 1), IIF(@expDateTo IS NULL, 0, 1)),
        (N'MBMFirstJoinedDate', N'dateJoined', IIF(@dateJoinedFrom IS NULL, 0, 1), IIF(@dateJoinedTo IS NULL, 0, 1)),
        (N'MBMValueDate', N'dor', IIF(@dorFrom IS NULL, 0, 1), IIF(@dorTo IS NULL, 0, 1)),
        (N'MBMDOB', N'dob', IIF(@dobFrom IS NULL, 0, 1), IIF(@dobTo IS NULL, 0, 1)),
        (N'MBMBordxDate', N'bordxDate', IIF(@bordxDateFrom IS NULL, 0, 1), IIF(@bordxDateTo IS NULL, 0, 1)),
        (N'MBMInvoiceDate', N'invoiceDate', IIF(@invoiceDateFrom IS NULL, 0, 1), IIF(@invoiceDateTo IS NULL, 0, 1)),
        (N'MBMEndtEffDate', N'endtEffDate', IIF(@endtEffDateFrom IS NULL, 0, 1), IIF(@endtEffDateTo IS NULL, 0, 1)),
        (N'MBMCancelDate', N'cancelDate', IIF(@cancelDateFrom IS NULL, 0, 1), IIF(@cancelDateTo IS NULL, 0, 1)),
        (N'MBMCRegDate', N'dorSupp', IIF(@withSuppRegDate = 1 AND @dorSuppFrom IS NOT NULL, 1, 0), IIF(@withSuppRegDate = 1 AND @dorSuppTo IS NOT NULL, 1, 0))
    ) r (Col, Param, FromSet, ToSet);

    -- "Blank" check boxes. (HISDB runs at a legacy compatibility level, so STRING_SPLIT is not available; keys are matched with
    -- CHARINDEX against a fixed list and never concatenated into the SQL.)
    DECLARE @blankList nvarchar(2002) = N',' + REPLACE(ISNULL(@blanks, N''), N' ', N'') + N',';
    DECLARE @blankClauses TABLE (BlankKey nvarchar(50) PRIMARY KEY, Clause nvarchar(200));
    INSERT INTO @blankClauses VALUES
        (N'health', N'(HLTCode IS NULL OR HLTCode = '''')'), (N'payor', N'(PAYCode IS NULL OR PAYCode = '''')'),
        (N'insuredGroup', N'(INSCode IS NULL OR INSCode = '''')'), (N'memberTypeBlank', N'(MBMMemberType IS NULL OR MBMMemberType = '''')'),
        (N'adultChild', N'(MBMAdultChild IS NULL OR MBMAdultChild = '''')'), (N'race', N'(RACCode IS NULL OR RACCode = '''')'),
        (N'sex', N'(MBMSex IS NULL OR MBMSex = '''')'), (N'relationship', N'(RELCode IS NULL OR RELCode = '''')'),
        (N'dataStatus', N'(MBMDataStatus IS NULL OR MBMDataStatus = '''')'), (N'status', N'(MBMStatus IS NULL OR MBMStatus = '''')'),
        (N'expired', N'(MBMExpiredStatus IS NULL OR MBMExpiredStatus = '''')'), (N'renewal', N'(MBMRenewFlag IS NULL OR MBMRenewFlag = '''')'),
        (N'premiumMode', N'(MBMInstallment IS NULL OR MBMInstallment = '''')'), (N'state', N'(STACode IS NULL OR STACode = '''')'),
        (N'takeOver', N'(MBMTakeover IS NULL OR MBMTakeover = '''')'), (N'agentCode', N'(MBMAgentCode IS NULL OR MBMAgentCode = '''')'),
        (N'membershipNo', N'(MBMNumber IS NULL OR MBMNumber = '''')'), (N'policyNo', N'(MBMPolicyNo IS NULL OR MBMPolicyNo = '''')'),
        (N'groupCompany', N'(MBMGroupCompany IS NULL OR MBMGroupCompany = '''')'), (N'plan', N'(PLNCode IS NULL OR PLNCode = '''')'),
        (N'ageBand', N'(AGECode IS NULL OR AGECode = '''')'), (N'employeeNo', N'(MBMEmployeeNo IS NULL OR MBMEmployeeNo = '''')'),
        (N'dayInsured', N'(DayInsured IS NULL)'), (N'ageDays', N'(AgeDays IS NULL)'), (N'delayBordx', N'(DelayBordx IS NULL)'),
        (N'delayInvBordx', N'(DelayInvBordx IS NULL)'), (N'delayReg', N'(DelayReg IS NULL)'), (N'delayInvD', N'(DelayInvD IS NULL)'),
        (N'delayInvE', N'(DelayInvE IS NULL)'), (N'premium', N'(MBMPremium IS NULL)'), (N'mcoFee', N'(MBMMCOFee IS NULL)'),
        (N'effDate', N'(MBMPayorEffDate IS NULL OR MBMPayorEffDate = '''')'), (N'expDate', N'(MBMPayorExpDate IS NULL OR MBMPayorExpDate = '''')'),
        (N'dateJoined', N'(MBMFirstJoinedDate IS NULL OR MBMFirstJoinedDate = '''')'), (N'dor', N'(MBMValueDate IS NULL OR MBMValueDate = '''')'),
        (N'dob', N'(MBMDOB IS NULL OR MBMDOB = '''')'), (N'bordxDate', N'(MBMBordxDate IS NULL OR MBMBordxDate = '''')'),
        (N'batch', N'(MBMBatchNo IS NULL OR MBMBatchNo = '''')'), (N'postCode', N'(MBMPostCode IS NULL OR MBMPostCode = '''')'),
        (N'policyYear', N'(MBMPolicyYear IS NULL OR MBMPolicyYear = '''')'), (N'invoiceNo', N'(MBMInvoiceNo IS NULL OR MBMInvoiceNo = '''')'),
        (N'invoiceDate', N'(MBMInvoiceDate IS NULL OR MBMInvoiceDate = '''')'), (N'ic', N'(MBMIcBcPp IS NULL OR MBMIcBcPp = '''')'),
        (N'labelStatus', N'(LabelStatus IS NULL OR LabelStatus = '''')'), (N'department', N'(MBMDepartment IS NULL OR MBMDepartment = '''')'),
        (N'productCategory', N'(PLNProCat IS NULL OR PLNProCat = '''')'), (N'endtEffDate', N'(MBMEndtEffDate IS NULL OR MBMEndtEffDate = '''')'),
        (N'cancelDate', N'(MBMCancelDate IS NULL OR MBMCancelDate = '''')'), (N'dorSupp', N'(MBMCRegDate IS NULL OR MBMCRegDate = '''')');
    SELECT @w += N' AND ' + c.Clause FROM @blankClauses c
    WHERE CHARINDEX(N',' + c.BlankKey + N',', @blankList) > 0 AND (c.BlankKey <> N'dorSupp' OR @withSuppRegDate = 1);

    -- Member type selection of the detail listings and of General Listing (Prin. Name).
    IF @memberTypeFilter IS NOT NULL
    BEGIN
        DECLARE @memberClause nvarchar(200) = CASE @memberTypeFilter
            WHEN N'DETAIL_C' THEN N'(MBMMemberType = ''P'' OR MBMMemberType = ''C'')'
            WHEN N'DETAIL_S' THEN N'(MBMMemberType = ''Q'')'
            WHEN N'NAME_P_C' THEN N'MBMMemberType = ''P'' AND MBMCCoverID = ''00'''
            WHEN N'NAME_P_S' THEN N'MBMMemberType = ''Q'' AND MBMCCoverID = ''00'''
            WHEN N'NAME_P_BOTH' THEN N'(MBMMemberType = ''P'' OR MBMMemberType = ''Q'') AND MBMCCoverID = ''00'''
            WHEN N'NAME_S_C' THEN N'MBMMemberType = ''C'''
            WHEN N'NAME_S_S' THEN N'MBMMemberType = ''S'''
            WHEN N'NAME_S_BOTH' THEN N'(MBMMemberType = ''S'' OR MBMMemberType = ''C'')'
            WHEN N'NAME_B_C' THEN N'(MBMMemberType = ''P'' OR MBMMemberType = ''C'')'
            WHEN N'NAME_B_S' THEN N'(MBMMemberType = ''Q'' OR MBMMemberType = ''S'')'
            WHEN N'NAME_B_BOTH' THEN N'(MBMMemberType = ''P'' OR MBMMemberType = ''Q'')' END;
        IF @memberClause IS NULL THROW 50003, N'Unknown tracking member type filter.', 1;
        SET @w += N' AND ' + @memberClause;
    END

    -- Per insured / adult-child count columns (Bordx and MCO invoice "INS Type & MBM Type", Branch by Agent).
    IF @extra IS NOT NULL
    BEGIN
        DECLARE @extraClause nvarchar(200) = CASE @extra
            WHEN N'I' THEN N'INSCode = ''I''' WHEN N'F' THEN N'INSCode = ''F''' WHEN N'G' THEN N'INSCode = ''G''' WHEN N'H' THEN N'INSCode = ''H'''
            WHEN N'I_C' THEN N'INSCode = ''I'' AND MBMAdultChild = ''C''' WHEN N'I_A' THEN N'INSCode = ''I'' AND MBMAdultChild = ''A'''
            WHEN N'G_C' THEN N'INSCode = ''G'' AND MBMAdultChild = ''C''' WHEN N'G_A' THEN N'INSCode = ''G'' AND MBMAdultChild = ''A''' END;
        IF @extraClause IS NULL THROW 50004, N'Unknown tracking count column.', 1;
        SET @w += N' AND ' + @extraClause;
    END

    -- Payors starting with Z are internal test payors and never reported.
    SET @w += N' AND SUBSTRING(PAYCode, 1, 1) <> ''Z''';

    DECLARE @sql nvarchar(max) = @prefix + @w + N' ' + @suffix;
    EXEC sp_executesql @sql,
        N'@health nvarchar(10), @payor nvarchar(10), @adultChild nvarchar(1), @race nvarchar(1), @sex nvarchar(1), @relationship nvarchar(2),
          @dataStatus nvarchar(1), @expired nvarchar(1), @premiumMode nvarchar(1), @state nvarchar(50), @takeOver nvarchar(1), @agentCode nvarchar(50),
          @membershipNo nvarchar(30), @groupCompany nvarchar(200), @labelStatus nvarchar(1), @department nvarchar(100), @productCategory nvarchar(10),
          @dayInsuredFrom decimal(19, 4), @dayInsuredTo decimal(19, 4), @ageDaysFrom decimal(19, 4), @ageDaysTo decimal(19, 4),
          @delayBordxFrom decimal(19, 4), @delayBordxTo decimal(19, 4), @delayInvBordxFrom decimal(19, 4), @delayInvBordxTo decimal(19, 4),
          @delayRegFrom decimal(19, 4), @delayRegTo decimal(19, 4), @delayInvDFrom decimal(19, 4), @delayInvDTo decimal(19, 4),
          @delayInvEFrom decimal(19, 4), @delayInvETo decimal(19, 4), @premiumFrom decimal(19, 4), @premiumTo decimal(19, 4),
          @mcoFeeFrom decimal(19, 4), @mcoFeeTo decimal(19, 4), @batchFrom decimal(19, 4), @batchTo decimal(19, 4),
          @employeeNoFrom nvarchar(50), @employeeNoTo nvarchar(50), @policyNoFrom nvarchar(50), @policyNoTo nvarchar(50),
          @planFrom nvarchar(10), @planTo nvarchar(10), @ageBandFrom nvarchar(10), @ageBandTo nvarchar(10), @postCodeFrom nvarchar(10),
          @postCodeTo nvarchar(10), @policyYearFrom nvarchar(10), @policyYearTo nvarchar(10), @invoiceNoFrom nvarchar(50), @invoiceNoTo nvarchar(50),
          @icFrom nvarchar(30), @icTo nvarchar(30), @effDateFrom date, @effDateTo date, @expDateFrom date, @expDateTo date,
          @dateJoinedFrom date, @dateJoinedTo date, @dorFrom date, @dorTo date, @dobFrom date, @dobTo date, @bordxDateFrom date, @bordxDateTo date,
          @invoiceDateFrom date, @invoiceDateTo date, @endtEffDateFrom date, @endtEffDateTo date, @cancelDateFrom date, @cancelDateTo date,
          @dorSuppFrom date, @dorSuppTo date',
        @health = @health, @payor = @payor, @adultChild = @adultChild, @race = @race, @sex = @sex, @relationship = @relationship,
        @dataStatus = @dataStatus, @expired = @expired, @premiumMode = @premiumMode, @state = @state, @takeOver = @takeOver, @agentCode = @agentCode,
        @membershipNo = @membershipNo, @groupCompany = @groupCompany, @labelStatus = @labelStatus, @department = @department,
        @productCategory = @productCategory, @dayInsuredFrom = @dayInsuredFrom, @dayInsuredTo = @dayInsuredTo, @ageDaysFrom = @ageDaysFrom,
        @ageDaysTo = @ageDaysTo, @delayBordxFrom = @delayBordxFrom, @delayBordxTo = @delayBordxTo, @delayInvBordxFrom = @delayInvBordxFrom,
        @delayInvBordxTo = @delayInvBordxTo, @delayRegFrom = @delayRegFrom, @delayRegTo = @delayRegTo, @delayInvDFrom = @delayInvDFrom,
        @delayInvDTo = @delayInvDTo, @delayInvEFrom = @delayInvEFrom, @delayInvETo = @delayInvETo, @premiumFrom = @premiumFrom,
        @premiumTo = @premiumTo, @mcoFeeFrom = @mcoFeeFrom, @mcoFeeTo = @mcoFeeTo, @batchFrom = @batchFrom, @batchTo = @batchTo,
        @employeeNoFrom = @employeeNoFrom, @employeeNoTo = @employeeNoTo, @policyNoFrom = @policyNoFrom, @policyNoTo = @policyNoTo,
        @planFrom = @planFrom, @planTo = @planTo, @ageBandFrom = @ageBandFrom, @ageBandTo = @ageBandTo, @postCodeFrom = @postCodeFrom,
        @postCodeTo = @postCodeTo, @policyYearFrom = @policyYearFrom, @policyYearTo = @policyYearTo, @invoiceNoFrom = @invoiceNoFrom,
        @invoiceNoTo = @invoiceNoTo, @icFrom = @icFrom, @icTo = @icTo, @effDateFrom = @effDateFrom, @effDateTo = @effDateTo,
        @expDateFrom = @expDateFrom, @expDateTo = @expDateTo, @dateJoinedFrom = @dateJoinedFrom, @dateJoinedTo = @dateJoinedTo,
        @dorFrom = @dorFrom, @dorTo = @dorTo, @dobFrom = @dobFrom, @dobTo = @dobTo, @bordxDateFrom = @bordxDateFrom, @bordxDateTo = @bordxDateTo,
        @invoiceDateFrom = @invoiceDateFrom, @invoiceDateTo = @invoiceDateTo, @endtEffDateFrom = @endtEffDateFrom, @endtEffDateTo = @endtEffDateTo,
        @cancelDateFrom = @cancelDateFrom, @cancelDateTo = @cancelDateTo, @dorSuppFrom = @dorSuppFrom, @dorSuppTo = @dorSuppTo;
END
GO
