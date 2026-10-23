using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dapper;
using Genisis.Api.Data;
using Genisis.Api.Finance;
using Genisis.Api.Finance.SunPosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Finance &gt; SUN Posting: journal rules, posting window, validation and the service's unit of work.</summary>
public class FinanceSunPostingTests
{
    private static SunPostingRow Row(string cas = "ST00073658", string version = "1", decimal amount = 120.50m, string? revInd = null,
        string coverId = "00", string health = "N", string? proCat = "IP", string? invM = null) => new()
    {
        CaseNo = cas, ClaimVersion = version, MembershipNo = "STHB2059503*04", PatientCoverId = coverId,
        MemberName = "A VERY LONG PRINCIPAL MEMBER NAME FOR TESTING", PatientName = coverId == "00" ? "A VERY LONG PRINCIPAL MEMBER NAME FOR TESTING" : "CHILD ONE",
        InvMStatus = invM, HealthType = health, Payor = "ST", GroupCompany = "ACME SDN BHD", InvoiceDate = new DateTime(2021, 7, 1),
        Amount = amount, BatchNo = invM == "1" ? "5031" : null, BordxRefNo = "NST0156046", BordxReversal = revInd, ProductCategory = proCat,
        AccMonth = invM == "1" ? "08" : null, AccYear = invM == "1" ? "2021" : null,
    };

    // ---------------- journal rules ----------------

    [Fact]
    public void A_positive_invoice_becomes_a_credit_to_8200_and_a_debit_to_6000()
    {
        var lines = SunJournal.Build([Row()], SunPostingCodes.ModeAll, SunPostingCodes.Positive);
        Assert.Equal(2, lines.Count);
        var (first, second) = (lines[0], lines[1]);
        Assert.Equal(("MJC", "8200ST", "C", 120.50m), (first.JournalType, first.AccCode, first.DebitCredit, first.Amount));
        Assert.Equal(("MJC", "6000ST", "D", 120.50m), (second.JournalType, second.AccCode, second.DebitCredit, second.Amount));
        Assert.Equal("ST00073658-1", first.TransRef);
        Assert.Equal("ST00073658-1", first.Index);
        Assert.Null(second.Index);
        Assert.Equal(("MED", "0R", "00073658", "IP", "0156046"), (first.Database, first.Hospital, first.CaseNo, first.ProductCategory, first.BordxRef));
        Assert.Equal(25, first.Description.Length);
        Assert.Equal("ACME SDN BHD", second.GroupCompany);
    }

    [Fact]
    public void A_negative_invoice_is_shown_positive_and_a_bordx_reversal_goes_to_6200()
    {
        var lines = SunJournal.Build([Row(amount: -80m, revInd: "Y"), Row(cas: "ST00000002", amount: -10m)], SunPostingCodes.ModeAll, SunPostingCodes.Negative);
        Assert.Equal(("MJD", "6200ST", "D", 80m), (lines[0].JournalType, lines[0].AccCode, lines[0].DebitCredit, lines[0].Amount));
        Assert.Equal(("6000ST", "C", 80m), (lines[1].AccCode, lines[1].DebitCredit, lines[1].Amount));
        Assert.Equal("8200ST", lines[2].AccCode);
    }

    [Fact]
    public void The_credit_and_debit_lists_have_one_line_per_invoice_with_the_stored_amount()
    {
        var credit = SunJournal.Build([Row(coverId: "02")], SunPostingCodes.ModeCredit, SunPostingCodes.Positive);
        var debit = SunJournal.Build([Row(coverId: "02")], SunPostingCodes.ModeDebit, SunPostingCodes.Negative);
        Assert.Equal(("8200ST", "C", 120.50m), (Assert.Single(credit).AccCode, credit[0].DebitCredit, credit[0].Amount));
        Assert.Equal(("6000ST", "D", 120.50m, "CHILD ONE"), (Assert.Single(debit).AccCode, debit[0].DebitCredit, debit[0].Amount, debit[0].Description));
        Assert.Null(credit[0].GroupCompany);
    }

    [Fact]
    public void Health_type_s_is_product_category_sm_otherwise_the_plan_category()
    {
        Assert.Equal("SM", SunJournal.ProductCategory(Row(health: "S", proCat: "IP")));
        Assert.Equal("IP", SunJournal.ProductCategory(Row(health: "N", proCat: " IP ")));
        Assert.Equal("", SunJournal.ProductCategory(Row(health: "N", proCat: null)));
    }

    [Fact]
    public void The_result_totals_each_side_flags_posted_lists_and_reports_a_cut_off()
    {
        var result = SunJournal.Result([Row(), Row(cas: "ST2", amount: 10m, invM: "1")], SunPostingCodes.ModeAll, SunPostingCodes.Positive);
        Assert.Equal((2, 130.50m, 130.50m, true, false), (result.Invoices, result.DebitTotal, result.CreditTotal, result.Posted, result.Truncated));
        Assert.Equal("Post", result.Lines[2].Status);
        Assert.Equal("08/2021", result.Lines[2].AccPeriod);

        var many = Enumerable.Range(0, FinanceLimits.FetchRows).Select(i => Row(cas: $"ST{i:D8}")).ToList();
        var capped = SunJournal.Result(many, SunPostingCodes.ModeAll, SunPostingCodes.Positive);
        Assert.True(capped.Truncated);
        Assert.Equal(FinanceLimits.MaxRows, capped.Invoices);
    }

    [Fact]
    public void The_journal_file_keeps_the_desktop_name_and_columns()
    {
        Assert.Equal("5031 MJC-Member 20210831.csv", SunJournal.FileName("5031", "P", new DateTime(2021, 8, 31)));
        Assert.Equal("5031 MJD-Member 20210831.csv", SunJournal.FileName("50/31", "N", new DateTime(2021, 8, 31)));
        var text = Encoding.UTF8.GetString(SunJournal.Csv(SunJournal.Build([Row()], SunPostingCodes.ModeAll, "P"))).TrimStart('\uFEFF');
        var rows = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length > 1 ? text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries) : text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("Chk,J/T,J/S,DB,Trans.Ref.,AccPeriod,Trans.Date,Description,AccCode,Amt,D/C", rows[0]);
        Assert.Equal(3, rows.Length);
        Assert.Contains(",01/07/2021,", rows[1]);
        Assert.Contains(",120.50,C,0R,", rows[1]);
    }

    // ---------------- posting window ----------------

    [Theory]
    [InlineData(null, "08:59", true)]
    [InlineData(null, "09:00", true)]
    [InlineData(null, "09:01", false)]
    [InlineData("Y", "08:00", false)]
    [InlineData("y", "08:00", false)]
    public void Posting_is_closed_until_after_the_end_time_unless_the_user_has_special_access(string? special, string time, bool closed)
    {
        var window = new PostingWindow { SpecialAccess = special, EndTime = new DateTime(1899, 12, 30, 9, 0, 0) };
        var now = DateTime.Parse($"2026-10-21 {time}", System.Globalization.CultureInfo.InvariantCulture);
        var message = SunPostingWindow.ClosedMessage(window, now);
        Assert.Equal(closed, message is not null);
        if (closed) Assert.Equal("Posting is ended at 09:00:00 AM ! ", message);
        Assert.Null(SunPostingWindow.ClosedMessage(new PostingWindow(), now));
    }

    // ---------------- validation ----------------

    private static SunPostRequest ValidPost() => new()
    {
        AccPeriod = "08/2021", PostType = "P", InvoiceDateTo = new DateTime(2021, 8, 31), BordxStatus = "1", Items = [new("ST00073658", "1")],
    };

    [Fact]
    public void Post_checks_follow_the_desktop_order_and_messages()
    {
        var validator = new SunPostRequestValidator();
        Assert.Null(validator.FirstError(ValidPost()));
        var all = new SunPostRequest { AccPeriod = null, InvoiceDateTo = null, BordxStatus = "0", Items = [] };
        Assert.Equal("Please select the record", validator.FirstError(all));
        all.Items = [new("X", "1")];
        Assert.Equal("Please Enter Invoice Date(To)", validator.FirstError(all));
        all.InvoiceDateTo = DateTime.Today;
        Assert.Equal("Please change Bordx Status", validator.FirstError(all));
        all.BordxStatus = "1";
        Assert.Equal("Please Keyin for Accounting Period ! ", validator.FirstError(all));
        all.AccPeriod = "13/2021";
        Assert.Equal("Select a valid accounting period.", validator.FirstError(all));
    }

    [Fact]
    public void Search_and_journal_queries_only_take_the_fixed_codes()
    {
        Assert.Null(new SunPostingFilterValidator().FirstError(new SunPostingFilter()));
        Assert.Equal("Select a valid search.", new SunPostingFilterValidator().FirstError(new SunPostingFilter { Mode = "x" }));
        Assert.Equal("Select a valid post type.", new SunPostingFilterValidator().FirstError(new SunPostingFilter { PostType = "Z" }));
        Assert.Equal("Please Enter Batch No", new SunJournalQueryValidator().FirstError(new SunJournalQuery()));
    }

    // ---------------- service ----------------

    private sealed class FakeClock(DateTime now) : IFinanceClock
    {
        public DateTime Now => now;
    }

    private sealed class FakeRepository : ISunPostingRepository
    {
        public PostingWindow Window { get; set; } = new() { SpecialAccess = "N", EndTime = new DateTime(1899, 12, 30, 9, 0, 0) };
        public Dictionary<string, int> Results { get; } = [];
        public List<(string Key, string Month, string Year, string Batch, string User)> Inserted { get; } = [];
        public List<int?> Signs { get; } = [];
        public List<SunPostingRow> Rows { get; set; } = [];
        public int Batches { get; private set; }

        public Task<(List<CodeName> HealthTypes, List<CodeName> Payors, List<CodeName> Periods)> LookupsAsync(IDbSession session) =>
            Task.FromResult<(List<CodeName>, List<CodeName>, List<CodeName>)>(([new("N", "NORMAL")], [new("ST", "STM")], [new("08/2021", "08/2021")]));

        public Task<PostingWindow> WindowAsync(IDbSession session, string userCode) => Task.FromResult(Window);

        public Task<List<SunPostingRow>> SearchAsync(IDbSession session, SunPostingFilter filter, int? amountSign)
        {
            Signs.Add(amountSign);
            return Task.FromResult(Rows);
        }

        public Task<string> NextBatchAsync(IDbSession session) => Task.FromResult((5031 + Batches++).ToString());

        public Task<int> InsertAsync(IDbSession session, SunPostingPick pick, string accMonth, string accYear, string batchNo, string userCode)
        {
            Inserted.Add((pick.Key, accMonth, accYear, batchNo, userCode));
            return Task.FromResult(Results.GetValueOrDefault(pick.Key, StoredProcedures.Ok));
        }
    }

    private static readonly DateTime AfterNine = new(2026, 10, 21, 10, 0, 0);

    private static SunPostingService Service(FakeSessions sessions, FakeRepository repository, DateTime? now = null) =>
        new(sessions, repository, new FakeClock(now ?? AfterNine), NullLogger<SunPostingService>.Instance);

    [Fact]
    public async Task Posting_takes_one_batch_posts_each_invoice_once_and_commits()
    {
        var sessions = new FakeSessions();
        var repository = new FakeRepository { Rows = [Row(invM: "1")] };
        var request = ValidPost();
        request.Items = [new("ST00073658", "1"), new(" ST00073658 ", "1 "), new("ST00000002", "2")];

        var result = await Service(sessions, repository).PostAsync(request, "TESTER");

        Assert.Equal(("5031", 2), (result.BatchNo, result.Posted));
        Assert.Equal("Record(s) Posted Sucessfully... ! ", result.Message);
        Assert.Equal([("ST00073658-1", "08", "2021", "5031", "TESTER"), ("ST00000002-2", "08", "2021", "5031", "TESTER")], repository.Inserted);
        Assert.True(sessions.Sessions.Single(s => s.Transactional).Committed);
        Assert.Equal(2, result.Journal.Lines.Count);
    }

    [Theory]
    [InlineData(StoredProcedures.Conflict, "ST00000002-2: Record(s) has been posted ! ")]
    [InlineData(StoredProcedures.NotFound, "Invoice ST00000002-2 was not found. Search again.")]
    public async Task A_refused_invoice_stops_the_post_and_nothing_is_saved(int code, string message)
    {
        var sessions = new FakeSessions();
        var repository = new FakeRepository();
        repository.Results["ST00000002-2"] = code;
        var request = ValidPost();
        request.Items = [new("ST00073658", "1"), new("ST00000002", "2")];

        var error = await Assert.ThrowsAsync<FinanceException>(() => Service(sessions, repository).PostAsync(request, "TESTER"));

        Assert.Equal(message, error.Message);
        Assert.True(sessions.Last.RolledBack);
        Assert.False(sessions.Last.Committed);
    }

    [Fact]
    public async Task Posting_is_refused_before_the_window_opens_without_taking_a_batch()
    {
        var sessions = new FakeSessions();
        var repository = new FakeRepository();
        var error = await Assert.ThrowsAsync<FinanceException>(() => Service(sessions, repository, new DateTime(2026, 10, 21, 8, 30, 0)).PostAsync(ValidPost(), "TESTER"));
        Assert.Equal("Posting is ended at 09:00:00 AM ! ", error.Message);
        Assert.Equal(0, repository.Batches);
        Assert.Empty(repository.Inserted);
    }

    [Fact]
    public async Task The_search_filters_the_amount_sign_for_the_posting_list_only()
    {
        var repository = new FakeRepository { Rows = [Row()] };
        var service = Service(new FakeSessions(), repository);
        await service.SearchAsync(new SunPostingFilter { PostType = "P" });
        await service.SearchAsync(new SunPostingFilter { PostType = "N" });
        var credit = await service.SearchAsync(new SunPostingFilter { Mode = "credit", PostType = "N" });
        Assert.Equal([1, -1, null], repository.Signs);
        Assert.Single(credit.Lines);
    }

    [Fact]
    public async Task Lookups_report_whether_posting_is_open()
    {
        var repository = new FakeRepository();
        Assert.Null((await Service(new FakeSessions(), repository).LookupsAsync("TESTER")).PostingClosedMessage);
        var closed = await Service(new FakeSessions(), repository, new DateTime(2026, 10, 21, 7, 0, 0)).LookupsAsync("TESTER");
        Assert.Equal("Posting is ended at 09:00:00 AM ! ", closed.PostingClosedMessage);
        Assert.Equal(["P", "N"], closed.PostTypes.Select(t => t.Code));
    }

    [Fact]
    public async Task A_batch_journal_reads_its_post_type_from_the_amounts_when_none_is_given()
    {
        Assert.Equal("N", SunJournal.PostTypeOf([Row(amount: 5m), Row(cas: "ST2", amount: -5m)]));
        Assert.Equal("P", SunJournal.PostTypeOf([Row(amount: 5m)]));
        var repository = new FakeRepository { Rows = [Row(amount: -115m, invM: "1")] };
        var file = await Service(new FakeSessions(), repository).JournalFileAsync(new SunJournalQuery { BatchNo = "5028", FileDate = new DateTime(2021, 8, 5) });
        Assert.Equal("5028 MJD-Member 20210805.csv", file.FileName);
        Assert.Contains(",115.00,D,", Encoding.UTF8.GetString(file.Content));
    }

    [Fact]
    public async Task An_empty_batch_has_no_journal_file()
    {
        var error = await Assert.ThrowsAsync<Genisis.Api.Common.NotFoundException>(() =>
            Service(new FakeSessions(), new FakeRepository()).JournalFileAsync(new SunJournalQuery { BatchNo = "9999" }));
        Assert.Equal("Report cannot be printed without any record.", error.Message);
    }
}

/// <summary>Finance &gt; SUN Posting against the development databases (GENISIS_TEST_MAINDB); writes are rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class FinanceSunPostingDatabaseTests
{
    private static readonly string? MainDb = TestDb.MainDb;

    [Fact]
    public async Task Lookups_and_a_posted_batch_search_return_rows()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var repository = new SunPostingRepository();
        await using var session = await TestDb.Sessions().OpenAsync();
        var (health, payors, periods) = await repository.LookupsAsync(session);
        Assert.NotEmpty(health);
        Assert.NotEmpty(payors);
        Assert.All(periods, p => Assert.Matches(@"^\d{2}/\d{4}$", p.Code));

        var batch = await session.Connection.ExecuteScalarAsync<string?>(
            "SELECT TOP 1 RTRIM(BatchNo) FROM genisis.WS_SUNPosting WHERE PostType = N'InvMBM' AND InvMStatus = '1' ORDER BY PostIndex DESC");
        if (batch is null) return;
        var rows = await repository.SearchAsync(session, new SunPostingFilter { BatchNoFrom = batch, BatchNoTo = batch, PostStatus = "1", BordxStatus = null }, null);
        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.True(r.Posted && r.BatchNo == batch));
    }

    [Fact]
    public async Task Posting_takes_the_next_batch_refuses_a_second_post_and_rolls_back()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var repository = new SunPostingRepository();
        var sessions = TestDb.Sessions();
        string before;
        await using (var read = await sessions.OpenAsync())
            before = await read.Connection.ExecuteScalarAsync<string>("SELECT MAX(BatchNo) FROM genisis.WS_SunPostBatch") ?? "";

        await using (var session = await sessions.BeginAsync())
        {
            var invoice = await session.Connection.QueryFirstOrDefaultAsync<(string Cas, string Version)>(
                "SELECT TOP 1 RTRIM(CASNumber), RTRIM(INVWSversion) FROM genisis.WS_SunPostingMbmInv WHERE InvMStatus IS NULL AND INVAmount > 0",
                transaction: session.Transaction);
            if (invoice.Cas is null) return;
            var batch = await repository.NextBatchAsync(session);
            Assert.Equal(int.Parse(before) + 1, int.Parse(batch));
            var pick = new SunPostingPick(invoice.Cas, invoice.Version);
            Assert.Equal(StoredProcedures.Ok, await repository.InsertAsync(session, pick, "08", "2021", batch, "TESTER"));
            Assert.Equal(StoredProcedures.Conflict, await repository.InsertAsync(session, pick, "08", "2021", batch, "TESTER"));
            Assert.Equal(StoredProcedures.NotFound, await repository.InsertAsync(session, new SunPostingPick("XX99999999", "1"), "08", "2021", batch, "TESTER"));
            var posted = await repository.SearchAsync(session, new SunPostingFilter { BatchNoFrom = batch, BatchNoTo = batch, PostStatus = "1", BordxStatus = null }, null);
            Assert.Contains(posted, r => r.CaseNo == invoice.Cas && r.ClaimVersion == invoice.Version);
        }

        await using var after = await sessions.OpenAsync();
        Assert.Equal(before, await after.Connection.ExecuteScalarAsync<string>("SELECT MAX(BatchNo) FROM genisis.WS_SunPostBatch"));
    }
}

/// <summary>Finance &gt; SUN Posting over HTTP: login, menu right 13 and the first validation message.</summary>
public class FinanceSunPostingApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly (HttpMethod Method, string Url)[] Routes =
    [
        (HttpMethod.Get, "/api/finance/sun-posting/lookups"),
        (HttpMethod.Get, "/api/finance/sun-posting/search"),
        (HttpMethod.Post, "/api/finance/sun-posting/post"),
        (HttpMethod.Get, "/api/finance/sun-posting/journal?batchNo=1"),
    ];

    private static HttpRequestMessage Request((HttpMethod Method, string Url) route, string json = "{}") =>
        new(route.Method, route.Url) { Content = route.Method == HttpMethod.Post ? new StringContent(json, Encoding.UTF8, "application/json") : null };

    [Fact]
    public async Task Every_endpoint_needs_a_login_and_menu_right_13()
    {
        using var anonymous = factory.ClientWith(null);
        using var none = factory.ClientWith(ApiFactory.NoRights);
        using var otherRights = factory.ClientWith("111111111111011111111111111111");
        foreach (var route in Routes)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.SendAsync(Request(route))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await none.SendAsync(Request(route))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await otherRights.SendAsync(Request(route))).StatusCode);
        }
    }

    [Theory]
    [InlineData("POST", "/api/finance/sun-posting/post", "{}", "Please select the record")]
    [InlineData("POST", "/api/finance/sun-posting/post", """{"items":[{"caseNo":"A","claimVersion":"1"}],"invoiceDateTo":"2021-08-31","bordxStatus":"1"}""", "Please Keyin for Accounting Period ! ")]
    [InlineData("GET", "/api/finance/sun-posting/search?mode=x", "{}", "Select a valid search.")]
    [InlineData("GET", "/api/finance/sun-posting/journal", "{}", "Please Enter Batch No")]
    public async Task Invalid_requests_get_the_first_message(string method, string url, string json, string message)
    {
        using var client = factory.ClientWith(ApiFactory.AllRights);
        var response = await client.SendAsync(Request((new HttpMethod(method), url), json));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(message, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString());
    }
}
