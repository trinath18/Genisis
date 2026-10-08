using Microsoft.Data.SqlClient;

namespace Genisis.Api.Data;

public class DbConnections(IConfiguration configuration)
{
    private string? _maintenanceDbName;

    public SqlConnection MainDb() => Open("MainDb");
    public SqlConnection Maintenance() => Open("Maintenance");

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

    public static string QuoteName(string name) => "[" + name.Replace("]", "]]") + "]";

    private SqlConnection Open(string name) => new(ConnectionString(name));

    private string ConnectionString(string name) =>
        configuration.GetConnectionString(name)
            ?? throw new InvalidOperationException($"Connection string '{name}' is not configured.");
}
