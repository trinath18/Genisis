using Genisis.Api.Data;

namespace Genisis.Api.Worksheet.Engine;

/// <summary>The available limit the patient's claims draw on, and the plan's annual limit type that decided whose it is.</summary>
public record BalanceLimit
{
    /// <summary>The available limit now; <c>null</c> when the covered person has none.</summary>
    public decimal? Balance { get; set; }

    /// <summary>AnnualLimit.SuppLimitStatus: A, B or C; empty when the plan has no annual limit row.</summary>
    public string? LimitType { get; set; }
}

/// <summary>Rules of the limit before a worksheet, checked against the saved <c>CLMPreviousBalance</c>.</summary>
public static class BalanceLimitRules
{
    /// <summary>
    /// The limit before the worksheet. Approving a worksheet takes its approved amount off the available limit, so the limit before it is the available limit plus what the case has
    /// approved already (nothing for a case that has no approved version). Over the saved worksheets this gives the saved value to the cent for 76 per cent of the principals' latest cases
    /// and 83 to 85 per cent of the dependants'; the rest are cases whose limit was changed after the claim (renewal, adjustment, a claim approved but not taken off).
    /// </summary>
    public static decimal PreviousBalance(decimal? balance, decimal approvedByTheCase) => (balance ?? 0m) + approvedByTheCase;
}

/// <summary>Reads the available limit of a saved worksheet version's patient.</summary>
public interface IBalanceLimitRepository
{
    /// <summary>The balance limit, or <c>null</c> when the version has no member.</summary>
    Task<BalanceLimit?> BalanceAsync(IDbSession session, string caseNo, int version);
}

/// <inheritdoc />
public sealed class BalanceLimitRepository : IBalanceLimitRepository
{
    /// <inheritdoc />
    public Task<BalanceLimit?> BalanceAsync(IDbSession session, string caseNo, int version) =>
        session.Connection.ProcFirstOrDefaultAsync<BalanceLimit>("genisis.WorksheetEngine_BalanceLimit", new { cas = caseNo, version }, session.Transaction);
}

/// <summary>Worksheet calculation engine, step 5b: the limit before a worksheet.</summary>
public class BalanceLimitService(IDbSessions sessions, IBalanceLimitRepository repository)
{
    /// <summary>The patient's available limit; <c>null</c> when there is no member to take it from.</summary>
    public async Task<BalanceLimit?> BalanceAsync(string caseNo, int version)
    {
        await using var session = await sessions.OpenAsync();
        return await repository.BalanceAsync(session, caseNo.Trim(), version);
    }
}
