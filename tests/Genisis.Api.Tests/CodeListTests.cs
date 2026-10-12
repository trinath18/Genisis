using Dapper;
using Genisis.Api.Data;
using Genisis.Api.Plans;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Tests;

public class CodeListTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");

    [Fact]
    public void Messages_follow_desktop()
    {
        var health = CodeLists.All["health-types"];
        Assert.Equal("Please Enter Health Type", CodeLists.Validate(health, new CodeRequest()));
        Assert.Equal("Please Enter Health Description", CodeLists.Validate(health, new CodeRequest { Code = "z" }));
        Assert.Equal("Code cannot be longer than 2 characters.", CodeLists.Validate(health, new CodeRequest { Code = "ABC", Description = "X" }));
        Assert.Null(CodeLists.Validate(health, new CodeRequest { Code = "z9", Description = "Test" }));
        Assert.Equal("Please enter MCO Status.", CodeLists.Validate(CodeLists.All["supp-mco-statuses"], new CodeRequest { Code = "9" }));
    }

    [Theory]
    [InlineData("health-types", "Z9", "N")]
    [InlineData("insured-types", "Z9", "I")]
    [InlineData("product-categories", "Z9", null)]
    [InlineData("supp-mco-statuses", "9", "A")]
    public async Task Add_duplicate_edit_delete_round_trip_is_rolled_back(string name, string code, string? inUse)
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var list = CodeLists.All[name];
        await using var conn = new SqlConnection(MainDb);
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        async Task<List<CodeRow>> Find(string c) => await conn.ProcQueryAsync<CodeRow>($"genisis.{list.Procedure}_Search", new { code = c }, tx);

        Assert.Empty(await Find(code));
        await CodeListService.SaveAsync(conn, tx, list, new CodeRequest { Code = code.ToLowerInvariant(), Description = " First " }, false, "TEST");
        Assert.Equal(new CodeRow(code, "First"), Assert.Single(await Find(code)));

        var dup = await Assert.ThrowsAsync<PlanException>(() =>
            CodeListService.SaveAsync(conn, tx, list, new CodeRequest { Code = code, Description = "Again" }, false, "TEST"));
        Assert.Equal(list.Duplicate, dup.Message);

        await CodeListService.SaveAsync(conn, tx, list, new CodeRequest { Code = code, Description = "Second" }, true, "TEST");
        Assert.Equal("Second", Assert.Single(await Find(code)).Description);

        await CodeListService.DeleteAsync(conn, tx, list, code);
        Assert.Empty(await Find(code));
        Assert.Equal("Record not found", (await Assert.ThrowsAsync<PlanException>(() => CodeListService.DeleteAsync(conn, tx, list, code))).Message);
        Assert.Equal("Record not found", (await Assert.ThrowsAsync<PlanException>(() =>
            CodeListService.SaveAsync(conn, tx, list, new CodeRequest { Code = code, Description = "X" }, true, "TEST"))).Message);

        inUse ??= await conn.ExecuteScalarAsync<string>("SELECT TOP 1 RTRIM(PLNProCat) FROM dbo.PLN WHERE PLNProCat IS NOT NULL", transaction: tx);
        var refused = await Assert.ThrowsAsync<PlanException>(() => CodeListService.DeleteAsync(conn, tx, list, inUse!));
        Assert.StartsWith("This record cannot be deleted because it is used in ", refused.Message);
        Assert.Single(await Find(inUse!));
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Special_note_is_set_and_cleared_on_the_plan_row_only()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await using var conn = new SqlConnection(MainDb);
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        var index = await conn.ExecuteScalarAsync<int>("SELECT MAX(PLNIndex) FROM dbo.PLN", transaction: tx);
        string? Note() => conn.ExecuteScalar<string?>("SELECT PLNSpecialNote FROM dbo.PLN WHERE PLNIndex = @index", new { index }, tx);

        await SpecialNoteService.SaveAsync(conn, tx, index, "  Covers dental  ");
        Assert.Equal("Covers dental", Note());
        await SpecialNoteService.SaveAsync(conn, tx, index, " ");
        Assert.Null(Note());
        Assert.Equal(1, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.PLN WHERE PLNIndex = @index", new { index }, tx));
        Assert.Equal("Record not found", (await Assert.ThrowsAsync<PlanException>(() => SpecialNoteService.SaveAsync(conn, tx, -1, "x"))).Message);
        await tx.RollbackAsync();
    }
}
