using Dapper;
using Genisis.Api.Case.PanelHospitals;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Maintenance.PanelHospitals;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Maintenance &gt; Panel Hospital Maintenance: the validators and the service, against fake repositories.</summary>
public class PanelHospitalMaintenanceRulesTests
{
    private sealed class FakePanels : IPanelHospitalsRepository
    {
        public Task<List<PanelCode>> GetCodesAsync(IDbSession session) => Task.FromResult(new List<PanelCode> { new() { Code = "AXA1", Description = "AXA panel" } });

        public Task<List<PanelHospital>> GetHospitalsAsync(IDbSession session, string code) => Task.FromResult(new List<PanelHospital> { new() { Code = "H1", Name = "Hospital 1" } });
    }

    private sealed class FakeRepository : IPanelHospitalMaintenanceRepository
    {
        public List<string> Calls { get; } = [];

        public Dictionary<string, int> AddResults { get; } = [];

        public Task<List<AvailableHospital>> GetAvailableAsync(IDbSession session, string code) => Task.FromResult(new List<AvailableHospital> { new() { Code = "H2", Name = "Hospital 2" } });

        public Task<int> AddAsync(IDbSession session, string code, string hospital, string userCode, DateTime today)
        {
            Calls.Add($"add {code} {hospital} {userCode} {today:yyyy-MM-dd}");
            return Task.FromResult(AddResults.GetValueOrDefault(hospital));
        }

        public Task<int> RemoveAsync(IDbSession session, string code, string hospital)
        {
            Calls.Add($"remove {code} {hospital}");
            return Task.FromResult(1);
        }
    }

    private static PanelHospitalMaintenanceService Service(FakeRepository repository, FakeSessions sessions) => new(sessions, new FakePanels(), repository, NullLogger<PanelHospitalMaintenanceService>.Instance);

    [Fact]
    public void A_panel_code_has_to_be_chosen_to_search_add_or_update()
    {
        Assert.Equal("Please Select Hospital Panel Code!", new PanelHospitalMaintenanceQueryValidator().FirstError(new PanelHospitalMaintenanceQuery()));
        Assert.Null(new PanelHospitalMaintenanceQueryValidator().FirstError(new PanelHospitalMaintenanceQuery { Code = "AXA1" }));
        var validator = new PanelHospitalMaintenanceRequestValidator();
        Assert.Equal("Please Select Hospital Panel Code!", validator.FirstError(new PanelHospitalMaintenanceRequest { Code = " " }));
        Assert.Null(validator.FirstError(new PanelHospitalMaintenanceRequest { Code = "AXA1" }));
        Assert.Equal("A hospital cannot be added and removed in the same update.", validator.FirstError(new PanelHospitalMaintenanceRequest { Code = "AXA1", Add = ["h1"], Remove = ["H1"] }));
        Assert.Equal("Select valid hospitals.", validator.FirstError(new PanelHospitalMaintenanceRequest { Code = "AXA1", Add = [" "] }));
        Assert.Equal("Select valid hospitals.", validator.FirstError(new PanelHospitalMaintenanceRequest { Code = "AXA1", Remove = ["ABCDEFGHIJK"] }));
    }

    [Fact]
    public async Task Update_adds_and_removes_each_hospital_once_in_one_transaction()
    {
        var repository = new FakeRepository();
        var sessions = new FakeSessions();
        var result = await Service(repository, sessions).UpdateAsync(new PanelHospitalMaintenanceRequest { Code = " AXA1 ", Add = ["H2", "h2", "H3"], Remove = ["H1", "H1"] }, "USER1");
        Assert.Equal(new PanelHospitalMaintenanceResult(2, 1), result);
        Assert.Equal(["add AXA1 H2 USER1 " + DateTime.Today.ToString("yyyy-MM-dd"), "add AXA1 H3 USER1 " + DateTime.Today.ToString("yyyy-MM-dd"), "remove AXA1 H1"], repository.Calls);
        Assert.True(sessions.Last.Committed);
    }

    [Fact]
    public async Task An_unknown_hospital_or_panel_code_refuses_the_whole_update()
    {
        var repository = new FakeRepository();
        repository.AddResults["H9"] = 1;
        var sessions = new FakeSessions();
        var error = await Assert.ThrowsAsync<NotFoundException>(() => Service(repository, sessions).UpdateAsync(new PanelHospitalMaintenanceRequest { Code = "AXA1", Add = ["H2", "H9"], Remove = ["H1"] }, "U"));
        Assert.Equal("Hospital H9 not found.", error.Message);
        Assert.False(sessions.Last.Committed);
        Assert.DoesNotContain("remove AXA1 H1", repository.Calls);
        repository.AddResults["H2"] = 2;
        Assert.Equal("Hospital Panel Code not found.", (await Assert.ThrowsAsync<NotFoundException>(() => Service(repository, new FakeSessions()).UpdateAsync(new PanelHospitalMaintenanceRequest { Code = "ZZ", Add = ["H2"] }, "U"))).Message);
    }

    [Fact]
    public async Task The_lists_are_the_codes_the_hospitals_of_the_panel_and_the_hospitals_that_can_be_added()
    {
        var service = Service(new FakeRepository(), new FakeSessions());
        Assert.Equal("AXA1", (await service.GetCodesAsync())[0].Code);
        Assert.Equal("H1", (await service.GetHospitalsAsync(" AXA1 "))[0].Code);
        Assert.Equal("H2", (await service.GetAvailableAsync("AXA1"))[0].Code);
    }
}

/// <summary>Maintenance &gt; Panel Hospital Maintenance against the database. Every write is made in a session that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class PanelHospitalMaintenanceDatabaseTests
{
    private static PanelHospitalMaintenanceRepository Repository => new();

    private const string Checksum = "SELECT CHECKSUM_AGG(CHECKSUM(HosPanelCode, HOSCode, PayCode, HLTType, Description, LastUpdateUSR, LastUpdateDate, PlanCodes, MBMGroupCompany)) FROM dbo.PanelHospitals";

    [Fact]
    public async Task A_hospital_is_removed_from_a_panel_and_put_back_with_the_user_and_day_and_nothing_survives_the_rollback()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var before = await read.Connection.ExecuteScalarAsync<int?>(Checksum, transaction: read.Transaction);
        var panel = await read.Connection.QueryFirstAsync("SELECT TOP (1) RTRIM(p.HosPanelCode) AS Code, RTRIM(p.HOSCode) AS Hospital FROM dbo.PanelHospitals p INNER JOIN genisis.Maint_HosPanelCode c ON c.HosPanelCode = p.HosPanelCode INNER JOIN dbo.HOS h ON h.HOSCode = p.HOSCode ORDER BY p.HosPanelCode, p.HOSCode", transaction: read.Transaction);
        string code = panel.Code, hospital = panel.Hospital;

        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var members = await new Genisis.Api.Case.PanelHospitals.PanelHospitalsRepository().GetHospitalsAsync(write, code);
            Assert.Contains(members, m => m.Code == hospital);
            Assert.DoesNotContain(await Repository.GetAvailableAsync(write, code), a => a.Code == hospital);

            Assert.Equal(1, await Repository.RemoveAsync(write, code, hospital));
            Assert.Equal(0, await Repository.RemoveAsync(write, code, hospital));
            Assert.DoesNotContain(await new Genisis.Api.Case.PanelHospitals.PanelHospitalsRepository().GetHospitalsAsync(write, code), m => m.Code == hospital);
            Assert.Contains(await Repository.GetAvailableAsync(write, code), a => a.Code == hospital);

            Assert.Equal(0, await Repository.AddAsync(write, code, hospital, "ZZTEST", new DateTime(2026, 10, 29)));
            var added = await write.Connection.QuerySingleAsync("SELECT RTRIM(LastUpdateUSR) AS UserCode, LastUpdateDate AS Day FROM dbo.PanelHospitals WHERE HosPanelCode = @code AND HOSCode = @hospital", new { code, hospital }, write.Transaction);
            Assert.Equal(("ZZTEST", new DateTime(2026, 10, 29)), ((string)added.UserCode, (DateTime)added.Day));
            Assert.Equal(3, await Repository.AddAsync(write, code, hospital, "ZZTWO", new DateTime(2026, 10, 30)));
            Assert.Equal(1, await write.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.PanelHospitals WHERE HosPanelCode = @code AND HOSCode = @hospital AND LastUpdateUSR = 'ZZTWO'", new { code, hospital }, write.Transaction));
            Assert.Equal(1, await write.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.PanelHospitals WHERE HosPanelCode = @code AND HOSCode = @hospital", new { code, hospital }, write.Transaction));
        }

        Assert.Equal(before, await read.Connection.ExecuteScalarAsync<int?>(Checksum, transaction: read.Transaction));
    }

    [Fact]
    public async Task An_unknown_hospital_or_panel_code_is_refused_by_the_procedure()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var write = await TestDb.Sessions().BeginAsync();
        var code = await write.Connection.ExecuteScalarAsync<string>("SELECT TOP (1) RTRIM(p.HosPanelCode) FROM dbo.PanelHospitals p INNER JOIN genisis.Maint_HosPanelCode c ON c.HosPanelCode = p.HosPanelCode ORDER BY p.HosPanelCode", transaction: write.Transaction);
        Assert.Equal(1, await Repository.AddAsync(write, code!, "NO-SUCH", "ZZTEST", DateTime.Today));
        Assert.Equal(2, await Repository.AddAsync(write, "NO-SUCH-PANEL-CODE", "H1", "ZZTEST", DateTime.Today));
        Assert.Equal(0, await Repository.RemoveAsync(write, code!, "NO-SUCH"));
    }

    [Fact]
    public async Task The_service_updates_a_panel_in_one_transaction_that_a_failed_hospital_rolls_back()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        var service = new PanelHospitalMaintenanceService(TestDb.Sessions(), new Genisis.Api.Case.PanelHospitals.PanelHospitalsRepository(), Repository, NullLogger<PanelHospitalMaintenanceService>.Instance);
        await using var read = await TestDb.Sessions().OpenAsync();
        var before = await read.Connection.ExecuteScalarAsync<int?>(Checksum, transaction: read.Transaction);
        var panel = await read.Connection.QueryFirstAsync("SELECT TOP (1) RTRIM(p.HosPanelCode) AS Code, RTRIM(p.HOSCode) AS Hospital FROM dbo.PanelHospitals p INNER JOIN genisis.Maint_HosPanelCode c ON c.HosPanelCode = p.HosPanelCode INNER JOIN dbo.HOS h ON h.HOSCode = p.HOSCode ORDER BY p.HosPanelCode, p.HOSCode", transaction: read.Transaction);
        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(new PanelHospitalMaintenanceRequest { Code = panel.Code, Remove = [(string)panel.Hospital], Add = ["NO-SUCH"] }, "ZZTEST"));
        Assert.Equal(before, await read.Connection.ExecuteScalarAsync<int?>(Checksum, transaction: read.Transaction));
    }
}
