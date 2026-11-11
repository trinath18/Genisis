using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Invoice;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Invoice > Original Invoice Update, without a database.</summary>
public class InvoiceOriginalRulesTests
{
    private sealed class FakeRepository : IInvoiceOriginalRepository
    {
        public List<OriginalInvoice> Invoices { get; } = [];
        public int Changed { get; set; } = 1;
        public (int Id, OriginalInvoiceUpdate Update)? Updated { get; private set; }

        public Task<List<OriginalInvoice>> FindAsync(IDbSession session, string caseNo, string version) => Task.FromResult(Invoices.Where(i => i.CaseNo == caseNo && i.Version == version).ToList());

        public Task<int> UpdateAsync(IDbSession session, int id, OriginalInvoiceUpdate update)
        {
            Updated = (id, update);
            return Task.FromResult(Changed);
        }

        public Task<List<InvoiceOption>> HospitalsAsync(IDbSession session) => Task.FromResult(new List<InvoiceOption> { new("H1", "A HOSPITAL") });
    }

    private static (InvoiceOriginalService Service, FakeRepository Repository, FakeSessions Sessions) Build()
    {
        var repository = new FakeRepository();
        var sessions = new FakeSessions();
        return (new InvoiceOriginalService(sessions, repository, NullLogger<InvoiceOriginalService>.Instance), repository, sessions);
    }

    private static OriginalInvoiceUpdate Valid() => new() { InvoiceNo = "INV-1", OriginalReceivedDate = new DateTime(2021, 8, 6), InvoiceDate = new DateTime(2021, 8, 1), HospitalCode = "H1" };

    [Fact]
    public void The_update_checks_run_in_the_desktops_order_and_use_its_messages()
    {
        var validator = new OriginalInvoiceUpdateValidator();
        Assert.Equal("Please Enter Invoice No!", validator.FirstError(new OriginalInvoiceUpdate()));
        Assert.Equal("Please Enter Invoice No!", validator.FirstError(new OriginalInvoiceUpdate { InvoiceNo = "  " }));
        Assert.Equal("Please Enter Invoice Date!", validator.FirstError(new OriginalInvoiceUpdate { InvoiceNo = "INV-1" }));
        Assert.Null(validator.FirstError(Valid()));
        Assert.Null(validator.FirstError(new OriginalInvoiceUpdate { InvoiceNo = "INV-1", OriginalReceivedDate = DateTime.Today }));
    }

    [Fact]
    public async Task Showing_needs_a_case_and_a_version_and_a_record()
    {
        var (service, repository, _) = Build();
        Assert.Equal("Please Enter Case No", (await Assert.ThrowsAsync<BusinessRuleException>(() => service.FindAsync(" ", "1"))).Message);
        Assert.Equal("Please Enter Case Version", (await Assert.ThrowsAsync<BusinessRuleException>(() => service.FindAsync("AB1", null))).Message);
        Assert.Equal("No record found!", (await Assert.ThrowsAsync<NotFoundException>(() => service.FindAsync("AB1", "1"))).Message);
        repository.Invoices.Add(new OriginalInvoice { Id = 7, CaseNo = "AB1", Version = "1" });
        Assert.Equal(7, Assert.Single(await service.FindAsync(" AB1 ", " 1 ")).Id);
    }

    [Fact]
    public async Task An_update_is_one_committed_transaction_and_a_missing_invoice_is_not_found_and_rolled_back()
    {
        var (service, repository, sessions) = Build();
        await service.UpdateAsync(5, Valid(), "E2E");
        Assert.Equal(5, repository.Updated?.Id);
        Assert.True(sessions.Last.Transactional);
        Assert.True(sessions.Last.Committed);

        repository.Changed = 0;
        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(6, Valid(), "E2E"));
        Assert.False(sessions.Last.Committed);
        Assert.True(sessions.Last.RolledBack);
    }
}

/// <summary>Invoice > Original Invoice Update, against the database; writes happen inside a transaction that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class InvoiceOriginalDatabaseTests
{
    [Fact]
    public async Task An_invoice_is_found_by_case_and_version_and_updated_in_a_rolled_back_transaction()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        var repository = new InvoiceOriginalRepository();
        await using var read = await TestDb.Sessions().OpenAsync();
        var picked = await read.Connection.QueryFirstAsync<(string Cas, string Version)>(
            "SELECT TOP (1) RTRIM(i.CASNumber), RTRIM(i.INVWSversion) FROM genisis.WS_ClaimInvoice i JOIN dbo.HOS h ON h.HOSCode = i.INVPayee WHERE i.CASNumber NOT LIKE 'ZZ%' AND i.INVOrigRecStatus = 'Y' AND i.InvNo <> '' " +
            "GROUP BY i.CASNumber, i.INVWSversion HAVING COUNT(*) = 1 ORDER BY i.CASNumber", transaction: read.Transaction);
        var found = Assert.Single(await repository.FindAsync(read, picked.Cas, picked.Version));
        Assert.Equal(picked.Cas, found.CaseNo);
        Assert.False(string.IsNullOrWhiteSpace(found.InvoiceNo));
        Assert.False(string.IsNullOrWhiteSpace(found.HospitalName));
        Assert.Empty(await repository.FindAsync(read, "NO SUCH CASE", "1"));
        var hospitals = await repository.HospitalsAsync(read);
        Assert.True(hospitals.Count > 100);

        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var other = hospitals.First(h => h.Code != found.HospitalCode);
            var changed = await repository.UpdateAsync(write, found.Id, new OriginalInvoiceUpdate
            {
                InvoiceNo = "E2E-TEST", OriginalReceivedDate = new DateTime(2020, 1, 2), InvoiceDate = new DateTime(2020, 1, 1), HospitalCode = other.Code,
            });
            Assert.Equal(1, changed);
            var after = Assert.Single(await repository.FindAsync(write, picked.Cas, picked.Version), i => i.Id == found.Id);
            Assert.Equal(("E2E-TEST", new DateTime(2020, 1, 2), new DateTime(2020, 1, 1), "Y", other.Code), (after.InvoiceNo, after.OriginalReceivedDate, after.InvoiceDate, after.OriginalStatus, after.HospitalCode));

            // a blank invoice date leaves the date, and a code that is not a hospital leaves the hospital
            await repository.UpdateAsync(write, found.Id, new OriginalInvoiceUpdate { InvoiceNo = "E2E-TEST-2", OriginalReceivedDate = new DateTime(2020, 1, 3), HospitalCode = "NOT A HOSPITAL" });
            var again = Assert.Single(await repository.FindAsync(write, picked.Cas, picked.Version), i => i.Id == found.Id);
            Assert.Equal((new DateTime(2020, 1, 1), other.Code), (again.InvoiceDate, again.HospitalCode));
            Assert.Equal(0, await repository.UpdateAsync(write, -1, Valid()));
        }

        var unchanged = Assert.Single(await repository.FindAsync(read, picked.Cas, picked.Version), i => i.Id == found.Id);
        Assert.Equal((found.InvoiceNo, found.InvoiceDate, found.OriginalReceivedDate, found.HospitalCode), (unchanged.InvoiceNo, unchanged.InvoiceDate, unchanged.OriginalReceivedDate, unchanged.HospitalCode));

        static OriginalInvoiceUpdate Valid() => new() { InvoiceNo = "x", OriginalReceivedDate = new DateTime(2020, 1, 1) };
    }
}
