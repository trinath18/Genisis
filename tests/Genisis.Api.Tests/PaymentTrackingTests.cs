using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Payment.Tracking;
using Genisis.Api.Worksheet;

namespace Genisis.Api.Tests;

/// <summary>Payment > Payment Tracking: the validator and the service, against a fake repository.</summary>
public class PaymentTrackingRulesTests
{
    private sealed class FakeRepository : IPaymentTrackingRepository
    {
        public string Used { get; private set; } = "";

        public Task<List<PaymentTrackingDetailRow>> DetailAsync(IDbSession session, PaymentTrackingFilter filter)
        {
            Used = "detail";
            return Task.FromResult(new List<PaymentTrackingDetailRow> { new() { PvNo = "CB1", Amount = 10.5m }, new() { PvNo = "CB2", Amount = null }, new() { PvNo = "CB3", Amount = 4m } });
        }

        public Task<List<PaymentTrackingSummaryRow>> SummaryAsync(IDbSession session, PaymentTrackingFilter filter)
        {
            Used = "summary " + filter.Mode;
            return Task.FromResult(new List<PaymentTrackingSummaryRow> { new() { Key1 = "H1", Cases = 2, Amount = 100m }, new() { Key1 = "H2", Cases = 1, Amount = 50.25m } });
        }
    }

    [Theory]
    [InlineData("source", "payor", false)]
    [InlineData("source", "member", true)]
    [InlineData("mode", "receipt", false)]
    [InlineData("mode", "payorDate", true)]
    [InlineData("health", "X", false)]
    [InlineData("health", "n - Non-Sihat", true)]
    [InlineData("status", "Z", false)]
    [InlineData("status", "p", true)]
    public void The_choices_are_the_ones_the_screen_offers(string field, string value, bool valid)
    {
        var filter = new PaymentTrackingFilter();
        if (field == "source") filter.Source = value;
        else if (field == "mode") filter.Mode = value;
        else if (field == "health") filter.HealthType = value;
        else filter.Status = value;
        Assert.Equal(valid, new PaymentTrackingFilterValidator().FirstError(filter) is null);
    }

    [Fact]
    public void The_pv_dates_have_to_be_in_order()
    {
        var bad = new PaymentTrackingFilter { DateFrom = new DateTime(2021, 3, 2), DateTo = new DateTime(2021, 3, 1) };
        Assert.Equal("PV Date From cannot be later than PV Date To.", new PaymentTrackingFilterValidator().FirstError(bad));
    }

    [Fact]
    public async Task The_detail_listing_totals_the_amount_column()
    {
        var repository = new FakeRepository();
        var result = await new PaymentTrackingService(new FakeSessions(), repository).SearchAsync(new PaymentTrackingFilter());
        Assert.Equal(("detail", 3, 14.5m, "hospital"), (repository.Used, result.Count, result.TotalAmount, result.Source));
        Assert.Empty(result.Summary);
    }

    [Fact]
    public async Task A_summary_totals_its_amounts()
    {
        var repository = new FakeRepository();
        var result = await new PaymentTrackingService(new FakeSessions(), repository).SearchAsync(new PaymentTrackingFilter { Source = "member", Mode = "hospital" });
        Assert.Equal(("summary hospital", 2, 150.25m, "member"), (repository.Used, result.Count, result.TotalAmount, result.Source));
        Assert.Empty(result.Detail);
    }
}

/// <summary>Payment Tracking against the database (read only).</summary>
public class PaymentTrackingDatabaseTests
{
    private static PaymentTrackingRepository Repository => new();

    private static async Task<int> Count(IDbSession s, string sql, object? args = null) => await s.Connection.ExecuteScalarAsync<int>(sql, args, s.Transaction);

    private static async Task<decimal> Sum(IDbSession s, string sql, object? args = null) => await s.Connection.ExecuteScalarAsync<decimal>(sql, args, s.Transaction);

    [Fact]
    public async Task The_detail_listing_gives_what_each_view_holds_for_a_pv_range_and_a_payor()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        foreach (var (source, view) in new[] { ("hospital", "Pay_TrackHospital"), ("member", "Pay_TrackPaymentMember") })
        {
            var all = await Repository.DetailAsync(session, new PaymentTrackingFilter { Source = source });
            Assert.Equal(Math.Min(await Count(session, $"SELECT COUNT(*) FROM genisis.{view}"), WorksheetLimits.FetchRows), all.Count);
            var first = all.FirstOrDefault(r => r.PvNo is not null && r.PayorCode is not null);
            if (first is null) continue;
            var expected = await Count(session, $"SELECT COUNT(*) FROM genisis.{view} WHERE PVNo = @pv AND PAYCode = @p", new { pv = first.PvNo, p = first.PayorCode });
            var found = await Repository.DetailAsync(session, new PaymentTrackingFilter { Source = source, PvFrom = " " + first.PvNo!.ToLowerInvariant(), PvTo = first.PvNo, Payor = first.PayorCode + " - X" });
            Assert.Equal(expected, found.Count);
            Assert.All(found, r => Assert.Equal(first.PvNo, r.PvNo));
        }
    }

    [Fact]
    public async Task The_summaries_count_and_add_up_the_view()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var byHospital = await Repository.SummaryAsync(session, new PaymentTrackingFilter { Source = "hospital", Mode = "hospital" });
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM (SELECT RTRIM(HOSName) AS h FROM genisis.Pay_TrackHospital GROUP BY HOSName) x"), byHospital.Count);
        Assert.Equal(await Count(session, "SELECT COUNT(HOSName) FROM genisis.Pay_TrackHospital"), byHospital.Sum(r => r.Cases));
        Assert.Equal(await Sum(session, "SELECT ISNULL(SUM(PVPaidAmt), 0) FROM genisis.Pay_TrackHospital"), byHospital.Sum(r => r.Amount ?? 0));

        var day = new DateTime(2021, 2, 8);
        var byPv = await Repository.SummaryAsync(session, new PaymentTrackingFilter { Source = "member", Mode = "pv", DateFrom = day, DateTo = day });
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM (SELECT PVNo, PVDate FROM genisis.Pay_TrackPaymentMember WHERE PVDate = @d GROUP BY PVNo, PVDate) x", new { d = day }), byPv.Count);
        Assert.Equal(await Sum(session, "SELECT ISNULL(SUM(PVPaidAmt), 0) FROM genisis.Pay_TrackPaymentMember WHERE PVDate = @d", new { d = day }), byPv.Sum(r => r.Amount ?? 0));
        Assert.Empty(await Repository.SummaryAsync(session, new PaymentTrackingFilter { Source = "hospital", Mode = "bordx", PvFrom = "0", PvTo = "0" }));
    }
}
