using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Payment;
using Genisis.Api.Payment.ToHospital;
using Genisis.Api.Payment.ToMember;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Payment > To Member: the validators and the service, against a fake repository.</summary>
public class PaymentToMemberRulesTests
{
    private sealed class FakeRepository : IPaymentToMemberRepository
    {
        public Dictionary<string, PaymentToMemberRow> Claims { get; } = [];

        public HashSet<string> Done { get; } = [];

        public List<(MemberVoucher Voucher, string Mode)> Inserted { get; } = [];

        public List<(string Case, string Remark)> Voided { get; } = [];

        private int seq = 112651;

        public Task<List<PaymentToMemberRow>> SearchAsync(IDbSession session, PaymentToMemberFilter filter, int max) => Task.FromResult(Claims.Values.ToList());

        public Task<PaymentToMemberRow?> ClaimAsync(IDbSession session, string source, string caseNo, string version) => Task.FromResult(Claims.GetValueOrDefault($"{caseNo}-{version}"));

        public Task<bool> LockDoneAsync(IDbSession session, string caseNo, string version) => Task.FromResult(Done.Contains($"{caseNo}-{version}"));

        public Task<string?> NextPvAsync(IDbSession session, string prefix) => Task.FromResult<string?>(prefix + (++seq).ToString("00000000"));

        public Task InsertAsync(IDbSession session, MemberVoucher voucher, DateTime pvDate, string paymentMode, string? remark, string userCode)
        {
            Inserted.Add((voucher, paymentMode));
            return Task.CompletedTask;
        }

        public Task VoidAsync(IDbSession session, string caseNo, string version, DateTime voidDate, string? remark, string userCode)
        {
            Voided.Add((caseNo, remark ?? ""));
            return Task.CompletedTask;
        }

        public void Add(string caseNo, decimal amount, string? pvInd = "CC", string? pending = null) =>
            Claims[caseNo + "-1"] = new PaymentToMemberRow
            {
                CaseNo = caseNo, ClaimVersion = "1", PayorCode = caseNo[..2], PvInd = pvInd, Amount = amount, PayTo = "MOHAMMAD FAIZ BIN MAJID", Bank = "MALAYAN BANKING BERHAD",
                Account = "164548167647", PayPending = pending,
            };
    }

    private static PaymentToMemberRequest Request(string mode, params string[] cases) => new()
    {
        Source = "auto", PvDate = new DateTime(2021, 8, 6), PaymentMode = mode, ChequeNo = mode == "CHEQUE" ? "MBB000100" : null,
        Claims = cases.Select(c => new PaymentMemberClaim { CaseNo = c, ClaimVersion = "1" }).ToList(),
    };

    private static PaymentToMemberService Service(FakeRepository repository, FakeSessions sessions) => new(sessions, repository, NullLogger<PaymentToMemberService>.Instance);

    [Fact]
    public void The_requests_need_a_source_claims_and_a_date()
    {
        Assert.Null(new PaymentToMemberRequestValidator().FirstError(Request("ONLINE", "PM1")));
        var bad = Request("ONLINE", "PM1");
        bad.Source = "x";
        Assert.Equal("Select Pay2MBM or Pay2MBM(Auto).", new PaymentToMemberRequestValidator().FirstError(bad));
        var noCheque = Request("CHEQUE", "PM1");
        noCheque.ChequeNo = "";
        Assert.StartsWith("Please enter the Cheque No", new PaymentToMemberRequestValidator().FirstError(noCheque));
        Assert.Equal("Please select the claims to void.", new PaymentToMemberVoidRequestValidator().FirstError(new PaymentToMemberVoidRequest { VoidDate = DateTime.Today, Claims = [] }));
        Assert.Equal("Select Pay2MBM or Pay2MBM(Auto).", new PaymentToMemberFilterValidator().FirstError(new PaymentToMemberFilter { Source = "hos" }));
    }

    [Fact]
    public async Task One_pv_per_claim_with_the_claims_payee_unless_it_is_changed()
    {
        var repository = new FakeRepository();
        repository.Add("PM00000001", 8459.25m);
        repository.Add("PM00000002", 296.50m, "CB");
        var request = Request("ONLINE", "PM00000001", "PM00000002", "pm00000001");
        request.Claims![1].PayTo = " guan chong sdn bhd ";
        var sessions = new FakeSessions();
        var done = await Service(repository, sessions).CreateAsync(request, "LISA");
        Assert.True(sessions.Last.Committed);
        Assert.Equal(2, done.Vouchers.Count);
        var first = done.Vouchers[0];
        Assert.Equal(("CC00112652", "MOHAMMAD FAIZ BIN MAJID", "164548167647", 8459.25m, "06082021"), (first.PvNo, first.PayTo, first.Account, first.Amount, first.ChequeNo));
        Assert.Equal(("Eight Thousand Four Hundred  And Fifty Nine", " And Cents Twenty Five Only"), (first.Wording, first.WordingTwo));
        Assert.Equal(("CB00112653", "GUAN CHONG SDN BHD"), (done.Vouchers[1].PvNo, done.Vouchers[1].PayTo));
        Assert.Equal("Two Hundred  And Ninety Six And Cents Fifty  Only", done.Vouchers[1].Wording);
    }

    [Fact]
    public async Task Cheques_are_numbered_on_and_a_claim_with_nothing_payable_has_to_be_voided()
    {
        var repository = new FakeRepository();
        repository.Add("PM00000001", 10m);
        repository.Add("PM00000002", 20m);
        repository.Add("PM00000003", 0m);
        repository.Add("PM00000004", 5m, pending: "Y");
        var service = Service(repository, new FakeSessions());
        Assert.Equal(["MBB000100", "MBB000101"], (await service.CreateAsync(Request("CHEQUE", "PM00000001", "PM00000002"), "U")).Vouchers.Select(v => v.ChequeNo));
        repository.Claims.Remove("PM00000001-1");
        repository.Done.Add("PM00000001-1");
        Assert.Equal("Claim PM00000001-1 has been paid or voided.", (await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(Request("ONLINE", "PM00000001"), "U"))).Message);
        Assert.Equal("Claim PM00000003-1 has nothing payable to the member: void it instead.", (await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(Request("ONLINE", "PM00000003"), "U"))).Message);
        Assert.Equal("The payment of claim PM00000004-1 is pending.", (await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(Request("ONLINE", "PM00000004"), "U"))).Message);
    }

    [Fact]
    public async Task Void_closes_the_claims_ticked()
    {
        var repository = new FakeRepository();
        repository.Add("PM00000003", 0m);
        repository.Add("PM00000005", 12m);
        var sessions = new FakeSessions();
        var done = await Service(repository, sessions).VoidAsync(new PaymentToMemberVoidRequest
        {
            Source = "dn", VoidDate = new DateTime(2021, 8, 6), Remark = "case declined", Claims = [new PaymentClaimKey { CaseNo = "PM00000003", ClaimVersion = "1" }, new PaymentClaimKey { CaseNo = "PM00000005", ClaimVersion = "1" }],
        }, "U");
        Assert.Equal(2, done.Count);
        Assert.True(sessions.Last.Committed);
        Assert.Equal([("PM00000003", "CASE DECLINED"), ("PM00000005", "CASE DECLINED")], repository.Voided);
    }
}

/// <summary>Payment > To Member against the database; every write is rolled back.</summary>
public class PaymentToMemberDatabaseTests
{
    private static PaymentToMemberRepository Repository => new();

    [Fact]
    public async Task Each_source_lists_its_claims_not_done_once_each()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        foreach (var (source, view) in new[] { ("dn", "Pay_ViewPayment2MBMNew"), ("auto", "Pay_ViewPayment2MBMAuto") })
        {
            var rows = await Repository.SearchAsync(session, new PaymentToMemberFilter { Source = source }, 100000);
            var expected = await session.Connection.ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) FROM (SELECT CASNumber, ClaimVersion FROM genisis.{view} WHERE PVNo IS NULL AND CASNumber IS NOT NULL GROUP BY CASNumber, ClaimVersion) x", transaction: session.Transaction);
            Assert.Equal(expected, rows.Count);
            Assert.Equal(rows.Count, rows.Select(r => (r.CaseNo, r.ClaimVersion)).Distinct().Count());
            var first = rows.FirstOrDefault(r => r.MemberNo is not null);
            if (first is null) continue;
            var byMember = await Repository.SearchAsync(session, new PaymentToMemberFilter { Source = source, MemberNo = first.MemberNo!.ToLowerInvariant(), Payor = first.PayorCode }, 100000);
            Assert.Contains(byMember, r => r.CaseNo == first.CaseNo);
            Assert.Equal(first.Amount, (await Repository.ClaimAsync(session, source, first.CaseNo, first.ClaimVersion))?.Amount);
        }
    }

    [Fact]
    public async Task A_pv_and_a_void_are_written_as_the_desktop_wrote_them()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var rows = await Repository.SearchAsync(read, new PaymentToMemberFilter { Source = "dn" }, 100000);
        var payable = rows.FirstOrDefault(r => r.Amount > 0 && r.PvInd is "CB" or "CC" or "CG");
        var nothing = rows.FirstOrDefault(r => r.Amount == 0);
        if (payable is null || nothing is null) return;
        var before = await read.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.Pay_Payment2MBM", transaction: read.Transaction);

        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var pv = (await Repository.NextPvAsync(write, payable.PvInd!))!;
            var (w1, w2) = PvAmountWords.Lines(payable.Amount!.Value);
            await Repository.InsertAsync(write, new MemberVoucher(pv, payable.CaseNo, payable.ClaimVersion, payable.PayTo, payable.Bank, payable.Account, payable.Amount.Value, "06082021", w1, w2),
                new DateTime(2021, 8, 6), "ONLINE", "TT TO MBB", "ZZTEST");
            var row = await write.Connection.QuerySingleAsync<(decimal Cheque, decimal Paid, string Mth, string St, string ChqSt, string Mode, string W1)>(
                "SELECT PVChequeAmt, MBMPaidAmt, PAYAccPeriodMth, RTRIM(PVPayMBMStatus) + PVStatus, PVChequeStatus, PVPymtMode, PVAmtWording FROM genisis.Pay_Payment2MBM WHERE PVNo = @pv", new { pv }, write.Transaction);
            Assert.Equal((payable.Amount.Value, payable.Amount.Value, "08", "FA", "Y", "ONLINE", w1), row);
            Assert.True(await Repository.LockDoneAsync(write, payable.CaseNo, payable.ClaimVersion));
            Assert.Equal(pv, (await Repository.ClaimAsync(write, "dn", payable.CaseNo, payable.ClaimVersion))?.PvNo?.Trim());

            await Repository.VoidAsync(write, nothing.CaseNo, nothing.ClaimVersion, new DateTime(2021, 8, 6), null, "ZZTEST");
            var voided = await write.Connection.QuerySingleAsync<(string Pv, string Chq, decimal Paid, string Mth, string? St)>(
                "SELECT PVNo, PVChequeNo, MBMPaidAmt, PAYAccPeriodMth, PVStatus FROM genisis.Pay_Payment2MBM WHERE CASNumber = @c AND ClaimVersion = @v", new { c = nothing.CaseNo, v = nothing.ClaimVersion }, write.Transaction);
            Assert.Equal(("VOID", "VOID", 0m, "8", (string?)null), voided);
            Assert.DoesNotContain(await Repository.SearchAsync(write, new PaymentToMemberFilter { Source = "dn" }, 100000), r => r.CaseNo == nothing.CaseNo && r.ClaimVersion == nothing.ClaimVersion);
        }

        Assert.Equal(before, await read.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.Pay_Payment2MBM", transaction: read.Transaction));
    }
}
