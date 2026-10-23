using Dapper;
using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Maintenance.MmaEntry;

/// <summary>Code values of the calculator.</summary>
public static class MmaEntryCodes
{
    /// <summary>Surgical procedures (frmMMAEntry Source 0): surgeon's and anaesthetist's fees.</summary>
    public const string Surgical = "S";

    /// <summary>Non-surgical procedures (Source 1): one procedure fee.</summary>
    public const string NonSurgical = "N";

    /// <summary>The desktop's six procedure lines.</summary>
    public const int MaxProcedures = 6;

    /// <summary>The longest OPCS code (MMAProcedures.OPCSCode).</summary>
    public const int CodeMaxLength = 10;
}

/// <summary>One procedure line: the OPCS code and the fees actually charged.</summary>
public class MmaEntryLine
{
    /// <summary>OPCS code.</summary>
    public string? Code { get; set; }

    /// <summary>Actual surgeon's fee (surgical) or procedure fee (non-surgical).</summary>
    public decimal? ActualFee { get; set; }

    /// <summary>Actual anaesthetist's fee (surgical only).</summary>
    public decimal? ActualAnaesthetistFee { get; set; }
}

/// <summary>The calculator's input.</summary>
public class MmaEntryRequest
{
    /// <summary>S (surgical) or N (non-surgical).</summary>
    public string? Mode { get; set; }

    /// <summary>Up to six procedure lines.</summary>
    public List<MmaEntryLine> Lines { get; set; } = [];
}

/// <summary>Mode, number of lines, code lengths and non-negative fees.</summary>
public sealed class MmaEntryRequestValidator : RequestValidator<MmaEntryRequest>
{
    /// <summary>Declares the rules.</summary>
    public MmaEntryRequestValidator()
    {
        RuleFor(r => r.Mode).Must(m => m is MmaEntryCodes.Surgical or MmaEntryCodes.NonSurgical).WithMessage("Choose surgical or non-surgical procedures.");
        RuleFor(r => r.Lines).Must(l => l.Count <= MmaEntryCodes.MaxProcedures).WithMessage($"At most {MmaEntryCodes.MaxProcedures} procedures.");
        RuleForEach(r => r.Lines).Must(l => (l.Code?.Trim().Length ?? 0) <= MmaEntryCodes.CodeMaxLength).WithMessage($"OPCS codes have at most {MmaEntryCodes.CodeMaxLength} characters.")
            .Must(l => l.ActualFee is null or >= 0 && l.ActualAnaesthetistFee is null or >= 0).WithMessage("Fees cannot be negative.");
    }
}

/// <summary>The schedule's rates of one OPCS code.</summary>
public record MmaRate
{
    /// <summary>OPCS code.</summary>
    public string Code { get; set; } = "";

    /// <summary>MMA description (OPCS narrative).</summary>
    public string? MmaDescription { get; set; }

    /// <summary>13th Schedule description.</summary>
    public string? ScheduleDescription { get; set; }

    /// <summary>MMA body part.</summary>
    public string? MmaBodyPart { get; set; }

    /// <summary>13th Schedule body part.</summary>
    public string? ScheduleBodyPart { get; set; }

    /// <summary>MMA page.</summary>
    public string? MmaPage { get; set; }

    /// <summary>13th Schedule page.</summary>
    public string? SchedulePage { get; set; }

    /// <summary>MMA surgeon's fee (the procedure fee for non-surgical lines).</summary>
    public decimal? MmaSurgeonFee { get; set; }

    /// <summary>MMA anaesthetist's fee.</summary>
    public decimal? MmaAnaesthetistFee { get; set; }

    /// <summary>13th Schedule surgeon's fee (maximum).</summary>
    public decimal? ScheduleSurgeonMax { get; set; }

    /// <summary>13th Schedule surgeon's fee (minimum).</summary>
    public decimal? ScheduleSurgeonMin { get; set; }

    /// <summary>13th Schedule anaesthetist's fee.</summary>
    public decimal? ScheduleAnaesthetist { get; set; }
}

/// <summary>One calculated line.</summary>
public record MmaEntryResultLine(
    string Code, bool Found, MmaRate? Rate, decimal ActualFee, decimal ActualAnaesthetistFee,
    bool FeeAboveSchedule, bool AnaesthetistFeeAboveSchedule);

/// <summary>The lines and the desktop's totals.</summary>
/// <param name="Lines">Calculated lines (blank codes dropped).</param>
/// <param name="TotalActualFee">Surgical fee (surgical) or procedure fee (non-surgical) passed to the invoice.</param>
/// <param name="TotalActualAnaesthetistFee">Anaesthetist's fee passed to the invoice (surgical).</param>
/// <param name="TotalMmaFee">Sum of MMA surgeon's / procedure fees.</param>
/// <param name="TotalMmaAnaesthetistFee">Sum of MMA anaesthetist's fees.</param>
/// <param name="TotalScheduleMax">Sum of 13th Schedule maximum fees.</param>
/// <param name="TotalScheduleMin">Sum of 13th Schedule minimum fees.</param>
/// <param name="TotalScheduleAnaesthetist">Sum of 13th Schedule anaesthetist's fees.</param>
public record MmaEntryResult(
    List<MmaEntryResultLine> Lines, decimal TotalActualFee, decimal TotalActualAnaesthetistFee, decimal TotalMmaFee,
    decimal TotalMmaAnaesthetistFee, decimal TotalScheduleMax, decimal TotalScheduleMin, decimal TotalScheduleAnaesthetist);

/// <summary>frmMMAEntry's arithmetic. Pure function.</summary>
public static class MmaFeeCalculator
{
    /// <summary>
    /// The *_Change handlers: each column is summed, and an actual fee above the 13th Schedule fee of its line is flagged (the
    /// desktop turned it red). Non-surgical lines have no anaesthetist's fee. A code the schedule does not have counts as 0.
    /// </summary>
    public static MmaEntryResult Calculate(string mode, IEnumerable<MmaEntryLine> lines, IReadOnlyDictionary<string, MmaRate> rates)
    {
        var surgical = mode == MmaEntryCodes.Surgical;
        var result = lines.Where(l => !string.IsNullOrWhiteSpace(l.Code)).Select(l =>
        {
            var code = l.Code!.Trim().ToUpperInvariant();
            var rate = rates.GetValueOrDefault(code);
            var fee = l.ActualFee ?? 0;
            var anaesthetist = surgical ? l.ActualAnaesthetistFee ?? 0 : 0;
            return new MmaEntryResultLine(code, rate is not null, rate, fee, anaesthetist,
                fee > (rate?.ScheduleSurgeonMax ?? 0), surgical && anaesthetist > (rate?.ScheduleAnaesthetist ?? 0));
        }).ToList();

        decimal Sum(Func<MmaEntryResultLine, decimal?> pick) => result.Sum(l => pick(l) ?? 0);
        return new MmaEntryResult(result,
            Sum(l => l.ActualFee), Sum(l => l.ActualAnaesthetistFee), Sum(l => l.Rate?.MmaSurgeonFee),
            surgical ? Sum(l => l.Rate?.MmaAnaesthetistFee) : 0, Sum(l => l.Rate?.ScheduleSurgeonMax), Sum(l => l.Rate?.ScheduleSurgeonMin),
            surgical ? Sum(l => l.Rate?.ScheduleAnaesthetist) : 0);
    }
}

/// <summary>Reads schedule rates.</summary>
public interface IMmaEntryRepository
{
    /// <summary>The rates of the given OPCS codes (codes the schedule lacks are missing from the result).</summary>
    Task<List<MmaRate>> RatesAsync(IDbSession session, IReadOnlyCollection<string> codes);
}

/// <inheritdoc />
public sealed class MmaEntryRepository : IMmaEntryRepository
{
    /// <inheritdoc />
    public Task<List<MmaRate>> RatesAsync(IDbSession session, IReadOnlyCollection<string> codes) =>
        session.Connection.ProcQueryAsync<MmaRate>("genisis.MmaEntry_Rates", Parameters(codes), session.Transaction);

    /// <summary>@c1..@c6: one parameter per procedure line.</summary>
    public static DynamicParameters Parameters(IReadOnlyCollection<string> codes)
    {
        if (codes.Count > MmaEntryCodes.MaxProcedures) throw new ArgumentException($"At most {MmaEntryCodes.MaxProcedures} codes.", nameof(codes));
        var parameters = new DynamicParameters();
        var n = 1;
        foreach (var code in codes) parameters.Add("c" + n++, code);
        return parameters;
    }
}

/// <summary>
/// Maintenance > MMA (legacy frmMMAEntry, "Procedures/OPCS"): the calculator the invoice entry opened to work out surgical or
/// non-surgical procedure fees against the MMA and 13th Schedule. It writes nothing.
/// </summary>
public class MmaEntryService(IDbSessions sessions, IMmaEntryRepository repository)
{
    /// <summary>Looks the codes up and calculates the totals.</summary>
    public async Task<MmaEntryResult> CalculateAsync(MmaEntryRequest request)
    {
        var codes = request.Lines.Select(l => l.Code?.Trim().ToUpperInvariant()).Where(c => !string.IsNullOrEmpty(c)).Select(c => c!).Distinct().ToList();
        var rates = new Dictionary<string, MmaRate>(StringComparer.OrdinalIgnoreCase);
        if (codes.Count > 0)
        {
            await using var session = await sessions.OpenAsync();
            foreach (var rate in await repository.RatesAsync(session, codes)) rates[rate.Code] = rate;
        }
        return MmaFeeCalculator.Calculate(request.Mode!, request.Lines, rates);
    }
}
