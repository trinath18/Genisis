using System.Data;
using Dapper;
using Genisis.Api.Data;

namespace Genisis.Api.Tracking;

/// <summary>Data access of the Tracking screen (genisis.Tracking_* procedures).</summary>
public interface ITrackingRepository
{
    Task<TrackingLookups> GetLookupsAsync(CancellationToken cancellationToken);

    /// <summary>Runs one MBMTracking case with the query's filters; rows keep the case's column names.</summary>
    Task<List<Dictionary<string, object?>>> RunCaseAsync(TrackingQuery query, int caseNumber, TrackingCaseOptions options, CancellationToken cancellationToken);
}

public sealed class TrackingRepository(DbConnections db) : ITrackingRepository
{
    public async Task<TrackingLookups> GetLookupsAsync(CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        using var grid = await conn.QueryMultipleAsync(new CommandDefinition("genisis.Tracking_Lookups",
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
        return new TrackingLookups(
            (await grid.ReadAsync<PayorOption>()).ToList(),
            (await grid.ReadAsync<TrackingOption>()).ToList(),
            (await grid.ReadAsync<TrackingOption>()).ToList());
    }

    public async Task<List<Dictionary<string, object?>>> RunCaseAsync(TrackingQuery query, int caseNumber, TrackingCaseOptions options,
        CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        var rows = await conn.QueryAsync(new CommandDefinition("genisis.Tracking_Run", Parameters(query, caseNumber, options),
            commandType: CommandType.StoredProcedure, commandTimeout: TrackingLimits.CommandTimeoutSeconds, cancellationToken: cancellationToken));
        return RowMapper.ToDictionaries(rows);
    }

    /// <summary>Filter keys are the procedure's parameter names; ranges add From / To.</summary>
    private static DynamicParameters Parameters(TrackingQuery query, int caseNumber, TrackingCaseOptions options)
    {
        var p = new DynamicParameters();
        p.Add("case", caseNumber);
        foreach (var (key, value) in query.Values) p.Add(key, value);
        foreach (var (key, (from, to)) in query.Ranges)
        {
            p.Add(key + "From", from);
            p.Add(key + "To", to);
        }
        if (query.Blanks.Count > 0) p.Add("blanks", string.Join(",", query.Blanks));
        p.Add("memberTypeFilter", options.MemberTypeFilter);
        p.Add("extra", options.CountFilter);
        p.Add("withSuppRegDate", options.WithSupplementaryRegistrationDate);
        return p;
    }
}
