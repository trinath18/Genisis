using Dapper;
using Genisis.Api.Data;
using Genisis.Api.Plans;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Tests;

public class BenefitLimitTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");

    [Fact]
    public void Messages_follow_desktop()
    {
        Assert.Equal("Please Enter Plan Code", BenefitLimitService.Validate(new BenefitLimitRequest()));
        Assert.Equal("Please Enter Health Code", BenefitLimitService.Validate(new BenefitLimitRequest { PlanCode = "M225" }));
        Assert.Equal("Please Enter Insured Code", BenefitLimitService.Validate(new BenefitLimitRequest { PlanCode = "M225", HealthCode = "N" }));
        var ok = new BenefitLimitRequest { PlanCode = "M225", HealthCode = "N", InsuredCode = "I" };
        Assert.Equal("Please Enter Annual Limit Effective Date", BenefitLimitService.Validate(ok));
        ok.EffectiveDate = new DateTime(2026, 1, 1);
        Assert.Null(BenefitLimitService.Validate(ok));
        ok.SuppStatus = "x";
        Assert.Equal("Supp. Status must be A, B or C.", BenefitLimitService.Validate(ok));

        Assert.Equal("Please Enter Mgmt. Fee", MgmtFeeService.Validate(new MgmtFeeRequest { PlanCode = "M225", HealthCode = "N", InsuredCode = "I" }));
        Assert.Equal("Mgmt. Fee cannot be negative.",
            MgmtFeeService.Validate(new MgmtFeeRequest { PlanCode = "M225", HealthCode = "N", InsuredCode = "I", Fee = -1 }));
    }

    [Fact]
    public async Task Benefit_limit_round_trip_is_rolled_back()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await using var conn = new SqlConnection(MainDb);
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        async Task<List<BenefitLimitRow>> Find(string plan) =>
            await conn.ProcQueryAsync<BenefitLimitRow>("genisis.BenefitLimit_Search", new { plan }, tx);

        var r = new BenefitLimitRequest
        {
            PlanCode = "zz98", HealthCode = "n", InsuredCode = "i", SuppStatus = "b", EffectiveDate = new DateTime(2026, 1, 1, 13, 0, 0),
            KidneyDialysisLimit = 1000, CancerLimit = 2000, HomeNursingLimit = 0,
        };
        var index = await BenefitLimitService.SaveAsync(conn, tx, null, r, "TEST");
        var row = Assert.Single(await Find("ZZ98"));
        Assert.Equal(new BenefitLimitRow(index, "ZZ98", "N", "I", new DateTime(2026, 1, 1), "B", 1000, 2000, 0), row);

        Assert.Equal(PlanKey.Duplicated, (await Assert.ThrowsAsync<PlanException>(() =>
            BenefitLimitService.SaveAsync(conn, tx, null, r, "TEST"))).Message);

        r.CancerLimit = 2500;
        await BenefitLimitService.SaveAsync(conn, tx, index, r, "TEST");
        Assert.Equal(2500, Assert.Single(await Find("ZZ98")).CancerLimit);

        r.InsuredCode = "F";
        var other = await BenefitLimitService.SaveAsync(conn, tx, null, r, "TEST");
        r.InsuredCode = "I";
        Assert.Equal(PlanKey.Duplicated, (await Assert.ThrowsAsync<PlanException>(() =>
            BenefitLimitService.SaveAsync(conn, tx, other, r, "TEST"))).Message);

        await BenefitLimitService.DeleteAsync(conn, tx, index);
        await BenefitLimitService.DeleteAsync(conn, tx, other);
        Assert.Empty(await Find("ZZ98"));
        Assert.Equal("Record not found", (await Assert.ThrowsAsync<PlanException>(() => BenefitLimitService.DeleteAsync(conn, tx, index))).Message);
        Assert.Equal("Record not found", (await Assert.ThrowsAsync<PlanException>(() =>
            BenefitLimitService.SaveAsync(conn, tx, index, r, "TEST"))).Message);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Mgmt_fee_round_trip_is_rolled_back()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await using var conn = new SqlConnection(MainDb);
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        async Task<List<MgmtFeeRow>> Find() => await conn.ProcQueryAsync<MgmtFeeRow>("genisis.MgmtFee_Search", new { plan = "ZZ98" }, tx);

        var r = new MgmtFeeRequest { PlanCode = "zz98", HealthCode = "n", InsuredCode = "g", Fee = 75 };
        await MgmtFeeService.SaveAsync(conn, tx, r, false, "TEST");
        Assert.Equal(new MgmtFeeRow("ZZ98", "N", "G", 75), Assert.Single(await Find()));
        Assert.Equal(PlanKey.Duplicated, (await Assert.ThrowsAsync<PlanException>(() => MgmtFeeService.SaveAsync(conn, tx, r, false, "TEST"))).Message);

        r.Fee = 80;
        await MgmtFeeService.SaveAsync(conn, tx, r, true, "TEST");
        Assert.Equal(80, Assert.Single(await Find()).Fee);

        await MgmtFeeService.DeleteAsync(conn, tx, "zz98", "n", "g");
        Assert.Empty(await Find());
        Assert.Equal("Record not found", (await Assert.ThrowsAsync<PlanException>(() => MgmtFeeService.DeleteAsync(conn, tx, "ZZ98", "N", "G"))).Message);
        Assert.Equal("Record not found", (await Assert.ThrowsAsync<PlanException>(() => MgmtFeeService.SaveAsync(conn, tx, r, true, "TEST"))).Message);
        Assert.Equal(19, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.Maint_CaseMgmtFee", transaction: tx));
        await tx.RollbackAsync();
    }
}
