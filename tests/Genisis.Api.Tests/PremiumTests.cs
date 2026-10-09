using Dapper;
using Genisis.Api.Data;
using Genisis.Api.Plans;
using Microsoft.Extensions.Configuration;

namespace Genisis.Api.Tests;

public class PremiumTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");

    private static PremiumCreateRequest Valid() => new()
    {
        InsuredCode = "i", HealthCode = "n", PayorCode = "xd", PlanCode = "zz9x", SuppStatus = "C", EffectiveDate = new DateTime(2026, 1, 1), Version = 1,
        Bands =
        [
            new() { AgeCode = "01", Amount = 100, SuppAmount = 40 },
            new() { AgeCode = "02", Amount = 200 },
            new() { AgeCode = "03", SuppAmount = 30 },
        ],
    };

    [Fact]
    public void Messages_follow_desktop_order()
    {
        Assert.Null(PremiumValidator.Validate(Valid()));
        Assert.Equal("Please Enter Insured Code", PremiumValidator.Validate(new PremiumCreateRequest()));
        var r = Valid(); r.PlanCode = " "; r.Version = null;
        Assert.Equal("Please Enter Plan Code", PremiumValidator.Validate(r));
        r = Valid(); r.EffectiveDate = null; r.Version = null;
        Assert.Equal("Please validate your Premium Effective date", PremiumValidator.Validate(r));
        r = Valid(); r.Version = null;
        Assert.Equal("Please Enter Premium Version", PremiumValidator.Validate(r));
        r = Valid(); r.Bands = [new() { AgeCode = "01", SuppAmount = 5 }];
        Assert.Equal("Please enter the premium for at least one age band.", PremiumValidator.Validate(r));
        r = Valid(); r.Bands.Add(new() { AgeCode = "08", Amount = 1 });
        Assert.Equal("Age band must be 01 to 07.", PremiumValidator.Validate(r));
        r = Valid(); r.Bands.Add(new() { AgeCode = "01", Amount = 1 });
        Assert.Equal("Each age band can only be entered once.", PremiumValidator.Validate(r));
        Assert.Equal("Please enter the premium amount.", PremiumValidator.Validate(new PremiumUpdateRequest { EffectiveDate = DateTime.Today, Version = 1 }));
    }

    [Fact]
    public async Task Save_edit_delete_round_trip_is_rolled_back()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var db = new DbConnections(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MainDb"] = MainDb, ["ConnectionStrings:Maintenance"] = MainDb,
        }).Build());
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        var count = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.PRM");

        await using (var tx = conn.BeginTransaction())
        {
            var codes = await PremiumService.CreateAsync(conn, tx, Valid(), "TEST");
            Assert.Equal(2, codes.Count);
            var rows = (await conn.QueryAsync(
                "SELECT AGECode, PRMAmount, SuppPrmAmt, SuppPrmStatus, PAYCode, PRMLastUpdateUser FROM dbo.PRM WHERE PLNCode = 'ZZ9X' ORDER BY AGECode", transaction: tx)).ToList();
            Assert.Equal(2, rows.Count);
            Assert.Equal("01", (string)rows[0].AGECode);
            Assert.Equal(100m, (decimal)rows[0].PRMAmount);
            Assert.Equal(40m, (decimal)rows[0].SuppPrmAmt);
            Assert.Equal("C", (string)rows[0].SuppPrmStatus);
            Assert.Equal("XD", (string)rows[0].PAYCode);
            Assert.Equal("TEST", (string)rows[0].PRMLastUpdateUser);
            Assert.Null((decimal?)rows[1].SuppPrmAmt);

            var dup = Valid(); dup.Bands = [new() { AgeCode = "02", Amount = 1 }];
            var ex = await Assert.ThrowsAsync<PlanException>(() => PremiumService.CreateAsync(conn, tx, dup, "TEST"));
            Assert.StartsWith("Record Duplicated!", ex.Message);
            dup.EffectiveDate = new DateTime(2027, 1, 1);
            var later = Assert.Single(await PremiumService.CreateAsync(conn, tx, dup, "TEST"));

            var edit = new PremiumUpdateRequest { Amount = 5, EffectiveDate = new DateTime(2026, 1, 1), Version = 1 };
            ex = await Assert.ThrowsAsync<PlanException>(() => PremiumService.UpdateAsync(conn, tx, later, edit, "TEST"));
            Assert.StartsWith("Record Duplicated!", ex.Message);
            ex = await Assert.ThrowsAsync<PlanException>(() => PremiumService.UpdateAsync(conn, tx, int.MaxValue, edit, "TEST"));
            Assert.Equal("Record not found", ex.Message);
            await PremiumService.UpdateAsync(conn, tx, codes[0], new() { Amount = 150, SuppAmount = 45, EffectiveDate = new DateTime(2026, 1, 1), Version = 2 }, "TEST");
            var row = await conn.QuerySingleAsync("SELECT PRMAmount, SuppPrmAmt, PRMVersion FROM dbo.PRM WHERE PRMCode = @code", new { code = codes[0] }, tx);
            Assert.Equal(150m, (decimal)row.PRMAmount);
            Assert.Equal(45m, (decimal)row.SuppPrmAmt);
            Assert.Equal(2, (int)row.PRMVersion);

            var a = Valid(); a.PlanCode = "ZZ8X"; a.SuppStatus = "A";
            var aCodes = await PremiumService.CreateAsync(conn, tx, a, "TEST");
            Assert.Equal(0, await conn.ExecuteScalarAsync<int>("SELECT COUNT(SuppPrmAmt) FROM dbo.PRM WHERE PLNCode = 'ZZ8X'", transaction: tx));

            await PremiumService.DeleteAsync(conn, tx, codes[1]);
            Assert.Equal(2, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.PRM WHERE PLNCode = 'ZZ9X'", transaction: tx));
            var used = await conn.ExecuteScalarAsync<int?>(
                """
                SELECT TOP 1 p.PRMCode FROM dbo.PRM p WHERE EXISTS (SELECT 1 FROM dbo.MBM m WHERE m.INSCode = p.INSCode AND m.HLTCode = p.HLTCode
                    AND m.PLNCode = p.PLNCode AND m.AGECode = p.AGECode AND (p.HLTCode = 'S' OR m.PAYCode = p.PAYCode))
                """, transaction: tx);
            Assert.NotNull(used);
            ex = await Assert.ThrowsAsync<PlanException>(() => PremiumService.DeleteAsync(conn, tx, used.Value));
            Assert.StartsWith("This record cannot be deleted", ex.Message);
            await tx.RollbackAsync();
        }
        Assert.Equal(count, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.PRM"));
    }
}
