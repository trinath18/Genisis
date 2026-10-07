using Microsoft.Data.SqlClient;

namespace Genisis.Api.Data;

public class DbConnections(IConfiguration configuration)
{
    public SqlConnection Hisdb() => Open("HISDB");
    public SqlConnection HisMaintenance() => Open("HISMaintenance");

    private SqlConnection Open(string name) =>
        new(configuration.GetConnectionString(name)
            ?? throw new InvalidOperationException($"Connection string '{name}' is not configured."));
}
