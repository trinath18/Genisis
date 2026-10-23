using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Finance.SunPosting;

/// <summary>Fixed codes of Finance &gt; SUN Posting (legacy frmPostingInvoiceMBM).</summary>
public static class SunPostingCodes
{
    /// <summary>SUNPosting.PostType of member invoices (the desktop's PostingType "InvMBM").</summary>
    public const string PostType = "InvMBM";

    /// <summary>Post type P: positive invoice amounts, journal MJC.</summary>
    public const string Positive = "P";

    /// <summary>Post type N: negative invoice amounts, journal MJD.</summary>
    public const string Negative = "N";

    /// <summary>Search mode of the posting list (the desktop's &amp;Search).</summary>
    public const string ModeAll = "all";

    /// <summary>Search mode of the credit list (&amp;SearchCR).</summary>
    public const string ModeCredit = "credit";

    /// <summary>Search mode of the debit list (&amp;SearchDN).</summary>
    public const string ModeDebit = "debit";

    /// <summary>Journal source database column ("DB").</summary>
    public const string Database = "MED";

    /// <summary>The "Hospital" column of a member invoice journal line.</summary>
    public const string Hospital = "0R";

    /// <summary>Product category of health type S (the desktop's default before GetProductCat).</summary>
    public const string StandardProductCategory = "SM";

    /// <summary>Post status list (cboPostStatus).</summary>
    public static readonly CodeName[] PostStatuses = [new("0", "0 - No"), new("1", "1 - Yes")];

    /// <summary>Bordx status list (CboBordx).</summary>
    public static readonly CodeName[] BordxStatuses = [new("0", "0 - No"), new("1", "1 - Yes")];

    /// <summary>Post type list (cboPostType; the desktop's caption reads "Posivite").</summary>
    public static readonly CodeName[] PostTypes = [new(Positive, "P - Positive"), new(Negative, "N - Negative")];
}

/// <summary>The drop-down lists of the screen, and why posting is closed right now (null when it is open).</summary>
/// <param name="HealthTypes">Health types (HLT).</param>
/// <param name="Payors">Payors (PAY).</param>
/// <param name="AccountingPeriods">Accounting periods "MM/YYYY" (maintenance AccPeriod), newest first.</param>
/// <param name="PostStatuses">Post status list.</param>
/// <param name="BordxStatuses">Bordx status list.</param>
/// <param name="PostTypes">Post type list.</param>
/// <param name="PostingClosedMessage">The desktop's "Posting is ended at ..." message while the posting window is closed for this user.</param>
public record SunPostingLookups(
    List<CodeName> HealthTypes, List<CodeName> Payors, List<CodeName> AccountingPeriods,
    IReadOnlyList<CodeName> PostStatuses, IReadOnlyList<CodeName> BordxStatuses, IReadOnlyList<CodeName> PostTypes, string? PostingClosedMessage);

/// <summary>The criteria of the search; every "from"/"to" pair is inclusive and ignored when empty.</summary>
public class SunPostingFilter
{
    /// <summary>"all" (posting list, two journal lines per invoice), "credit" or "debit" (one line per invoice).</summary>
    public string Mode { get; set; } = SunPostingCodes.ModeAll;

    /// <summary>Health type code.</summary>
    public string? HealthType { get; set; }

    /// <summary>Payor code.</summary>
    public string? Payor { get; set; }

    /// <summary>Claim reg (invoice entered) date from.</summary>
    public DateTime? InvoiceDateFrom { get; set; }

    /// <summary>Claim reg (invoice entered) date to.</summary>
    public DateTime? InvoiceDateTo { get; set; }

    /// <summary>Claim (case) number from.</summary>
    public string? CaseNoFrom { get; set; }

    /// <summary>Claim (case) number to.</summary>
    public string? CaseNoTo { get; set; }

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

    /// <summary>Post type P (positive amounts) or N (negative amounts); used by the posting list only.</summary>
    public string PostType { get; set; } = SunPostingCodes.Positive;
}

/// <summary>One invoice of the view (a row of the legacy SUN_HIS_Posting 'InvMBM' result, plus patient name and product category).</summary>
public record SunPostingRow
{
    /// <summary>Case number.</summary>
    public string CaseNo { get; set; } = "";

    /// <summary>Claim (worksheet) version.</summary>
    public string ClaimVersion { get; set; } = "";

    /// <summary>Membership number.</summary>
    public string? MembershipNo { get; set; }

    /// <summary>Patient cover id (00 = the principal).</summary>
    public string? PatientCoverId { get; set; }

    /// <summary>Member (principal) name.</summary>
    public string? MemberName { get; set; }

    /// <summary>Patient name (covered person when the cover id is not 00).</summary>
    public string? PatientName { get; set; }

    /// <summary>Member invoice posted flag (1 = posted).</summary>
    public string? InvMStatus { get; set; }

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

    /// <summary>Group company.</summary>
    public string? GroupCompany { get; set; }

    /// <summary>Invoice entered (claim close) date.</summary>
    public DateTime? InvoiceDate { get; set; }

    /// <summary>Invoice amount payable by Medix to the member.</summary>
    public decimal? Amount { get; set; }

    /// <summary>Post batch number.</summary>
    public string? BatchNo { get; set; }

    /// <summary>Bordx date.</summary>
    public DateTime? BordxDate { get; set; }

    /// <summary>Bordx reference number.</summary>
    public string? BordxRefNo { get; set; }

    /// <summary>Bordx reversal indicator (Y = reversal).</summary>
    public string? BordxReversal { get; set; }

    /// <summary>PLN.PLNProCat of the health type / payor.</summary>
    public string? ProductCategory { get; set; }

    /// <summary>True when the invoice has been posted as a member invoice.</summary>
    public bool Posted => InvMStatus?.Trim() == "1";
}

/// <summary>One line of the SUN journal (a row of lstMBMInvList / lstMBMInvCDList).</summary>
public record JournalLine
{
    /// <summary>"Post" when the invoice is posted (the desktop's Chk column).</summary>
    public string Status { get; init; } = "";

    /// <summary>Journal type: MJC (P) or MJD (N).</summary>
    public string JournalType { get; init; } = "";

    /// <summary>Journal source (SUN log-in id; blank as in the desktop).</summary>
    public string JournalSource { get; init; } = "";

    /// <summary>Database: MED.</summary>
    public string Database { get; init; } = SunPostingCodes.Database;

    /// <summary>Transaction reference: case number - claim version.</summary>
    public string TransRef { get; init; } = "";

    /// <summary>Accounting period "MM/YYYY" of a posted invoice.</summary>
    public string? AccPeriod { get; init; }

    /// <summary>Transaction (invoice entered) date.</summary>
    public DateTime? TransDate { get; init; }

    /// <summary>Description: member or patient name, at most 25 characters.</summary>
    public string Description { get; init; } = "";

    /// <summary>Account code.</summary>
    public string AccCode { get; init; } = "";

    /// <summary>Amount.</summary>
    public decimal Amount { get; init; }

    /// <summary>D (debit) or C (credit).</summary>
    public string DebitCredit { get; init; } = "";

    /// <summary>Hospital column: 0R for member invoices.</summary>
    public string Hospital { get; init; } = SunPostingCodes.Hospital;

    /// <summary>Group company (posting list only).</summary>
    public string? GroupCompany { get; init; }

    /// <summary>Payor code.</summary>
    public string? Payor { get; init; }

    /// <summary>Case number without the payor prefix (characters 3 to 10).</summary>
    public string CaseNo { get; init; } = "";

    /// <summary>Product category.</summary>
    public string? ProductCategory { get; init; }

    /// <summary>Bordx reference (last 7 characters).</summary>
    public string? BordxRef { get; init; }

    /// <summary>Post batch number.</summary>
    public string? BatchNo { get; init; }

    /// <summary>The invoice key the Post button uses (case-version), on the first line of each pair as in the desktop.</summary>
    public string? Index { get; init; }

    /// <summary>Case number of the invoice (full), for selecting.</summary>
    public string InvoiceCaseNo { get; init; } = "";

    /// <summary>Claim version of the invoice, for selecting.</summary>
    public string InvoiceVersion { get; init; } = "";
}

/// <summary>The journal lines of a search with the footer figures.</summary>
/// <param name="Lines">Journal lines (two per invoice in the posting list, one in the credit / debit lists).</param>
/// <param name="Invoices">Number of invoices listed (the desktop's Record(s)).</param>
/// <param name="DebitTotal">Total of the D lines.</param>
/// <param name="CreditTotal">Total of the C lines.</param>
/// <param name="Posted">True when any listed invoice is already posted (the desktop's LblPost "Yes").</param>
/// <param name="Truncated">True when more than <see cref="FinanceLimits.MaxRows"/> invoices matched.</param>
public record SunPostingSearchResult(List<JournalLine> Lines, int Invoices, decimal DebitTotal, decimal CreditTotal, bool Posted, bool Truncated);

/// <summary>An invoice ticked for posting.</summary>
/// <param name="CaseNo">Case number.</param>
/// <param name="ClaimVersion">Claim version.</param>
public record SunPostingPick(string CaseNo, string ClaimVersion)
{
    /// <summary>"case-version", the desktop's Trans.Ref.</summary>
    public string Key => $"{CaseNo.Trim()}-{ClaimVersion.Trim()}";
}

/// <summary>The Post request (CmdPost_Click): the ticked invoices and the posting details.</summary>
public class SunPostRequest
{
    /// <summary>Accounting period "MM/YYYY".</summary>
    public string? AccPeriod { get; set; }

    /// <summary>Post type P or N (journal MJC / MJD and the journal file name).</summary>
    public string PostType { get; set; } = SunPostingCodes.Positive;

    /// <summary>Claim reg date "to" of the search (required by the desktop; used in the journal file name).</summary>
    public DateTime? InvoiceDateTo { get; set; }

    /// <summary>Bordx status of the search (the desktop refused to post invoices without a bordx).</summary>
    public string? BordxStatus { get; set; }

    /// <summary>The ticked invoices.</summary>
    public List<SunPostingPick> Items { get; set; } = [];
}

/// <summary>The result of a post.</summary>
/// <param name="BatchNo">The new post batch number.</param>
/// <param name="Posted">Number of invoices posted.</param>
/// <param name="Message">The desktop's confirmation.</param>
/// <param name="Journal">The journal lines of the batch (what the desktop re-listed and exported).</param>
public record SunPostResult(string BatchNo, int Posted, string Message, SunPostingSearchResult Journal);

/// <summary>Which journal file to build: a posted batch.</summary>
public class SunJournalQuery
{
    /// <summary>Post batch number.</summary>
    public string? BatchNo { get; set; }

    /// <summary>Post type P or N of the batch; when empty it is read from the batch (negative invoice amounts = N).</summary>
    public string? PostType { get; set; }

    /// <summary>Date in the file name (the desktop used the claim reg date "to"); today when empty.</summary>
    public DateTime? FileDate { get; set; }
}

/// <summary>A journal file to download.</summary>
/// <param name="FileName">File name.</param>
/// <param name="Content">CSV bytes.</param>
public record JournalFile(string FileName, byte[] Content);

/// <summary>The user's special access flag and the end of the posting window (TimeMain 'POSTING').</summary>
public record PostingWindow
{
    /// <summary>USR.SpecialAccess.</summary>
    public string? SpecialAccess { get; set; }

    /// <summary>TimeMain.EndTime (only the time of day counts).</summary>
    public DateTime? EndTime { get; set; }
}

/// <summary>The search criteria checks (the drop-down lists are fixed).</summary>
public sealed class SunPostingFilterValidator : RequestValidator<SunPostingFilter>
{
    /// <summary>Declares the rules.</summary>
    public SunPostingFilterValidator()
    {
        RuleFor(f => f.Mode).Must(m => FinanceLimits.IsBlankOr(m, SunPostingCodes.ModeAll, SunPostingCodes.ModeCredit, SunPostingCodes.ModeDebit))
            .WithMessage("Select a valid search.");
        RuleFor(f => f.PostStatus).Must(s => FinanceLimits.IsBlankOr(s, "0", "1")).WithMessage("Select a valid post status.");
        RuleFor(f => f.BordxStatus).Must(s => FinanceLimits.IsBlankOr(s, "0", "1")).WithMessage("Select a valid bordx status.");
        RuleFor(f => f.PostType).Must(t => t is SunPostingCodes.Positive or SunPostingCodes.Negative).WithMessage("Select a valid post type.");
    }
}

/// <summary>CmdPost_Click's checks, in the desktop's order and with its messages.</summary>
public sealed partial class SunPostRequestValidator : RequestValidator<SunPostRequest>
{
    /// <summary>Declares the rules.</summary>
    public SunPostRequestValidator()
    {
        RuleFor(r => r.Items).Must(items => items.Count > 0).WithMessage("Please select the record");
        RuleFor(r => r.InvoiceDateTo).NotNull().WithMessage("Please Enter Invoice Date(To)");
        RuleFor(r => r.BordxStatus).Must(s => s?.Trim() != "0").WithMessage("Please change Bordx Status");
        RuleFor(r => r.AccPeriod).NotEmpty().WithMessage("Please Keyin for Accounting Period ! ");
        RuleFor(r => r.AccPeriod).Must(p => Period().IsMatch(p!.Trim())).WithMessage("Select a valid accounting period.");
        RuleFor(r => r.PostType).Must(t => t is SunPostingCodes.Positive or SunPostingCodes.Negative).WithMessage("Select a valid post type.");
    }

    [GeneratedRegex(@"^(0[1-9]|1[0-2])/\d{4}$")]
    private static partial Regex Period();
}

/// <summary>The journal download checks.</summary>
public sealed class SunJournalQueryValidator : RequestValidator<SunJournalQuery>
{
    /// <summary>Declares the rules.</summary>
    public SunJournalQueryValidator()
    {
        RuleFor(q => q.BatchNo).NotEmpty().WithMessage("Please Enter Batch No");
        RuleFor(q => q.PostType).Must(t => FinanceLimits.IsBlankOr(t, SunPostingCodes.Positive, SunPostingCodes.Negative)).WithMessage("Select a valid post type.");
    }
}

/// <summary>The lists, the posting window, the invoices and the posting writes of the screen.</summary>
public interface ISunPostingRepository
{
    /// <summary>Health types, payors and accounting periods.</summary>
    Task<(List<CodeName> HealthTypes, List<CodeName> Payors, List<CodeName> Periods)> LookupsAsync(IDbSession session);

    /// <summary>The user's special access flag and the posting window end time.</summary>
    Task<PostingWindow> WindowAsync(IDbSession session, string userCode);

    /// <summary>The invoices that match (at most <see cref="FinanceLimits.FetchRows"/>). <paramref name="amountSign"/>: 1 positive, -1 negative, null both.</summary>
    Task<List<SunPostingRow>> SearchAsync(IDbSession session, SunPostingFilter filter, int? amountSign);

    /// <summary>Takes the next post batch number.</summary>
    Task<string> NextBatchAsync(IDbSession session);

    /// <summary>Posts one invoice; returns a <see cref="StoredProcedures"/> result code.</summary>
    Task<int> InsertAsync(IDbSession session, SunPostingPick pick, string accMonth, string accYear, string batchNo, string userCode);
}

/// <inheritdoc />
public sealed class SunPostingRepository : ISunPostingRepository
{
    /// <inheritdoc />
    public async Task<(List<CodeName> HealthTypes, List<CodeName> Payors, List<CodeName> Periods)> LookupsAsync(IDbSession session)
    {
        using var grid = await session.Connection.ProcMultipleAsync("genisis.SunPostingMbm_Lookups", null, session.Transaction);
        return ((await grid.ReadAsync<CodeName>()).ToList(), (await grid.ReadAsync<CodeName>()).ToList(), (await grid.ReadAsync<CodeName>()).ToList());
    }

    /// <inheritdoc />
    public async Task<PostingWindow> WindowAsync(IDbSession session, string userCode) =>
        await session.Connection.ProcFirstOrDefaultAsync<PostingWindow>("genisis.SunPosting_Window", new { userCode }, session.Transaction) ?? new PostingWindow();

    /// <inheritdoc />
    public Task<List<SunPostingRow>> SearchAsync(IDbSession session, SunPostingFilter f, int? amountSign) =>
        session.Connection.ProcQueryAsync<SunPostingRow>("genisis.SunPostingMbm_Search", new
        {
            healthType = FinanceLimits.Text(f.HealthType), payor = FinanceLimits.Text(f.Payor),
            invoiceFrom = FinanceLimits.Day(f.InvoiceDateFrom), invoiceTo = FinanceLimits.Day(f.InvoiceDateTo),
            caseFrom = FinanceLimits.Text(f.CaseNoFrom), caseTo = FinanceLimits.Text(f.CaseNoTo),
            postStatus = FinanceLimits.Text(f.PostStatus), postFrom = FinanceLimits.Day(f.PostDateFrom), postTo = FinanceLimits.Day(f.PostDateTo),
            bordxStatus = FinanceLimits.Text(f.BordxStatus), batchFrom = FinanceLimits.Text(f.BatchNoFrom), batchTo = FinanceLimits.Text(f.BatchNoTo),
            bordxFrom = FinanceLimits.Day(f.BordxDateFrom), bordxTo = FinanceLimits.Day(f.BordxDateTo),
            amountSign = (short?)amountSign, max = FinanceLimits.FetchRows,
        }, session.Transaction);

    /// <inheritdoc />
    public async Task<string> NextBatchAsync(IDbSession session) =>
        await session.Connection.ProcScalarAsync<string>("genisis.SunPosting_NextBatch", null, session.Transaction)
            ?? throw new InvalidOperationException("genisis.SunPosting_NextBatch returned no batch number.");

    /// <inheritdoc />
    public async Task<int> InsertAsync(IDbSession session, SunPostingPick pick, string accMonth, string accYear, string batchNo, string userCode) =>
        await session.Connection.ProcScalarAsync<int>("genisis.SunPostingMbm_Insert",
            new { cas = pick.CaseNo.Trim(), version = pick.ClaimVersion.Trim(), accMonth, accYear, batchNo, userCode }, session.Transaction);
}

/// <summary>The journal rules of MBMInvListing / MBMInvCRListing / MBMInvDRListing and the journal file (pure, unit-tested).</summary>
public static partial class SunJournal
{
    private const int DescriptionLength = 25;
    private const int BordxRefLength = 7;

    /// <summary>Journal type of a post type: MJC for P, MJD for N.</summary>
    public static string JournalType(string postType) => postType == SunPostingCodes.Positive ? "MJC" : "MJD";

    /// <summary>The product category: SM for health type S, otherwise PLN.PLNProCat (blank when none).</summary>
    public static string ProductCategory(SunPostingRow row) =>
        row.HealthType?.Trim() == "S" ? SunPostingCodes.StandardProductCategory : row.ProductCategory?.Trim() ?? "";

    /// <summary>
    /// The journal lines of a list. "all": two lines per invoice. Line 1: the member, account 8200 + payor (6200 + payor for a
    /// negative post of a bordx reversal), C for P / D for N. Line 2: the patient, account 6000 + payor, the opposite side. N amounts
    /// are shown positive. "credit": line 1 only as C, account 8200. "debit": line 2 only as D, account 6000. Amounts as stored.
    /// </summary>
    public static List<JournalLine> Build(IEnumerable<SunPostingRow> rows, string mode, string postType)
    {
        var lines = new List<JournalLine>();
        var journalType = JournalType(postType);
        foreach (var row in rows)
        {
            var payor = row.Payor?.Trim() ?? "";
            var amount = row.Amount ?? 0m;
            var common = new JournalLine
            {
                Status = row.Posted ? "Post" : "",
                JournalType = journalType,
                TransRef = $"{row.CaseNo.Trim()}-{row.ClaimVersion.Trim()}",
                AccPeriod = string.IsNullOrWhiteSpace(row.AccMonth) && string.IsNullOrWhiteSpace(row.AccYear) ? null : $"{row.AccMonth?.Trim()}/{row.AccYear?.Trim()}",
                TransDate = row.InvoiceDate,
                Payor = payor,
                CaseNo = CaseNoWithoutPayor(row.CaseNo),
                ProductCategory = ProductCategory(row),
                BordxRef = string.IsNullOrWhiteSpace(row.BordxRefNo) ? "" : Right(row.BordxRefNo.Trim(), BordxRefLength),
                BatchNo = row.BatchNo?.Trim() ?? "",
                InvoiceCaseNo = row.CaseNo.Trim(),
                InvoiceVersion = row.ClaimVersion.Trim(),
            };
            var member = Left(row.MemberName, DescriptionLength);
            var patient = Left(row.PatientName, DescriptionLength);

            if (mode == SunPostingCodes.ModeCredit)
            {
                lines.Add(common with { Description = member, AccCode = "8200" + payor, Amount = amount, DebitCredit = "C" });
                continue;
            }
            if (mode == SunPostingCodes.ModeDebit)
            {
                lines.Add(common with { Description = patient, AccCode = "6000" + payor, Amount = amount, DebitCredit = "D" });
                continue;
            }

            var positive = postType == SunPostingCodes.Positive;
            var lineAmount = positive ? amount : -amount;
            var firstAccount = !positive && row.BordxReversal?.Trim() == "Y" ? "6200" + payor : "8200" + payor;
            lines.Add(common with
            {
                Description = member, AccCode = firstAccount, Amount = lineAmount, DebitCredit = positive ? "C" : "D",
                GroupCompany = row.GroupCompany?.Trim() ?? "", Index = common.TransRef,
            });
            lines.Add(common with
            {
                Description = patient, AccCode = "6000" + payor, Amount = lineAmount, DebitCredit = positive ? "D" : "C",
                GroupCompany = row.GroupCompany?.Trim() ?? "",
            });
        }
        return lines;
    }

    /// <summary>The post type of a posted batch: N when its invoices carry negative amounts (the N search lists INVAmount &lt; 0), else P.</summary>
    public static string PostTypeOf(IEnumerable<SunPostingRow> rows) =>
        rows.Any(r => r.Amount < 0) ? SunPostingCodes.Negative : SunPostingCodes.Positive;

    /// <summary>The search result: lines, invoice count, D / C totals, posted flag, cut-off.</summary>
    public static SunPostingSearchResult Result(List<SunPostingRow> rows, string mode, string postType)
    {
        var truncated = rows.Count > FinanceLimits.MaxRows;
        var shown = truncated ? rows.Take(FinanceLimits.MaxRows).ToList() : rows;
        var lines = Build(shown, mode, postType);
        return new SunPostingSearchResult(lines, shown.Count, lines.Where(l => l.DebitCredit == "D").Sum(l => l.Amount),
            lines.Where(l => l.DebitCredit == "C").Sum(l => l.Amount), shown.Any(r => r.Posted), truncated);
    }

    /// <summary>The desktop's export name: "&lt;batch&gt; MJC-Member yyyymmdd" (MJD for N).</summary>
    public static string FileName(string batchNo, string postType, DateTime date) =>
        $"{SafeName().Replace(batchNo.Trim(), "")} {JournalType(postType)}-Member {date:yyyyMMdd}.csv";

    /// <summary>The journal as CSV with the desktop's column headings (UTF-8 with BOM so Excel opens it correctly).</summary>
    public static byte[] Csv(IEnumerable<JournalLine> lines)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Chk,J/T,J/S,DB,Trans.Ref.,AccPeriod,Trans.Date,Description,AccCode,Amt,D/C,Hospital,Grp Company,Payor,Case No,Product Cat.,Bordx Ref,BatchNo,Index");
        foreach (var l in lines)
            csv.AppendLine(string.Join(",", new[]
            {
                l.Status, l.JournalType, l.JournalSource, l.Database, l.TransRef, l.AccPeriod ?? "",
                l.TransDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "", l.Description, l.AccCode,
                l.Amount.ToString("0.00", CultureInfo.InvariantCulture), l.DebitCredit, l.Hospital, l.GroupCompany ?? "", l.Payor ?? "",
                l.CaseNo, l.ProductCategory ?? "", l.BordxRef ?? "", l.BatchNo ?? "", l.Index ?? "",
            }.Select(Quote)));
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

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

    /// <summary>Quotes a cell when needed, and neutralises leading = + @ so a spreadsheet does not run it as a formula.</summary>
    private static string Quote(string text)
    {
        if (text.Length > 0 && text[0] is '=' or '+' or '@' && !decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            text = "'" + text;
        return text.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }

    [GeneratedRegex("[^A-Za-z0-9]+")]
    private static partial Regex SafeName();
}

/// <summary>The posting window rule of Timer1_Timer / CheckTime (pure, unit-tested).</summary>
public static class SunPostingWindow
{
    /// <summary>
    /// The desktop closed the form with "Posting is ended at hh:mm:ss AM" while the time of day (to the minute) was not after
    /// TimeMain('POSTING').EndTime, unless the user has SpecialAccess = 'Y'. Reproduced as is; null means posting is open.
    /// </summary>
    public static string? ClosedMessage(PostingWindow window, DateTime now)
    {
        if (window.SpecialAccess?.Trim().ToUpperInvariant() == "Y" || window.EndTime is not { } end) return null;
        var current = new TimeSpan(now.Hour, now.Minute, 0);
        var ending = new TimeSpan(end.Hour, end.Minute, 0);
        return ending - current >= TimeSpan.Zero ? $"Posting is ended at {end.ToString("hh:mm:ss tt", CultureInfo.InvariantCulture)} ! " : null;
    }
}

/// <summary>Finance &gt; SUN Posting (legacy frmPostingInvoiceMBM, "Member Invoice Posting", menu position 13).</summary>
public class SunPostingService(IDbSessions sessions, ISunPostingRepository repository, IFinanceClock clock, ILogger<SunPostingService> log)
{
    /// <summary>ComboListing, plus whether the posting window is open for the user.</summary>
    public async Task<SunPostingLookups> LookupsAsync(string userCode)
    {
        await using var session = await sessions.OpenAsync();
        var (health, payors, periods) = await repository.LookupsAsync(session);
        var window = await repository.WindowAsync(session, userCode);
        return new SunPostingLookups(health, payors, periods, SunPostingCodes.PostStatuses, SunPostingCodes.BordxStatuses, SunPostingCodes.PostTypes,
            SunPostingWindow.ClosedMessage(window, clock.Now));
    }

    /// <summary>&amp;Search / &amp;SearchCR / &amp;SearchDN: the journal lines of the matching invoices.</summary>
    public async Task<SunPostingSearchResult> SearchAsync(SunPostingFilter filter)
    {
        var mode = string.IsNullOrWhiteSpace(filter.Mode) ? SunPostingCodes.ModeAll : filter.Mode.Trim();
        int? sign = mode == SunPostingCodes.ModeAll ? (filter.PostType == SunPostingCodes.Positive ? 1 : -1) : null;
        await using var session = await sessions.OpenAsync();
        return SunJournal.Result(await repository.SearchAsync(session, filter, sign), mode, filter.PostType);
    }

    /// <summary>
    /// CmdPost_Click: takes a batch number and posts every ticked invoice in one transaction. Refused while the posting window is closed
    /// for the user, or when an invoice is gone or already posted (nothing is saved then). Returns the batch and its journal.
    /// </summary>
    public async Task<SunPostResult> PostAsync(SunPostRequest request, string userCode)
    {
        var picks = request.Items.DistinctBy(p => p.Key).ToList();
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
                if (result == StoredProcedures.NotFound) throw new FinanceException($"Invoice {pick.Key} was not found. Search again.");
                if (result == StoredProcedures.Conflict) throw new FinanceException($"{pick.Key}: Record(s) has been posted ! ");
            }
            await session.CommitAsync();
        }
        log.LogInformation("SUN posting batch {Batch}: {Count} member invoice(s) posted by {User} for {Period}", batchNo, picks.Count, userCode, period);
        var journal = await JournalAsync(batchNo, request.PostType);
        return new SunPostResult(batchNo, picks.Count, "Record(s) Posted Sucessfully... ! ", journal);
    }

    /// <summary>The journal file of a posted batch (the desktop's export to the SUN import folder).</summary>
    public async Task<JournalFile> JournalFileAsync(SunJournalQuery query)
    {
        var batchNo = query.BatchNo!.Trim();
        var rows = await BatchRowsAsync(batchNo);
        if (rows.Count == 0) throw new NotFoundException("Report cannot be printed without any record.");
        var postType = FinanceLimits.Text(query.PostType) ?? SunJournal.PostTypeOf(rows);
        var journal = SunJournal.Result(rows, SunPostingCodes.ModeAll, postType);
        return new JournalFile(SunJournal.FileName(batchNo, postType, query.FileDate ?? clock.Now), SunJournal.Csv(journal.Lines));
    }

    /// <summary>MBMInvListing(" and BatchNo = ...") after a post: the batch's invoices, without the other criteria.</summary>
    private async Task<SunPostingSearchResult> JournalAsync(string batchNo, string postType) =>
        SunJournal.Result(await BatchRowsAsync(batchNo), SunPostingCodes.ModeAll, postType);

    private async Task<List<SunPostingRow>> BatchRowsAsync(string batchNo)
    {
        await using var session = await sessions.OpenAsync();
        return await repository.SearchAsync(session, new SunPostingFilter { BatchNoFrom = batchNo, BatchNoTo = batchNo, PostStatus = null, BordxStatus = null }, null);
    }
}
