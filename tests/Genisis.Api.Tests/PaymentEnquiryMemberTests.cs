using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Payment.EnquiryHospital;
using Genisis.Api.Payment.EnquiryMember;

namespace Genisis.Api.Tests;

/// <summary>Payment &gt; Enquiry &gt; Payment To Member: the validator and the service, against a fake repository.</summary>
public class PaymentEnquiryMemberRulesTests
{
    private sealed class FakeRepository : IPaymentEnquiryMemberRepository
    {
        public List<PaymentEnquiryMemberRow> Payments { get; set; } = [];

        public List<PaymentEnquiryMemberRow> Reissues { get; set; } = [];

        public List<string> Calls { get; } = [];

        public Task<PaymentMemberLookups> LookupsAsync(IDbSession session) => Task.FromResult(new PaymentMemberLookups([new PaymentCode("AC", "ALLIANZ")], [new PaymentHealthType("N", "Non-Sihat")]));

        public Task<List<PaymentEnquiryMemberRow>> SearchAsync(IDbSession session, PaymentEnquiryMemberFilter filter, bool reissue, int max)
        {
            Calls.Add($"search reissue={reissue} max={max}");
            return Task.FromResult(reissue ? Reissues : Payments);
        }

        public Task<List<PaymentEnquiryMemberReceipt>> ReceiptsAsync(IDbSession session, IReadOnlyCollection<(string CaseNo, string Version)> claims)
        {
            Calls.Add("receipts " + string.Join(',', claims.Select(c => $"{c.CaseNo}-{c.Version}")));
            return Task.FromResult(claims.Select(c => new PaymentEnquiryMemberReceipt { CaseNo = c.CaseNo, ClaimVersion = c.Version, Kind = "PAY" }).ToList());
        }
    }

    [Fact]
    public void The_payment_status_the_claimability_the_period_and_the_box_lengths_are_checked()
    {
        var validator = new PaymentEnquiryMemberFilterValidator();
        Assert.Null(validator.FirstError(new PaymentEnquiryMemberFilter()));
        Assert.Null(validator.FirstError(new PaymentEnquiryMemberFilter { Payment = 5, WsStatus = "AA", MonthFrom = 1, MonthTo = 12, YearFrom = 1999, YearTo = 3000 }));
        Assert.Equal("Select a valid Pymt Status.", validator.FirstError(new PaymentEnquiryMemberFilter { Payment = -1 }));
        Assert.Equal("Select a valid WS Claimability.", validator.FirstError(new PaymentEnquiryMemberFilter { WsStatus = "XX" }));
        Assert.Equal("A/C Period Mth must be 1 to 12.", validator.FirstError(new PaymentEnquiryMemberFilter { MonthTo = 13 }));
        Assert.Equal("A/C Period Yr must be 1999 to 3000.", validator.FirstError(new PaymentEnquiryMemberFilter { YearFrom = 1998 }));
        Assert.Equal("Member Name is too long.", validator.FirstError(new PaymentEnquiryMemberFilter { MbmName = new string('m', 101) }));
        Assert.Equal("A number is too long.", validator.FirstError(new PaymentEnquiryMemberFilter { CaseNo = new string('c', 31) }));
        Assert.Equal("A number is too long.", validator.FirstError(new PaymentEnquiryMemberFilter { CrNoFrom = new string('c', 41) }));
    }

    [Fact]
    public async Task Search_gives_the_payments_the_reissued_ones_and_the_receipts_of_the_claims_listed_once_each()
    {
        var repository = new FakeRepository();
        var service = new PaymentEnquiryMemberService(new FakeSessions(), repository);
        Assert.Equal("No record found!", (await Assert.ThrowsAsync<NotFoundException>(() => service.SearchAsync(new PaymentEnquiryMemberFilter()))).Message);
        repository.Payments =
        [
            new PaymentEnquiryMemberRow { CaseNo = "A1", ClaimVersion = "1", Payable = 100m, PaidAmt = 40m },
            new PaymentEnquiryMemberRow { CaseNo = "A1", ClaimVersion = "1", Payable = 100m, PaidAmt = 60m },
            new PaymentEnquiryMemberRow { CaseNo = "B2", ClaimVersion = "2", Payable = 50m, PaidAmt = null },
        ];
        repository.Reissues = [new PaymentEnquiryMemberRow { CaseNo = "A1", ClaimVersion = "1", PvStatus = "R" }];
        var result = await service.SearchAsync(new PaymentEnquiryMemberFilter());
        Assert.Equal((3, 1, 2, 250m, 100m, false), (result.Payments.Count, result.Reissues.Count, result.Receipts.Count, result.TotalPayable, result.TotalPaid, result.Truncated));
        Assert.Equal(["search reissue=False max=5001", "search reissue=True max=5000", "receipts A1-1,B2-2"], repository.Calls.Skip(1));
        Assert.Equal("ALLIANZ", (await service.LookupsAsync()).Payors[0].Name);
    }

    [Fact]
    public async Task More_than_5000_payments_are_cut_and_said_so()
    {
        var repository = new FakeRepository { Payments = Enumerable.Range(0, 5001).Select(i => new PaymentEnquiryMemberRow { CaseNo = "C" + i, ClaimVersion = "1", Payable = 1m }).ToList() };
        var result = await new PaymentEnquiryMemberService(new FakeSessions(), repository).SearchAsync(new PaymentEnquiryMemberFilter());
        Assert.Equal((5000, true, 5000m), (result.Payments.Count, result.Truncated, result.TotalPayable));
    }
}

/// <summary>Payment &gt; Enquiry &gt; Payment To Member against the database (read only).</summary>
[Collection(DatabaseWriteCollection.Name)]
public class PaymentEnquiryMemberDatabaseTests
{
    private static PaymentEnquiryMemberRepository Repository => new();

    [Fact]
    public async Task The_payments_are_the_claims_with_a_debit_credit_note_filtered_by_every_box_and_the_reissued_ones_are_status_R()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var lookups = await Repository.LookupsAsync(session);
        Assert.True(lookups.Payors.Count > 10 && lookups.HealthTypes.Count >= 2);

        var all = await Repository.SearchAsync(session, new PaymentEnquiryMemberFilter(), false, 100000);
        Assert.NotEmpty(all);
        var claims = await session.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.WS_Claim WHERE DebitCreditRefNo IS NOT NULL", transaction: session.Transaction);
        Assert.True(all.Select(r => (r.CaseNo, r.ClaimVersion)).Distinct().Count() <= claims);
        Assert.All(all, r => Assert.False(string.IsNullOrEmpty(r.CrNo) && r.CrNo is not ""));
        var paid = await Repository.SearchAsync(session, new PaymentEnquiryMemberFilter { Payment = 1 }, false, 100000);
        var unpaid = await Repository.SearchAsync(session, new PaymentEnquiryMemberFilter { Payment = 2 }, false, 100000);
        Assert.All(paid, r => Assert.Equal("F", r.PayStatus));
        Assert.All(unpaid, r => Assert.True(string.IsNullOrEmpty(r.PayStatus)));
        Assert.All(unpaid, r => Assert.Equal(r.Payable, r.Os));
        Assert.All(unpaid, r => Assert.Null(r.PaidTotal));

        var one = paid.First(r => !string.IsNullOrEmpty(r.PvNo));
        var byClaim = await Repository.SearchAsync(session, new PaymentEnquiryMemberFilter { CaseNo = one.CaseNo, Version = one.ClaimVersion }, false, 1000);
        Assert.NotEmpty(byClaim);
        Assert.All(byClaim, r => Assert.Equal((one.CaseNo, one.ClaimVersion), (r.CaseNo, r.ClaimVersion)));
        var byPv = await Repository.SearchAsync(session, new PaymentEnquiryMemberFilter { PvNoFrom = one.PvNo, PvNoTo = one.PvNo }, false, 1000);
        Assert.Contains(byPv, r => r.PvIndex == one.PvIndex);
        var byName = await Repository.SearchAsync(session, new PaymentEnquiryMemberFilter { MbmName = (one.MemberName ?? "")[..Math.Min(8, (one.MemberName ?? "").Length)] }, false, 100000);
        Assert.Contains(byName, r => r.PvIndex == one.PvIndex);
        var byCr = await Repository.SearchAsync(session, new PaymentEnquiryMemberFilter { CrNoFrom = one.CrNo, CrNoTo = one.CrNo }, false, 1000);
        Assert.Contains(byCr, r => r.CaseNo == one.CaseNo);
        var noPv = await Repository.SearchAsync(session, new PaymentEnquiryMemberFilter { BlankPvNo = true }, false, 100000);
        Assert.All(noPv, r => Assert.True(string.IsNullOrEmpty(r.PvNo)));
        var reissued = await Repository.SearchAsync(session, new PaymentEnquiryMemberFilter(), true, 1000);
        Assert.NotEmpty(reissued);
        Assert.All(reissued, r => Assert.Equal("R", r.PvStatus));
        Assert.Empty(await Repository.SearchAsync(session, new PaymentEnquiryMemberFilter { CaseNo = "ZZZZZZZZ" }, false, 10));
    }

    [Fact]
    public async Task The_receipts_of_a_claim_come_as_a_line_for_each_receipt_from_MNRB_the_payor_and_the_member()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var receipts = await Repository.ReceiptsAsync(session, [("AC00000644", "1"), ("ZZ-NONE", "1")]);
        Assert.NotEmpty(receipts);
        Assert.All(receipts, r => Assert.Equal("AC00000644", r.CaseNo));
        Assert.All(receipts, r => Assert.Contains(r.Kind, new[] { "MNRB", "PAY", "MBM" }));
        Assert.All(receipts, r => Assert.False(string.IsNullOrWhiteSpace(r.ReceiptNo)));
        var view = await session.Connection.QueryAsync<(string? Mnrb, string? Pay, string? Mbm)>("SELECT ReceiptNoMNRB, ReceiptNoPAY, ReceiptNoMBM FROM genisis.Pay_ViewReceipt WHERE CASNumber = 'AC00000644' AND ClaimVersion = '1'", transaction: session.Transaction);
        Assert.Equal(view.Sum(v => new[] { v.Mnrb, v.Pay, v.Mbm }.Count(n => !string.IsNullOrWhiteSpace(n))), receipts.Count);
        Assert.Empty(await Repository.ReceiptsAsync(session, []));
    }
}
