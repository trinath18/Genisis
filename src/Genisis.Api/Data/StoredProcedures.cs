using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Data;

/// <summary>Calls to the genisis.* stored procedures (scripts in sql/). Guarded writes return a Result column.</summary>
public static class StoredProcedures
{
    /// <summary>The write was done.</summary>
    public const int Ok = 0;

    /// <summary>The record asked for does not exist.</summary>
    public const int NotFound = 1;

    /// <summary>The write was refused: the record exists already or is still in use.</summary>
    public const int Conflict = 2;

    /// <summary>SQL Server error 2812: the procedure has not been deployed to this database.</summary>
    public const int MissingProcedure = 2812;

    /// <summary>Executes a procedure and returns the rows affected.</summary>
    public static Task<int> ProcExecAsync(this SqlConnection conn, string name, object? p = null, SqlTransaction? tx = null) =>
        conn.ExecuteAsync(name, p, tx, commandType: CommandType.StoredProcedure);

    /// <summary>The first row of a procedure's result, or default.</summary>
    public static Task<T?> ProcFirstOrDefaultAsync<T>(this SqlConnection conn, string name, object? p = null, SqlTransaction? tx = null) =>
        conn.QueryFirstOrDefaultAsync<T>(name, p, tx, commandType: CommandType.StoredProcedure);

    /// <summary>The first column of the first row of a procedure's result.</summary>
    public static Task<T?> ProcScalarAsync<T>(this SqlConnection conn, string name, object? p = null, SqlTransaction? tx = null) =>
        conn.ExecuteScalarAsync<T>(name, p, tx, commandType: CommandType.StoredProcedure);

    /// <summary>The one row a procedure returns (throws when there is not exactly one).</summary>
    public static Task<T> ProcSingleAsync<T>(this SqlConnection conn, string name, object? p = null, SqlTransaction? tx = null) =>
        conn.QuerySingleAsync<T>(name, p, tx, commandType: CommandType.StoredProcedure);

    /// <summary>Every row of a procedure's result.</summary>
    public static async Task<List<T>> ProcQueryAsync<T>(this SqlConnection conn, string name, object? p = null, SqlTransaction? tx = null) =>
        (await conn.QueryAsync<T>(name, p, tx, commandType: CommandType.StoredProcedure)).ToList();

    /// <summary>A procedure that returns several result sets; dispose the reader when done.</summary>
    public static Task<SqlMapper.GridReader> ProcMultipleAsync(this SqlConnection conn, string name, object? p = null, SqlTransaction? tx = null) =>
        conn.QueryMultipleAsync(name, p, tx, commandType: CommandType.StoredProcedure);
}
