using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;

namespace Genisis.Api.ClaimBordx.Delivery;

/// <summary>The two steps of the delivery chain this screen family covers: a bordx sent to its payor (<c>FrmSendtoPayor</c>) or sent to accounts (<c>FrmSendtoAC</c>).</summary>
public static class BordxDeliveryKinds
{
    /// <summary>The route name of "Send to Payor".</summary>
    public const string ToPayor = "to-payor";

    /// <summary>The route name of "Send to A/C".</summary>
    public const string ToAccounts = "to-accounts";

    /// <summary>The route constraint both controllers use.</summary>
    public const string RoutePattern = "{kind:regex(^(to-payor|to-accounts)$)}";

    /// <summary>The code the SQL procedures take for a route name: <c>P</c> or <c>A</c>.</summary>
    public static char Code(string kind) => kind == ToPayor ? 'P' : 'A';

    /// <summary>The name of the step in the messages.</summary>
    public static string Label(string kind) => kind == ToPayor ? "to payor" : "to accounts";
}

/// <summary>One bordx of the delivery list (a row of <c>lstBordxDelivery</c>).</summary>
public record BordxDeliveryRow
{
    /// <summary>Payor code.</summary>
    public string? Payor { get; set; }

    /// <summary>Bordx number (<c>BordxRefNo</c>).</summary>
    public string BordxNo { get; set; } = "";

    /// <summary>Bordx date.</summary>
    public DateTime? BordxDate { get; set; }

    /// <summary>Bordx claim amount.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Bordx status (C closed, R returned...).</summary>
    public string? BordxStatus { get; set; }

    /// <summary>The screen's own step is done: sent to the payor, or sent to accounts.</summary>
    public bool Sent { get; set; }

    /// <summary>The date of that step.</summary>
    public DateTime? SentOn { get; set; }

    /// <summary>Who did that step.</summary>
    public string? SentBy { get; set; }

    /// <summary>Date sent to the payor.</summary>
    public DateTime? ToPayorDate { get; set; }

    /// <summary>Date received back from the payor.</summary>
    public DateTime? FromPayorDate { get; set; }

    /// <summary>Date sent to MNRB.</summary>
    public DateTime? ToMnrbDate { get; set; }

    /// <summary>Date received back from MNRB.</summary>
    public DateTime? FromMnrbDate { get; set; }

    /// <summary>A/C period, month.</summary>
    public string? PeriodMonth { get; set; }

    /// <summary>A/C period, year.</summary>
    public string? PeriodYear { get; set; }
}

/// <summary>The criteria of the search: every "from"/"to" pair is inclusive and ignored when empty.</summary>
public class BordxDeliveryFilter
{
    /// <summary>"Use Scan": one bordx number instead of a range.</summary>
    public bool UseScan { get; set; }

    /// <summary>Payor code.</summary>
    public string? Payor { get; set; }

    /// <summary>Bordx number, or the start of the range.</summary>
    public string? BordxFrom { get; set; }

    /// <summary>End of the bordx number range (range mode only).</summary>
    public string? BordxTo { get; set; }

    /// <summary>Bordx date from.</summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>Bordx date to.</summary>
    public DateTime? DateTo { get; set; }

    /// <summary>Bordx amount from.</summary>
    public decimal? AmountFrom { get; set; }

    /// <summary>Bordx amount to.</summary>
    public decimal? AmountTo { get; set; }

    /// <summary>A/C month from (send to payor only; compared as text, as the desktop did).</summary>
    public string? MonthFrom { get; set; }

    /// <summary>A/C month to (send to payor only).</summary>
    public string? MonthTo { get; set; }

    /// <summary>A/C year from (send to payor only).</summary>
    public string? YearFrom { get; set; }

    /// <summary>A/C year to (send to payor only).</summary>
    public string? YearTo { get; set; }

    /// <summary>Date sent to accounts from (send to A/C only).</summary>
    public DateTime? SentFrom { get; set; }

    /// <summary>Date sent to accounts to (send to A/C only).</summary>
    public DateTime? SentTo { get; set; }

    /// <summary>Status: <c>0</c> not sent, <c>1</c> sent, <c>2</c> both (the screen starts on not sent).</summary>
    public string? Status { get; set; }
}

/// <summary>The ticked bordx and the date to record (<c>CmdToPayor_Click</c>, <c>CmdToAC_Click</c>).</summary>
public class BordxDeliveryRequest
{
    /// <summary>The date of the step.</summary>
    public DateTime? Date { get; set; }

    /// <summary>The bordx numbers that were ticked.</summary>
    public List<string> BordxNos { get; set; } = [];
}

/// <summary>CmdSearch_Click's checks: in a scan a payor and a bordx number are needed, in that order.</summary>
public sealed class BordxDeliveryFilterValidator : RequestValidator<BordxDeliveryFilter>
{
    /// <summary>Declares the rules.</summary>
    public BordxDeliveryFilterValidator()
    {
        RuleFor(f => f).Must(f => !f.UseScan || !string.IsNullOrWhiteSpace(f.Payor)).WithMessage("Please select a Payor");
        RuleFor(f => f).Must(f => !f.UseScan || !string.IsNullOrWhiteSpace(f.BordxFrom)).WithMessage("Please Enter Bordx No");
        RuleFor(f => f.Status).Must(s => string.IsNullOrWhiteSpace(s) || s.Trim() is "0" or "1" or "2").WithMessage("Select a valid status.");
    }
}

/// <summary>The update button's checks, in the desktop's order: something ticked, then the date.</summary>
public sealed class BordxDeliveryRequestValidator : RequestValidator<BordxDeliveryRequest>
{
    /// <summary>Declares the rules.</summary>
    public BordxDeliveryRequestValidator()
    {
        RuleFor(r => r.BordxNos).Must(n => n.Any(x => !string.IsNullOrWhiteSpace(x))).WithMessage("Please select at least one record to be updated!");
        RuleFor(r => r.Date).NotNull().WithMessage("Please Enter in date");
    }
}

/// <summary>Reads the bordx of the delivery lists and marks them as sent.</summary>
public interface IBordxDeliveryRepository
{
    /// <summary>The bordx that match the filter (at most <see cref="WorksheetLimits.FetchRows"/>).</summary>
    Task<List<BordxDeliveryRow>> SearchAsync(IDbSession session, char kind, BordxDeliveryFilter filter);

    /// <summary>Marks one bordx as sent; returns a <see cref="StoredProcedures"/> result code.</summary>
    Task<int> SendAsync(IDbSession session, char kind, string bordxNo, DateTime date, string userCode);
}

/// <inheritdoc />
public sealed class BordxDeliveryRepository : IBordxDeliveryRepository
{
    /// <inheritdoc />
    public Task<List<BordxDeliveryRow>> SearchAsync(IDbSession session, char kind, BordxDeliveryFilter f) =>
        session.Connection.ProcQueryAsync<BordxDeliveryRow>("genisis.BordxDelivery_Search", new
        {
            kind = kind.ToString(), scan = f.UseScan, payor = WorksheetLimits.Text(f.Payor), bordxFrom = WorksheetLimits.Text(f.BordxFrom), bordxTo = f.UseScan ? null : WorksheetLimits.Text(f.BordxTo),
            dateFrom = WorksheetLimits.Day(f.DateFrom), dateTo = WorksheetLimits.Day(f.DateTo), amountFrom = f.AmountFrom, amountTo = f.AmountTo,
            monthFrom = WorksheetLimits.Text(f.MonthFrom), monthTo = WorksheetLimits.Text(f.MonthTo), yearFrom = WorksheetLimits.Text(f.YearFrom), yearTo = WorksheetLimits.Text(f.YearTo),
            sentFrom = WorksheetLimits.Day(f.SentFrom), sentTo = WorksheetLimits.Day(f.SentTo), status = WorksheetLimits.Text(f.Status), max = WorksheetLimits.FetchRows,
        }, session.Transaction);

    /// <inheritdoc />
    public async Task<int> SendAsync(IDbSession session, char kind, string bordxNo, DateTime date, string userCode) =>
        await session.Connection.ProcScalarAsync<int>("genisis.BordxDelivery_Send", new { kind = kind.ToString(), bordxNo = bordxNo.Trim(), date = date.Date, userCode }, session.Transaction);
}

/// <summary>Claim Bordx > Bor Delivery > Send to Payor and Send to A/C (legacy <c>FrmSendtoPayor</c> and <c>FrmSendtoAC</c>).</summary>
public class BordxDeliveryService(IDbSessions sessions, IBordxDeliveryRepository repository, ILogger<BordxDeliveryService> log)
{
    /// <summary>CmdSearch_Click: the bordx that match the criteria.</summary>
    public async Task<SearchResult<BordxDeliveryRow>> SearchAsync(string kind, BordxDeliveryFilter filter)
    {
        await using var session = await sessions.OpenAsync();
        return WorksheetLimits.Cap(await repository.SearchAsync(session, BordxDeliveryKinds.Code(kind), filter));
    }

    /// <summary>
    /// CmdToPayor_Click / CmdToAC_Click: records the date on the ticked bordx in one transaction. Throws <see cref="BusinessRuleException"/> when a bordx is gone or was already sent; nothing is saved
    /// then. Returns how many bordx were updated.
    /// </summary>
    public async Task<int> SendAsync(string kind, BordxDeliveryRequest request, string userCode)
    {
        var numbers = request.BordxNos.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        await using var session = await sessions.BeginAsync();
        foreach (var number in numbers)
        {
            var result = await repository.SendAsync(session, BordxDeliveryKinds.Code(kind), number, request.Date!.Value.Date, userCode);
            if (result == StoredProcedures.NotFound) throw new BusinessRuleException($"Bordx {number} was not found. Search again.");
            if (result == StoredProcedures.Conflict) throw new BusinessRuleException($"{number}: Record has been Updated!");
        }
        await session.CommitAsync();
        log.LogInformation("{Count} bordx sent {Step} by {User} on {Date:yyyy-MM-dd}", numbers.Count, BordxDeliveryKinds.Label(kind), userCode, request.Date);
        return numbers.Count;
    }
}
