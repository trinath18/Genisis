using System.Data;
using Dapper;
using Genisis.Api.Data;
using static Genisis.Api.Data.RowMapper;

namespace Genisis.Api.Membership;

public sealed record BenefitSchedules(List<Dictionary<string, object?>> Principal, List<Dictionary<string, object?>> Supplementary);

public sealed record PolicyWordingList(string? Wording, IReadOnlyList<string> Files, bool FolderFound);

/// <summary>Enquiry tabs added to the web screen (HIS Benefits, Acc History, Fees, Non Disclosure, Policy Terms, LT Limit, T/O Claims, WS, Policy Wording).</summary>
public interface IEnquiryTabsService
{
    /// <returns><c>null</c> when the membership does not exist (same for the other members returning a nullable).</returns>
    Task<BenefitSchedules?> GetBenefitsAsync(string membershipNo, CancellationToken cancellationToken);
    Task<List<Dictionary<string, object?>>?> GetAccountHistoryAsync(string membershipNo, CancellationToken cancellationToken);
    Task<Dictionary<string, object?>?> GetFeesAsync(string membershipNo, CancellationToken cancellationToken);
    Task<List<Dictionary<string, object?>>?> GetNonDisclosureAsync(string membershipNo, CancellationToken cancellationToken);
    /// <returns><c>null</c> when the membership does not exist; a result with a <c>null</c> row when its plan has no terms.</returns>
    Task<(bool Found, Dictionary<string, object?>? Terms)> GetPolicyTermsAsync(string membershipNo, CancellationToken cancellationToken);
    Task<List<Dictionary<string, object?>>> GetLifetimeLimitsAsync(string membershipNo, CancellationToken cancellationToken);
    Task<List<Dictionary<string, object?>>?> GetTakeoverClaimsAsync(string membershipNo, CancellationToken cancellationToken);
    Task<List<Dictionary<string, object?>>> GetWorksheetsAsync(string membershipNo, CancellationToken cancellationToken);
    Task<PolicyWordingList> GetPolicyWordingAsync(string membershipNo, CancellationToken cancellationToken);
    Task<PolicyWordingFile?> FindPolicyWordingFileAsync(string membershipNo, string fileName, CancellationToken cancellationToken);
}

public sealed class EnquiryTabsService(DbConnections db, IPolicyWordingStore wordingStore) : IEnquiryTabsService
{
    private static class Columns
    {
        public const string Plan = "PLNCode";
        public const string Health = "HLTCode";
        public const string Insured = "INSCode";
        public const string Payor = "PAYCode";
        public const string Policy = "MBMPolicyNo";
        public const string PayorEffectiveDate = "MBMPayorEffDate";
        public const string SupplementaryPlan = "MBMCPLNCode";
    }

    private static string? Text(Dictionary<string, object?> row, string column) =>
        row.TryGetValue(column, out var value) ? value?.ToString()?.Trim() : null;

    /// <summary>The desktop's member query (SearchMBMCovPersonsProc): the principal row first, one row per covered person.</summary>
    private async Task<List<Dictionary<string, object?>>> MemberRowsAsync(string membershipNo, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        return ToDictionaries(await conn.QueryAsync(new CommandDefinition("SearchMBMCovPersonsProc", new { MBMNumber = membershipNo.Trim() },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken)));
    }

    private async Task<string?> GetIcAsync(string membershipNo)
    {
        await using var conn = db.MainDb();
        return await conn.ProcScalarAsync<string?>("genisis.Membership_GetIc", new { mbmNumber = membershipNo.Trim() });
    }

    private async Task<List<Dictionary<string, object?>>> QueryAsync(string procedure, object parameters)
    {
        await using var conn = db.MainDb();
        return ToDictionaries(await conn.ProcQueryAsync<dynamic>(procedure, parameters));
    }

    /// <summary>The principal's benefit schedule, plus the supplementaries' schedule when they are on another plan.</summary>
    public async Task<BenefitSchedules?> GetBenefitsAsync(string membershipNo, CancellationToken cancellationToken)
    {
        var rows = await MemberRowsAsync(membershipNo, cancellationToken);
        if (rows.Count == 0) return null;
        var principalRow = rows[0];
        var plan = Text(principalRow, Columns.Plan);
        var health = Text(principalRow, Columns.Health);
        var insured = Text(principalRow, Columns.Insured);
        if (string.IsNullOrEmpty(plan) || principalRow.GetValueOrDefault(Columns.PayorEffectiveDate) is not DateTime effective) return new BenefitSchedules([], []);

        Task<List<Dictionary<string, object?>>> Schedule(string planCode) =>
            QueryAsync("genisis.Enquiry_Benefits", new { plan = planCode, hlt = health, ins = insured, eff = effective });
        var principal = await Schedule(plan);
        var supplementaryPlan = rows.Select(r => Text(r, Columns.SupplementaryPlan)).FirstOrDefault(p => !string.IsNullOrEmpty(p)) ?? plan;
        var supplementary = !InsuredTypes.IsFamily(insured) ? [] : supplementaryPlan == plan ? principal : await Schedule(supplementaryPlan);
        return new BenefitSchedules(principal, supplementary);
    }

    public async Task<List<Dictionary<string, object?>>?> GetAccountHistoryAsync(string membershipNo, CancellationToken cancellationToken) =>
        await GetIcAsync(membershipNo) is { } ic ? await QueryAsync("genisis.Enquiry_AccountHistory", new { ic }) : null;

    public async Task<Dictionary<string, object?>?> GetFeesAsync(string membershipNo, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        var row = await conn.ProcFirstOrDefaultAsync<dynamic>("genisis.Enquiry_Fees", new { number = membershipNo.Trim() });
        return row is null ? null : ToDictionary(row);
    }

    public async Task<List<Dictionary<string, object?>>?> GetNonDisclosureAsync(string membershipNo, CancellationToken cancellationToken) =>
        await GetIcAsync(membershipNo) is { } ic ? await QueryAsync("genisis.Enquiry_NonDisclosure", new { ic }) : null;

    public async Task<(bool Found, Dictionary<string, object?>? Terms)> GetPolicyTermsAsync(string membershipNo, CancellationToken cancellationToken)
    {
        var rows = await MemberRowsAsync(membershipNo, cancellationToken);
        if (rows.Count == 0) return (false, null);
        var principal = rows[0];
        await using var conn = db.MainDb();
        var row = await conn.ProcFirstOrDefaultAsync<dynamic>("genisis.Enquiry_PolicyTerms",
            new { plan = Text(principal, Columns.Plan), hlt = Text(principal, Columns.Health), pay = Text(principal, Columns.Payor) });
        return (true, row is null ? null : ToDictionary(row));
    }

    public Task<List<Dictionary<string, object?>>> GetLifetimeLimitsAsync(string membershipNo, CancellationToken cancellationToken) =>
        QueryAsync("genisis.Enquiry_LifetimeLimits", new { number = membershipNo.Trim() });

    public async Task<List<Dictionary<string, object?>>?> GetTakeoverClaimsAsync(string membershipNo, CancellationToken cancellationToken)
    {
        var rows = await MemberRowsAsync(membershipNo, cancellationToken);
        return rows.Count == 0 ? null : await QueryAsync("genisis.Enquiry_TakeoverClaims", new { policy = Text(rows[0], Columns.Policy) });
    }

    public async Task<List<Dictionary<string, object?>>> GetWorksheetsAsync(string membershipNo, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        return ToDictionaries(await conn.QueryAsync(new CommandDefinition("SearchWorksheet", new { MBMNumber = membershipNo.Trim() },
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken)));
    }

    private async Task<string?> WordingAsync(string membershipNo)
    {
        await using var conn = db.MainDb();
        return (await conn.ProcScalarAsync<string?>("genisis.Enquiry_PolicyWording", new { number = membershipNo.Trim() }))?.Trim();
    }

    public async Task<PolicyWordingList> GetPolicyWordingAsync(string membershipNo, CancellationToken cancellationToken)
    {
        var wording = await WordingAsync(membershipNo);
        if (string.IsNullOrEmpty(wording)) return new PolicyWordingList(null, [], false);
        var (files, found) = wordingStore.ListFiles(wording);
        return new PolicyWordingList(wording, files, found);
    }

    public async Task<PolicyWordingFile?> FindPolicyWordingFileAsync(string membershipNo, string fileName, CancellationToken cancellationToken) =>
        await WordingAsync(membershipNo) is { Length: > 0 } wording ? wordingStore.Find(wording, fileName) : null;
}
