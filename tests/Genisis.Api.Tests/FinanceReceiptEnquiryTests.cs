using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Finance;
using Genisis.Api.Finance.ReceiptEnquiry;

namespace Genisis.Api.Tests;

/// <summary>Finance &gt; Receipt Enquiry: validation and the service's figures.</summary>
public class FinanceReceiptEnquiryTests
{
    private sealed class FakeRepository : IReceiptEnquiryRepository
    {
        public List<ReceiptRow> Rows { get; set; } = [];
        public (ReceiptRow, List<ReceiptAllocation>, List<ReceiptInvoice>, List<ReceiptPosting>)? Detail { get; set; }

        public Task<(List<CodeName> Types, List<CodeName> Payors)> LookupsAsync(IDbSession session) =>
            Task.FromResult<(List<CodeName>, List<CodeName>)>(([new("IPCLM", "IPCLM")], [new("ST", "STM")]));

        public Task<List<ReceiptRow>> SearchAsync(IDbSession session, ReceiptEnquiryFilter filter) => Task.FromResult(Rows);

        public Task<(ReceiptRow Receipt, List<ReceiptAllocation> Allocations, List<ReceiptInvoice> Invoices, List<ReceiptPosting> Postings)?> GetAsync(IDbSession session, int id) =>
            Task.FromResult(Detail);
    }

    [Fact]
    public void A_search_needs_one_criterion_and_valid_values()
    {
        var validator = new ReceiptEnquiryFilterValidator();
        Assert.Equal("Please enter at least one search criterion.", validator.FirstError(new ReceiptEnquiryFilter()));
        Assert.Null(validator.FirstError(new ReceiptEnquiryFilter { ReceiptNoFrom = "RB55000" }));
        Assert.Null(validator.FirstError(new ReceiptEnquiryFilter { ReceiptDateFrom = new DateTime(2021, 7, 1) }));
        Assert.Equal("Receipt Date from cannot be after Receipt Date to.",
            validator.FirstError(new ReceiptEnquiryFilter { ReceiptDateFrom = new DateTime(2021, 8, 1), ReceiptDateTo = new DateTime(2021, 7, 1) }));
        Assert.Equal("Select a valid posted status.", validator.FirstError(new ReceiptEnquiryFilter { Posted = "9" }));
    }

    [Fact]
    public async Task The_search_totals_the_listed_receipts_and_reports_a_cut_off()
    {
        var repository = new FakeRepository { Rows = [new() { Id = 1, Amount = 10.5m }, new() { Id = 2, Amount = -2m }, new() { Id = 3 }] };
        var result = await new ReceiptEnquiryService(new FakeSessions(), repository).SearchAsync(new ReceiptEnquiryFilter { Payor = "ST" });
        Assert.Equal((3, 8.5m, false), (result.Rows.Count, result.Total, result.Truncated));

        repository.Rows = Enumerable.Range(0, FinanceLimits.FetchRows).Select(i => new ReceiptRow { Id = i, Amount = 1m }).ToList();
        var capped = await new ReceiptEnquiryService(new FakeSessions(), repository).SearchAsync(new ReceiptEnquiryFilter { Payor = "ST" });
        Assert.True(capped.Truncated);
        Assert.Equal(FinanceLimits.MaxRows, capped.Rows.Count);
    }

    [Fact]
    public async Task A_claim_receipt_shows_its_allocated_total_and_what_is_left()
    {
        var repository = new FakeRepository
        {
            Detail = (new ReceiptRow { Id = 7, ReceiptNo = "RB55092", Amount = 5413.35m }, [new() { Allocated = 1634.90m }, new() { Allocated = -265m }], [], []),
        };
        var detail = await new ReceiptEnquiryService(new FakeSessions(), repository).GetAsync(7);
        Assert.Equal((2, 1369.90m, 4043.45m), (detail.Receipt.AllocationLines, detail.Receipt.Allocated, detail.Unallocated));
    }

    [Fact]
    public async Task A_fee_receipt_has_no_allocation_figures_and_a_missing_receipt_is_not_found()
    {
        var repository = new FakeRepository { Detail = (new ReceiptRow { Id = 8, Amount = 285.28m, Description = "S34041" }, [], [new() { InvoiceNo = "S34041", BillAmount = 495m }], []) };
        var detail = await new ReceiptEnquiryService(new FakeSessions(), repository).GetAsync(8);
        Assert.Null(detail.Unallocated);
        Assert.Null(detail.Receipt.Allocated);
        Assert.Single(detail.Invoices);

        repository.Detail = null;
        var error = await Assert.ThrowsAsync<NotFoundException>(() => new ReceiptEnquiryService(new FakeSessions(), repository).GetAsync(9));
        Assert.Equal("No record found!", error.Message);
    }
}

/// <summary>Finance &gt; Receipt Enquiry against the development databases (GENISIS_TEST_MAINDB); read only.</summary>
public class FinanceReceiptEnquiryDatabaseTests
{
    [Fact]
    public async Task Lookups_search_and_a_claim_receipt_with_lines_are_read()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        var service = new ReceiptEnquiryService(TestDb.Sessions(), new ReceiptEnquiryRepository());
        var lookups = await service.LookupsAsync();
        Assert.NotEmpty(lookups.Payors);

        await using var session = await TestDb.Sessions().OpenAsync();
        var sample = await session.Connection.QueryFirstOrDefaultAsync<(int Id, string No)>(
            "SELECT TOP 1 s.ReceiptIndex, RTRIM(s.ReceiptNo) FROM genisis.Pay_ReceiptSummary s WHERE EXISTS (SELECT 1 FROM genisis.Pay_ReceiptfrPAY p WHERE p.ReceiptNo = s.ReceiptNo) ORDER BY s.ReceiptIndex DESC");
        if (sample.No is null) return;

        var found = await service.SearchAsync(new ReceiptEnquiryFilter { ReceiptNoFrom = sample.No, ReceiptNoTo = sample.No });
        var row = Assert.Single(found.Rows, r => r.Id == sample.Id);
        Assert.True(row.AllocationLines > 0);

        var detail = await service.GetAsync(sample.Id);
        Assert.Equal(row.AllocationLines, detail.Allocations.Count);
        Assert.Equal(row.Allocated, detail.Receipt.Allocated);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(-1));
    }
}

/// <summary>Finance &gt; Receipt Enquiry over HTTP: login, menu right 9 and validation.</summary>
public class FinanceReceiptEnquiryApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly string[] Urls = ["/api/finance/receipt-enquiry/lookups", "/api/finance/receipt-enquiry/search?payor=ST", "/api/finance/receipt-enquiry/1"];

    [Fact]
    public async Task Every_endpoint_needs_a_login_and_menu_right_9()
    {
        using var anonymous = factory.ClientWith(null);
        using var otherRights = factory.ClientWith("111111110111111111111111111111");
        foreach (var url in Urls)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(url)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await otherRights.GetAsync(url)).StatusCode);
        }
    }

    [Fact]
    public async Task A_search_without_criteria_is_refused_with_the_message()
    {
        using var client = factory.ClientWith(ApiFactory.AllRights);
        var response = await client.GetAsync("/api/finance/receipt-enquiry/search");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Please enter at least one search criterion.", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString());
    }
}
