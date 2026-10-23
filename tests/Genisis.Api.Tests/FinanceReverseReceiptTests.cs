using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Finance;
using Genisis.Api.Finance.InvoiceReceipt;
using Genisis.Api.Finance.ReceiptEnquiry;
using Genisis.Api.Finance.ReverseReceipt;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Finance &gt; Reverse Receipt: rules, numbering and the unit of work.</summary>
public class FinanceReverseReceiptTests
{
    private sealed class FakeReceipts(ReceiptRow? receipt) : IReceiptEnquiryRepository
    {
        public Task<(List<CodeName> Types, List<CodeName> Payors)> LookupsAsync(IDbSession session) => throw new NotSupportedException();
        public Task<List<ReceiptRow>> SearchAsync(IDbSession session, ReceiptEnquiryFilter filter) => throw new NotSupportedException();

        public Task<(ReceiptRow Receipt, List<ReceiptAllocation> Allocations, List<ReceiptInvoice> Invoices, List<ReceiptPosting> Postings)?> GetAsync(IDbSession session, int id) =>
            Task.FromResult<(ReceiptRow, List<ReceiptAllocation>, List<ReceiptInvoice>, List<ReceiptPosting>)?>(receipt is null ? null : (receipt, [], [], []));
    }

    private sealed class FakeNumbering : IInvoiceReceiptRepository
    {
        public List<string> Prefixes { get; } = [];

        public Task<(List<CodeName> Prefixes, List<CodeName> Payors)> LookupsAsync(IDbSession session) =>
            Task.FromResult<(List<CodeName>, List<CodeName>)>(([new("RA", "RA"), new("RB", "RB"), new("RC", "RC")], []));

        public Task<List<OutstandingInvoice>> SearchAsync(IDbSession session, InvoiceReceiptFilter filter) => throw new NotSupportedException();
        public Task<List<ReceiptRow>> ReceiptsAsync(IDbSession session, string invoiceNo) => throw new NotSupportedException();

        public Task<string> NextNumberAsync(IDbSession session, string prefix)
        {
            Prefixes.Add(prefix);
            return Task.FromResult(prefix + "55700");
        }

        public Task<(int Result, int? Id)> InsertAsync(IDbSession session, string receiptNo, InvoiceReceiptRequest request, string description) => throw new NotSupportedException();
    }

    private sealed class FakeReverse(int result) : IReverseReceiptRepository
    {
        public (int Id, string No, int Month, int Year)? Call { get; private set; }

        public Task<(int Result, int? Id, int Lines)> ReverseAsync(IDbSession session, int id, string newNo, DateTime date, int accMonth, int accYear, string userCode)
        {
            Call = (id, newNo, accMonth, accYear);
            return Task.FromResult<(int, int?, int)>(result == StoredProcedures.Ok ? (0, 500, 11) : (result, null, 0));
        }
    }

    private static readonly ReverseReceiptRequest Request = new() { ReversalDate = new DateTime(2021, 8, 10), AccPeriod = "08/2021" };

    private static ReverseReceiptService Service(FakeSessions sessions, ReceiptRow? receipt, FakeNumbering numbering, FakeReverse reverse) =>
        new(sessions, new FakeReceipts(receipt), numbering, reverse, NullLogger<ReverseReceiptService>.Instance);

    [Fact]
    public void Only_a_positive_receipt_that_is_not_yet_reversed_can_be_reversed()
    {
        Assert.Null(ReverseReceiptService.Problem(new ReceiptRow { ReceiptNo = "RB55092", Amount = 5413.35m, Description = "AH-IP" }));
        Assert.Equal("A receipt without a receipt number cannot be reversed.", ReverseReceiptService.Problem(new ReceiptRow { ReceiptNo = " ", Amount = 1m }));
        Assert.Contains("is a reversal", ReverseReceiptService.Problem(new ReceiptRow { ReceiptNo = "RB1", Amount = -5m }));
        Assert.Contains("is a reversal", ReverseReceiptService.Problem(new ReceiptRow { ReceiptNo = "RB2", Amount = 5m, Description = "REV RB1-X" }));
        Assert.Equal("Receipt RB3 has already been reversed by RB9.", ReverseReceiptService.Problem(new ReceiptRow { ReceiptNo = "RB3", Amount = 5m, ReversedBy = "RB9" }));
    }

    [Theory]
    [InlineData("RC43092", "RC")]
    [InlineData("ra20647", "RA")]
    [InlineData("10117", "RB")]
    [InlineData("CB00111181", "RB")]
    public void The_reversal_number_keeps_the_original_sequence_prefix(string original, string prefix) =>
        Assert.Equal(prefix, ReverseReceiptService.Prefix(original, [new("RA", "RA"), new("RB", "RB"), new("RC", "RC")]));

    [Fact]
    public void Reversal_checks_follow_the_order_and_messages()
    {
        var validator = new ReverseReceiptRequestValidator();
        Assert.Null(validator.FirstError(Request));
        Assert.Equal("Please Enter Reversal Date", validator.FirstError(new ReverseReceiptRequest()));
        Assert.Equal("Please Keyin for Accounting Period ! ", validator.FirstError(new ReverseReceiptRequest { ReversalDate = DateTime.Today }));
        Assert.Equal("Select a valid accounting period.", validator.FirstError(new ReverseReceiptRequest { ReversalDate = DateTime.Today, AccPeriod = "8/2021" }));
    }

    [Fact]
    public async Task A_reversal_takes_the_next_number_of_the_original_prefix_and_commits()
    {
        var sessions = new FakeSessions();
        var numbering = new FakeNumbering();
        var reverse = new FakeReverse(StoredProcedures.Ok);
        var result = await Service(sessions, new ReceiptRow { Id = 7, ReceiptNo = "RC43092", Amount = 45308.63m }, numbering, reverse).ReverseAsync(7, Request, "TESTER");
        Assert.Equal((500, "RC55700", 11, "Receipt RC43092 reversed by RC55700."), (result.Id, result.ReceiptNo, result.Lines, result.Message));
        Assert.Equal((7, "RC55700", 8, 2021), reverse.Call);
        Assert.True(sessions.Last.Committed);
    }

    [Fact]
    public async Task A_refused_or_missing_receipt_writes_nothing()
    {
        var numbering = new FakeNumbering();
        var sessions = new FakeSessions();
        var already = await Assert.ThrowsAsync<FinanceException>(() =>
            Service(sessions, new ReceiptRow { ReceiptNo = "RB1", Amount = 5m, ReversedBy = "RB2" }, numbering, new FakeReverse(0)).ReverseAsync(1, Request, "TESTER"));
        Assert.Equal("Receipt RB1 has already been reversed by RB2.", already.Message);
        Assert.Empty(numbering.Prefixes);
        Assert.False(sessions.Last.Committed);

        await Assert.ThrowsAsync<NotFoundException>(() => Service(new FakeSessions(), null, numbering, new FakeReverse(0)).ReverseAsync(1, Request, "TESTER"));

        var raced = new FakeSessions();
        var conflict = await Assert.ThrowsAsync<FinanceException>(() =>
            Service(raced, new ReceiptRow { ReceiptNo = "RB1", Amount = 5m }, numbering, new FakeReverse(StoredProcedures.Conflict)).ReverseAsync(1, Request, "TESTER"));
        Assert.Contains("cannot be reversed", conflict.Message);
        Assert.True(raced.Last.RolledBack);
    }
}

/// <summary>Finance &gt; Reverse Receipt and Short Payment against the development databases; writes are rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class FinanceReverseReceiptDatabaseTests
{
    [Fact]
    public async Task A_claim_receipt_is_reversed_with_its_lines_once_and_the_enquiry_shows_it()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        var sessions = TestDb.Sessions();
        await using var session = await sessions.BeginAsync();
        var sample = await session.Connection.QueryFirstOrDefaultAsync<(int Id, string No, decimal Amount)>(
            """
            SELECT TOP 1 s.ReceiptIndex, RTRIM(s.ReceiptNo), s.ReceiptAmt FROM genisis.Pay_ReceiptSummary s
            WHERE s.ReceiptAmt > 0 AND s.ReceiptNo LIKE 'RB%' AND EXISTS (SELECT 1 FROM genisis.Pay_ReceiptfrPAY p WHERE p.ReceiptNo = s.ReceiptNo)
              AND NOT EXISTS (SELECT 1 FROM genisis.Pay_ReceiptSummary r WHERE r.ReceiptDesc LIKE 'REV ' + RTRIM(s.ReceiptNo) + '-%')
            ORDER BY s.ReceiptIndex DESC
            """, transaction: session.Transaction);
        if (sample.No is null) return;
        var lines = await session.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.Pay_ReceiptfrPAY WHERE ReceiptNo = @no", new { no = sample.No }, session.Transaction);

        var newNo = await new InvoiceReceiptRepository().NextNumberAsync(session, "RB");
        var repository = new ReverseReceiptRepository();
        var (result, id, copied) = await repository.ReverseAsync(session, sample.Id, newNo, new DateTime(2021, 8, 10), 8, 2021, "TESTER");
        Assert.Equal((StoredProcedures.Ok, lines), (result, copied));
        Assert.Equal(StoredProcedures.Conflict, (await repository.ReverseAsync(session, sample.Id, newNo + "X", new DateTime(2021, 8, 10), 8, 2021, "TESTER")).Result);
        Assert.Equal(StoredProcedures.Conflict, (await repository.ReverseAsync(session, id!.Value, newNo + "Y", new DateTime(2021, 8, 10), 8, 2021, "TESTER")).Result);

        var reversal = (await new ReceiptEnquiryRepository().GetAsync(session, id.Value))!.Value;
        Assert.Equal((-sample.Amount, lines, true), (reversal.Receipt.Amount, reversal.Allocations.Count, reversal.Receipt.IsReversal));
        var original = (await new ReceiptEnquiryRepository().GetAsync(session, sample.Id))!.Value;
        Assert.Equal(newNo, original.Receipt.ReversedBy);
        var listed = await new ReceiptEnquiryRepository().SearchAsync(session, new ReceiptEnquiryFilter { ReceiptNoFrom = sample.No, ReceiptNoTo = sample.No });
        Assert.Contains(listed, r => r.Id == sample.Id && r.ReversedBy == newNo);
    }

    [Fact]
    public async Task The_short_payment_list_holds_only_part_paid_invoices_with_a_balance()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var rows = await new InvoiceReceiptRepository().SearchAsync(session, new InvoiceReceiptFilter { PartPaidOnly = true, OutstandingOnly = false });
        Assert.All(rows, r => Assert.True(r.Receipts > 0 && r.Outstanding > 0, r.InvoiceNo));
    }
}

/// <summary>Finance &gt; Reverse Receipt over HTTP.</summary>
public class FinanceReverseReceiptApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Reversing_needs_menu_right_9_and_a_date_and_period()
    {
        using var otherRights = factory.ClientWith("111111110111111111111111111111");
        Assert.Equal(HttpStatusCode.Forbidden, (await otherRights.PostAsJsonAsync("/api/finance/reverse-receipt/1", new { })).StatusCode);
        using var client = factory.ClientWith(ApiFactory.AllRights);
        var response = await client.PostAsJsonAsync("/api/finance/reverse-receipt/1", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Please Enter Reversal Date", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString());
    }
}
