using Genisis.Api.Data;

namespace Genisis.Api.Worksheet.Engine;

/// <summary>A worksheet worked out from its invoices and the patient's plan, with nothing taken from a saved worksheet.</summary>
/// <param name="Grid">The rows: actual, limit, approved, hospital and member amounts.</param>
/// <param name="Final">The summary rows.</param>
/// <param name="Limits">The limit text the plan gave each row.</param>
/// <param name="PreviousBalance">The limit before the worksheet.</param>
/// <param name="CoPayment">The co-payment the plan asks.</param>
/// <param name="RoomDays">The Room &amp; Board days the limits were worked out with.</param>
/// <param name="IcuDays">The ICU days the limits were worked out with.</param>
/// <param name="PlanHasRows">Whether the plan has benefit rows at all.</param>
public record WorksheetCalculation(WorksheetEvaluation Grid, WorksheetFinal Final, IReadOnlyDictionary<int, string> Limits, decimal PreviousBalance, decimal CoPayment, decimal RoomDays, decimal IcuDays, bool PlanHasRows);

/// <summary>Works a worksheet out from its invoices and the patient's plan.</summary>
public interface IWorksheetCalculator
{
    /// <summary>Calculates a version of a case: <c>null</c> when the version has no member.</summary>
    /// <param name="caseNo">The case.</param>
    /// <param name="version">The worksheet version whose invoices are used.</param>
    /// <param name="approvedByTheCase">What the case's earlier versions approved; zero for a new case.</param>
    /// <param name="roomDays">Days in Room &amp; Board to use instead of the invoices' (null: the invoices').</param>
    /// <param name="icuDays">Days in ICU to use instead of the invoices' (null: the invoices').</param>
    Task<WorksheetCalculation?> CalculateAsync(string caseNo, int version, decimal approvedByTheCase = 0m, decimal? roomDays = null, decimal? icuDays = null);
}

/// <summary>
/// Worksheet calculation engine, step 6: the pieces together. The actual amounts come from the invoices, the limits from the plan's benefit rows, the co-payment from the plan's co-insurance
/// the days of the stay from the invoices, and the limit before the worksheet from the patient's available limit; the rows and the summary are worked out from those. This is what Registration does when it opens a worksheet.
/// </summary>
public class WorksheetCalculator(IDbSessions sessions, IWorksheetEngineRepository engine, IPlanBenefitRepository benefits, IPlanTermsRepository terms, IBalanceLimitRepository balance, IStayDaysRepository stay) : IWorksheetCalculator
{
    /// <summary>
    /// Calculates a version of a case.
    /// </summary>
    /// <param name="caseNo">The case.</param>
    /// <param name="version">The worksheet version whose invoices are used.</param>
    /// <param name="approvedByTheCase">What the case's earlier versions approved and took off the available limit; zero for a new case.</param>
    /// <param name="roomDays">Days in Room &amp; Board to use instead of the invoices' (null: the invoices').</param>
    /// <param name="icuDays">Days in ICU to use instead of the invoices' (null: the invoices').</param>
    /// <returns>The calculation, or <c>null</c> when the version has no member (so no plan and no available limit).</returns>
    public async Task<WorksheetCalculation?> CalculateAsync(string caseNo, int version, decimal approvedByTheCase = 0m, decimal? roomDays = null, decimal? icuDays = null)
    {
        await using var session = await sessions.OpenAsync();
        var cas = caseNo.Trim();
        if (await balance.BalanceAsync(session, cas, version) is not { } limit) return null;
        var amounts = await engine.ActualAsync(session, cas, version);
        var withAmount = amounts.Where(a => a.Actual > 0).Select(a => a.Slot).ToHashSet();
        var days = await stay.DaysAsync(session, cas, version);
        var rows = await benefits.BenefitsAsync(session, cas, version);
        var (room, icu) = (roomDays ?? days.RoomDays, icuDays ?? days.IcuDays);
        var limits = PlanLimitRules.Derive(rows, room, icu, withAmount);
        var grid = WorksheetRules.Evaluate(amounts, limits.ToDictionary(l => l.Key, l => (string?)l.Value));
        var coPayment = CoPaymentRules.CoPayment(grid, await terms.TermsAsync(session, cas, version));
        var previous = BalanceLimitRules.PreviousBalance(limit.Balance, approvedByTheCase);
        return new WorksheetCalculation(grid, WorksheetRules.Final(grid, coPayment, previous), limits, previous, coPayment, room, icu, rows.Count > 0);
    }
}
