using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Genisis.Api.Data;
using Genisis.Api.Finance;
using Genisis.Api.Finance.InvoiceReceipt;
using Genisis.Api.Finance.ReceiptEnquiry;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Finance &gt; Invoice Receipt / Notification-Suspension: checks, numbering and the unit of work.</summary>
public class FinanceInvoiceReceiptTests
{
    private sealed class FakeRepository : IInvoiceReceiptRepository
    {
        public int Result { get; set; } = StoredProcedures.Ok;
        public List<string> Prefixes { get; } = [];
        public List<(string No, string Description, string? Type)> Inserted { get; } = [];

        public Task<(List<CodeName> Prefixes, List<CodeName> Payors)> LookupsAsync(IDbSession session) =>
            Task.FromResult<(List<CodeName>, List<CodeName>)>(([new("RB", "RB")], [new("CT", "CT")]));

        public Task<List<OutstandingInvoice>> SearchAsync(IDbSession session, InvoiceReceiptFilter filter) =>
            Task.FromResult(new List<OutstandingInvoice> { new() { InvoiceNo = "S1", Outstanding = 10m }, new() { InvoiceNo = "S2", Outstanding = -2.5m } });

        public Task<List<ReceiptRow>> ReceiptsAsync(IDbSession session, string invoiceNo) => Task.FromResult(new List<ReceiptRow>());

        public Task<string> NextNumberAsync(IDbSession session, string prefix)
        {
            Prefixes.Add(prefix);
            return Task.FromResult(prefix + "55638");
        }

        public Task<(int Result, int? Id)> InsertAsync(IDbSession session, string receiptNo, InvoiceReceiptRequest request, string description)
        {
            Inserted.Add((receiptNo, description, request.ReceiptType));
            return Task.FromResult<(int, int?)>(Result == StoredProcedures.Ok ? (0, 99) : (Result, null));
        }
    }

    private static InvoiceReceiptRequest Valid() => new()
    {
        InvoiceNo = "S34041", ReceiptType = "IPFEE", ReceiptDate = new DateTime(2021, 8, 10), Amount = 209.72m, ChequeNo = "ONLINE", Payor = "CT",
    };

    private static InvoiceReceiptService Service(FakeSessions sessions, FakeRepository repository) =>
        new(sessions, repository, NullLogger<InvoiceReceiptService>.Instance);

    [Fact]
    public void Save_checks_follow_the_desktop_order()
    {
        var validator = new InvoiceReceiptRequestValidator();
        Assert.Null(validator.FirstError(Valid()));
        Assert.Equal("Please select the invoice", validator.FirstError(new InvoiceReceiptRequest()));
        Assert.Equal("Please Enter Rec. Type", validator.FirstError(new InvoiceReceiptRequest { InvoiceNo = "S1" }));
        Assert.Equal("Select a valid receipt type.", validator.FirstError(new InvoiceReceiptRequest { InvoiceNo = "S1", ReceiptType = "IPCLM" }));
        Assert.Equal("Please Enter Receipt Date", validator.FirstError(new InvoiceReceiptRequest { InvoiceNo = "S1", ReceiptType = "IPFEE" }));
        Assert.Equal("Please Enter Receipt Amount", validator.FirstError(new InvoiceReceiptRequest { InvoiceNo = "S1", ReceiptType = "IPFEE", ReceiptDate = DateTime.Today }));
        var zero = Valid();
        zero.Amount = 0;
        Assert.Equal("Receipt Amount cannot be zero.", validator.FirstError(zero));
    }

    [Fact]
    public void The_manual_form_also_needs_the_receipt_number()
    {
        var validator = new ManualInvoiceReceiptRequestValidator();
        Assert.Equal("Please Enter Receipt No", validator.FirstError(new ManualInvoiceReceiptRequest()));
        Assert.Null(validator.FirstError(new ManualInvoiceReceiptRequest
        {
            ReceiptNo = "RB99999", InvoiceNo = "S1", ReceiptType = "OPFEE", ReceiptDate = DateTime.Today, Amount = 1m,
        }));
    }

    [Fact]
    public async Task An_auto_receipt_takes_the_next_number_of_the_prefix_and_defaults_the_description_to_the_invoice()
    {
        var sessions = new FakeSessions();
        var repository = new FakeRepository();
        var request = Valid();
        request.Prefix = " rc ";
        var result = await Service(sessions, repository).RecordAsync(request, "TESTER");
        Assert.Equal((99, "RC55638", "Record Saved..."), (result.Id, result.ReceiptNo, result.Message));
        Assert.Equal(["RC"], repository.Prefixes);
        Assert.Equal(("RC55638", "S34041", "IPFEE"), repository.Inserted.Single());
        Assert.True(sessions.Last.Committed);

        await Service(new FakeSessions(), repository).RecordAsync(Valid(), "TESTER");
        Assert.Equal("RB", repository.Prefixes[^1]);
    }

    [Fact]
    public async Task A_manual_receipt_keeps_the_typed_number_and_a_used_number_is_refused()
    {
        var repository = new FakeRepository();
        var manual = new ManualInvoiceReceiptRequest
        {
            ReceiptNo = " RB12345 ", InvoiceNo = "S1", ReceiptType = "IPFEE", ReceiptDate = DateTime.Today, Amount = 5m, Description = "IC-S1",
        };
        Assert.Equal("RB12345", (await Service(new FakeSessions(), repository).RecordManualAsync(manual, "TESTER")).ReceiptNo);
        Assert.Empty(repository.Prefixes);
        Assert.Equal("IC-S1", repository.Inserted.Single().Description);

        repository.Result = StoredProcedures.Conflict;
        var sessions = new FakeSessions();
        var error = await Assert.ThrowsAsync<FinanceException>(() => Service(sessions, repository).RecordManualAsync(manual, "TESTER"));
        Assert.Equal("Receipt No RB12345 already exists.", error.Message);
        Assert.True(sessions.Last.RolledBack);
    }

    [Fact]
    public async Task The_invoice_search_totals_the_balances()
    {
        var result = await Service(new FakeSessions(), new FakeRepository()).SearchAsync(new InvoiceReceiptFilter());
        Assert.Equal((2, 7.5m, false), (result.Rows.Count, result.TotalOutstanding, result.Truncated));
    }
}

/// <summary>Finance &gt; Invoice Receipt against the development databases (GENISIS_TEST_MAINDB); writes are rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class FinanceInvoiceReceiptDatabaseTests
{
    [Fact]
    public async Task A_receipt_takes_the_next_number_reduces_the_balance_refuses_a_reused_number_and_rolls_back()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        var repository = new InvoiceReceiptRepository();
        var sessions = TestDb.Sessions();
        string before;
        OutstandingInvoice invoice;
        await using (var read = await sessions.OpenAsync())
        {
            before = await read.Connection.ExecuteScalarAsync<string>("SELECT RecSeqNo FROM genisis.Pay_RecSeq WHERE RCInd = 'RB'") ?? "";
            var open = await repository.SearchAsync(read, new InvoiceReceiptFilter { InvoiceDateFrom = new DateTime(2021, 7, 1), OutstandingOnly = true });
            if (open.Count == 0) return;
            invoice = open[0];
        }

        await using (var session = await sessions.BeginAsync())
        {
            var number = await repository.NextNumberAsync(session, "RB");
            Assert.Equal($"RB{int.Parse(before) + 1:D5}", number);
            var request = new InvoiceReceiptRequest { InvoiceNo = invoice.InvoiceNo, ReceiptType = "IPFEE", ReceiptDate = new DateTime(2021, 8, 10), Amount = 1.25m, Payor = invoice.Payor };
            var (result, id) = await repository.InsertAsync(session, number, request, invoice.InvoiceNo);
            Assert.Equal(StoredProcedures.Ok, result);
            Assert.NotNull(id);
            Assert.Equal(StoredProcedures.Conflict, (await repository.InsertAsync(session, number, request, invoice.InvoiceNo)).Result);

            var after = Assert.Single(await repository.SearchAsync(session, new InvoiceReceiptFilter { InvoiceNoFrom = invoice.InvoiceNo, InvoiceNoTo = invoice.InvoiceNo, OutstandingOnly = false }));
            Assert.Equal(invoice.Outstanding - 1.25m, after.Outstanding);
            Assert.Contains(await repository.ReceiptsAsync(session, invoice.InvoiceNo), r => r.ReceiptNo == number && r.Description == invoice.InvoiceNo);
        }

        await using var check = await sessions.OpenAsync();
        Assert.Equal(before, await check.Connection.ExecuteScalarAsync<string>("SELECT RecSeqNo FROM genisis.Pay_RecSeq WHERE RCInd = 'RB'"));
    }
}

/// <summary>Finance &gt; Invoice Receipt over HTTP: rights 9 / 12 and validation.</summary>
public class FinanceInvoiceReceiptApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Only9 = "000000001000000000000000000000";
    private const string Only12 = "000000000001000000000000000000";

    [Fact]
    public async Task Reading_needs_9_or_12_auto_numbering_needs_9_and_manual_numbering_needs_12()
    {
        using var anonymous = factory.ClientWith(null);
        using var none = factory.ClientWith(ApiFactory.NoRights);
        using var nine = factory.ClientWith(Only9);
        using var twelve = factory.ClientWith(Only12);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/finance/invoice-receipt/lookups")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await none.GetAsync("/api/finance/invoice-receipt/lookups")).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await nine.PostAsJsonAsync("/api/finance/invoice-receipt/receipts", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await nine.PostAsJsonAsync("/api/finance/invoice-receipt/receipts/manual", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await twelve.PostAsJsonAsync("/api/finance/invoice-receipt/receipts/manual", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await twelve.PostAsJsonAsync("/api/finance/invoice-receipt/receipts", new { })).StatusCode);
    }

    [Theory]
    [InlineData("/api/finance/invoice-receipt/receipts", "Please select the invoice")]
    [InlineData("/api/finance/invoice-receipt/receipts/manual", "Please Enter Receipt No")]
    public async Task An_empty_save_gets_the_first_message(string url, string message)
    {
        using var client = factory.ClientWith(ApiFactory.AllRights);
        var response = await client.PostAsJsonAsync(url, new { });
        Assert.Equal(message, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString());
    }
}
