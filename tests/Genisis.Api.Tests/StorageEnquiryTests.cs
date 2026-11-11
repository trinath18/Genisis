using Dapper;
using Genisis.Api.ClaimBordx.Storage;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Tests;

/// <summary>Claim Bordx > Storage > Enquiry: the validators and the service, against a fake repository.</summary>
public class StorageEnquiryRulesTests
{
    private sealed class FakeRepository : IStorageEnquiryRepository
    {
        public Task<StorageLookups> LookupsAsync(IDbSession session) => Task.FromResult(new StorageLookups(["C1"], ["USER1"]));

        public Task<List<StorageCartonRow>> CartonsAsync(IDbSession session, StorageCartonFilter filter) => Task.FromResult(Enumerable.Range(0, 5002).Select(i => new StorageCartonRow { CartonNo = "C" + i }).ToList());

        public Task<List<StorageFileRow>> FilesAsync(IDbSession session, StorageFileFilter filter) => Task.FromResult(new List<StorageFileRow> { new() { CaseNo = "AB1", ClaimVersion = 1 } });

        public Task<List<StorageCartonFile>> CartonFilesAsync(IDbSession session, string carton) => Task.FromResult(new List<StorageCartonFile> { new() { CaseNo = carton } });
    }

    private static StorageEnquiryService Service() => new(new FakeSessions(), new FakeRepository());

    [Fact]
    public void A_scan_needs_a_case_number_and_a_range_does_not()
    {
        var validator = new StorageFileFilterValidator();
        Assert.Equal("Please Enter Case No", validator.FirstError(new StorageFileFilter { UseScan = true }));
        Assert.Null(validator.FirstError(new StorageFileFilter { UseScan = true, CaseFrom = "AB1" }));
        Assert.Null(validator.FirstError(new StorageFileFilter()));
    }

    [Theory]
    [InlineData("I", true)]
    [InlineData("o", true)]
    [InlineData("", true)]
    [InlineData("X", false)]
    public void The_file_status_is_in_or_out(string status, bool valid) => Assert.Equal(valid, new StorageFileFilterValidator().FirstError(new StorageFileFilter { FileStatus = status }) is null);

    [Theory]
    [InlineData("M", true)]
    [InlineData("s", true)]
    [InlineData("R", true)]
    [InlineData("", true)]
    [InlineData("Z", false)]
    public void The_carton_status_is_medix_store_or_return(string status, bool valid) => Assert.Equal(valid, new StorageCartonFilterValidator().FirstError(new StorageCartonFilter { Status = status }) is null);

    [Fact]
    public async Task A_search_returns_at_most_five_thousand_rows_and_says_when_it_cut_the_list()
    {
        var result = await Service().CartonsAsync(new StorageCartonFilter());
        Assert.Equal((5000, true), (result.Rows.Count, result.Truncated));
        Assert.False((await Service().FilesAsync(new StorageFileFilter())).Truncated);
    }

    [Fact]
    public async Task The_carton_listing_needs_a_carton_number()
    {
        var failure = await Assert.ThrowsAsync<BusinessRuleException>(() => Service().CartonFilesAsync(" "));
        Assert.Equal("Please select Carton No.", failure.Message);
        Assert.Single(await Service().CartonFilesAsync("C1"));
    }
}

/// <summary>Storage Enquiry against the database (read only).</summary>
public class StorageEnquiryDatabaseTests
{
    private static StorageEnquiryRepository Repository => new();

    private static async Task<int> Count(IDbSession s, string sql, object? args = null) => await s.Connection.ExecuteScalarAsync<int>(sql, args, s.Transaction);

    [Fact]
    public async Task The_lookups_are_the_distinct_cartons_and_the_user_names()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var lookups = await Repository.LookupsAsync(session);
        Assert.Equal(await Count(session, "SELECT COUNT(DISTINCT RTRIM(CRTCartonNo)) FROM genisis.WS_Carton"), lookups.Cartons.Count);
        Assert.Equal(lookups.Cartons.Distinct().Count(), lookups.Cartons.Count);
        Assert.True(lookups.Users.Count > 0);
    }

    [Fact]
    public async Task The_carton_search_gives_what_the_table_holds_for_each_criterion()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        Assert.Equal(WorksheetLimitsFetch(await Count(session, "SELECT COUNT(*) FROM genisis.WS_Carton")), (await Repository.CartonsAsync(session, new StorageCartonFilter())).Count);
        foreach (var status in new[] { "M", "S", "R" })
            Assert.Equal(await Count(session, "SELECT COUNT(*) FROM genisis.WS_Carton WHERE CRTCartonStatus = @status", new { status }), (await Repository.CartonsAsync(session, new StorageCartonFilter { Status = status.ToLowerInvariant() })).Count);

        var one = await session.Connection.QueryFirstAsync<string>("SELECT TOP (1) RTRIM(CRTCartonNo) FROM genisis.WS_Carton WHERE CRTCartonNo > '0' ORDER BY CRTCartonNo DESC", transaction: session.Transaction);
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM genisis.WS_Carton WHERE CRTCartonNo >= @one AND CRTCartonNo <= @one", new { one }), (await Repository.CartonsAsync(session, new StorageCartonFilter { CartonFrom = " " + one + " ", CartonTo = one })).Count);

        var dated = await Repository.CartonsAsync(session, new StorageCartonFilter { SendFrom = new DateTime(2019, 1, 1), SendTo = new DateTime(2019, 12, 31) });
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM genisis.WS_Carton WHERE DOSend >= '20190101' AND DOSend <= '20191231'"), dated.Count);
        Assert.All(dated, c => Assert.Equal(2019, c.SendDate!.Value.Year));
    }

    private static int WorksheetLimitsFetch(int count) => Math.Min(count, Genisis.Api.Worksheet.WorksheetLimits.FetchRows);

    [Fact]
    public async Task The_file_search_gives_what_the_view_holds_for_each_criterion_and_a_scan_finds_one_case()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().OpenAsync();
        var sample = await session.Connection.QueryFirstAsync<(string Cas, string Carton)>("SELECT TOP (1) RTRIM(CASNumber), RTRIM(STRCartonNo) FROM genisis.WS_StorageClaim WHERE STRCartonNo IS NOT NULL ORDER BY CASNumber DESC", transaction: session.Transaction);

        var scan = await Repository.FilesAsync(session, new StorageFileFilter { UseScan = true, CaseFrom = sample.Cas.ToLowerInvariant(), CaseTo = "ignored" });
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM (SELECT DISTINCT CASNumber, ClaimVersion, STRCartonNo, STRFileStatus, STRRequestBy, DORequest, DODelivery, DOReturn, STRRemarks, LastUpdateDate FROM genisis.WS_StorageClaim WHERE STRCartonNo IS NOT NULL AND CASNumber = @cas) x", new { cas = sample.Cas }), scan.Count);
        Assert.All(scan, f => Assert.Equal(sample.Cas, f.CaseNo));

        var inCarton = await Repository.FilesAsync(session, new StorageFileFilter { CartonFrom = sample.Carton, CartonTo = sample.Carton, FileStatus = "i" });
        Assert.All(inCarton, f => Assert.Equal(("I", sample.Carton), (f.FileStatus, f.CartonNo)));
        var carton = await Repository.CartonFilesAsync(session, sample.Carton.ToLowerInvariant());
        Assert.Equal(await Count(session, "SELECT COUNT(*) FROM genisis.WS_Storage WHERE STRCartonNo = @carton", new { carton = sample.Carton }), carton.Count);

        var byPayor = await Repository.FilesAsync(session, new StorageFileFilter { PayorFrom = sample.Cas[..2] + " - A PAYOR", PayorTo = sample.Cas[..2] });
        Assert.NotEmpty(byPayor);
        Assert.All(byPayor, f => Assert.Equal(sample.Cas[..2], f.CaseNo[..2]));
        var sorted = byPayor.OrderBy(f => f.CaseNo, StringComparer.Ordinal).ThenBy(f => f.ClaimVersion).Select(f => (f.CaseNo, f.ClaimVersion));
        Assert.Equal(sorted, byPayor.Select(f => (f.CaseNo, f.ClaimVersion)));
        Assert.All(byPayor.Where(f => f.ReturnDate is not null && f.DeliveryDate is not null), f => Assert.Equal((int)(f.ReturnDate!.Value.Date - f.DeliveryDate!.Value.Date).TotalDays, f.Duration));
    }
}
