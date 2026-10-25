namespace Genisis.Api.Worksheet.Engine;

/// <summary>What <c>saveSameLimit</c> reads: the limit texts of the worksheet, what the plan allows in days, what the worksheet used, and the member's excess undertaking.</summary>
/// <param name="Limits">The limit texts of the worksheet rows (<see cref="SameLimitRow.Limits"/>).</param>
/// <param name="Allowed">The days the plan allows (used when the case has no row yet).</param>
/// <param name="Used">The days the worksheet used.</param>
/// <param name="MemberUndertaking">The excess the member undertook before the worksheet (used when the case has no row yet).</param>
/// <param name="UndertakingUsed">The excess the worksheet takes from the undertaking.</param>
/// <param name="PerDisabilityLimit">The plan's original per-disability (major medical) limit; 0 when there is none.</param>
public sealed record SameLimitInput(
    IReadOnlyDictionary<string, string> Limits,
    WorksheetRows.LimitDays Allowed,
    WorksheetRows.LimitDays Used,
    decimal MemberUndertaking = 0m,
    decimal UndertakingUsed = 0m,
    decimal PerDisabilityLimit = 0m);

/// <summary>What the case has left as the same-limit row says (nothing for a case without a row).</summary>
public sealed record SameLimitLeft(decimal Board, decimal Icu, decimal PhysicianVisits, decimal KidneyMonths, decimal Cash, decimal Guardian, decimal? UndertakingExcess)
{
    /// <summary>The six day counts.</summary>
    public WorksheetRows.LimitDays Days => new(Board, Icu, PhysicianVisits, KidneyMonths, Cash, Guardian);
}

/// <summary>
/// The row of <c>ClaimSameLimit</c> as <c>saveSameLimit</c> writes it: what is left of the case's limits and days after a worksheet (one row per case, changed by every worksheet of the case). A row's limit is the
/// limit the worksheet showed, and for the benefits that are used up (room and board, ICU, services, theatre, surgical, anaesthetist, post-hospitalisation and organ) the limit less what the worksheet approved.
/// </summary>
public static class SameLimitRow
{
    /// <summary>
    /// The worksheet rows whose limit is written as what is left: services, theatre, surgical, anaesthetist and organ (the source also does it for the co-paid pre-hospitalisation sums of the flat layout). Room and
    /// board and ICU are written as shown: the saved rows keep their limit whatever was approved.
    /// </summary>
    public static readonly IReadOnlySet<int> RemainingSlots = new HashSet<int> { 3, 4, 8, 9, 18 };

    /// <summary>The post-hospitalisation row, which is worked out apart.</summary>
    public const int PostSlot = 12;

    /// <summary>The worksheet rows the routine does not write: home nursing and the others that are not co-paid.</summary>
    public static readonly IReadOnlySet<int> NotWritten = new HashSet<int> { 24, 26 };

    /// <summary>
    /// What is left of one limit, as the desktop works it out from the limit as shown and the approved cell as shown (a dash for none; the organ row shows 0): <c>FULL</c> stays <c>FULL</c>; with something shown as
    /// approved a numeric limit is written with two decimals, less the approved amount; otherwise a limit that sorts before the approved cell as text (VB compares the two as strings: a dash or a percent sign before
    /// an amount) becomes 0; any other limit is written as it is, a number with two decimals.
    /// </summary>
    public static string Remaining(string limit, string approvedCell)
    {
        if (string.Equals(limit, "FULL", StringComparison.OrdinalIgnoreCase)) return limit;
        var numeric = decimal.TryParse(limit.Replace(",", ""), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var number);
        var approvedNumber = decimal.TryParse(approvedCell.Replace(",", ""), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var amount);
        if (approvedCell != WorksheetRows.Dash && numeric && approvedNumber) return PlanLimitRules.Money(number - amount);
        if (string.CompareOrdinal(limit, approvedCell) < 0) return "0";
        return numeric ? PlanLimitRules.Money(number) : limit;
    }

    /// <summary>
    /// The days that are left after a worksheet: for a case without a row yet what the plan allows less what the worksheet used; for a case with one, what it had left less what the worksheet used, or none when the
    /// worksheet used all of it or more. Each of the six counts (room and board, ICU, physician visits, kidney months, cash and guardian days) is worked out alone.
    /// </summary>
    /// <param name="previous">What the case had left, or <c>null</c> when it has no row.</param>
    /// <param name="allowed">What the plan allows (used for a case without a row).</param>
    /// <param name="used">What the worksheet used.</param>
    public static WorksheetRows.LimitDays NextDays(WorksheetRows.LimitDays? previous, WorksheetRows.LimitDays allowed, WorksheetRows.LimitDays used)
    {
        static decimal Next(decimal? left, decimal allowed, decimal used) => left is null ? allowed - used : left > used ? left.Value - used : 0m;
        return new WorksheetRows.LimitDays(
            Next(previous?.Board, allowed.Board, used.Board), Next(previous?.Icu, allowed.Icu, used.Icu), Next(previous?.PhysicianVisits, allowed.PhysicianVisits, used.PhysicianVisits),
            Next(previous?.KidneyMonths, allowed.KidneyMonths, used.KidneyMonths), Next(previous?.Cash, allowed.Cash, used.Cash), Next(previous?.Guardian, allowed.Guardian, used.Guardian));
    }

    /// <summary>The excess undertaking that is left: what the case had (or the member undertook), less what the worksheet takes, and none when it takes all of it or more.</summary>
    public static decimal NextUndertaking(decimal? previous, decimal member, decimal used)
    {
        var left = previous ?? member;
        return left > used ? left - used : 0m;
    }

    /// <summary>The limit texts of the row for the worksheet's grid, in the columns the plan layout writes.</summary>
    public static Dictionary<string, string> Limits(WorksheetEvaluation grid, bool flat)
    {
        var rows = new Dictionary<string, string>();
        foreach (var (slot, column) in WorksheetRows.Columns(flat).Where((c) => !NotWritten.Contains(c.Slot)))
        {
            var line = grid.Lines.FirstOrDefault((l) => l.Slot == slot);
            var text = line?.Limit?.Trim();
            var limit = string.IsNullOrEmpty(text) ? WorksheetRows.Dash : text;
            if (slot == PostSlot)
                rows[column] = line is { Approved: > 0m } && ClaimVersionRow.NumericLimit(limit) > 0m ? PlanLimitRules.Money(Math.Max(ClaimVersionRow.NumericLimit(limit) - line.Approved, 0m)) : PlanLimitRules.Money(0m);
            else if (RemainingSlots.Contains(slot) || flat && slot is 8 or 13)
                rows[column] = Remaining(limit, WorksheetRows.Cell(line?.Approved ?? 0m, WorksheetRows.Kind.Approved, slot));
            else
                rows[column] = limit;
        }
        return rows;
    }
}
