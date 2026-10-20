using Genisis.Api.Data;

namespace Genisis.Api.Adjustment;

public sealed record TempMembershipCheck(string MembershipNo, string? PayorCode, bool Temporary);

public interface ITempAdjustmentService
{
    Task<TempMembershipCheck?> CheckAsync(string membershipNo, CancellationToken cancellationToken);
}

/// <summary>
/// Membership &gt; Temp MBM Adj (legacy FrmTempMBMAdj). The form is an older copy of Adjustment (frmNewMBMAdjustment) that FindMBM
/// limits to memberships of temporary payors. The web version opens the Adjustment editor for those memberships only.
/// </summary>
public sealed class TempAdjustmentService(DbConnections db) : ITempAdjustmentService
{
    public const string NotTemporary = "This is not a Temporary Membership! No further changes are allowed!";

    public async Task<TempMembershipCheck?> CheckAsync(string membershipNo, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync(cancellationToken);
        return await conn.ProcFirstOrDefaultAsync<TempMembershipCheck>("genisis.TempAdjustment_Payor", new { number = membershipNo.Trim() });
    }
}
