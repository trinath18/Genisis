using Genisis.Api.Data;

namespace Genisis.Api.Worksheet.Engine;

/// <summary>A worksheet worked out from its invoices and the patient's plan, with nothing taken from a saved worksheet.</summary>
/// <param name="Grid">The rows: actual, limit, approved, hospital and member amounts.</param>
/// <param name="Final">The summary rows.</param>
/// <param name="Limits">The limit text the plan gave each row.</param>
/// <param name="PreviousBalance">The limit before the worksheet.</param>
/// <param name="CoPayment">The co-payment the plan asks.</param>
public record WorksheetCalculation(WorksheetEvaluation Grid, WorksheetFinal Final, IReadOnlyDictionary<int, string> Limits, decimal PreviousBalance, decimal CoPayment);

/// <summary>
/// Worksheet calculation engine, step 6: the pieces together. The actual amounts come from the invoices, the limits from the plan's benefit rows, the co-payment from the plan's co-insurance
/// and the limit before the worksheet from the patient's available limit; the rows and the summary are worked out from those. This is what Registration does when it opens a worksheet.
/// </summary>
public class WorksheetCalculator(IDbSessions sessions, IWorksheetEngineRepository engine, IPlanBenefitRepository benefits, IPlanTermsRepository terms, IBalanceLimitRepository balance)
{
    /// <summary>
    /// Calculates a version of a case.
    /// </summary>
    /// <param name="caseNo">The case.</param>
    /// <param name="version">The worksheet version whose invoices are used.</param>
    /// <param name="roomDays">Days of the stay in Room &amp; Board (entered with the invoice).</param>
    /// <param name="icuDays">Days of the stay in ICU.</param>
    /// <param name="approvedByTheCase">What the case's earlier versions approved and took off the available limit; zero for a new case.</param>
    /// <returns>The calculation, or <c>null</c> when the version has no member (so no plan and no available limit).</returns>
    public async Task<WorksheetCalculation?> CalculateAsync(string caseNo, int version, int roomDays, int icuDays, decimal approvedByTheCase = 0m)
    {
        await using var session = await sessions.OpenAsync();
        var cas = caseNo.Trim();
        if (await balance.BalanceAsync(session, cas, version) is not { } limit) return null;
        var amounts = await engine.ActualAsync(session, cas, version);
        var withAmount = amounts.Where(a => a.Actual > 0).Select(a => a.Slot).ToHashSet();
        var limits = PlanLimitRules.Derive(await benefits.BenefitsAsync(session, cas, version), roomDays, icuDays, withAmount);
        var grid = WorksheetRules.Evaluate(amounts, limits.ToDictionary(l => l.Key, l => (string?)l.Value));
        var coPayment = CoPaymentRules.CoInsurance(grid.Totals.Approved, await terms.TermsAsync(session, cas, version));
        var previous = BalanceLimitRules.PreviousBalance(limit.Balance, approvedByTheCase);
        return new WorksheetCalculation(grid, WorksheetRules.Final(grid, coPayment, previous), limits, previous, coPayment);
    }
}
