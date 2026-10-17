using Genisis.Api.Data;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Mma;

public interface IMmaService
{
    Task<MmaProcedureSearchResult> SearchProceduresAsync(string? bodyPart, string? code, CancellationToken cancellationToken);
    /// <returns>The procedure's row index.</returns>
    Task<int> SaveProcedureAsync(int? index, MmaProcedureRequest request, string userCode, CancellationToken cancellationToken);
    Task DeleteProcedureAsync(int index, CancellationToken cancellationToken);
    Task<List<MmaFeeRow>> SearchFeesAsync(string? surgeonCategory, string? anaesthetistCategory, CancellationToken cancellationToken);
    /// <returns>The fee row's index.</returns>
    Task<int> SaveFeeAsync(int? index, MmaFeeRequest request, string userCode, CancellationToken cancellationToken);
    Task DeleteFeeAsync(int index, CancellationToken cancellationToken);
    Task<ScheduleMove> MoveScheduleAsync(string? code, ScheduleDirection direction, CancellationToken cancellationToken);
}

/// <summary>Maintenance &gt; Entity (legacy frmMMA: Procedures and Look Up Table tabs) and Maintenance &gt; Accounts
/// (legacy frmMMA13thSchDesc). The tables are in the maintenance database, reached through genisis.Maint_* synonyms.</summary>
public sealed class MmaService(DbConnections db) : IMmaService
{
    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? Like(string? value) => Text(value) is { } text ? SqlText.EscapeLike(text) : null;

    // ---------- Procedures tab ----------

    public async Task<MmaProcedureSearchResult> SearchProceduresAsync(string? bodyPart, string? code, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        var rows = await conn.ProcQueryAsync<MmaProcedureRow>("genisis.MmaProcedure_Search",
            new { top = MmaLimits.SearchRows + 1, bodyPart = Like(bodyPart), code = Like(code) });
        var truncated = rows.Count > MmaLimits.SearchRows;
        return new MmaProcedureSearchResult(truncated ? rows[..MmaLimits.SearchRows] : rows, truncated, MmaLimits.SearchRows);
    }

    public Task<int> SaveProcedureAsync(int? index, MmaProcedureRequest request, string userCode, CancellationToken cancellationToken) =>
        InTransactionAsync((conn, tx) => SaveProcedureAsync(conn, tx, index, request, userCode), cancellationToken);

    public Task DeleteProcedureAsync(int index, CancellationToken cancellationToken) =>
        InTransactionAsync(async (conn, tx) => { await DeleteProcedureAsync(conn, tx, index); return 0; }, cancellationToken);

    internal static async Task<int> SaveProcedureAsync(SqlConnection conn, SqlTransaction tx, int? index, MmaProcedureRequest request, string userCode)
    {
        var (result, saved) = await conn.ProcSingleAsync<(int Result, int Index)>("genisis.MmaProcedure_Save", new
        {
            index,
            bodyPart = Text(request.BodyPart),
            code = Text(request.Code),
            narrative = Text(request.Narrative),
            surgeonCategory = Text(request.SurgeonCategory),
            anaesthetistCategory = Text(request.AnaesthetistCategory),
            pageNo = Text(request.PageNo),
            partACost = Text(request.PartACost),
            part = Text(request.Part)?.ToUpperInvariant(),
            userCode,
        }, tx);
        if (result == StoredProcedures.NotFound) throw new MmaException(MmaMessages.NotFound);
        return saved;
    }

    internal static async Task DeleteProcedureAsync(SqlConnection conn, SqlTransaction tx, int index)
    {
        if (await conn.ProcScalarAsync<int>("genisis.MmaProcedure_Delete", new { index }, tx) == StoredProcedures.NotFound)
            throw new MmaException(MmaMessages.NotFound);
    }

    // ---------- Look Up Table tab ----------

    public async Task<List<MmaFeeRow>> SearchFeesAsync(string? surgeonCategory, string? anaesthetistCategory, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        return await conn.ProcQueryAsync<MmaFeeRow>("genisis.MmaFee_Search",
            new { surgeonCategory = Like(surgeonCategory), anaesthetistCategory = Like(anaesthetistCategory) });
    }

    public Task<int> SaveFeeAsync(int? index, MmaFeeRequest request, string userCode, CancellationToken cancellationToken) =>
        InTransactionAsync((conn, tx) => SaveFeeAsync(conn, tx, index, request, userCode), cancellationToken);

    public Task DeleteFeeAsync(int index, CancellationToken cancellationToken) =>
        InTransactionAsync(async (conn, tx) => { await DeleteFeeAsync(conn, tx, index); return 0; }, cancellationToken);

    internal static async Task<int> SaveFeeAsync(SqlConnection conn, SqlTransaction tx, int? index, MmaFeeRequest request, string userCode)
    {
        var (result, saved) = await conn.ProcSingleAsync<(int Result, int Index)>("genisis.MmaFee_Save", new
        {
            index,
            surgeonCategory = Text(request.SurgeonCategory),
            surgeonFee = request.SurgeonFee,
            anaesthetistCategory = Text(request.AnaesthetistCategory),
            anaesthetistFee = request.AnaesthetistFee,
            userCode,
        }, tx);
        if (result == StoredProcedures.NotFound) throw new MmaException(MmaMessages.NotFound);
        return saved;
    }

    internal static async Task DeleteFeeAsync(SqlConnection conn, SqlTransaction tx, int index)
    {
        switch (await conn.ProcScalarAsync<int>("genisis.MmaFee_Delete", new { index }, tx))
        {
            case StoredProcedures.NotFound: throw new MmaException(MmaMessages.NotFound);
            case StoredProcedures.Conflict: throw new MmaException(MmaMessages.FeeInUse);
        }
    }

    // ---------- Accounts (MMA and 13th Schedule Description) ----------

    /// <summary>Next / previous code (first / last when the code is empty), or the first code at or after it; the desktop message at either end.</summary>
    public async Task<ScheduleMove> MoveScheduleAsync(string? code, ScheduleDirection direction, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        var row = await conn.ProcFirstOrDefaultAsync<MmaScheduleRow>("genisis.MmaSchedule_Move", new { code = Text(code), direction = (int)direction });
        var message = row is not null ? null : direction switch
        {
            ScheduleDirection.Previous => MmaMessages.FirstRecord,
            ScheduleDirection.Next => MmaMessages.LastRecord,
            _ => MmaMessages.NoCode,
        };
        return new ScheduleMove(row, message);
    }

    private async Task<T> InTransactionAsync<T>(Func<SqlConnection, SqlTransaction, Task<T>> work, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync(cancellationToken);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(cancellationToken);
        var result = await work(conn, tx);
        await tx.CommitAsync(cancellationToken);
        return result;
    }
}
