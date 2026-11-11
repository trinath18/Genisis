using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Payment.ContraAllocation;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Payment &gt; Contra Allocation: the validators and the service, against a fake repository.</summary>
public class ContraAllocationRulesTests
{
    private sealed class FakeRepository : IContraAllocationRepository
    {
        public List<ContraAllocationRow> Rows { get; set; } = [];

        public Dictionary<int, int> Results { get; } = [];

        public List<string> Calls { get; } = [];

        public Task<List<ContraAllocationRow>> SearchAsync(IDbSession session, ContraAllocationFilter filter, int max)
        {
            Calls.Add($"search {max}");
            return Task.FromResult(Rows);
        }

        public Task<string> NextNumberAsync(IDbSession session)
        {
            Calls.Add("number");
            return Task.FromResult("003105");
        }

        public Task<int> AllocateAsync(IDbSession session, int id, string allocationNo, DateTime today)
        {
            Calls.Add($"allocate {id} {allocationNo} {today:yyyy-MM-dd}");
            return Task.FromResult(Results.GetValueOrDefault(id));
        }
    }

    private static ContraAllocationService Service(FakeRepository repository, FakeSessions sessions) => new(sessions, repository, NullLogger<ContraAllocationService>.Instance);

    [Fact]
    public void The_status_and_the_number_boxes_are_checked()
    {
        var validator = new ContraAllocationFilterValidator();
        Assert.Null(validator.FirstError(new ContraAllocationFilter()));
        Assert.Equal("Select a valid status.", validator.FirstError(new ContraAllocationFilter { Status = 3 }));
        Assert.Equal("A number is too long (15 characters at most).", validator.FirstError(new ContraAllocationFilter { CaseFrom = new string('x', 16) }));
        var request = new ContraAllocationRequestValidator();
        Assert.Equal("Please select the record", request.FirstError(new ContraAllocationRequest()));
        Assert.Equal("Please select the record", request.FirstError(new ContraAllocationRequest { Ids = [] }));
        Assert.Null(request.FirstError(new ContraAllocationRequest { Ids = [1] }));
    }

    [Fact]
    public async Task Nothing_found_is_not_found_and_more_than_5000_rows_are_cut()
    {
        var repository = new FakeRepository();
        Assert.Equal("No record found!", (await Assert.ThrowsAsync<NotFoundException>(() => Service(repository, new FakeSessions()).SearchAsync(new ContraAllocationFilter()))).Message);
        repository.Rows = Enumerable.Range(1, 5001).Select(i => new ContraAllocationRow { Id = i }).ToList();
        var result = await Service(repository, new FakeSessions()).SearchAsync(new ContraAllocationFilter());
        Assert.Equal((5000, true), (result.Rows.Count, result.Truncated));
        Assert.Contains("search 5001", repository.Calls);
    }

    [Fact]
    public async Task Allocating_gives_every_row_ticked_one_number_in_one_transaction()
    {
        var repository = new FakeRepository();
        var sessions = new FakeSessions();
        var done = await Service(repository, sessions).AllocateAsync(new ContraAllocationRequest { Ids = [5, 7, 5] }, "USER1");
        Assert.Equal(new ContraAllocationDone("003105", 2), done);
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        Assert.Equal(["number", $"allocate 5 003105 {today}", $"allocate 7 003105 {today}"], repository.Calls);
        Assert.True(sessions.Last.Committed);
    }

    [Fact]
    public async Task A_row_that_is_missing_allocated_already_or_without_a_claim_refuses_the_whole_allocation()
    {
        foreach (var (result, message) in new[] { (1, "Record not found."), (2, "Record has been allocated!"), (3, "The record has no claim to allocate.") })
        {
            var repository = new FakeRepository();
            repository.Results[7] = result;
            var sessions = new FakeSessions();
            var error = await Assert.ThrowsAnyAsync<BusinessRuleException>(() => Service(repository, sessions).AllocateAsync(new ContraAllocationRequest { Ids = [5, 7] }, "U"));
            Assert.Equal(message, error.Message);
            Assert.False(sessions.Last.Committed);
        }
    }
}

/// <summary>Payment &gt; Contra Allocation against the database. Every write is made in a session that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class ContraAllocationDatabaseTests
{
    private static ContraAllocationRepository Repository => new();

    private const string Checksum = "SELECT CHECKSUM_AGG(CHECKSUM(CONIndex, RefNo, AllocationNo, AllocationDate, CaseNo, VersionNo, CaseAmt, ContraCaseNo, ContraVersionNo, ContraCaseAmt, ContraStatus)) FROM genisis.Pay_ContraAllocation";

    [Fact]
    public async Task The_search_filters_by_status_case_contra_case_allocation_and_date_and_a_ref_from_alone_is_a_lower_bound()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var all = await Repository.SearchAsync(session, new ContraAllocationFilter(), 100000);
        var allocated = await Repository.SearchAsync(session, new ContraAllocationFilter { Status = 1 }, 100000);
        var open = await Repository.SearchAsync(session, new ContraAllocationFilter { Status = 2 }, 100000);
        Assert.Equal(all.Count, allocated.Count + open.Count);
        Assert.All(allocated, r => Assert.Equal("A", r.ContraStatus));
        Assert.All(open, r => Assert.True(string.IsNullOrEmpty(r.ContraStatus)));
        Assert.Equal(await session.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.Pay_ContraAllocation", transaction: session.Transaction), all.Count);

        var one = allocated[0];
        var byCase = await Repository.SearchAsync(session, new ContraAllocationFilter { ScanCase = one.CaseNo }, 1000);
        Assert.NotEmpty(byCase);
        Assert.All(byCase, r => Assert.Equal(one.CaseNo, r.CaseNo));
        var range = await Repository.SearchAsync(session, new ContraAllocationFilter { CaseFrom = one.CaseNo, CaseTo = one.CaseNo, ContraFrom = one.ContraCaseNo, ContraTo = one.ContraCaseNo, AllocationFrom = one.AllocationNo, AllocationTo = one.AllocationNo, DateFrom = one.AllocationDate!.Value.Date, DateTo = one.AllocationDate.Value.Date }, 1000);
        Assert.Contains(range, r => r.Id == one.Id);
        var refFrom = await Repository.SearchAsync(session, new ContraAllocationFilter { RefFrom = one.RefNo }, 100000);
        Assert.Contains(refFrom, r => r.Id == one.Id);
        Assert.Empty(await Repository.SearchAsync(session, new ContraAllocationFilter { CaseFrom = "ZZZZZZZZ" }, 10));
    }

    [Fact]
    public async Task Allocating_numbers_the_rows_from_the_counter_stamps_them_and_refuses_what_cannot_be_allocated_and_nothing_survives_the_rollback()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var before = await read.Connection.ExecuteScalarAsync<int?>(Checksum, transaction: read.Transaction);
        var seq = await read.Connection.ExecuteScalarAsync<string>("SELECT CAST(ISNULL(MAX(CAST(RTRIM(AllocationNo) AS int)), 100000) AS nvarchar(10)) FROM genisis.Pay_ContraAllocation WHERE AllocationNo IS NOT NULL AND LEN(RTRIM(AllocationNo)) = 6 AND RTRIM(AllocationNo) NOT LIKE N'%[^0-9]%'", transaction: read.Transaction);
        var openRows = await Repository.SearchAsync(read, new ContraAllocationFilter { Status = 2 }, 100000);
        var withClaim = openRows.Where(r => !string.IsNullOrEmpty(r.CaseNo)).Take(2).ToList();
        var receiptOnly = openRows.First(r => string.IsNullOrEmpty(r.CaseNo));
        Assert.Equal(2, withClaim.Count);

        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var expected = (int.Parse(seq!) + 1).ToString("000000");
            var number = await Repository.NextNumberAsync(write);
            Assert.Equal(expected, number);
            Assert.Equal(expected, await Repository.NextNumberAsync(write));
            Assert.Equal(0, await Repository.AllocateAsync(write, withClaim[0].Id, number, new DateTime(2026, 10, 29, 15, 0, 0)));
            Assert.Equal((int.Parse(seq!) + 2).ToString("000000"), await Repository.NextNumberAsync(write));
            var row = await write.Connection.QuerySingleAsync("SELECT RTRIM(AllocationNo) AS No, AllocationDate AS Day, RTRIM(ContraStatus) AS Status FROM genisis.Pay_ContraAllocation WHERE CONIndex = @id", new { id = withClaim[0].Id }, write.Transaction);
            Assert.Equal((number, new DateTime(2026, 10, 29, 15, 0, 0), "A"), ((string)row.No, (DateTime)row.Day, (string)row.Status));
            Assert.Equal(2, await Repository.AllocateAsync(write, withClaim[0].Id, number, DateTime.Today));
            Assert.Equal(1, await Repository.AllocateAsync(write, int.MaxValue, number, DateTime.Today));
            Assert.Equal(3, await Repository.AllocateAsync(write, receiptOnly.Id, number, DateTime.Today));
            Assert.Equal(1, await write.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.Pay_ContraAllocation WHERE CONIndex = @id AND ContraStatus = 'A'", new { id = withClaim[0].Id }, write.Transaction));
            Assert.Equal(0, await write.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.Pay_ContraAllocation WHERE CONIndex = @id AND ContraStatus = 'A'", new { id = withClaim[1].Id }, write.Transaction));
        }

        Assert.Equal(before, await read.Connection.ExecuteScalarAsync<int?>(Checksum, transaction: read.Transaction));
    }

    [Fact]
    public async Task With_no_allocation_on_the_table_the_numbers_start_at_100001_and_other_text_is_ignored()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var write = await TestDb.Sessions().BeginAsync();
        await write.Connection.ExecuteAsync("UPDATE genisis.Pay_ContraAllocation SET AllocationNo = NULL, AllocationDate = NULL, ContraStatus = NULL", transaction: write.Transaction);
        Assert.Equal("100001", await Repository.NextNumberAsync(write));
        await write.Connection.ExecuteAsync("UPDATE TOP (1) genisis.Pay_ContraAllocation SET AllocationNo = 'AR0310'", transaction: write.Transaction);
        Assert.Equal("100001", await Repository.NextNumberAsync(write));
    }

    [Fact]
    public async Task The_service_allocates_in_one_transaction_that_a_refused_row_rolls_back_with_its_number()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        var service = new ContraAllocationService(TestDb.Sessions(), Repository, NullLogger<ContraAllocationService>.Instance);
        await using var read = await TestDb.Sessions().OpenAsync();
        var before = await read.Connection.ExecuteScalarAsync<int?>(Checksum, transaction: read.Transaction);
        var open = (await Repository.SearchAsync(read, new ContraAllocationFilter { Status = 2 }, 100000)).Where(r => !string.IsNullOrEmpty(r.CaseNo)).First();
        await Assert.ThrowsAsync<NotFoundException>(() => service.AllocateAsync(new ContraAllocationRequest { Ids = [open.Id, int.MaxValue] }, "ZZTEST"));
        Assert.Equal(before, await read.Connection.ExecuteScalarAsync<int?>(Checksum, transaction: read.Transaction));
    }
}
