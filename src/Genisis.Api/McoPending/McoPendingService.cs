using System.Data;
using Genisis.Api.Data;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.McoPending;

public interface IMcoPendingService
{
    Task<McoPendingSearchResult> SearchAsync(McoPendingSearch search, CancellationToken cancellationToken);
    Task<McoPendingUpdateResult> UpdateAsync(McoPendingUpdate update, string userCode, CancellationToken cancellationToken);
}

/// <summary>Membership &gt; MCO Pending (legacy frmMBMMCOPending: MBMInquiryList, cmdUpdate_Click, UpdateMCOPending).</summary>
public sealed class McoPendingService(DbConnections db) : IMcoPendingService
{
    public async Task<McoPendingSearchResult> SearchAsync(McoPendingSearch search, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync(cancellationToken);
        var rows = await conn.ProcQueryAsync<McoPendingRow>("genisis.McoPending_Search", new
        {
            numberFrom = Text(search.NumberFrom), numberTo = Text(search.NumberTo), bordxFrom = search.BordxDateFrom?.Date,
            bordxTo = search.BordxDateTo?.Date, status = McoPendingValidator.Code(search.Status), limit = McoPendingLimits.SearchLimit + 1,
        });
        var truncated = rows.Count > McoPendingLimits.SearchLimit;
        if (truncated) rows.RemoveAt(rows.Count - 1);
        return new McoPendingSearchResult(rows, truncated);
    }

    public async Task<McoPendingUpdateResult> UpdateAsync(McoPendingUpdate update, string userCode, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync(cancellationToken);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var result = await UpdateAsync(conn, tx, update, userCode);
        await tx.CommitAsync(cancellationToken);
        return result;
    }

    /// <summary>Runs inside the caller's transaction (the tests roll it back). All checked rows are updated, or none.</summary>
    internal static async Task<McoPendingUpdateResult> UpdateAsync(SqlConnection conn, SqlTransaction tx, McoPendingUpdate update, string userCode)
    {
        var status = McoPendingValidator.Code(update.Status)!;
        var today = await conn.ProcSingleAsync<DateTime>("genisis.Server_Today", tx: tx);
        var updated = 0;
        foreach (var number in update.MembershipNos.Select(n => n.Trim()).Where(n => n != "").Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var current = await conn.ProcFirstOrDefaultAsync<McoPendingCurrent>("genisis.McoPending_Get", new { number }, tx)
                ?? throw new McoPendingException($"{McoPendingMessages.MembershipNotFound} ({number})");
            switch (McoPendingValidator.Decide(current.PendingExists, current.Status, status))
            {
                case McoPendingAction.Refuse:
                    throw new McoPendingException($"{McoPendingMessages.AlreadyBilled} ({current.MembershipNo})");
                case McoPendingAction.Insert:
                    await conn.ProcExecAsync("genisis.McoPending_Insert", new
                    {
                        number = current.MembershipNo, bordxDate = current.BordxDate, status, user = userCode, today,
                    }, tx);
                    updated++;
                    break;
                case McoPendingAction.Delete:
                    await conn.ProcExecAsync("genisis.McoPending_Delete", new { number = current.MembershipNo }, tx);
                    updated++;
                    break;
            }
        }
        return new McoPendingUpdateResult(McoPendingMessages.Updated, updated);
    }

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
