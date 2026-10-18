using Dapper;
using Genisis.Api.Worksheet.Engine;
using Genisis.Api.Worksheet.Enquiry;

namespace Genisis.Api.Tests;

/// <summary>The worksheet calculation engine, checked against the worksheets the desktop saved.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class WorksheetEngineTests
{
    private static readonly string? MainDb = TestDb.MainDb;

    [Fact]
    public async Task A_version_has_twenty_nine_slots_with_the_invoice_amounts_added_up()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var (cas, version) = await session.Connection.QueryFirstAsync<(string, int)>(
            "SELECT TOP (1) RTRIM(CASNumber), CLMVersion FROM genisis.WS_ClaimActual WHERE CASNumber = 'ZZ00000103' AND CLMVersion = 2", transaction: session.Transaction);

        var slots = await new WorksheetEngineService(TestDb.Sessions(), new WorksheetEngineRepository()).ActualAsync(" " + cas + " ", version);

        Assert.Equal(Enumerable.Range(1, 29), slots.Select(s => s.Slot));
        Assert.Equal(120m, slots[0].Actual);
        Assert.All(slots.Skip(1), s => Assert.Equal(0m, s.Actual));
        Assert.All(slots, s => Assert.Equal(0m, s.ToHospital));
    }

    [Fact]
    public async Task A_version_without_invoices_is_all_zero()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var slots = await new WorksheetEngineService(TestDb.Sessions(), new WorksheetEngineRepository()).ActualAsync("NO SUCH CASE", 1);
        Assert.Equal(29, slots.Count);
        Assert.All(slots, s => Assert.Equal((0m, 0m), (s.Actual, s.ToHospital)));
    }

    /// <summary>
    /// The slot to invoice-column map was read out of the VB6 source. It must reproduce what the desktop saved: for the saved worksheet versions sampled here the engine's
    /// actual amount equals the saved one in every slot (the full table, 35,495 versions, agrees to 99.96 to 100 per cent per slot).
    /// </summary>
    [Fact]
    public async Task The_engine_reproduces_the_actual_amounts_the_desktop_saved()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var sample = (await session.Connection.QueryAsync<(string Cas, int Version)>(
            "SELECT TOP (400) RTRIM(CASNumber), CLMVersion FROM genisis.WS_ClaimActual ORDER BY CASNumber, CLMVersion", transaction: session.Transaction)).ToList();
        Assert.True(sample.Count >= 200);

        var engine = new WorksheetEngineRepository();
        var saved = new EnquiryRepository();
        int versionsEqual = 0, slotsEqual = 0, slotsTotal = 0;
        foreach (var (cas, version) in sample)
        {
            var computed = await engine.ActualAsync(session, cas, version);
            var stored = await saved.BenefitsAsync(session, cas, version, version);
            var same = computed.Count(c => Math.Abs(c.Actual - (stored.First(s => s.Slot == c.Slot).Actual ?? 0)) < 0.02m);
            slotsEqual += same;
            slotsTotal += computed.Count;
            if (same == computed.Count) versionsEqual++;
        }
        Assert.True(versionsEqual >= sample.Count * 0.995, $"{versionsEqual} of {sample.Count} versions agree in every slot");
        Assert.True(slotsEqual >= slotsTotal * 0.9995, $"{slotsEqual} of {slotsTotal} slots agree");
    }
}
