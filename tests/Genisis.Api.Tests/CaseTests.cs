using Genisis.Api.Case;
using Genisis.Api.Controllers;
using Genisis.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Genisis.Api.Tests;

/// <summary>Runs the case endpoints against the real databases when GENISIS_TEST_MAINDB /
/// GENISIS_TEST_WORKSHEET are set; otherwise skipped.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class CaseTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");
    private static readonly string? MaintenanceDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINTENANCE");
    private static readonly string? WorkSheetDb = Environment.GetEnvironmentVariable("GENISIS_TEST_WORKSHEET");

    private static DbConnections Db() => new(new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?>
        {
            ["ConnectionStrings:MainDb"] = MainDb,
            ["ConnectionStrings:Maintenance"] = MaintenanceDb ?? MainDb,
            ["ConnectionStrings:WorkSheet"] = WorkSheetDb ?? MainDb,
        }).Build());

    private static object? Value(IActionResult r) => Assert.IsType<OkObjectResult>(r).Value;

    [Fact]
    public async Task Search_finds_cases_and_detail_loads()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var c = new CaseController(Db());

        var found = Assert.IsAssignableFrom<System.Collections.IList>(Value(await c.Search("TA", "number")));
        Assert.NotEmpty(found);
        var casNumber = (string)((IDictionary<string, object?>)found[0]!)["CASNumber"]!;

        var detail = Assert.IsAssignableFrom<IDictionary<string, object?>>(Value(await c.Get(casNumber)));
        Assert.Equal(casNumber, detail["CASNumber"]?.ToString());
    }

    [Fact]
    public async Task Tracking_list_and_summary_run()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var c = new CaseController(Db());

        var rows = Assert.IsAssignableFrom<System.Collections.IList>(Value(await c.Tracking(new CaseTrackingFilter())));
        Assert.NotEmpty(rows);
        var summary = Assert.IsAssignableFrom<IDictionary<string, object?>>(Value(await c.Summary(new CaseTrackingFilter())));
        Assert.True(Convert.ToInt64(summary["CaseCount"]) > 0);

        var open = Assert.IsAssignableFrom<System.Collections.IList>(Value(await c.Tracking(new CaseTrackingFilter { Status = "O" })));
        Assert.All(rows.Cast<IDictionary<string, object?>>().Concat(open.Cast<IDictionary<string, object?>>()),
            r => Assert.True(r.ContainsKey("CASNumber")));
    }

    [Fact]
    public async Task Lookups_return_lists()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var c = new CaseController(Db());
        var lookups = Value(await c.Lookups())!;
        Assert.NotEmpty((System.Collections.IEnumerable)lookups.GetType().GetProperty("hospitals")!.GetValue(lookups)!);
        Assert.NotEmpty((System.Collections.IEnumerable)lookups.GetType().GetProperty("payors")!.GetValue(lookups)!);
    }

    [Fact]
    public async Task Claims_load_from_worksheet_db()
    {
        if (MainDb is null || WorkSheetDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var c = new CaseController(Db());
        var found = Assert.IsAssignableFrom<System.Collections.IList>(Value(await c.Search("TA", "number")));
        var casNumber = (string)((IDictionary<string, object?>)found[0]!)["CASNumber"]!;
        Assert.NotNull(Value(await c.Claims(casNumber)));
    }
}
