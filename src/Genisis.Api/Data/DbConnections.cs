using Microsoft.Data.SqlClient;

namespace Genisis.Api.Data;

/// <summary>Creates connections to the three databases from the <c>ConnectionStrings</c> section (all on one server, Windows authentication).</summary>
/// <param name="configuration">Application configuration.</param>
public class DbConnections(IConfiguration configuration)
{
    private string? _maintenanceDbName;

    /// <summary>A new, unopened connection to the main database (HISDB).</summary>
    public SqlConnection MainDb() => Open("MainDb");
    /// <summary>A new, unopened connection to the maintenance database (HISMaintenance).</summary>
    public SqlConnection Maintenance() => Open("Maintenance");
    /// <summary>A new, unopened connection to the worksheet database (HISWorkSheet).</summary>
    public SqlConnection WorkSheet() => Open("WorkSheet");

    /// <summary>
    /// Quoted database name of the Maintenance connection, for cross-database joins from MainDb.
    /// Uses the connection string's catalog, or the login's default database when the catalog is omitted.
    /// </summary>
    public async Task<string> MaintenanceDbNameAsync()
    {
        if (_maintenanceDbName is not null) return _maintenanceDbName;

        var name = new SqlConnectionStringBuilder(ConnectionString("Maintenance")).InitialCatalog;
        if (string.IsNullOrWhiteSpace(name))
        {
            await using var conn = Maintenance();
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("SELECT DB_NAME()", conn);
            name = (string?)await cmd.ExecuteScalarAsync();
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Could not resolve the Maintenance database name.");
        }

        return _maintenanceDbName = QuoteName(name);
    }

    /// <summary>Quotes a database or object name for use in SQL text (<c>QUOTENAME</c>).</summary>
    public static string QuoteName(string name) => "[" + name.Replace("]", "]]") + "]";

    private SqlConnection Open(string name) => new(ConnectionString(name));

    private string ConnectionString(string name) =>
        configuration.GetConnectionString(name)
            ?? throw new InvalidOperationException($"Connection string '{name}' is not configured.");
}
