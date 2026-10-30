using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Receipt.Tracking;

namespace Genisis.Api.Tests;

/// <summary>Receipt > Receipt Tracking: the validator and the service, against a fake repository.</summary>
public class ReceiptTrackingRulesTests
{
    private sealed class FakeRepository : IReceiptTrackingRepository
    {
        public string Used { get; private set; } = "";

        public Task<List<ReceiptTrackingDetailRow>> DetailAsync(IDbSession session, ReceiptTrackingFilter filter)
        {
            Used = "detail";
            return Task.FromResult(new List<ReceiptTrackingDetailRow> { new() { ReceiptNo = "R1", Amount = 10.5m }, new() { ReceiptNo = "R2", Amount = null }, new() { ReceiptNo = "R3", Amount = 4m } });
        }

        public Task<List<ReceiptTrackingSummaryRow>> SummaryAsync(IDbSession session, ReceiptTrackingFilter filter)
        {
            Used = "summary " + filter.Mode;
            return Task.FromResult(new List<ReceiptTrackingSummaryRow> { new() { Key1 = "B1", Cases = 2, Amount = 100m }, new() { Key1 = "B2", Cases = 1, Amount = 50.25m } });
        }
    }

    [Theory]
    [InlineData("source", "supplier", false)]
    [InlineData("source", "mnrb", true)]
    [InlineData("mode", "weekly", false)]
    [InlineData("mode", "receipt", true)]
    [InlineData("health", "X", false)]
    [InlineData("health", "s - Sihat", true)]
    public void The_choices_are_the_ones_the_option_buttons_offer(string field, string value, bool valid)
    {
        var filter = new ReceiptTrackingFilter();
        if (field == "source") filter.Source = value;
        else if (field == "mode") filter.Mode = value;
        else filter.HealthType = value;
        Assert.Equal(valid, new ReceiptTrackingFilterValidator().FirstError(filter) is null);
    }

    [Fact]
    public async Task The_detail_listing_totals_the_amount_column()
    {
        var repository = new FakeRepository();
        var result = await new ReceiptTrackingService(new FakeSessions(), repository).SearchAsync(new ReceiptTrackingFilter());
        Assert.Equal(("detail", 3, 14.5m, "member"), (repository.Used, result.Count, result.TotalAmount, result.Source));
        Assert.Empty(result.Summary);
    }

    [Fact]
    public async Task A_summary_totals_its_amounts()
    {
        var repository = new FakeRepository();
        var result = await new ReceiptTrackingService(new FakeSessions(), repository).SearchAsync(new ReceiptTrackingFilter { Source = "payor", Mode = "bordx" });
        Assert.Equal(("summary bordx", 2, 150.25m, "payor"), (repository.Used, result.Count, result.TotalAmount, result.Source));
        Assert.Empty(result.Detail);
    }
}

/// <summary>Receipt Tracking against the database (read only).</summary>
public class ReceiptTrackingDatabaseTests
{
    private static ReceiptTrackingRepository Repository => new();

    private static async Task<int> Count(IDbSession s, string sql, object? args = null) => await s.Connection.ExecuteScalarAsync<int>(sql, args, s.Transaction);

    [Fact]
    public async Task The_detail_listing_gives_what_each_view_holds_for_each_criterion()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        foreach (var (source, view) in new[] { ("member", "Pay_TrackMember"), ("payor", "Pay_TrackPayor"), ("mnrb", "Pay_TrackMnrb") })
        {
            var all = await Repository.DetailAsync(session, new ReceiptTrackingFilter { Source = source, ReceiptFrom = "0", ReceiptTo = "ZZZZZZZZZZ" });
            Assert.Equal(Math.Min(await Count(session, $"SELECT COUNT(*) FROM genisis.{view} WHERE ReceiptNo >= '0' AND ReceiptNo <= 'ZZZZZZZZZZ'"), WorksheetLimitsFetch), all.Count);
            if (all.Count == 0) continue;
            var first = all.FirstOrDefault(r => r.ReceiptNo is not null && r.PayorCode is not null);
            if (first is null) continue;
            Assert.Equal(await Count(session, $"SELECT COUNT(*) FROM genisis.{view} WHERE ReceiptNo >= @r AND ReceiptNo <= @r AND PAYCode = @p", new { r = first.ReceiptNo, p = first.PayorCode }),
                (await Repository.DetailAsync(session, new ReceiptTrackingFilter { Source = source, ReceiptFrom = " " + first.ReceiptNo!.ToLowerInvariant(), ReceiptTo = first.ReceiptNo, Payor = first.PayorCode + " - X" })).Count);
        }
    }

    private static int WorksheetLimitsFetch => Genisis.Api.Worksheet.WorksheetLimits.FetchRows;

    [Fact]
    public async Task The_member_detail_shows_the_receipt_amount_and_the_payor_detail_the_cheque_amount_and_the_amount_criterion_is_on_the_receipt_amount()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var member = await Repository.DetailAsync(session, new ReceiptTrackingFilter { Source = "member", AmountFrom = 1000m, AmountTo = 1100m });
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM genisis.Pay_TrackMember WHERE ReceiptAmt >= 1000 AND ReceiptAmt <= 1100"), member.Count);
        Assert.All(member, r => Assert.InRange(r.Amount!.Value, 1000m, 1100m));
        var payor = await Repository.DetailAsync(session, new ReceiptTrackingFilter { Source = "payor", AmountFrom = 1000m, AmountTo = 1100m });
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM genisis.Pay_TrackPayor WHERE ReceiptAmt >= 1000 AND ReceiptAmt <= 1100"), payor.Count);
        var cheques = await session.Connection.ExecuteScalarAsync<decimal>("SELECT ISNULL(SUM(CheckAmt), 0) FROM genisis.Pay_TrackPayor WHERE ReceiptAmt >= 1000 AND ReceiptAmt <= 1100", transaction: session.Transaction);
        Assert.Equal(cheques, payor.Sum(r => r.Amount ?? 0));
    }

    [Fact]
    public async Task The_summaries_count_and_add_up_the_summary_views()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var byPayor = await Repository.SummaryAsync(session, new ReceiptTrackingFilter { Source = "member", Mode = "payor" });
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM (SELECT RTRIM(PAYCode) AS p FROM genisis.Pay_TrackMember2 GROUP BY RTRIM(PAYCode)) x"), byPayor.Count);
        Assert.Equal(await Count(session, "SELECT COUNT(ReceiptNo) FROM genisis.Pay_TrackMember2"), byPayor.Sum(r => r.Cases));
        Assert.Equal(await session.Connection.ExecuteScalarAsync<decimal>("SELECT ISNULL(SUM(ReceiptMBMAmt), 0) FROM genisis.Pay_TrackMember2", transaction: session.Transaction), byPayor.Sum(r => r.Amount ?? 0));

        var byBordx = await Repository.SummaryAsync(session, new ReceiptTrackingFilter { Source = "payor", Mode = "bordx" });
        Assert.Equal(Math.Min(await Count(session, "SELECT COUNT(*) FROM (SELECT BordxNo, BordxDate FROM genisis.Pay_TrackPayor2 GROUP BY BordxNo, BordxDate) x"), WorksheetLimitsFetch), byBordx.Count);
        var one = await Repository.SummaryAsync(session, new ReceiptTrackingFilter { Source = "member", Mode = "receipt", ReceiptFrom = "0", ReceiptTo = "0" });
        Assert.Empty(one);
        var byReceipt = await Repository.SummaryAsync(session, new ReceiptTrackingFilter { Source = "payor", Mode = "receipt", Payor = "AH" });
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM (SELECT ReceiptNo, ReceiptDate FROM genisis.Pay_TrackPayor2 WHERE PAYCode = 'AH' GROUP BY ReceiptNo, ReceiptDate) x"), byReceipt.Count);
        Assert.Empty(await Repository.SummaryAsync(session, new ReceiptTrackingFilter { Source = "mnrb", Mode = "bordx" }));
    }
}
