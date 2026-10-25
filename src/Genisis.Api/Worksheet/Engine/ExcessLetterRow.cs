namespace Genisis.Api.Worksheet.Engine;

/// <summary>What <c>saveExcessLetter</c> reads besides the grid: who saves, the invoice shown, the member's BTBG state and the excess the member undertook.</summary>
public sealed record ExcessLetterInput
{
    /// <summary>Case number.</summary>
    public required string CaseNo { get; init; }

    /// <summary>The user who saves (the letter's issuer).</summary>
    public required string UserCode { get; init; }

    /// <summary>The date of the save.</summary>
    public required DateTime Today { get; init; }

    /// <summary>The invoice number(s) shown on the worksheet (<c>txtINVNo</c>).</summary>
    public string? InvoiceNo { get; init; }

    /// <summary>The amount of the first hospital-paid invoice of the case; <c>null</c> when it has none, in which case the desktop does not make a letter.</summary>
    public decimal? HospitalInvoiceAmount { get; init; }

    /// <summary>The member is BTBG (<c>MBMBTBGStatus</c>): the letter then lists only the rows the member's own flags allow.</summary>
    public bool MemberBtbg { get; init; }

    /// <summary>For a BTBG member: the guardian allowance row is listed.</summary>
    public bool GuardianAllowed { get; init; }

    /// <summary>For a BTBG member: the medical report row is listed.</summary>
    public bool MedicalReportAllowed { get; init; }

    /// <summary>For a BTBG member: the telephone row is listed.</summary>
    public bool TelephoneAllowed { get; init; }

    /// <summary>For a BTBG member: the admin registration row is listed.</summary>
    public bool AdminAllowed { get; init; }

    /// <summary>For a BTBG member: the others row is listed.</summary>
    public bool OthersAllowed { get; init; }

    /// <summary>The excess the member undertook (row 33 of the member column), what <c>UndExcByMBM</c> adds to the excess total.</summary>
    public decimal UndertakingExcess { get; init; }

    /// <summary>The description of the others row (<c>EXCOthersDesc</c>).</summary>
    public string? OthersDescription { get; init; }
}

/// <summary>
/// The row of <c>CAS_Excess</c> (the excess letter) as <c>saveExcessLetter</c> writes it, keyed by the columns it fills. The desktop makes one letter per case: when the worksheet leaves the member an excess
/// or a limit exceeded and a hospital-paid invoice exists; the sequence behind its number (<c>EXC_SEQ</c>) moves on at every such save, the letter row is added only when the case has none. The source adds the
/// undertaking, the co-payment and the limit exceeded to the member total for <c>ExcessAmt</c>; the saved letters equal the member's excess alone in 95.9 per cent of 4,861 (the longer sum in 94.6), so the totals already hold them.
/// </summary>
public static class ExcessLetterRow
{
    /// <summary>The worksheet row each letter column takes from the member column, and whether the column holds <c>0</c> for an empty cell (what the saved letters have) or stays empty.</summary>
    public static readonly IReadOnlyList<(int Slot, string Column, bool ZeroForEmpty)> Rows =
    [
        (1, "EXCBoardAmt", true), (2, "EXCICUAmt", true), (3, "EXCServices", true), (4, "EXCTheatre", true), (5, "EXCMRI", false), (6, "EXCPreHSAmt", false), (7, "EXCPreHosp", false),
        (8, "EXCSurgicalAmt", true), (9, "EXCAna", true), (10, "EXCNSPreHOS", false), (11, "EXCPhysician", true), (12, "EXCPost", false), (13, "EXCMPreHSAmt", false), (14, "EXCEmergency", false),
        (15, "EXCAmbulance", true), (16, "EXCOutpatient", false), (17, "EXCMonthly", false), (18, "EXCOrgan", false), (19, "EXCTax", false), (20, "EXCGuardianAMT", true), (21, "EXCMedical", true),
        (22, "EXCPhone", false), (23, "EXCAdmin", true), (25, "EXCOthers1", true), (27, "EXCBoardTaxAmt", true), (28, "EXCICUTaxAmt", true), (29, "EXCLodgerTaxAmt", true),
    ];

    /// <summary>The columns a BTBG member's letter can fill, with the flag of the member that lets each in.</summary>
    public static readonly IReadOnlyList<(string Column, int Slot)> BtbgRows = [("EXCGuardianAMT", 20), ("EXCMedical", 21), ("EXCPhone", 22), ("EXCAdmin", 23), ("EXCOthers1", 25)];

    /// <summary>The letter is made: the member is left an excess or a limit exceeded, and the case has a hospital-paid invoice.</summary>
    public static bool Required(WorksheetFinal final, decimal? hospitalInvoiceAmount) => hospitalInvoiceAmount is not null && (final.Excess < 0 || final.LimitExceeded < 0);

    /// <summary>The number of the letter: <c>E</c>, the case, a dash and the sequence (<c>GetExcessCode</c>).</summary>
    public static string Number(string caseNo, int sequence) => $"E{caseNo.Trim()}-{sequence}";

    /// <summary>What a letter cell holds for a member amount: the amount owed as a number when the member owes (an amount under one, which includes zero), otherwise nothing.</summary>
    public static string? Cell(decimal memberAmount, bool zeroForEmpty) =>
        memberAmount < 0m || memberAmount == 0m && zeroForEmpty ? WorksheetRows.Number(memberAmount == 0m ? 0m : -memberAmount) : null;

    /// <summary>The columns of the letter, without its number (the number comes from the sequence when the row is written). <c>null</c> when no letter is made.</summary>
    public static Dictionary<string, object?>? Build(ExcessLetterInput i, WorksheetEvaluation grid, WorksheetFinal final)
    {
        if (!Required(final, i.HospitalInvoiceAmount)) return null;
        decimal Member(int slot) => grid.Lines.FirstOrDefault((l) => l.Slot == slot)?.ToMember ?? 0m;
        var row = new Dictionary<string, object?>
        {
            ["CASNumber"] = i.CaseNo.Trim(),
            ["INVNo"] = string.IsNullOrEmpty(i.InvoiceNo) ? null : i.InvoiceNo.Trim(),
            ["INVAmt"] = i.HospitalInvoiceAmount,
            ["EXCDate"] = i.Today.Date,
        };

        if (i.MemberBtbg)
        {
            var allowed = new Dictionary<string, bool> { ["EXCGuardianAMT"] = i.GuardianAllowed, ["EXCMedical"] = i.MedicalReportAllowed, ["EXCPhone"] = i.TelephoneAllowed, ["EXCAdmin"] = i.AdminAllowed, ["EXCOthers1"] = i.OthersAllowed };
            foreach (var (column, slot) in BtbgRows) row[column] = allowed[column] ? Cell(Member(slot), zeroForEmpty: false) : null;
        }
        else
        {
            foreach (var (slot, column, zero) in Rows)
            {
                var amount = Member(slot);
                row[column] = column == "EXCBoardAmt" && amount >= 1m ? WorksheetRows.Dash : Cell(amount, zero);
            }
            row["EXCUndExcess"] = Cell(-Math.Abs(i.UndertakingExcess), zeroForEmpty: true);
            row["EXCCoPayment"] = Cell(-Math.Abs(final.CoPayment), zeroForEmpty: true);
            row["EXCTotalExceed"] = Cell(final.LimitExceeded, zeroForEmpty: true);
            row["ExcessAmt"] = -final.Excess;
            row["EXCOthersDesc"] = string.IsNullOrEmpty(i.OthersDescription) ? null : i.OthersDescription;
        }

        row["EXCIssuedBy"] = i.UserCode;
        row["EXCIssuedDate"] = i.Today.Date;
        return row;
    }
}
