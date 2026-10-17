using FluentValidation;
using Genisis.Api.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Genisis.Api.Tests;

/// <summary>A unit of work that touches no database: services can be tested against fake repositories.</summary>
internal sealed class FakeSession(bool transactional) : IDbSession
{
    /// <summary>True once <see cref="CommitAsync"/> ran.</summary>
    public bool Committed { get; private set; }

    /// <summary>True when the session ended without a commit (rolled back or disposed).</summary>
    public bool RolledBack { get; private set; }

    public bool Transactional => transactional;

    public SqlConnection Connection { get; } = new();

    public SqlTransaction? Transaction => null;

    public Task CommitAsync()
    {
        Committed = true;
        return Task.CompletedTask;
    }

    public Task RollbackAsync()
    {
        RolledBack = !Committed;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await RollbackAsync();
        await Connection.DisposeAsync();
    }
}

/// <summary>Hands out <see cref="FakeSession"/>s and remembers them so a test can see what happened to each.</summary>
internal sealed class FakeSessions : IDbSessions
{
    public List<FakeSession> Sessions { get; } = [];

    public FakeSession Last => Sessions[^1];

    public Task<IDbSession> OpenAsync() => Task.FromResult(Add(new FakeSession(false)));

    public Task<IDbSession> BeginAsync() => Task.FromResult(Add(new FakeSession(true)));

    private IDbSession Add(FakeSession session)
    {
        Sessions.Add(session);
        return session;
    }
}

/// <summary>Connection settings of the database tests (skipped when GENISIS_TEST_MAINDB is not set).</summary>
internal static class TestDb
{
    public static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");
    public static readonly string? Maintenance = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINTENANCE");
    public static readonly string? WorkSheet = Environment.GetEnvironmentVariable("GENISIS_TEST_WORKSHEET");

    public static DbConnections Connections() => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:MainDb"] = MainDb,
        ["ConnectionStrings:Maintenance"] = Maintenance ?? MainDb,
        ["ConnectionStrings:WorkSheet"] = WorkSheet ?? MainDb,
    }).Build());

    public static IDbSessions Sessions() => new DbSessions(Connections());
}

internal static class ValidatorExtensions
{
    /// <summary>The first message the validator reports (what the API answers with), or null when the request is valid.</summary>
    public static string? FirstError<T>(this IValidator<T> validator, T request) => validator.Validate(request).Errors.FirstOrDefault()?.ErrorMessage;
}
