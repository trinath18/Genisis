using System.Globalization;

namespace Genisis.Api.Worksheet.Engine;

/// <summary>
/// The rows the desktop's Registration save writes to <c>ClaimActual</c>, <c>ClaimApprove</c>, <c>ClaimHos</c> and <c>ClaimMember</c> (<c>saveActual</c>, <c>saveApprove</c>, <c>saveHos</c>, <c>saveMember</c> of
/// <c>frmWorksheetRegGST2015</c>): the cells of the worksheet grid as text, one column of the grid each (Actual, Approved, Payable to hospital, Payable to member), by the column of the table each row goes to.
/// </summary>
public static class WorksheetRows
{
    /// <summary>The text of a cell with nothing in it.</summary>
    public const string Dash = "-";

    /// <summary>
    /// Whether the plan is written the flat way: plan families 2 to 5 and payor IB keep two sums (<c>CLMCOPreHSSum</c>, <c>CLMCOMPreHSSum</c>) in rows 8 and 13 and no pre- and post-hospital rows 6, 7, 9, 10, 11, 12.
    /// </summary>
    public static bool Flat(string? planFamily, string? payor) => planFamily?.Trim() is "2" or "3" or "4" or "5" || string.Equals(payor?.Trim(), "IB", StringComparison.OrdinalIgnoreCase);

    private static readonly (int Slot, string Column)[] Always =
    [
        (1, "CLMBoardAmt"), (2, "CLMICUAmt"), (3, "CLMServices"), (4, "CLMTheatre"), (5, "CLMMRI"), (14, "CLMAccPostAmt"), (15, "CLMAmbulance"), (16, "CLMOutpatient"), (17, "CLMMonthly"), (18, "CLMOrgan"),
        (19, "CLMTax"), (20, "CLMGuardianAmt"), (21, "CLMMedical"), (22, "CLMPhone"), (23, "CLMAdmin"), (24, "CLMHomeNCAmt"), (25, "CLMOthers1"), (26, "CLMOthersNotCoPay"), (27, "CLMBoardTaxAmt"),
        (28, "CLMICUTaxAmt"), (29, "CLMLodgerTaxAmt"),
    ];

    private static readonly (int Slot, string Column)[] Itemised =
        [(6, "CLMPreHSAmt"), (7, "CLMPreHosp"), (8, "CLMSurgicalAmt"), (9, "CLMAna"), (10, "CLMNSPreHos"), (11, "CLMPhysician"), (12, "CLMPost"), (13, "CLMMPreHSAmt")];

    private static readonly (int Slot, string Column)[] FlatSums = [(8, "CLMCOPreHSSum"), (13, "CLMCOMPreHSSum")];

    /// <summary>Every column the five grid tables have: the two plan layouts together. A procedure takes all of them; the ones a layout does not write are empty text.</summary>
    public static IReadOnlyList<string> AllColumns { get; } = [.. Always.Select((c) => c.Column), .. Itemised.Select((c) => c.Column), .. FlatSums.Select((c) => c.Column)];

    /// <summary>The columns of <c>ClaimLimit</c> that hold the days the limits were worked out with.</summary>
    public static IReadOnlyList<string> DayColumns { get; } = ["CLMBoardDays", "CLMICUDays", "CLMINHOSPhyDays", "CLMKIDDCDays", "CLMCashDays", "CLMGurdianDays"];

    /// <summary>The column each worksheet row (grid row) is written to.</summary>
    public static IReadOnlyList<(int Slot, string Column)> Columns(bool flat) => [.. Always, .. (flat ? FlatSums : Itemised)];

    /// <summary>Which column of the worksheet grid a table holds.</summary>
    public enum Kind
    {
        /// <summary>What the invoices add up to.</summary>
        Actual,

        /// <summary>What the plan lets through.</summary>
        Approved,

        /// <summary>Payable to the hospital.</summary>
        Hospital,

        /// <summary>Payable to, or (negative) due from, the member.</summary>
        Member,
    }

    /// <summary>A cell as the grid shows it, VB's <c>Format(x, "#,#.00")</c>: thousands separated, two decimals, no digit before the point for an amount under one (".50").</summary>
    public static string Grid(decimal value)
    {
        var text = Math.Abs(value).ToString("#,##0.00", CultureInfo.InvariantCulture);
        if (text.StartsWith("0.", StringComparison.Ordinal)) text = text[1..];
        return value < 0 ? "-" + text : text;
    }

    /// <summary>VB's string of a number (no separators, no trailing zeros): <c>-94.4</c>, <c>0.5</c>.</summary>
    public static string Number(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>The worksheet rows of the monthly (kidney dialysis) and organ benefits, whose approved cell the desktop in use since 2020 saves as "0" when it is empty (718 of 771 approved rows of 2021; before 2019 always a dash).</summary>
    public static readonly IReadOnlySet<int> ApprovedZeroRows = new HashSet<int> { 17, 18 };

    /// <summary>
    /// The text one cell is saved as. An empty cell is a dash (for the approved cell of the monthly and organ rows, <see cref="ApprovedZeroRows"/>, a "0"). The member's column is the exception: an amount under 1, which includes everything the member owes (a negative number), is saved as the number VB
    /// makes of it (<c>-94.4</c>, <c>0.5</c>) and not as the grid shows it (<c>saveMember</c>: <c>(Fgrid.Text * -1) * -1</c>).
    /// </summary>
    public static string Cell(decimal value, Kind kind, int slot = 0)
    {
        if (value == 0m) return kind == Kind.Approved && ApprovedZeroRows.Contains(slot) ? "0" : Dash;
        return kind == Kind.Member && value < 1m ? Number(value) : Grid(value);
    }

    /// <summary>The days the limits were worked out with, as <c>saveLimit</c> keeps them next to the limits (<c>DBDaysLmt</c>, <c>ICUDaysLmt</c>, <c>PhyVisitLmt</c>, <c>KidneyMonthsLmt</c>, <c>daycashLmt</c>, <c>lodgerdayLmt</c>).</summary>
    /// <param name="Board">Room &amp; Board days.</param>
    /// <param name="Icu">ICU days.</param>
    /// <param name="PhysicianVisits">Physician visit days (the itemised layout only).</param>
    /// <param name="KidneyMonths">Kidney months.</param>
    /// <param name="Cash">Cash allowance days.</param>
    /// <param name="Guardian">Guardian (lodger) days.</param>
    public record LimitDays(decimal Board = 0m, decimal Icu = 0m, decimal PhysicianVisits = 0m, decimal KidneyMonths = 0m, decimal Cash = 0m, decimal Guardian = 0m);

    /// <summary>
    /// The row of <c>ClaimLimit</c> (<c>saveLimit</c>): the limit text of each worksheet row (the grid's Limit column, a dash for a row with none) in the same columns as the other tables, and the days the limits were worked out
    /// with as VB's number (<c>0</c> when there are none). The physician visit days are written for the itemised layout only.
    /// </summary>
    public static Dictionary<string, string> Limits(WorksheetEvaluation grid, bool flat, LimitDays days)
    {
        var rows = new Dictionary<string, string>();
        foreach (var (slot, column) in Columns(flat))
        {
            var text = grid.Lines.FirstOrDefault((l) => l.Slot == slot)?.Limit?.Trim();
            rows[column] = string.IsNullOrEmpty(text) ? Dash : text;
        }
        rows["CLMBoardDays"] = Number(days.Board);
        rows["CLMICUDays"] = Number(days.Icu);
        if (!flat) rows["CLMINHOSPhyDays"] = Number(days.PhysicianVisits);
        rows["CLMKIDDCDays"] = Number(days.KidneyMonths);
        rows["CLMCashDays"] = Number(days.Cash);
        rows["CLMGurdianDays"] = Number(days.Guardian);
        return rows;
    }

    /// <summary>
    /// The GST copy of the actual row (<c>saveActualGST</c>): the grid's GST column, which is a dash in every row because GST has been zero-rated since 2018 (all but 5 of the 33,945 versions saved since July 2020).
    /// </summary>
    public static Dictionary<string, string> ActualGst(bool flat) => Columns(flat).ToDictionary((c) => c.Column, (_) => Dash);

    /// <summary>The cash type of the worksheet (<c>SaveClaim</c>): <c>C</c> cashless when the hospital is paid, <c>R</c> reimbursement when the member is; <c>null</c> when nobody is named.</summary>
    public static string? CashType(string? payTo) => string.IsNullOrWhiteSpace(payTo) ? null : string.Equals(payTo.Trim(), "H", StringComparison.OrdinalIgnoreCase) ? "C" : "R";

    /// <summary>
    /// The totals of the version row of <c>Claim</c> that follow from the grid and the summary rows (<c>SaveClaim</c>): the total actual and approved, the final approve (the same as the total approved), the co-payment,
    /// the limit exceeded, the excess (what the member owes, negative) and the reimbursement (what is payable to the member); and the Final Total row: payable to the hospital (the hospital row plus what the member owes,
    /// 0 for a reimbursement) and to the member (the positive member cells). With the member's available limit given, the balance limit, which the desktop in use saves as the previous balance (the source of the desktop
    /// we have subtracts the final approve; the saved rows do in 10 of 3,369 since July 2020 and equal the previous balance in 3,158).
    /// </summary>
    /// <param name="grid">The evaluated worksheet.</param>
    /// <param name="final">The summary rows.</param>
    /// <param name="cashType"><see cref="CashType"/>.</param>
    /// <param name="previousBalance">The member's available limit before the worksheet, or <c>null</c> when the plan has none.</param>
    public static Dictionary<string, decimal> ClaimTotals(WorksheetEvaluation grid, WorksheetFinal final, string? cashType = null, decimal? previousBalance = null)
    {
        var totals = ClaimSummary(grid, final);
        var reimbursement = string.Equals(cashType, "R", StringComparison.OrdinalIgnoreCase);
        totals["CLMPayableByMedix2H"] = reimbursement ? 0m : grid.Totals.ToHospital + final.Excess;
        totals["CLMPayableByMedix2M"] = final.PayableToMember;
        if (previousBalance is { } previous)
        {
            totals["CLMPreviousBalance"] = previous;
            totals["CLMBalanceLimit"] = previous;
        }
        return totals;
    }

    private static Dictionary<string, decimal> ClaimSummary(WorksheetEvaluation grid, WorksheetFinal final) => new()
    {
        ["CLMTotalActual"] = grid.Totals.Actual,
        ["CLMTotalApproved"] = final.TotalApproved,
        ["CLMFinalApprove"] = final.FinalApprove,
        ["CLMCoPayRB"] = final.CoPayment,
        ["CLMLimitExceeded"] = final.LimitExceeded,
        ["CLMExcess"] = final.Excess,
        ["CLMReimburse"] = final.PayableToMember,
    };

    /// <summary>The columns and texts of one table for an evaluated worksheet. Rows the plan family does not write are left out.</summary>
    public static Dictionary<string, string> Build(WorksheetEvaluation grid, bool flat, Kind kind)
    {
        var rows = new Dictionary<string, string>();
        foreach (var (slot, column) in Columns(flat))
        {
            var line = grid.Lines.FirstOrDefault((l) => l.Slot == slot);
            var value = kind switch { Kind.Actual => line?.Actual, Kind.Approved => line?.Approved, Kind.Hospital => line?.ToHospital, _ => line?.ToMember } ?? 0m;
            rows[column] = Cell(value, kind, slot);
        }
        return rows;
    }
}
