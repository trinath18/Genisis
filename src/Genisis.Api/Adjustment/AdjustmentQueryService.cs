using Genisis.Api.Data;
using static Genisis.Api.Data.RowMapper;

namespace Genisis.Api.Adjustment;

public sealed record AdjustmentOption(string Code, string Name);
public sealed record AdjustmentPayorOption(string Code, string Name, string? HealthCode);

public sealed record AdjustmentLookups(IReadOnlyList<AdjustmentOption> HealthCodes, IReadOnlyList<AdjustmentPayorOption> Payors,
    IReadOnlyList<AdjustmentOption> InsuredTypes, IReadOnlyList<AdjustmentOption> Races, IReadOnlyList<AdjustmentOption> Nationalities,
    IReadOnlyList<AdjustmentOption> Relationships, IReadOnlyList<AdjustmentOption> States, IReadOnlyList<AdjustmentOption> Cities,
    IReadOnlyList<AdjustmentOption> EndorsementTypes);

/// <summary>Search by: membership no, name, IC or policy no (desktop frmSearchMBM).</summary>
public static class AdjustmentSearchFields
{
    public static readonly IReadOnlySet<string> All = new HashSet<string> { "number", "name", "ic", "policy" };
}

public interface IAdjustmentQueryService
{
    Task<AdjustmentLookups> GetLookupsAsync(CancellationToken cancellationToken);
    Task<List<AdjustmentOption>> GetPlansAsync(string healthCode, string? payorCode, CancellationToken cancellationToken);
    Task<List<Dictionary<string, object?>>> SearchAsync(string text, string field, CancellationToken cancellationToken);
    /// <returns>The next / previous membership number, or <c>null</c> at the first / last record.</returns>
    Task<string?> NavigateAsync(string from, bool previous, CancellationToken cancellationToken);
    Task<List<Dictionary<string, object?>>> GetAdjustmentHistoryAsync(string membershipNo, CancellationToken cancellationToken);
    Task<List<Dictionary<string, object?>>> GetMemberHistoryAsync(AdjustmentMember member, CancellationToken cancellationToken);
    Task<int> GetLastBatchAsync(DateTime bordxDate, string? payorCode, CancellationToken cancellationToken);
    /// <returns>Another membership already using the policy number, or <c>null</c>.</returns>
    Task<string?> FindPolicyOwnerAsync(string policyNo, string membershipNo, CancellationToken cancellationToken);
}

/// <summary>Read-only queries of the Adjustment screen (drop-downs, search, navigation, history).</summary>
public sealed class AdjustmentQueryService(DbConnections db) : IAdjustmentQueryService
{
    private const string PreviousDirection = "prev";

    public async Task<AdjustmentLookups> GetLookupsAsync(CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        using var grid = await conn.ProcMultipleAsync("genisis.Registration_Lookups");
        async Task<List<AdjustmentOption>> Options() => (await grid.ReadAsync<AdjustmentOption>()).ToList();
        var healthCodes = await Options();
        var payors = (await grid.ReadAsync<AdjustmentPayorOption>()).ToList();
        return new AdjustmentLookups(healthCodes, payors, await Options(), await Options(), await Options(), await Options(), await Options(), await Options(),
            EndorsementTypes.All.Select(t => new AdjustmentOption(t.Key, t.Value)).ToList());
    }

    public async Task<List<AdjustmentOption>> GetPlansAsync(string healthCode, string? payorCode, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        return await conn.ProcQueryAsync<AdjustmentOption>("genisis.Registration_Plans", new { healthCode = healthCode.Trim(), payorCode = (payorCode ?? "").Trim() });
    }

    public async Task<List<Dictionary<string, object?>>> SearchAsync(string text, string field, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        return ToDictionaries(await conn.ProcQueryAsync<dynamic>("genisis.Membership_Search",
            new { max = AdjustmentLimits.SearchMaximumRows, by = field, q = text, prefix = SqlText.EscapeLike(text) + "%" }));
    }

    public static bool IsPrevious(string? direction) => string.Equals(direction, PreviousDirection, StringComparison.OrdinalIgnoreCase);

    public async Task<string?> NavigateAsync(string from, bool previous, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        var number = await conn.ProcScalarAsync<string?>("genisis.Adjustment_Navigate", new { from = from.Trim(), direction = previous ? -1 : 1 });
        return number?.Trim();
    }

    public async Task<List<Dictionary<string, object?>>> GetAdjustmentHistoryAsync(string membershipNo, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        return ToDictionaries(await conn.ProcQueryAsync<dynamic>("genisis.Membership_Adjustments", new { mbmNumber = membershipNo.Trim() }));
    }

    public async Task<List<Dictionary<string, object?>>> GetMemberHistoryAsync(AdjustmentMember member, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        return ToDictionaries(await conn.ProcQueryAsync<dynamic>("genisis.Adjustment_History",
            new { ic = member.IcBcPp?.Trim(), name = member.Name?.Trim(), dob = member.DateOfBirth }));
    }

    public async Task<int> GetLastBatchAsync(DateTime bordxDate, string? payorCode, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        var payor = string.IsNullOrWhiteSpace(payorCode) ? null : payorCode.Trim();
        return await conn.ProcScalarAsync<int>("genisis.Adjustment_LastBatch", new { bordx = bordxDate.Date, pay = payor });
    }

    public async Task<string?> FindPolicyOwnerAsync(string policyNo, string membershipNo, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        var owner = await conn.ProcScalarAsync<string?>("genisis.Adjustment_PolicyOwner", new { policy = policyNo.Trim(), number = membershipNo.Trim() });
        return owner?.Trim();
    }
}
