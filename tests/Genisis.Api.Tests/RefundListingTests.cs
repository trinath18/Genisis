using Dapper;
using Genisis.Api.Common;
using Genisis.Api.ContributionBilling.RefundListing;
using Genisis.Api.Data;

namespace Genisis.Api.Tests;

/// <summary>Contribution Billing > Refund Listing: the validator, the arithmetic and the service, against a fake repository.</summary>
public class RefundListingRulesTests
{
    private sealed class FakeRepository : IRefundListingRepository
    {
        public bool CancelStatus { get; set; }

        public bool SupplementaryAsked { get; private set; }

        public List<RefundRaw> Principals { get; } = [];

        public List<RefundRaw> Supplementary { get; } = [];

        public Task<(string Code, string? Name, bool CancelStatus)?> PayorAsync(IDbSession session, string payor) =>
            Task.FromResult<(string, string?, bool)?>(payor == "ZZ" ? null : (payor, "A PAYOR", CancelStatus));

        public Task<List<RefundRaw>> PrincipalsAsync(IDbSession session, string payor, RefundFilter filter) => Task.FromResult(Principals);

        public Task<List<RefundRaw>> SupplementaryAsync(IDbSession session, string payor, RefundFilter filter)
        {
            SupplementaryAsked = true;
            return Task.FromResult(Supplementary);
        }
    }

    private static RefundRaw Raw(string member, string ins, decimal? fee, decimal? utilised, decimal? refund, int? days = 5, int? cases = 0) =>
        new() { MemberNo = member, InsCode = ins, McoFee = fee, McoUtilised = utilised, McoRefund = refund, DaysUtilised = days, TotalCases = cases, MemberName = "N", MonthsUtilised = null };

    [Fact]
    public void The_date_selection_is_checked_before_the_payor()
    {
        var validator = new RefundFilterValidator();
        Assert.Equal("Please Select Date Selection!", validator.FirstError(new RefundFilter()));
        Assert.Equal("Please Select Payor!", validator.FirstError(new RefundFilter { DateBy = "effective" }));
        Assert.Null(validator.FirstError(new RefundFilter { DateBy = "bordx", Payor = "AB - A PAYOR", Printed = "Y - Yes" }));
        Assert.NotNull(validator.FirstError(new RefundFilter { DateBy = "monthly", Payor = "AB" }));
        Assert.NotNull(validator.FirstError(new RefundFilter { DateBy = "bordx", Payor = "AB", Printed = "X" }));
        Assert.Equal("AB", RefundRules.PayorCode(" ab - A Payor "));
    }

    [Theory]
    [InlineData(100, 20, false, 75)]
    [InlineData(100, 25, false, 75)]
    [InlineData(100, 26, false, 26)]
    [InlineData(100, 20, true, 50)]
    [InlineData(100, 50, true, 50)]
    [InlineData(100, 51, true, 51)]
    public void The_refund_min_follows_the_desktops_code(int fee, int utilised, bool family, int expected) => Assert.Equal((decimal)expected, RefundRules.RefundMin(fee, utilised, family));

    [Fact]
    public void A_missing_utilised_fee_gives_zero_and_a_missing_fee_counts_as_zero()
    {
        Assert.Equal(0m, RefundRules.RefundMin(100m, null, true));
        Assert.Equal(-25m, RefundRules.RefundMin(null, 10m, false));
    }

    [Fact]
    public void Days_utilised_below_zero_show_as_zero_only_for_payors_that_list_cancelled_members()
    {
        Assert.Equal((0, -5, 0, null), (RefundRules.DaysUtilised(-5, true), RefundRules.DaysUtilised(-5, false), RefundRules.DaysUtilised(null, true), RefundRules.DaysUtilised(null, false)));
    }

    [Fact]
    public async Task Family_principals_are_followed_by_their_supplementary_members_and_the_total_adds_the_principals_refunds()
    {
        var repository = new FakeRepository();
        repository.Principals.AddRange([Raw("M1", "H ", 100m, 20m, 10.5m, cases: 2), Raw("M2", "G ", 50m, 40m, null), Raw("M3", "F ", 30m, null, 4m)]);
        repository.Supplementary.AddRange([Raw("M1", "", 17m, 4.5m, 12m), Raw("M3", "", 17m, 10m, 6m), Raw("M9", "", 1m, 1m, 1m)]);
        var result = await new RefundListingService(new FakeSessions(), repository).SearchAsync(new RefundFilter { DateBy = "effective", Payor = "ab" });
        Assert.Equal(["M1", "M1", "M2", "M3", "M3"], result.Rows.Select(r => r.MemberNo));
        Assert.Equal(["principal", "supplementary", "principal", "principal", "supplementary"], result.Rows.Select(r => r.Kind));
        Assert.Equal((3, 2, 14.5m), (result.Principals, result.Supplementary, result.TotalRefund));
        Assert.Equal((50m, "This member had 2 Case(s)", 40m, 0m, -8m), (result.Rows[0].McoRefundMin, result.Rows[0].Remarks, result.Rows[2].McoRefundMin, result.Rows[3].McoRefundMin, result.Rows[1].McoRefundMin));
    }

    [Fact]
    public async Task Without_a_family_principal_the_supplementary_members_are_not_read_and_an_unknown_payor_is_not_found()
    {
        var repository = new FakeRepository();
        repository.Principals.Add(Raw("M2", "G ", 50m, 40m, 1m));
        var service = new RefundListingService(new FakeSessions(), repository);
        await service.SearchAsync(new RefundFilter { DateBy = "bordx", Payor = "AB" });
        Assert.False(repository.SupplementaryAsked);
        var failure = await Assert.ThrowsAsync<NotFoundException>(() => service.SearchAsync(new RefundFilter { DateBy = "bordx", Payor = "ZZ" }));
        Assert.Equal("Payor not found", failure.Message);
    }
}

/// <summary>Refund Listing against the database (read only).</summary>
public class RefundListingDatabaseTests
{
    private static RefundListingRepository Repository => new();

    private static async Task<int> Count(IDbSession s, string sql, object? args = null) => await s.Connection.ExecuteScalarAsync<int>(sql, args, s.Transaction);

    [Fact]
    public async Task The_principals_and_the_supplementary_members_are_what_the_views_hold_for_each_criterion()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var payor = await Repository.PayorAsync(session, "AX");
        Assert.NotNull(payor);
        Assert.Equal("AX", payor!.Value.Code);
        Assert.Null(await Repository.PayorAsync(session, "QQ"));

        var all = new RefundFilter { DateBy = "effective", DateFrom = new DateTime(2021, 7, 1), DateTo = new DateTime(2021, 12, 31), Printed = "N" };
        var principals = await Repository.PrincipalsAsync(session, "AX", all);
        Assert.NotEmpty(principals);
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM (SELECT DISTINCT MBMNumber, MBMPolicyNo, MBMName, MBMMCOUtilised, MBMStatus, MBMCOVID, MBMENDtEffDate, MBMENDtBordxDate, MBMPayorEffDate, MBMPayorExpDate, MBMMCOFee, MBMMCORefund, MBMBordxDate, DayInsured, MBMIcBcPp, DayUtilised, INSCode, MBMCRNote, MBMCRNoteDate, MBMInvoiceNo, MBMInvoiceDate, TotalCase, CheckDay, MonthUtilised FROM dbo.VIEW_MBM_Refund_Search WHERE PAYCode = 'AX' AND MBMENDtEffDate >= '2021-07-01' AND MBMENDtEffDate <= '2021-12-31' AND (MBMCRNote IS NULL OR MBMCRNote = '')) x"), principals.Count);
        Assert.All(principals, p => Assert.InRange(p.EndtEffDate!.Value, all.DateFrom!.Value, all.DateTo!.Value));
        Assert.Equal(principals.Select(p => p.MemberNo).Order(StringComparer.Ordinal), principals.Select(p => p.MemberNo));

        Assert.Empty(await Repository.PrincipalsAsync(session, "AX", new RefundFilter { DateBy = "effective", DateFrom = all.DateFrom, DateTo = all.DateTo, Printed = "Y" }));
        var byBordx = await Repository.PrincipalsAsync(session, "AX", new RefundFilter { DateBy = "bordx", DateFrom = new DateTime(2099, 1, 1), Printed = "N" });
        Assert.Empty(byBordx);

        var supplementary = await Repository.SupplementaryAsync(session, "ST", new RefundFilter { DateBy = "effective", DateFrom = new DateTime(2021, 7, 1), DateTo = new DateTime(2021, 12, 31) });
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM (SELECT DISTINCT MBMNumber, MBMCOVID, MBMPolicyNo, MBMName, MBMStatus, MBMIcBcPp, MBMBordxDate, MBMPayorEffDate, MBMPayorExpDate, DayInsured, MBMENDtEffDate, MBMENDtBordxDate, DayUtilised, MonthUtilised, MBMMCOFee, MBMMCOUtilised, MBMMCORefund, MBMInvoiceNo, MBMInvoiceDate, TotalCase, MBMCRNote, MBMCRNoteDate FROM dbo.View_MBM_Supplementary_Refund WHERE PAYCode = 'ST' AND MBMENDtEffDate >= '2021-07-01' AND MBMENDtEffDate <= '2021-12-31') x"), supplementary.Count);
    }

    [Fact]
    public async Task The_service_follows_each_family_principal_with_its_supplementary_members()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        var service = new RefundListingService(TestDb.Sessions(), Repository);
        var result = await service.SearchAsync(new RefundFilter { DateBy = "effective", Payor = "st - x", DateFrom = new DateTime(2021, 7, 1), DateTo = new DateTime(2021, 12, 31) });
        Assert.Equal(result.Principals, result.Rows.Count(r => r.Kind == "principal"));
        Assert.Equal(result.Supplementary, result.Rows.Count(r => r.Kind == "supplementary"));
        Assert.True(result.Supplementary > 0);
        for (var i = 0; i < result.Rows.Count; i++)
            if (result.Rows[i].Kind == "supplementary") Assert.Equal(result.Rows[i - 1].MemberNo, result.Rows[i].MemberNo);
        Assert.Equal(result.Rows.Where(r => r.Kind == "principal").Sum(r => r.McoRefund ?? 0), result.TotalRefund);
        await Assert.ThrowsAsync<NotFoundException>(() => service.SearchAsync(new RefundFilter { DateBy = "effective", Payor = "QQ" }));
    }
}
