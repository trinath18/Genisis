using Dapper;
using Genisis.Api.Case;
using Genisis.Api.Common;
using Genisis.Api.Worksheet;
using Genisis.Api.Worksheet.Adjustment;
using Genisis.Api.Worksheet.Enquiry;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Worksheet > Worksheet Adjustment, step 3: the rules of Final LG (cmdFinalLG_Click).</summary>
public class WorksheetFinalLgRulesTests
{
    private static readonly DateTime Jan = new(2024, 1, 1);

    [Theory]
    [InlineData("AX", true)]
    [InlineData(" ax ", true)]
    [InlineData("AH", false)]
    [InlineData(null, false)]
    public void The_take_over_clause_is_asked_only_for_payor_AX_with_different_policy_dates(string? payor, bool asked) =>
        Assert.Equal(asked, FinalLgRules.NeedsTakeOverCheck(payor, Jan, Jan.AddMonths(6)));

    [Fact]
    public void The_take_over_clause_is_not_asked_when_the_dates_are_the_same_whatever_the_time_of_day()
    {
        Assert.False(FinalLgRules.NeedsTakeOverCheck("AX", Jan, Jan.AddHours(5)));
        Assert.True(FinalLgRules.NeedsTakeOverCheck("AX", null, Jan));
        Assert.False(FinalLgRules.NeedsTakeOverCheck("AX", null, null));
    }

    [Fact]
    public void The_member_comes_before_the_patient_name_in_the_desktops_order()
    {
        Assert.Equal("Please Enter Membership Information", FinalLgRules.MemberProblem(" ", "ALI"));
        Assert.Equal("Please Enter Membership Information", FinalLgRules.MemberProblem(null, null));
        Assert.Equal("Please Enter Patient Name", FinalLgRules.MemberProblem("AHA0001", ""));
        Assert.Null(FinalLgRules.MemberProblem("AHA0001", "ALI"));
    }

    [Theory]
    [InlineData("H", 500.00, 500.00, true)]
    [InlineData("H", 500.004, 500.00, true)]
    [InlineData("H", 500.01, 500.00, false)]
    [InlineData("h", 0, 120, false)]
    [InlineData("M", 999, 120, true)]
    [InlineData(null, 999, 120, true)]
    public void A_worksheet_paid_to_the_hospital_needs_the_LG_amount_to_be_its_final_total_to_the_cent(string? payTo, double lg, double total, bool matches) =>
        Assert.Equal(matches, FinalLgRules.MatchesWorksheet(payTo, (decimal)lg, (decimal)total));

    [Theory]
    [InlineData(1000, 1000, 0, true)]
    [InlineData(1000.01, 1000, 0, false)]
    [InlineData(1500, 1000, 600, true)]
    [InlineData(1500, 1000, 400, false)]
    public void The_LG_amount_may_be_the_balance_limit_plus_what_the_worksheet_itself_approved(double lg, double balance, double approved, bool within) =>
        Assert.Equal(within, FinalLgRules.WithinBalance((decimal)lg, (decimal)balance, (decimal)approved));

    [Fact]
    public void A_plan_without_a_balance_limit_has_no_limit_to_test() => Assert.True(FinalLgRules.WithinBalance(1_000_000, null, 0));

    [Fact]
    public void The_prompt_is_Are_you_sure_when_all_is_well_and_the_warning_otherwise()
    {
        Assert.Equal("Are you sure?", FinalLgRules.Prompt(true));
        Assert.Equal("Please check LG Amount, do you want to continue?", FinalLgRules.Prompt(false));
        Assert.Equal("Have you checked Take Over clause?", FinalLgRules.TakeOverQuestion);
    }

    [Theory]
    [InlineData("12,345.60", 12345.60)]
    [InlineData("0.00", 0)]
    [InlineData("-50.00", -50)]
    public void The_balance_the_case_check_reports_is_read_back_as_a_number(string text, double expected) => Assert.Equal((decimal)expected, FinalLgRules.ParseBalance(text));

    [Fact]
    public void No_balance_text_is_no_balance()
    {
        Assert.Null(FinalLgRules.ParseBalance(null));
        Assert.Null(FinalLgRules.ParseBalance(""));
    }

    [Fact]
    public void The_reason_is_the_final_diagnoses_comma_separated()
    {
        var diagnoses = new[]
        {
            new EnquiryDiagnosis { Source = "First", Slot = 1, Diagnosis = "PRELIM" },
            new EnquiryDiagnosis { Source = "Final", Slot = 2, Diagnosis = "ABSCESS" },
            new EnquiryDiagnosis { Source = "Final", Slot = 1, Diagnosis = "FISTULA" },
        };
        Assert.Equal("FISTULA, ABSCESS", FinalLgRules.Reason(diagnoses));
    }
}

/// <summary>Final LG against the development databases. The letter is issued by code that commits, so the rows it touches are saved first and put back afterwards.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class WorksheetFinalLgDatabaseTests
{
    private static readonly string? MainDb = TestDb.MainDb;

    private static FinalLgService Service()
    {
        var connections = TestDb.Connections();
        var documents = new CaseDocumentsService(connections, new CaseRegistrationService(connections, NullLogger<CaseRegistrationService>.Instance), NullLogger<CaseDocumentsService>.Instance);
        return new FinalLgService(TestDb.Sessions(), new EnquiryRepository(), new AdjustmentRepository(), documents);
    }

    private static FinalLgRequest Request(decimal amount) => new()
    {
        Purpose = "Follow-up", RequestDate = DateTime.Today, RequestTime = "10:00", RequestMode = "Email", RequestBy = "Hospital", GLDate = DateTime.Today, GLTime = "10:10",
        LgAmount = amount, Remarks = "UNIT TEST",
    };

    /// <summary>A worksheet whose case is registered, has a member and a record in the maintenance database, and whose version has invoices.</summary>
    private static async Task<(string Cas, int Version)> Worksheet(Microsoft.Data.SqlClient.SqlConnection conn) =>
        await conn.QueryFirstAsync<(string, int)>(
            """
            SELECT TOP (1) RTRIM(c.CASNumber), c.ClaimVersion FROM genisis.WS_Claim c
            WHERE EXISTS (SELECT 1 FROM dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v WHERE v.CASNumber = c.CASNumber AND v.ClaimVersion = c.ClaimVersion AND v.MBMNumber IS NOT NULL AND ISNULL(RTRIM(v.MBMName), '') <> '')
              AND EXISTS (SELECT 1 FROM genisis.Maint_tblRecord r WHERE r.CaseNo = c.CASNumber)
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimInvoice i WHERE i.CASNumber = c.CASNumber AND i.INVWSversion = CAST(c.ClaimVersion AS nvarchar(4)))
              AND EXISTS (SELECT 1 FROM dbo.CAS s WHERE s.CASNumber = c.CASNumber)
              AND NOT EXISTS (SELECT 1 FROM genisis.Maint_CASLG g WHERE g.CASENo = c.CASNumber AND g.LGPurpose = 'Follow-up')
            ORDER BY c.CASNumber DESC, c.ClaimVersion
            """);

    [Fact]
    public async Task The_check_compares_the_LG_amount_with_the_worksheets_final_total()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await using var conn = TestDb.Connections().MainDb();
        await conn.OpenAsync();
        var (cas, version) = await Worksheet(conn);
        var service = Service();

        var probe = await service.CheckAsync(cas, version, Request(1));
        var total = probe.FinalTotal;
        var payTo = await new AdjustmentRepository().PayToAsync(await TestDb.Sessions().OpenAsync(), cas, version);

        var exact = await service.CheckAsync(cas, version, Request(total));
        Assert.True(exact.MatchesWorksheet);
        Assert.Equal(total, exact.FinalTotal);

        var off = await service.CheckAsync(cas, version, Request(total + 1));
        Assert.Equal(!string.Equals(payTo?.Trim(), "H", StringComparison.OrdinalIgnoreCase), off.MatchesWorksheet);
        Assert.Equal(off.WithinLimit ? FinalLgRules.Confirm : FinalLgRules.CheckAmount, off.Prompt);
        Assert.False(off.TakeOverCheck);
    }

    [Fact]
    public async Task The_check_refuses_a_bad_form_a_missing_worksheet_and_a_bad_version()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var service = Service();
        var bad = Request(10);
        bad.Remarks = "";
        Assert.Equal("Please enter remark", (await Assert.ThrowsAsync<WorksheetException>(() => service.CheckAsync("NO SUCH CASE", 1, bad))).Message, ignoreCase: true);
        await Assert.ThrowsAsync<NotFoundException>(() => service.CheckAsync("NO SUCH CASE", 1, Request(10)));
        Assert.Equal("Please select a version nombor!", (await Assert.ThrowsAsync<WorksheetException>(() => service.CheckAsync("NO SUCH CASE", 0, Request(10)))).Message);
    }

    [Fact]
    public async Task Final_LG_issues_the_letter_numbers_the_invoices_and_marks_the_case_then_everything_is_put_back()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await using var conn = TestDb.Connections().MainDb();
        await conn.OpenAsync();
        var (cas, version) = await Worksheet(conn);
        var args = new { cas, version };

        Assert.Matches("^[A-Za-z0-9]+$", cas);
        await conn.ExecuteAsync(
            $"""
            SELECT * INTO #fl_caslg FROM genisis.Maint_CASLG WHERE CASENo = '{cas}';
            SELECT RecId, IsLGissued, IsWSdone, CaseProgress INTO #fl_rec FROM genisis.Maint_tblRecord WHERE CaseNo = '{cas}';
            SELECT CASNumber, INVIndex, LGReference INTO #fl_inv FROM genisis.WS_ClaimInvoice WHERE CASNumber = '{cas}';
            """);
        var lgBefore = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.Maint_CASLG WHERE CASENo = @cas", args);
        // nothing is written until the snapshot is proven to be there
        Assert.Equal(lgBefore, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM #fl_caslg"));
        Assert.True(await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM #fl_rec") > 0);
        Assert.True(await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM #fl_inv") > 0);
        var recBefore = await conn.QueryFirstAsync<(string?, string?, string?)>("SELECT TOP (1) RTRIM(IsLGissued), RTRIM(IsWSdone), RTRIM(CaseProgress) FROM genisis.Maint_tblRecord WHERE CaseNo = @cas ORDER BY RecId", args);

        string? lgNumber = null;
        try
        {
            var result = await Service().IssueAsync(cas, version, Request(123.45m), "UNITTEST", "Unit Tester");
            lgNumber = result.LgNumber;

            Assert.Matches($"^[LB]{cas}-\\d+$", lgNumber);
            Assert.Equal(cas, result.Letter.Fields["caseNo"]);
            Assert.Equal(lgNumber, result.Letter.Fields["lgNo"]);

            var lg = await conn.QueryFirstAsync<(string, decimal, string, string)>(
                "SELECT RTRIM(LGPurpose), LGAmt, RTRIM(LGIssuedBy), RTRIM(LGRemark) FROM genisis.Maint_CASLG WHERE CASENo = @cas AND LGPurpose = 'Follow-up'", args);
            Assert.Equal(("Follow-up", 123.45m, "UNITTEST", "UNIT TEST"), lg);
            Assert.Equal(lgBefore + 1, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.Maint_CASLG WHERE CASENo = @cas", args));

            var references = (await conn.QueryAsync<string>("SELECT RTRIM(LGReference) FROM genisis.WS_ClaimInvoice WHERE CASNumber = @cas AND INVWSversion = CAST(@version AS nvarchar(4))", args)).ToList();
            Assert.NotEmpty(references);
            Assert.All(references, r => Assert.Equal(lgNumber, r));

            var rec = await conn.QueryFirstAsync<(string, string, string)>("SELECT TOP (1) RTRIM(IsLGissued), RTRIM(IsWSdone), RTRIM(CaseProgress) FROM genisis.Maint_tblRecord WHERE CaseNo = @cas ORDER BY RecId", args);
            Assert.Equal(("1", "1", "FINAL GL Issued"), rec);
        }
        finally
        {
            await conn.ExecuteAsync(
                $"""
                SET XACT_ABORT ON;
                IF OBJECT_ID('tempdb..#fl_caslg') IS NULL OR OBJECT_ID('tempdb..#fl_rec') IS NULL OR OBJECT_ID('tempdb..#fl_inv') IS NULL
                    THROW 50001, 'The snapshot is missing; nothing was put back.', 1;
                BEGIN TRAN;
                DELETE FROM genisis.Maint_CASLG WHERE CASENo = '{cas}';
                INSERT INTO genisis.Maint_CASLG SELECT * FROM #fl_caslg;
                UPDATE r SET IsLGissued = b.IsLGissued, IsWSdone = b.IsWSdone, CaseProgress = b.CaseProgress FROM genisis.Maint_tblRecord r JOIN #fl_rec b ON b.RecId = r.RecId;
                UPDATE i SET LGReference = b.LGReference FROM genisis.WS_ClaimInvoice i JOIN #fl_inv b ON b.CASNumber = i.CASNumber AND b.INVIndex = i.INVIndex;
                {(lgNumber is null ? "" : $"DELETE FROM dbo.MBMAndroidNotification WHERE LGNumber = '{lgNumber.Replace("'", "''")}';")}
                COMMIT;
                """);
        }

        Assert.Equal(lgBefore, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.Maint_CASLG WHERE CASENo = @cas", args));
        Assert.Equal(recBefore, await conn.QueryFirstAsync<(string?, string?, string?)>("SELECT TOP (1) RTRIM(IsLGissued), RTRIM(IsWSdone), RTRIM(CaseProgress) FROM genisis.Maint_tblRecord WHERE CaseNo = @cas ORDER BY RecId", args));
    }
}
