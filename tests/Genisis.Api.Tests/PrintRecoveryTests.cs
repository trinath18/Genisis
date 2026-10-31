using Dapper;
using Genisis.Api.ClaimBordx.Recovery;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Tests;

/// <summary>Claim Bordx > Print Recovery: the validator and the service, against a fake repository.</summary>
public class PrintRecoveryRulesTests
{
    private sealed class FakeRepository : IPrintRecoveryRepository
    {
        public RecoveryFilter? Searched { get; private set; }

        public Task<List<RecoveryCompany>> CompaniesAsync(IDbSession session) => Task.FromResult(new List<RecoveryCompany> { new("ACME SDN BHD", "1"), new("MDEC", "2") });

        public Task<List<RecoveryRow>> SearchAsync(IDbSession session, RecoveryFilter filter)
        {
            Searched = filter;
            return Task.FromResult(new List<RecoveryRow>
            {
                new() { CaseNo = "AB1", ClaimVersion = 1, BordxAmount = 100m, Recovery = -10.5m, ActualAmount = 120m },
                new() { CaseNo = "AB2", ClaimVersion = 2, BordxAmount = 50m, Recovery = -4.5m, ActualAmount = null },
            });
        }
    }

    private static PrintRecoveryService Service(FakeRepository repository) => new(new FakeSessions(), repository);

    [Fact]
    public void A_group_company_is_needed()
    {
        var validator = new RecoveryFilterValidator();
        Assert.Equal("Please select Group Company", validator.FirstError(new RecoveryFilter()));
        Assert.Equal("Please select Group Company", validator.FirstError(new RecoveryFilter { Company = "  " }));
        Assert.Null(validator.FirstError(new RecoveryFilter { Company = "ACME SDN BHD" }));
    }

    [Fact]
    public async Task The_search_gives_the_clients_layout_and_adds_up_the_rows()
    {
        var repository = new FakeRepository();
        var result = await Service(repository).SearchAsync(new RecoveryFilter { Company = "acme sdn bhd ", BordxFrom = "B1" });
        Assert.Equal(("1", 150m, -15m, 120m, false), (result.ReportType, result.TotalBordxAmount, result.TotalRecovery, result.TotalActual, result.Truncated));
        Assert.Equal(("ACME SDN BHD", "B1"), (repository.Searched!.Company, repository.Searched.BordxFrom));
    }

    [Fact]
    public async Task A_company_that_is_not_a_btbg_client_is_not_found_and_the_list_is_cut_at_five_thousand()
    {
        var failure = await Assert.ThrowsAsync<NotFoundException>(() => Service(new FakeRepository()).SearchAsync(new RecoveryFilter { Company = "SOMEONE ELSE" }));
        Assert.Equal("Group Company not found", failure.Message);
        Assert.Equal(["ACME SDN BHD", "MDEC"], (await Service(new FakeRepository()).CompaniesAsync()).Select(c => c.Name));
    }
}

/// <summary>Print Recovery against the database (read only). It reads the heavy claim report view, so it runs with the write tests (one at a time) instead of next to them.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class PrintRecoveryDatabaseTests
{
    private static PrintRecoveryRepository Repository => new();

    private static async Task<int> Count(IDbSession s, string sql, object? args = null) => await s.Connection.ExecuteScalarAsync<int>(sql, args, s.Transaction);

    [Fact]
    public async Task The_clients_are_the_btbg_group_companies_and_the_search_gives_the_negative_payables_of_the_company_for_each_criterion()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var companies = await Repository.CompaniesAsync(session);
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM dbo.CompanyGrp WHERE BTBGClient = 'Y'"), companies.Count);
        Assert.Contains(companies, c => c.ReportType == "2");

        var company = await session.Connection.QueryFirstAsync<string>("SELECT TOP (1) RTRIM(v.MBMGroupCompany) FROM genisis.WS_ClaimReport v WHERE v.CLMPayableByMedix2M < 0 AND v.MBMGroupCompany IN (SELECT g.CoGRPName FROM dbo.CompanyGrp g WHERE g.BTBGClient = 'Y') GROUP BY v.MBMGroupCompany ORDER BY COUNT(*) DESC", transaction: session.Transaction);
        var rows = await Repository.SearchAsync(session, new RecoveryFilter { Company = company });
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM genisis.WS_ClaimReport WHERE CLMPayableByMedix2M < 0 AND MBMGroupCompany = @company", new { company }), rows.Count);
        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.True(r.Recovery < 0));
        Assert.Equal(rows.OrderBy(r => r.CaseNo, StringComparer.Ordinal).ThenBy(r => r.ClaimVersion).Select(r => (r.CaseNo, r.ClaimVersion)).Count(), rows.Count);

        var first = rows.First(r => r.BordxNo is not null && r.ClaimOpenDate is not null);
        var one = await Repository.SearchAsync(session, new RecoveryFilter { Company = company, BordxFrom = first.BordxNo, BordxTo = first.BordxNo });
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM genisis.WS_ClaimReport WHERE CLMPayableByMedix2M < 0 AND MBMGroupCompany = @company AND BordxRefNo >= @b AND BordxRefNo <= @b", new { company, b = first.BordxNo }), one.Count);
        Assert.All(one, r => Assert.Equal(first.BordxNo, r.BordxNo));

        var opened = first.ClaimOpenDate!.Value.Date;
        var byOpen = await Repository.SearchAsync(session, new RecoveryFilter { Company = company, OpenFrom = opened, OpenTo = opened });
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM genisis.WS_ClaimReport WHERE CLMPayableByMedix2M < 0 AND MBMGroupCompany = @company AND ClaimOpenDate >= @d AND ClaimOpenDate <= @d", new { company, d = opened }), byOpen.Count);
        Assert.Empty(await Repository.SearchAsync(session, new RecoveryFilter { Company = company, BordxDateFrom = new DateTime(2099, 1, 1) }));
    }

    [Fact]
    public async Task The_service_totals_the_rows_and_names_the_layout()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        var service = new PrintRecoveryService(TestDb.Sessions(), Repository);
        var mdec = (await service.CompaniesAsync()).First(c => c.ReportType == "2");
        var result = await service.SearchAsync(new RecoveryFilter { Company = mdec.Name });
        Assert.Equal("2", result.ReportType);
        Assert.Equal(result.Rows.Sum(r => r.Recovery ?? 0), result.TotalRecovery);
        Assert.True(result.TotalRecovery < 0);
        await Assert.ThrowsAsync<NotFoundException>(() => service.SearchAsync(new RecoveryFilter { Company = "NO SUCH COMPANY" }));
    }
}
