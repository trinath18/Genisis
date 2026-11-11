using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;

namespace Genisis.Api.ClaimBordx.Revise;

/// <summary>One worksheet of a bordx (a row of the <c>lstWS</c> list of <c>FrmBordxRevise</c>); the bordx's own columns repeat on every row, as the procedure returned them.</summary>
public record BordxReviseRow
{
    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Claim version.</summary>
    public int ClaimVersion { get; set; }

    /// <summary>The worksheet's number as the list shows it: case - version.</summary>
    public string ClaimNo => $"{CaseNo.Trim()} - {ClaimVersion}";

    /// <summary>Bordx date.</summary>
    public DateTime? BordxDate { get; set; }

    /// <summary>Bordx printed date.</summary>
    public DateTime? BordxPrintedDate { get; set; }

    /// <summary>The bordx has been sent to the payor or to MNRB.</summary>
    public bool SentOut { get; set; }

    /// <summary>Date the worksheet was opened (Claim Reg Date).</summary>
    public DateTime? ClaimOpenDate { get; set; }

    /// <summary>Member number.</summary>
    public string? MbmNumber { get; set; }

    /// <summary>Patient name.</summary>
    public string? PatientName { get; set; }

    /// <summary>Date admitted.</summary>
    public DateTime? DateAdmitted { get; set; }

    /// <summary>Date discharged.</summary>
    public DateTime? DischargeDate { get; set; }

    /// <summary>Approved amount of the worksheet.</summary>
    public decimal? TotalApproved { get; set; }

    /// <summary>Case status.</summary>
    public string? CaseStatus { get; set; }
}

/// <summary>A bordx with its worksheets (<c>BordxReviseListing</c>): the date and printed date labels, the item count and the approved amount, and whether the bordx can still be revised.</summary>
public record BordxReviseResult(string BordxNo, DateTime? BordxDate, DateTime? PrintedDate, int Items, decimal TotalApproved, bool SentOut, List<BordxReviseRow> Rows);

/// <summary>The bordx to look up (the "Bordx No" box).</summary>
public class BordxReviseFilter
{
    /// <summary>Bordx number.</summary>
    public string? BordxNo { get; set; }
}

/// <summary>The ticked worksheets to take out of a bordx (<c>CmdReject_Click</c>).</summary>
public class BordxReviseRequest
{
    /// <summary>The bordx the worksheets are in.</summary>
    public string? BordxNo { get; set; }

    /// <summary>The ticked worksheets.</summary>
    public List<WorksheetPick> Items { get; set; } = [];
}

/// <summary>CmdGo_Click's check: a bordx number is needed.</summary>
public sealed class BordxReviseFilterValidator : RequestValidator<BordxReviseFilter>
{
    /// <summary>Declares the rules.</summary>
    public BordxReviseFilterValidator() => RuleFor(f => f.BordxNo).Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Please select a Bordx No");
}

/// <summary>CmdReject_Click's checks: a bordx and something ticked. The desktop did nothing when nothing was ticked; the web says so.</summary>
public sealed class BordxReviseRequestValidator : RequestValidator<BordxReviseRequest>
{
    /// <summary>Declares the rules.</summary>
    public BordxReviseRequestValidator()
    {
        RuleFor(r => r.BordxNo).Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Please select a Bordx No");
        RuleFor(r => r.Items).Must(items => items.Count > 0).WithMessage("Please select at least one record to be removed!");
    }
}

/// <summary>Reads the worksheets of a bordx and takes them out of it.</summary>
public interface IBordxReviseRepository
{
    /// <summary>The worksheets of the bordx.</summary>
    Task<List<BordxReviseRow>> SearchAsync(IDbSession session, string bordxNo);

    /// <summary>Takes one worksheet out of the bordx; returns 0, a <see cref="StoredProcedures"/> code (1 not found, 2 not in this bordx) or <see cref="BordxReviseCodes.SentOut"/>.</summary>
    Task<int> RemoveAsync(IDbSession session, string bordxNo, WorksheetPick pick, string userCode, DateTime today);
}

/// <summary>The result code <c>genisis.BordxRevise_Remove</c> adds to <see cref="StoredProcedures"/>'s.</summary>
public static class BordxReviseCodes
{
    /// <summary>The bordx has been sent to the payor or to MNRB.</summary>
    public const int SentOut = 3;
}

/// <inheritdoc />
public sealed class BordxReviseRepository : IBordxReviseRepository
{
    /// <inheritdoc />
    public Task<List<BordxReviseRow>> SearchAsync(IDbSession session, string bordxNo) =>
        session.Connection.ProcQueryAsync<BordxReviseRow>("genisis.BordxRevise_Search", new { bordxNo = bordxNo.Trim() }, session.Transaction);

    /// <inheritdoc />
    public async Task<int> RemoveAsync(IDbSession session, string bordxNo, WorksheetPick pick, string userCode, DateTime today) =>
        await session.Connection.ProcScalarAsync<int>("genisis.BordxRevise_Remove", new { bordxNo = bordxNo.Trim(), cas = pick.CaseNo.Trim(), version = pick.ClaimVersion, userCode, today = today.Date }, session.Transaction);
}

/// <summary>Claim Bordx > Worksheet > Remove Worksheet (legacy <c>FrmBordxRevise</c>, "Bordereaux Revise").</summary>
public class BordxReviseService(IDbSessions sessions, IBordxReviseRepository repository, ILogger<BordxReviseService> log)
{
    /// <summary>CmdGo_Click: the worksheets of a bordx; an unknown bordx has none.</summary>
    public async Task<BordxReviseResult> SearchAsync(BordxReviseFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        var rows = await repository.SearchAsync(session, filter.BordxNo!);
        var first = rows.FirstOrDefault();
        return new BordxReviseResult(filter.BordxNo!.Trim(), first?.BordxDate, first?.BordxPrintedDate, rows.Count, rows.Sum(r => r.TotalApproved ?? 0m), first?.SentOut ?? false, rows);
    }

    /// <summary>
    /// CmdReject_Click: takes the ticked worksheets out of the bordx in one transaction. Throws <see cref="BusinessRuleException"/> when a worksheet is gone or is not in the bordx or the bordx
    /// has been sent out; nothing is saved then. Returns how many worksheets were removed.
    /// </summary>
    public async Task<int> RemoveAsync(BordxReviseRequest request, string userCode)
    {
        var picks = request.Items.DistinctBy(p => (p.CaseNo.Trim(), p.ClaimVersion)).ToList();
        var bordxNo = request.BordxNo!.Trim();
        await using var session = await sessions.BeginAsync();
        foreach (var pick in picks)
        {
            var result = await repository.RemoveAsync(session, bordxNo, pick, userCode, DateTime.Today);
            if (result == BordxReviseCodes.SentOut) throw new BusinessRuleException("This Bordx has been sent out.");
            if (result == StoredProcedures.NotFound) throw new BusinessRuleException($"Worksheet {pick.WksNo} or bordx {bordxNo} was not found. Search again.");
            if (result == StoredProcedures.Conflict) throw new BusinessRuleException($"Worksheet {pick.WksNo} is not in bordx {bordxNo}. Search again.");
        }
        await session.CommitAsync();
        log.LogInformation("{Count} worksheet(s) removed from bordx {Bordx} by {User}", picks.Count, bordxNo, userCode);
        return picks.Count;
    }
}
