using System.Text.RegularExpressions;
using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Finance.SunPosting;

namespace Genisis.Api.Finance.SunPostingHosp;

/// <summary>A payor of the drop-down, with the health type it belongs to.</summary>
public record PayorOption(string Code, string Name, string? HealthType);

/// <summary>The drop-down lists of the screen, and why posting is closed right now (null when it is open).</summary>
public record SunPostingHospLookups(
    List<CodeName> HealthTypes, List<PayorOption> Payors, List<CodeName> Hospitals, List<CodeName> AccountingPeriods, IReadOnlyList<CodeName> PostStatuses, IReadOnlyList<CodeName> BordxStatuses, string? PostingClosedMessage);

/// <summary>The criteria of the search; every "from"/"to" pair is inclusive and ignored when empty.</summary>
public class SunPostingHospFilter
{
    /// <summary>"all" (posting list, two journal lines per invoice), "credit" or "debit" (one line per invoice).</summary>
    public string Mode { get; set; } = SunPostingCodes.ModeAll;

    /// <summary>Health type code.</summary>
    public string? HealthType { get; set; }

    /// <summary>Hospital (its name).</summary>
    public string? Hospital { get; set; }

    /// <summary>Payor code.</summary>
    public string? Payor { get; set; }

    /// <summary>Invoice date from.</summary>
    public DateTime? InvoiceDateFrom { get; set; }

    /// <summary>Invoice date to.</summary>
    public DateTime? InvoiceDateTo { get; set; }

    /// <summary>Invoice number from.</summary>
    public string? InvoiceNoFrom { get; set; }

    /// <summary>Invoice number to.</summary>
    public string? InvoiceNoTo { get; set; }

    /// <summary>Post status: 0 not posted, 1 posted, blank both.</summary>
    public string? PostStatus { get; set; } = "0";

    /// <summary>Post date from.</summary>
    public DateTime? PostDateFrom { get; set; }

    /// <summary>Post date to.</summary>
    public DateTime? PostDateTo { get; set; }

    /// <summary>Bordx status: 0 no bordx reference, 1 has one, blank both.</summary>
    public string? BordxStatus { get; set; } = "1";

    /// <summary>Post batch number from.</summary>
    public string? BatchNoFrom { get; set; }

    /// <summary>Post batch number to.</summary>
    public string? BatchNoTo { get; set; }

    /// <summary>Bordx date from.</summary>
    public DateTime? BordxDateFrom { get; set; }

    /// <summary>Bordx date to.</summary>
    public DateTime? BordxDateTo { get; set; }
}

/// <summary>One hospital invoice of the view (a row of the legacy SUN_HIS_Posting 'InvHosp' result, plus patient name and product category).</summary>
public sealed class SunPostingHospRow
{
    /// <summary>Invoice index (ClaimInvoice.INVIndex): the key the post uses.</summary>
    public string InvoiceIndex { get; set; } = "";

    /// <summary>Invoice number.</summary>
    public string? InvoiceNo { get; set; }

    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Claim version.</summary>
    public string ClaimVersion { get; set; } = "";

    /// <summary>Membership number.</summary>
    public string? MembershipNo { get; set; }

    /// <summary>Patient cover id (00 = the principal).</summary>
    public string? PatientCoverId { get; set; }

    /// <summary>Member name.</summary>
    public string? MemberName { get; set; }

    /// <summary>Patient name (the covered person when the cover id is not 00).</summary>
    public string? PatientName { get; set; }

    /// <summary>Hospital invoice posted flag (1 = posted).</summary>
    public string? InvHStatus { get; set; }

    /// <summary>Post status flag.</summary>
    public string? PostStatus { get; set; }

    /// <summary>Post date.</summary>
    public DateTime? PostDate { get; set; }

    /// <summary>Accounting month of the posting.</summary>
    public string? AccMonth { get; set; }

    /// <summary>Accounting year of the posting.</summary>
    public string? AccYear { get; set; }

    /// <summary>Health type.</summary>
    public string? HealthType { get; set; }

    /// <summary>Payor code.</summary>
    public string? Payor { get; set; }

    /// <summary>Invoice date.</summary>
    public DateTime? InvoiceDate { get; set; }

    /// <summary>Amount payable to the hospital.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Post batch number.</summary>
    public string? BatchNo { get; set; }

    /// <summary>Bordx date.</summary>
    public DateTime? BordxDate { get; set; }

    /// <summary>Bordx reference number.</summary>
    public string? BordxRefNo { get; set; }

    /// <summary>The hospital's account code (HOS.HOSAccCode).</summary>
    public string? HospitalAccCode { get; set; }

    /// <summary>The hospital's name.</summary>
    public string? HospitalName { get; set; }

    /// <summary>PLN.PLNProCat of the health type and payor.</summary>
    public string? ProductCategory { get; set; }

    /// <summary>True when the invoice has been posted as a hospital invoice.</summary>
    public bool Posted => InvHStatus?.Trim() == "1";
}

/// <summary>The Post request (CmdPost_Click): the ticked invoices and the posting details.</summary>
public class SunPostHospRequest
{
    /// <summary>Accounting period "MM/YYYY".</summary>
    public string? AccPeriod { get; set; }

    /// <summary>Invoice date "to" of the search (required by the desktop; used in the journal file name).</summary>
    public DateTime? InvoiceDateTo { get; set; }

    /// <summary>Bordx status of the search (the desktop refused to post invoices without a bordx).</summary>
    public string? BordxStatus { get; set; }

    /// <summary>The ticked invoices (ClaimInvoice.INVIndex).</summary>
    public List<string> InvoiceIndexes { get; set; } = [];
}

/// <summary>Which journal file to build: a posted batch.</summary>
public class SunHospJournalQuery
{
    /// <summary>Post batch number.</summary>
    public string? BatchNo { get; set; }

    /// <summary>Date in the file name (the desktop used the invoice date "to"); today when empty.</summary>
    public DateTime? FileDate { get; set; }
}

/// <summary>The search criteria checks (the drop-down lists are fixed).</summary>
public sealed class SunPostingHospFilterValidator : RequestValidator<SunPostingHospFilter>
{
    /// <summary>Declares the rules.</summary>
    public SunPostingHospFilterValidator()
    {
        RuleFor(f => f.Mode).Must(m => FinanceLimits.IsBlankOr(m, SunPostingCodes.ModeAll, SunPostingCodes.ModeCredit, SunPostingCodes.ModeDebit)).WithMessage("Select a valid search.");
        RuleFor(f => f.PostStatus).Must(s => FinanceLimits.IsBlankOr(s, "0", "1")).WithMessage("Select a valid post status.");
        RuleFor(f => f.BordxStatus).Must(s => FinanceLimits.IsBlankOr(s, "0", "1")).WithMessage("Select a valid bordx status.");
    }
}

/// <summary>CmdPost_Click's checks, in the desktop's order and with its messages.</summary>
public sealed partial class SunPostHospRequestValidator : RequestValidator<SunPostHospRequest>
{
    /// <summary>Declares the rules.</summary>
    public SunPostHospRequestValidator()
    {
        RuleFor(r => r.InvoiceIndexes).Must(items => items.Count > 0).WithMessage("Please select the record");
        RuleFor(r => r.InvoiceDateTo).NotNull().WithMessage("Please Enter Invoice Date(To)");
        RuleFor(r => r.BordxStatus).Must(s => s?.Trim() != "0").WithMessage("Please Change Bordx Status");
        RuleFor(r => r.AccPeriod).NotEmpty().WithMessage("Please Keyin for Accounting Period ! ");
        RuleFor(r => r.AccPeriod).Must(p => Period().IsMatch(p!.Trim())).WithMessage("Select a valid accounting period.");
    }

    [GeneratedRegex(@"^(0[1-9]|1[0-2])/\d{4}$")]
    private static partial Regex Period();
}

/// <summary>The journal download checks.</summary>
public sealed class SunHospJournalQueryValidator : RequestValidator<SunHospJournalQuery>
{
    /// <summary>Declares the rules.</summary>
    public SunHospJournalQueryValidator() => RuleFor(q => q.BatchNo).NotEmpty().WithMessage("Please Enter Batch No");
}

/// <summary>The lists, the invoices and the posting write of the screen.</summary>
public interface ISunPostingHospRepository
{
    /// <summary>Health types, payors, hospitals and accounting periods.</summary>
    Task<(List<CodeName> HealthTypes, List<PayorOption> Payors, List<CodeName> Hospitals, List<CodeName> Periods)> LookupsAsync(IDbSession session);

    /// <summary>The user's special access flag and the posting window end time.</summary>
    Task<PostingWindow> WindowAsync(IDbSession session, string userCode);

    /// <summary>The invoices that match (at most <see cref="FinanceLimits.FetchRows"/>).</summary>
    Task<List<SunPostingHospRow>> SearchAsync(IDbSession session, SunPostingHospFilter filter);

    /// <summary>Takes the next post batch number.</summary>
    Task<string> NextBatchAsync(IDbSession session);

    /// <summary>Posts one invoice; returns a <see cref="StoredProcedures"/> result code.</summary>
    Task<int> InsertAsync(IDbSession session, string invoiceIndex, string accMonth, string accYear, string batchNo, string userCode);
}

/// <inheritdoc />
public sealed class SunPostingHospRepository : ISunPostingHospRepository
{
    /// <inheritdoc />
    public async Task<(List<CodeName> HealthTypes, List<PayorOption> Payors, List<CodeName> Hospitals, List<CodeName> Periods)> LookupsAsync(IDbSession session)
    {
        using var grid = await session.Connection.ProcMultipleAsync("genisis.SunPostingHosp_Lookups", null, session.Transaction);
        return ((await grid.ReadAsync<CodeName>()).ToList(), (await grid.ReadAsync<PayorOption>()).ToList(), (await grid.ReadAsync<CodeName>()).ToList(), (await grid.ReadAsync<CodeName>()).ToList());
    }

    /// <inheritdoc />
    public async Task<PostingWindow> WindowAsync(IDbSession session, string userCode) =>
        await session.Connection.ProcFirstOrDefaultAsync<PostingWindow>("genisis.SunPosting_Window", new { userCode }, session.Transaction) ?? new PostingWindow();

    /// <inheritdoc />
    public Task<List<SunPostingHospRow>> SearchAsync(IDbSession session, SunPostingHospFilter f) =>
        session.Connection.ProcQueryAsync<SunPostingHospRow>("genisis.SunPostingHosp_Search", new
        {
            healthType = FinanceLimits.Text(f.HealthType), hospital = FinanceLimits.Text(f.Hospital), payor = FinanceLimits.Text(f.Payor), invoiceFrom = FinanceLimits.Day(f.InvoiceDateFrom),
            invoiceTo = FinanceLimits.Day(f.InvoiceDateTo), invNoFrom = FinanceLimits.Text(f.InvoiceNoFrom), invNoTo = FinanceLimits.Text(f.InvoiceNoTo), postStatus = FinanceLimits.Text(f.PostStatus),
            postFrom = FinanceLimits.Day(f.PostDateFrom), postTo = FinanceLimits.Day(f.PostDateTo), bordxStatus = FinanceLimits.Text(f.BordxStatus), batchFrom = FinanceLimits.Text(f.BatchNoFrom),
            batchTo = FinanceLimits.Text(f.BatchNoTo), bordxFrom = FinanceLimits.Day(f.BordxDateFrom), bordxTo = FinanceLimits.Day(f.BordxDateTo), max = FinanceLimits.FetchRows,
        }, session.Transaction);

    /// <inheritdoc />
    public async Task<string> NextBatchAsync(IDbSession session) =>
        await session.Connection.ProcScalarAsync<string>("genisis.SunPosting_NextBatch", null, session.Transaction) ?? throw new InvalidOperationException("genisis.SunPosting_NextBatch returned no batch number.");

    /// <inheritdoc />
    public async Task<int> InsertAsync(IDbSession session, string invoiceIndex, string accMonth, string accYear, string batchNo, string userCode) =>
        await session.Connection.ProcScalarAsync<int>("genisis.SunPostingHosp_Insert", new { invIndex = invoiceIndex, accMonth, accYear, batchNo, userCode }, session.Transaction);
}

/// <summary>The journal rules of HospInvListing / HospInvCRListing / HospInvDNListing and the journal file (pure, unit-tested).</summary>
public static class SunJournalHosp
{
    private const int DescriptionLength = 25;
    private const int BordxRefLength = 7;

    /// <summary>The journal type of every hospital invoice line.</summary>
    public const string JournalType = "RC";

    /// <summary>The product category: SM for health type S, otherwise PLN.PLNProCat (blank when none).</summary>
    public static string ProductCategory(SunPostingHospRow row) => row.HealthType?.Trim() == "S" ? SunPostingCodes.StandardProductCategory : row.ProductCategory?.Trim() ?? "";

    /// <summary>The hospital column: the hospital's account code without its first character.</summary>
    public static string HospitalColumn(string? accCode)
    {
        var trimmed = accCode?.Trim() ?? "";
        return trimmed.Length <= 1 ? "" : trimmed[1..];
    }

    /// <summary>
    /// The journal lines of a list; <see cref="JournalLine.InvoiceCaseNo"/> holds the invoice index (a case version can have more than one hospital invoice), which ticks the two lines of an invoice together. "all": two lines per invoice, the debit to account 6000 + payor and the credit to the hospital's account (HOSAccCode), the amount payable to the hospital, journal RC, the case
    /// number as case-version. "credit": the credit line only, "debit": the debit line only, each with the case number alone (as the desktop's lists showed it).
    /// </summary>
    public static List<JournalLine> Build(IEnumerable<SunPostingHospRow> rows, string mode)
    {
        var lines = new List<JournalLine>();
        foreach (var row in rows)
        {
            var payor = row.Payor?.Trim() ?? "";
            var amount = row.Amount ?? 0m;
            var caseNo = CaseNoWithoutPayor(row.CaseNo);
            var all = mode == SunPostingCodes.ModeAll;
            var common = new JournalLine
            {
                Status = row.Posted ? "Post" : "",
                JournalType = JournalType,
                TransRef = row.InvoiceNo?.Trim() ?? "",
                AccPeriod = string.IsNullOrWhiteSpace(row.AccMonth) && string.IsNullOrWhiteSpace(row.AccYear) ? null : $"{row.AccMonth?.Trim()}/{row.AccYear?.Trim()}",
                TransDate = row.InvoiceDate,
                Description = Left(row.PatientName, DescriptionLength),
                Amount = amount,
                Hospital = HospitalColumn(row.HospitalAccCode),
                GroupCompany = "",
                Payor = payor,
                CaseNo = all ? $"{caseNo}-{row.ClaimVersion.Trim()}" : caseNo,
                ProductCategory = ProductCategory(row),
                BordxRef = string.IsNullOrWhiteSpace(row.BordxRefNo) ? "" : Right(row.BordxRefNo.Trim(), BordxRefLength),
                BatchNo = row.BatchNo?.Trim() ?? "",
                InvoiceCaseNo = row.InvoiceIndex.Trim(),
                InvoiceVersion = "",
            };
            var debit = common with { AccCode = "6000" + payor, DebitCredit = "D" };
            var credit = common with { AccCode = row.HospitalAccCode?.Trim() ?? "", DebitCredit = "C", Index = row.InvoiceIndex.Trim() };
            if (mode == SunPostingCodes.ModeCredit) lines.Add(credit);
            else if (mode == SunPostingCodes.ModeDebit) lines.Add(debit);
            else lines.AddRange([debit, credit]);
        }

        return lines;
    }

    /// <summary>The search result: lines, invoice count, D / C totals, posted flag, cut-off.</summary>
    public static SunPostingSearchResult Result(List<SunPostingHospRow> rows, string mode)
    {
        var truncated = rows.Count > FinanceLimits.MaxRows;
        var shown = truncated ? rows.Take(FinanceLimits.MaxRows).ToList() : rows;
        var lines = Build(shown, mode);
        return new SunPostingSearchResult(lines, shown.Count, lines.Where(l => l.DebitCredit == "D").Sum(l => l.Amount), lines.Where(l => l.DebitCredit == "C").Sum(l => l.Amount), shown.Any(r => r.Posted), truncated);
    }

    /// <summary>The desktop's export name: "&lt;batch&gt; RC-HospInv yyyymmdd".</summary>
    public static string FileName(string batchNo, DateTime date) => $"{Regex.Replace(batchNo.Trim(), "[^A-Za-z0-9]+", "")} RC-HospInv {date:yyyyMMdd}.csv";

    private static string CaseNoWithoutPayor(string caseNo)
    {
        var trimmed = caseNo.Trim();
        return trimmed.Length <= 2 ? "" : trimmed.Substring(2, Math.Min(8, trimmed.Length - 2));
    }

    private static string Left(string? text, int length)
    {
        var trimmed = text?.Trim() ?? "";
        return trimmed.Length <= length ? trimmed : trimmed[..length];
    }

    private static string Right(string text, int length) => text.Length <= length ? text : text[^length..];
}

/// <summary>Posting > Invoice > Hospital (legacy frmPostingInvoiceHosp, "Posting - HospInv").</summary>
public class SunPostingHospService(IDbSessions sessions, ISunPostingHospRepository repository, IFinanceClock clock, ILogger<SunPostingHospService> log)
{
    /// <summary>ComboListing, plus whether the posting window is open for the user.</summary>
    public async Task<SunPostingHospLookups> LookupsAsync(string userCode)
    {
        await using var session = await sessions.OpenAsync();
        var (health, payors, hospitals, periods) = await repository.LookupsAsync(session);
        var window = await repository.WindowAsync(session, userCode);
        return new SunPostingHospLookups(health, payors, hospitals, periods, SunPostingCodes.PostStatuses, SunPostingCodes.BordxStatuses, SunPostingWindow.ClosedMessage(window, clock.Now));
    }

    /// <summary>&amp;Search / &amp;SearchCR / &amp;SearchDN: the journal lines of the matching invoices.</summary>
    public async Task<SunPostingSearchResult> SearchAsync(SunPostingHospFilter filter)
    {
        var mode = string.IsNullOrWhiteSpace(filter.Mode) ? SunPostingCodes.ModeAll : filter.Mode.Trim();
        await using var session = await sessions.OpenAsync();
        return SunJournalHosp.Result(await repository.SearchAsync(session, filter), mode);
    }

    /// <summary>
    /// CmdPost_Click: takes a batch number and posts every ticked invoice in one transaction. Refused while the posting window is closed for the user, or when an invoice is gone or already posted (nothing is
    /// saved then). Returns the batch and its journal.
    /// </summary>
    public async Task<SunPostResult> PostAsync(SunPostHospRequest request, string userCode)
    {
        var picks = request.InvoiceIndexes.Select(i => i.Trim()).Where(i => i != "").Distinct().ToList();
        var period = request.AccPeriod!.Trim();
        var (month, year) = (period[..2], period[3..]);
        string batchNo;
        await using (var session = await sessions.BeginAsync())
        {
            if (SunPostingWindow.ClosedMessage(await repository.WindowAsync(session, userCode), clock.Now) is { } closed) throw new FinanceException(closed);
            batchNo = await repository.NextBatchAsync(session);
            foreach (var pick in picks)
            {
                var result = await repository.InsertAsync(session, pick, month, year, batchNo, userCode);
                if (result == StoredProcedures.NotFound) throw new FinanceException($"Invoice {pick} was not found. Search again.");
                if (result == StoredProcedures.Conflict) throw new FinanceException($"{pick}: Record(s) has been posted ! ");
            }

            await session.CommitAsync();
        }

        log.LogInformation("SUN posting batch {Batch}: {Count} hospital invoice(s) posted by {User} for {Period}", batchNo, picks.Count, userCode, period);
        return new SunPostResult(batchNo, picks.Count, "Record(s) Posted Sucessfully... ! ", SunJournalHosp.Result(await BatchRowsAsync(batchNo), SunPostingCodes.ModeAll));
    }

    /// <summary>The journal file of a posted batch (the desktop's export to the SUN import folder).</summary>
    public async Task<JournalFile> JournalFileAsync(SunHospJournalQuery query)
    {
        var batchNo = query.BatchNo!.Trim();
        var rows = await BatchRowsAsync(batchNo);
        if (rows.Count == 0) throw new NotFoundException("Report cannot be printed without any record.");
        return new JournalFile(SunJournalHosp.FileName(batchNo, query.FileDate ?? clock.Now), SunJournal.Csv(SunJournalHosp.Result(rows, SunPostingCodes.ModeAll).Lines));
    }

    private async Task<List<SunPostingHospRow>> BatchRowsAsync(string batchNo)
    {
        await using var session = await sessions.OpenAsync();
        return await repository.SearchAsync(session, new SunPostingHospFilter { BatchNoFrom = batchNo, BatchNoTo = batchNo, PostStatus = null, BordxStatus = null });
    }
}
