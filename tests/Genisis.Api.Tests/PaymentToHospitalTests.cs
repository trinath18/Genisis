using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Payment;
using Genisis.Api.Payment.ToHospital;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>The amount of a PV in words (CurrencyConversion.bas), against the vouchers the desktop wrote.</summary>
public class PvAmountWordsTests
{
    [Theory]
    [InlineData("43760.70", "Forty Three Thousand Seven Hundred  And Sixty", " And Cents Seventy  Only")]
    [InlineData("12058.59", "Twelve Thousand And Fifty Eight", " And Cents Fifty Nine Only")]
    [InlineData("56302.65", "Fifty Six Thousand Three Hundred Two", " And Cents Sixty Five Only")]
    [InlineData("7315.05", "Seven Thousand Three Hundred  And Fifteen", " And Cents Five Only")]
    [InlineData("2103.46", "Two Thousand One Hundred Three And Cents Forty Six Only", "")]
    [InlineData("208", "Two Hundred Eight Only", "")]
    [InlineData("16624", "Sixteen Thousand Six Hundred  And Twenty Four Only", "")]
    [InlineData("24357", "Twenty Four Thousand Three Hundred  And Fifty Seven", " Only")]
    [InlineData("1250000.01", "One Million Two Hundred  And Fifty Thousand ", " And  Cent One Only")]
    public void The_words_are_the_desktops(string amount, string line1, string line2) =>
        Assert.Equal((line1, line2), PvAmountWords.Lines(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public async Task Every_hospital_voucher_on_the_database_has_the_words_the_port_gives()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var rows = (await session.Connection.QueryAsync<(decimal Amount, string Line1, string? Line2)>(
            "SELECT DISTINCT PVHOSChequeAmt, PVAmtWording, PVAmtWordingTwo FROM genisis.Pay_Payment2HOS WHERE PVHOSChequeAmt > 0 AND PVAmtWording IS NOT NULL", transaction: session.Transaction)).ToList();
        Assert.NotEmpty(rows);
        var wrong = rows.Where(r =>
        {
            var (l1, l2) = PvAmountWords.Lines(r.Amount);
            var stored2 = r.Line2 ?? "";
            return l1 != r.Line1 || (l2 != stored2 && !(l2 == " Only" && stored2 == ""));
        }).ToList();
        // A voucher whose amount was changed after it was printed keeps the old words; there are a few.
        Assert.True(wrong.Count * 200 < rows.Count, $"{wrong.Count} of {rows.Count} differ, e.g. {string.Join(" | ", wrong.Take(3).Select(w => $"{w.Amount}: [{w.Line1}][{w.Line2}] vs [{PvAmountWords.Lines(w.Amount)}]"))}");
    }
}

/// <summary>Payment > To Hospital: the validators and the service, against a fake repository.</summary>
public class PaymentToHospitalRulesTests
{
    private sealed class FakeRepository : IPaymentToHospitalRepository
    {
        public Dictionary<string, PaymentToHospitalRow> Claims { get; } = [];

        public HashSet<string> Paid { get; } = [];

        public List<(PaymentVoucher Voucher, PaymentToHospitalRow Claim, string Mode)> Inserted { get; } = [];

        private readonly Dictionary<string, int> sequences = new() { ["CB"] = 112242, ["CC"] = 112651, ["CG"] = 32308 };

        public Task<List<PaymentToHospitalRow>> SearchAsync(IDbSession session, PaymentToHospitalFilter filter, int max) => Task.FromResult(Claims.Values.Take(max).ToList());

        public Task<PaymentToHospitalRow?> ClaimAsync(IDbSession session, string caseNo, string version) => Task.FromResult(Claims.GetValueOrDefault($"{caseNo}-{version}"));

        public Task<bool> LockPaidAsync(IDbSession session, string caseNo, string version) => Task.FromResult(Paid.Contains($"{caseNo}-{version}"));

        public Task<string?> NextPvAsync(IDbSession session, string prefix) => Task.FromResult<string?>(prefix + (++sequences[prefix]).ToString("00000000"));

        public Task InsertAsync(IDbSession session, PaymentVoucher voucher, DateTime pvDate, PaymentToHospitalRow claim, string paymentMode, string? remark, string userCode)
        {
            Inserted.Add((voucher, claim, paymentMode));
            return Task.CompletedTask;
        }

        public PaymentToHospitalRow Add(string caseNo, string hospital, decimal amount, string pvInd = "CB", string? pending = null)
        {
            var row = new PaymentToHospitalRow { CaseNo = caseNo, ClaimVersion = "1", HospitalCode = hospital, HospitalName = hospital + " HOSPITAL", Payee = hospital + " SDN BHD", PvInd = pvInd, PayorCode = caseNo[..2], Amount = amount, PayPending = pending };
            Claims[caseNo + "-1"] = row;
            return row;
        }
    }

    private static PaymentToHospitalRequest Request(string mode, params string[] cases) =>
        new() { PvDate = new DateTime(2021, 8, 6), PaymentMode = mode, ChequeNo = mode == "CHEQUE" ? "MBB026496" : null, Claims = cases.Select(c => new PaymentClaimKey { CaseNo = c, ClaimVersion = "1" }).ToList() };

    private static PaymentToHospitalService Service(FakeRepository repository, FakeSessions sessions) => new(sessions, repository, NullLogger<PaymentToHospitalService>.Instance);

    [Fact]
    public void Generate_pv_needs_claims_a_date_a_mode_and_for_a_cheque_its_number()
    {
        var validator = new PaymentToHospitalRequestValidator();
        Assert.Equal("Please select the claims to pay.", validator.FirstError(new PaymentToHospitalRequest { PvDate = DateTime.Today, Claims = [] }));
        Assert.Equal("Please enter the PV Date.", validator.FirstError(new PaymentToHospitalRequest { Claims = [new() { CaseNo = "ST1", ClaimVersion = "1" }] }));
        Assert.Equal("Payment Mode is ONLINE or CHEQUE.", validator.FirstError(Request("CASH", "ST1")));
        var cheque = Request("CHEQUE", "ST1");
        cheque.ChequeNo = "MBB";
        Assert.StartsWith("Please enter the Cheque No", validator.FirstError(cheque));
        Assert.Null(validator.FirstError(Request("CHEQUE", "ST1")));
        Assert.Null(validator.FirstError(Request("ONLINE", "ST1")));
    }

    [Theory]
    [InlineData("MBB026496", "MBB026497")]
    [InlineData("MBB026499", "MBB026500")]
    [InlineData("999", "1000")]
    [InlineData("A-0009", "A-0010")]
    public void The_next_cheque_is_the_number_plus_one(string cheque, string next) => Assert.Equal(next, PaymentCheques.Next(cheque));

    [Fact]
    public async Task One_pv_per_hospital_and_prefix_with_the_total_and_its_words_on_every_row()
    {
        var repository = new FakeRepository();
        repository.Add("ST00000001", "KBARU", 1000.50m);
        repository.Add("ST00000002", "KBARU", 43760.20m);
        repository.Add("ST00000003", "AWCH", 208m);
        repository.Add("PM00000004", "KBARU", 10m, "CC");
        var sessions = new FakeSessions();
        var done = await Service(repository, sessions).CreateAsync(Request("ONLINE", "ST00000001", "st00000002", "ST00000003", "PM00000004", "ST00000001"), "LISA");

        Assert.True(sessions.Last.Committed);
        Assert.Equal(["CB00112243", "CB00112244", "CC00112652"], done.Vouchers.Select(v => v.PvNo));
        var kbaru = done.Vouchers.Single(v => v.PvNo == "CB00112244");
        Assert.Equal(("KBARU HOSPITAL", "KBARU SDN BHD", 2, 44760.70m, "06082021"), (kbaru.HospitalName, kbaru.Payee, kbaru.Claims, kbaru.Amount, kbaru.ChequeNo));
        Assert.Equal(("Forty Four Thousand Seven Hundred  And Sixty", " And Cents Seventy  Only"), (kbaru.Wording, kbaru.WordingTwo));
        Assert.Equal(4, repository.Inserted.Count);
        Assert.All(repository.Inserted.Where(i => i.Voucher.PvNo == "CB00112244"), i => Assert.Equal(44760.70m, i.Voucher.Amount));
    }

    [Fact]
    public async Task Cheques_take_the_next_numbers()
    {
        var repository = new FakeRepository();
        repository.Add("ST00000001", "AAA", 10m);
        repository.Add("ST00000002", "BBB", 20m);
        var done = await Service(repository, new FakeSessions()).CreateAsync(Request("CHEQUE", "ST00000001", "ST00000002"), "LISA");
        Assert.Equal(["MBB026496", "MBB026497"], done.Vouchers.Select(v => v.ChequeNo));
        Assert.All(repository.Inserted, i => Assert.Equal("CHEQUE", i.Mode));
    }

    [Fact]
    public async Task A_claim_paid_held_or_without_a_pv_prefix_stops_the_run()
    {
        var repository = new FakeRepository();
        repository.Add("ST00000001", "AAA", 10m);
        repository.Add("ST00000002", "AAA", 10m, pending: "Y").PendingRemark = "AWAITING GL";
        repository.Add("ZZ00000003", "AAA", 10m, pvInd: null!);
        repository.Paid.Add("ST00000004-1");
        var sessions = new FakeSessions();
        var service = Service(repository, sessions);
        Assert.Equal("Claim ST00000004-1 has been paid.", (await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(Request("ONLINE", "ST00000001", "ST00000004"), "U"))).Message);
        Assert.Equal("The payment of claim ST00000002-1 is pending: AWAITING GL.", (await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(Request("ONLINE", "ST00000002"), "U"))).Message);
        Assert.Equal("Payor ZZ of claim ZZ00000003-1 has no PV prefix (PAY.PVInd).", (await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(Request("ONLINE", "ZZ00000003"), "U"))).Message);
        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(Request("ONLINE", "XX00000009"), "U"));
        Assert.Empty(repository.Inserted);
        Assert.All(sessions.Sessions, s => Assert.False(s.Committed));
    }

    [Fact]
    public async Task A_pv_that_adds_up_to_nothing_is_refused()
    {
        var repository = new FakeRepository();
        repository.Add("ST00000001", "AAA", 10m);
        repository.Add("ST00000002", "AAA", -10m);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(repository, new FakeSessions()).CreateAsync(Request("ONLINE", "ST00000001", "ST00000002"), "U"));
    }
}

/// <summary>Payment > To Hospital against the database; every write is rolled back.</summary>
public class PaymentToHospitalDatabaseTests
{
    private static PaymentToHospitalRepository Repository => new();

    [Fact]
    public async Task The_search_lists_each_unpaid_claim_once_with_its_payors_prefix()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var rows = await Repository.SearchAsync(session, new PaymentToHospitalFilter(), 100000);
        var expected = await session.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM (SELECT CASNumber, ClaimVersion FROM genisis.Pay_Payment2HOSNew WHERE PVHOSNo IS NULL GROUP BY CASNumber, ClaimVersion) x", transaction: session.Transaction);
        Assert.Equal(expected, rows.Count);
        Assert.Equal(rows.Count, rows.Select(r => (r.CaseNo, r.ClaimVersion)).Distinct().Count());
        Assert.All(rows, r => Assert.Null(r.PvNo));
        var first = rows.FirstOrDefault(r => r.PayorCode is not null);
        if (first is null) return;
        var byPayor = await Repository.SearchAsync(session, new PaymentToHospitalFilter { Payor = first.PayorCode + " - X", Hospital = first.HospitalCode }, 100000);
        Assert.Contains(byPayor, r => r.CaseNo == first.CaseNo);
        Assert.All(byPayor, r => Assert.Equal((first.PayorCode, first.HospitalCode), (r.PayorCode, r.HospitalCode)));
        Assert.Equal(first.CaseNo, (await Repository.ClaimAsync(session, first.CaseNo, first.ClaimVersion))?.CaseNo);
    }

    [Fact]
    public async Task A_pv_takes_the_next_number_of_its_counter_and_writes_a_row_per_claim()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var claim = (await Repository.SearchAsync(read, new PaymentToHospitalFilter(), 100000)).FirstOrDefault(r => r.PvInd == "CB" && r.PayPending != "Y" && r.Amount > 0);
        if (claim is null) return;
        var seq = await read.Connection.ExecuteScalarAsync<string>("SELECT PVSeqNo FROM genisis.Pay_PVNSeq2013", transaction: read.Transaction);
        var rowsBefore = await read.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.Pay_Payment2HOS", transaction: read.Transaction);

        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            Assert.False(await Repository.LockPaidAsync(write, claim.CaseNo, claim.ClaimVersion));
            var pv = await Repository.NextPvAsync(write, "CB");
            Assert.Equal("CB" + (int.Parse(seq!.Trim()) + 1).ToString("00000000"), pv);
            Assert.Null(await Repository.NextPvAsync(write, "ZZ"));
            var (w1, w2) = PvAmountWords.Lines(claim.Amount!.Value);
            var voucher = new PaymentVoucher(pv!, claim.HospitalName, claim.Payee, 1, claim.Amount.Value, "06082021", w1, w2);
            await Repository.InsertAsync(write, voucher, new DateTime(2021, 8, 6, 15, 30, 0), claim, "ONLINE", "ZZTEST", "ZZTEST");
            var row = await write.Connection.QuerySingleAsync(
                "SELECT RTRIM(PVHOSNo) AS Pv, PVHOSDate AS Day, PVHOSChequeDate AS ChequeDay, PVHOSChequeAmt AS Total, HOSPaidAmt AS Paid, HOSAccPeriodMth AS Mth, HOSAccPeriodYr AS Yr, RTRIM(PVPayHOSStatus) AS St, PVStatus AS PvSt, PVChequeStatus AS ChqSt, PVPymtMode AS Mode, PVAmtWording AS W1 FROM genisis.Pay_Payment2HOS WHERE CASNumber = @c AND ClaimVersion = @v",
                new { c = claim.CaseNo, v = claim.ClaimVersion }, write.Transaction);
            Assert.Equal((pv, new DateTime(2021, 8, 6), new DateTime(2021, 8, 6), claim.Amount.Value, claim.Amount.Value, "08", "2021"), ((string)row.Pv, (DateTime)row.Day, (DateTime)row.ChequeDay, (decimal)row.Total, (decimal)row.Paid, (string)row.Mth, (string)row.Yr));
            Assert.Equal(("F", "A", "Y", "ONLINE", w1), ((string)row.St, (string)row.PvSt, (string)row.ChqSt, (string)row.Mode, (string)row.W1));
            Assert.True(await Repository.LockPaidAsync(write, claim.CaseNo, claim.ClaimVersion));
            Assert.Equal(pv, (await Repository.ClaimAsync(write, claim.CaseNo, claim.ClaimVersion))?.PvNo);
            Assert.Equal("CB" + (int.Parse(seq.Trim()) + 2).ToString("00000000"), await Repository.NextPvAsync(write, "CB"));
        }

        Assert.Equal(seq, await read.Connection.ExecuteScalarAsync<string>("SELECT PVSeqNo FROM genisis.Pay_PVNSeq2013", transaction: read.Transaction));
        Assert.Equal(rowsBefore, await read.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.Pay_Payment2HOS", transaction: read.Transaction));
    }
}
