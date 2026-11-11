using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Finance;
using Genisis.Api.Finance.SunPosting;
using Genisis.Api.Finance.SunPostingHosp;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Posting > Invoice > Hospital: the journal rules, the validators and the service, against a fake repository.</summary>
public class SunPostingHospRulesTests
{
    private sealed class FakeClock(DateTime now) : IFinanceClock
    {
        public DateTime Now => now;
    }

    private sealed class FakeRepository : ISunPostingHospRepository
    {
        public PostingWindow Window { get; set; } = new();

        public List<SunPostingHospRow> Rows { get; set; } = [];

        public Dictionary<string, int> Results { get; } = [];

        public List<string> Calls { get; } = [];

        public Task<(List<CodeName> HealthTypes, List<PayorOption> Payors, List<CodeName> Hospitals, List<CodeName> Periods)> LookupsAsync(IDbSession session) =>
            Task.FromResult((new List<CodeName> { new("N", "NON") }, new List<PayorOption> { new("AC", "ALLIANZ", "N") }, new List<CodeName> { new("H1", "HOSPITAL ONE") }, new List<CodeName> { new("10/2026", "10/2026") }));

        public Task<PostingWindow> WindowAsync(IDbSession session, string userCode) => Task.FromResult(Window);

        public Task<List<SunPostingHospRow>> SearchAsync(IDbSession session, SunPostingHospFilter filter)
        {
            Calls.Add($"search batch={filter.BatchNoFrom} post={filter.PostStatus}");
            return Task.FromResult(Rows);
        }

        public Task<string> NextBatchAsync(IDbSession session)
        {
            Calls.Add("batch");
            return Task.FromResult("5001");
        }

        public Task<int> InsertAsync(IDbSession session, string invoiceIndex, string accMonth, string accYear, string batchNo, string userCode)
        {
            Calls.Add($"insert {invoiceIndex} {accMonth}/{accYear} {batchNo}");
            return Task.FromResult(Results.GetValueOrDefault(invoiceIndex));
        }
    }

    private static readonly DateTime Afternoon = new(2026, 10, 30, 17, 30, 0);

    private static SunPostingHospService Service(FakeRepository repository, FakeSessions sessions, DateTime? now = null) => new(sessions, repository, new FakeClock(now ?? Afternoon), NullLogger<SunPostingHospService>.Instance);

    private static SunPostingHospRow Row(string index = "7", string health = "N", decimal amount = 100m, bool posted = false, string? patient = "A VERY LONG PATIENT NAMEXYZ") =>
        new()
        {
            InvoiceIndex = index, InvoiceNo = "I18-1", CaseNo = "AC00001618", ClaimVersion = "2", PatientName = patient, HealthType = health, Payor = "AC", InvoiceDate = new DateTime(2021, 8, 4), Amount = amount, BatchNo = posted ? "4949" : null,
            InvHStatus = posted ? "1" : null, AccMonth = posted ? "02" : null, AccYear = posted ? "2021" : null, BordxRefNo = "NAC0002381", HospitalAccCode = "83G004", HospitalName = "GENERAL PRACTITIONER", ProductCategory = "NS",
        };

    [Fact]
    public void The_posting_list_has_a_debit_to_the_payor_and_a_credit_to_the_hospital_for_every_invoice()
    {
        var lines = SunJournalHosp.Build([Row()], SunPostingCodes.ModeAll);
        Assert.Equal(2, lines.Count);
        var (debit, credit) = (lines[0], lines[1]);
        Assert.Equal(("RC", "I18-1", "6000AC", "D", "A VERY LONG PATIENT NAMEX", "3G004", "00001618-2", "NS", "0002381"), (debit.JournalType, debit.TransRef, debit.AccCode, debit.DebitCredit, debit.Description, debit.Hospital, debit.CaseNo, debit.ProductCategory, debit.BordxRef));
        Assert.Equal(("83G004", "C", "7", 100m), (credit.AccCode, credit.DebitCredit, credit.Index, credit.Amount));
        Assert.Null(debit.Index);
        Assert.Equal(("7", ""), (credit.InvoiceCaseNo, credit.InvoiceVersion));
        Assert.Equal("SM", SunJournalHosp.Build([Row(health: "S")], SunPostingCodes.ModeAll)[0].ProductCategory);
    }

    [Fact]
    public void The_credit_and_debit_lists_have_one_line_with_the_case_number_alone_and_a_posted_invoice_is_marked_with_its_period()
    {
        var credit = SunJournalHosp.Build([Row(posted: true)], SunPostingCodes.ModeCredit).Single();
        Assert.Equal(("83G004", "C", "00001618", "Post", "02/2021", "4949"), (credit.AccCode, credit.DebitCredit, credit.CaseNo, credit.Status, credit.AccPeriod, credit.BatchNo));
        var debit = SunJournalHosp.Build([Row()], SunPostingCodes.ModeDebit).Single();
        Assert.Equal(("6000AC", "D", "", null), (debit.AccCode, debit.DebitCredit, debit.Status, debit.AccPeriod));
        var result = SunJournalHosp.Result([Row(amount: 40m), Row("8", amount: 60m, posted: true)], SunPostingCodes.ModeAll);
        Assert.Equal((2, 100m, 100m, true, false), (result.Invoices, result.DebitTotal, result.CreditTotal, result.Posted, result.Truncated));
        Assert.Equal("", SunJournalHosp.HospitalColumn(null));
        Assert.Equal("3G004", SunJournalHosp.HospitalColumn(" 83G004 "));
        Assert.Equal("4949 RC-HospInv 20261030.csv", SunJournalHosp.FileName("4949", new DateTime(2026, 10, 30)));
    }

    [Fact]
    public void Post_checks_come_in_the_desktops_order()
    {
        var validator = new SunPostHospRequestValidator();
        Assert.Equal("Please select the record", validator.FirstError(new SunPostHospRequest()));
        Assert.Equal("Please Enter Invoice Date(To)", validator.FirstError(new SunPostHospRequest { InvoiceIndexes = ["7"] }));
        Assert.Equal("Please Change Bordx Status", validator.FirstError(new SunPostHospRequest { InvoiceIndexes = ["7"], InvoiceDateTo = Afternoon, BordxStatus = "0" }));
        Assert.Equal("Please Keyin for Accounting Period ! ", validator.FirstError(new SunPostHospRequest { InvoiceIndexes = ["7"], InvoiceDateTo = Afternoon, BordxStatus = "1" }));
        Assert.Equal("Select a valid accounting period.", validator.FirstError(new SunPostHospRequest { InvoiceIndexes = ["7"], InvoiceDateTo = Afternoon, BordxStatus = "1", AccPeriod = "13/2026" }));
        Assert.Null(validator.FirstError(new SunPostHospRequest { InvoiceIndexes = ["7"], InvoiceDateTo = Afternoon, BordxStatus = "1", AccPeriod = "10/2026" }));
        Assert.NotNull(new SunPostingHospFilterValidator().FirstError(new SunPostingHospFilter { Mode = "x" }));
        Assert.Null(new SunPostingHospFilterValidator().FirstError(new SunPostingHospFilter()));
        Assert.Equal("Please Enter Batch No", new SunHospJournalQueryValidator().FirstError(new SunHospJournalQuery()));
    }

    private static SunPostHospRequest Post(params string[] indexes) => new() { AccPeriod = "10/2026", InvoiceDateTo = Afternoon, BordxStatus = "1", InvoiceIndexes = [.. indexes] };

    [Fact]
    public async Task Post_takes_one_batch_inserts_each_invoice_once_in_one_transaction_and_returns_the_batchs_journal()
    {
        var repository = new FakeRepository { Rows = [Row()] };
        var sessions = new FakeSessions();
        var result = await Service(repository, sessions).PostAsync(Post("7", " 8 ", "7"), "U1");
        Assert.Equal(["batch", "insert 7 10/2026 5001", "insert 8 10/2026 5001", "search batch=5001 post="], repository.Calls);
        Assert.True(sessions.Sessions.Single(x => x.Transactional).Committed);
        Assert.Equal(("5001", 2, "Record(s) Posted Sucessfully... ! "), (result.BatchNo, result.Posted, result.Message));
        Assert.Equal(2, result.Journal.Lines.Count);
    }

    [Fact]
    public async Task Post_is_refused_while_the_window_is_closed_unless_the_user_has_special_access_and_when_an_invoice_is_gone_or_posted()
    {
        var closed = new FakeRepository { Window = new PostingWindow { EndTime = new DateTime(1900, 1, 1, 18, 0, 0) } };
        Assert.Contains("Posting is ended at", (await Assert.ThrowsAsync<FinanceException>(() => Service(closed, new FakeSessions()).PostAsync(Post("7"), "U1"))).Message);
        Assert.Empty(closed.Calls);
        closed.Window = new PostingWindow { EndTime = new DateTime(1900, 1, 1, 18, 0, 0), SpecialAccess = "Y" };
        closed.Rows = [Row()];
        Assert.Equal(1, (await Service(closed, new FakeSessions()).PostAsync(Post("7"), "U1")).Posted);

        var gone = new FakeRepository();
        gone.Results["7"] = StoredProcedures.NotFound;
        var sessions = new FakeSessions();
        Assert.Equal("Invoice 7 was not found. Search again.", (await Assert.ThrowsAsync<FinanceException>(() => Service(gone, sessions).PostAsync(Post("7"), "U1"))).Message);
        Assert.False(sessions.Last.Committed);
        var posted = new FakeRepository();
        posted.Results["7"] = StoredProcedures.Conflict;
        Assert.Equal("7: Record(s) has been posted ! ", (await Assert.ThrowsAsync<FinanceException>(() => Service(posted, new FakeSessions()).PostAsync(Post("7"), "U1"))).Message);
    }

    [Fact]
    public async Task The_journal_file_is_the_batchs_csv_and_nothing_to_print_is_not_found()
    {
        var repository = new FakeRepository();
        Assert.Equal("Report cannot be printed without any record.", (await Assert.ThrowsAsync<NotFoundException>(() => Service(repository, new FakeSessions()).JournalFileAsync(new SunHospJournalQuery { BatchNo = "4949" }))).Message);
        repository.Rows = [Row(posted: true)];
        var file = await Service(repository, new FakeSessions()).JournalFileAsync(new SunHospJournalQuery { BatchNo = "4949", FileDate = new DateTime(2026, 10, 29) });
        Assert.Equal("4949 RC-HospInv 20261029.csv", file.FileName);
        var text = System.Text.Encoding.UTF8.GetString(file.Content);
        Assert.Contains("Chk,J/T,J/S,DB,Trans.Ref.", text);
        Assert.Contains("Post,RC,,MED,I18-1,02/2021,04/08/2021,A VERY LONG PATIENT NAMEX,6000AC,100.00,D,3G004,,AC,00001618-2,NS,0002381,4949,", text);
        Assert.Contains("Post,RC,,MED,I18-1,02/2021,04/08/2021,A VERY LONG PATIENT NAMEX,83G004,100.00,C,3G004,,AC,00001618-2,NS,0002381,4949,7", text);
        var lookups = await Service(repository, new FakeSessions()).LookupsAsync("U1");
        Assert.Equal(("AC", "N"), (lookups.Payors[0].Code, lookups.Payors[0].HealthType));
        Assert.Null(lookups.PostingClosedMessage);
    }
}

/// <summary>Hospital invoice posting against the database. The write is made in a session that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class SunPostingHospDatabaseTests
{
    private static SunPostingHospRepository Repository => new();

    private static async Task<long> Count(IDbSession s, string sql, object? args = null) => await s.Connection.ExecuteScalarAsync<long>(sql, args, s.Transaction);

    private const string Checksum = "SELECT CAST((SELECT ISNULL(CHECKSUM_AGG(CHECKSUM(PostIndex, INVIndex, InvHStatus, PostType, BatchNo)), 0) + COUNT(*) FROM genisis.WS_SUNPosting) AS bigint) + (SELECT ISNULL(CHECKSUM_AGG(CHECKSUM(BatchNo)), 0) FROM genisis.WS_SunPostBatch)";

    [Fact]
    public async Task The_search_gives_the_view_by_post_status_hospital_and_batch_and_the_lookups_are_filled()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var unposted = await Repository.SearchAsync(session, new SunPostingHospFilter { Payor = "AC", PostStatus = "0", BordxStatus = "1" });
        Assert.NotEmpty(unposted);
        Assert.All(unposted, r => Assert.True(!r.Posted && r.Payor == "AC" && !string.IsNullOrEmpty(r.BordxRefNo)));
        Assert.Equal(unposted.Count, await Count(session, "SELECT COUNT(*) FROM genisis.WS_SunPostingHospInv WHERE PAYCode = 'AC' AND (InvHStatus IS NULL OR InvHStatus = '0') AND (PostStatus IS NULL OR PostStatus = '0') AND INVBordxRefNo <> ''"));
        var posted = await Repository.SearchAsync(session, new SunPostingHospFilter { PostStatus = "1", BatchNoFrom = "4949", BatchNoTo = "4949", BordxStatus = null });
        Assert.NotEmpty(posted);
        Assert.All(posted, r => Assert.True(r.Posted && r.BatchNo == "4949"));
        var hospital = posted[0].HospitalName;
        Assert.All(await Repository.SearchAsync(session, new SunPostingHospFilter { Hospital = hospital, BatchNoFrom = "4949", BatchNoTo = "4949", PostStatus = null, BordxStatus = null }), r => Assert.Equal(hospital, r.HospitalName));
        var one = await Repository.SearchAsync(session, new SunPostingHospFilter { InvoiceNoFrom = posted[0].InvoiceNo, InvoiceNoTo = posted[0].InvoiceNo, PostStatus = null, BordxStatus = null });
        Assert.Contains(one, r => r.InvoiceIndex == posted[0].InvoiceIndex);
        Assert.Empty(await Repository.SearchAsync(session, new SunPostingHospFilter { BatchNoFrom = "ZZZZZ", PostStatus = null, BordxStatus = null }));
        var (health, payors, hospitals, periods) = await Repository.LookupsAsync(session);
        Assert.True(health.Count > 0 && payors.Count > 0 && hospitals.Count > 0 && periods.Count > 0);
        Assert.Contains(payors, p => p.Code == "AC" && !string.IsNullOrEmpty(p.HealthType));
    }

    [Fact]
    public async Task Posting_an_invoice_adds_one_SUNPosting_row_an_invoice_posted_already_is_refused_and_nothing_survives_the_rollback()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var before = await Count(read, Checksum);
        var invoice = (await Repository.SearchAsync(read, new SunPostingHospFilter { Payor = "AC", PostStatus = "0", BordxStatus = "1" }))[0];
        var posted = (await Repository.SearchAsync(read, new SunPostingHospFilter { PostStatus = "1", BatchNoFrom = "4949", BatchNoTo = "4949", BordxStatus = null }))[0];
        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var batch = await Repository.NextBatchAsync(write);
            Assert.Equal(0, await Repository.InsertAsync(write, invoice.InvoiceIndex, "10", "2026", batch, "E2E"));
            Assert.Equal(1, await Count(write, "SELECT COUNT(*) FROM genisis.WS_SUNPosting WHERE INVIndex = @i AND PostType = 'InvHosp' AND InvHStatus = '1' AND PostStatus = '1' AND BatchNo = @b AND AccMonth = '10' AND AccYear = '2026' AND PostBy = 'E2E'", new { i = invoice.InvoiceIndex, b = batch }));
            Assert.Equal(StoredProcedures.Conflict, await Repository.InsertAsync(write, invoice.InvoiceIndex, "10", "2026", batch, "E2E"));
            Assert.Equal(StoredProcedures.Conflict, await Repository.InsertAsync(write, posted.InvoiceIndex, "10", "2026", batch, "E2E"));
            Assert.Equal(StoredProcedures.NotFound, await Repository.InsertAsync(write, "999999999", "10", "2026", batch, "E2E"));
            var listed = await Repository.SearchAsync(write, new SunPostingHospFilter { BatchNoFrom = batch, BatchNoTo = batch, PostStatus = null, BordxStatus = null });
            Assert.Single(listed);
            Assert.True(listed[0].Posted);
        }

        Assert.Equal(before, await Count(read, Checksum));
    }
}
