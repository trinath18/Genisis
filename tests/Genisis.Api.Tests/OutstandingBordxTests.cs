using Dapper;
using Genisis.Api.ClaimBordx.Control;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Tests;

/// <summary>Claim Bordx > Monitoring > Outstanding Bordx: the validator, the day counts, the buckets and the totals, against fake repositories.</summary>
public class OutstandingBordxRulesTests
{
    private sealed class FakePayors : IBordxControlRepository
    {
        public Task<List<BordxControlPayor>> PayorsAsync(IDbSession session) => Task.FromResult(new List<BordxControlPayor> { new("AB", "A PAYOR", "N"), new("SH", "SIHAT", "S") });

        public Task<List<BordxControlRow>> SihatAsync(IDbSession session, string? payor, BordxControlFilter filter) => Task.FromResult(new List<BordxControlRow>());

        public Task<List<BordxControlRow>> NonSihatAsync(IDbSession session, string? payor, BordxControlFilter filter) => Task.FromResult(new List<BordxControlRow>());
    }

    private sealed class FakeRepository : IOutstandingBordxRepository
    {
        public List<string> Calls { get; } = [];

        public Task<List<OutstandingRow>> DetailAsync(IDbSession session, string section, string? payor, string? health)
        {
            Calls.Add($"detail {section} {payor} {health}");
            return Task.FromResult(new List<OutstandingRow>
            {
                new() { BordxNo = "B1", PayorCode = "SH", Amount = 100m, SentDate = new DateTime(2021, 5, 1) },
                new() { BordxNo = "B2", PayorCode = "SH", Amount = 50m, SentDate = new DateTime(2021, 5, 20) },
                new() { BordxNo = "B3", PayorCode = "SH", Amount = 25m, SentDate = new DateTime(2021, 6, 1) },
            });
        }

        public Task<List<OutstandingRow>> SummaryAsync(IDbSession session, string section, string? payor, string? health)
        {
            Calls.Add($"summary {section} {payor} {health}");
            return Task.FromResult(new List<OutstandingRow> { new() { PayorCode = "SH", Claims = 3, Amount = 175m } });
        }
    }

    private static OutstandingBordxService Service(FakeRepository repository) => new(new FakeSessions(), new FakePayors(), repository);

    [Fact]
    public void A_detail_listing_of_one_payor_needs_the_payor_but_a_summary_or_an_all_does_not()
    {
        var validator = new OutstandingFilterValidator();
        Assert.Equal("Please Enter Payor Code.", validator.FirstError(new OutstandingFilter()));
        Assert.Null(validator.FirstError(new OutstandingFilter { Payor = "SH - SIHAT" }));
        Assert.Null(validator.FirstError(new OutstandingFilter { Scope = "allSihat" }));
        Assert.Null(validator.FirstError(new OutstandingFilter { Mode = "summaryN" }));
        Assert.NotNull(validator.FirstError(new OutstandingFilter { Mode = "weekly", Payor = "SH" }));
    }

    [Theory]
    [InlineData(2021, 6, 1, 2021, 6, 1, 1)]
    [InlineData(2021, 6, 1, 2021, 5, 20, 13)]
    [InlineData(2021, 6, 1, 2021, 5, 19, 14)]
    [InlineData(2021, 6, 1, 2021, 5, 18, 15)]
    [InlineData(2021, 6, 1, 2021, 6, 2, 1)]
    public void The_days_outstanding_count_the_day_it_was_sent_and_a_zero_shows_as_one(int ay, int am, int ad, int sy, int sm, int sd, int expected) =>
        Assert.Equal(expected, OutstandingBordxService.DaysOutstanding(new DateTime(ay, am, ad), new DateTime(sy, sm, sd)));

    [Fact]
    public async Task A_sihat_payor_gets_two_lists_split_at_fourteen_days_and_a_total_due_that_adds_them()
    {
        var repository = new FakeRepository();
        var result = await Service(repository).SearchAsync(new OutstandingFilter { Payor = "sh - SIHAT", AsAt = new DateTime(2021, 6, 1) });
        Assert.Equal("S", result.Kind);
        Assert.Equal(["detail payor SH ", "detail mnrb SH "], repository.Calls);
        var list = result.Sections[0];
        Assert.Equal(("Bordx with Payor", 175m, 75m, 100m), (list.Title, list.SubTotal, list.Within14, list.Over14));
        Assert.Equal([32, 13, 1], list.Rows.Select(r => r.Days!.Value));
        Assert.Equal([null, (decimal?)50m, 25m], list.Rows.Select(r => r.Within14));
        Assert.Equal([100m, null, null], list.Rows.Select(r => r.Over14));
        Assert.Equal(350m, result.TotalDue);
        Assert.Equal("Not Paid by MNRB", result.Sections[1].Title);
    }

    [Fact]
    public async Task A_non_sihat_payor_gets_one_list_and_no_total_due_and_the_all_scopes_filter_on_the_health_code()
    {
        var repository = new FakeRepository();
        var one = await Service(repository).SearchAsync(new OutstandingFilter { Payor = "AB", AsAt = new DateTime(2021, 6, 1) });
        Assert.Equal(("N", (decimal?)null), (one.Kind, one.TotalDue));
        Assert.Equal(["detail nonSihat AB "], repository.Calls);
        repository.Calls.Clear();
        await Service(repository).SearchAsync(new OutstandingFilter { Scope = "allNonSihat", Payor = "ignored" });
        await Service(repository).SearchAsync(new OutstandingFilter { Scope = "allSihat" });
        Assert.Equal(["detail nonSihat  N", "detail payor  S", "detail mnrb  S"], repository.Calls);
    }

    [Fact]
    public async Task A_summary_lists_the_payors_with_their_claims_and_amounts_and_an_unknown_payor_is_not_found()
    {
        var repository = new FakeRepository();
        var result = await Service(repository).SearchAsync(new OutstandingFilter { Mode = "summaryS" });
        Assert.Equal(["summary payor  S", "summary mnrb  S"], repository.Calls);
        Assert.Equal((1, 3, 175m, (int?)null), (result.Sections[0].Rows[0].Item, result.Sections[0].Rows[0].Claims, result.Sections[0].SubTotal, result.Sections[0].Rows[0].Days));
        var failure = await Assert.ThrowsAsync<NotFoundException>(() => Service(repository).SearchAsync(new OutstandingFilter { Payor = "ZZ" }));
        Assert.Equal("Payor not found", failure.Message);
    }
}

/// <summary>Outstanding Bordx against the database. Nothing has been sent in the development data, so the rows are made by changing bordx inside a session that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class OutstandingBordxDatabaseTests
{
    private static OutstandingBordxRepository Repository => new();

    private static async Task<int> Count(IDbSession s, string sql, object? args = null) => await s.Connection.ExecuteScalarAsync<int>(sql, args, s.Transaction);

    [Fact]
    public async Task Nothing_is_outstanding_in_the_data_and_bordx_that_are_sent_appear_in_the_right_list_and_nothing_survives_the_rollback()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var before = await Count(read, "SELECT CHECKSUM_AGG(CHECKSUM(BordxRefNo, BordxPAYToStatus, BordxPAYDOCFRStatus, BordxPAYFRStatus, BordxPAYSentDate, BordxMNRBToStatus, BordxMNRBSentDate)) FROM genisis.WS_Bordx");
        Assert.Empty(await Repository.DetailAsync(read, "payor", null, null));
        Assert.Empty(await Repository.SummaryAsync(read, "nonSihat", null, "N"));

        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var bordx = (await write.Connection.QueryAsync<(string No, string Payor)>("SELECT TOP (4) RTRIM(BordxRefNo), RTRIM(PAYCode) FROM genisis.WS_OutstandingBordx ORDER BY BordxRefNo DESC", transaction: write.Transaction)).ToList();
            Assert.Equal(4, bordx.Count);
            var sent = new DateTime(2026, 9, 1);
            // 0: sent, documents not back; 1: sent, documents back and fully paid; 2: sent, partly paid; 3: sent to MNRB, no payment status
            await write.Connection.ExecuteAsync("UPDATE genisis.WS_Bordx SET BordxPAYToStatus = '1', BordxPAYSentDate = @sent, BordxPAYDOCFRStatus = '0', BordxPAYFRStatus = NULL WHERE BordxRefNo = @no", new { sent, no = bordx[0].No }, write.Transaction);
            await write.Connection.ExecuteAsync("UPDATE genisis.WS_Bordx SET BordxPAYToStatus = '1', BordxPAYSentDate = @sent, BordxPAYDOCFRStatus = '1', BordxPAYFRStatus = 'F' WHERE BordxRefNo = @no", new { sent, no = bordx[1].No }, write.Transaction);
            await write.Connection.ExecuteAsync("UPDATE genisis.WS_Bordx SET BordxPAYToStatus = '1', BordxPAYSentDate = @sent, BordxPAYDOCFRStatus = '1', BordxPAYFRStatus = 'P' WHERE BordxRefNo = @no", new { sent, no = bordx[2].No }, write.Transaction);
            await write.Connection.ExecuteAsync("UPDATE genisis.WS_Bordx SET BordxMNRBToStatus = '1', BordxMNRBSentDate = @sent, BordxMNRBFRStatus = NULL WHERE BordxRefNo = @no", new { sent, no = bordx[3].No }, write.Transaction);

            var withPayor = await Repository.DetailAsync(write, "payor", null, null);
            Assert.Contains(bordx[0].No, withPayor.Select(r => r.BordxNo));
            Assert.DoesNotContain(bordx[1].No, withPayor.Select(r => r.BordxNo));
            Assert.Equal(sent, withPayor.Single(r => r.BordxNo == bordx[0].No).SentDate);
            Assert.Equal(await Count(write, "SELECT COUNT(*) FROM genisis.WS_OutstandingBordx WHERE BordxPAYToStatus = 1 AND BordxPAYDOCFRStatus = 0"), withPayor.Count);

            var nonSihat = await Repository.DetailAsync(write, "nonSihat", bordx[2].Payor, "N");
            Assert.Contains(bordx[2].No, nonSihat.Select(r => r.BordxNo));
            Assert.DoesNotContain(bordx[1].No, nonSihat.Select(r => r.BordxNo));
            Assert.All(nonSihat, r => Assert.Equal(bordx[2].Payor, r.PayorCode));

            var mnrb = await Repository.DetailAsync(write, "mnrb", bordx[3].Payor, null);
            Assert.Equal([bordx[3].No], mnrb.Select(r => r.BordxNo));
            Assert.Empty(await Repository.DetailAsync(write, "mnrb", bordx[3].Payor, "S"));

            var summary = await Repository.SummaryAsync(write, "payor", bordx[0].Payor, null);
            var line = Assert.Single(summary);
            Assert.Equal((await write.Connection.ExecuteScalarAsync<decimal>("SELECT SUM(BordxClaimAmt) FROM genisis.WS_OutstandingBordx WHERE PAYCode = @p AND BordxPAYToStatus = 1 AND BordxPAYDOCFRStatus = 0", new { p = bordx[0].Payor }, write.Transaction), await Count(write, "SELECT SUM(BordxNoClaims) FROM genisis.WS_OutstandingBordx WHERE PAYCode = @p AND BordxPAYToStatus = 1 AND BordxPAYDOCFRStatus = 0", new { p = bordx[0].Payor })), (line.Amount!.Value, line.Claims!.Value));
            Assert.Equal(bordx[0].Payor, line.PayorCode);
        }

        Assert.Equal(before, await Count(read, "SELECT CHECKSUM_AGG(CHECKSUM(BordxRefNo, BordxPAYToStatus, BordxPAYDOCFRStatus, BordxPAYFRStatus, BordxPAYSentDate, BordxMNRBToStatus, BordxMNRBSentDate)) FROM genisis.WS_Bordx"));
    }
}
