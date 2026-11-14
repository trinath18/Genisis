using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Maintenance.SurgicalSchedule;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Maintenance > Surgical Schedule (rebuilt on dbo.PLNSurgOperation): the checks and the service, against a fake repository.</summary>
public class SurgicalScheduleRulesTests
{
    private sealed class FakeRepository(int saveResult = 7, int deleteResult = 0, bool planExists = true) : ISurgicalScheduleRepository
    {
        public List<string> Calls { get; } = [];

        public Task<List<SurgicalSchedulePayor>> GetPayorsAsync(IDbSession session) => Task.FromResult(new List<SurgicalSchedulePayor> { new() { Code = "AX", Name = "AXA" } });

        public Task<SurgicalSchedulePlan?> GetPlanAsync(IDbSession session, string payor, string plan) =>
            Task.FromResult(planExists ? new SurgicalSchedulePlan { Payor = payor, PlanCode = plan, Description = "Plan" } : null);

        public Task<List<SurgicalScheduleRow>> GetRowsAsync(IDbSession session, string payor, string plan) => Task.FromResult(new List<SurgicalScheduleRow> { new() { Index = 1 } });

        public Task<int> SaveAsync(IDbSession session, int? index, string payor, string plan, string main, string? sub, int percentage)
        {
            Calls.Add($"save {index} {payor} {plan} {main} {sub} {percentage}");
            return Task.FromResult(saveResult);
        }

        public Task<int> DeleteAsync(IDbSession session, int index, string payor, string plan)
        {
            Calls.Add($"delete {index} {payor} {plan}");
            return Task.FromResult(deleteResult);
        }
    }

    private static SurgicalScheduleService Service(FakeSessions sessions, FakeRepository repository) => new(sessions, repository, NullLogger<SurgicalScheduleService>.Instance);

    private static SurgicalScheduleRequest Request(string? main = "MAJOR", int? percentage = 80) => new() { Payor = " ax ", Plan = " a1 ", MainCategory = main, SubCategory = "  ", Percentage = percentage };

    [Fact]
    public void A_search_needs_a_payor_and_then_a_plan()
    {
        var validator = new SurgicalScheduleQueryValidator();
        Assert.Equal("Please Select Payor!", validator.FirstError(new SurgicalScheduleQuery()));
        Assert.Equal("Please Enter Plan Code!", validator.FirstError(new SurgicalScheduleQuery { Payor = "AX" }));
        Assert.Null(validator.FirstError(new SurgicalScheduleQuery { Payor = "AX", Plan = "A1" }));
    }

    [Fact]
    public void A_row_needs_a_main_category_that_fits_and_a_percentage_from_0_to_100()
    {
        var validator = new SurgicalScheduleRequestValidator();
        Assert.Equal("Please Enter Surgical Main Category!", validator.FirstError(Request(main: " ")));
        Assert.Equal("Surgical Main Category can have at most 50 characters.", validator.FirstError(Request(main: new string('X', 51))));
        Assert.Equal("Surgical Sub Category can have at most 200 characters.", validator.FirstError(new SurgicalScheduleRequest { Payor = "AX", Plan = "A1", MainCategory = "M", SubCategory = new string('X', 201), Percentage = 1 }));
        Assert.Equal("Please Enter Percentage!", validator.FirstError(Request(percentage: null)));
        Assert.Equal("Percentage must be from 0 to 100.", validator.FirstError(Request(percentage: 101)));
        Assert.Null(validator.FirstError(Request(percentage: 0)));
    }

    [Fact]
    public async Task A_save_sends_the_trimmed_upper_case_key_and_commits()
    {
        var sessions = new FakeSessions();
        var repository = new FakeRepository();
        Assert.Equal(7, await Service(sessions, repository).SaveAsync(null, Request(), "ZZTEST"));
        Assert.Equal(["save  AX A1 MAJOR  80"], repository.Calls);
        Assert.True(sessions.Last.Committed);
    }

    [Theory]
    [InlineData(-1, "The row was not found. Search again.")]
    [InlineData(-2, "Plan A1 of payor AX was not found.")]
    [InlineData(-3, "The plan already has this Surgical Main Category and Sub Category.")]
    public async Task A_refused_save_says_why_and_commits_nothing(int result, string message)
    {
        var sessions = new FakeSessions();
        var error = await Assert.ThrowsAnyAsync<BusinessRuleException>(() => Service(sessions, new FakeRepository(saveResult: result)).SaveAsync(3, Request(), "ZZTEST"));
        Assert.Equal(message, error.Message);
        Assert.False(sessions.Last.Committed);
    }

    [Fact]
    public async Task An_unknown_plan_or_row_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Service(new FakeSessions(), new FakeRepository(planExists: false)).GetAsync(new SurgicalScheduleQuery { Payor = "AX", Plan = "ZZ" }));
        var sessions = new FakeSessions();
        await Assert.ThrowsAsync<NotFoundException>(() => Service(sessions, new FakeRepository(deleteResult: 1)).DeleteAsync(9, new SurgicalScheduleQuery { Payor = "AX", Plan = "A1" }, "ZZTEST"));
        Assert.False(sessions.Last.Committed);
    }
}

/// <summary>Surgical Schedule against the database: every write runs in a transaction that is rolled back.</summary>
public class SurgicalScheduleDatabaseTests
{
    private static SurgicalScheduleRepository Repository => new();

    [Fact]
    public async Task Rows_are_added_listed_changed_and_deleted_for_a_real_plan_only()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var session = await TestDb.Sessions().BeginAsync();
        var (pay, plan) = await session.Connection.QueryFirstAsync<(string, string)>("SELECT TOP (1) RTRIM(PAYCode), RTRIM(PLNCode) FROM dbo.PLN WHERE PAYCode = 'AX' ORDER BY PLNCode", transaction: session.Transaction);
        Assert.Contains(await Repository.GetPayorsAsync(session), p => p.Code == "AX");
        Assert.Equal(plan, (await Repository.GetPlanAsync(session, pay, plan))!.PlanCode);
        Assert.Null(await Repository.GetPlanAsync(session, pay, "ZZZZ"));
        var before = (await Repository.GetRowsAsync(session, pay, plan)).Count;

        var index = await Repository.SaveAsync(session, null, pay, plan, "ZZ MAJOR", "ZZ SUB", 80);
        Assert.True(index > 0);
        Assert.Equal(-3, await Repository.SaveAsync(session, null, pay, plan, "ZZ MAJOR", "ZZ SUB", 70));
        Assert.Equal(-2, await Repository.SaveAsync(session, null, pay, "ZZZZ", "ZZ MAJOR", null, 70));
        var row = Assert.Single(await Repository.GetRowsAsync(session, pay, plan), r => r.Index == index);
        Assert.Equal(("ZZ MAJOR", "ZZ SUB", 80), (row.MainCategory, row.SubCategory, row.Percentage));

        Assert.Equal(index, await Repository.SaveAsync(session, index, pay, plan, "ZZ MINOR", null, 50));
        row = Assert.Single(await Repository.GetRowsAsync(session, pay, plan), r => r.Index == index);
        Assert.Equal(("ZZ MINOR", (string?)null, 50), (row.MainCategory, row.SubCategory, row.Percentage));
        var otherPlan = await session.Connection.QueryFirstAsync<string>("SELECT TOP (1) RTRIM(PLNCode) FROM dbo.PLN WHERE PAYCode = @pay AND PLNCode <> @plan ORDER BY PLNCode", new { pay, plan }, session.Transaction);
        Assert.Equal(-1, await Repository.SaveAsync(session, index, pay, otherPlan, "ZZ MINOR", null, 50));

        Assert.Equal(StoredProcedures.NotFound, await Repository.DeleteAsync(session, index, "ZZ", plan));
        Assert.Equal(StoredProcedures.Ok, await Repository.DeleteAsync(session, index, pay, plan));
        Assert.Equal(before, (await Repository.GetRowsAsync(session, pay, plan)).Count);
    }
}
