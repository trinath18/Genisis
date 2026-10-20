using Genisis.Api.Data;
using Genisis.Api.Maintenance.AccountTransaction;
using Microsoft.Extensions.Configuration;

namespace Genisis.Api.Tests;

public class AccountTransactionTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");

    [Fact]
    public void Part_A_rows_show_only_the_part_A_fee()
    {
        var partA = AccountTransactionService.ShowFees(new AccountTransactionRow { Part = "A ", PartACost = "365", SurgeonFee = 1, AnaesthetistFee = 2 });
        Assert.Equal(("365", null, null), (partA.PartACost, partA.SurgeonFee, partA.AnaesthetistFee));
        var partB = AccountTransactionService.ShowFees(new AccountTransactionRow { Part = "B", PartACost = "9", SurgeonFee = 725, AnaesthetistFee = 300 });
        Assert.Equal((null, 725m, 300m), (partB.PartACost, partB.SurgeonFee, partB.AnaesthetistFee));
    }

    [Fact]
    public void Criteria_are_optional_but_bounded()
    {
        var validator = new AccountTransactionQueryValidator();
        Assert.True(validator.Validate(new AccountTransactionQuery()).IsValid);
        var result = validator.Validate(new AccountTransactionQuery { Opcs = new string('x', AccountTransactionQueryValidator.MaxLength + 1) });
        Assert.Equal($"OPCS must be at most {AccountTransactionQueryValidator.MaxLength} characters.", Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task The_schedule_is_searched_with_typed_criteria()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var sessions = new DbSessions(new DbConnections(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:MainDb"] = MainDb }).Build()));
        var service = new AccountTransactionService(sessions, new AccountTransactionRepository());

        var all = await service.SearchAsync(new AccountTransactionQuery());
        Assert.NotEmpty(all.Rows);
        Assert.False(all.Truncated);
        Assert.All(all.Rows.Where(r => r.Part == "A"), r => Assert.Null(r.SurgeonFee));

        var sample = all.Rows.First(r => !string.IsNullOrEmpty(r.Opcs) && !string.IsNullOrEmpty(r.Part));
        var found = await service.SearchAsync(new AccountTransactionQuery { Opcs = sample.Opcs, Part = sample.Part });
        Assert.Contains(found.Rows, r => r.Opcs == sample.Opcs);
        Assert.All(found.Rows, r => Assert.Equal(sample.Part, r.Part));
        Assert.Empty((await service.SearchAsync(new AccountTransactionQuery { Opcs = "NO-SUCH-OPCS-CODE" })).Rows);
    }
}
