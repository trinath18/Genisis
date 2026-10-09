using Dapper;
using Genisis.Api.Data;
using Genisis.Api.Plans;
using Microsoft.Extensions.Configuration;

namespace Genisis.Api.Tests;

public class PlanValidatorTests
{
    internal static PlanCreateRequest Valid() => new()
    {
        HealthCode = "N", PayorCode = "WG", NumberOfPlans = 2, GroupCompany = "GENISIS TEST CO",
        Plans = [new() { Description = "TEST PLAN ONE" }, new() { Description = "TEST PLAN TWO" }],
        AnnualLimitInd = "Y", LifetimeStatus = "N", PremiumInd = "Y", McoInd = "Y", TopUpStatus = "N", SpecialGracePeriod = "N",
        CoPayment = "N", EffectiveDate = new DateTime(2026, 1, 1), ProductCategory = "NS", Meal = "N", Nursing = "N", Tax = "N",
        Mri = "N", ManagementFee = "N", DisIndicator = "N", StartAge = 0, EndAge = 65, PolicyWording = "eTIQA GROUP",
    };

    [Fact]
    public void Valid_request_passes() => Assert.Null(PlanValidator.Validate(Valid()));

    [Theory]
    [InlineData("health", "Please Enter Health Type")]
    [InlineData("payor", "Please Enter Payor Code")]
    [InlineData("count", "Please Enter No of Plan Code")]
    [InlineData("group", "Please Enter Group Company")]
    [InlineData("grace", "Please Enter Special GPeriod Days")]
    [InlineData("effective", "Please Enter Effective Date")]
    [InlineData("sCode", "Please Enter Plan Code ")]
    [InlineData("axClient", "Please Select Client Plan")]
    [InlineData("tooMany", "No of Plan Code must be 1 to 6.")]
    [InlineData("badClient", "Client Plan is not valid for this payor.")]
    public void Desktop_messages(string change, string expected)
    {
        var r = Valid();
        switch (change)
        {
            case "health": r.HealthCode = ""; break;
            case "payor": r.PayorCode = " "; break;
            case "count": r.NumberOfPlans = null; break;
            case "group": r.GroupCompany = null; break;
            case "grace": r.SpecialGracePeriod = "Y"; break;
            case "effective": r.EffectiveDate = null; break;
            case "sCode": r.HealthCode = "S"; r.Plans = [new() { Description = "X" }]; break;
            case "axClient": r.PayorCode = "AX"; break;
            case "tooMany": r.NumberOfPlans = 7; break;
            case "badClient": r.ClientPlan = "PLAN1"; break;
        }
        Assert.Equal(expected, PlanValidator.Validate(r));
    }

    [Fact]
    public void Like_wildcards_are_literal() => Assert.Equal("A[_]0[%]1[[]", SqlText.EscapeLike("A_0%1["));

    [Fact]
    public void Client_plans_follow_payor()
    {
        Assert.Equal(12, PlanValidator.ClientPlans("AX").Count);
        Assert.Equal(18, PlanValidator.ClientPlans("et").Count);
        Assert.Empty(PlanValidator.ClientPlans("WG"));
    }
}

/// <summary>Rolled-back save against the development databases (skipped unless GENISIS_TEST_MAINDB / _MAINTENANCE are set).</summary>
public class PlanDatabaseTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");
    private static readonly string? Maintenance = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINTENANCE");

    [Fact]
    public async Task Create_uses_plan_sequence_and_payor_defaults()
    {
        if (MainDb is null || Maintenance is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var db = new DbConnections(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MainDb"] = MainDb, ["ConnectionStrings:Maintenance"] = Maintenance,
        }).Build());
        var maintenanceDb = await db.MaintenanceDbNameAsync();
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        var before = await conn.ExecuteScalarAsync<string>("SELECT PLNNoSeq FROM dbo.PLNSeq WHERE PLNSeqCode = 'G'");

        await using (var tx = conn.BeginTransaction())
        {
            var r = PlanValidatorTests.Valid();
            r.PayorCode = "ET";
            r.ClientPlan = "PLAN2";
            r.GroupCompany = "  genisis test co";
            var created = await PlanService.SaveAsync(conn, tx, r, "TEST");
            var n = int.Parse(before!);
            Assert.Equal([$"G{n + 1:000}", $"G{n + 2:000}"], created.Select(c => c.Code));
            Assert.Equal((n + 2).ToString("000"), await conn.ExecuteScalarAsync<string>("SELECT PLNNoSeq FROM dbo.PLNSeq WHERE PLNSeqCode = 'G'", transaction: tx));
            var row = await conn.QuerySingleAsync(
                "SELECT PLNDescription, CoPaymentInd, PLNCoPayPerc, PLNLGPrivate, PLNMobile, isIPDependent, PLNPayorPlan FROM dbo.PLN WHERE PLNIndex = @i",
                new { i = created[0].Index }, tx);
            Assert.Equal("TEST PLAN ONE", (string)row.PLNDescription);
            Assert.Equal("Y", (string)row.CoPaymentInd);
            Assert.Equal(20, (int)row.PLNCoPayPerc);
            Assert.Equal(5, (int)row.PLNLGPrivate);
            Assert.Equal("N", (string)row.PLNMobile);
            Assert.Equal("PLAN2", (string)row.PLNPayorPlan);
            await tx.RollbackAsync();
        }
        Assert.Equal(before, await conn.ExecuteScalarAsync<string>("SELECT PLNNoSeq FROM dbo.PLNSeq WHERE PLNSeqCode = 'G'"));
    }
}

public class PlanUpdateTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");

    [Fact]
    public async Task Update_edits_the_row_but_not_its_identity()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await using var conn = new Microsoft.Data.SqlClient.SqlConnection(MainDb);
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        var r = PlanValidatorTests.Valid();
        r.HealthCode = "S";
        r.NumberOfPlans = null;
        r.Plans = [new() { Code = "ZQ9", Description = "BEFORE" }];
        var index = (await PlanService.SaveAsync(conn, tx, r, "TEST")).Single().Index;

        r.Plans = [new() { Code = "ZQ9", Description = "after edit" }];
        r.GroupCompany = "EDITED CO";
        r.EndAge = 70;
        r.CoPayPercent = 15;
        await PlanService.UpdateAsync(conn, tx, index, r, "TEST2");
        var row = await conn.QuerySingleAsync(
            "SELECT RTRIM(PLNCode) AS PLNCode, PLNDescription, GRPCompany, PLNEndAge, PLNCoPayPerc, PLNLastUpdateUser FROM dbo.PLN WHERE PLNIndex = @index",
            new { index }, tx);
        Assert.Equal("ZQ9", (string)row.PLNCode);
        Assert.Equal("AFTER EDIT", (string)row.PLNDescription);
        Assert.Equal("EDITED CO", (string)row.GRPCompany);
        Assert.Equal(70, (int)row.PLNEndAge);
        Assert.Equal(15, (int)row.PLNCoPayPerc);
        Assert.Equal("TEST2", (string)row.PLNLastUpdateUser);

        r.Plans = [new() { Code = "ZQ8", Description = "x" }];
        var changedCode = await Assert.ThrowsAsync<PlanException>(() => PlanService.UpdateAsync(conn, tx, index, r, "TEST"));
        Assert.Contains("cannot be changed", changedCode.Message);
        r.Plans = [new() { Code = "ZQ9", Description = "x" }];
        r.PayorCode = "ET";
        await Assert.ThrowsAsync<PlanException>(() => PlanService.UpdateAsync(conn, tx, index, r, "TEST"));
        var missing = await Assert.ThrowsAsync<PlanException>(() => PlanService.UpdateAsync(conn, tx, -1, r, "TEST"));
        Assert.Equal("Record not found", missing.Message);
        await tx.RollbackAsync();
    }
}
