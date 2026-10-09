using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Tests;

/// <summary>Builds sql/genisis-main.sql from sql/main/*.sql and deploys it to the test database.</summary>
public static class SqlScripts
{
    public const string DefaultMaintenanceDb = "DECLARE @MaintenanceDb sysname = N'HISMaintenance';";
    public static readonly string Root = FindRoot();
    public static readonly string[] Parts = ["main", "maintenance"];
    public static string CombinedPath(string part) => Path.Combine(Root, "sql", $"genisis-{part}.sql");

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _deployed;

    public static string BuildCombined(string part)
    {
        var sb = new StringBuilder();
        sb.Append($"""
            -- GENERATED FILE: built from sql/{part}/*.sql. Edit those files, then rebuild this one with
            --     GENISIS_WRITE_SQL=1 dotnet test --filter Combined_scripts_are_up_to_date
            -- How to run: open this file in SSMS, pick the {part} database in the "Available Databases" box,
            -- {(part == "main" ? "check @MaintenanceDb in the first section, then " : "")}press F5 (Execute).
            -- Safe to run again: it only creates or replaces genisis.* procedures and synonyms; tables and data are not changed.

            """);
        foreach (var file in Directory.GetFiles(Path.Combine(Root, "sql", part), "*.sql").Order(StringComparer.Ordinal))
            sb.Append($"\n-- ===== {part}/{Path.GetFileName(file)} =====\n").Append(Normalize(File.ReadAllText(file)).TrimEnd('\n')).Append('\n');
        sb.Append("\nPRINT N'Done.';\nGO\nSET NOEXEC OFF;\nGO\n");
        return sb.ToString();
    }

    public static string Normalize(string s) => s.Replace("\r\n", "\n");

    /// <summary>Runs the combined script once per test run (it is idempotent).</summary>
    public static async Task EnsureDeployedAsync(string mainConnectionString)
    {
        await Gate.WaitAsync();
        try
        {
            if (_deployed) return;
            var maintenanceCs = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINTENANCE");
            var maintenance = maintenanceCs is null ? "" : new SqlConnectionStringBuilder(maintenanceCs).InitialCatalog;
            var main = File.ReadAllText(CombinedPath("main"));
            if (maintenance != "")
                main = main.Replace(DefaultMaintenanceDb, $"DECLARE @MaintenanceDb sysname = N'{maintenance.Replace("'", "''")}';");
            await RunAsync(mainConnectionString, main);
            if (maintenanceCs is not null) await RunAsync(maintenanceCs, File.ReadAllText(CombinedPath("maintenance")));
            _deployed = true;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task RunAsync(string connectionString, string script)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            if (!string.IsNullOrWhiteSpace(batch)) await conn.ExecuteAsync(batch);
    }

    private static string FindRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, "sql", "main"))) return d.FullName;
        throw new DirectoryNotFoundException("sql/main not found above " + AppContext.BaseDirectory);
    }
}

public class SqlScriptTests
{
    [Fact]
    public void Combined_scripts_are_up_to_date()
    {
        foreach (var part in SqlScripts.Parts)
        {
            var expected = SqlScripts.BuildCombined(part);
            var path = SqlScripts.CombinedPath(part);
            if (Environment.GetEnvironmentVariable("GENISIS_WRITE_SQL") == "1") File.WriteAllText(path, expected);
            Assert.True(File.Exists(path) && expected == SqlScripts.Normalize(File.ReadAllText(path)),
                $"sql/genisis-{part}.sql is missing or out of date; rebuild it with GENISIS_WRITE_SQL=1 dotnet test");
        }
    }

    [Fact]
    public void Setup_script_keeps_the_maintenance_db_variable_the_tests_replace()
    {
        Assert.Contains(SqlScripts.DefaultMaintenanceDb, File.ReadAllText(SqlScripts.CombinedPath("main")));
    }
}

public class ReadProcedureTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");

    [Fact]
    public async Task Lookups_and_searches_return_rows()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var db = new Genisis.Api.Data.DbConnections(new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:MainDb"] = MainDb, ["ConnectionStrings:Maintenance"] = MainDb }).Build());
        await using var conn = db.MainDb();
        using (var grid = await Genisis.Api.Data.StoredProcedures.ProcMultipleAsync(conn, "genisis.PlanMaintenance_Lookups"))
        {
            Assert.NotEmpty(await grid.ReadAsync<Genisis.Api.Controllers.PlanMaintenanceController.Option>());
            Assert.NotEmpty(await grid.ReadAsync<Genisis.Api.Controllers.PlanMaintenanceController.Option>());
            Assert.NotEmpty(await grid.ReadAsync<Genisis.Api.Controllers.PlanMaintenanceController.Option>());
            Assert.NotEmpty(await grid.ReadAsync<string>());
        }
        Assert.NotEmpty(await Genisis.Api.Data.StoredProcedures.ProcQueryAsync<Genisis.Api.Controllers.PlanMaintenanceController.Option>(conn, "genisis.Lookup_InsuredTypes"));

        var (plans, plansTruncated) = await new Genisis.Api.Plans.PlanService(db).SearchAsync(new Genisis.Api.Plans.PlanSearch());
        Assert.True(plansTruncated);
        Assert.Equal(Genisis.Api.Plans.PlanService.SearchLimit, plans.Count);
        var (limits, _) = await new Genisis.Api.Plans.AnnualLimitService(db).SearchAsync(plans[0].Code, null, null, null, null);
        Assert.All(limits, l => Assert.Equal(plans[0].Code, l.PlanCode));
        var (premiums, premiumsTruncated) = await new Genisis.Api.Plans.PremiumService(db).SearchAsync(null, null, "N", "01", null);
        Assert.True(premiumsTruncated);
        Assert.All(premiums, p => Assert.Equal(("N", "01"), (p.HealthCode, p.AgeCode)));
    }

    [Fact]
    public async Task Login_and_enquiry_procedures_run()
    {
        var maintenance = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINTENANCE");
        if (MainDb is null || maintenance is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await using (var m = new SqlConnection(maintenance))
        {
            var code = await m.ExecuteScalarAsync<string>("SELECT TOP 1 USRCode FROM dbo.USR WHERE USRStatus = 'A'");
            var user = await m.QuerySingleAsync("genisis.User_GetActive", new { code }, commandType: System.Data.CommandType.StoredProcedure);
            Assert.Equal(code, (string)user.USRCode);
        }
        var db = new Genisis.Api.Data.DbConnections(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:MainDb"] = MainDb, ["ConnectionStrings:Maintenance"] = maintenance }).Build());
        var c = new Genisis.Api.Controllers.MembershipController(db);
        object? Value(Microsoft.AspNetCore.Mvc.IActionResult r) => Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(r).Value;
        var found = Assert.IsAssignableFrom<System.Collections.IList>(Value(await c.Search("UBHA0077475", "number")));
        Assert.NotEmpty(found);
        const string member = "UBHA0077475*05";
        foreach (var r in new[] { await c.Get(member), await c.Adjustments(member), await c.MemberHistory(member), await c.Account(member), await c.Notes(member) })
            Assert.NotNull(Value(r));
        var history = Value(await c.MemberHistory(member))!;
        Assert.NotEmpty((System.Collections.IEnumerable)history.GetType().GetProperty("principal")!.GetValue(history)!);
    }
}
