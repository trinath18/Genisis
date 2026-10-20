using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Invoice;

namespace Genisis.Api.Tests;

/// <summary>Invoice Processing, read side, against the database.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class InvoiceProcessingDatabaseTests
{
    private static InvoiceService Service() => new(TestDb.Sessions(), new InvoiceRepository());

    [Fact]
    public async Task A_case_gives_its_header_and_its_invoices_oldest_first()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var cas = await session.Connection.ExecuteScalarAsync<string>(
            "SELECT TOP (1) RTRIM(i.CASNumber) FROM genisis.WS_ClaimInvoice i JOIN dbo.CAS c ON c.CASNumber = i.CASNumber JOIN dbo.MBM m ON m.MBMNumber = c.MBMNumber WHERE i.CASNumber NOT LIKE 'ZZ%' " +
            "GROUP BY i.CASNumber HAVING COUNT(*) >= 2 ORDER BY i.CASNumber", transaction: session.Transaction);
        Assert.NotNull(cas);

        var result = await Service().GetCaseAsync(cas!);
        Assert.Equal(cas, result.Header.CaseNo);
        Assert.False(string.IsNullOrWhiteSpace(result.Header.MemberNo));
        Assert.True(result.Invoices.Count >= 2);
        Assert.Equal(result.Invoices.Select(i => i.Index).OrderBy(i => i), result.Invoices.Select(i => i.Index));
        Assert.All(result.Invoices, i => Assert.False(string.IsNullOrWhiteSpace(i.InvoiceNo)));
    }

    [Fact]
    public async Task An_invoice_gives_29_rows_that_add_up_to_its_amount_for_most_invoices()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var cases = (await session.Connection.QueryAsync<string>(
            "SELECT TOP (40) RTRIM(i.CASNumber) FROM genisis.WS_ClaimInvoice i JOIN dbo.CAS c ON c.CASNumber = i.CASNumber WHERE i.CASNumber NOT LIKE 'ZZ%' AND ABS(CHECKSUM(i.CASNumber)) % 500 = 0 GROUP BY i.CASNumber ORDER BY i.CASNumber",
            transaction: session.Transaction)).ToList();
        int invoices = 0, matching = 0;
        foreach (var cas in cases)
            foreach (var summary in (await Service().GetCaseAsync(cas)).Invoices)
            {
                var detail = await Service().GetInvoiceAsync(cas, summary.Index);
                Assert.Equal(Enumerable.Range(1, 29), detail.Rows.Select(r => r.Slot));
                Assert.All(detail.Rows, r => Assert.False(string.IsNullOrWhiteSpace(r.Benefit)));
                Assert.Equal(detail.Rows.Sum(r => r.Amount), detail.RowsTotal);
                invoices++;
                matching += detail.RowsMatchAmount ? 1 : 0;
            }
        Assert.True(invoices >= 30, $"only {invoices} invoices");
        Assert.True(matching >= invoices * 0.90, $"rows add up to the amount for {matching} of {invoices}");
    }

    [Fact]
    public async Task An_unknown_case_or_invoice_is_record_not_found()
    {
        var mainDb = TestDb.MainDb;
        if (mainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(mainDb);
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => Service().GetCaseAsync("NO SUCH CASE"));
        Assert.Equal("Record not found!", ex.Message);
        await using var session = await TestDb.Sessions().OpenAsync();
        var cas = await session.Connection.ExecuteScalarAsync<string>("SELECT TOP (1) RTRIM(i.CASNumber) FROM genisis.WS_ClaimInvoice i JOIN dbo.CAS c ON c.CASNumber = i.CASNumber WHERE i.CASNumber NOT LIKE 'ZZ%'", transaction: session.Transaction);
        await Assert.ThrowsAsync<NotFoundException>(() => Service().GetInvoiceAsync(cas!, -5));
    }
}
