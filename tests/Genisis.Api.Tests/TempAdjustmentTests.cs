using Dapper;
using Genisis.Api.Adjustment;
using Genisis.Api.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Genisis.Api.Tests;

public class TempAdjustmentTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");

    [Fact]
    public async Task Only_memberships_of_temporary_payors_are_temporary()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        string? temporary, regular;
        await using (var conn = new SqlConnection(MainDb))
        {
            temporary = await conn.ExecuteScalarAsync<string?>(
                "SELECT TOP (1) RTRIM(m.MBMNumber) FROM dbo.MBM m JOIN dbo.PAY p ON p.PAYCode = m.PAYCode WHERE p.tempPayIND = 'Y'");
            regular = await conn.ExecuteScalarAsync<string?>(
                "SELECT TOP (1) RTRIM(m.MBMNumber) FROM dbo.MBM m JOIN dbo.PAY p ON p.PAYCode = m.PAYCode WHERE ISNULL(p.tempPayIND, '') <> 'Y'");
        }
        var service = new TempAdjustmentService(new DbConnections(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:MainDb"] = MainDb }).Build()));

        if (temporary is not null) Assert.True((await service.CheckAsync(temporary, CancellationToken.None))!.Temporary);
        if (regular is not null) Assert.False((await service.CheckAsync(regular, CancellationToken.None))!.Temporary);
        Assert.Null(await service.CheckAsync("NO-SUCH-MEMBER", CancellationToken.None));
    }
}
