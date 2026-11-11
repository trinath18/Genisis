using Dapper;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Maintenance.MemberDates;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Maintenance > Member (Valid Date Range): the validator and the service, against a fake repository.</summary>
public class MemberDatesRulesTests
{
    private sealed class FakeRepository : IMemberDatesRepository
    {
        public List<(MemberDatesRequest Request, string User, DateTime Today)> Saved { get; } = [];

        public Task<MemberDatesRow> GetAsync(IDbSession session) => Task.FromResult(new MemberDatesRow { Found = true, EffectiveStart = new DateTime(1999, 1, 1) });

        public Task<int> SaveAsync(IDbSession session, MemberDatesRequest request, string userCode, DateTime today)
        {
            Saved.Add((request, userCode, today));
            return Task.FromResult(0);
        }
    }

    private static MemberDatesRequest Full() => new()
    {
        EffectiveStart = new DateTime(1999, 1, 1), EffectiveEnd = new DateTime(2021, 12, 31), ExpiryStart = new DateTime(2000, 12, 31), ExpiryEnd = new DateTime(2021, 12, 31),
        BirthStart = new DateTime(1920, 1, 1), BirthEnd = new DateTime(2021, 12, 31),
    };

    [Fact]
    public void The_six_dates_are_asked_for_in_the_desktops_order_one_message_at_a_time()
    {
        var validator = new MemberDatesRequestValidator();
        var request = new MemberDatesRequest();
        var expected = new[] { "Please Enter Effective Start Date", "Please Enter Effective End Date", "Please Enter Expiry Start Date", "Please Enter Expiry End Date", "Please Enter DOB Start Date", "Please Enter DOB End Date" };
        var fill = new Action[]
        {
            () => request.EffectiveStart = new DateTime(1999, 1, 1), () => request.EffectiveEnd = new DateTime(2021, 12, 31), () => request.ExpiryStart = new DateTime(2000, 12, 31),
            () => request.ExpiryEnd = new DateTime(2021, 12, 31), () => request.BirthStart = new DateTime(1920, 1, 1), () => request.BirthEnd = new DateTime(2021, 12, 31),
        };
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], validator.FirstError(request));
            fill[i]();
        }
        Assert.Null(validator.FirstError(request));
    }

    [Fact]
    public async Task Saving_commits_with_the_user_and_the_day()
    {
        var sessions = new FakeSessions();
        var repository = new FakeRepository();
        await new MemberDatesService(sessions, repository, NullLogger<MemberDatesService>.Instance).SaveAsync(Full(), "USER1");
        Assert.Equal(("USER1", DateTime.Today), (repository.Saved[0].User, repository.Saved[0].Today));
        Assert.True(sessions.Last.Committed);
    }

    [Fact]
    public async Task Loading_returns_the_saved_row()
    {
        var row = await new MemberDatesService(new FakeSessions(), new FakeRepository(), NullLogger<MemberDatesService>.Instance).GetAsync();
        Assert.True(row.Found);
    }
}

/// <summary>Maintenance > Member against the database. Every write is made in a session that is rolled back.</summary>
[Collection(DatabaseWriteCollection.Name)]
public class MemberDatesDatabaseTests
{
    private static MemberDatesRepository Repository => new();

    private static MemberDatesRequest Request(int year) => new()
    {
        EffectiveStart = new DateTime(year, 1, 1), EffectiveEnd = new DateTime(year, 2, 2), ExpiryStart = new DateTime(year, 3, 3), ExpiryEnd = new DateTime(year, 4, 4),
        BirthStart = new DateTime(year, 5, 5), BirthEnd = new DateTime(year, 6, 6),
    };

    private const string Checksum = "SELECT CHECKSUM_AGG(CHECKSUM(ValidID, MBMEffStart, MBMEffEnd, MBMExpStart, MBMExpEnd, MBMDOBStart, MBMDOBEnd, MBMValidDateLastUpdateUser, MBMValidDateLastUpdateDate)) FROM dbo.MBMValidDateRange";

    [Fact]
    public async Task The_saved_row_is_read_back_and_a_save_updates_that_row_and_stamps_the_user_and_day_and_nothing_survives_the_rollback()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var read = await TestDb.Sessions().OpenAsync();
        var before = await read.Connection.ExecuteScalarAsync<int>(Checksum, transaction: read.Transaction);
        var first = await Repository.GetAsync(read);
        var table = await read.Connection.QueryFirstAsync("SELECT TOP (1) MBMEffStart, MBMExpEnd, MBMDOBStart, RTRIM(MBMValidDateLastUpdateUser) AS UserCode FROM dbo.MBMValidDateRange ORDER BY ValidID", transaction: read.Transaction);
        Assert.Equal((true, (DateTime)table.MBMEffStart, (DateTime)table.MBMExpEnd, (DateTime)table.MBMDOBStart, (string)table.UserCode), (first.Found, first.EffectiveStart!.Value, first.ExpiryEnd!.Value, first.BirthStart!.Value, first.UpdatedBy));

        await using (var write = await TestDb.Sessions().BeginAsync())
        {
            var rows = await write.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.MBMValidDateRange", transaction: write.Transaction);
            Assert.Equal(0, await Repository.SaveAsync(write, Request(2030), "ZZTEST", new DateTime(2026, 10, 29, 12, 0, 0)));
            var saved = await Repository.GetAsync(write);
            Assert.Equal((new DateTime(2030, 1, 1), new DateTime(2030, 2, 2), new DateTime(2030, 3, 3), new DateTime(2030, 4, 4), new DateTime(2030, 5, 5), new DateTime(2030, 6, 6), "ZZTEST", new DateTime(2026, 10, 29)),
                (saved.EffectiveStart!.Value, saved.EffectiveEnd!.Value, saved.ExpiryStart!.Value, saved.ExpiryEnd!.Value, saved.BirthStart!.Value, saved.BirthEnd!.Value, saved.UpdatedBy, saved.UpdatedOn!.Value));
            Assert.Equal(rows, await write.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.MBMValidDateRange", transaction: write.Transaction));
        }

        Assert.Equal(before, await read.Connection.ExecuteScalarAsync<int>(Checksum, transaction: read.Transaction));
    }

    [Fact]
    public async Task An_empty_table_reads_as_not_found_and_a_save_adds_the_row()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        await using var write = await TestDb.Sessions().BeginAsync();
        await write.Connection.ExecuteAsync("DELETE FROM dbo.MBMValidDateRange", transaction: write.Transaction);
        Assert.False((await Repository.GetAsync(write)).Found);
        Assert.Equal(1, await Repository.SaveAsync(write, Request(2031), "ZZTEST", DateTime.Today));
        var added = await Repository.GetAsync(write);
        Assert.Equal((true, new DateTime(2031, 1, 1), "ZZTEST"), (added.Found, added.EffectiveStart!.Value, added.UpdatedBy));
        Assert.Equal(1, await write.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.MBMValidDateRange", transaction: write.Transaction));
    }
}
