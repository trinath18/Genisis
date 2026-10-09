using Dapper;
using Genisis.Api.Data;
using Genisis.Api.Plans;
using Microsoft.Extensions.Configuration;

namespace Genisis.Api.Tests;

public class AnnualLimitTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");

    internal static AnnualLimitRequest Valid() => new()
    {
        PlanCode = "zz9x", HealthCode = "n", InsuredCode = "i", AnnualLimit = 50000, SuppLimitStatus = "B", SuppLimit = 20000,
        SuppLifetimeLimit = 100000, EffectiveDate = new DateTime(2026, 1, 1), Version = 1,
    };

    [Fact]
    public void Valid_request_passes() => Assert.Null(AnnualLimitValidator.Validate(Valid(), lifetimePlan: false));

    [Fact]
    public void Messages_follow_desktop_order()
    {
        Assert.Equal("Please Enter Plan Code", AnnualLimitValidator.Validate(new AnnualLimitRequest(), false));
        var r = Valid(); r.AnnualLimit = null; r.EffectiveDate = null;
        Assert.Equal("Please Enter Annual Limit", AnnualLimitValidator.Validate(r, false));
        r = Valid(); r.EffectiveDate = null;
        Assert.Equal("Please Enter Life Time Limit", AnnualLimitValidator.Validate(r, true));
        Assert.Equal("Please Enter Annual Limit Effective Date", AnnualLimitValidator.Validate(r, false));
        r = Valid(); r.Version = null;
        Assert.Equal("Please Enter Annual Limit Version", AnnualLimitValidator.Validate(r, false));
        r = Valid(); r.SuppLimitStatus = "C"; r.SuppLimit = null;
        Assert.Equal("Please Enter Supp Premium Limit", AnnualLimitValidator.Validate(r, false));
        r = Valid(); r.SuppLifetimeLimit = null;
        Assert.Equal("Please Enter Supp Life Time Limit", AnnualLimitValidator.Validate(r, false));
        r = Valid(); r.SuppLimitStatus = "A"; r.SuppLimit = null; r.SuppLifetimeLimit = null;
        Assert.Null(AnnualLimitValidator.Validate(r, false));
    }

    [Fact]
    public async Task Save_duplicate_update_round_trip_is_rolled_back()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var db = new DbConnections(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MainDb"] = MainDb, ["ConnectionStrings:Maintenance"] = MainDb,
        }).Build());
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        var lifetimePlan = await conn.ExecuteScalarAsync<string>("SELECT TOP 1 RTRIM(PLNCode) FROM dbo.PLN WHERE PLNLifeTimeStatus = 'Y'");
        var count = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AnnualLimit");

        await using (var tx = conn.BeginTransaction())
        {
            var lt = Valid(); lt.PlanCode = lifetimePlan;
            var ex = await Assert.ThrowsAsync<PlanException>(() => AnnualLimitService.SaveAsync(conn, tx, lt, null, "TEST"));
            Assert.Equal("Please Enter Life Time Limit", ex.Message);

            var index = await AnnualLimitService.SaveAsync(conn, tx, Valid(), null, "TEST");
            ex = await Assert.ThrowsAsync<PlanException>(() => AnnualLimitService.SaveAsync(conn, tx, Valid(), null, "TEST"));
            Assert.StartsWith("Record Duplicated!", ex.Message);

            var a = Valid(); a.SuppLimitStatus = "A"; a.SuppLimit = null; a.SuppLifetimeLimit = null; a.AnnualLimit = 60000; a.PayorCode = "et";
            await AnnualLimitService.SaveAsync(conn, tx, a, index, "TEST");
            var row = await conn.QuerySingleAsync(
                "SELECT PLNCode, HLTCode, PAYCode, AnnLimit, SuppLimitStatus, SuppLimit, SuppLifeTimeLimit, DisabilityLmt, AnnualLastUpdatedUser FROM dbo.AnnualLimit WHERE AnnualIndex = @index",
                new { index }, tx);
            Assert.Equal("ZZ9X", (string)row.PLNCode);
            Assert.Equal("ET", (string)row.PAYCode);
            Assert.Equal(60000m, (decimal)row.AnnLimit);
            Assert.Equal("A", (string)row.SuppLimitStatus);
            Assert.Equal(20000m, (decimal)row.SuppLimit);
            Assert.Equal(0m, (decimal)row.DisabilityLmt);
            Assert.Equal("TEST", (string)row.AnnualLastUpdatedUser);

            var other = Valid(); other.PlanCode = "ZZ8X";
            var otherIndex = await AnnualLimitService.SaveAsync(conn, tx, other, null, "TEST");
            ex = await Assert.ThrowsAsync<PlanException>(() => AnnualLimitService.SaveAsync(conn, tx, Valid(), otherIndex, "TEST"));
            Assert.StartsWith("Record Duplicated!", ex.Message);

            ex = await Assert.ThrowsAsync<PlanException>(() => AnnualLimitService.SaveAsync(conn, tx, Valid(), int.MaxValue, "TEST"));
            Assert.Equal("Record not found", ex.Message);

            await conn.ExecuteAsync("UPDATE dbo.AnnualLimit SET PLNCode = 'ZZ9X' WHERE AnnualIndex = @otherIndex", new { otherIndex }, tx);
            var keep = Valid(); keep.AnnualLimit = 70000;
            Assert.Equal(otherIndex, await AnnualLimitService.SaveAsync(conn, tx, keep, otherIndex, "TEST"));
            await tx.RollbackAsync();
        }
        Assert.Equal(count, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AnnualLimit"));
    }
}
