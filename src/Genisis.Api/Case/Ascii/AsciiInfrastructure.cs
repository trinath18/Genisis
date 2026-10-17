using System.Data;
using Dapper;
using Genisis.Api.Data;
using static Genisis.Api.Data.RowMapper;

namespace Genisis.Api.Case.Ascii;

/// <inheritdoc />
public sealed class AsciiRepository : IAsciiRepository
{
    private static async Task<List<Dictionary<string, object?>>> RowsAsync(IDbSession session, string procedure, object parameters) =>
        ToDictionaries(await session.Connection.QueryAsync(procedure, parameters, session.Transaction, commandType: CommandType.StoredProcedure));

    /// <inheritdoc />
    public Task<List<Dictionary<string, object?>>> GetAcceptedClaimsAsync(IDbSession session, string payor, DateTime from, DateTime to, bool reprint, string worksheetStatus) =>
        RowsAsync(session, "genisis.AsciiClaim_Accepted", new { pay = payor, dateFrom = from, dateTo = to, reprint, wsStatus = worksheetStatus });

    /// <inheritdoc />
    public Task<List<Dictionary<string, object?>>> GetRejectedClaimsAsync(IDbSession session, string payor, DateTime from, DateTime to) =>
        RowsAsync(session, "genisis.AsciiClaim_Rejected", new { pay = payor, dateFrom = from, dateTo = to });

    /// <inheritdoc />
    public Task<List<Dictionary<string, object?>>> GetCasesAsync(IDbSession session, string payor, DateTime from, DateTime to, bool reprint, string endorsement) =>
        RowsAsync(session, "genisis.AsciiCase_Rows", new { pay = payor, dateFrom = from, dateTo = to, reprint, endorsement });

    /// <inheritdoc />
    public Task<string> NextBatchAsync(IDbSession session, string kind, string payor, DateTime date) =>
        session.Connection.ProcSingleAsync<string>("genisis.Ascii_NextBatch", new { kind, pay = payor, date }, session.Transaction);

    /// <inheritdoc />
    public Task MarkClaimExportedAsync(IDbSession session, string cas, int version) =>
        session.Connection.ProcExecAsync("genisis.AsciiClaim_MarkExported", new { cas, version }, session.Transaction);

    /// <inheritdoc />
    public Task MarkCaseExportedAsync(IDbSession session, string cas) =>
        session.Connection.ProcExecAsync("genisis.AsciiCase_MarkExported", new { cas }, session.Transaction);
}

/// <summary>
/// The payor files folder: <c>Ascii:ClaimFolder</c> / <c>Ascii:CaseFolder</c>, else <c>ClaimASCI</c> / <c>CaseASCII</c> under
/// <c>Documents:RootPath</c> (the desktop's SSRVPath). File names carry the database's batch number, so concurrent exports never share a file.
/// </summary>
public sealed class AsciiFileStore(IConfiguration configuration) : IAsciiFileStore
{
    /// <summary>Kind of the Claim ASCII folder.</summary>
    public const string ClaimKind = "claim";

    /// <summary>Kind of the Case ASCII folder.</summary>
    public const string CaseKind = "case";

    private string? Folder(string kind)
    {
        var configured = configuration[kind == ClaimKind ? "Ascii:ClaimFolder" : "Ascii:CaseFolder"];
        if (!string.IsNullOrWhiteSpace(configured)) return Path.GetFullPath(configured);
        var root = configuration["Documents:RootPath"];
        return string.IsNullOrWhiteSpace(root) ? null : Path.Combine(Path.GetFullPath(root), kind == ClaimKind ? "ClaimASCI" : "CaseASCII");
    }

    /// <inheritdoc />
    public async Task<string> WriteAsync(string kind, string name, string text)
    {
        var folder = Folder(kind) ?? throw new AsciiException(
            kind == ClaimKind
                ? "The Claim ASCII output folder is not configured (Ascii:ClaimFolder or Documents:RootPath)."
                : "The Case ASCII output folder is not configured (Ascii:CaseFolder or Documents:RootPath).");
        try
        {
            Directory.CreateDirectory(folder);
            await File.WriteAllBytesAsync(Path.Combine(folder, name), AsciiFormat.Encode(text));
        }
        catch (IOException ex)
        {
            throw new AsciiException($"The file could not be written to the server folder: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new AsciiException($"The file could not be written to the server folder: {ex.Message}");
        }
        return name;
    }

    /// <inheritdoc />
    public string? Find(string kind, string name)
    {
        if (Folder(kind) is not { } folder || string.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name)) return null;
        var path = Path.Combine(folder, name);
        return File.Exists(path) ? path : null;
    }
}
