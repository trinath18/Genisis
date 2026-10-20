using Dapper;
using Genisis.Api.McoPending;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Tests;

public class McoPendingTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");

    [Theory]
    [InlineData(false, null, "P", McoPendingAction.Insert)]
    [InlineData(false, null, "R", McoPendingAction.Insert)]
    [InlineData(true, "P", "P", McoPendingAction.None)]
    [InlineData(true, "P", "R", McoPendingAction.Delete)]
    [InlineData(true, "B", "R", McoPendingAction.Refuse)]
    [InlineData(true, " b ", "P", McoPendingAction.Refuse)]
    public void Update_follows_update_mco_pending(bool exists, string? current, string status, McoPendingAction expected) =>
        Assert.Equal(expected, McoPendingValidator.Decide(exists, current, status));

    [Fact]
    public void Request_messages_follow_the_form()
    {
        Assert.Equal(McoPendingMessages.SelectStatus, McoPendingValidator.ValidateUpdate(new McoPendingUpdate { MembershipNos = ["X"] }));
        Assert.Equal(McoPendingMessages.SelectStatus, McoPendingValidator.ValidateUpdate(new McoPendingUpdate { Status = "B", MembershipNos = ["X"] }));
        Assert.Equal(McoPendingMessages.SelectRecord, McoPendingValidator.ValidateUpdate(new McoPendingUpdate { Status = "p", MembershipNos = [" "] }));
        Assert.Null(McoPendingValidator.ValidateUpdate(new McoPendingUpdate { Status = "r", MembershipNos = ["X"] }));
        Assert.Null(McoPendingValidator.ValidateSearch(new McoPendingSearch { Status = "b" }));
        Assert.Null(McoPendingValidator.ValidateSearch(new McoPendingSearch()));
        Assert.Equal(McoPendingMessages.InvalidSearchStatus, McoPendingValidator.ValidateSearch(new McoPendingSearch { Status = "X" }));
    }

    [Fact]
    public async Task Pending_then_release_round_trip_is_rolled_back()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        await using var conn = new SqlConnection(MainDb);
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        var number = await conn.ExecuteScalarAsync<string?>("""
            SELECT TOP (1) RTRIM(x.MBMNumber) FROM genisis.Maint_MBMCrossReference x
            WHERE x.MBMBordxDate IS NOT NULL AND NOT EXISTS (SELECT 1 FROM genisis.Maint_MCOPending p WHERE p.MBMNumber = x.MBMNumber)
            ORDER BY x.MBMBordxDate DESC
            """, transaction: tx);
        if (number is null) return;
        string? Status() => conn.ExecuteScalar<string?>("SELECT RTRIM(Status) FROM genisis.Maint_MCOPending WHERE MBMNumber = @number", new { number }, tx);

        var pending = await McoPendingService.UpdateAsync(conn, tx, new McoPendingUpdate { Status = "P", MembershipNos = [number, number] }, "TEST");
        Assert.Equal((McoPendingMessages.Updated, 1), (pending.Message, pending.Updated));
        Assert.Equal("P", Status());
        Assert.Equal(await conn.ExecuteScalarAsync<DateTime>("SELECT MBMBordxDate FROM genisis.Maint_MBMCrossReference WHERE MBMNumber = @number", new { number }, tx),
            await conn.ExecuteScalarAsync<DateTime>("SELECT MBMBordxDate FROM genisis.Maint_MCOPending WHERE MBMNumber = @number", new { number }, tx));
        Assert.Equal(0, (await McoPendingService.UpdateAsync(conn, tx, new McoPendingUpdate { Status = "P", MembershipNos = [number] }, "TEST")).Updated);

        var found = await conn.QueryAsync<McoPendingRow>("genisis.McoPending_Search",
            new { numberFrom = number, numberTo = number, status = "P", limit = 10 }, tx, commandType: System.Data.CommandType.StoredProcedure);
        Assert.Equal("P", Assert.Single(found).Status);

        Assert.Equal(1, (await McoPendingService.UpdateAsync(conn, tx, new McoPendingUpdate { Status = "R", MembershipNos = [number] }, "TEST")).Updated);
        Assert.Null(Status());

        await conn.ExecuteAsync("INSERT INTO genisis.Maint_MCOPending (MBMNumber, Status) VALUES (@number, 'B')", new { number }, tx);
        var billed = await Assert.ThrowsAsync<McoPendingException>(() =>
            McoPendingService.UpdateAsync(conn, tx, new McoPendingUpdate { Status = "R", MembershipNos = [number] }, "TEST"));
        Assert.StartsWith(McoPendingMessages.AlreadyBilled, billed.Message);
        await Assert.ThrowsAsync<McoPendingException>(() =>
            McoPendingService.UpdateAsync(conn, tx, new McoPendingUpdate { Status = "P", MembershipNos = ["NO-SUCH-MEMBER"] }, "TEST"));

        // A pending row without a membership cross-reference is listed under its own number and can be released.
        const string orphan = "ZZ-TEST-ORPHAN";
        await conn.ExecuteAsync("INSERT INTO genisis.Maint_MCOPending (MBMNumber, MBMBordxDate, Status) VALUES (@orphan, '2020-01-01', 'P')", new { orphan }, tx);
        var orphanRow = Assert.Single(await conn.QueryAsync<McoPendingRow>("genisis.McoPending_Search",
            new { numberFrom = orphan, numberTo = orphan, limit = 10 }, tx, commandType: System.Data.CommandType.StoredProcedure));
        Assert.Equal((orphan, "P", new DateTime(2020, 1, 1)), (orphanRow.MembershipNo, orphanRow.Status, orphanRow.BordxDate!.Value));
        Assert.Equal(1, (await McoPendingService.UpdateAsync(conn, tx, new McoPendingUpdate { Status = "R", MembershipNos = [orphan] }, "TEST")).Updated);
        Assert.Equal(0, await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genisis.Maint_MCOPending WHERE MBMNumber = @orphan", new { orphan }, tx));
        await tx.RollbackAsync();
    }
}
