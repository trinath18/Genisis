using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Worksheet.SendToAr;

/// <summary>One case of the Send to A/R list (a row of the FrmARRecFrCLM list).</summary>
public record SendToArRow
{
    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Date sent to audit review (A/R).</summary>
    public DateTime? DateSent { get; set; }

    /// <summary>User who passed it.</summary>
    public string? PassedBy { get; set; }

    /// <summary>Status: 1 passed, 0 not yet.</summary>
    public string? Status { get; set; }
}

/// <summary>The criteria of the Send to A/R search.</summary>
public class SendToArFilter
{
    /// <summary>"Use Scan": one case number instead of a range.</summary>
    public bool UseScan { get; set; } = true;

    /// <summary>Case number, or the start of the range.</summary>
    public string? WksNoFrom { get; set; }

    /// <summary>End of the case number range (range mode only).</summary>
    public string? WksNoTo { get; set; }

    /// <summary>Date sent from.</summary>
    public DateTime? DateSentFrom { get; set; }

    /// <summary>Date sent to.</summary>
    public DateTime? DateSentTo { get; set; }

    /// <summary>Token status: 0 both, 1 passed, 2 not yet passed.</summary>
    public string? TokenStatus { get; set; }
}

/// <summary>The ticked cases (CmdToAC_Click).</summary>
public class SendToArRequest
{
    /// <summary>Case numbers.</summary>
    public List<string> Cases { get; set; } = [];
}

/// <summary>CmdSearch_Click's check, plus the fixed list the drop-down offers.</summary>
public sealed class SendToArFilterValidator : RequestValidator<SendToArFilter>
{
    /// <summary>Declares the rules.</summary>
    public SendToArFilterValidator()
    {
        RuleFor(f => f).Must(f => !f.UseScan || !string.IsNullOrWhiteSpace(f.WksNoFrom)).WithMessage("Please Enter Case No");
        RuleFor(f => f.TokenStatus).Must(WorksheetLimits.IsTokenStatus).WithMessage("Select a valid token status.");
    }
}

/// <summary>CmdToAC_Click's check.</summary>
public sealed class SendToArRequestValidator : RequestValidator<SendToArRequest>
{
    /// <summary>Declares the rules.</summary>
    public SendToArRequestValidator() =>
        RuleFor(r => r.Cases).Must(cases => cases.Count > 0).WithMessage("Please select a record before proceeding!");
}

/// <summary>Reads the cases and marks them as sent to audit review.</summary>
public interface ISendToArRepository
{
    /// <summary>The cases that match the filter (at most <see cref="WorksheetLimits.FetchRows"/>).</summary>
    Task<List<SendToArRow>> SearchAsync(IDbSession session, SendToArFilter filter);

    /// <summary>Marks one case as sent to audit review today; returns a <see cref="StoredProcedures"/> result code.</summary>
    Task<int> SendAsync(IDbSession session, string cas, string userCode);
}

/// <inheritdoc />
public sealed class SendToArRepository : ISendToArRepository
{
    /// <inheritdoc />
    public Task<List<SendToArRow>> SearchAsync(IDbSession session, SendToArFilter f) =>
        session.Connection.ProcQueryAsync<SendToArRow>("genisis.WorksheetAr_Search", new
        {
            scan = f.UseScan, wksFrom = WorksheetLimits.Text(f.WksNoFrom), wksTo = f.UseScan ? null : WorksheetLimits.Text(f.WksNoTo),
            sentFrom = WorksheetLimits.Day(f.DateSentFrom), sentTo = WorksheetLimits.Day(f.DateSentTo),
            status = WorksheetLimits.Text(f.TokenStatus), max = WorksheetLimits.FetchRows,
        }, session.Transaction);

    /// <inheritdoc />
    public async Task<int> SendAsync(IDbSession session, string cas, string userCode) =>
        await session.Connection.ProcScalarAsync<int>("genisis.WorksheetAr_Send", new { cas = cas.Trim(), userCode }, session.Transaction);
}

/// <summary>Worksheet > Worksheet Send to A/R (legacy FrmARRecFrCLM, "Date Send to Audit Review").</summary>
public class SendToArService(IDbSessions sessions, ISendToArRepository repository, ILogger<SendToArService> log)
{
    /// <summary>CmdSearch_Click: the cases that match the criteria.</summary>
    public async Task<SearchResult<SendToArRow>> SearchAsync(SendToArFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        return WorksheetLimits.Cap(await repository.SearchAsync(session, filter));
    }

    /// <summary>
    /// CmdToAC_Click: marks the ticked cases as sent today, in one transaction. Throws <see cref="WorksheetException"/> when a case is
    /// gone; nothing is saved then. Returns how many cases were updated.
    /// </summary>
    public async Task<int> SendAsync(SendToArRequest request, string userCode)
    {
        var cases = request.Cases.Select(c => c.Trim()).Where(c => c.Length > 0).Distinct().ToList();
        await using var session = await sessions.BeginAsync();
        foreach (var cas in cases)
            if (await repository.SendAsync(session, cas, userCode) == StoredProcedures.NotFound)
                throw new WorksheetException($"Case {cas} was not found. Search again.");
        await session.CommitAsync();
        log.LogInformation("{Count} case(s) sent to audit review by {User}", cases.Count, userCode);
        return cases.Count;
    }
}
