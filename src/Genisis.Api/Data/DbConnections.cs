using Microsoft.Data.SqlClient;

namespace Genisis.Api.Data;

public class DbConnections(IConfiguration configuration)
{
    public SqlConnection MainDb() => Open("MainDb");
    public SqlConnection Maintenance() => Open("Maintenance");

    /// <summary>Quoted database name of the Maintenance connection, for cross-database joins from MainDb.</summary>
    public string MaintenanceDbName =>
        "[" + new SqlConnectionStringBuilder(ConnectionString("Maintenance")).InitialCatalog.Replace("]", "]]") + "]";

    private SqlConnection Open(string name) => new(ConnectionString(name));

    private string ConnectionString(string name) =>
        configuration.GetConnectionString(name)
            ?? throw new InvalidOperationException($"Connection string '{name}' is not configured.");
}
