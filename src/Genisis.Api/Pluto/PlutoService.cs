using System.Data;
using Dapper;
using Genisis.Api.Common.Errors;
using Genisis.Api.Data;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Pluto;

public sealed record PlutoUploadResult(string Message, int Rows);

public sealed record PlutoPayor(string Code, string? Name);

/// <summary>Desktop cboHLTCode: S - Sihat, N - Non-Sihat.</summary>
public static class PlutoHealthCodes
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>([Genisis.Api.Membership.HealthTypes.Sihat, Genisis.Api.Membership.HealthTypes.NonSihat]);
}

public sealed class PlutoAsciiRequest
{
    public string? HealthCode { get; set; }
    public string? PayorCode { get; set; }
    public DateTime? BordxDate { get; set; }
}

public interface IPlutoService
{
    Task<PlutoUploadResult> UploadAsync(IReadOnlyList<PlutoUploadRow> rows, CancellationToken cancellationToken);
    Task<PlutoAsciiFile?> ExportAsciiAsync(PlutoAsciiRequest request, CancellationToken cancellationToken);
    Task<List<PlutoPayor>> GetPayorsAsync(string healthCode, CancellationToken cancellationToken);
}

/// <summary>Upload Membership Excel (Pluto) and Generate Membership ASCII (Pluto).</summary>
public sealed class PlutoService(DbConnections db, PlutoAsciiExporter exporter) : IPlutoService
{
    public async Task<PlutoUploadResult> UploadAsync(IReadOnlyList<PlutoUploadRow> rows, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync(cancellationToken);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await InsertAsync(conn, tx, rows, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return new PlutoUploadResult(PlutoMessages.Uploaded, rows.Count);
    }

    /// <summary>All rows in the caller's transaction (the tests roll it back), as the desktop committed only after the last row.</summary>
    internal static async Task InsertAsync(SqlConnection conn, SqlTransaction tx, IReadOnlyList<PlutoUploadRow> rows, CancellationToken cancellationToken)
    {
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await conn.ProcExecAsync("genisis.Pluto_InsertUpload", new DynamicParameters(row.Values), tx);
            }
            catch (SqlException e) when (SqlErrorNumbers.IsDuplicateKey(e.Number))
            {
                throw new PlutoException(PlutoMessages.AlreadyUploaded(row.RowNumber, row.Values[PlutoExcelLayout.NumberColumn]));
            }
        }
    }

    public async Task<List<PlutoPayor>> GetPayorsAsync(string healthCode, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync(cancellationToken);
        return await conn.ProcQueryAsync<PlutoPayor>("genisis.Pluto_Payors", new { hlt = healthCode.Trim().ToUpperInvariant() });
    }

    public Task<PlutoAsciiFile?> ExportAsciiAsync(PlutoAsciiRequest request, CancellationToken cancellationToken) =>
        exporter.ExportAsync(request.PayorCode!.Trim().ToUpperInvariant(), request.BordxDate!.Value, cancellationToken);

    /// <summary>CmdGenerate_Click checks, in the desktop's order.</summary>
    public static string? Validate(PlutoAsciiRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.HealthCode)) return PlutoMessages.EnterHealthCode;
        if (string.IsNullOrWhiteSpace(request.PayorCode)) return PlutoMessages.EnterPayorCode;
        if (request.BordxDate is null) return PlutoMessages.EnterBordxDate;
        return null;
    }
}
