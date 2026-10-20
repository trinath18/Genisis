using System.Globalization;
using System.Text.RegularExpressions;
using Genisis.Api.Data;

namespace Genisis.Api.Worksheet.Engine;

/// <summary>The amount of one worksheet grid row (slot), added up from the invoices of a version.</summary>
public record SlotAmount
{
    /// <summary>Grid row, 1 to 29: 1 Room &amp; Board ... 25 Emergency Sickness Treatment, 26 Others - No Co-Pay, 27 to 29 the service taxes.</summary>
    public int Slot { get; set; }

    /// <summary>The actual amount: every invoice of the version.</summary>
    public decimal Actual { get; set; }

    /// <summary>The part of <see cref="Actual"/> on invoices paid to the hospital (cashless), the desktop's H1..H29.</summary>
    public decimal ToHospital { get; set; }
}

/// <summary>The limit column of one grid row as the desktop kept it: an amount, the word FULL, or nothing.</summary>
public record SlotLimit
{
    /// <summary>Grid row, 1 to 29.</summary>
    public int Slot { get; set; }

    /// <summary>The saved limit text, for example <c>200.00</c>, <c>FULL</c> or <c>-</c>; <c>null</c> when the version has no saved limits.</summary>
    public string? LimitText { get; set; }
}

/// <summary>One evaluated grid row.</summary>
/// <param name="Slot">Grid row, 1 to 29.</param>
/// <param name="Actual">Actual amount from the invoices.</param>
/// <param name="Limit">The limit text the approved amount was worked out from.</param>
/// <param name="Approved">Approved amount: the actual amount held to the limit.</param>
/// <param name="ToHospital">Amount on invoices paid to the hospital (the desktop's Payable to Hosp column).</param>
/// <param name="ToMember">Approved minus the hospital amount: positive is due to the member, negative is due from the member (Due to/(frm) Mem).</param>
public record WorksheetLine(int Slot, decimal Actual, string? Limit, decimal Approved, decimal ToHospital, decimal ToMember);

/// <summary>The column totals of an evaluated worksheet.</summary>
/// <param name="Actual">Total actual.</param>
/// <param name="Approved">Total approved.</param>
/// <param name="ToHospital">Total payable to the hospital.</param>
/// <param name="ToMember">Total due to / (from) the member.</param>
public record WorksheetTotals(decimal Actual, decimal Approved, decimal ToHospital, decimal ToMember);

/// <summary>A worksheet version evaluated row by row.</summary>
/// <param name="Lines">The 29 rows.</param>
/// <param name="Totals">The column totals.</param>
public record WorksheetEvaluation(List<WorksheetLine> Lines, WorksheetTotals Totals);

/// <summary>The inputs of the summary rows that come from the plan and the member rather than from the grid.</summary>
public record SavedSummaryInputs
{
    /// <summary>Co-payment on Room &amp; Board (the desktop's CoPayApp), as saved.</summary>
    public decimal CoPayment { get; set; }

    /// <summary>The member's available limit before this worksheet (CLMPreviousBalance), as saved.</summary>
    public decimal PreviousBalance { get; set; }
}

/// <summary>The summary rows under the grid that follow from the grid and the two plan / member inputs.</summary>
/// <param name="SumApproved">Total of the approved column.</param>
/// <param name="CoPayment">Co-payment taken off.</param>
/// <param name="LimitExceeded">Zero, or the (negative) amount above the member's available limit.</param>
/// <param name="TotalApproved">Approved after co-payment and the limit.</param>
/// <param name="FinalApprove">Final approved amount; the same as the total approved.</param>
/// <param name="Excess">Total of the negative member amounts: what the member has to pay (negative).</param>
/// <param name="PayableToMember">Total of the positive member amounts.</param>
public record WorksheetFinal(decimal SumApproved, decimal CoPayment, decimal LimitExceeded, decimal TotalApproved, decimal FinalApprove, decimal Excess, decimal PayableToMember);

/// <summary>The rules of the worksheet grid, ported from the desktop's GetMin / GetMax / DueToFromMBM.</summary>
public static partial class WorksheetRules
{
    /// <summary>The limit word that lets the whole actual amount through.</summary>
    public const string Full = "FULL";

    [GeneratedRegex(@"^\s*(?:\(\s*(?<n>\d[\d,]*(?:\.\d*)?|\.\d+)\s*\)|(?<sign>[+-])?\s*(?<n2>\d[\d,]*(?:\.\d*)?|\.\d+)(?<trail>-)?)\s*$")]
    private static partial Regex NumericText();

    /// <summary>
    /// Reads a limit as VB6's IsNumeric / CDbl do: an optional sign (leading or trailing) or brackets for a negative, thousands commas and a decimal point.
    /// Words, a lone dash, blank text and percentages are not numbers.
    /// </summary>
    public static bool TryParseLimit(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var m = NumericText().Match(text);
        if (!m.Success) return false;
        var digits = (m.Groups["n"].Success ? m.Groups["n"].Value : m.Groups["n2"].Value).Replace(",", "");
        if (!decimal.TryParse(digits, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)) return false;
        var negative = m.Groups["n"].Success || m.Groups["sign"].Value == "-" || m.Groups["trail"].Success;
        value = negative ? -amount : amount;
        return true;
    }

    /// <summary>
    /// The approved amount of one row. GetMin: the actual amount held to the limit (for a negative amount, to a negative limit); the word FULL lets all of it through;
    /// any other text (a dash, blank) approves nothing. With <paramref name="payMember"/> the desktop's GetMax is used instead: the limit is a floor, not a ceiling.
    /// </summary>
    public static decimal Approve(decimal actual, string? limit, bool payMember = false)
    {
        if (!TryParseLimit(limit, out var y)) return string.Equals(limit?.Trim(), Full, StringComparison.OrdinalIgnoreCase) ? actual : 0m;
        if (payMember) return y >= 0 ? (actual < y ? y : actual) : (actual > y ? y : actual);
        return actual >= 0 ? (actual > y ? y : actual) : (actual < y ? y : actual);
    }

    /// <summary>
    /// The summary rows: the approved total less the co-payment, held to the member's available limit (the part above it is the limit exceeded, a negative amount; a
    /// negative balance pays nothing); the final amount is that total. Checked against 31,199 saved worksheets, where it agrees in 99.4 per cent of versions.
    /// </summary>
    /// <param name="grid">The evaluated rows.</param>
    /// <param name="coPayment">Co-payment, as the plan works it out.</param>
    /// <param name="previousBalance">The member's available limit before this worksheet.</param>
    public static WorksheetFinal Final(WorksheetEvaluation grid, decimal coPayment, decimal previousBalance)
    {
        var afterCoPayment = grid.Totals.Approved - coPayment;
        var over = afterCoPayment > previousBalance;
        var limitExceeded = over ? previousBalance - afterCoPayment : 0m;
        var total = over ? Math.Max(previousBalance, 0m) : afterCoPayment;
        return new WorksheetFinal(
            grid.Totals.Approved, coPayment, limitExceeded, total, total,
            grid.Lines.Where(l => l.ToMember < 0).Sum(l => l.ToMember), grid.Lines.Where(l => l.ToMember > 0).Sum(l => l.ToMember));
    }

    /// <summary>Evaluates every row: approved from the actual amount and its limit, the hospital amount as given, the member amount as approved minus hospital.</summary>
    /// <param name="amounts">Actual and hospital amounts per row.</param>
    /// <param name="limits">Limit text per row; a row without an entry has no limit text.</param>
    /// <param name="payMemberRows">Rows the desktop flags to pay the member (its AppStatus Y); none by default.</param>
    public static WorksheetEvaluation Evaluate(IEnumerable<SlotAmount> amounts, IReadOnlyDictionary<int, string?> limits, IReadOnlySet<int>? payMemberRows = null)
    {
        var lines = amounts.OrderBy(a => a.Slot).Select(a =>
        {
            limits.TryGetValue(a.Slot, out var limit);
            var approved = Approve(a.Actual, limit, payMemberRows?.Contains(a.Slot) == true);
            return new WorksheetLine(a.Slot, a.Actual, limit, approved, a.ToHospital, approved - a.ToHospital);
        }).ToList();
        return new WorksheetEvaluation(lines, new WorksheetTotals(lines.Sum(l => l.Actual), lines.Sum(l => l.Approved), lines.Sum(l => l.ToHospital), lines.Sum(l => l.ToMember)));
    }
}

/// <summary>Reads the inputs of the worksheet calculation.</summary>
public interface IWorksheetEngineRepository
{
    /// <summary>The 29 slots of a version with the amounts added up from its invoices (zero for a slot without an invoice amount).</summary>
    Task<List<SlotAmount>> ActualAsync(IDbSession session, string caseNo, int version);

    /// <summary>The limit text saved for each row of a version; empty when the version has no saved limits.</summary>
    Task<List<SlotLimit>> LimitsAsync(IDbSession session, string caseNo, int version);

    /// <summary>The co-payment and available limit saved with a version; <c>null</c> when the version has no saved worksheet.</summary>
    Task<SavedSummaryInputs?> SummaryInputsAsync(IDbSession session, string caseNo, int version);
}

/// <inheritdoc />
public sealed class WorksheetEngineRepository : IWorksheetEngineRepository
{
    /// <inheritdoc />
    public Task<List<SlotAmount>> ActualAsync(IDbSession session, string caseNo, int version) =>
        session.Connection.ProcQueryAsync<SlotAmount>("genisis.WorksheetEngine_Actual", new { cas = caseNo, version }, session.Transaction);

    /// <inheritdoc />
    public Task<List<SlotLimit>> LimitsAsync(IDbSession session, string caseNo, int version) =>
        session.Connection.ProcQueryAsync<SlotLimit>("genisis.WorksheetEngine_Limits", new { cas = caseNo, version }, session.Transaction);

    /// <inheritdoc />
    public Task<SavedSummaryInputs?> SummaryInputsAsync(IDbSession session, string caseNo, int version) =>
        session.Connection.ProcFirstOrDefaultAsync<SavedSummaryInputs>("genisis.WorksheetEngine_SummaryInputs", new { cas = caseNo, version }, session.Transaction);
}

/// <summary>
/// The worksheet calculation the desktop's Registration and Adjustment forms run (getDatatoWS / getFinalTotal). It is built up in steps, each checked against the
/// worksheets the desktop saved: step 1 adds up the actual amounts from the invoices; step 2 holds each row to its limit and splits it between hospital and member.
/// </summary>
public class WorksheetEngineService(IDbSessions sessions, IWorksheetEngineRepository repository)
{
    /// <summary>The actual amounts of a version, row by row, as the Actual column of the worksheet grid shows them.</summary>
    public async Task<List<SlotAmount>> ActualAsync(string caseNo, int version)
    {
        await using var session = await sessions.OpenAsync();
        return await repository.ActualAsync(session, caseNo.Trim(), version);
    }

    /// <summary>
    /// A saved version evaluated again: actual and hospital amounts from its invoices, held to the limit text saved with it. Used to check the engine against what the
    /// desktop saved, and by Adjustment to recalculate after invoices change.
    /// </summary>
    public async Task<WorksheetEvaluation> EvaluateSavedAsync(string caseNo, int version)
    {
        await using var session = await sessions.OpenAsync();
        var cas = caseNo.Trim();
        var amounts = await repository.ActualAsync(session, cas, version);
        var limits = (await repository.LimitsAsync(session, cas, version)).ToDictionary(l => l.Slot, l => l.LimitText);
        return WorksheetRules.Evaluate(amounts, limits);
    }

    /// <summary>A saved version evaluated again, with the summary rows worked out from the grid and the co-payment and available limit saved with it.</summary>
    public async Task<(WorksheetEvaluation Grid, WorksheetFinal Final)?> FinalSavedAsync(string caseNo, int version)
    {
        await using var session = await sessions.OpenAsync();
        var cas = caseNo.Trim();
        var inputs = await repository.SummaryInputsAsync(session, cas, version);
        if (inputs is null) return null;
        var amounts = await repository.ActualAsync(session, cas, version);
        var limits = (await repository.LimitsAsync(session, cas, version)).ToDictionary(l => l.Slot, l => l.LimitText);
        var grid = WorksheetRules.Evaluate(amounts, limits);
        return (grid, WorksheetRules.Final(grid, inputs.CoPayment, inputs.PreviousBalance));
    }
}
