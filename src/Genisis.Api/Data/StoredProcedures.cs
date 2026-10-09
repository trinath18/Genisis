using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Data;

/// <summary>Calls to the genisis.* stored procedures (scripts in sql/). Guarded writes return a Result column.</summary>
public static class StoredProcedures
{
    public const int Ok = 0, NotFound = 1, Conflict = 2;

    /// <summary>SQL Server error 2812: the procedure has not been deployed to this database.</summary>
    public const int MissingProcedure = 2812;

    public static Task<int> ProcExecAsync(this SqlConnection conn, string name, object? p = null, SqlTransaction? tx = null) =>
        conn.ExecuteAsync(name, p, tx, commandType: CommandType.StoredProcedure);

    public static Task<T?> ProcFirstOrDefaultAsync<T>(this SqlConnection conn, string name, object? p = null, SqlTransaction? tx = null) =>
        conn.QueryFirstOrDefaultAsync<T>(name, p, tx, commandType: CommandType.StoredProcedure);

    public static Task<T?> ProcScalarAsync<T>(this SqlConnection conn, string name, object? p = null, SqlTransaction? tx = null) =>
        conn.ExecuteScalarAsync<T>(name, p, tx, commandType: CommandType.StoredProcedure);

    public static Task<T> ProcSingleAsync<T>(this SqlConnection conn, string name, object? p = null, SqlTransaction? tx = null) =>
        conn.QuerySingleAsync<T>(name, p, tx, commandType: CommandType.StoredProcedure);

    public static async Task<List<T>> ProcQueryAsync<T>(this SqlConnection conn, string name, object? p = null, SqlTransaction? tx = null) =>
        (await conn.QueryAsync<T>(name, p, tx, commandType: CommandType.StoredProcedure)).ToList();

    public static Task<SqlMapper.GridReader> ProcMultipleAsync(this SqlConnection conn, string name, object? p = null, SqlTransaction? tx = null) =>
        conn.QueryMultipleAsync(name, p, tx, commandType: CommandType.StoredProcedure);
}
