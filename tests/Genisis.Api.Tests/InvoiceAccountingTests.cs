using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Invoice;
using Genisis.Api.Worksheet;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>The rules of the Invoice HOS and MBM Accounting forms, without a database.</summary>
public class InvoiceAccountingRulesTests
{
    [Theory]
    [InlineData(100, 150, "C50")]
    [InlineData(100, 100, "C0")]
    [InlineData(150, 100, "D50")]
    [InlineData(-30, 100, "C70")]
    [InlineData(-30, -100, "D70")]
    [InlineData(100, -40, "D40")]
    [InlineData(0, 25, "C25")]
    public void Debit_and_credit_follow_the_listing_s_four_cases(int previous, int payable, string expected)
    {
        var (debit, credit) = InvoiceAccountingRules.DebitCredit(previous, payable);
        Assert.Equal(expected, debit is { } d ? $"D{d:0.##}" : $"C{credit:0.##}");
    }

    [Fact]
    public void A_negative_result_is_a_debit_without_its_sign_and_zero_is_a_credit_of_nothing()
    {
        Assert.Equal((70m, null), InvoiceAccountingRules.DebitCredit(-30m, -100m));
        Assert.Equal((null, 0m), InvoiceAccountingRules.DebitCredit(100m, 100m));
        Assert.Equal((40m, null), InvoiceAccountingRules.DebitCredit(100m, -40m));
    }

    [Fact]
    public void With_no_previous_amount_or_no_payable_amount_the_payable_amount_stands()
    {
        Assert.Equal((null, 80m), InvoiceAccountingRules.DebitCredit(null, 80m));
        Assert.Equal((null, 0m), InvoiceAccountingRules.DebitCredit(null, null));
        Assert.Equal((null, 0m), InvoiceAccountingRules.DebitCredit(0m, null));
    }

    [Fact]
    public void A_batch_label_is_the_kind_the_day_and_the_number_of_the_day()
    {
        Assert.Equal("H23102026-1", InvoiceAccountingRules.BatchLabel('H', new DateOnly(2026, 10, 23), 1));
        Assert.Equal("M01022026-12", InvoiceAccountingRules.BatchLabel('M', new DateOnly(2026, 2, 1), 12));
        Assert.Equal(new DateOnly(2026, 10, 23), InvoiceAccountingRules.BatchDay("H23102026"));
        Assert.Equal(new DateOnly(2005, 8, 19), InvoiceAccountingRules.BatchDay(" M19082005 "));
        Assert.Null(InvoiceAccountingRules.BatchDay("H31022026"));
        Assert.Null(InvoiceAccountingRules.BatchDay(null));
        Assert.Null(InvoiceAccountingRules.BatchDay("23102026"));
    }

    [Theory]
    [InlineData("N", true, false)]
    [InlineData("r", true, true)]
    [InlineData(" R ", true, true)]
    [InlineData("", false, false)]
    [InlineData(null, false, false)]
    [InlineData("X", false, false)]
    public void The_print_status_is_new_or_reprint(string? status, bool valid, bool reprint)
    {
        Assert.Equal(valid, InvoiceAccountingRules.IsPrintStatus(status));
        Assert.Equal(reprint, InvoiceAccountingRules.IsReprint(status));
    }

    [Fact]
    public void Both_searches_need_a_print_status_with_the_desktop_message()
    {
        Assert.Equal("Please select Print Status", new HospitalAccountingFilterValidator().FirstError(new HospitalAccountingFilter { PrintStatus = "" }));
        Assert.Equal("Please select Print Status", new MemberAccountingFilterValidator().FirstError(new MemberAccountingFilter { PrintStatus = null }));
        Assert.Null(new HospitalAccountingFilterValidator().FirstError(new HospitalAccountingFilter()));
        Assert.Null(new MemberAccountingFilterValidator().FirstError(new MemberAccountingFilter { PrintStatus = "R" }));
    }

    [Fact]
    public void The_period_update_checks_month_year_and_the_ticked_invoices_in_that_order()
    {
        var validator = new AccountingPeriodRequestValidator();
        var item = new AccountingPeriodItem("AB1", "1");
        Assert.Equal("Month cannot be Empty.", validator.FirstError(new AccountingPeriodRequest()));
        Assert.Equal("Month cannot be Empty.", validator.FirstError(new AccountingPeriodRequest { Month = 13, Year = 2026, Items = [item] }));
        Assert.Equal("Year cannot be empty!", validator.FirstError(new AccountingPeriodRequest { Month = 6, Items = [item] }));
        Assert.Equal("Please select at least one record to be updated! ", validator.FirstError(new AccountingPeriodRequest { Month = 6, Year = 2026 }));
        Assert.Null(validator.FirstError(new AccountingPeriodRequest { Month = 6, Year = 2026, Items = [item] }));
    }
}

/// <summary>The services of the two accounting forms against fake repositories.</summary>
public class InvoiceAccountingServiceTests
{
    private sealed class FakeClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class FakeHospital : IHospitalAccountingRepository
    {
        public int Listed { get; set; } = 3;
        public int Marked { get; set; } = 3;
        public string? PrintedBatch { get; private set; }
        public string? PrintedBy { get; private set; }
        public DateOnly? BatchDay { get; private set; }

        public Task<List<HospitalAccountingRow>> SearchAsync(IDbSession session, HospitalAccountingFilter filter, int max) =>
            Task.FromResult(Enumerable.Range(1, Math.Min(Listed, max)).Select(i => new HospitalAccountingRow { CaseNo = $"AB{i}", Version = "1" }).ToList());

        public Task<string> NextBatchAsync(IDbSession session, DateOnly day)
        {
            BatchDay = day;
            return Task.FromResult(InvoiceAccountingRules.BatchLabel('H', day, 4));
        }

        public Task<int> PrintAsync(IDbSession session, HospitalAccountingFilter filter, string batch, string userCode)
        {
            PrintedBatch = batch;
            PrintedBy = userCode;
            return Task.FromResult(Marked);
        }

        public Task<List<InvoiceOption>> HospitalsAsync(IDbSession session) => Task.FromResult(new List<InvoiceOption> { new("H1", "A HOSPITAL") });
    }

    private sealed class FakeMember : IMemberAccountingRepository
    {
        public int Listed { get; set; } = 2;
        public HashSet<string> Missing { get; } = [];
        public List<(string, int, int)> Periods { get; } = [];

        public Task<List<MemberAccountingRow>> SearchAsync(IDbSession session, MemberAccountingFilter filter, int max) =>
            Task.FromResult(Enumerable.Range(1, Math.Min(Listed, max)).Select(i => new MemberAccountingRow { CaseNo = $"AB{i}", Version = "1" }).ToList());

        public Task<string> NextBatchAsync(IDbSession session, DateOnly day) => Task.FromResult(InvoiceAccountingRules.BatchLabel('M', day, 1));

        public Task<int> PrintAsync(IDbSession session, MemberAccountingFilter filter, string batch, string userCode) => Task.FromResult(2);

        public Task<int> SetPeriodAsync(IDbSession session, AccountingPeriodItem item, int month, int year, string userCode)
        {
            Periods.Add((item.CaseNo, month, year));
            return Task.FromResult(Missing.Contains(item.CaseNo) ? 0 : 1);
        }
    }

    private static readonly FakeClock Today = new(new DateTime(2026, 10, 23, 9, 30, 0));

    [Fact]
    public async Task A_new_print_takes_the_days_batch_marks_the_invoices_and_commits_once()
    {
        var repository = new FakeHospital();
        var sessions = new FakeSessions();
        var result = await new HospitalAccountingService(sessions, repository, Today, NullLogger<HospitalAccountingService>.Instance).PrintAsync(new HospitalAccountingFilter(), "E2E");
        Assert.Equal(("H23102026-4", 3, 3), (result.Batch, result.Updated, result.Rows));
        Assert.Equal(("H23102026-4", "E2E", new DateOnly(2026, 10, 23)), (repository.PrintedBatch, repository.PrintedBy, repository.BatchDay));
        Assert.True(sessions.Last.Transactional);
        Assert.True(sessions.Last.Committed);
    }

    [Fact]
    public async Task A_reprint_marks_nothing_and_takes_no_batch()
    {
        var repository = new FakeHospital();
        var sessions = new FakeSessions();
        var result = await new HospitalAccountingService(sessions, repository, Today, NullLogger<HospitalAccountingService>.Instance).PrintAsync(new HospitalAccountingFilter { PrintStatus = "R" }, "E2E");
        Assert.Equal((null, 0, 3), (result.Batch, result.Updated, result.Rows));
        Assert.Null(repository.PrintedBatch);
        Assert.Null(repository.BatchDay);
        Assert.False(sessions.Last.Committed);
    }

    [Fact]
    public async Task Nothing_listed_or_too_much_listed_is_refused_and_nothing_is_marked()
    {
        var repository = new FakeHospital { Listed = 0 };
        var sessions = new FakeSessions();
        var service = new HospitalAccountingService(sessions, repository, Today, NullLogger<HospitalAccountingService>.Instance);
        Assert.Equal("Please select a record(s) before proceeding!", (await Assert.ThrowsAsync<BusinessRuleException>(() => service.PrintAsync(new HospitalAccountingFilter(), "E2E"))).Message);
        repository.Listed = InvoiceAccountingRules.MaxRows + 1;
        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => service.PrintAsync(new HospitalAccountingFilter(), "E2E"));
        Assert.Contains("narrow the search", refused.Message);
        Assert.Null(repository.PrintedBatch);
        Assert.False(sessions.Last.Committed);
    }

    [Fact]
    public async Task A_search_over_the_limit_is_cut_off_and_says_so()
    {
        var repository = new FakeHospital { Listed = InvoiceAccountingRules.MaxRows + 1 };
        var service = new HospitalAccountingService(new FakeSessions(), repository, Today, NullLogger<HospitalAccountingService>.Instance);
        var result = await service.SearchAsync(new HospitalAccountingFilter());
        Assert.True(result.Truncated);
        Assert.Equal(InvoiceAccountingRules.MaxRows, result.Rows.Count);
        repository.Listed = 12;
        Assert.False((await service.SearchAsync(new HospitalAccountingFilter())).Truncated);
    }

    [Fact]
    public async Task The_member_print_uses_the_member_batch_and_the_period_update_stops_at_a_missing_invoice_with_nothing_saved()
    {
        var repository = new FakeMember();
        var sessions = new FakeSessions();
        var service = new MemberAccountingService(sessions, repository, Today, NullLogger<MemberAccountingService>.Instance);
        Assert.Equal("M23102026-1", (await service.PrintAsync(new MemberAccountingFilter(), "E2E")).Batch);

        var request = new AccountingPeriodRequest { Month = 7, Year = 2026, Items = [new("AB1", "1"), new("AB1", "1"), new("AB2", "1")] };
        Assert.Equal(2, await service.SetPeriodAsync(request, "E2E"));
        Assert.Equal(2, repository.Periods.Count);
        Assert.True(sessions.Last.Committed);

        repository.Missing.Add("AB2");
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.SetPeriodAsync(request, "E2E"));
        Assert.Contains("AB2-1", ex.Message);
        Assert.False(sessions.Last.Committed);
    }
}

/// <summary>The two accounting forms against the database; everything that writes happens inside a transaction that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class InvoiceAccountingDatabaseTests
{
    private static readonly DateOnly Day = new(2026, 10, 23);

    [Fact]
    public async Task The_hospital_search_filters_by_status_hospital_and_dates_and_works_out_debit_and_credit()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        var repository = new HospitalAccountingRepository();
        await using var session = await TestDb.Sessions().OpenAsync();
        var hospital = await session.Connection.ExecuteScalarAsync<string>(
            "SELECT TOP (1) RTRIM(INVPayee) FROM genisis.WS_HOSInvoice WHERE INVPayTo = 'H' AND InvPrintStatus = '0' AND INVDate >= '20150101' GROUP BY INVPayee HAVING COUNT(*) BETWEEN 3 AND 40 ORDER BY INVPayee", transaction: session.Transaction);
        Assert.NotNull(hospital);

        var all = await repository.SearchAsync(session, new HospitalAccountingFilter { HospitalCode = hospital }, 5001);
        Assert.InRange(all.Count, 3, 40);
        Assert.All(all, r => Assert.Equal(hospital, r.HospitalCode));
        Assert.All(all, r => Assert.Null(r.BatchDate));
        Assert.All(all, r => Assert.True(r.Debit is null != r.Credit is null));
        var dated = all.Where(r => r.InvoiceDate is not null).OrderBy(r => r.InvoiceDate).ToList();
        var from = dated[0].InvoiceDate!.Value;
        var narrowed = await repository.SearchAsync(session, new HospitalAccountingFilter { HospitalCode = hospital, InvoiceDateFrom = from, InvoiceDateTo = from }, 5001);
        Assert.Equal(all.Count(r => r.InvoiceDate!.Value.Date == from.Date), narrowed.Count);
        Assert.Empty(await repository.SearchAsync(session, new HospitalAccountingFilter { HospitalCode = "NOT A HOSPITAL" }, 5001));

        var printed = await repository.SearchAsync(session, new HospitalAccountingFilter { PrintStatus = "R", BatchDateFrom = new DateTime(2002, 3, 18), BatchDateTo = new DateTime(2002, 3, 18) }, 5001);
        Assert.NotEmpty(printed);
        Assert.All(printed, r => Assert.Equal("H18032002", r.BatchDate));
        Assert.Empty(await repository.SearchAsync(session, new HospitalAccountingFilter { PrintStatus = "R", BatchDateFrom = new DateTime(2002, 3, 19), BatchDateTo = new DateTime(2002, 3, 20) }, 0 + 5001));
    }

    [Fact]
    public async Task A_hospital_print_marks_the_listed_invoices_with_the_days_batch_and_a_rollback_undoes_it_all()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        var repository = new HospitalAccountingRepository();
        await using var read = await TestDb.Sessions().OpenAsync();
        var hospital = await read.Connection.ExecuteScalarAsync<string>(
            "SELECT TOP (1) RTRIM(INVPayee) FROM genisis.WS_HOSInvoice WHERE INVPayTo = 'H' AND InvPrintStatus = '0' GROUP BY INVPayee HAVING COUNT(*) BETWEEN 3 AND 25 ORDER BY INVPayee", transaction: read.Transaction);
        var filter = new HospitalAccountingFilter { HospitalCode = hospital };
        var before = await repository.SearchAsync(read, filter, 5001);
        var batchesBefore = await read.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.WS_INVACHSEQ", transaction: read.Transaction);

        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var first = await repository.NextBatchAsync(write, Day);
            var second = await repository.NextBatchAsync(write, Day);
            Assert.Equal(("H23102026-1", "H23102026-2"), (first, second));
            var marked = await repository.PrintAsync(write, filter, second, "E2E");
            Assert.True(marked >= before.Count, $"{marked} invoices marked for {before.Count} listed");

            Assert.Empty(await repository.SearchAsync(write, filter, 5001));
            var reprint = await repository.SearchAsync(write, new HospitalAccountingFilter { HospitalCode = hospital, PrintStatus = "R", BatchDateFrom = new DateTime(2026, 10, 23), BatchDateTo = new DateTime(2026, 10, 23), BatchNoFrom = 2, BatchNoTo = 2 }, 5001);
            Assert.Equal(before.Count, reprint.Count);
            Assert.All(reprint, r => Assert.Equal(("H23102026", 2), (r.BatchDate, r.BatchNo)));
            Assert.All(reprint, r => Assert.Equal(before.Single(b => b.CaseNo == r.CaseNo && b.Version == r.Version && b.InvoiceNo == r.InvoiceNo).Payable ?? 0m, r.PreviousAmount));
            var logon = await write.Connection.ExecuteScalarAsync<string>("SELECT TOP (1) RTRIM(InvPrintLogon) FROM genisis.WS_ClaimInvoice WHERE INVBatchDate = 'H23102026'", transaction: write.Transaction);
            Assert.Equal("E2E", logon);
        }

        var after = await repository.SearchAsync(read, filter, 5001);
        Assert.Equal(before.Count, after.Count);
        Assert.Equal(batchesBefore, await read.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.WS_INVACHSEQ", transaction: read.Transaction));
        Assert.Equal(0, await read.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.WS_ClaimInvoice WHERE INVBatchDate = 'H23102026'", transaction: read.Transaction));
    }

    [Fact]
    public async Task The_member_accounting_search_print_and_period_work_and_a_rollback_undoes_them()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        var repository = new MemberAccountingRepository();
        await using var read = await TestDb.Sessions().OpenAsync();
        var payor = await read.Connection.ExecuteScalarAsync<string>(
            "SELECT TOP (1) LEFT(CASNumber, 2) FROM genisis.WS_MBMInvoice1 WHERE InvPrintStatus = '0' GROUP BY LEFT(CASNumber, 2) HAVING COUNT(*) BETWEEN 3 AND 40 ORDER BY LEFT(CASNumber, 2)", transaction: read.Transaction);
        Assert.NotNull(payor);
        var filter = new MemberAccountingFilter { Payor = payor };
        var before = await repository.SearchAsync(read, filter, 5001);
        Assert.InRange(before.Count, 3, 40);
        Assert.All(before, r => Assert.StartsWith(payor, r.CaseNo));
        Assert.All(before, r => Assert.Null(r.BatchDate));
        Assert.Empty(await repository.SearchAsync(read, new MemberAccountingFilter { Payor = "Q9" }, 5001));
        const string memberPaid = "SELECT COUNT(*) FROM genisis.WS_MBMInvoice1 WHERE InvPrintStatus = '0' AND INVPayTo = 'M' AND LEFT(CASNumber, 2) = @payor";
        var memberPaidBefore = await read.Connection.ExecuteScalarAsync<int>(memberPaid, new { payor }, read.Transaction);
        Assert.True(memberPaidBefore > 0);
        var pick = before.First(r => r.PayToCode == "M");
        var memberInvoices = await read.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM genisis.WS_ClaimInvoice WHERE CASNumber = @cas AND INVWSversion = @version AND INVPayTo = 'M'", new { cas = pick.CaseNo, version = pick.Version }, read.Transaction);
        Assert.True(memberInvoices > 0);

        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var batch = await repository.NextBatchAsync(write, Day);
            Assert.Equal("M23102026-1", batch);
            Assert.True(await repository.PrintAsync(write, filter, batch, "E2E") >= memberPaidBefore);
            // the print marks the member-paid invoices only, as the desktop's does, so a hospital-paid row of the list stays new
            Assert.Equal(0, await write.Connection.ExecuteScalarAsync<int>(memberPaid, new { payor }, write.Transaction));
            Assert.All(await repository.SearchAsync(write, filter, 5001), r => Assert.NotEqual("M", r.PayToCode));
            var reprint = await repository.SearchAsync(write, new MemberAccountingFilter { Payor = payor, PrintStatus = "R", BatchNoFrom = 1, BatchNoTo = 1, BatchDateFrom = new DateTime(2026, 10, 23), BatchDateTo = new DateTime(2026, 10, 23) }, 5001);
            Assert.Equal(before.Count(r => r.PayToCode == "M"), reprint.Count);

            // the period goes to the member-paid invoices of the worksheet version, and a version with none changes nothing
            Assert.Equal(memberInvoices, await repository.SetPeriodAsync(write, new AccountingPeriodItem(pick.CaseNo, pick.Version), 7, 2026, "E2E"));
            var periods = (await write.Connection.QueryAsync<(int Month, int Year, string User)>(
                "SELECT CAST(AccPeriodMonth AS int), CAST(AccPeriodYear AS int), RTRIM(LastAccPeriodUser) FROM genisis.WS_ClaimInvoice WHERE CASNumber = @cas AND INVWSversion = @version AND INVPayTo = 'M'",
                new { cas = pick.CaseNo, version = pick.Version }, write.Transaction)).ToList();
            Assert.All(periods, p => Assert.Equal((7, 2026, "E2E"), p));
            Assert.Equal(0, await repository.SetPeriodAsync(write, new AccountingPeriodItem("NO SUCH CASE", "1"), 7, 2026, "E2E"));
        }

        Assert.Equal(before.Count, (await repository.SearchAsync(read, filter, 5001)).Count);
        Assert.Equal(0, await read.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.WS_ClaimInvoice WHERE INVBatchDate = 'M23102026'", transaction: read.Transaction));
    }
}
