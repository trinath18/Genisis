using Genisis.Api.Data;

namespace Genisis.Api.Worksheet.Engine;

/// <summary>The co-payment terms of the patient's plan (<c>PLN</c>).</summary>
public record PlanTerms
{
    /// <summary>Co-insurance percentage (PLNCoINSPerc): the share of the approved total the member pays.</summary>
    public decimal CoInsurancePercent { get; set; }

    /// <summary>Whether the co-insurance applies (PLNCoInsSign = Y).</summary>
    public string? CoInsuranceSign { get; set; }

    /// <summary>Room &amp; Board co-payment percentage (PLNCoPayPerc).</summary>
    public decimal RoomCoPayPercent { get; set; }

    /// <summary>Room &amp; Board co-payment indicator (CoPaymentInd).</summary>
    public string? CoPaymentInd { get; set; }

    /// <summary>The member's own co-payment percentage (<c>MBM.MBMTopupCopay</c>; payor TK members have 20).</summary>
    public decimal MemberCoPayPercent { get; set; }

    /// <summary>Whether the member's percentage applies (<c>MBM.MBMTopupCopayInd</c> = Y).</summary>
    public string? MemberCoPayInd { get; set; }

    /// <summary>The first letter of the plan code (families 2 and 3 take the MRI share off the base of the Room &amp; Board co-payment).</summary>
    public string? PlanFamily { get; set; }

    /// <summary>The plan is a new SM plan (<c>PLN.NewSMPlan</c> = Y).</summary>
    public string? NewSmPlan { get; set; }

    /// <summary>The MRI percentage of a new SM plan (0 for the others).</summary>
    public decimal MriPercent { get; set; }

    /// <summary>The plan is a major-medical plan (<c>PLN.PLNMajorMedInd</c> = Y).</summary>
    public string? MajorMedInd { get; set; }

    /// <summary>Whether the payor carries the Room &amp; Board tax (<c>PAY.PAYRBTax</c> = Y, read for the first two letters of the case number); it chooses between the two branches of the co-payment.</summary>
    public string? PayorRoomTax { get; set; }

    /// <summary>The new Room &amp; Board tax summed over the version's invoices.</summary>
    public decimal RoomTaxNew { get; set; }

    /// <summary>The gross Room &amp; Board summed over the version's invoices.</summary>
    public decimal GrossRoomBoard { get; set; }

    /// <summary>The latest invoice date of the version; from 1 April 2008 the co-payment is rounded to 5 cents.</summary>
    public DateTime? LastInvoiceDate { get; set; }
}

/// <summary>Rules of the co-payment, checked against the co-payment the desktop saved (<c>CLMCoPayRB</c>).</summary>
public static class CoPaymentRules
{
    private static bool Yes(string? v) => string.Equals(v?.Trim(), "Y", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The co-insurance: the plan's percentage of the approved total, to the cent, when the plan has a percentage and its sign is Y. It is right for 83 per cent of the worksheets it applies to.
    /// </summary>
    public static decimal CoInsurance(decimal totalApproved, PlanTerms? terms) =>
        terms is { CoInsurancePercent: > 0 } && Yes(terms.CoInsuranceSign) ? Math.Round(totalApproved * terms.CoInsurancePercent / 100m, 2, MidpointRounding.AwayFromZero) : 0m;

    /// <summary>Whether the member carries a co-payment percentage of their own that applies (MBMTopupCopayInd = Y and a percentage).</summary>
    public static bool HasMemberShare(PlanTerms? terms) => terms is { MemberCoPayPercent: > 0 } && Yes(terms.MemberCoPayInd);

    /// <summary>
    /// The member's own share, the desktop's major-medical formula: the member's percentage of the approved total less the approved Room &amp; Board and the approved taxes. On the saved worksheets of payor
    /// TK members (20 per cent) it gives the saved co-payment to the cent in 94 of 98.
    /// </summary>
    /// <param name="totalApproved">The approved total.</param>
    /// <param name="boardApproved">The approved Room &amp; Board row (row 1).</param>
    /// <param name="taxesApproved">The approved tax rows (R&amp;B, ICU and lodger tax: rows 27 to 29).</param>
    /// <param name="terms">The patient's terms.</param>
    public static decimal MemberShare(decimal totalApproved, decimal boardApproved, decimal taxesApproved, PlanTerms? terms) =>
        HasMemberShare(terms) ? Math.Round((totalApproved - boardApproved - taxesApproved) * terms!.MemberCoPayPercent / 100m, 2, MidpointRounding.AwayFromZero) : 0m;

    private static readonly DateTime CentRounding = new(2008, 4, 1);

    /// <summary>
    /// The Room &amp; Board co-payment (the plan's <c>CoPaymentInd</c> = Y with <c>PLNCoPayPerc</c>), as the desktop's display routine works it out. It applies when the room charged is above the plan's limit: the approved
    /// Room &amp; Board row is below the actual one or below the gross (branch A, a payor without the Room &amp; Board tax), or below the actual less the new Room &amp; Board tax (branch B, <c>PAY.PAYRBTax</c> = Y). The base is the
    /// approved total less the approved Room &amp; Board row and the approved Others-no-co-pay row (row 26), and: for plan families 2 and 3 and new SM plans also less the MRI share of the actual MRI and not the tax; in branch A
    /// also less the approved Room &amp; Board tax (row 27); in branch B less that tax only for a major-medical plan. The result is rounded to the cent and, for invoices from 1 April 2008, to the nearest 5 cents.
    /// On the saved worksheets with a Room &amp; Board co-payment it explains 94 of 96 (the payor's setting has changed over the years, so a worksheet made under the other branch differs by the tax).
    /// </summary>
    /// <param name="grid">The evaluated worksheet.</param>
    /// <param name="terms">The patient's terms.</param>
    public static decimal RoomAndBoard(WorksheetEvaluation grid, PlanTerms? terms)
    {
        if (terms is not { RoomCoPayPercent: > 0 } || !Yes(terms.CoPaymentInd)) return 0m;
        WorksheetLine? Line(int slot) => grid.Lines.FirstOrDefault((l) => l.Slot == slot);
        var board = Line(1);
        if (board is null) return 0m;
        var roomTax = Yes(terms.PayorRoomTax);
        var triggered = roomTax
            ? !string.Equals(board.Limit?.Trim(), "FULL", StringComparison.OrdinalIgnoreCase) && board.Approved < board.Actual - Math.Abs(terms.RoomTaxNew)
            : board.Approved < board.Actual || board.Approved < terms.GrossRoomBoard;
        if (!triggered) return 0m;
        var total = grid.Totals.Approved;
        var othersNoCoPay = Line(26)?.Approved ?? 0m;
        var tax = Line(27)?.Approved ?? 0m;
        var family = terms.PlanFamily?.Trim() is "2" or "3" || Yes(terms.NewSmPlan);
        var baseAmount = family ? total - board.Approved - (Line(5)?.Actual ?? 0m) * terms.MriPercent / 100m - othersNoCoPay
            : !roomTax ? total - tax - board.Approved - othersNoCoPay
            : !Yes(terms.MajorMedInd) ? total - board.Approved - othersNoCoPay
            : total - board.Approved - tax - othersNoCoPay;
        var amount = Math.Round(baseAmount * terms.RoomCoPayPercent / 100m, 2, MidpointRounding.AwayFromZero);
        return terms.LastInvoiceDate is { } date && date >= CentRounding ? Math.Round(amount / 0.05m, 0, MidpointRounding.AwayFromZero) * 0.05m : amount;
    }

    /// <summary>
    /// The co-payment of an evaluated worksheet: the member's own share when the member has a percentage, otherwise the Room &amp; Board co-payment when the room charged is above the plan's limit, otherwise the plan's
    /// co-insurance. The co-insurance that goes to the ex-gratia row is not ported.
    /// </summary>
    public static decimal CoPayment(WorksheetEvaluation grid, PlanTerms? terms)
    {
        if (HasMemberShare(terms))
        {
            decimal Approved(params int[] slots) => grid.Lines.Where((l) => slots.Contains(l.Slot)).Sum((l) => l.Approved);
            return MemberShare(grid.Totals.Approved, Approved(1), Approved(27, 28, 29), terms);
        }
        var roomAndBoard = RoomAndBoard(grid, terms);
        return roomAndBoard != 0m ? roomAndBoard : CoInsurance(grid.Totals.Approved, terms);
    }
}

/// <summary>Reads the plan's co-payment terms for a saved worksheet version.</summary>
public interface IPlanTermsRepository
{
    /// <summary>The terms of the patient's plan; <c>null</c> when the version has no member or its plan has no row.</summary>
    Task<PlanTerms?> TermsAsync(IDbSession session, string caseNo, int version);
}

/// <inheritdoc />
public sealed class PlanTermsRepository : IPlanTermsRepository
{
    /// <inheritdoc />
    public Task<PlanTerms?> TermsAsync(IDbSession session, string caseNo, int version) =>
        session.Connection.ProcFirstOrDefaultAsync<PlanTerms>("genisis.WorksheetEngine_PlanTerms", new { cas = caseNo, version }, session.Transaction);
}

/// <summary>Worksheet calculation engine, step 5: the co-payment.</summary>
public class CoPaymentService(IDbSessions sessions, IPlanTermsRepository repository)
{
    /// <summary>The co-insurance of a saved version for an approved total.</summary>
    public async Task<decimal> CoInsuranceAsync(string caseNo, int version, decimal totalApproved)
    {
        await using var session = await sessions.OpenAsync();
        return CoPaymentRules.CoInsurance(totalApproved, await repository.TermsAsync(session, caseNo.Trim(), version));
    }

    /// <summary>The co-payment of a saved version, from its evaluated grid.</summary>
    public async Task<decimal> CoPaymentAsync(string caseNo, int version, WorksheetEvaluation grid)
    {
        await using var session = await sessions.OpenAsync();
        return CoPaymentRules.CoPayment(grid, await repository.TermsAsync(session, caseNo.Trim(), version));
    }
}
