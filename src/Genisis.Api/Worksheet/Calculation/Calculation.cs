using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet.Engine;

namespace Genisis.Api.Worksheet.Calculation;

/// <summary>One row of the calculated worksheet grid.</summary>
public record CalculationLine
{
    /// <summary>Grid row, 1 to 29.</summary>
    public int Slot { get; set; }

    /// <summary>Benefit category.</summary>
    public string? Category { get; set; }

    /// <summary>Benefit name.</summary>
    public string Benefit { get; set; } = "";

    /// <summary>The limit the plan gives the row (an amount, FULL, or empty when the plan gives none).</summary>
    public string? Limit { get; set; }

    /// <summary>The limit the desktop saved with the version, when there is one.</summary>
    public string? SavedLimit { get; set; }

    /// <summary>The actual amount added up from the invoices.</summary>
    public decimal Actual { get; set; }

    /// <summary>The approved amount: the actual amount held to the limit.</summary>
    public decimal Approved { get; set; }

    /// <summary>The part paid to the hospital.</summary>
    public decimal ToHospital { get; set; }

    /// <summary>The part due to (negative: from) the member.</summary>
    public decimal ToMember { get; set; }
}

/// <summary>The summary rows of a worksheet, calculated or saved.</summary>
/// <param name="SumApproved">The approved amounts added up.</param>
/// <param name="CoPayment">The co-payment.</param>
/// <param name="LimitExceeded">The amount above the available limit (zero or negative).</param>
/// <param name="TotalApproved">The approved total after the co-payment and the available limit.</param>
/// <param name="PreviousBalance">The limit before the worksheet.</param>
public record CalculationSummary(decimal SumApproved, decimal CoPayment, decimal LimitExceeded, decimal TotalApproved, decimal? PreviousBalance);

/// <summary>A worksheet calculated from its invoices and the patient's plan, beside what was saved.</summary>
public record CalculationResult
{
    /// <summary>The case.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>The worksheet version whose invoices were used.</summary>
    public int Version { get; set; }

    /// <summary>All versions the case has, for the version list.</summary>
    public List<int> Versions { get; set; } = [];

    /// <summary>Days in Room &amp; Board, summed over the invoices.</summary>
    public decimal RoomDays { get; set; }

    /// <summary>Days in ICU, summed over the invoices.</summary>
    public decimal IcuDays { get; set; }

    /// <summary>The 29 rows.</summary>
    public List<CalculationLine> Lines { get; set; } = [];

    /// <summary>The summary the engine calculated.</summary>
    public CalculationSummary? Calculated { get; set; }

    /// <summary>The summary the desktop saved with the version.</summary>
    public CalculationSummary? Saved { get; set; }

    /// <summary>Where the calculation cannot be trusted, in words for the user.</summary>
    public List<string> Notes { get; set; } = [];
}

/// <summary>The figures the desktop saved with a version, as the calculation screen compares them.</summary>
public record SavedFigures(decimal TotalApproved, decimal LimitExceeded, decimal CoPayment, decimal? PreviousBalance);

/// <summary>A grid row's caption.</summary>
public record RowCaption(int Slot, string? BenefitCode, string? Category, string Benefit);

/// <summary>Reads what the calculation screen shows besides the engine's figures.</summary>
public interface ICalculationRepository
{
    /// <summary>The captions of the 29 rows.</summary>
    Task<List<RowCaption>> CaptionsAsync(IDbSession session);

    /// <summary>The figures saved with a version; <c>null</c> when it has none.</summary>
    Task<SavedFigures?> SavedAsync(IDbSession session, string caseNo, int version);

    /// <summary>The versions of a case, oldest first.</summary>
    Task<List<int>> VersionsAsync(IDbSession session, string caseNo);
}

/// <inheritdoc />
public sealed class CalculationRepository : ICalculationRepository
{
    /// <inheritdoc />
    public Task<List<RowCaption>> CaptionsAsync(IDbSession session) => session.Connection.ProcQueryAsync<RowCaption>("genisis.WorksheetCalc_Captions", null, session.Transaction);

    /// <inheritdoc />
    public Task<SavedFigures?> SavedAsync(IDbSession session, string caseNo, int version) =>
        session.Connection.ProcFirstOrDefaultAsync<SavedFigures>("genisis.WorksheetCalc_Saved", new { cas = caseNo, version }, session.Transaction);

    /// <inheritdoc />
    public Task<List<int>> VersionsAsync(IDbSession session, string caseNo) => session.Connection.ProcQueryAsync<int>("genisis.WorksheetCalc_Versions", new { cas = caseNo }, session.Transaction);
}

/// <summary>Rules of what the calculation screen tells the user about its figures.</summary>
public static class CalculationRules
{
    /// <summary>
    /// The notes that say where a calculation cannot be trusted: no limit from the plan for a row that has an amount (the row is then shown as not covered, which can be wrong), and
    /// a plan that has no benefit rows at all.
    /// </summary>
    public static List<string> Notes(IReadOnlyList<CalculationLine> lines, bool planHasRows)
    {
        var notes = new List<string>();
        if (!planHasRows) notes.Add("The plan has no benefit rows in the database, so no limits could be taken from it; every row is shown as not covered.");
        var missing = lines.Where(l => l.Actual != 0 && string.IsNullOrWhiteSpace(l.Limit)).Select(l => l.Benefit).ToList();
        if (planHasRows && missing.Count > 0)
            notes.Add($"The plan gives no limit for: {string.Join(", ", missing)}. They are shown as not covered; the desktop may allow them, so check them before relying on the totals.");
        return notes;
    }
}

/// <summary>
/// Worksheet Registration, calculation step: works a worksheet out from its invoices and the patient's plan and shows it beside what the desktop saved. It writes nothing; saving a new
/// worksheet needs invoice entry and the save procedures, which are not migrated (see worksheet-screens.md).
/// </summary>
public class CalculationService(IDbSessions sessions, ICalculationRepository repository, IWorksheetEngineRepository engine, IWorksheetCalculator calculator)
{
    private const string NotFound = "Record not found!";

    /// <summary>
    /// The calculation of a case's worksheet at a version (the latest when none is given). Throws <see cref="NotFoundException"/> ("Record not found!") when the case has no such
    /// worksheet or its member is not in the database.
    /// </summary>
    public async Task<CalculationResult> GetAsync(string caseNo, int? version)
    {
        var cas = caseNo.Trim();
        await using var session = await sessions.OpenAsync();
        var versions = await repository.VersionsAsync(session, cas);
        if (versions.Count == 0) throw new NotFoundException(NotFound);
        var chosen = version ?? versions.Max();
        if (!versions.Contains(chosen)) throw new NotFoundException(NotFound);

        var saved = await repository.SavedAsync(session, cas, chosen);
        var calculation = await calculator.CalculateAsync(cas, chosen, saved?.TotalApproved ?? 0m) ?? throw new NotFoundException(NotFound);
        var captions = (await repository.CaptionsAsync(session)).ToDictionary(c => c.Slot);
        var savedLimits = (await engine.LimitsAsync(session, cas, chosen)).ToDictionary(l => l.Slot, l => l.LimitText);

        var lines = calculation.Grid.Lines.Select(l =>
        {
            captions.TryGetValue(l.Slot, out var caption);
            savedLimits.TryGetValue(l.Slot, out var savedLimit);
            return new CalculationLine
            {
                Slot = l.Slot, Category = caption?.Category, Benefit = caption?.Benefit ?? $"Row {l.Slot}", Limit = l.Limit, SavedLimit = savedLimit,
                Actual = l.Actual, Approved = l.Approved, ToHospital = l.ToHospital, ToMember = l.ToMember,
            };
        }).ToList();
        var final = calculation.Final;
        return new CalculationResult
        {
            CaseNo = cas, Version = chosen, Versions = versions,
            RoomDays = calculation.RoomDays, IcuDays = calculation.IcuDays,
            Lines = lines,
            Calculated = new CalculationSummary(final.SumApproved, final.CoPayment, final.LimitExceeded, final.TotalApproved, calculation.PreviousBalance),
            Saved = saved is null ? null : new CalculationSummary(0m, saved.CoPayment, saved.LimitExceeded, saved.TotalApproved, saved.PreviousBalance),
            Notes = CalculationRules.Notes(lines, calculation.PlanHasRows),
        };
    }
}
