using Microsoft.Data.SqlClient;

namespace Genisis.Api.Data;

/// <summary>
/// One unit of work on the main database: a connection and, for writes, a transaction. Repositories run their commands on it;
/// services decide where it begins and ends. Disposing a session that was not committed rolls it back.
/// </summary>
public interface IDbSession : IAsyncDisposable
{
    /// <summary>The ADO.NET connection repositories run their commands on.</summary>
    SqlConnection Connection { get; }

    /// <summary>The open transaction, or null for a read-only session.</summary>
    SqlTransaction? Transaction { get; }

    /// <summary>Commits the transaction (no-op for a read-only session).</summary>
    Task CommitAsync();

    /// <summary>Rolls the transaction back (no-op for a read-only session or once finished).</summary>
    Task RollbackAsync();
}

/// <summary>Creates <see cref="IDbSession"/>s on the main database. Other databases are reached through synonyms (see sql/README.md).</summary>
public interface IDbSessions
{
    /// <summary>A read-only session.</summary>
    Task<IDbSession> OpenAsync();

    /// <summary>A session with a transaction; roll back on dispose unless <see cref="IDbSession.CommitAsync"/> was called.</summary>
    Task<IDbSession> BeginAsync();
}

/// <inheritdoc />
public sealed class DbSessions(DbConnections db) : IDbSessions
{
    /// <inheritdoc />
    public async Task<IDbSession> OpenAsync()
    {
        var connection = db.MainDb();
        await connection.OpenAsync();
        return new DbSession(connection, null);
    }

    /// <inheritdoc />
    public async Task<IDbSession> BeginAsync()
    {
        var connection = db.MainDb();
        await connection.OpenAsync();
        return new DbSession(connection, (SqlTransaction)await connection.BeginTransactionAsync());
    }

    private sealed class DbSession(SqlConnection connection, SqlTransaction? transaction) : IDbSession
    {
        private bool _finished;

        public SqlConnection Connection => connection;
        public SqlTransaction? Transaction => transaction;

        public async Task CommitAsync()
        {
            if (transaction is null || _finished) return;
            _finished = true;
            await transaction.CommitAsync();
        }

        public async Task RollbackAsync()
        {
            if (transaction is null || _finished) return;
            _finished = true;
            await transaction.RollbackAsync();
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await RollbackAsync();
            }
            catch (InvalidOperationException)
            {
                // The connection already failed; the server has rolled the transaction back and the original error is the one to report.
            }
            finally
            {
                if (transaction is not null) await transaction.DisposeAsync();
                await connection.DisposeAsync();
            }
        }
    }
}
