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

    /// <summary>
    /// The co-payment of an evaluated worksheet: the member's own share when the member has a percentage, otherwise the plan's co-insurance. The Room &amp; Board co-payment (a plan with CoPaymentInd Y when the room
    /// charged is above the plan's) and the co-insurance that goes to the ex-gratia row are not ported.
    /// </summary>
    public static decimal CoPayment(WorksheetEvaluation grid, PlanTerms? terms)
    {
        if (!HasMemberShare(terms)) return CoInsurance(grid.Totals.Approved, terms);
        decimal Approved(params int[] slots) => grid.Lines.Where((l) => slots.Contains(l.Slot)).Sum((l) => l.Approved);
        return MemberShare(grid.Totals.Approved, Approved(1), Approved(27, 28, 29), terms);
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
