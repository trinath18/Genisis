using Dapper;
using Genisis.Api.ClaimBordx.Control;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Tests;

/// <summary>Claim Bordx > Monitoring > Bordx Control: the validator, the day counts and the layout choice, against a fake repository.</summary>
public class BordxControlRulesTests
{
    private sealed class FakeRepository : IBordxControlRepository
    {
        public string? Used { get; private set; }

        public Task<List<BordxControlPayor>> PayorsAsync(IDbSession session) => Task.FromResult(new List<BordxControlPayor> { new("AB", "A PAYOR", "N"), new("SH", "SIHAT", "S"), new("XX", "NO KIND", null) });

        public Task<List<BordxControlRow>> SihatAsync(IDbSession session, string? payor, BordxControlFilter filter)
        {
            Used = "S:" + payor;
            return Task.FromResult(new List<BordxControlRow>
            {
                new() { BordxNo = "S1", Amount = 100m, Days = 0, Days2 = 3 }, new() { BordxNo = "S2", Amount = 50.5m, Days = null, Days2 = 0 }, new() { BordxNo = "S3", Amount = null },
            });
        }

        public Task<List<BordxControlRow>> NonSihatAsync(IDbSession session, string? payor, BordxControlFilter filter)
        {
            Used = "N:" + payor;
            return Task.FromResult(new List<BordxControlRow> { new() { BordxNo = "N1", Amount = 10m, Days = 5, Days2 = 0 } });
        }
    }

    private static BordxControlService Service(FakeRepository repository) => new(new FakeSessions(), repository);

    [Fact]
    public void A_payor_is_needed_unless_one_of_the_all_scopes_is_chosen()
    {
        var validator = new BordxControlFilterValidator();
        Assert.Equal("Please Enter Payor Code.", validator.FirstError(new BordxControlFilter()));
        Assert.Null(validator.FirstError(new BordxControlFilter { Payor = "AB - A PAYOR" }));
        Assert.Null(validator.FirstError(new BordxControlFilter { Scope = "allSihat" }));
        Assert.Equal("Select a payor, All Sihat or All Non-Sihat.", validator.FirstError(new BordxControlFilter { Scope = "everything" }));
    }

    [Theory]
    [InlineData("PayTo", "2", false)]
    [InlineData("PayTo", "1", true)]
    [InlineData("MnrbTo", "B", true)]
    [InlineData("MnrbTo", "F", false)]
    [InlineData("MnrbFr", "u", true)]
    [InlineData("PayFr", "X", false)]
    [InlineData("PayDocFr", "0", true)]
    public void The_status_choices_are_the_ones_the_radio_buttons_offer(string field, string value, bool valid)
    {
        var filter = new BordxControlFilter { Scope = "allNonSihat" };
        typeof(BordxControlFilter).GetProperty(field)!.SetValue(filter, value);
        Assert.Equal(valid, new BordxControlFilterValidator().FirstError(filter) is null);
    }

    [Fact]
    public async Task A_sihat_payor_gets_the_sihat_layout_with_a_same_day_count_of_one_and_the_total_days_added()
    {
        var repository = new FakeRepository();
        var result = await Service(repository).SearchAsync(new BordxControlFilter { Payor = "sh - SIHAT" });
        Assert.Equal(("S", "S:SH"), (result.Kind, repository.Used));
        Assert.Equal([(1, 3, 4), (null, 1, 1), (null, null, 0)], result.Rows.Select(r => (r.Days, r.Days2, r.TotalDays)));
        Assert.Equal(150.5m, result.TotalAmount);
    }

    [Fact]
    public async Task A_payor_with_no_health_code_is_taken_as_sihat_like_the_desktop_did_and_a_non_sihat_payor_has_no_total_days()
    {
        var repository = new FakeRepository();
        Assert.Equal("S", (await Service(repository).SearchAsync(new BordxControlFilter { Payor = "XX" })).Kind);
        var result = await Service(repository).SearchAsync(new BordxControlFilter { Payor = "AB" });
        Assert.Equal(("N", "N:AB"), (result.Kind, repository.Used));
        Assert.Equal((5, 1, (int?)null), (result.Rows[0].Days, result.Rows[0].Days2, result.Rows[0].TotalDays));
    }

    [Fact]
    public async Task The_all_scopes_search_every_payor_of_the_layout_and_an_unknown_payor_is_not_found()
    {
        var repository = new FakeRepository();
        Assert.Equal(("N", "N:"), ((await Service(repository).SearchAsync(new BordxControlFilter { Scope = "allNonSihat" })).Kind, repository.Used));
        Assert.Equal(("S", "S:"), ((await Service(repository).SearchAsync(new BordxControlFilter { Scope = "allSihat" })).Kind, repository.Used));
        var failure = await Assert.ThrowsAsync<NotFoundException>(() => Service(repository).SearchAsync(new BordxControlFilter { Payor = "ZZ" }));
        Assert.Equal("Payor not found", failure.Message);
    }
}

/// <summary>Bordx Control against the database (read only).</summary>
public class BordxControlDatabaseTests
{
    private static BordxControlRepository Repository => new();

    private static async Task<int> Count(IDbSession s, string sql, object? args = null) => await s.Connection.ExecuteScalarAsync<int>(sql, args, s.Transaction);

    [Fact]
    public async Task The_non_sihat_layout_gives_the_rows_of_the_view_for_each_criterion_and_the_sihat_layout_has_none_in_this_data()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var payors = await Repository.PayorsAsync(session);
        Assert.Contains(payors, p => p.HealthCode == "N");
        var payor = await session.Connection.QueryFirstAsync<string>("SELECT TOP (1) BordxPAYCode FROM genisis.WS_BordxControlN GROUP BY BordxPAYCode ORDER BY COUNT(*)", transaction: session.Transaction);
        var expected = await Count(session, "SELECT COUNT(*) FROM genisis.WS_BordxControlN WHERE BordxPAYCode = @payor", new { payor });

        var all = await Repository.NonSihatAsync(session, payor, new BordxControlFilter());
        Assert.Equal(Math.Min(expected, Genisis.Api.Worksheet.WorksheetLimits.FetchRows), all.Count);
        Assert.All(all, r => Assert.Equal(payor, r.PayorCode));

        foreach (var flag in new[] { "F", "P", "U" })
            Assert.Equal(
                Math.Min(await Count(session, "SELECT COUNT(*) FROM genisis.WS_BordxControlN WHERE BordxPAYCode = @payor AND ((@f = 'U' AND (BordxPAYFrStatus = '' OR BordxPAYFrStatus IS NULL)) OR BordxPAYFrStatus = @f)", new { payor, f = flag }), Genisis.Api.Worksheet.WorksheetLimits.FetchRows),
                (await Repository.NonSihatAsync(session, payor, new BordxControlFilter { PayFr = flag.ToLowerInvariant() })).Count);

        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM genisis.WS_BordxControlN WHERE BordxPAYCode = @payor AND BordxPAYToStatus = '1'", new { payor }), (await Repository.NonSihatAsync(session, payor, new BordxControlFilter { PayTo = "1" })).Count);
        var first = all[0];
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM genisis.WS_BordxControlN WHERE BordxPAYCode = @payor AND BordxRefNo >= @n AND BordxRefNo <= @n", new { payor, n = first.BordxNo }),
            (await Repository.NonSihatAsync(session, payor, new BordxControlFilter { BordxFrom = " " + first.BordxNo.ToLowerInvariant(), BordxTo = first.BordxNo })).Count);
        var dated = await Repository.NonSihatAsync(session, payor, new BordxControlFilter { DateFrom = new DateTime(2020, 1, 1), DateTo = new DateTime(2020, 12, 31) });
        Assert.Equal(Math.Min(await Count(session, "SELECT COUNT(*) FROM genisis.WS_BordxControlN WHERE BordxPAYCode = @payor AND BordxDate >= '20200101' AND BordxDate <= '20201231'", new { payor }), Genisis.Api.Worksheet.WorksheetLimits.FetchRows), dated.Count);
        Assert.All(dated, r => Assert.Equal(2020, r.BordxDate!.Value.Year));

        Assert.All(all.Where(r => r.SentToPayor is not null && r.DocReceived is not null), r => Assert.Equal((int)(r.DocReceived!.Value.Date - r.SentToPayor!.Value.Date).TotalDays, r.Days));
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM genisis.WS_BordxControlS"), (await Repository.SihatAsync(session, null, new BordxControlFilter { MnrbTo = "B" })).Count);
    }

    [Fact]
    public async Task The_service_picks_the_layout_from_the_payors_health_code_and_totals_the_amounts()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        var service = new BordxControlService(TestDb.Sessions(), Repository);
        var payor = (await service.PayorsAsync()).First(p => p.HealthCode == "N");
        var result = await service.SearchAsync(new BordxControlFilter { Payor = payor.Code + " - " + payor.Name, PayTo = "0" });
        Assert.Equal("N", result.Kind);
        Assert.Equal(result.Rows.Sum(r => r.Amount ?? 0), result.TotalAmount);
        Assert.All(result.Rows, r => Assert.Null(r.TotalDays));
        await Assert.ThrowsAsync<NotFoundException>(() => service.SearchAsync(new BordxControlFilter { Payor = "QQ" }));
    }
}
