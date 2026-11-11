using Dapper;
using Genisis.Api.Common;
using Genisis.Api.ContributionBilling.McoReinstatement;
using Genisis.Api.Data;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Contribution Billing > MCO Reinstatement: the validators and the service, against a fake repository.</summary>
public class McoReinstatementRulesTests
{
    private sealed class FakeRepository : IMcoReinstatementRepository
    {
        public List<McoRow> Rows { get; set; } = [];

        public List<string> Calls { get; } = [];

        public Task<List<McoRow>> SearchAsync(IDbSession session, McoFilter filter)
        {
            Calls.Add("search " + (filter.Criteria == "invoice" ? filter.InvoiceNo : "batch"));
            return Task.FromResult(filter.Criteria == "invoice" && Calls.Contains("stamped") ? Rows.Select(r => r with { InvoiceNo = filter.InvoiceNo, InvoiceDate = new DateTime(2026, 10, 30) }).ToList() : Rows);
        }

        public Task<McoPayor?> PayorAsync(IDbSession session, string payor) => Task.FromResult<McoPayor?>(new McoPayor(payor, "A PAYOR", "1", "2", "3", "C", "T", "F", "P"));

        public Task<List<McoInvoiceLine>> LinesAsync(IDbSession session, McoFilter filter)
        {
            Calls.Add("lines");
            return Task.FromResult(new List<McoInvoiceLine> { new() { InsCode = "H", AdultChild = "A", Principals = 2, CoveredPersons = 3, McoFee = 10.5m } });
        }

        public Task<string> NextInvoiceAsync(IDbSession session, string payor, string prefix)
        {
            Calls.Add($"next {payor} {prefix}");
            return Task.FromResult(prefix + "00042");
        }

        public Task<int> StampAsync(IDbSession session, McoRow row, string invoiceNo, DateTime invoiceDate, string userCode)
        {
            Calls.Add("stamped");
            return Task.FromResult(1);
        }
    }

    private static McoRow Row(string member, string cover, string? invoice = null, bool covered = false) =>
        new() { MemberNo = member, CoverId = cover, InvoiceNo = invoice, IsCovered = covered, PayorCode = "AH", BordxDate = new DateTime(2021, 7, 26), BatchNo = "1" };

    private static McoReinstatementService Service(FakeRepository repository, FakeSessions sessions) => new(sessions, repository, NullLogger<McoReinstatementService>.Instance);

    private static McoPrintRequest Request(bool allocate, string type = "invoice") => new() { Criteria = "batch", Payor = "AH - X", BordxDate = new DateTime(2021, 7, 26), BatchNo = "1", ReportType = type, Allocate = allocate };

    [Fact]
    public void The_checks_come_in_the_desktops_order()
    {
        var validator = new McoFilterValidator();
        Assert.Equal("Please Enter MCO Criteria!", validator.FirstError(new McoFilter()));
        Assert.Equal("Please Enter Payor!", validator.FirstError(new McoFilter { Criteria = "batch" }));
        Assert.Equal("Please Enter Bordereaux Date!", validator.FirstError(new McoFilter { Criteria = "batch", Payor = "AH" }));
        Assert.Equal("Please Enter Batch No!", validator.FirstError(new McoFilter { Criteria = "batch", Payor = "AH", BordxDate = new DateTime(2021, 7, 26) }));
        Assert.Equal("Batch No must be a number.", validator.FirstError(new McoFilter { Criteria = "batch", Payor = "AH", BordxDate = new DateTime(2021, 7, 26), BatchNo = "1A" }));
        Assert.Null(validator.FirstError(new McoFilter { Criteria = "batch", Payor = "AH - X", BordxDate = new DateTime(2021, 7, 26), BatchNo = "01" }));
        Assert.Equal("Please Enter Invoice No!", validator.FirstError(new McoFilter { Criteria = "invoice" }));
        Assert.Null(validator.FirstError(new McoFilter { Criteria = "invoice", InvoiceNo = "S1" }));
        Assert.Equal("1", McoRules.Batch(" 01 "));
        Assert.Null(McoRules.Batch("x"));
        Assert.Equal(11m, McoRules.Rounded(10.5m));
    }

    [Fact]
    public void Print_and_preview_need_a_report_type()
    {
        var validator = new McoPrintRequestValidator();
        Assert.Equal("Please Select Report Type !", validator.FirstError(Request(true, "")));
        Assert.Equal("Select Invoice or Listing.", validator.FirstError(Request(true, "pdf")));
        Assert.Null(validator.FirstError(Request(true)));
        Assert.Equal("Please Enter Payor!", validator.FirstError(new McoPrintRequest { Criteria = "batch", ReportType = "invoice" }));
    }

    [Fact]
    public async Task Search_counts_principals_and_covered_persons_and_nothing_found_is_not_found()
    {
        var repository = new FakeRepository { Rows = [Row("M1", "00"), Row("M1", "01", covered: true), Row("M2", "00")] };
        var result = await Service(repository, new FakeSessions()).SearchAsync(new McoFilter { Criteria = "batch", Payor = "AH" });
        Assert.Equal((2, 1, 0), (result.Principals, result.Supplementary, result.Invoices.Count));
        repository.Rows = [];
        var failure = await Assert.ThrowsAsync<NotFoundException>(() => Service(repository, new FakeSessions()).SearchAsync(new McoFilter { Criteria = "invoice", InvoiceNo = "S9" }));
        Assert.Equal("Record not found !", failure.Message);
    }

    [Fact]
    public async Task Print_gives_one_new_invoice_number_to_every_line_in_one_transaction()
    {
        var repository = new FakeRepository { Rows = [Row("M1", "00"), Row("M1", "01", covered: true)] };
        var sessions = new FakeSessions();
        var document = await Service(repository, sessions).PrintAsync(Request(true), "U1");
        Assert.Equal(("invoice", true, "P00042"), (document.ReportType, document.Allocated, document.InvoiceNo));
        Assert.Equal(["search batch", "next AH P", "stamped", "stamped", "search P00042", "lines"], repository.Calls);
        Assert.True(sessions.Last.Committed);
        Assert.Single(document.Lines);
        Assert.All(document.Rows, r => Assert.Equal("P00042", r.InvoiceNo));
    }

    [Fact]
    public async Task A_batch_that_has_its_invoice_is_reprinted_and_a_preview_writes_nothing()
    {
        var repository = new FakeRepository { Rows = [Row("M1", "00", "S1"), Row("M1", "01", "S1", true)] };
        var sessions = new FakeSessions();
        var reprint = await Service(repository, sessions).PrintAsync(Request(true), "U1");
        Assert.Equal((false, "S1"), (reprint.Allocated, reprint.InvoiceNo));
        Assert.False(sessions.Last.Committed);
        Assert.DoesNotContain(repository.Calls, c => c.StartsWith("next") || c == "stamped");

        var fresh = new FakeRepository { Rows = [Row("M1", "00")] };
        var preview = await Service(fresh, new FakeSessions()).PrintAsync(Request(false, "listing"), "U1");
        Assert.Equal((false, null, 0), (preview.Allocated, preview.InvoiceNo, preview.Lines.Count));
        Assert.DoesNotContain(fresh.Calls, c => c.StartsWith("next") || c == "stamped");
    }

    [Fact]
    public async Task Different_invoices_or_only_some_lines_invoiced_are_refused_and_nothing_is_written()
    {
        foreach (var rows in new[] { new List<McoRow> { Row("M1", "00", "S1"), Row("M2", "00", "S2") }, [Row("M1", "00", "S1"), Row("M2", "00")] })
        {
            var repository = new FakeRepository { Rows = rows };
            var sessions = new FakeSessions();
            var failure = await Assert.ThrowsAsync<BusinessRuleException>(() => Service(repository, sessions).PrintAsync(Request(true), "U1"));
            Assert.Equal("Different Invoice Found in Same Batch or Range, Please Search by Invoice No ! ", failure.Message);
            Assert.False(sessions.Last.Committed);
            Assert.DoesNotContain(repository.Calls, c => c == "stamped");
        }
    }
}

/// <summary>MCO Reinstatement against the database. Every write is made in a session that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class McoReinstatementDatabaseTests
{
    private static McoReinstatementRepository Repository => new();

    private static async Task<int> Count(IDbSession s, string sql, object? args = null) => await s.Connection.ExecuteScalarAsync<int>(sql, args, s.Transaction);

    private const string Checksum = "SELECT ISNULL(CHECKSUM_AGG(CHECKSUM(MBMNumber, MBMCoverID, MBMInvoiceNo, MBMInvoiceDate, MBMInvoiceBy)), 0) + COUNT(*) FROM dbo.MBMReinstatement";

    private const string CounterChecksum = "SELECT ISNULL(CHECKSUM_AGG(CHECKSUM(MCOInvIndex, PAYCode, Prefix, MCOInvoice_No)), 0) + COUNT(*) FROM dbo.MBMMCOINVQ";

    [Fact]
    public async Task The_search_gives_a_batch_of_a_payor_with_its_invoice_lines_and_an_invoice_gives_its_members()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var batch = await session.Connection.QueryFirstAsync<(DateTime Date, string Batch, string Payor)>("SELECT TOP (1) v.MBMEndtBordxDate, LTRIM(RTRIM(v.MBMEndtBordxBatch)), RTRIM(v.PAYCode) FROM dbo.VIEW_MBM_MCOReinstatement v WHERE v.MBMEndtBordxDate = '2021-07-26' AND v.PAYCode = 'AH'", transaction: session.Transaction);
        var filter = new McoFilter { Criteria = "batch", Payor = batch.Payor + " - X", BordxDate = batch.Date, BatchNo = batch.Batch };
        var rows = await Repository.SearchAsync(session, filter);
        Assert.NotEmpty(rows);
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM (SELECT DISTINCT MBMNumber, MBMCOVID, MBMCCoverID, MBMCRELCode, MBMName, MBMCName, Type, MBMEndtBordxDate, MBMEndtBordxBatch, MBMPolicyNo, MBMPayorEffDate, MBMPayorExpDate, MBMInvoiceNo, MBMInvoiceDate, MBMMCOFee, MBMIcBcPp, MBMCICBCPPNo, MBMGroupCompany, DayInsured FROM dbo.VIEW_MBM_MCOReinstatement WHERE MBMEndtBordxDate = @d AND LTRIM(RTRIM(MBMEndtBordxBatch)) = @b AND PAYCode = @p) x", new { d = batch.Date, b = batch.Batch, p = batch.Payor }), rows.Count);
        Assert.All(rows, r => Assert.Equal(batch.Batch, r.BatchNo));
        var lines = await Repository.LinesAsync(session, filter);
        Assert.NotEmpty(lines);
        Assert.Equal(rows.Count(r => !r.IsCovered), lines.Sum(l => l.Principals));
        Assert.Empty(await Repository.SearchAsync(session, new McoFilter { Criteria = "batch", Payor = batch.Payor, BordxDate = batch.Date, BatchNo = "99999" }));

        var invoiced = await session.Connection.QueryFirstAsync<string>("SELECT TOP (1) RTRIM(MBMInvoiceNo) FROM dbo.MBMReinstatement WHERE MBMInvoiceNo <> '' ORDER BY MBMInvoiceDate DESC", transaction: session.Transaction);
        var byInvoice = await Repository.SearchAsync(session, new McoFilter { Criteria = "invoice", InvoiceNo = invoiced });
        Assert.NotEmpty(byInvoice);
        Assert.All(byInvoice, r => Assert.Equal(invoiced, r.InvoiceNo));
        Assert.Equal("P", (await Repository.PayorAsync(session, "XP"))!.Prefix);
        Assert.Equal("S", (await Repository.PayorAsync(session, "AH"))!.Prefix);
        Assert.Null(await Repository.PayorAsync(session, "QQ"));
    }

    [Fact]
    public async Task The_next_invoice_number_follows_the_shared_counter_or_the_payors_own_and_a_batch_is_stamped_and_nothing_survives_the_rollback()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var (before, counters) = (await Count(read, Checksum), await Count(read, CounterChecksum));
        var shared = await read.Connection.QueryFirstAsync<(int Index, string No, string Prefix)>("SELECT TOP (1) MCOInvIndex, RTRIM(MCOInvoice_No), RTRIM(Prefix) FROM dbo.MBMMCOINVQ ORDER BY MCOInvIndex", transaction: read.Transaction);
        var batch = await read.Connection.QueryFirstAsync<(DateTime Date, string Batch)>("SELECT TOP (1) MBMEndtBordxDate, LTRIM(RTRIM(MBMEndtBordxBatch)) FROM dbo.VIEW_MBM_MCOReinstatement WHERE MBMEndtBordxDate = '2021-07-26' AND PAYCode = 'AH' AND (MBMInvoiceNo IS NULL OR MBMInvoiceNo = '')", transaction: read.Transaction);
        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var next = await Repository.NextInvoiceAsync(write, "AH", "S");
            Assert.Equal(shared.Prefix + (int.Parse(shared.No) + 1).ToString("00000"), next);
            var second = await Repository.NextInvoiceAsync(write, "ZY", "S");
            Assert.Equal((int.Parse(shared.No) + 2).ToString("00000"), second[shared.Prefix.Length..]);
            Assert.Equal((int.Parse(shared.No) + 2).ToString("00000"), await write.Connection.ExecuteScalarAsync<string>("SELECT RTRIM(MCOInvoice_No) FROM dbo.MBMMCOINVQ WHERE MCOInvIndex = @i", new { i = shared.Index }, write.Transaction));

            Assert.Equal("Q00001", await Repository.NextInvoiceAsync(write, "ZY", "Q"));
            Assert.Equal("Q00002", await Repository.NextInvoiceAsync(write, "ZY", "Q"));
            Assert.Equal(1, await Count(write, "SELECT COUNT(*) FROM dbo.MBMMCOINVQ WHERE PAYCode = 'ZY'"));

            var filter = new McoFilter { Criteria = "batch", Payor = "AH", BordxDate = batch.Date, BatchNo = batch.Batch };
            var rows = await Repository.SearchAsync(write, filter);
            Assert.NotEmpty(rows);
            foreach (var row in rows) Assert.Equal(1, await Repository.StampAsync(write, row, next, new DateTime(2026, 10, 30), "E2E"));
            Assert.Equal(0, await Repository.StampAsync(write, rows[0], "S-AGAIN", new DateTime(2026, 10, 30), "E2E"));
            var stamped = await Repository.SearchAsync(write, new McoFilter { Criteria = "invoice", InvoiceNo = next });
            Assert.Equal(rows.Count, stamped.Count);
            Assert.All(stamped, r => Assert.Equal(next, r.InvoiceNo));
            Assert.Equal("E2E", await write.Connection.ExecuteScalarAsync<string>("SELECT TOP (1) RTRIM(MBMInvoiceBy) FROM dbo.MBMReinstatement WHERE MBMInvoiceNo = @n", new { n = next }, write.Transaction));
        }

        Assert.Equal(before, await Count(read, Checksum));
        Assert.Equal(counters, await Count(read, CounterChecksum));
    }
}
