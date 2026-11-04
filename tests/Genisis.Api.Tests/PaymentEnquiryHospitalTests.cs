using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Payment.EnquiryHospital;

namespace Genisis.Api.Tests;

/// <summary>Payment &gt; Enquiry &gt; Payment To Hospital: the validator and the service, against a fake repository.</summary>
public class PaymentEnquiryHospitalRulesTests
{
    private sealed class FakeRepository : IPaymentEnquiryHospitalRepository
    {
        public List<PaymentEnquiryHospitalRow> Rows { get; set; } = [];

        public List<int> Maxes { get; } = [];

        public Task<PaymentEnquiryLookups> LookupsAsync(IDbSession session) =>
            Task.FromResult(new PaymentEnquiryLookups([new PaymentCode("H1", "Hospital 1")], [new PaymentCode("AC", "ALLIANZ")], [new PaymentHealthType("N", "Non-Sihat")]));

        public Task<List<PaymentEnquiryHospitalRow>> SearchAsync(IDbSession session, PaymentEnquiryHospitalFilter filter, int max)
        {
            Maxes.Add(max);
            return Task.FromResult(Rows);
        }
    }

    [Fact]
    public void The_payment_status_the_claimability_and_the_box_lengths_are_checked()
    {
        var validator = new PaymentEnquiryHospitalFilterValidator();
        Assert.Null(validator.FirstError(new PaymentEnquiryHospitalFilter()));
        Assert.Null(validator.FirstError(new PaymentEnquiryHospitalFilter { Payment = 5, WsStatus = "BR", Hospital = "H1", Payor = "AC", HealthType = "N" }));
        Assert.Equal("Select a valid Pymt Status.", validator.FirstError(new PaymentEnquiryHospitalFilter { Payment = 6 }));
        Assert.Equal("Select a valid WS Claimability.", validator.FirstError(new PaymentEnquiryHospitalFilter { WsStatus = "ZZ" }));
        Assert.Equal("Clm No is too long.", validator.FirstError(new PaymentEnquiryHospitalFilter { CaseFrom = new string('c', 31) }));
        Assert.Equal("A PV number is too long.", validator.FirstError(new PaymentEnquiryHospitalFilter { PvNoFrom = new string('p', 41) }));
        Assert.Equal("A box is too long.", validator.FirstError(new PaymentEnquiryHospitalFilter { InvNo = new string('i', 101) }));
    }

    [Fact]
    public async Task Search_totals_what_is_payable_cuts_at_5000_and_nothing_found_is_not_found()
    {
        var repository = new FakeRepository();
        var service = new PaymentEnquiryHospitalService(new FakeSessions(), repository);
        Assert.Equal("No record found!", (await Assert.ThrowsAsync<NotFoundException>(() => service.SearchAsync(new PaymentEnquiryHospitalFilter()))).Message);
        repository.Rows = [new PaymentEnquiryHospitalRow { Payable = 10.5m }, new PaymentEnquiryHospitalRow { Payable = null }, new PaymentEnquiryHospitalRow { Payable = -3m }];
        var result = await service.SearchAsync(new PaymentEnquiryHospitalFilter());
        Assert.Equal((3, 7.5m, false), (result.Rows.Count, result.TotalPayable, result.Truncated));
        repository.Rows = Enumerable.Range(0, 5001).Select(_ => new PaymentEnquiryHospitalRow { Payable = 1m }).ToList();
        var cut = await service.SearchAsync(new PaymentEnquiryHospitalFilter());
        Assert.Equal((5000, 5000m, true), (cut.Rows.Count, cut.TotalPayable, cut.Truncated));
        Assert.Contains(5001, repository.Maxes);
        Assert.Equal("H1", (await service.LookupsAsync()).Hospitals[0].Code);
    }
}

/// <summary>Payment &gt; Enquiry &gt; Payment To Hospital against the database (read only).</summary>
[Collection(DatabaseWriteCollection.Name)]
public class PaymentEnquiryHospitalDatabaseTests
{
    private static PaymentEnquiryHospitalRepository Repository => new();

    [Fact]
    public async Task The_rows_are_the_view_with_the_amounts_the_status_and_the_receipts_the_desktop_worked_out()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var (hospitals, payors, healthTypes) = await Repository.LookupsAsync(session) is { } l ? (l.Hospitals, l.Payors, l.HealthTypes) : default;
        Assert.True(hospitals.Count > 100 && payors.Count > 10 && healthTypes.Count >= 2);

        var all = await Repository.SearchAsync(session, new PaymentEnquiryHospitalFilter(), 100000);
        Assert.Equal(await session.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.Pay_ViewPaymentHospEnq", transaction: session.Transaction), all.Count);
        var paid = await Repository.SearchAsync(session, new PaymentEnquiryHospitalFilter { Payment = 1 }, 100000);
        var unpaid = await Repository.SearchAsync(session, new PaymentEnquiryHospitalFilter { Payment = 2 }, 100000);
        var partial = await Repository.SearchAsync(session, new PaymentEnquiryHospitalFilter { Payment = 3 }, 100000);
        var paidAndPartial = await Repository.SearchAsync(session, new PaymentEnquiryHospitalFilter { Payment = 4 }, 100000);
        var unpaidAndPartial = await Repository.SearchAsync(session, new PaymentEnquiryHospitalFilter { Payment = 5 }, 100000);
        Assert.Equal((all.Count, paid.Count + partial.Count, unpaid.Count + partial.Count), (paid.Count + unpaid.Count + partial.Count + all.Count(r => !new[] { "F", "P", "", null }.Contains(r.PayStatus)), paidAndPartial.Count, unpaidAndPartial.Count));
        Assert.All(paid, r => Assert.Equal("F", r.PayStatus));
        Assert.All(unpaid, r => Assert.True(string.IsNullOrEmpty(r.PayStatus)));
        Assert.All(unpaid, r => Assert.Equal(r.Payable, r.Os));
        Assert.All(unpaid, r => Assert.Null(r.PaidTotal));
        Assert.All(paid, r => Assert.Null(r.Os));

        var one = paid.First(r => !string.IsNullOrEmpty(r.CaseNo) && !string.IsNullOrEmpty(r.PvNo));
        var byCase = await Repository.SearchAsync(session, new PaymentEnquiryHospitalFilter { ScanCase = one.CaseNo }, 1000);
        Assert.NotEmpty(byCase);
        Assert.All(byCase, r => Assert.Equal(one.CaseNo, r.CaseNo));
        var byPv = await Repository.SearchAsync(session, new PaymentEnquiryHospitalFilter { PvNoFrom = one.PvNo, PvNoTo = one.PvNo }, 1000);
        Assert.Contains(byPv, r => r.PvIndex == one.PvIndex);
        Assert.All(byPv, r => Assert.Equal(one.PvNo, r.PvNo));
        var byDate = await Repository.SearchAsync(session, new PaymentEnquiryHospitalFilter { PvDateFrom = one.PaymentDate!.Value.Date, PvDateTo = one.PaymentDate.Value.Date, InvNo = one.InvNo }, 1000);
        Assert.Contains(byDate, r => r.PvIndex == one.PvIndex);
        var noPv = await Repository.SearchAsync(session, new PaymentEnquiryHospitalFilter { BlankPvNo = true }, 100000);
        Assert.All(noPv, r => Assert.True(string.IsNullOrEmpty(r.PvNo)));
        var noInvDate = await Repository.SearchAsync(session, new PaymentEnquiryHospitalFilter { BlankInvDate = true }, 100000);
        Assert.All(noInvDate, r => Assert.Null(r.InvDate));
        Assert.Empty(await Repository.SearchAsync(session, new PaymentEnquiryHospitalFilter { CaseFrom = "ZZZZZZZZ" }, 10));
    }

    [Fact]
    public async Task A_claim_with_a_receipt_shows_its_numbers_and_the_amount_and_the_receipt_amount_adds_the_three_kinds()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var claim = await session.Connection.QueryFirstOrDefaultAsync(
            "SELECT TOP (1) RTRIM(e.CASNumber) AS CaseNo, RTRIM(e.ClaimVersion) AS Version FROM genisis.Pay_ViewPaymentHospEnq e INNER JOIN genisis.Pay_ViewReceipt v ON v.CASNumber = e.CASNumber AND v.ClaimVersion = e.ClaimVersion " +
            "WHERE RTRIM(ISNULL(v.ReceiptNoMNRB, '')) + RTRIM(ISNULL(v.ReceiptNoPAY, '')) <> '' ORDER BY e.CASNumber", transaction: session.Transaction);
        if (claim is null) return;
        var rows = await Repository.SearchAsync(session, new PaymentEnquiryHospitalFilter { ScanCase = (string)claim.CaseNo }, 100);
        var withReceipt = rows.First(r => r.ClaimVersion == (string)claim.Version);
        Assert.False(string.IsNullOrEmpty(withReceipt.ReceiptNo));
        var expected = await session.Connection.QueryAsync<(decimal? Mnrb, decimal? Pay, decimal? Mbm)>(
            "SELECT CheckAmtMNRB, CheckAmtPAY, ReceiptAmtMBM FROM genisis.Pay_ViewReceipt WHERE CASNumber = @c AND ClaimVersion = @v AND RTRIM(ISNULL(ReceiptNoMNRB, '')) + RTRIM(ISNULL(ReceiptNoPAY, '')) + RTRIM(ISNULL(ReceiptNoMBM, '')) <> ''",
            new { c = (string)claim.CaseNo, v = (string)claim.Version }, session.Transaction);
        Assert.Contains(expected, e => (e.Mnrb ?? 0) + (e.Pay ?? 0) + (e.Mbm ?? 0) == withReceipt.ReceiptAmt);
    }
}
