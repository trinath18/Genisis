using System.Data;
using Dapper;
using Genisis.Api.Data;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Bordx;

public interface IBordxService
{
    Task<BordxLookups> GetLookupsAsync(CancellationToken cancellationToken);

    /// <summary>Verify: every problem in the file, nothing written.</summary>
    Task<VerifyResult> VerifyAsync(IReadOnlyList<BordxLine> lines, BordxSettings settings, string fileName, CancellationToken cancellationToken);

    /// <summary>Upload: the whole file in one transaction; any failure (or cancellation) imports nothing.</summary>
    Task<UploadResult> UploadAsync(IReadOnlyList<BordxLine> lines, BordxSettings settings, string userCode, CancellationToken cancellationToken);

    /// <returns>The ASCII file, or <c>null</c> when no member matches.</returns>
    Task<AsciiFile?> ExportAsciiAsync(AsciiRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Membership &gt; Upload Bordx (legacy FrmNewMBMEndt10), standard layout and its ZURICH / TAK variant. Uploads go through the desktop's
/// UploadDb staging procedures (TmpUpload_*, then SaveUploadDate) under a per-run staging key.
/// </summary>
public sealed class BordxService(DbConnections db, BordxAsciiExporter asciiExporter, ILogger<BordxService> logger) : IBordxService
{
    /// <summary>Payor row of genisis.Registration_Lookups (it also carries the payor's health type).</summary>
    private sealed record PayorRow(string Code, string Name, string? HealthCode);

    public async Task<BordxLookups> GetLookupsAsync(CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        using var grid = await conn.QueryMultipleAsync(new CommandDefinition("genisis.Registration_Lookups",
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
        var healthCodes = (await grid.ReadAsync<BordxOption>()).ToList();
        var payors = (await grid.ReadAsync<PayorRow>()).Select(p => new BordxOption(p.Code, p.Name)).ToList();
        return new BordxLookups(healthCodes, payors, BordxAsciiExporter.PlanFilters);
    }

    public async Task<VerifyResult> VerifyAsync(IReadOnlyList<BordxLine> lines, BordxSettings settings, string fileName, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync(cancellationToken);
        var format = await BordxFormat.DetectAsync(conn, null, lines[0], settings.PayorCode);
        return await new BordxFileVerifier(conn, lines, settings, fileName, format, cancellationToken).VerifyAsync();
    }

    public async Task<UploadResult> UploadAsync(IReadOnlyList<BordxLine> lines, BordxSettings settings, string userCode, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync(cancellationToken);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var result = await UploadAsync(conn, tx, lines, settings, userCode, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        logger.LogInformation("Bordx upload by {User}: batch {Batch}, {NewMembers} new, {Supplementaries} supplementary, {Cancelled} cancelled, {Reinstated} reinstated",
            userCode, result.BatchNo, result.NewMembers, result.Supplementaries, result.Cancelled, result.Reinstated);
        return result;
    }

    /// <summary>The upload inside a caller-owned transaction (used by the tests, which roll back).</summary>
    internal static async Task<UploadResult> UploadAsync(SqlConnection conn, SqlTransaction tx, IReadOnlyList<BordxLine> lines, BordxSettings settings,
        string userCode, CancellationToken cancellationToken)
    {
        var format = await BordxFormat.DetectAsync(conn, tx, lines[0], settings.PayorCode);
        var stagingKey = BordxValues.StagingKeyPrefix + Guid.NewGuid().ToString("N");
        await conn.ProcExecAsync("genisis.Bordx_ClearStaging", new { runKey = stagingKey }, tx);
        var today = await conn.ProcSingleAsync<DateTime>("genisis.Server_Today", tx: tx);

        var context = new BordxUploadContext(conn, tx, lines, settings, format, userCode, stagingKey, today, cancellationToken);
        var result = await new BordxUploader(context).RunAsync();

        // SaveUploadDate swallows its own errors with a bare ROLLBACK; inside this transaction that surfaces as a transaction-count
        // error, so a failed copy can never look like a success.
        await conn.ProcExecAsync("genisis.Bordx_CommitStaging", new { runKey = stagingKey }, tx);
        return result;
    }

    public Task<AsciiFile?> ExportAsciiAsync(AsciiRequest request, CancellationToken cancellationToken) => asciiExporter.ExportAsync(request, cancellationToken);
}
