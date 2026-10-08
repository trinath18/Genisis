using Dapper;
using Genisis.Api.Data;
using Genisis.Api.Registration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Genisis.Api.Tests;

/// <summary>
/// Runs against a development copy of the databases when GENISIS_TEST_MAINDB and GENISIS_TEST_MAINTENANCE are set
/// (otherwise the tests return immediately). Nothing is left behind: writes are rolled back.
/// Fixture: payor WG, plan AL02, health code N, insured code H (present in the development backups).
/// Restored backups cannot open the production database master key, so the write test disables the four
/// encryption triggers inside its own transaction; the rollback re-enables them.
/// </summary>
public class RegistrationDatabaseTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");
    private static readonly string? Maintenance = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINTENANCE");

    private static DbConnections Db() => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:MainDb"] = MainDb,
        ["ConnectionStrings:Maintenance"] = Maintenance,
    }).Build());

    private static RegistrationRequest Fixture()
    {
        var r = RegistrationValidatorTests.Valid();
        r.PayorCode = "WG";
        r.PlanCode = "AL02";
        r.InsuredType = "H";
        r.Name = "GENISIS TEST " + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        r.Email = "test@example.com";
        r.CoveredPersons.Add(new CoveredPersonRequest { Relationship = "SP", Name = "TEST SPOUSE", DateOfBirth = new DateTime(1982, 5, 5), Sex = "F" });
        r.CoveredPersons.Add(new CoveredPersonRequest { Relationship = "C", Name = "TEST CHILD", DateOfBirth = new DateTime(2015, 3, 3), Sex = "M" });
        return r;
    }

    private sealed record Counters(string Check, string Security, string? Payor);

    private static async Task<Counters> ReadCounters(SqlConnection conn, string maintenanceDb, SqlTransaction? tx = null) => new(
        await conn.ExecuteScalarAsync<string>("SELECT MBMChkDigits FROM dbo.MBMCheckDigits", transaction: tx) ?? "",
        await conn.ExecuteScalarAsync<string>("SELECT MBMSecurityCode FROM dbo.MBMSecurityDigits", transaction: tx) ?? "",
        await conn.ExecuteScalarAsync<string?>($"SELECT RTRIM(SEQNumber) FROM {maintenanceDb}.dbo.MBMSEQ05 WHERE PAYCode = 'WG'", transaction: tx));

    [Fact]
    public async Task Save_writes_every_table_with_legacy_numbers()
    {
        if (MainDb is null || Maintenance is null) return;
        var db = Db();
        var request = Fixture();
        Assert.Null(RegistrationValidator.Validate(request));
        RegistrationService.Normalize(request);
        var maintenanceDb = await db.MaintenanceDbNameAsync();
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        var before = await ReadCounters(conn, maintenanceDb);

        await using (var tx = conn.BeginTransaction())
        {
            if (!await MasterKeyOpensAsync(conn, tx))
                await conn.ExecuteAsync(
                    """
                    DISABLE TRIGGER trgICEncrypt ON dbo.MBM; DISABLE TRIGGER trgMbmContactEncrypt ON dbo.MBMTwo;
                    DISABLE TRIGGER trgBankAcEncrypt ON dbo.MBMOthersTwo; DISABLE TRIGGER trgCICEncrypt ON dbo.MBMCoveredPersons;
                    """, transaction: tx);
            var result = await RegistrationService.SaveAsync(conn, tx, maintenanceDb, request, "TEST");
            var n = result.MembershipNo;
            var expectedSeq = LegacyRules.NextPayorSequence(before.Payor);
            var expectedCheck = LegacyRules.NextCheckDigits(before.Check);
            Assert.Equal($"WGH{expectedSeq}{expectedCheck}*01", n);
            Assert.Equal(new Counters(expectedCheck, LegacyRules.NextSecurityCode(before.Security), expectedSeq),
                await ReadCounters(conn, maintenanceDb, tx));

            async Task<int> Count(string sql) => await conn.ExecuteScalarAsync<int>(sql, new { n, b = n.Split('*')[0] }, tx);
            Assert.Equal(1, await Count("SELECT COUNT(*) FROM dbo.MBM WHERE MBMNumber = @n AND MBMStatus = 'A' AND PLNCode = 'AL02'"));
            Assert.Equal(1, await Count("SELECT COUNT(*) FROM dbo.MBMTwo WHERE MBMNumber = @n"));
            Assert.Equal(1, await Count("SELECT COUNT(*) FROM dbo.MBMOthers WHERE MBMNumber = @n"));
            Assert.Equal(1, await Count("SELECT COUNT(*) FROM dbo.MBMOthersTwo WHERE MBMNumber = @n"));
            Assert.Equal(1, await Count($"SELECT COUNT(*) FROM {maintenanceDb}.dbo.MBMCrossReference WHERE MBMNumber = @n"));
            Assert.Equal(2, await Count("SELECT COUNT(*) FROM dbo.MBMCoveredPersons WHERE MBMCNumber = @n"));
            Assert.Equal(2, await Count("SELECT COUNT(*) FROM dbo.MBMCoveredPersonsTwo WHERE MBMCNumber = @n"));
            Assert.Equal(1, await Count("SELECT COUNT(*) FROM dbo.MBMCoveredPersons WHERE MBMCNumber = @n AND MBMCCoverID = '01' AND MBMCRELCode = 'SP'"));
            Assert.Equal(1, await Count("SELECT COUNT(*) FROM dbo.MBMSCSecurity WHERE MBMNumber = @b"));
            Assert.Equal(1, await Count("SELECT COUNT(*) FROM dbo.BAT WHERE PAYCode = 'WG' AND BATBordxDate = '2021-08-06' AND BordxType = 'N'"));
            Assert.Equal(2, result.Quote.CoveredPersons.Count);
            Assert.True(result.Quote.Premium > 0, "Expected a premium for the fixture plan");
            tx.Rollback();
        }

        Assert.Equal(before, await ReadCounters(conn, maintenanceDb));
    }

    private static async Task<bool> MasterKeyOpensAsync(SqlConnection conn, SqlTransaction tx)
    {
        try
        {
            await conn.ExecuteAsync("OPEN SYMMETRIC KEY SQLSymmetricKey_256 DECRYPTION BY CERTIFICATE SelfSignedCertificate; CLOSE SYMMETRIC KEY SQLSymmetricKey_256;", transaction: tx);
            return true;
        }
        catch (SqlException)
        {
            return false;
        }
    }

    [Fact]
    public async Task Failed_save_rolls_back_every_write()
    {
        if (MainDb is null || Maintenance is null) return;
        var db = Db();
        var request = Fixture();
        request.Email = new string('x', 90) + "@example.com";
        var maintenanceDb = await db.MaintenanceDbNameAsync();
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        var before = await ReadCounters(conn, maintenanceDb);

        await Assert.ThrowsAsync<SqlException>(() => new RegistrationService(db).RegisterAsync(request, "TEST"));

        Assert.Equal(before, await ReadCounters(conn, maintenanceDb));
        Assert.Equal(0, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.MBM WHERE MBMName = @name", new { name = request.Name }));
    }

    [Fact]
    public async Task Quote_prices_the_fixture_plan()
    {
        if (MainDb is null || Maintenance is null) return;
        var quote = await new RegistrationService(Db()).QuoteAsync(Fixture());
        Assert.Equal("A", quote.AdultChild);
        Assert.Equal(["A", "C"], quote.CoveredPersons.Select(c => c.AdultChild));
    }
}
