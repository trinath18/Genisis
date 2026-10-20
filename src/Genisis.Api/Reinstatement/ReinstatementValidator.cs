using Genisis.Api.Membership;

namespace Genisis.Api.Reinstatement;

/// <summary>Request and state checks of cmdReintstate_Click, in the desktop's order. Pure functions.</summary>
public static class ReinstatementValidator
{
    /// <summary>Only a cancelled membership can be reinstated; the desktop says "active" for every other status.</summary>
    public static string? Blocked(ReinstatementMember member) =>
        member.Status?.Trim().ToUpperInvariant() == MemberStatus.Cancelled ? null : ReinstatementMessages.StillActive;

    public static string? ValidateRequest(ReinstatementRequest request)
    {
        if (request.EndtBordxDate is null) return ReinstatementMessages.EnterEndtBordxDate;
        var batch = request.BatchNo?.Trim() ?? "";
        if (batch == "") return ReinstatementMessages.EnterBatchNo;
        if (batch.Length > ReinstatementLimits.BatchNoMaxLength || !batch.All(char.IsAsciiDigit)) return ReinstatementMessages.InvalidBatchNo;
        if (request.ReinstateDate is null) return ReinstatementMessages.EnterReinstateDate;
        if (request.LapsedDate is null) return ReinstatementMessages.EnterLapsedDate;
        if (request.ExpiryDate is null) return ReinstatementMessages.EnterExpiryDate;
        return null;
    }
}
