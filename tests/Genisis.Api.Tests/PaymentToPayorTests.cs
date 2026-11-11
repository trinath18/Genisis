using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Payment.ToHospital;
using Genisis.Api.Payment.ToPayor;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Payment > Payor: the validators and the service, against a fake repository.</summary>
public class PaymentToPayorRulesTests
{
    private sealed class FakeRepository : IPaymentToPayorRepository
    {
        public Dictionary<string, PaymentToPayorRow> Claims { get; } = [];

        public HashSet<string> Paid { get; } = [];

        public List<(PayorVoucher Voucher, PaymentToPayorRow Claim)> Inserted { get; } = [];

        private int seq = 112242;

        public Task<List<PaymentToPayorRow>> SearchAsync(IDbSession session, PaymentToPayorFilter filter, int max) => Task.FromResult(Claims.Values.ToList());

        public Task<PaymentToPayorRow?> ClaimAsync(IDbSession session, string caseNo, string version) => Task.FromResult(Claims.GetValueOrDefault($"{caseNo}-{version}"));

        public Task<bool> LockPaidAsync(IDbSession session, string caseNo, string version) => Task.FromResult(Paid.Contains($"{caseNo}-{version}"));

        public Task<string?> NextPvAsync(IDbSession session, string prefix) => Task.FromResult<string?>(prefix + (++seq).ToString("00000000"));

        public Task InsertAsync(IDbSession session, PayorVoucher voucher, DateTime pvDate, PaymentToPayorRow claim, string? remark, string userCode)
        {
            Inserted.Add((voucher, claim));
            return Task.CompletedTask;
        }

        public void Add(string caseNo, decimal amount, string? pvInd = "CB") =>
            Claims[caseNo + "-2"] = new PaymentToPayorRow { CaseNo = caseNo, ClaimVersion = "2", PayorCode = caseNo[..2], PayorName = caseNo[..2] + " BHD", PvInd = pvInd, Amount = amount };
    }

    private static PaymentToPayorRequest Request(params string[] cases) =>
        new() { PvDate = new DateTime(2009, 12, 8), ChequeNo = "CB002704", Claims = cases.Select(c => new PaymentClaimKey { CaseNo = c, ClaimVersion = "2" }).ToList() };

    private static PaymentToPayorService Service(FakeRepository repository, FakeSessions sessions) => new(sessions, repository, NullLogger<PaymentToPayorService>.Instance);

    [Fact]
    public void Generate_pv_needs_claims_a_date_and_a_cheque()
    {
        var validator = new PaymentToPayorRequestValidator();
        Assert.Null(validator.FirstError(Request("WT00925")));
        Assert.Equal("Please select the claims to pay.", validator.FirstError(Request()));
        var noCheque = Request("WT00925");
        noCheque.ChequeNo = " ";
        Assert.StartsWith("Please enter the Cheque No", validator.FirstError(noCheque));
        Assert.Equal("Close Date From cannot be later than Close Date To.",
            new PaymentToPayorFilterValidator().FirstError(new PaymentToPayorFilter { CloseFrom = new DateTime(2021, 2, 2), CloseTo = new DateTime(2021, 2, 1) }));
    }

    [Fact]
    public async Task One_pv_per_payor_with_the_cheques_numbered_on()
    {
        var repository = new FakeRepository();
        repository.Add("WT00925", 27.30m);
        repository.Add("WT00926", 27.30m);
        repository.Add("XI00001", 10m);
        var sessions = new FakeSessions();
        var done = await Service(repository, sessions).CreateAsync(Request("XI00001", "WT00925", "WT00926"), "ELSIE");
        Assert.True(sessions.Last.Committed);
        Assert.Equal([("CB00112243", "WT", 2, 54.60m, "CB002704"), ("CB00112244", "XI", 1, 10m, "CB002705")], done.Vouchers.Select(v => (v.PvNo, v.PayorCode, v.Claims, v.Amount, v.ChequeNo)));
        Assert.Equal(3, repository.Inserted.Count);
    }

    [Fact]
    public async Task A_claim_paid_back_not_reversed_or_without_a_prefix_stops_the_run()
    {
        var repository = new FakeRepository();
        repository.Add("WT00925", 27.30m);
        repository.Add("ZZ00001", 5m, pvInd: null);
        repository.Add("WT00927", 0m);
        repository.Paid.Add("WT00926-2");
        var service = Service(repository, new FakeSessions());
        Assert.Equal("Claim WT00926-2 has been paid back.", (await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(Request("WT00925", "WT00926"), "U"))).Message);
        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(Request("WT00999"), "U"));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(Request("ZZ00001"), "U"));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(Request("WT00927"), "U"));
        Assert.Empty(repository.Inserted);
    }
}

/// <summary>Payment > Payor against the database; every write is rolled back.</summary>
public class PaymentToPayorDatabaseTests
{
    private static PaymentToPayorRepository Repository => new();

    [Fact]
    public async Task The_search_lists_the_reversed_claims_not_paid_back_once_each()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var rows = await Repository.SearchAsync(session, new PaymentToPayorFilter(), 100000);
        var expected = await session.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM (SELECT CASNumber, ClaimVersion FROM genisis.Pay_ViewPaymentPayor WHERE PVPAYNo IS NULL GROUP BY CASNumber, ClaimVersion) x", transaction: session.Transaction);
        Assert.Equal(expected, rows.Count);
        Assert.All(rows, r => Assert.True(r.Amount > 0 && r.PvNo is null && r.PayorCode == r.CaseNo[..2]));
        var first = rows.FirstOrDefault();
        if (first is null) return;
        var byPayor = await Repository.SearchAsync(session, new PaymentToPayorFilter { Payor = first.PayorCode!.ToLowerInvariant() }, 100000);
        Assert.All(byPayor, r => Assert.Equal(first.PayorCode, r.PayorCode));
        Assert.Equal(first.Amount, (await Repository.ClaimAsync(session, first.CaseNo, first.ClaimVersion))?.Amount);
    }

    [Fact]
    public async Task A_pv_writes_a_row_per_claim_with_the_pv_total()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var claims = (await Repository.SearchAsync(read, new PaymentToPayorFilter(), 100000)).Where(r => r.PvInd == "CB").Take(2).ToList();
        if (claims.Count < 2) return;
        var before = await read.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.Pay_Payment2PAY", transaction: read.Transaction);

        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var pv = (await Repository.NextPvAsync(write, "CB"))!;
            var voucher = new PayorVoucher(pv, claims[0].PayorCode!, null, 2, claims.Sum(c => c.Amount!.Value), "ZZ000001");
            foreach (var c in claims)
            {
                Assert.False(await Repository.LockPaidAsync(write, c.CaseNo, c.ClaimVersion));
                await Repository.InsertAsync(write, voucher, new DateTime(2021, 8, 6), c, null, "ZZTEST");
                Assert.True(await Repository.LockPaidAsync(write, c.CaseNo, c.ClaimVersion));
            }

            var rows = (await write.Connection.QueryAsync<(decimal Total, decimal Paid, string Mth, string Yr, string To, string St)>(
                "SELECT PVPAYChequeAmt, PAYPaidAmt, PAYAccPeriodMth, PAYAccPeriodYr, PVPAYto, RTRIM(PVPayPAYStatus) + PVStatus FROM genisis.Pay_Payment2PAY WHERE PVPAYNo = @pv ORDER BY PVPAYIndex", new { pv }, write.Transaction)).ToList();
            Assert.Equal(2, rows.Count);
            Assert.All(rows, r => Assert.Equal((voucher.Amount, "08", "2021", claims[0].PayorCode, "FA"), (r.Total, r.Mth, r.Yr, r.To, r.St)));
            Assert.Equal(claims.Select(c => c.Amount!.Value), rows.Select(r => r.Paid));
            Assert.Equal(pv, (await Repository.ClaimAsync(write, claims[0].CaseNo, claims[0].ClaimVersion))?.PvNo);
        }

        Assert.Equal(before, await read.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.Pay_Payment2PAY", transaction: read.Transaction));
    }
}
