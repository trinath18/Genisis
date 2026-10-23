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

    /// <summary>
    /// What the desktop shows for a row that has an amount when the plan has benefit rows but none for this benefit: FULL for hospital supplies, anaesthetist, physician visits and accident
    /// (every one of 276, 39, 89 and 115 such rows in the saved worksheets), 50.00 for the medical report (35 of 35). The other benefits show "-" (not covered) and get no entry.
    /// </summary>
    public static readonly IReadOnlyList<(string Code, int Slot, string Text)> WhenThePlanHasNoRow = [("103", 3, "FULL"), ("204", 9, "FULL"), ("302", 11, "FULL"), ("401", 14, "FULL"), ("801", 21, "50.00")];

    /// <summary>The hospital code of the non-panel hospital. For payor TK its Room &amp; Board limit is FULL, whatever the plan's rate (620 of 622 saved worksheets; only Room &amp; Board has an amount in them).</summary>
    public const string NonPanelHospital = "NP";

    /// <summary>
    /// Limits that follow a payor's own convention, not the plan: where the payor's plan has no benefit row for the code and the row has an amount, the desktop saved FULL (evidence: the saved
    /// worksheets of a one-in-eight sample). They are inferred from data and not from the desktop's code (the snapshot we have does not contain them), so the calculation says where one was used.
    /// </summary>
    public static readonly IReadOnlyList<(string Payor, string Code, int Slot, string Text, string Evidence)> PayorDefaults =
    [
        ("TK", "301", 10, "FULL", "28 of 28"), ("XL", "301", 10, "FULL", "16 of 18"), ("XL", "202", 7, "FULL", "22 of 26"), ("AX", "804", 24, "FULL", "25 of 32"), ("SE", "803", 23, "FULL", "91 of 97"),
    ];

    /// <summary>Worksheet row of "Others": expenses the plan does not cover. Whatever the plan says, the limit saved with an amount in it is 0.00 (in 99 per cent of the versions).</summary>
    public const int Others = 25;

    /// <summary>Worksheet row of Room &amp; Board.</summary>
    public const int RoomAndBoard = 1;

    /// <summary>Worksheet row of ICU.</summary>
    public const int Icu = 2;

    /// <summary>Worksheet row of the service tax on Room &amp; Board.</summary>
    public const int RoomAndBoardTax = 27;

    /// <summary>Worksheet row of the service tax on ICU (a limit only when ICU is FULL).</summary>
    public const int IcuTax = 28;

    /// <summary>The service tax limit is this share of the Room &amp; Board limit (6 per cent, in 98.6 per cent of the saved worksheets that have one).</summary>
    public const decimal TaxRate = 0.06m;

    /// <summary>A limit as the worksheet shows it: thousands separated, two decimals.</summary>
    public static string Money(decimal value) => value.ToString("#,##0.00", CultureInfo.InvariantCulture);

    private static decimal? Number(string? text) => decimal.TryParse(text?.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>
    /// The limit text of each row the plan fixes, keyed by worksheet row. Room &amp; Board and ICU are the rate per day times the days of the stay (<paramref name="roomDays"/>,
    /// <paramref name="icuDays"/>), held to the days the plan allows when it has such a limit (no Room &amp; Board days: no limit); the service tax is a share of the Room &amp; Board limit.
    /// </summary>
    public static Dictionary<int, string> Derive(IEnumerable<PlanBenefit> benefits, decimal roomDays, decimal icuDays, IReadOnlySet<int>? rowsWithAmount = null, string? payor = null, string? hospital = null)
    {
        var limits = new Dictionary<int, string>();
        var any = false;
        var seen = new HashSet<string>();
        foreach (var b in benefits)
        {
            any = true;
            var code = b.Code.Trim();
            seen.Add(code);
            if (Slots.TryGetValue(code, out var slot) && !string.IsNullOrWhiteSpace(b.Limit)) limits[slot] = b.Limit.Trim();
            else if (CaseSlots.TryGetValue(code, out var caseSlot) && rowsWithAmount?.Contains(caseSlot) == true && !string.IsNullOrWhiteSpace(b.Limit)) limits[caseSlot] = b.Limit.Trim();
            else if (code is "101" or "102" && Number(b.Claimable) is { } rate)
            {
                var days = code == "101" ? roomDays : icuDays;
                if (Number(b.Days) is { } allowed and > 0) days = Math.Min(days, allowed);
                if (code == "101" && days <= 0) continue; // a stay with no Room & Board days shows no limit ("-"); ICU with none shows 0.00
                limits[code == "101" ? RoomAndBoard : Icu] = Money(rate * days);
            }
        }
        if (any && rowsWithAmount is not null)
            foreach (var (missingCode, slot, text) in WhenThePlanHasNoRow)
                if (!seen.Contains(missingCode) && rowsWithAmount.Contains(slot) && !limits.ContainsKey(slot)) limits[slot] = text;
        if (any && rowsWithAmount is not null)
            foreach (var (defaultPayor, code, slot, text, _) in PayorDefaults)
                if (string.Equals(payor?.Trim(), defaultPayor, StringComparison.OrdinalIgnoreCase) && !seen.Contains(code) && rowsWithAmount.Contains(slot) && !limits.ContainsKey(slot)) limits[slot] = text;
        // payor TK's non-panel hospital: Room & Board FULL, the plan's rate notwithstanding
        if (string.Equals(payor?.Trim(), "TK", StringComparison.OrdinalIgnoreCase) && string.Equals(hospital?.Trim(), NonPanelHospital, StringComparison.OrdinalIgnoreCase) && rowsWithAmount?.Contains(RoomAndBoard) == true) limits[RoomAndBoard] = "FULL";
        if (any && rowsWithAmount?.Contains(Others) == true) limits[Others] = Money(0);
        if (limits.TryGetValue(RoomAndBoard, out var room))
        {
            // a FULL Room & Board makes its service tax FULL too, and a FULL ICU its tax (the display routine, tmpRBSearchTax / TotalICU)
            if (string.Equals(room.Trim(), "FULL", StringComparison.OrdinalIgnoreCase)) limits[RoomAndBoardTax] = "FULL";
            else if (Number(room) is { } roomLimit and > 0) limits[RoomAndBoardTax] = Money(Math.Round(roomLimit * TaxRate, 2, MidpointRounding.AwayFromZero));
        }
        if (limits.TryGetValue(Icu, out var icu) && string.Equals(icu.Trim(), "FULL", StringComparison.OrdinalIgnoreCase)) limits[IcuTax] = "FULL";
        return limits;
    }

    /// <summary>
    /// The days a limit was worked out with (<c>DBDaysLmt</c>, <c>ICUDaysLmt</c>): the days of the stay held to the days the plan allows for the benefit (<paramref name="code"/> 101 Room &amp; Board, 102 ICU), or the days of
    /// the stay when the plan gives no day limit.
    /// </summary>
    public static decimal LimitedDays(IEnumerable<PlanBenefit> benefits, string code, decimal stayDays) =>
        Number(benefits.FirstOrDefault((b) => b.Code.Trim() == code)?.Days) is { } allowed and > 0 ? Math.Min(stayDays, allowed) : stayDays;

    /// <summary>
    /// The worksheet rows whose limit <see cref="Derive"/> took from a payor's convention and not from the plan (<see cref="PayorDefaults"/> and the non-panel Room &amp; Board of payor TK), so the calculation can say so.
    /// </summary>
    public static List<int> ConventionSlots(IEnumerable<PlanBenefit> benefits, IReadOnlySet<int>? rowsWithAmount, string? payor, string? hospital)
    {
        var slots = new List<int>();
        if (rowsWithAmount is null) return slots;
        var seen = benefits.Select((b) => b.Code.Trim()).ToHashSet();
        var tk = string.Equals(payor?.Trim(), "TK", StringComparison.OrdinalIgnoreCase);
        if (tk && string.Equals(hospital?.Trim(), NonPanelHospital, StringComparison.OrdinalIgnoreCase) && rowsWithAmount.Contains(RoomAndBoard)) slots.Add(RoomAndBoard);
        if (seen.Count > 0)
            foreach (var (defaultPayor, code, slot, _, _) in PayorDefaults)
                if (string.Equals(payor?.Trim(), defaultPayor, StringComparison.OrdinalIgnoreCase) && !seen.Contains(code) && rowsWithAmount.Contains(slot) && !slots.Contains(slot)) slots.Add(slot);
        return slots;
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
    public async Task<Dictionary<int, string>> LimitsAsync(string caseNo, int version, decimal roomDays, decimal icuDays, IReadOnlySet<int>? rowsWithAmount = null, string? payor = null, string? hospital = null)
    {
        await using var session = await sessions.OpenAsync();
        return PlanLimitRules.Derive(await repository.BenefitsAsync(session, caseNo.Trim(), version), roomDays, icuDays, rowsWithAmount, payor, hospital);
    }
}
