using System.Globalization;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Pluto;

namespace Genisis.Api.ClaimBordx.SendDate;

/// <summary>
/// The three uploads of Claim Bordx > Bor Delivery > Bordx Send Date (legacy <c>frmUploadApproval</c>, "Upload Send/Approval/Return"; the menu item has no click handler and
/// <c>frmUploadBordxSentDate</c>, the form whose name matches, is a copy of it without code).
/// </summary>
public static class BordxSendDateKinds
{
    /// <summary>"Send Date" (cmdUploadSend): the bordx's sent dates.</summary>
    public const string Send = "send";

    /// <summary>"App Date" (cmdUpload): the payor's approval code and date on the bordx's claims.</summary>
    public const string Approval = "approval";

    /// <summary>"Return Date" (cmdReturnDate): the date the claims came back from the payor.</summary>
    public const string Return = "return";

    /// <summary>The route constraint the controller uses.</summary>
    public const string RoutePattern = "{kind:regex(^(send|approval|return)$)}";
}

/// <summary>The Excel columns each upload reads (1 = column A), as the desktop's loops read them.</summary>
public static class BordxSendDateLayout
{
    /// <summary>The first data row (row 1 is the header).</summary>
    public const int FirstRow = 2;

    /// <summary>The desktop's loop stopped at row 60,000.</summary>
    public const int LastRow = 59_999;

    /// <summary>The largest file accepted.</summary>
    public const long MaximumFileBytes = 30L * 1024 * 1024;

    /// <summary>(bordx, amount, approval code, approval date, date) columns of an upload; 0 = not read.</summary>
    public static (int Bordx, int Amount, int AppCode, int AppDate, int Date) Columns(string kind) => kind switch
    {
        BordxSendDateKinds.Approval => (1, 10, 8, 9, 0),
        BordxSendDateKinds.Return => (2, 3, 18, 19, 20),
        _ => (2, 3, 0, 0, 20),
    };
}

/// <summary>The messages of the screen.</summary>
public static class BordxSendDateMessages
{
    /// <summary>No file was chosen.</summary>
    public const string FileRequired = "Please select file to upload!";

    /// <summary>The file is not a workbook.</summary>
    public const string NotAnExcelFile = "The file is not an Excel workbook.";

    /// <summary>Row 2 has an empty column A.</summary>
    public const string NoRows = "The file has no records (the rows start at row 2, and column A of a row must not be empty).";

    /// <summary>The verification found errors (the desktop's message).</summary>
    public const string Errors = "Error encoutered. Please check the file!";

    /// <summary>The verification found none.</summary>
    public const string Verified = "File verified successfully!";

    /// <summary>The upload was written.</summary>
    public const string Uploaded = "Records have been uploaded!";
}

/// <summary>One row of the file (a row of <c>lstSendDate</c> / <c>lstErrorList</c>), with what is wrong with it.</summary>
public record BordxSendDateLine
{
    /// <summary>The Excel row.</summary>
    public int Line { get; set; }

    /// <summary>Bordx number.</summary>
    public string BordxNo { get; set; } = "";

    /// <summary>The amount as the file has it.</summary>
    public string Amount { get; set; } = "";

    /// <summary>The payor's approval code (approval and return files).</summary>
    public string? AppCode { get; set; }

    /// <summary>The approval date as the file has it (approval and return files).</summary>
    public string? AppDate { get; set; }

    /// <summary>The send or return date as the file has it.</summary>
    public string? Date { get; set; }

    /// <summary>What is wrong with the row; empty when it is fine.</summary>
    public List<string> Problems { get; set; } = [];

    internal DateTime? ParsedDate { get; set; }

    internal DateTime? ParsedAppDate { get; set; }
}

/// <summary>What a verification or an upload found.</summary>
public record BordxSendDateResult
{
    /// <summary>The upload: send, approval or return.</summary>
    public string Kind { get; set; } = "";

    /// <summary>Every row of the file.</summary>
    public List<BordxSendDateLine> Rows { get; set; } = [];

    /// <summary>How many rows have a problem.</summary>
    public int Errors { get; set; }

    /// <summary>How many rows were written (0 for a verification).</summary>
    public int Updated { get; set; }

    /// <summary>The message the desktop showed.</summary>
    public string Message { get; set; } = "";
}

/// <summary>Reads the rows of an upload and checks them as the desktop's "Verify" buttons did.</summary>
public static class BordxSendDateReader
{
    /// <summary>The rows from row 2 until the first row whose column A is empty.</summary>
    public static List<BordxSendDateLine> Read(string kind, IReadOnlyList<object?[]> sheet)
    {
        var (bordx, amount, appCode, appDate, date) = BordxSendDateLayout.Columns(kind);
        var lines = new List<BordxSendDateLine>();
        for (var index = BordxSendDateLayout.FirstRow - 1; index < sheet.Count && index < BordxSendDateLayout.LastRow; index++)
        {
            var cells = sheet[index];
            if (PlutoExcelReader.Text(Cell(cells, 1)) == "") break;
            var line = new BordxSendDateLine
            {
                Line = index + 1,
                BordxNo = PlutoExcelReader.Text(Cell(cells, bordx)),
                Amount = PlutoExcelReader.Text(Cell(cells, amount)),
                AppCode = appCode == 0 ? null : PlutoExcelReader.Text(Cell(cells, appCode)),
                AppDate = appDate == 0 ? null : PlutoExcelReader.Text(Cell(cells, appDate)),
                Date = date == 0 ? null : PlutoExcelReader.Text(Cell(cells, date)),
                ParsedAppDate = appDate == 0 ? null : PlutoExcelReader.Date(Cell(cells, appDate)),
                ParsedDate = date == 0 ? null : PlutoExcelReader.Date(Cell(cells, date)),
            };
            line.Problems = Check(kind, line);
            lines.Add(line);
        }
        return lines;
    }

    /// <summary>
    /// cmdVerify / cmdVerifySend / cmdVerifyReturn: a bordx number of 8 or 10 characters starting with N or S, a numeric amount, and the upload's own date (send or return date; for an approval a code of
    /// 12 characters and an approval date).
    /// </summary>
    public static List<string> Check(string kind, BordxSendDateLine line)
    {
        var problems = new List<string>();
        if (line.BordxNo.Length is not (8 or 10)) problems.Add("Bordx No must have 8 or 10 characters.");
        if (!line.BordxNo.StartsWith('N') && !line.BordxNo.StartsWith('S')) problems.Add("Bordx No must start with N or S.");
        if (!decimal.TryParse(line.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out _)) problems.Add("Bordx Amount is not a number.");
        if (kind == BordxSendDateKinds.Approval)
        {
            if ((line.AppCode ?? "").Length != 12) problems.Add("Approval Code must have 12 characters.");
            if (line.ParsedAppDate is null) problems.Add("Approval Date is not a date.");
        }
        else if (line.ParsedDate is null)
        {
            problems.Add(kind == BordxSendDateKinds.Return ? "Return Date is not a date." : "Send Date is not a date.");
        }
        return problems;
    }

    /// <summary>A bordx number the lookup can take (letters and digits only).</summary>
    public static bool Plain(string bordxNo) => bordxNo.Length > 0 && bordxNo.All(char.IsAsciiLetterOrDigit);

    private static object? Cell(object?[] cells, int column) => column >= 1 && column <= cells.Length ? cells[column - 1] : null;
}

/// <summary>Looks up and writes the bordx of an upload.</summary>
public interface IBordxSendDateRepository
{
    /// <summary>The numbers of <paramref name="bordxNos"/> that exist.</summary>
    Task<HashSet<string>> KnownAsync(IDbSession session, IEnumerable<string> bordxNos);

    /// <summary>UpdateSent; a <see cref="StoredProcedures"/> result code.</summary>
    Task<int> SendAsync(IDbSession session, string bordxNo, DateTime date, string userCode);

    /// <summary>cmdUpload's update of the claims; a <see cref="StoredProcedures"/> result code.</summary>
    Task<int> ApproveAsync(IDbSession session, string bordxNo, string appCode, DateTime appDate);

    /// <summary>UpdateReturnPayor; a <see cref="StoredProcedures"/> result code.</summary>
    Task<int> ReturnAsync(IDbSession session, string bordxNo, DateTime date, string userCode, DateTime today);
}

/// <inheritdoc />
public sealed class BordxSendDateRepository : IBordxSendDateRepository
{
    /// <inheritdoc />
    public async Task<HashSet<string>> KnownAsync(IDbSession session, IEnumerable<string> bordxNos)
    {
        var numbers = bordxNos.Where(BordxSendDateReader.Plain).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (numbers.Count == 0) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var known = await session.Connection.ProcQueryAsync<string>("genisis.BordxSendDate_Known", new { numbers = string.Join('|', numbers) }, session.Transaction);
        return new HashSet<string>(known, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<int> SendAsync(IDbSession session, string bordxNo, DateTime date, string userCode) =>
        await session.Connection.ProcScalarAsync<int>("genisis.BordxSendDate_Send", new { bordxNo, date = date.Date, userCode }, session.Transaction);

    /// <inheritdoc />
    public async Task<int> ApproveAsync(IDbSession session, string bordxNo, string appCode, DateTime appDate) =>
        await session.Connection.ProcScalarAsync<int>("genisis.BordxSendDate_Approve", new { bordxNo, appCode, appDate = appDate.Date }, session.Transaction);

    /// <inheritdoc />
    public async Task<int> ReturnAsync(IDbSession session, string bordxNo, DateTime date, string userCode, DateTime today) =>
        await session.Connection.ProcScalarAsync<int>("genisis.BordxSendDate_Return", new { bordxNo, date = date.Date, userCode, today = today.Date }, session.Transaction);
}

/// <summary>Claim Bordx > Bor Delivery > Bordx Send Date (legacy frmUploadApproval): verify a file, then upload it.</summary>
public class BordxSendDateService(IDbSessions sessions, IBordxSendDateRepository repository, TimeProvider clock, ILogger<BordxSendDateService> log)
{
    /// <summary>The "Verify" buttons: every row of the file and what is wrong with it. A bordx that does not exist is an error here (the desktop's update found no row and said nothing).</summary>
    public async Task<BordxSendDateResult> VerifyAsync(string kind, IReadOnlyList<object?[]> sheet)
    {
        await using var session = await sessions.OpenAsync();
        return await CheckAsync(session, kind, sheet);
    }

    /// <summary>
    /// The upload buttons: checks the file again and writes every row in one transaction. Throws <see cref="BusinessRuleException"/> when a row has a problem; nothing is written then (the desktop
    /// enabled the upload button only after a clean verification).
    /// </summary>
    public async Task<BordxSendDateResult> UploadAsync(string kind, IReadOnlyList<object?[]> sheet, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        var result = await CheckAsync(session, kind, sheet);
        if (result.Rows.Count == 0) throw new BusinessRuleException(BordxSendDateMessages.NoRows);
        if (result.Errors > 0) throw new BusinessRuleException(BordxSendDateMessages.Errors);
        var today = clock.GetLocalNow().Date;
        foreach (var line in result.Rows)
        {
            var code = kind switch
            {
                BordxSendDateKinds.Approval => await repository.ApproveAsync(session, line.BordxNo, line.AppCode!, line.ParsedAppDate!.Value),
                BordxSendDateKinds.Return => await repository.ReturnAsync(session, line.BordxNo, line.ParsedDate!.Value, userCode, today),
                _ => await repository.SendAsync(session, line.BordxNo, line.ParsedDate!.Value, userCode),
            };
            if (code == StoredProcedures.NotFound) throw new BusinessRuleException($"Bordx {line.BordxNo} (row {line.Line}) was not found.");
        }
        await session.CommitAsync();
        log.LogInformation("Bordx Send Date: {Count} row(s) of a {Kind} file uploaded by {User}", result.Rows.Count, kind, userCode);
        return result with { Updated = result.Rows.Count, Message = BordxSendDateMessages.Uploaded };
    }

    private async Task<BordxSendDateResult> CheckAsync(IDbSession session, string kind, IReadOnlyList<object?[]> sheet)
    {
        var rows = BordxSendDateReader.Read(kind, sheet);
        var known = await repository.KnownAsync(session, rows.Where(r => r.Problems.Count == 0).Select(r => r.BordxNo));
        foreach (var row in rows.Where(r => r.Problems.Count == 0 && !known.Contains(r.BordxNo))) row.Problems.Add("Bordx No was not found.");
        var errors = rows.Count(r => r.Problems.Count > 0);
        var message = rows.Count == 0 ? BordxSendDateMessages.NoRows : errors > 0 ? BordxSendDateMessages.Errors : BordxSendDateMessages.Verified;
        return new BordxSendDateResult { Kind = kind, Rows = rows, Errors = errors, Message = message };
    }
}
