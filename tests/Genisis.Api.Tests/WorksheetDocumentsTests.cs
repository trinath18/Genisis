using System.Text.RegularExpressions;
using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;
using Genisis.Api.Worksheet.Adjustment;
using Genisis.Api.Worksheet.Enquiry;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Worksheet > Worksheet Adjustment, step 2: the credit / debit note and the Excess letter.</summary>
public class WorksheetDocumentsTests
{
    // ---------------- rules ----------------

    [Theory]
    [InlineData(100, 0, 100)]
    [InlineData(-25.5, 0, -25.5)]
    [InlineData(0, -333.2, -333.2)]
    [InlineData(40, -90, 40)]
    [InlineData(0, 0, 0)]
    public void The_amount_due_is_what_Medix_pays_the_member_or_else_the_excess(double payable, double excess, double expected) =>
        Assert.Equal((decimal)expected, NoteRules.Due(new EnquirySummary { PayableToMember = (decimal)payable, TotalExcess = (decimal)excess }));

    [Theory]
    [InlineData(10, "Credit")]
    [InlineData(-10, "Debit")]
    [InlineData(0, "None")]
    public void The_kind_of_note_follows_the_sign_of_the_amount(double due, string kind) => Assert.Equal(kind, NoteRules.KindOf((decimal)due));

    [Fact]
    public void The_disability_is_the_final_diagnoses_one_per_line_in_order()
    {
        var text = NoteRules.Disability(
        [
            new EnquiryDiagnosis { Source = "First", Slot = 1, Diagnosis = "PRELIM" },
            new EnquiryDiagnosis { Source = "Final", Slot = 2, Diagnosis = " ABSCESS " },
            new EnquiryDiagnosis { Source = "Final", Slot = 1, Diagnosis = "FISTULA" },
            new EnquiryDiagnosis { Source = "Final", Slot = 3, Diagnosis = " " },
        ]);
        Assert.Equal("FISTULA\nABSCESS", text);
    }

    [Theory]
    [InlineData("1", "MAS")]
    [InlineData(" 2 ", "Etiqa")]
    [InlineData("", "Medix")]
    [InlineData(null, "Medix")]
    [InlineData("9", "Medix")]
    public void The_plan_selects_the_excess_letter_template(string? excessPrivate, string template) => Assert.Equal(template, ExcessRules.TemplateOf(excessPrivate));

    [Fact]
    public void The_excess_letter_lists_the_items_that_have_an_amount_and_totals_them_with_the_extras()
    {
        var row = new ExcessRow { RoomAndBoard = 100, Surgical = 50.5m, Others = 20, OthersDescription = " CERADAN CREAM ", LimitExceeded = 10, CoPayment = 5, UndertakenExcess = 2 };
        var items = ExcessRules.Items(row);
        Assert.Equal(["Room & Board", "Surgical Fees", "CERADAN CREAM"], items.Select(i => i.Label));
        Assert.Equal([100m, 50.5m, 20m], items.Select(i => i.Amount));
        Assert.Equal(187.5m, ExcessRules.Total(row));
        Assert.Equal("Others", ExcessRules.Items(new ExcessRow { Others = 1 })[0].Label);
        Assert.Empty(ExcessRules.Items(new ExcessRow()));
    }

    // ---------------- service with fakes ----------------

    private sealed class FakeEnquiry : IEnquiryRepository
    {
        public EnquiryHeader? Header = new() { CaseNo = "AH1 ", MemberNo = "M1", MemberName = "MEMBER", PatientName = "PATIENT", Hospital = "HOSPITAL", PlanCode = "BO48", BordxRefNo = "B9", DebitCreditRefNo = "" };
        public EnquirySummary Summary = new();
        public Task<List<EnquiryCaseRow>> SearchCasesAsync(IDbSession session, EnquiryCaseSearch search, int max) => Task.FromResult(new List<EnquiryCaseRow>());
        public Task<List<EnquiryVersion>> VersionsAsync(IDbSession session, string caseNo) => Task.FromResult(new List<EnquiryVersion>());
        public Task<EnquiryHeader?> HeaderAsync(IDbSession session, string caseNo, int version) => Task.FromResult(Header);
        public Task<List<EnquiryDiagnosis>> DiagnosesAsync(IDbSession session, string caseNo) => Task.FromResult(new List<EnquiryDiagnosis> { new() { Source = "Final", Slot = 1, Diagnosis = "FISTULA" } });
        public Task<List<EnquiryInvoice>> InvoicesAsync(IDbSession session, string caseNo, int? version, string? payTo) => Task.FromResult(new List<EnquiryInvoice>());
        public Task<List<EnquiryBenefit>> BenefitsAsync(IDbSession session, string caseNo, int fromVersion, int toVersion) => Task.FromResult(new List<EnquiryBenefit>());
        public Task<EnquirySummary> SummaryAsync(IDbSession session, string caseNo, int fromVersion, int toVersion) => Task.FromResult(Summary);
        public Task<EnquiryOthers?> OthersAsync(IDbSession session, string caseNo, int version) => Task.FromResult<EnquiryOthers?>(new EnquiryOthers { City = "KL", State = "WP", CreditDebitRemarks = "REMARK" });
        public Task<List<EnquiryLgRow>> LgListAsync(IDbSession session, string caseNo) => Task.FromResult(new List<EnquiryLgRow>());
    }

    private sealed class FakeDocuments : IDocumentsRepository
    {
        public string? Assigned = "DAH00007";
        public int Assigns, Marks;
        public ExcessRow? Excess = new() { ExcessNo = "EAH1-1", Others = 12, ExcessTemplate = "2" };
        public Task<NoteMemberRow?> NoteMemberAsync(IDbSession session, string caseNo, int version) =>
            Task.FromResult<NoteMemberRow?>(new NoteMemberRow { Address1 = "1 Jalan", PostCode = "50000", CompanyPlan = true });
        public Task<string?> AssignDebitNoAsync(IDbSession session, string caseNo, int version) { Assigns++; return Task.FromResult(Assigned); }
        public Task<ExcessRow?> ExcessAsync(IDbSession session, string caseNo, int version) => Task.FromResult(Excess);
        public Task MarkExcessLetterAsync(IDbSession session, string caseNo) { Marks++; return Task.CompletedTask; }
    }

    private static WorksheetDocumentsService Service(FakeSessions sessions, FakeEnquiry enquiry, FakeDocuments documents) =>
        new(sessions, enquiry, documents, NullLogger<WorksheetDocumentsService>.Instance);

    [Fact]
    public async Task A_note_carries_the_worksheet_the_member_and_the_amount_due()
    {
        var enquiry = new FakeEnquiry { Summary = new EnquirySummary { PayableToMember = 150 } };
        enquiry.Header!.DebitCreditRefNo = " CAH00012 ";
        var note = await Service(new FakeSessions(), enquiry, new FakeDocuments()).NoteAsync(" AH1 ", 2);
        Assert.Equal((NoteRules.Credit, 150m, "CAH00012", "AH1", 2), (note.Kind, note.Amount, note.Number, note.CaseNo, note.Version));
        Assert.Equal(("PATIENT", "MEMBER", "HOSPITAL", "FISTULA", "1 Jalan", "KL", "REMARK", true), (note.PatientName, note.MemberName, note.Hospital, note.Disability, note.Address1, note.City, note.Remarks, note.CompanyPlan));
    }

    [Fact]
    public async Task A_debit_note_without_a_number_is_given_one_and_saved()
    {
        var sessions = new FakeSessions();
        var documents = new FakeDocuments();
        var enquiry = new FakeEnquiry { Summary = new EnquirySummary { TotalExcess = -333.2m } };
        var note = await Service(sessions, enquiry, documents).DebitNoteAsync("AH1", 1, "TESTER");
        Assert.Equal((NoteRules.Debit, 333.2m), (note.Kind, note.Amount));
        Assert.Equal(1, documents.Assigns);
        Assert.True(sessions.Last.Committed);
    }

    [Fact]
    public async Task A_debit_note_that_has_a_number_keeps_it_and_one_that_is_not_due_is_refused()
    {
        var documents = new FakeDocuments();
        var enquiry = new FakeEnquiry { Summary = new EnquirySummary { PayableToMember = -20 } };
        enquiry.Header!.DebitCreditRefNo = "DAH00001";
        Assert.Equal("DAH00001", (await Service(new FakeSessions(), enquiry, documents).DebitNoteAsync("AH1", 1, "TESTER")).Number);
        Assert.Equal(0, documents.Assigns);

        var credit = new FakeEnquiry { Summary = new EnquirySummary { PayableToMember = 20 } };
        var sessions = new FakeSessions();
        var error = await Assert.ThrowsAsync<WorksheetException>(() => Service(sessions, credit, documents).DebitNoteAsync("AH1", 1, "TESTER"));
        Assert.Equal("A debit note is for an amount due from the member; there is none on this worksheet.", error.Message);
        Assert.False(sessions.Last.Committed);
    }

    [Fact]
    public async Task A_missing_worksheet_or_version_is_refused_for_notes_and_the_letter()
    {
        var enquiry = new FakeEnquiry { Header = null };
        await Assert.ThrowsAsync<NotFoundException>(() => Service(new FakeSessions(), enquiry, new FakeDocuments()).NoteAsync("X", 1));
        await Assert.ThrowsAsync<NotFoundException>(() => Service(new FakeSessions(), enquiry, new FakeDocuments()).ExcessLetterAsync("X", 1, "TESTER"));
        Assert.Equal("Please select a version nombor!", (await Assert.ThrowsAsync<WorksheetException>(() => Service(new FakeSessions(), new FakeEnquiry(), new FakeDocuments()).NoteAsync("X", 0))).Message);
    }

    [Fact]
    public async Task The_excess_letter_is_made_from_the_excess_record_and_the_case_is_marked()
    {
        var sessions = new FakeSessions();
        var documents = new FakeDocuments();
        var letter = await Service(sessions, new FakeEnquiry(), documents).ExcessLetterAsync("AH1", 1, "TESTER");
        Assert.Equal(("Etiqa", "EAH1-1", "AH1", "PATIENT", 12m), (letter.Template, letter.ExcessNo, letter.CaseNo, letter.PatientName, letter.TotalExcess));
        Assert.Equal(1, documents.Marks);
        Assert.True(sessions.Last.Committed);
    }

    [Fact]
    public async Task A_case_without_an_excess_record_has_no_letter_and_is_not_marked()
    {
        var sessions = new FakeSessions();
        var documents = new FakeDocuments { Excess = null };
        var error = await Assert.ThrowsAsync<NotFoundException>(() => Service(sessions, new FakeEnquiry(), documents).ExcessLetterAsync("AH1", 1, "TESTER"));
        Assert.Equal("There is no excess record for this case.", error.Message);
        Assert.Equal(0, documents.Marks);
        Assert.False(sessions.Last.Committed);
    }
}

/// <summary>The notes and the Excess letter against the development databases. Every write happens in a session that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class WorksheetDocumentsDatabaseTests
{
    private static readonly string? MainDb = TestDb.MainDb;

    [Fact]
    public async Task The_excess_record_of_a_case_is_read_with_its_amounts_and_the_case_can_be_marked()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var repository = new DocumentsRepository();
        await using var session = await TestDb.Sessions().BeginAsync();
        var cas = await session.Connection.QueryFirstAsync<string>(
            """
            SELECT TOP (1) RTRIM(e.CASNumber) FROM genisis.WS_ExcessLetter e
            WHERE genisis.Worksheet_Amount(e.EXCOthers1) > 0 ORDER BY e.CASNumber DESC
            """, transaction: session.Transaction);

        var row = (await repository.ExcessAsync(session, cas, 1))!;
        Assert.False(string.IsNullOrWhiteSpace(row.ExcessNo));
        Assert.True(row.Others > 0);
        Assert.Equal(string.IsNullOrWhiteSpace(row.OthersDescription) ? "Others" : row.OthersDescription.Trim(), ExcessRules.Items(row).Last().Label);
        Assert.True(ExcessRules.Total(row) >= row.Others);

        Assert.Null(await repository.ExcessAsync(session, "NO SUCH CASE", 1));

        var recorded = await session.Connection.QueryFirstAsync<string>("SELECT TOP (1) RTRIM(CaseNo) FROM genisis.Maint_tblRecord WHERE RTRIM(CaseNo) <> ''", transaction: session.Transaction);
        await session.Connection.ExecuteAsync("UPDATE genisis.Maint_tblRecord SET ExcessLetter = NULL WHERE CaseNo = @recorded", new { recorded }, session.Transaction);
        await repository.MarkExcessLetterAsync(session, recorded);
        Assert.Equal("Y", await session.Connection.ExecuteScalarAsync<string>("SELECT TOP (1) RTRIM(ExcessLetter) FROM genisis.Maint_tblRecord WHERE CaseNo = @recorded", new { recorded }, session.Transaction));
    }

    [Fact]
    public async Task A_debit_note_number_is_the_next_of_its_payor_and_is_kept()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var repository = new DocumentsRepository();
        await using var session = await TestDb.Sessions().BeginAsync();
        var (cas, version) = await session.Connection.QueryFirstAsync<(string, int)>(
            "SELECT TOP (1) RTRIM(CASNumber), ClaimVersion FROM genisis.WS_Claim WHERE ISNULL(DebitCreditRefNo, '') = '' AND CASNumber NOT LIKE 'ZZ%' ORDER BY CASNumber DESC", transaction: session.Transaction);
        var payor = cas[..2];
        var before = await session.Connection.ExecuteScalarAsync<int?>("SELECT DRSeq FROM genisis.WS_DRSeq WHERE Payor = @payor", new { payor }, session.Transaction) ?? 0;

        var number = await repository.AssignDebitNoAsync(session, cas, version);
        Assert.Matches(new Regex($"^D{payor}\\d{{5,}}$"), number);
        Assert.Equal($"D{payor}{before + 1:00000}", number);
        Assert.Equal(before + 1, await session.Connection.ExecuteScalarAsync<int>("SELECT DRSeq FROM genisis.WS_DRSeq WHERE Payor = @payor", new { payor }, session.Transaction));
        var saved = await session.Connection.QueryFirstAsync<(string, DateTime)>("SELECT RTRIM(DebitCreditRefNo), DebitCreditDate FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @version", new { cas, version }, session.Transaction);
        Assert.Equal((number, DateTime.Today), saved);

        Assert.Equal(number, await repository.AssignDebitNoAsync(session, cas, version));
        Assert.Equal(before + 1, await session.Connection.ExecuteScalarAsync<int>("SELECT DRSeq FROM genisis.WS_DRSeq WHERE Payor = @payor", new { payor }, session.Transaction));
        Assert.Null(await repository.AssignDebitNoAsync(session, "NO SUCH CASE", 1));
    }

    [Fact]
    public async Task A_note_of_a_worksheet_that_has_a_number_shows_it_with_the_member_and_the_amount_due()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await using var probe = await TestDb.Sessions().OpenAsync();
        var row = await probe.Connection.QueryFirstOrDefaultAsync<(string Cas, int Version, string Ref)>(
            """
            SELECT TOP (1) RTRIM(v.CASNumber), v.ClaimVersion, RTRIM(v.DebitCreditRefNo) FROM dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v
            WHERE v.MBMNumber IS NOT NULL AND ISNULL(v.DebitCreditRefNo, '') <> '' AND ISNULL(RTRIM(v.MBMName), '') <> '' ORDER BY v.CASNumber DESC, v.ClaimVersion
            """, transaction: probe.Transaction);
        if (row.Cas is null) return;

        var service = new WorksheetDocumentsService(TestDb.Sessions(), new EnquiryRepository(), new DocumentsRepository(), NullLogger<WorksheetDocumentsService>.Instance);
        var note = await service.NoteAsync(row.Cas, row.Version);
        Assert.Equal(row.Ref, note.Number);
        Assert.Equal((row.Cas, row.Version), (note.CaseNo, note.Version));
        Assert.False(string.IsNullOrWhiteSpace(note.MemberName));
        Assert.True(note.Amount >= 0);
        Assert.Contains(note.Kind, new[] { NoteRules.Credit, NoteRules.Debit, NoteRules.None });
    }
}

/// <summary>A version that does not exist has nothing to evaluate: the engine reports that with <c>null</c> instead of failing.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class WorksheetEngineMissingVersionTests
{
    [Fact]
    public async Task A_missing_version_has_no_summary_inputs()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        var service = new Genisis.Api.Worksheet.Engine.WorksheetEngineService(TestDb.Sessions(), new Genisis.Api.Worksheet.Engine.WorksheetEngineRepository());
        Assert.Null(await service.FinalSavedAsync("NO SUCH CASE", 1));
    }
}
