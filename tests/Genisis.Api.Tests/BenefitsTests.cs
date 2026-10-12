using Dapper;
using Genisis.Api.Benefits;
using Genisis.Api.Data;
using Genisis.Api.Plans;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Tests;

public class BenefitsTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");
    private static readonly DateTime Effective = new(2099, 1, 1);

    [Fact]
    public void Messages_follow_desktop()
    {
        Assert.Equal("Please Enter Category Code", BenefitService.Validate(new BenefitCategoryRequest()));
        Assert.Equal("Please Enter Category Description", BenefitService.Validate(new BenefitCategoryRequest { Code = "Z" }));
        Assert.Equal("Category Code cannot be longer than 10 characters.", BenefitService.Validate(new BenefitCategoryRequest { Code = "12345678901", Description = "X" }));

        Assert.Equal("Please Enter Category Code", BenefitService.Validate(new BenefitSubCategoryRequest()));
        Assert.Equal("Please Enter Sub Category Code", BenefitService.Validate(new BenefitSubCategoryRequest { CategoryCode = "1" }));
        Assert.Equal("Please Enter Sub Category Description", BenefitService.Validate(new BenefitSubCategoryRequest { CategoryCode = "1", Code = "Z1" }));

        var entry = new BenefitEntryRequest();
        Assert.Equal("Please Enter Health Type", BenefitService.Validate(entry));
        entry.HealthCode = "N";
        Assert.Equal("Please Enter Insured Type", BenefitService.Validate(entry));
        entry.InsuredCode = "I";
        Assert.Equal("Please Enter Plan Code", BenefitService.Validate(entry));
        entry.PlanCode = "M225";
        Assert.Equal("Please Enter a valid Effective Date", BenefitService.Validate(entry));
        entry.EffectiveDate = Effective;
        Assert.Equal("Please Enter RM Per Disability for at least one sub category.", BenefitService.Validate(entry));
        entry.Lines = [new() { SubCategoryCode = "101", Days = 10 }];
        Assert.Equal("Sub category 101: Please Enter RM Per Disability", BenefitService.Validate(entry));
        entry.Lines = [new() { SubCategoryCode = "101", PerDisability = "FULL" }, new() { SubCategoryCode = "101 ", PerDisability = "1" }];
        Assert.Equal("Sub category 101 is entered more than once.", BenefitService.Validate(entry));
        entry.Lines = [new() { SubCategoryCode = "101", Days = 40000, PerDisability = "1" }];
        Assert.Equal("Sub category 101: Days/Months must be a whole number from 0 to 32767.", BenefitService.Validate(entry));
        entry.Lines = [new() { SubCategoryCode = "101", PerDisability = "FULL" }, new() { SubCategoryCode = "102" }];
        Assert.Null(BenefitService.Validate(entry));

        Assert.Equal("Please Enter Benefit Sub Category Code", BenefitService.Validate(new BenefitRequest()));
        Assert.Equal("Please Enter RM Per Disability",
            BenefitService.Validate(new BenefitRequest { SubCategoryCode = "101", HealthCode = "N", InsuredCode = "I", PlanCode = "M225", EffectiveDate = Effective }));
    }

    [Fact]
    public async Task Category_and_sub_category_round_trip_is_rolled_back()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await using var conn = new SqlConnection(MainDb);
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        async Task<List<BenefitCategoryRow>> Categories(string? code = null, string? description = null) =>
            await conn.ProcQueryAsync<BenefitCategoryRow>("genisis.BenefitCategory_Search", new { code, description }, tx);
        async Task<List<BenefitSubCategoryRow>> Subs(string code) =>
            await conn.ProcQueryAsync<BenefitSubCategoryRow>("genisis.BenefitSubCategory_Search", new { code }, tx);

        await BenefitService.SaveCategoryAsync(conn, tx, new BenefitCategoryRequest { Code = "zz9", Description = " Test Category " }, false, "TEST");
        Assert.Equal(new BenefitCategoryRow("ZZ9", "Test Category"), Assert.Single(await Categories("ZZ9")));
        Assert.Contains(await Categories(description: "Test Categ"), c => c.Code == "ZZ9");
        Assert.Equal(BenefitService.Duplicated, (await Assert.ThrowsAsync<PlanException>(() =>
            BenefitService.SaveCategoryAsync(conn, tx, new BenefitCategoryRequest { Code = "ZZ9", Description = "Again" }, false, "TEST"))).Message);
        await BenefitService.SaveCategoryAsync(conn, tx, new BenefitCategoryRequest { Code = "ZZ9", Description = "Renamed" }, true, "TEST");
        Assert.Equal("Renamed", Assert.Single(await Categories("ZZ9")).Description);

        Assert.Equal("Category Code was not found.", (await Assert.ThrowsAsync<PlanException>(() =>
            BenefitService.SaveSubCategoryAsync(conn, tx, new BenefitSubCategoryRequest { CategoryCode = "ZZ8", Code = "ZZ91", Name = "X" }, false, "TEST"))).Message);
        await BenefitService.SaveSubCategoryAsync(conn, tx, new BenefitSubCategoryRequest { CategoryCode = "zz9", Code = "zz91", Name = "Test Sub" }, false, "TEST");
        Assert.Equal(new BenefitSubCategoryRow("ZZ9", "Renamed", "ZZ91", "Test Sub"), Assert.Single(await Subs("ZZ91")));
        Assert.Equal(BenefitService.Duplicated, (await Assert.ThrowsAsync<PlanException>(() =>
            BenefitService.SaveSubCategoryAsync(conn, tx, new BenefitSubCategoryRequest { CategoryCode = "ZZ9", Code = "ZZ91", Name = "Y" }, false, "TEST"))).Message);

        var refused = await Assert.ThrowsAsync<PlanException>(() => BenefitService.DeleteCategoryAsync(conn, tx, "ZZ9"));
        Assert.Equal("This record cannot be deleted because it is used in Benefit table!", refused.Message);

        var used = await conn.ExecuteScalarAsync<string>("SELECT TOP 1 RTRIM(BNFCode) FROM dbo.BNF", transaction: tx);
        Assert.StartsWith("This record cannot be deleted", (await Assert.ThrowsAsync<PlanException>(() => BenefitService.DeleteSubCategoryAsync(conn, tx, used!))).Message);

        await BenefitService.DeleteSubCategoryAsync(conn, tx, "ZZ91");
        Assert.Empty(await Subs("ZZ91"));
        await BenefitService.DeleteCategoryAsync(conn, tx, "ZZ9");
        Assert.Empty(await Categories("ZZ9"));
        Assert.Equal("Record not found", (await Assert.ThrowsAsync<PlanException>(() => BenefitService.DeleteCategoryAsync(conn, tx, "ZZ9"))).Message);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Entry_and_listing_round_trip_is_rolled_back()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await using var conn = new SqlConnection(MainDb);
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();

        // A plan with no members on health N / insured I, so its rows can be deleted.
        var plan = await conn.ExecuteScalarAsync<string>(
            "SELECT TOP 1 RTRIM(p.PLNCode) FROM dbo.PLN p WHERE p.HLTCode = 'N' AND NOT EXISTS (SELECT 1 FROM dbo.MBM m WHERE m.PLNCode = p.PLNCode) ORDER BY p.PLNIndex DESC",
            transaction: tx);
        Assert.NotNull(plan);
        async Task<List<BenefitRow>> Rows() =>
            await conn.ProcQueryAsync<BenefitRow>("genisis.Benefit_Search", new { top = 100, plan, effective = Effective }, tx);

        var entry = new BenefitEntryRequest
        {
            PlanCode = plan!.ToLowerInvariant(), InsuredCode = "i", HealthCode = "n", EffectiveDate = Effective.AddHours(9),
            Lines = [new() { SubCategoryCode = "101", Days = 120, RatePerDay = "80", PerDisability = "9600" }, new() { SubCategoryCode = "103", PerDisability = "full" }, new()],
        };
        Assert.Equal(2, await BenefitService.SaveEntryAsync(conn, tx, entry, "TEST"));
        var rows = await Rows();
        Assert.Equal(2, rows.Count);
        var room = rows.Single(r => r.SubCategoryCode == "101");
        Assert.Equal((plan, "I", "N", (short?)120, "80", "9600", Effective), (room.PlanCode, room.InsuredCode, room.HealthCode, room.Days, room.RatePerDay, room.PerDisability, room.EffectiveDate));
        Assert.Equal("FULL", rows.Single(r => r.SubCategoryCode == "103").PerDisability);
        Assert.Null(await conn.ExecuteScalarAsync<short?>("SELECT BNFEffDateVersion FROM dbo.BNF WHERE BNFCode = '101' AND PLNCode = @plan AND BNFEffDate = @Effective", new { plan, Effective }, tx));

        var lines = await conn.ProcQueryAsync<BenefitEntryLine>("genisis.BenefitEntry_Load", new { plan, insured = "I", health = "N", effective = Effective }, tx);
        Assert.Equal("9600", lines.Single(l => l.Code == "101").PerDisability);
        Assert.Null(lines.Single(l => l.Code == "102").PerDisability);
        Assert.Equal(BenefitService.SetExists, (await Assert.ThrowsAsync<PlanException>(() => BenefitService.SaveEntryAsync(conn, tx, entry, "TEST"))).Message);

        var single = new BenefitRequest { SubCategoryCode = "102", HealthCode = "N", InsuredCode = "I", PlanCode = plan, EffectiveDate = Effective, Days = 20, RatePerDay = "200", PerDisability = "4000" };
        await BenefitService.CreateAsync(conn, tx, single, "TEST");
        Assert.Equal((short?)1, await conn.ExecuteScalarAsync<short?>("SELECT BNFEffDateVersion FROM dbo.BNF WHERE BNFCode = '102' AND PLNCode = @plan AND BNFEffDate = @Effective", new { plan, Effective }, tx));
        Assert.Equal($"{BenefitService.Duplicated} (sub category 102)", (await Assert.ThrowsAsync<PlanException>(() => BenefitService.CreateAsync(conn, tx, single, "TEST"))).Message);
        Assert.Equal("Plan Code was not found.", (await Assert.ThrowsAsync<PlanException>(() =>
            BenefitService.CreateAsync(conn, tx, new BenefitRequest { SubCategoryCode = "102", HealthCode = "N", InsuredCode = "I", PlanCode = "ZZ#9", EffectiveDate = Effective, PerDisability = "1" }, "TEST"))).Message);

        single.Days = null;
        single.RatePerDay = " ";
        single.PerDisability = "5000";
        await BenefitService.UpdateAsync(conn, tx, single, "TEST");
        var updated = (await Rows()).Single(r => r.SubCategoryCode == "102");
        Assert.Equal(((short?)null, (string?)null, "5000"), (updated.Days, updated.RatePerDay, updated.PerDisability));

        await BenefitService.DeleteAsync(conn, tx, "102", "I", plan, Effective);
        Assert.Equal(2, (await Rows()).Count);
        Assert.Equal("Record not found", (await Assert.ThrowsAsync<PlanException>(() => BenefitService.DeleteAsync(conn, tx, "102", "I", plan, Effective))).Message);
        Assert.Equal("Record not found", (await Assert.ThrowsAsync<PlanException>(() => BenefitService.UpdateAsync(conn, tx, single, "TEST"))).Message);

        var withMember = await conn.QueryFirstAsync<(string Code, string Insured, string Plan, DateTime Effective)>(
            "SELECT TOP 1 RTRIM(b.BNFCode), RTRIM(b.INSCode), RTRIM(b.PLNCode), b.BNFEffDate FROM dbo.BNF b JOIN dbo.MBM m ON m.PLNCode = b.PLNCode AND m.HLTCode = b.HLTCode AND m.INSCode = b.INSCode",
            transaction: tx);
        Assert.Equal("This record cannot be deleted because it is used in Member table!", (await Assert.ThrowsAsync<PlanException>(() =>
            BenefitService.DeleteAsync(conn, tx, withMember.Code, withMember.Insured, withMember.Plan, withMember.Effective))).Message);
        await tx.RollbackAsync();
    }
}
