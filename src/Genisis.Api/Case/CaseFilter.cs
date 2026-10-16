using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Case;

/// <summary>Optional filters for Case Tracking, mirroring the FrmCaseTracking filter panel.</summary>
public class CaseTrackingFilter
{
    public string? Payor { get; set; }
    public string? CaseFrom { get; set; }
    public string? CaseTo { get; set; }
    public string? MbmFrom { get; set; }
    public string? MbmTo { get; set; }
    public DateTime? RegFrom { get; set; }
    public DateTime? RegTo { get; set; }
    public string? Batch { get; set; }
    public string? Patient { get; set; }
    public string? Grp { get; set; }

    /// <summary>O = open, D = discharged, OC = open or closed.</summary>
    public string? Status { get; set; }

    /// <summary>A = approved set, R = repudiated set, or an exact 2-letter CASClaimability code.</summary>
    public string? Claimability { get; set; }

    /// <summary>Y = pending only, N = not pending (blank included).</summary>
    public string? Pending { get; set; }

    /// <summary>P / C / B - claim pending flag (summary only).</summary>
    public string? ClaimPending { get; set; }

    public string? RegBy { get; set; }
    public string? PayType { get; set; }
    public string? UserToken { get; set; }
    public string? Sex { get; set; }

    /// <summary>principal or supplementary.</summary>
    public string? MemberType { get; set; }

    public string? HltCode { get; set; }
    public string? InsCode { get; set; }

    /// <summary>P / R / T, or N which also matches blank and NULL.</summary>
    public string? RenewFlag { get; set; }

    public string? Takeover { get; set; }
    public string? AgentCode { get; set; }
    public string? PolicyNo { get; set; }
    public string? Doctor { get; set; }
    public string? RepFrom { get; set; }
    public string? RepTo { get; set; }
    public string? ClaimNatureFrom { get; set; }
    public string? ClaimNatureTo { get; set; }
    public string? HosName { get; set; }
    public string? StayType { get; set; }
}
