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
/// The limit of each worksheet row as the plan gives it, ported from the Registration form's <c>display_benefit</c>. Only the rules the saved worksheets confirm are here. A row that has no
/// amount shows no limit in the desktop ("-"), and nothing can be approved on it, so the rows whose limit depends on the case are derived only when the row has an amount (see
/// <see cref="CaseSlots"/>). Left out: the kidney and cancer balances of members on a lifetime plan, and the tax rows of ICU and lodger.
/// </summary>
public static class PlanLimitRules
{
    /// <summary>The benefit code of the rows whose limit is the plan's FULL / amount as it stands, and the worksheet row (slot) it fills.</summary>
    public static readonly IReadOnlyDictionary<string, int> Slots = new Dictionary<string, int>
    {
        ["103"] = 3, ["104"] = 4, ["201"] = 6, ["202"] = 7, ["203"] = 8, ["204"] = 9, ["301"] = 10, ["302"] = 11, ["401"] = 14, ["402"] = 15, ["403"] = 16, ["501"] = 18,
    };

    /// <summary>
    /// The benefit code of the rows that show the plan's FULL / amount only when the worksheet has an amount in them (post-hospitalisation, kidney / cancer, guardian, medical report, admin,
    /// home nursing), and the worksheet row it fills. With an amount in the row the saved limit equals the plan's in 90 to 95 per cent of the versions.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> CaseSlots = new Dictionary<string, int> { ["303"] = 12, ["404"] = 17, ["701"] = 20, ["801"] = 21, ["803"] = 23, ["804"] = 24 };

    /// <summary>Worksheet row of "Others": expenses the plan does not cover. Whatever the plan says, the limit saved with an amount in it is 0.00 (in 99 per cent of the versions).</summary>
    public const int Others = 25;

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
    public static Dictionary<int, string> Derive(IEnumerable<PlanBenefit> benefits, int roomDays, int icuDays, IReadOnlySet<int>? rowsWithAmount = null)
    {
        var limits = new Dictionary<int, string>();
        var any = false;
        foreach (var b in benefits)
        {
            any = true;
            var code = b.Code.Trim();
            if (Slots.TryGetValue(code, out var slot) && !string.IsNullOrWhiteSpace(b.Limit)) limits[slot] = b.Limit.Trim();
            else if (CaseSlots.TryGetValue(code, out var caseSlot) && rowsWithAmount?.Contains(caseSlot) == true && !string.IsNullOrWhiteSpace(b.Limit)) limits[caseSlot] = b.Limit.Trim();
            else if (code is "101" or "102" && Number(b.Claimable) is { } rate)
            {
                var days = code == "101" ? roomDays : icuDays;
                if (Number(b.Days) is { } allowed and > 0) days = Math.Min(days, (int)allowed);
                if (code == "101" && days <= 0) continue; // a stay with no Room & Board days shows no limit ("-"); ICU with none shows 0.00
                limits[code == "101" ? RoomAndBoard : Icu] = Money(rate * days);
            }
        }
        if (any && rowsWithAmount?.Contains(Others) == true) limits[Others] = Money(0);
        if (limits.TryGetValue(RoomAndBoard, out var room) && Number(room) is { } roomLimit and > 0) limits[RoomAndBoardTax] = Money(Math.Round(roomLimit * TaxRate, 2, MidpointRounding.AwayFromZero));
        return limits;
    }

    /// <summary>
    /// Whether two limits have the same effect on what is approved (<c>GetMin</c>): FULL lets everything through, an amount caps, and anything else (a dash, empty) approves nothing, like 0.00. The desktop
    /// saves "0.00" or "-" for a row the plan does not cover, which approve the same.
    /// </summary>
    public static bool SameEffect(string? derived, string? saved)
    {
        static (bool Full, decimal Amount) Effect(string? t) => t?.Trim().ToUpperInvariant() == "FULL" ? (true, 0m) : (false, Number(t) ?? 0m);
        return Effect(derived) == Effect(saved);
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
    /// <summary>The limit text of the rows the plan fixes for a saved version, given the days of the stay in Room &amp; Board and ICU and the rows that have an amount.</summary>
    public async Task<Dictionary<int, string>> LimitsAsync(string caseNo, int version, int roomDays, int icuDays, IReadOnlySet<int>? rowsWithAmount = null)
    {
        await using var session = await sessions.OpenAsync();
        return PlanLimitRules.Derive(await repository.BenefitsAsync(session, caseNo.Trim(), version), roomDays, icuDays, rowsWithAmount);
    }
}
