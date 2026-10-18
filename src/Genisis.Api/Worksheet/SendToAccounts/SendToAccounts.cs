using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Worksheet.SendToAccounts;

/// <summary>One worksheet of the Send to A/C list (a row of the FrmWksSendToAC list).</summary>
public record SendToAccountsRow
{
    /// <summary>Payor code (the first two letters of the case number).</summary>
    public string? Payor { get; set; }

    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Claim version.</summary>
    public int ClaimVersion { get; set; }

    /// <summary>Date the worksheet was opened (Wks Date).</summary>
    public DateTime? WksDate { get; set; }

    /// <summary>Worksheet amount (the bordx amount).</summary>
    public decimal? WksAmount { get; set; }

    /// <summary>Date sent to accounts.</summary>
    public DateTime? DateToAc { get; set; }

    /// <summary>User who passed it to accounts.</summary>
    public string? PassedBy { get; set; }

    /// <summary>Already sent to accounts (token status 1).</summary>
    public bool Sent { get; set; }

    /// <summary>Hospital cheque date.</summary>
    public DateTime? HosChequeDate { get; set; }

    /// <summary>Member cheque date.</summary>
    public DateTime? MbmChequeDate { get; set; }

    /// <summary>Bordx number.</summary>
    public string? BordxNo { get; set; }
}

/// <summary>The criteria of the Send to A/C search: every "from"/"to" pair is inclusive and ignored when empty.</summary>
public class SendToAccountsFilter
{
    /// <summary>"Use Scan": one worksheet (case) number instead of a range.</summary>
    public bool UseScan { get; set; } = true;

    /// <summary>Case number, or the start of the range.</summary>
    public string? WksNoFrom { get; set; }

    /// <summary>End of the case number range (range mode only).</summary>
    public string? WksNoTo { get; set; }

    /// <summary>Payor code.</summary>
    public string? Payor { get; set; }

    /// <summary>Worksheet date from.</summary>
    public DateTime? WksDateFrom { get; set; }

    /// <summary>Worksheet date to.</summary>
    public DateTime? WksDateTo { get; set; }

    /// <summary>Worksheet amount from.</summary>
    public decimal? AmountFrom { get; set; }

    /// <summary>Worksheet amount to.</summary>
    public decimal? AmountTo { get; set; }

    /// <summary>Date sent from.</summary>
    public DateTime? DateSentFrom { get; set; }

    /// <summary>Date sent to.</summary>
    public DateTime? DateSentTo { get; set; }

    /// <summary>Token status: 0 both, 1 passed, 2 not yet passed.</summary>
    public string? TokenStatus { get; set; }

    /// <summary>Bordx number from.</summary>
    public string? BordxFrom { get; set; }

    /// <summary>Bordx number to.</summary>
    public string? BordxTo { get; set; }
}

/// <summary>The ticked worksheets and the date to record (CmdToAC_Click).</summary>
public class SendToAccountsRequest
{
    /// <summary>"Date Sent".</summary>
    public DateTime? Date { get; set; }

    /// <summary>The ticked worksheets.</summary>
    public List<WorksheetPick> Items { get; set; } = [];
}

/// <summary>CmdSearch_Click's check, plus the fixed list the drop-down offers.</summary>
public sealed class SendToAccountsFilterValidator : RequestValidator<SendToAccountsFilter>
{
    /// <summary>Declares the rules.</summary>
    public SendToAccountsFilterValidator()
    {
        RuleFor(f => f).Must(f => !f.UseScan || !string.IsNullOrWhiteSpace(f.WksNoFrom)).WithMessage("Please Enter Worksheet No");
        RuleFor(f => f.TokenStatus).Must(WorksheetLimits.IsTokenStatus).WithMessage("Select a valid token status.");
    }
}

/// <summary>CmdToAC_Click's checks, in the desktop's order.</summary>
public sealed class SendToAccountsRequestValidator : RequestValidator<SendToAccountsRequest>
{
    /// <summary>Declares the rules.</summary>
    public SendToAccountsRequestValidator()
    {
        RuleFor(r => r.Date).NotNull().WithMessage("Please Enter in date");
        RuleFor(r => r.Items).Must(items => items.Count > 0).WithMessage("Please select at least one record to be updated!");
    }
}

/// <summary>Reads the worksheets waiting to be sent and marks them as sent.</summary>
public interface ISendToAccountsRepository
{
    /// <summary>The worksheets that match the filter (at most <see cref="WorksheetLimits.FetchRows"/>).</summary>
    Task<List<SendToAccountsRow>> SearchAsync(IDbSession session, SendToAccountsFilter filter);

    /// <summary>Marks one worksheet as sent; returns a <see cref="StoredProcedures"/> result code.</summary>
    Task<int> SendAsync(IDbSession session, WorksheetPick pick, DateTime date, string userCode);
}

/// <inheritdoc />
public sealed class SendToAccountsRepository : ISendToAccountsRepository
{
    /// <inheritdoc />
    public Task<List<SendToAccountsRow>> SearchAsync(IDbSession session, SendToAccountsFilter f) =>
        session.Connection.ProcQueryAsync<SendToAccountsRow>("genisis.WorksheetAccounts_Search", new
        {
            scan = f.UseScan, wksFrom = WorksheetLimits.Text(f.WksNoFrom), wksTo = f.UseScan ? null : WorksheetLimits.Text(f.WksNoTo),
            payor = WorksheetLimits.Text(f.Payor), openFrom = WorksheetLimits.Day(f.WksDateFrom), openTo = WorksheetLimits.Day(f.WksDateTo),
            amountFrom = f.AmountFrom, amountTo = f.AmountTo, sentFrom = WorksheetLimits.Day(f.DateSentFrom), sentTo = WorksheetLimits.Day(f.DateSentTo),
            tokenStatus = WorksheetLimits.Text(f.TokenStatus), bordxFrom = WorksheetLimits.Text(f.BordxFrom), bordxTo = WorksheetLimits.Text(f.BordxTo),
            max = WorksheetLimits.FetchRows,
        }, session.Transaction);

    /// <inheritdoc />
    public async Task<int> SendAsync(IDbSession session, WorksheetPick pick, DateTime date, string userCode) =>
        await session.Connection.ProcScalarAsync<int>("genisis.WorksheetAccounts_Send",
            new { cas = pick.CaseNo.Trim(), version = pick.ClaimVersion, date = date.Date, userCode }, session.Transaction);
}

/// <summary>Worksheet > Worksheet Send to A/C (legacy FrmWksSendToAC, "Claims Send To A/C").</summary>
public class SendToAccountsService(IDbSessions sessions, ISendToAccountsRepository repository, ILogger<SendToAccountsService> log)
{
    /// <summary>CmdSearch_Click: the worksheets that match the criteria.</summary>
    public async Task<SearchResult<SendToAccountsRow>> SearchAsync(SendToAccountsFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        return WorksheetLimits.Cap(await repository.SearchAsync(session, filter));
    }

    /// <summary>
    /// CmdToAC_Click: records the date on the ticked worksheets and adds the "sent to account" line to their case remarks, in one
    /// transaction. Throws <see cref="WorksheetException"/> when a worksheet is gone or was already sent; nothing is saved then.
    /// Returns how many worksheets were updated.
    /// </summary>
    public async Task<int> SendAsync(SendToAccountsRequest request, string userCode)
    {
        var picks = request.Items.DistinctBy(p => (p.CaseNo.Trim(), p.ClaimVersion)).ToList();
        await using var session = await sessions.BeginAsync();
        foreach (var pick in picks)
        {
            var result = await repository.SendAsync(session, pick, request.Date!.Value, userCode);
            if (result == StoredProcedures.NotFound) throw new WorksheetException($"Worksheet {pick.WksNo} was not found. Search again.");
            if (result == StoredProcedures.Conflict) throw new WorksheetException($"{pick.WksNo}: Record has been Updated!");
        }
        await session.CommitAsync();
        log.LogInformation("{Count} worksheet(s) sent to accounts by {User} on {Date:yyyy-MM-dd}", picks.Count, userCode, request.Date);
        return picks.Count;
    }
}
