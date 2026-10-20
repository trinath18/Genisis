using System.Data;
using Dapper;
using Genisis.Api.Common.Errors;
using Genisis.Api.Data;
using Genisis.Api.Membership;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Deletion;

public interface IDeletionService
{
    Task<DeletionLookups> GetLookupsAsync(CancellationToken cancellationToken);
    Task<DeletionListResult> ListAsync(DeletionCriteria criteria, CancellationToken cancellationToken);
    Task<DeletionResult> DeleteAsync(DeletionCriteria criteria, CancellationToken cancellationToken);
}

/// <summary>Membership &gt; Deletion (legacy frmDeleteMembers: DELMBMListing, DeletePrincipals).</summary>
public sealed class DeletionService(DbConnections db) : IDeletionService
{
    public async Task<DeletionLookups> GetLookupsAsync(CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync(cancellationToken);
        using var grid = await conn.ProcMultipleAsync("genisis.Deletion_Lookups");
        return new DeletionLookups((await grid.ReadAsync<DeletionOption>()).ToList(), (await grid.ReadAsync<DeletionOption>()).ToList());
    }

    public async Task<DeletionListResult> ListAsync(DeletionCriteria criteria, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync(cancellationToken);
        return await ListAsync(conn, null, criteria);
    }

    internal static async Task<DeletionListResult> ListAsync(SqlConnection conn, SqlTransaction? tx, DeletionCriteria criteria)
    {
        var p = Parameters(criteria);
        p["limit"] = DeletionLimits.ListLimit + 1;
        using var grid = await conn.ProcMultipleAsync("genisis.Deletion_List", new DynamicParameters(p), tx);
        var principals = (await grid.ReadAsync<DeletionPrincipal>()).ToList();
        var covered = (await grid.ReadAsync<DeletionCovered>()).ToList();
        var truncated = principals.Count > DeletionLimits.ListLimit;
        if (truncated) principals.RemoveAt(principals.Count - 1);
        return Listing(principals, covered, truncated);
    }

    /// <summary>
    /// Each principal (cover 00) followed by its listed covered persons. The desktop counts principals of member type P only, and
    /// covered persons whose member type is not P.
    /// </summary>
    internal static DeletionListResult Listing(List<DeletionPrincipal> principals, List<DeletionCovered> covered, bool truncated)
    {
        var byMember = covered.ToLookup(c => c.MembershipNo, StringComparer.OrdinalIgnoreCase);
        var rows = new List<DeletionRow>();
        int totalPrincipal = 0, totalSupplementary = 0;
        foreach (var p in principals)
        {
            rows.Add(new DeletionRow { MembershipNo = p.MembershipNo, CoverId = CoverIds.Principal, Name = p.Name, BordxDate = p.BordxDate, BatchNo = p.BatchNo });
            if (p.MemberType?.Trim() == MemberTypes.PrincipalCombined) totalPrincipal++;
            if (!InsuredTypes.IsFamily(p.InsuredType)) continue;
            foreach (var c in byMember[p.MembershipNo])
            {
                rows.Add(new DeletionRow { MembershipNo = c.MembershipNo, CoverId = c.CoverId, Name = c.Name, BordxDate = c.BordxDate, BatchNo = c.BatchNo });
                if (c.MemberType?.Trim() != MemberTypes.PrincipalCombined) totalSupplementary++;
            }
        }
        var message = principals.Count == 0 ? DeletionMessages.NoRecord : DeletionMessages.Listed;
        return new DeletionListResult(message, rows, totalPrincipal, totalSupplementary, principals.Count, truncated);
    }

    public async Task<DeletionResult> DeleteAsync(DeletionCriteria criteria, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync(cancellationToken);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var result = await DeleteAsync(conn, tx, criteria);
        if (result.Memberships == 0) await tx.RollbackAsync(cancellationToken);
        else await tx.CommitAsync(cancellationToken);
        return result;
    }

    /// <summary>Archive and delete in the caller's transaction (the tests roll it back): all selected memberships, or none.</summary>
    internal static async Task<DeletionResult> DeleteAsync(SqlConnection conn, SqlTransaction tx, DeletionCriteria criteria)
    {
        var p = Parameters(criteria);
        p["today"] = await conn.ProcSingleAsync<DateTime>("genisis.Server_Today", tx: tx);
        DeletionCounts counts;
        try
        {
            counts = await conn.ProcSingleAsync<DeletionCounts>("genisis.Deletion_Delete", new DynamicParameters(p), tx);
        }
        catch (SqlException e) when (SqlErrorNumbers.IsDuplicateKey(e.Number))
        {
            throw new DeletionException(DeletionMessages.AlreadyArchived);
        }
        catch (SqlException e) when (e.Number == SqlErrorNumbers.IndexDisabled)
        {
            throw new DeletionException(DeletionMessages.ArchiveUnavailable);
        }
        var message = counts.Memberships == 0 ? DeletionMessages.NoRecord : DeletionMessages.Deleted;
        return new DeletionResult(message, counts.Principals, counts.Supplementaries, counts.Memberships);
    }

    private static Dictionary<string, object?> Parameters(DeletionCriteria c) => new()
    {
        ["hlt"] = DeletionValidator.Code(c.HealthCode),
        ["pay"] = DeletionValidator.Code(c.PayorCode),
        ["option"] = DeletionValidator.Code(c.Option),
        ["bordxFrom"] = c.BordxDateFrom?.Date,
        ["bordxTo"] = c.BordxDateTo?.Date,
        ["receiptDate"] = c.ReceiptDate?.Date,
        ["batch"] = c.BatchNo,
        ["numberFrom"] = c.NumberFrom?.Trim(),
        ["numberTo"] = c.NumberTo?.Trim(),
    };
}
