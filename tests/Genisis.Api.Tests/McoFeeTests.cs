using Dapper;
using Genisis.Api.Data;
using Genisis.Api.Plans;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Tests;

public class McoFeeTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");

    private static McoFeeCreateRequest Request(string plan) => new()
    {
        PlanCode = plan.ToLowerInvariant(), HealthCode = "n", SuppStatus = "b", EffectiveDate = new DateTime(2099, 1, 1, 9, 0, 0),
        Rows =
        [
            new() { InsuredCode = "i", Adult = 10, Child = 5, SuppAdult = 99, SuppChild = 99 },
            new() { InsuredCode = "F", Adult = 20, Child = 8, SuppAdult = 3, SuppChild = 2 },
            new() { InsuredCode = "G", Child = 1 },
        ],
    };

    [Fact]
    public void Messages_follow_desktop()
    {
        Assert.Equal("Please Enter Health Code", McoFeeService.Validate(new McoFeeCreateRequest()));
        Assert.Equal("Please Enter Plan Code", McoFeeService.Validate(new McoFeeCreateRequest { HealthCode = "N" }));
        Assert.Equal("Please Enter Covered ID Charge", McoFeeService.Validate(new McoFeeCreateRequest { HealthCode = "N", PlanCode = "M225", SuppStatus = "D" }));
        Assert.Equal("Please validate your MCO Effective date", McoFeeService.Validate(new McoFeeCreateRequest { HealthCode = "N", PlanCode = "M225" }));
        var r = Request("M225");
        Assert.Null(McoFeeService.Validate(r));
        r.Rows.ForEach(x => x.Adult = null);
        Assert.Equal("Please Enter MCO Fees ", McoFeeService.Validate(r));
        r.Rows[0].InsuredCode = "X";
        Assert.Equal("Insured type must be I, F, G or H.", McoFeeService.Validate(r));
    }

    [Fact]
    public async Task Add_duplicate_edit_delete_round_trip_is_rolled_back()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await using var conn = new SqlConnection(MainDb);
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        var free = await conn.ExecuteScalarAsync<string>(
            "SELECT TOP 1 RTRIM(p.PLNCode) FROM dbo.PLN p WHERE p.HLTCode = 'N' AND NOT EXISTS (SELECT 1 FROM dbo.MBM m WHERE m.PLNCode = p.PLNCode)",
            transaction: tx);
        var used = await conn.ExecuteScalarAsync<string>(
            "SELECT TOP 1 RTRIM(m.PLNCode) FROM dbo.MBM m JOIN dbo.PLN p ON p.PLNCode = m.PLNCode AND p.HLTCode = 'N' WHERE m.HLTCode = 'N' AND m.INSCode = 'I'",
            transaction: tx);
        async Task<List<McoFeeRow>> Find(string plan) =>
            await conn.ProcQueryAsync<McoFeeRow>("genisis.McoFee_Search", new { top = 50, plan, effective = new DateTime(2099, 1, 1) }, tx);

        var codes = await McoFeeService.CreateAsync(conn, tx, Request(free!), "TEST");
        Assert.Equal(2, codes.Count);
        var rows = (await Find(free!)).OrderBy(x => x.Index).ToList();
        Assert.Equal(new McoFeeRow(codes[0], free, "N", "I", "B", 10, 5, null, null, new DateTime(2099, 1, 1), null), rows[0]);
        Assert.Equal(new McoFeeRow(codes[1], free, "N", "F", "B", 20, 8, 3, 2, new DateTime(2099, 1, 1), null), rows[1]);

        Assert.StartsWith(PlanKey.Duplicated, (await Assert.ThrowsAsync<PlanException>(() =>
            McoFeeService.CreateAsync(conn, tx, Request(free!), "TEST"))).Message);
        Assert.Equal("Please validate your Plan Code", (await Assert.ThrowsAsync<PlanException>(() =>
            McoFeeService.CreateAsync(conn, tx, Request("ZZZZ"), "TEST"))).Message);

        var edit = new McoFeeUpdateRequest { EffectiveDate = new DateTime(2099, 3, 1), Adult = 25, Child = 9, SuppAdult = 4, SuppChild = 1 };
        await McoFeeService.UpdateAsync(conn, tx, codes[1], edit, "TEST");
        Assert.Equal(25, await conn.ExecuteScalarAsync<decimal>("SELECT MCOAmountAdult FROM dbo.MCO WHERE MCOCode = @c", new { c = codes[1] }, tx));
        var again = Request(free!);
        again.EffectiveDate = new DateTime(2099, 2, 1);
        var newer = await McoFeeService.CreateAsync(conn, tx, again, "TEST");
        Assert.Equal(PlanKey.Duplicated, (await Assert.ThrowsAsync<PlanException>(() =>
            McoFeeService.UpdateAsync(conn, tx, newer[0], new McoFeeUpdateRequest { EffectiveDate = new DateTime(2099, 1, 1), Adult = 1 }, "TEST"))).Message);

        await McoFeeService.DeleteAsync(conn, tx, codes[0]);
        Assert.Equal("Record not found", (await Assert.ThrowsAsync<PlanException>(() => McoFeeService.DeleteAsync(conn, tx, codes[0]))).Message);
        Assert.Equal("Record not found", (await Assert.ThrowsAsync<PlanException>(() =>
            McoFeeService.UpdateAsync(conn, tx, codes[0], edit, "TEST"))).Message);

        var inUse = await McoFeeService.CreateAsync(conn, tx, Request(used!), "TEST");
        Assert.Equal("This record cannot be deleted because it is used in Member Table!",
            (await Assert.ThrowsAsync<PlanException>(() => McoFeeService.DeleteAsync(conn, tx, inUse[0]))).Message);
        await tx.RollbackAsync();
    }
}
