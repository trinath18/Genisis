using Genisis.Api.Data;
using Genisis.Api.Worksheet;
using Genisis.Api.Worksheet.SendToAccounts;
using Genisis.Api.Worksheet.SendToAr;
using Genisis.Api.Worksheet.Tracking;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>The rules, validators and services of Worksheet Tracking, Send to A/C and Send to A/R, without a database.</summary>
public class WorksheetScreensTests
{
    // ---------------- shared ----------------

    [Fact]
    public void Search_results_are_capped_and_say_so()
    {
        var small = WorksheetLimits.Cap(Enumerable.Range(0, WorksheetLimits.MaxRows).ToList());
        Assert.False(small.Truncated);
        Assert.Equal(WorksheetLimits.MaxRows, small.Rows.Count);

        var big = WorksheetLimits.Cap(Enumerable.Range(0, WorksheetLimits.FetchRows).ToList());
        Assert.True(big.Truncated);
        Assert.Equal(WorksheetLimits.MaxRows, big.Rows.Count);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(" AC001 ", "AC001")]
    public void Blank_criteria_are_no_criteria(string? value, string? expected) => Assert.Equal(expected, WorksheetLimits.Text(value));

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("0", true)]
    [InlineData(" 2 ", true)]
    [InlineData("3", false)]
    [InlineData("Both", false)]
    public void Token_status_is_blank_or_one_of_the_three_choices(string? value, bool valid) => Assert.Equal(valid, WorksheetLimits.IsTokenStatus(value));

    [Fact]
    public void A_pick_shows_the_wks_no_as_case_dash_version() => Assert.Equal("AC001-3", new WorksheetPick(" AC001 ", 3).WksNo);

    // ---------------- Worksheet Tracking: rules and validators ----------------

    [Theory]
    [InlineData(null, null)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(42, 42)]
    public void A_worksheet_closed_the_day_it_was_registered_counts_as_one_day(int? raw, int? shown) => Assert.Equal(shown, TrackingRules.DaysOpen(raw));

    [Fact]
    public void Tracking_search_in_scan_mode_needs_a_worksheet_number()
    {
        var validator = new TrackingFilterValidator();
        Assert.Equal("Please Enter Worksheet No", validator.FirstError(new TrackingFilter { UseScan = true, WksNoFrom = "  " }));
        Assert.Null(validator.FirstError(new TrackingFilter { UseScan = true, WksNoFrom = "AC001" }));
        Assert.Null(validator.FirstError(new TrackingFilter { UseScan = false }));
    }

    [Theory]
    [InlineData("A", "B", true)]
    [InlineData("R", "P", true)]
    [InlineData(null, null, true)]
    [InlineData("", "", true)]
    [InlineData("X", "B", false)]
    [InlineData("A", "N", false)]
    public void Tracking_drop_down_values_must_be_the_ones_offered(string? claimability, string? pending, bool valid)
    {
        var error = new TrackingFilterValidator().FirstError(new TrackingFilter { UseScan = false, Claimability = claimability, Pending = pending });
        Assert.Equal(valid, error is null);
    }

    [Fact]
    public void Tracking_update_reports_the_desktops_messages_in_order()
    {
        var validator = new TrackingUpdateRequestValidator();
        var items = new List<WorksheetPick> { new("AC001", 1) };
        Assert.Equal("Please Enter Date", validator.FirstError(new TrackingUpdateRequest { Status = "P", Items = items }));
        Assert.Equal("Please Enter Pending Status", validator.FirstError(new TrackingUpdateRequest { Date = DateTime.Today, Items = items }));
        Assert.Equal("Please Enter Pending Status", validator.FirstError(new TrackingUpdateRequest { Date = DateTime.Today, Status = "B", Items = items }));
        Assert.Equal("Please select at least one record to be updated!", validator.FirstError(new TrackingUpdateRequest { Date = DateTime.Today, Status = "C" }));
        Assert.Null(validator.FirstError(new TrackingUpdateRequest { Date = DateTime.Today, Status = " p ", Items = items }));
    }

    // ---------------- Worksheet Tracking: service ----------------

    private sealed class FakeTrackingRepository : ITrackingRepository
    {
        public List<TrackingRow> Rows = [];
        public List<string> Companies = [];
        public Dictionary<string, int> Results = [];
        public List<(string Wks, DateTime Date, string Status, string User)> Updated = [];
        public string? CompaniesFor;

        public Task<List<TrackingRow>> SearchAsync(IDbSession session, TrackingFilter filter) => Task.FromResult(Rows);

        public Task<List<string>> GroupCompaniesAsync(IDbSession session, string payor)
        {
            CompaniesFor = payor;
            return Task.FromResult(Companies);
        }

        public Task<int> UpdateAsync(IDbSession session, WorksheetPick pick, DateTime date, string status, string userCode)
        {
            var result = Results.GetValueOrDefault(pick.WksNo, StoredProcedures.Ok);
            if (result == StoredProcedures.Ok) Updated.Add((pick.WksNo, date, status, userCode));
            return Task.FromResult(result);
        }
    }

    private static TrackingService TrackingService(FakeSessions sessions, FakeTrackingRepository repository) => new(sessions, repository, NullLogger<TrackingService>.Instance);

    [Fact]
    public async Task Tracking_search_applies_the_day_count_rule_and_reports_a_cut_off()
    {
        var repository = new FakeTrackingRepository
        {
            Rows = Enumerable.Range(0, WorksheetLimits.FetchRows).Select(i => new TrackingRow { CaseNo = $"AC{i:00000}", DaysOpen = i == 0 ? 0 : 5 }).ToList(),
        };
        var sessions = new FakeSessions();
        var result = await TrackingService(sessions, repository).SearchAsync(new TrackingFilter());

        Assert.True(result.Truncated);
        Assert.Equal(WorksheetLimits.MaxRows, result.Rows.Count);
        Assert.Equal(1, result.Rows[0].DaysOpen);
        Assert.Equal(5, result.Rows[1].DaysOpen);
        Assert.All(sessions.Sessions, s => Assert.False(s.Transactional));
    }

    [Fact]
    public async Task Tracking_group_companies_need_a_payor_and_use_a_trimmed_code()
    {
        var repository = new FakeTrackingRepository { Companies = ["ACME"] };
        var service = TrackingService(new FakeSessions(), repository);
        Assert.Empty(await service.GroupCompaniesAsync("  "));
        Assert.Null(repository.CompaniesFor);
        Assert.Equal(["ACME"], await service.GroupCompaniesAsync(" AX "));
        Assert.Equal("AX", repository.CompaniesFor);
    }

    [Fact]
    public async Task Tracking_update_saves_every_ticked_worksheet_once_in_one_transaction()
    {
        var repository = new FakeTrackingRepository();
        var sessions = new FakeSessions();
        var request = new TrackingUpdateRequest
        {
            Date = new DateTime(2026, 10, 18, 14, 30, 0), Status = " p ",
            Items = [new("AC001", 1), new(" AC001 ", 1), new("AC002", 2)],
        };
        var count = await TrackingService(sessions, repository).UpdateAsync(request, "TESTER");

        Assert.Equal(2, count);
        Assert.Equal([("AC001-1", request.Date.Value, "P", "TESTER"), ("AC002-2", request.Date.Value, "P", "TESTER")], repository.Updated);
        Assert.True(sessions.Last.Transactional);
        Assert.True(sessions.Last.Committed);
    }

    [Fact]
    public async Task Tracking_update_refuses_a_bordxed_worksheet_and_saves_nothing()
    {
        var repository = new FakeTrackingRepository { Results = { ["AC002-1"] = StoredProcedures.Conflict } };
        var sessions = new FakeSessions();
        var request = new TrackingUpdateRequest { Date = DateTime.Today, Status = "C", Items = [new("AC001", 1), new("AC002", 1)] };
        var error = await Assert.ThrowsAsync<WorksheetException>(() => TrackingService(sessions, repository).UpdateAsync(request, "TESTER"));

        Assert.Equal("AC002-1: This claim has been bordxed!", error.Message);
        Assert.False(sessions.Last.Committed);
        Assert.True(sessions.Last.RolledBack);
    }

    [Fact]
    public async Task Tracking_update_says_which_worksheet_is_gone()
    {
        var repository = new FakeTrackingRepository { Results = { ["AC001-4"] = StoredProcedures.NotFound } };
        var request = new TrackingUpdateRequest { Date = DateTime.Today, Status = "P", Items = [new("AC001", 4)] };
        var error = await Assert.ThrowsAsync<WorksheetException>(() => TrackingService(new FakeSessions(), repository).UpdateAsync(request, "TESTER"));
        Assert.Equal("Worksheet AC001-4 was not found. Search again.", error.Message);
    }

    // ---------------- Send to A/C ----------------

    [Fact]
    public void Send_to_accounts_search_checks_in_the_desktops_order()
    {
        var validator = new SendToAccountsFilterValidator();
        Assert.Equal("Please Enter Worksheet No", validator.FirstError(new SendToAccountsFilter { UseScan = true, TokenStatus = "9" }));
        Assert.Equal("Select a valid token status.", validator.FirstError(new SendToAccountsFilter { UseScan = false, TokenStatus = "9" }));
        Assert.Null(validator.FirstError(new SendToAccountsFilter { UseScan = true, WksNoFrom = "AC001", TokenStatus = "0" }));
    }

    [Fact]
    public void Send_to_accounts_needs_a_date_then_a_ticked_worksheet()
    {
        var validator = new SendToAccountsRequestValidator();
        Assert.Equal("Please Enter in date", validator.FirstError(new SendToAccountsRequest { Items = [new("AC001", 1)] }));
        Assert.Equal("Please select at least one record to be updated!", validator.FirstError(new SendToAccountsRequest { Date = DateTime.Today }));
        Assert.Null(validator.FirstError(new SendToAccountsRequest { Date = DateTime.Today, Items = [new("AC001", 1)] }));
    }

    private sealed class FakeAccountsRepository : ISendToAccountsRepository
    {
        public Dictionary<string, int> Results = [];
        public List<string> Sent = [];

        public Task<List<SendToAccountsRow>> SearchAsync(IDbSession session, SendToAccountsFilter filter) => Task.FromResult(new List<SendToAccountsRow>());

        public Task<int> SendAsync(IDbSession session, WorksheetPick pick, DateTime date, string userCode)
        {
            var result = Results.GetValueOrDefault(pick.WksNo, StoredProcedures.Ok);
            if (result == StoredProcedures.Ok) Sent.Add($"{pick.WksNo}:{date:yyyy-MM-dd}:{userCode}");
            return Task.FromResult(result);
        }
    }

    private static SendToAccountsService AccountsService(FakeSessions sessions, FakeAccountsRepository repository) => new(sessions, repository, NullLogger<SendToAccountsService>.Instance);

    [Fact]
    public async Task Send_to_accounts_marks_each_worksheet_once_and_commits()
    {
        var repository = new FakeAccountsRepository();
        var sessions = new FakeSessions();
        var request = new SendToAccountsRequest { Date = new DateTime(2026, 10, 18), Items = [new("AC001", 1), new("AC001", 1), new("AC009", 2)] };
        Assert.Equal(2, await AccountsService(sessions, repository).SendAsync(request, "TESTER"));
        Assert.Equal(["AC001-1:2026-10-18:TESTER", "AC009-2:2026-10-18:TESTER"], repository.Sent);
        Assert.True(sessions.Last.Committed);
    }

    [Fact]
    public async Task Send_to_accounts_refuses_a_worksheet_that_was_already_sent_and_saves_nothing()
    {
        var repository = new FakeAccountsRepository { Results = { ["AC009-2"] = StoredProcedures.Conflict } };
        var sessions = new FakeSessions();
        var request = new SendToAccountsRequest { Date = DateTime.Today, Items = [new("AC001", 1), new("AC009", 2)] };
        var error = await Assert.ThrowsAsync<WorksheetException>(() => AccountsService(sessions, repository).SendAsync(request, "TESTER"));
        Assert.Equal("AC009-2: Record has been Updated!", error.Message);
        Assert.False(sessions.Last.Committed);
    }

    // ---------------- Send to A/R ----------------

    [Fact]
    public void Send_to_ar_search_names_the_case_number_in_its_message()
    {
        var validator = new SendToArFilterValidator();
        Assert.Equal("Please Enter Case No", validator.FirstError(new SendToArFilter { UseScan = true }));
        Assert.Equal("Select a valid token status.", validator.FirstError(new SendToArFilter { UseScan = false, TokenStatus = "7" }));
        Assert.Null(validator.FirstError(new SendToArFilter { UseScan = true, WksNoFrom = "AC001" }));
    }

    [Fact]
    public void Send_to_ar_needs_a_ticked_case() =>
        Assert.Equal("Please select a record before proceeding!", new SendToArRequestValidator().FirstError(new SendToArRequest()));

    private sealed class FakeArRepository : ISendToArRepository
    {
        public HashSet<string> Missing = [];
        public List<string> Sent = [];

        public Task<List<SendToArRow>> SearchAsync(IDbSession session, SendToArFilter filter) => Task.FromResult(new List<SendToArRow>());

        public Task<int> SendAsync(IDbSession session, string cas, string userCode)
        {
            if (Missing.Contains(cas)) return Task.FromResult(StoredProcedures.NotFound);
            Sent.Add($"{cas}:{userCode}");
            return Task.FromResult(StoredProcedures.Ok);
        }
    }

    [Fact]
    public async Task Send_to_ar_trims_and_de_duplicates_the_cases_and_commits()
    {
        var repository = new FakeArRepository();
        var sessions = new FakeSessions();
        var service = new SendToArService(sessions, repository, NullLogger<SendToArService>.Instance);
        Assert.Equal(2, await service.SendAsync(new SendToArRequest { Cases = [" AC001 ", "AC001", "", "AC002"] }, "TESTER"));
        Assert.Equal(["AC001:TESTER", "AC002:TESTER"], repository.Sent);
        Assert.True(sessions.Last.Committed);
    }

    [Fact]
    public async Task Send_to_ar_names_a_missing_case_and_saves_nothing()
    {
        var repository = new FakeArRepository { Missing = ["AC002"] };
        var sessions = new FakeSessions();
        var service = new SendToArService(sessions, repository, NullLogger<SendToArService>.Instance);
        var error = await Assert.ThrowsAsync<WorksheetException>(() => service.SendAsync(new SendToArRequest { Cases = ["AC001", "AC002"] }, "TESTER"));
        Assert.Equal("Case AC002 was not found. Search again.", error.Message);
        Assert.False(sessions.Last.Committed);
    }
}
