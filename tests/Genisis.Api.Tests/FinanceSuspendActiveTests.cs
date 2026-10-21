using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Finance;
using Genisis.Api.Finance.SuspendActive;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Finance &gt; Suspend / Active: rules, checks and the unit of work.</summary>
public class FinanceSuspendActiveTests
{
    private sealed class FakeRepository(int stopResult = 0, int reissueResult = 0) : ISuspendActiveRepository
    {
        public Task<List<VoucherRow>> SearchAsync(IDbSession session, VoucherFilter filter) => Task.FromResult(new List<VoucherRow> { new() { Id = 1 } });
        public Task<int> StopAsync(IDbSession session, int id, StopVoucherRequest request, string userCode) => Task.FromResult(stopResult);

        public Task<(int Result, string? PvNo)> ReissueAsync(IDbSession session, int id, ReissueVoucherRequest request, string userCode) =>
            Task.FromResult<(int, string?)>(reissueResult == 0 ? (0, "CC00112652") : (reissueResult, null));
    }

    private static SuspendActiveService Service(FakeSessions sessions, FakeRepository repository) => new(sessions, repository, NullLogger<SuspendActiveService>.Instance);

    [Fact]
    public void Only_an_active_payment_can_be_stopped_and_only_a_stopped_one_re_issued()
    {
        var active = new VoucherRow { PvNo = "CB1", Status = "A", Amount = 117.5m };
        Assert.Null(SuspendActiveService.StopProblem(active));
        Assert.Null(SuspendActiveService.StopProblem(active with { Status = "R" }));
        Assert.Equal("PV CB1 is not an active payment, so it cannot be stopped.", SuspendActiveService.StopProblem(active with { Status = "C", Amount = -117.5m }));
        Assert.Equal("PV CB1 has already been stopped (C).", SuspendActiveService.StopProblem(active with { StoppedAs = "C" }));

        Assert.Equal("PV CB1 has not been stopped. Cancel or expire it first.", SuspendActiveService.ReissueProblem(active));
        Assert.Null(SuspendActiveService.ReissueProblem(active with { StoppedAs = "E" }));
        Assert.Equal("PV CB1 has already been re-issued as CB2.", SuspendActiveService.ReissueProblem(active with { StoppedAs = "C", ReissuedAs = "CB2" }));
    }

    [Fact]
    public void The_checks_follow_the_order_and_messages()
    {
        Assert.Equal("Please enter at least one search criterion.", new VoucherFilterValidator().FirstError(new VoucherFilter()));
        Assert.Equal("Select a valid PV status.", new VoucherFilterValidator().FirstError(new VoucherFilter { Status = "X" }));
        Assert.Equal("Please select Cancel or Expired.", new StopVoucherRequestValidator().FirstError(new StopVoucherRequest { Status = "A" }));
        Assert.Equal("Please Enter Date", new StopVoucherRequestValidator().FirstError(new StopVoucherRequest { Status = "C" }));
        var reissue = new ReissueVoucherRequestValidator();
        Assert.Equal("Please Enter Date", reissue.FirstError(new ReissueVoucherRequest()));
        Assert.Equal("Please Enter Cheque No / TT Reference", reissue.FirstError(new ReissueVoucherRequest { ActionDate = DateTime.Today }));
        Assert.Equal("Select a valid payment mode.", reissue.FirstError(new ReissueVoucherRequest { ActionDate = DateTime.Today, ChequeNo = "1" }));
        Assert.Equal("Please Enter Remark", reissue.FirstError(new ReissueVoucherRequest { ActionDate = DateTime.Today, ChequeNo = "1", PaymentMode = "ONLINE" }));
    }

    [Fact]
    public async Task Stop_and_reissue_commit_and_report()
    {
        var sessions = new FakeSessions();
        Assert.Equal("Payment cancelled.", await Service(sessions, new FakeRepository()).StopAsync(1, new StopVoucherRequest { Status = "C", ActionDate = DateTime.Today }, "TESTER"));
        Assert.True(sessions.Last.Committed);
        Assert.Equal("Payment marked as expired.", await Service(new FakeSessions(), new FakeRepository()).StopAsync(1, new StopVoucherRequest { Status = "E", ActionDate = DateTime.Today }, "TESTER"));
        var (pv, message) = await Service(new FakeSessions(), new FakeRepository()).ReissueAsync(1, new ReissueVoucherRequest(), "TESTER");
        Assert.Equal(("CC00112652", "Payment re-issued as PV CC00112652."), (pv, message));
    }

    [Fact]
    public async Task Refused_writes_roll_back()
    {
        var sessions = new FakeSessions();
        await Assert.ThrowsAsync<FinanceException>(() => Service(sessions, new FakeRepository(stopResult: 2)).StopAsync(1, new StopVoucherRequest { Status = "C", ActionDate = DateTime.Today }, "TESTER"));
        Assert.True(sessions.Last.RolledBack);
        await Assert.ThrowsAsync<NotFoundException>(() => Service(new FakeSessions(), new FakeRepository(stopResult: 1)).StopAsync(1, new StopVoucherRequest { Status = "C", ActionDate = DateTime.Today }, "TESTER"));
        var reissue = new FakeSessions();
        await Assert.ThrowsAsync<FinanceException>(() => Service(reissue, new FakeRepository(reissueResult: 2)).ReissueAsync(1, new ReissueVoucherRequest(), "TESTER"));
        Assert.True(reissue.Last.RolledBack);
    }
}

/// <summary>Finance &gt; Suspend / Active against the development databases; writes are rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class FinanceSuspendActiveDatabaseTests
{
    [Fact]
    public async Task A_payment_is_stopped_once_re_issued_once_under_a_new_pv_and_the_search_links_them()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        var repository = new SuspendActiveRepository();
        await using var session = await TestDb.Sessions().BeginAsync();
        var sample = await session.Connection.QueryFirstOrDefaultAsync<(int Id, string Pv, string Cas)>(
            """
            SELECT TOP 1 p.PVMBMIndex, RTRIM(p.PVNo), RTRIM(p.CASNumber) FROM genisis.Pay_Payment2MBM p
            WHERE p.PVStatus = 'A' AND p.PVChequeAmt > 0 AND LEFT(p.PVNo, 2) IN ('CB', 'CC', 'CG')
              AND NOT EXISTS (SELECT 1 FROM genisis.Pay_Payment2MBM s WHERE RTRIM(s.PVNo) = RTRIM(p.PVNo) AND s.PVStatus IN ('C', 'E'))
            ORDER BY p.PVMBMIndex DESC
            """, transaction: session.Transaction);
        if (sample.Pv is null) return;

        var stop = new StopVoucherRequest { Status = "C", ActionDate = new DateTime(2021, 8, 10), Remark = "STOP CHQ TEST" };
        Assert.Equal(StoredProcedures.Ok, await repository.StopAsync(session, sample.Id, stop, "TESTER"));
        Assert.Equal(StoredProcedures.Conflict, await repository.StopAsync(session, sample.Id, stop, "TESTER"));

        var reissue = new ReissueVoucherRequest { ActionDate = new DateTime(2021, 8, 11), ChequeNo = "11082021", PaymentMode = "ONLINE", Remark = "RE-ISSUE TEST" };
        var (result, newPv) = await repository.ReissueAsync(session, sample.Id, reissue, "TESTER");
        Assert.Equal(StoredProcedures.Ok, result);
        Assert.StartsWith(sample.Pv[..2], newPv);
        Assert.NotEqual(sample.Pv, newPv);
        Assert.Equal(StoredProcedures.Conflict, (await repository.ReissueAsync(session, sample.Id, reissue, "TESTER")).Result);

        var rows = await repository.SearchAsync(session, new VoucherFilter { CaseNo = sample.Cas });
        var original = Assert.Single(rows, r => r.Id == sample.Id);
        Assert.Equal(("C", newPv), (original.StoppedAs, original.ReissuedAs));
        Assert.Contains(rows, r => r.PvNo == sample.Pv && r.Status == "C" && r.Amount < 0);
        Assert.Contains(rows, r => r.PvNo == newPv && r.Status == "R" && r.Amount > 0 && r.ChequeNo == "11082021");
        Assert.All(rows, r => Assert.True(r.PayeeAccount is null || r.PayeeAccount.Length <= 4 || r.PayeeAccount.StartsWith("****")));
    }
}

/// <summary>Finance &gt; Suspend / Active over HTTP.</summary>
public class FinanceSuspendActiveApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Every_endpoint_needs_menu_right_12_and_writes_are_checked()
    {
        using var otherRights = factory.ClientWith("111111111110111111111111111111");
        Assert.Equal(HttpStatusCode.Forbidden, (await otherRights.GetAsync("/api/finance/suspend-active/lookups")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherRights.PostAsJsonAsync("/api/finance/suspend-active/1/stop", new { })).StatusCode);
        using var client = factory.ClientWith(ApiFactory.AllRights);
        var lookups = await client.GetFromJsonAsync<JsonElement>("/api/finance/suspend-active/lookups");
        Assert.Equal(4, lookups.GetProperty("statuses").GetArrayLength());
        var response = await client.PostAsJsonAsync("/api/finance/suspend-active/1/reissue", new { });
        Assert.Equal("Please Enter Date", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString());
    }
}
