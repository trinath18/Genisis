using Dapper;
using Genisis.Api.ClaimBordx.Delivery;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Claim Bordx > Bor Delivery: the validators and the service, against a fake repository.</summary>
public class BordxDeliveryRulesTests
{
    private sealed class FakeRepository(Dictionary<string, int>? results = null) : IBordxDeliveryRepository
    {
        public List<(char Kind, string No, DateTime Date, string User)> Sent { get; } = [];

        public Task<List<BordxDeliveryRow>> SearchAsync(IDbSession session, char kind, BordxDeliveryFilter filter) =>
            Task.FromResult(Enumerable.Range(0, 5002).Select((i) => new BordxDeliveryRow { BordxNo = "N" + i }).ToList());

        public Task<int> SendAsync(IDbSession session, char kind, string bordxNo, DateTime date, string userCode)
        {
            Sent.Add((kind, bordxNo, date, userCode));
            return Task.FromResult(results?.GetValueOrDefault(bordxNo) ?? StoredProcedures.Ok);
        }
    }

    private static BordxDeliveryService Service(FakeSessions sessions, FakeRepository repository) => new(sessions, repository, NullLogger<BordxDeliveryService>.Instance);

    [Fact]
    public void A_scan_needs_a_payor_and_then_a_bordx_number_and_a_range_needs_neither()
    {
        var validator = new BordxDeliveryFilterValidator();
        Assert.Equal("Please select a Payor", validator.FirstError(new BordxDeliveryFilter { UseScan = true }));
        Assert.Equal("Please Enter Bordx No", validator.FirstError(new BordxDeliveryFilter { UseScan = true, Payor = "AB - A PAYOR" }));
        Assert.Null(validator.FirstError(new BordxDeliveryFilter { UseScan = true, Payor = "AB", BordxFrom = "NAB0000001" }));
        Assert.Null(validator.FirstError(new BordxDeliveryFilter()));
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("1", true)]
    [InlineData("2", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("3", false)]
    public void Only_the_three_statuses_of_the_drop_down_are_accepted(string? status, bool valid) =>
        Assert.Equal(valid, new BordxDeliveryFilterValidator().FirstError(new BordxDeliveryFilter { Status = status }) is null);

    [Fact]
    public void The_update_checks_run_in_the_desktops_order_something_ticked_and_then_the_date()
    {
        var validator = new BordxDeliveryRequestValidator();
        Assert.Equal("Please select at least one record to be updated!", validator.FirstError(new BordxDeliveryRequest()));
        Assert.Equal("Please select at least one record to be updated!", validator.FirstError(new BordxDeliveryRequest { BordxNos = [" ", ""] }));
        Assert.Equal("Please Enter in date", validator.FirstError(new BordxDeliveryRequest { BordxNos = ["N1"] }));
        Assert.Null(validator.FirstError(new BordxDeliveryRequest { BordxNos = ["N1"], Date = new DateTime(2026, 10, 5) }));
    }

    [Theory]
    [InlineData(BordxDeliveryKinds.ToPayor, 'P', "to payor")]
    [InlineData(BordxDeliveryKinds.ToAccounts, 'A', "to accounts")]
    [InlineData(BordxDeliveryKinds.FromPayor, 'R', "as returned from the payor")]
    [InlineData(BordxDeliveryKinds.Approval, 'V', "as approved")]
    public void The_route_names_map_to_the_codes_the_procedures_take(string kind, char code, string label) => Assert.Equal((code, label), (BordxDeliveryKinds.Code(kind), BordxDeliveryKinds.Label(kind)));

    [Fact]
    public async Task A_search_returns_at_most_five_thousand_rows_and_says_when_it_cut_the_list()
    {
        var result = await Service(new FakeSessions(), new FakeRepository()).SearchAsync(BordxDeliveryKinds.ToPayor, new BordxDeliveryFilter());
        Assert.Equal((5000, true), (result.Rows.Count, result.Truncated));
    }

    [Fact]
    public async Task The_ticked_bordx_are_updated_once_each_in_one_transaction_that_is_committed()
    {
        var sessions = new FakeSessions();
        var repository = new FakeRepository();
        var updated = await Service(sessions, repository).SendAsync(BordxDeliveryKinds.ToAccounts, new BordxDeliveryRequest { Date = new DateTime(2026, 10, 5, 13, 0, 0), BordxNos = ["N1", " N1 ", "n1", "N2", ""] }, "USER1");
        Assert.Equal(2, updated);
        Assert.Equal([('A', "N1", new DateTime(2026, 10, 5), "USER1"), ('A', "N2", new DateTime(2026, 10, 5), "USER1")], repository.Sent);
        Assert.True(sessions.Last.Committed);
    }

    [Theory]
    [InlineData(StoredProcedures.NotFound, "Bordx N2 was not found. Search again.")]
    [InlineData(StoredProcedures.Conflict, "N2: Record has been Updated!")]
    public async Task A_bordx_that_is_gone_or_already_sent_stops_the_whole_update_and_nothing_is_saved(int result, string message)
    {
        var sessions = new FakeSessions();
        var failure = await Assert.ThrowsAsync<BusinessRuleException>(() => Service(sessions, new FakeRepository(new() { ["N2"] = result }))
            .SendAsync(BordxDeliveryKinds.ToPayor, new BordxDeliveryRequest { Date = new DateTime(2026, 10, 5), BordxNos = ["N1", "N2", "N3"] }, "USER1"));
        Assert.Equal(message, failure.Message);
        Assert.False(sessions.Last.Committed);
        Assert.True(sessions.Last.RolledBack);
    }
}

/// <summary>Claim Bordx > Bor Delivery against the database. Every write is made in a session that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class BordxDeliveryDatabaseTests
{
    private static BordxDeliveryRepository Repository => new();

    private static async Task<(string Payor, string From, string To)> APayorWithBordxAsync(IDbSession session)
    {
        var payor = await session.Connection.QueryFirstAsync<string>("SELECT TOP (1) RTRIM(BordxPAYCode) FROM genisis.WS_Bordx GROUP BY BordxPAYCode HAVING COUNT(*) BETWEEN 40 AND 4000 ORDER BY COUNT(*) DESC", transaction: session.Transaction);
        var range = await session.Connection.QueryFirstAsync<(string, string)>("SELECT MIN(RTRIM(BordxRefNo)), MAX(RTRIM(BordxRefNo)) FROM genisis.WS_Bordx WHERE BordxPAYCode = @payor", new { payor }, session.Transaction);
        return (payor, range.Item1, range.Item2);
    }

    [Fact]
    public async Task The_search_gives_what_the_table_holds_for_each_criterion()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var (payor, from, to) = await APayorWithBordxAsync(session);
        async Task<int> Count(string where, object? args = null) => await session.Connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM genisis.WS_Bordx WHERE {where}", args, session.Transaction);

        foreach (var kind in new[] { 'P', 'A' })
        {
            var all = await Repository.SearchAsync(session, kind, new BordxDeliveryFilter { Payor = payor });
            Assert.Equal(await Count("BordxPAYCode = @payor", new { payor }), all.Count);
            Assert.All(all, (r) => Assert.Equal(payor, r.Payor));
            Assert.Equal(all.OrderBy((r) => r.BordxNo, StringComparer.Ordinal).Select((r) => r.BordxNo), all.Select((r) => r.BordxNo));

            var scan = await Repository.SearchAsync(session, kind, new BordxDeliveryFilter { UseScan = true, Payor = payor, BordxFrom = from, BordxTo = "ignored in a scan" });
            Assert.Equal(await Count("BordxPAYCode = @payor AND BordxRefNo = @from", new { payor, from }), scan.Count);

            var range = await Repository.SearchAsync(session, kind, new BordxDeliveryFilter { Payor = payor, BordxFrom = from, BordxTo = to });
            Assert.Equal(all.Count, range.Count);

            var dated = await Repository.SearchAsync(session, kind, new BordxDeliveryFilter { Payor = payor, DateFrom = new DateTime(2021, 4, 1), DateTo = new DateTime(2021, 4, 30) });
            Assert.Equal(await Count("BordxPAYCode = @payor AND BordxDate >= '20210401' AND BordxDate <= '20210430'", new { payor }), dated.Count);

            var big = await Repository.SearchAsync(session, kind, new BordxDeliveryFilter { Payor = payor, AmountFrom = 1000m, AmountTo = 5000m });
            Assert.Equal(await Count("BordxPAYCode = @payor AND BordxClaimAmt >= 1000 AND BordxClaimAmt <= 5000", new { payor }), big.Count);
            Assert.All(big, (r) => Assert.InRange(r.Amount ?? 0m, 1000m, 5000m));

            Assert.Equal(all.Count, (await Repository.SearchAsync(session, kind, new BordxDeliveryFilter { Payor = payor, Status = "0" })).Count);
            Assert.Empty(await Repository.SearchAsync(session, kind, new BordxDeliveryFilter { Payor = payor, Status = "1" }));
            Assert.Equal(all.Count, (await Repository.SearchAsync(session, kind, new BordxDeliveryFilter { Payor = payor, Status = "2" })).Count);
        }
    }

    [Fact]
    public async Task The_period_of_the_payor_screen_and_the_date_sent_of_the_accounts_screen_filter_only_their_own_screen()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().BeginAsync();
        var (payor, from, _) = await APayorWithBordxAsync(session);
        await session.Connection.ExecuteAsync("UPDATE genisis.WS_Bordx SET PAYToACPeriodMth = '3', PAYToACPeriodYR = '2021', BordxSendACDate = '20210410' WHERE BordxRefNo = @from", new { from }, session.Transaction);

        var byPeriod = new BordxDeliveryFilter { Payor = payor, MonthFrom = "3", MonthTo = "3", YearFrom = "2021", YearTo = "2021" };
        Assert.Equal([from], (await Repository.SearchAsync(session, 'P', byPeriod)).Select((r) => r.BordxNo));
        Assert.True((await Repository.SearchAsync(session, 'A', byPeriod)).Count > 1);

        var bySent = new BordxDeliveryFilter { Payor = payor, SentFrom = new DateTime(2021, 4, 10), SentTo = new DateTime(2021, 4, 10) };
        Assert.Equal([from], (await Repository.SearchAsync(session, 'A', bySent)).Select((r) => r.BordxNo));
        Assert.True((await Repository.SearchAsync(session, 'P', bySent)).Count > 1);
    }

    [Fact]
    public async Task Returned_from_payor_lists_only_the_bordx_already_sent_to_accounts_and_the_period_filters_it_like_send_to_payor()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().BeginAsync();
        var (payor, from, to) = await APayorWithBordxAsync(session);
        Assert.Empty(await Repository.SearchAsync(session, 'R', new BordxDeliveryFilter { Payor = payor }));

        await session.Connection.ExecuteAsync("UPDATE genisis.WS_Bordx SET BordxSendtoACStatus = 1, PAYToACPeriodMth = '3', PAYToACPeriodYR = '2021' WHERE BordxRefNo IN (@from, @to)", new { from, to }, session.Transaction);
        var listed = await Repository.SearchAsync(session, 'R', new BordxDeliveryFilter { Payor = payor });
        Assert.Equal(new[] { from, to }.Distinct().Order(StringComparer.Ordinal), listed.Select((r) => r.BordxNo));
        Assert.All(listed, (r) => Assert.False(r.Sent));
        Assert.Equal(listed.Count, (await Repository.SearchAsync(session, 'R', new BordxDeliveryFilter { Payor = payor, MonthFrom = "3", MonthTo = "3", YearFrom = "2021", YearTo = "2021" })).Count);
        Assert.Empty(await Repository.SearchAsync(session, 'R', new BordxDeliveryFilter { Payor = payor, Status = "1" }));
    }

    [Fact]
    public async Task Returning_a_bordx_sets_its_three_columns_and_approves_only_the_claims_that_were_not_approved_and_a_second_return_is_refused()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var bordx = await read.Connection.QueryFirstAsync<string>("SELECT TOP (1) RTRIM(b.BordxRefNo) FROM genisis.WS_Bordx b WHERE EXISTS (SELECT 1 FROM genisis.WS_Claim c WHERE c.BordxRefNo = b.BordxRefNo) ORDER BY b.BordxRefNo", transaction: read.Transaction);
        var before = await read.Connection.ExecuteScalarAsync<int>("SELECT CHECKSUM_AGG(CHECKSUM(BordxRefNo, BordxPAYDOCFRStatus, BordxPAYRecDocDate, BordxPAYRecDocBy)) FROM genisis.WS_Bordx", transaction: read.Transaction, commandTimeout: 120);
        var claimsBefore = await read.Connection.ExecuteScalarAsync<int>("SELECT CHECKSUM_AGG(CHECKSUM(CASNumber, ClaimVersion, CLMAppStatus)) FROM genisis.WS_Claim", transaction: read.Transaction, commandTimeout: 120);

        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var claims = (await write.Connection.QueryAsync<(string Cas, int Version)>("SELECT RTRIM(CASNumber), ClaimVersion FROM genisis.WS_Claim WHERE BordxRefNo = @bordx ORDER BY CASNumber, ClaimVersion", new { bordx }, write.Transaction)).ToList();
            Assert.True(claims.Count >= 1);
            await write.Connection.ExecuteAsync("UPDATE genisis.WS_Claim SET CLMAppStatus = NULL WHERE BordxRefNo = @bordx AND CASNumber = @cas AND ClaimVersion = @version", new { bordx, cas = claims[0].Cas, version = claims[0].Version }, write.Transaction);
            if (claims.Count > 1)
                await write.Connection.ExecuteAsync("UPDATE genisis.WS_Claim SET CLMAppStatus = 'R' WHERE BordxRefNo = @bordx AND CASNumber = @cas AND ClaimVersion = @version", new { bordx, cas = claims[1].Cas, version = claims[1].Version }, write.Transaction);

            var date = new DateTime(2026, 10, 5);
            Assert.Equal(StoredProcedures.Ok, await Repository.SendAsync(write, 'R', bordx, date, "ZZTEST"));
            var row = await write.Connection.QueryFirstAsync("SELECT BordxPAYDOCFRStatus, BordxPAYRecDocDate, BordxPAYRecDocBy, BordxPAYToStatus, BordxSendtoACStatus FROM genisis.WS_Bordx WHERE BordxRefNo = @bordx", new { bordx }, write.Transaction);
            Assert.Equal(("1", date, "ZZTEST", "0", false), (((string)row.BordxPAYDOCFRStatus).Trim(), (DateTime)row.BordxPAYRecDocDate, ((string)row.BordxPAYRecDocBy).Trim(), ((string)row.BordxPAYToStatus).Trim(), (bool)row.BordxSendtoACStatus));

            Assert.Equal("Y", (await write.Connection.ExecuteScalarAsync<string>("SELECT RTRIM(CLMAppStatus) FROM genisis.WS_Claim WHERE BordxRefNo = @bordx AND CASNumber = @cas AND ClaimVersion = @version", new { bordx, cas = claims[0].Cas, version = claims[0].Version }, write.Transaction)));
            if (claims.Count > 1)
                Assert.Equal("R", (await write.Connection.ExecuteScalarAsync<string>("SELECT RTRIM(CLMAppStatus) FROM genisis.WS_Claim WHERE BordxRefNo = @bordx AND CASNumber = @cas AND ClaimVersion = @version", new { bordx, cas = claims[1].Cas, version = claims[1].Version }, write.Transaction)));
            Assert.Equal(StoredProcedures.Conflict, await Repository.SendAsync(write, 'R', bordx, date, "ZZTEST"));
        }

        Assert.Equal(before, await read.Connection.ExecuteScalarAsync<int>("SELECT CHECKSUM_AGG(CHECKSUM(BordxRefNo, BordxPAYDOCFRStatus, BordxPAYRecDocDate, BordxPAYRecDocBy)) FROM genisis.WS_Bordx", transaction: read.Transaction, commandTimeout: 120));
        Assert.Equal(claimsBefore, await read.Connection.ExecuteScalarAsync<int>("SELECT CHECKSUM_AGG(CHECKSUM(CASNumber, ClaimVersion, CLMAppStatus)) FROM genisis.WS_Claim", transaction: read.Transaction, commandTimeout: 120));
    }

    [Fact]
    public async Task Approving_a_bordx_sets_its_three_approval_columns_lists_it_as_approved_and_a_second_approval_is_refused()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var (payor, from, to) = await APayorWithBordxAsync(read);
        var all = (await Repository.SearchAsync(read, 'V', new BordxDeliveryFilter { Payor = payor })).Count;
        Assert.True(all > 1);
        var before = await read.Connection.ExecuteScalarAsync<int>("SELECT CHECKSUM_AGG(CHECKSUM(BordxRefNo, BordxAppStatus, BordxAppDate, BordxAppBy)) FROM genisis.WS_Bordx", transaction: read.Transaction, commandTimeout: 120);

        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var date = new DateTime(2026, 10, 7);
            Assert.Equal(StoredProcedures.Ok, await Repository.SendAsync(write, 'V', from, date, "ZZTEST"));
            var row = await write.Connection.QueryFirstAsync("SELECT BordxAppStatus, BordxAppDate, BordxAppBy, BordxPAYToStatus, BordxSendtoACStatus FROM genisis.WS_Bordx WHERE BordxRefNo = @from", new { from }, write.Transaction);
            Assert.Equal((true, date, "ZZTEST", "0", false), ((bool)row.BordxAppStatus, (DateTime)row.BordxAppDate, ((string)row.BordxAppBy).Trim(), ((string)row.BordxPAYToStatus).Trim(), (bool)row.BordxSendtoACStatus));
            Assert.Equal(StoredProcedures.Conflict, await Repository.SendAsync(write, 'V', from, date, "ZZTEST"));

            var approved = await Repository.SearchAsync(write, 'V', new BordxDeliveryFilter { Payor = payor, Status = "1" });
            var row0 = Assert.Single(approved);
            Assert.Equal((from, true, date, "ZZTEST"), (row0.BordxNo, row0.Sent, row0.SentOn, row0.SentBy));
            Assert.Equal(all - 1, (await Repository.SearchAsync(write, 'V', new BordxDeliveryFilter { Payor = payor, Status = "0" })).Count);
            Assert.Equal(all, (await Repository.SearchAsync(write, 'V', new BordxDeliveryFilter { Payor = payor, Status = "2" })).Count);
            Assert.NotEqual(to, from);
        }

        Assert.Equal(before, await read.Connection.ExecuteScalarAsync<int>("SELECT CHECKSUM_AGG(CHECKSUM(BordxRefNo, BordxAppStatus, BordxAppDate, BordxAppBy)) FROM genisis.WS_Bordx", transaction: read.Transaction, commandTimeout: 120));
    }

    [Fact]
    public async Task Sending_a_bordx_sets_its_own_three_columns_and_a_second_send_is_refused_and_a_missing_bordx_is_not_found()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var (payor, from, to) = await APayorWithBordxAsync(read);
        var before = await read.Connection.ExecuteScalarAsync<int>("SELECT CHECKSUM_AGG(CHECKSUM(BordxRefNo, BordxPAYToStatus, BordxPAYSentDate, BordxPAYSentBy, BordxSendtoACStatus, BordxSendACDate, BordxSendACBy)) FROM genisis.WS_Bordx", transaction: read.Transaction, commandTimeout: 120);

        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var date = new DateTime(2026, 10, 5, 15, 30, 0);
            Assert.Equal(StoredProcedures.Ok, await Repository.SendAsync(write, 'P', from, date, "ZZTEST"));
            var row = await write.Connection.QueryFirstAsync("SELECT BordxPAYToStatus, BordxPAYSentDate, BordxPAYSentBy, BordxSendtoACStatus, BordxSendACDate FROM genisis.WS_Bordx WHERE BordxRefNo = @from", new { from }, write.Transaction);
            Assert.Equal(("1", date.Date, "ZZTEST", false, null), (((string)row.BordxPAYToStatus).Trim(), (DateTime)row.BordxPAYSentDate, ((string)row.BordxPAYSentBy).Trim(), (bool)row.BordxSendtoACStatus, (DateTime?)row.BordxSendACDate));
            Assert.Equal(StoredProcedures.Conflict, await Repository.SendAsync(write, 'P', from, date, "ZZTEST"));

            Assert.Equal(StoredProcedures.Ok, await Repository.SendAsync(write, 'A', to, date, "ZZTEST2"));
            var other = await write.Connection.QueryFirstAsync("SELECT BordxPAYToStatus, BordxSendtoACStatus, BordxSendACDate, BordxSendACBy FROM genisis.WS_Bordx WHERE BordxRefNo = @to", new { to }, write.Transaction);
            Assert.Equal(("0", true, date.Date, "ZZTEST2"), (((string)other.BordxPAYToStatus).Trim(), (bool)other.BordxSendtoACStatus, (DateTime)other.BordxSendACDate, ((string)other.BordxSendACBy).Trim()));
            Assert.Equal(StoredProcedures.Conflict, await Repository.SendAsync(write, 'A', to, date, "ZZTEST2"));

            Assert.Equal(StoredProcedures.NotFound, await Repository.SendAsync(write, 'P', "NO SUCH BORDX", date, "ZZTEST"));
            Assert.Single(await Repository.SearchAsync(write, 'P', new BordxDeliveryFilter { Payor = payor, Status = "1" }));
        }

        Assert.Equal(before, await read.Connection.ExecuteScalarAsync<int>("SELECT CHECKSUM_AGG(CHECKSUM(BordxRefNo, BordxPAYToStatus, BordxPAYSentDate, BordxPAYSentBy, BordxSendtoACStatus, BordxSendACDate, BordxSendACBy)) FROM genisis.WS_Bordx", transaction: read.Transaction, commandTimeout: 120));
    }
}
