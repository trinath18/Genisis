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
    public static string CombinedPath => Path.Combine(Root, "sql", "genisis-main.sql");

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _deployed;

    public static string BuildCombined()
    {
        var sb = new StringBuilder();
        sb.Append("""
            -- GENERATED FILE: built from sql/main/*.sql. Edit those files, then rebuild this one with
            --     GENISIS_WRITE_SQL=1 dotnet test --filter Combined_main_script_is_up_to_date
            -- How to run: open this file in SSMS, pick the main database in the "Available Databases" box,
            -- check @MaintenanceDb in the first section, then press F5 (Execute).
            -- Safe to run again: it only creates or replaces genisis.* procedures and synonyms; tables and data are not changed.

            """);
        foreach (var file in Directory.GetFiles(Path.Combine(Root, "sql", "main"), "*.sql").Order(StringComparer.Ordinal))
            sb.Append($"\n-- ===== main/{Path.GetFileName(file)} =====\n").Append(Normalize(File.ReadAllText(file)).TrimEnd('\n')).Append('\n');
        sb.Append("\nSET NOEXEC OFF;\nPRINT N'Done.';\nGO\n");
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
            var maintenance = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINTENANCE") is { } m
                ? new SqlConnectionStringBuilder(m).InitialCatalog : "";
            var script = File.ReadAllText(CombinedPath);
            if (maintenance != "")
                script = script.Replace(DefaultMaintenanceDb, $"DECLARE @MaintenanceDb sysname = N'{maintenance.Replace("'", "''")}';");
            await using var conn = new SqlConnection(mainConnectionString);
            await conn.OpenAsync();
            foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                if (!string.IsNullOrWhiteSpace(batch)) await conn.ExecuteAsync(batch);
            _deployed = true;
        }
        finally
        {
            Gate.Release();
        }
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
    public void Combined_main_script_is_up_to_date()
    {
        var expected = SqlScripts.BuildCombined();
        if (Environment.GetEnvironmentVariable("GENISIS_WRITE_SQL") == "1") File.WriteAllText(SqlScripts.CombinedPath, expected);
        Assert.True(File.Exists(SqlScripts.CombinedPath), "sql/genisis-main.sql is missing; rebuild it with GENISIS_WRITE_SQL=1 dotnet test");
        Assert.True(expected == SqlScripts.Normalize(File.ReadAllText(SqlScripts.CombinedPath)),
            "sql/genisis-main.sql is out of date; rebuild it with GENISIS_WRITE_SQL=1 dotnet test");
    }

    [Fact]
    public void Setup_script_keeps_the_maintenance_db_variable_the_tests_replace()
    {
        Assert.Contains(SqlScripts.DefaultMaintenanceDb, File.ReadAllText(SqlScripts.CombinedPath));
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
}
