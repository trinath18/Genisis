using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Payment.PvUpdate;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Payment > PV Update (Manual): the validators and the service, against a fake repository.</summary>
public class PvUpdateRulesTests
{
    private sealed class FakeRepository(int result) : IPvUpdateRepository
    {
        public Task<List<PvUpdateListRow>> FindAsync(IDbSession session, PvUpdateFilter filter, int max) => Task.FromResult(new List<PvUpdateListRow>());

        public Task<List<PvUpdateRow>> RowsAsync(IDbSession session, string source, string pvNo) =>
            Task.FromResult(pvNo == "CB1" ? new List<PvUpdateRow> { new() { Id = 1, PvNo = "CB1" } } : []);

        public Task<(int Result, int Rows)> SaveAsync(IDbSession session, PvUpdateRequest request, string userCode) => Task.FromResult((result, result == 0 ? 3 : 0));
    }

    private static PvUpdateRequest Valid() => new() { Source = "member", PvNo = "CC1", ChequeNo = "MBB1", ChequeDate = new DateTime(2021, 8, 7), PaymentMode = "CHEQUE" };

    private static PvUpdateService Service(int result, FakeSessions sessions) => new(sessions, new FakeRepository(result), NullLogger<PvUpdateService>.Instance);

    [Fact]
    public void Save_needs_a_pv_a_mode_a_cheque_and_its_date()
    {
        var validator = new PvUpdateRequestValidator();
        Assert.Null(validator.FirstError(Valid()));
        var r = Valid();
        r.ChequeNo = " ";
        Assert.Equal("Please enter the Cheque No.", validator.FirstError(r));
        r = Valid();
        r.ChequeDate = null;
        Assert.Equal("Please enter the Cheque Date.", validator.FirstError(r));
        r = Valid();
        r.Source = "payor";
        Assert.Equal("Select To Hospital or To Member.", validator.FirstError(r));
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, "PV CC1 not found.")]
    [InlineData(2, "PV CC1 has been posted to SUN and cannot be changed.")]
    [InlineData(3, "PV CC1 has been stopped (cancelled or expired) and cannot be changed.")]
    public async Task Save_commits_or_says_why_not(int result, string? message)
    {
        var sessions = new FakeSessions();
        if (message is null)
        {
            Assert.Equal(3, (await Service(result, sessions).SaveAsync(Valid(), "U")).Rows);
            Assert.True(sessions.Last.Committed);
            return;
        }

        var e = await Assert.ThrowsAnyAsync<BusinessRuleException>(() => Service(result, sessions).SaveAsync(Valid(), "U"));
        Assert.Equal(message, e.Message);
        Assert.False(sessions.Last.Committed);
    }

    [Fact]
    public async Task An_unknown_pv_is_not_found()
    {
        var service = Service(0, new FakeSessions());
        Assert.Single((await service.RowsAsync("hospital", " cb1 ")).Rows);
        await Assert.ThrowsAsync<NotFoundException>(() => service.RowsAsync("hospital", "CB2"));
    }
}

/// <summary>PV Update against the database; every write is rolled back.</summary>
public class PvUpdateDatabaseTests
{
    private static PvUpdateRepository Repository => new();

    [Fact]
    public async Task A_member_pv_is_found_changed_on_every_row_and_a_stopped_one_refused()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var pv = await read.Connection.ExecuteScalarAsync<string?>(
            "SELECT TOP 1 RTRIM(PVNo) FROM genisis.Pay_Payment2MBM m WHERE PVStatus = 'A' AND PVNo LIKE 'C[BCG]%' AND ISNULL(PVSUNNo, N'') = N'' AND NOT EXISTS (SELECT 1 FROM genisis.Pay_Payment2MBM s WHERE s.PVNo = m.PVNo AND s.PVStatus IN ('C', 'E')) ORDER BY PVMBMIndex DESC",
            transaction: read.Transaction);
        var stopped = await read.Connection.ExecuteScalarAsync<string?>("SELECT TOP 1 RTRIM(PVNo) FROM genisis.Pay_Payment2MBM WHERE PVStatus IN ('C', 'E')", transaction: read.Transaction);
        if (pv is null) return;
        var found = Assert.Single(await Repository.FindAsync(read, new PvUpdateFilter { Source = "member", PvNo = pv.ToLowerInvariant() }, 10));
        var rows = await Repository.RowsAsync(read, "member", pv);
        Assert.Equal(found.Claims, rows.Count);
        var before = rows[0];

        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var saved = await Repository.SaveAsync(write, new PvUpdateRequest
            {
                Source = "member", PvNo = pv, ChequeNo = "zz000001", ChequeDate = new DateTime(2021, 8, 9, 10, 0, 0), PaymentMode = "CHEQUE", PayTo = "zz payee", Bank = "zz bank", Account = "123", Remark = "ZZTEST",
            }, "ZZTEST");
            Assert.Equal((0, rows.Count), saved);
            var after = (await Repository.RowsAsync(write, "member", pv))[0];
            Assert.Equal(("ZZ000001", new DateTime(2021, 8, 9), "CHEQUE", "ZZ PAYEE", "ZZ BANK", "123", "ZZTEST"), (after.ChequeNo, after.ChequeDate, after.PaymentMode, after.PayTo, after.Bank, after.Account, after.Remark));
            Assert.Equal((before.Paid, before.Amount, before.PvDate, before.Status, before.AccMonth), (after.Paid, after.Amount, after.PvDate, after.Status, after.AccMonth));
            Assert.Null(await write.Connection.ExecuteScalarAsync<string?>("SELECT TOP 1 PVChequeStatus FROM genisis.Pay_Payment2MBM WHERE PVNo = @pv", new { pv }, write.Transaction));
            if (stopped is not null)
                Assert.Equal(3, (await Repository.SaveAsync(write, new PvUpdateRequest { Source = "member", PvNo = stopped, ChequeNo = "X1", ChequeDate = DateTime.Today, PaymentMode = "CHEQUE" }, "ZZTEST")).Result);
            Assert.Equal(1, (await Repository.SaveAsync(write, new PvUpdateRequest { Source = "hospital", PvNo = "ZZNOPE", ChequeNo = "X1", ChequeDate = DateTime.Today, PaymentMode = "ONLINE" }, "ZZTEST")).Result);
        }

        Assert.Equal(before, (await Repository.RowsAsync(read, "member", pv))[0]);
    }
}
