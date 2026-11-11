namespace Genisis.Api.Worksheet.Engine;

/// <summary>
/// The row of <c>ClaimPayMBMStatus</c> as <c>savePayMBMStatus</c> writes it: for each worksheet row a <c>Y</c> when the plan has a limit for it, nothing was invoiced and something is payable (the limit is above
/// zero, the actual is zero and the approved is above zero), otherwise <c>N</c>. Of the 33,717 closed versions saved since July 2020 none has a <c>Y</c> in a column this routine writes.
/// </summary>
public static class PayMbmStatusRow
{
    private static readonly (int Slot, string Column)[] Common =
    [
        (1, "CLMBoardAmt"), (2, "CLMICUAmt"), (3, "CLMServices"), (4, "CLMTheatre"), (5, "CLMMRI"), (14, "CLMAccPostAmt"), (15, "CLMAmbulance"), (16, "CLMOutpatient"), (17, "CLMMonthly"), (18, "CLMOrgan"),
        (19, "CLMCashDays"), (20, "CLMGuardianAmt"), (21, "CLMMedical"), (22, "CLMPhone"), (23, "CLMAdmin"), (24, "CLMOthers1"), (25, "CLMOthersNotCoPay"), (26, "CLMBoardTaxAmt"), (27, "CLMICUTaxAmt"),
        (28, "CLMLodgerTaxAmt"),
    ];

    private static readonly (int Slot, string Column)[] Itemised =
        [(6, "CLMPreHSAmt"), (7, "CLMPreHosp"), (8, "CLMSurgicalAmt"), (9, "CLMAna"), (10, "CLMNSPreHos"), (11, "CLMPhysician"), (12, "CLMPost"), (13, "CLMMPreHSAmt")];

    private static readonly (int Slot, string Column)[] FlatSums = [(8, "CLMCOPreHSSum"), (13, "CLMCOMPreHSSum")];

    /// <summary>
    /// Every column the routine can write: the two plan layouts together (the procedure takes all of them, the ones a layout does not write are left as they are).
    /// </summary>
    public static IReadOnlyList<string> AllColumns { get; } = [.. Common.Select((c) => c.Column), .. Itemised.Select((c) => c.Column), .. FlatSums.Select((c) => c.Column)];

    /// <summary>
    /// The worksheet row each column tests. The routine was written for an older grid, so from row 19 on its column names are one row behind the grid of today: the tax row is tested for
    /// <c>CLMCashDays</c>, the home nursing row for <c>CLMOthers1</c>, and the lodger tax row is not tested. The saved rows follow it (<c>CLMTax</c> is empty, <c>CLMCashDays</c> is <c>N</c>), so it is kept.
    /// </summary>
    public static IReadOnlyList<(int Slot, string Column)> Map(bool flat) => [.. Common, .. (flat ? FlatSums : Itemised)];

    /// <summary>The flag of one worksheet row: the limit is a number above zero, nothing was invoiced and something is approved.</summary>
    public static bool Flag(WorksheetLine? line) =>
        line is not null && ClaimVersionRow.NumericLimit(line.Limit) > 0 && line.Actual == 0m && line.Approved > 0m;

    /// <summary>The <c>Y</c> or <c>N</c> of each column the plan layout writes.</summary>
    public static Dictionary<string, string> Build(WorksheetEvaluation grid, bool flat) =>
        Map(flat).ToDictionary((c) => c.Column, (c) => Flag(grid.Lines.FirstOrDefault((l) => l.Slot == c.Slot)) ? "Y" : "N");
}
