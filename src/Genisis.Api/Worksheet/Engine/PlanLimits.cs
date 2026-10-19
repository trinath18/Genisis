using System.Globalization;
using Genisis.Api.Data;

namespace Genisis.Api.Worksheet.Engine;

/// <summary>One benefit row of the patient's plan that is in force on the admission date (<c>BNF</c>).</summary>
public record PlanBenefit
{
    /// <summary>Benefit code: 101 Room &amp; Board, 102 ICU, 103 Hospital Supplies &amp; Services, and so on.</summary>
    public string Code { get; set; } = "";

    /// <summary>Days the benefit allows (BNFNoOfDays); empty when it has no day limit.</summary>
    public string? Days { get; set; }

    /// <summary>The rate per day (BNFClaimAbleAmount): Room &amp; Board and ICU.</summary>
    public string? Claimable { get; set; }

    /// <summary>The limit as the plan gives it (BNFRMPerDis): <c>FULL</c> or an amount.</summary>
    public string? Limit { get; set; }
}

/// <summary>
/// The limit of each worksheet row as the plan gives it, ported from the Registration form's <c>display_benefit</c>. Only the rules the saved worksheets confirm are here (see
/// <see cref="Slots"/>); the rows whose limit also depends on the case (post-hospitalisation, kidney and cancer treatment, guardian, medical report, others) are left to the next step.
/// </summary>
public static class PlanLimitRules
{
    /// <summary>The benefit code of the rows whose limit is the plan's FULL / amount as it stands, and the worksheet row (slot) it fills.</summary>
    public static readonly IReadOnlyDictionary<string, int> Slots = new Dictionary<string, int>
    {
        ["103"] = 3, ["104"] = 4, ["201"] = 6, ["202"] = 7, ["203"] = 8, ["204"] = 9, ["301"] = 10, ["302"] = 11, ["401"] = 14, ["402"] = 15, ["403"] = 16, ["501"] = 18,
    };

    /// <summary>Worksheet row of Room &amp; Board.</summary>
    public const int RoomAndBoard = 1;

    /// <summary>Worksheet row of ICU.</summary>
    public const int Icu = 2;

    /// <summary>Worksheet row of the service tax on Room &amp; Board.</summary>
    public const int RoomAndBoardTax = 27;

    /// <summary>The service tax limit is this share of the Room &amp; Board limit (6 per cent, in 98.6 per cent of the saved worksheets that have one).</summary>
    public const decimal TaxRate = 0.06m;

    /// <summary>A limit as the worksheet shows it: thousands separated, two decimals.</summary>
    public static string Money(decimal value) => value.ToString("#,##0.00", CultureInfo.InvariantCulture);

    private static decimal? Number(string? text) => decimal.TryParse(text?.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>
    /// The limit text of each row the plan fixes, keyed by worksheet row. Room &amp; Board and ICU are the rate per day times the days of the stay (<paramref name="roomDays"/>,
    /// <paramref name="icuDays"/>), held to the days the plan allows when it has such a limit (no Room &amp; Board days: no limit); the service tax is a share of the Room &amp; Board limit.
    /// </summary>
    public static Dictionary<int, string> Derive(IEnumerable<PlanBenefit> benefits, int roomDays, int icuDays)
    {
        var limits = new Dictionary<int, string>();
        foreach (var b in benefits)
        {
            var code = b.Code.Trim();
            if (Slots.TryGetValue(code, out var slot) && !string.IsNullOrWhiteSpace(b.Limit)) limits[slot] = b.Limit.Trim();
            else if (code is "101" or "102" && Number(b.Claimable) is { } rate)
            {
                var days = code == "101" ? roomDays : icuDays;
                if (Number(b.Days) is { } allowed and > 0) days = Math.Min(days, (int)allowed);
                if (code == "101" && days <= 0) continue; // a stay with no Room & Board days shows no limit ("-"); ICU with none shows 0.00
                limits[code == "101" ? RoomAndBoard : Icu] = Money(rate * days);
            }
        }
        if (limits.TryGetValue(RoomAndBoard, out var room) && Number(room) is { } roomLimit and > 0) limits[RoomAndBoardTax] = Money(Math.Round(roomLimit * TaxRate, 2, MidpointRounding.AwayFromZero));
        return limits;
    }

    /// <summary>Whether a derived limit equals what the desktop saved: FULL is FULL, amounts are equal to the cent whatever their separators.</summary>
    public static bool SameLimit(string? derived, string? saved) =>
        string.Equals(derived?.Trim(), saved?.Trim(), StringComparison.OrdinalIgnoreCase) || (Number(derived) is { } a && Number(saved) is { } b && a == b);
}

/// <summary>Reads the plan's benefit rows for a saved worksheet version.</summary>
public interface IPlanBenefitRepository
{
    /// <summary>The benefit rows in force on the admission date for the patient's plan, insured type and health type; empty when the version has no member or its plan has none.</summary>
    Task<List<PlanBenefit>> BenefitsAsync(IDbSession session, string caseNo, int version);
}

/// <inheritdoc />
public sealed class PlanBenefitRepository : IPlanBenefitRepository
{
    /// <inheritdoc />
    public Task<List<PlanBenefit>> BenefitsAsync(IDbSession session, string caseNo, int version) =>
        session.Connection.ProcQueryAsync<PlanBenefit>("genisis.WorksheetEngine_PlanBenefits", new { cas = caseNo, version }, session.Transaction);
}

/// <summary>Worksheet calculation engine, step 4: the limits the plan fixes.</summary>
public class PlanLimitService(IDbSessions sessions, IPlanBenefitRepository repository)
{
    /// <summary>The limit text of the rows the plan fixes for a saved version, given the days of the stay in Room &amp; Board and ICU.</summary>
    public async Task<Dictionary<int, string>> LimitsAsync(string caseNo, int version, int roomDays, int icuDays)
    {
        await using var session = await sessions.OpenAsync();
        return PlanLimitRules.Derive(await repository.BenefitsAsync(session, caseNo.Trim(), version), roomDays, icuDays);
    }
}
