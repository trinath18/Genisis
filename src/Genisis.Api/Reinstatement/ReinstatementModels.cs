using System.Text.Json.Serialization;

namespace Genisis.Api.Reinstatement;

/// <summary>The membership fields frmMBMReintstatement shows and uses (genisis.Reinstatement_Load).</summary>
public sealed class ReinstatementMember
{
    public string MembershipNo { get; set; } = "";
    public string? Name { get; set; }
    public string? PolicyNo { get; set; }
    /// <summary>Written to MBMAmendHistory only; not sent to the browser.</summary>
    [JsonIgnore] public string? IcBcPp { get; set; }
    public string? Status { get; set; }
    public DateTime? PayorEffectiveDate { get; set; }
    public DateTime? PayorExpiryDate { get; set; }
    public DateTime? FinalExpiryDate { get; set; }
    public string? InsuredType { get; set; }
    public string? PlanCode { get; set; }
    public string? AdultChild { get; set; }
    public string? PayorCode { get; set; }
    public string? HealthCode { get; set; }
    public string? AgeCode { get; set; }
    public string? Sex { get; set; }
    public decimal? McoFee { get; set; }
    public decimal? Premium { get; set; }
    /// <summary>MBMTwo.MBMCancelDate: the date the membership lapsed.</summary>
    public DateTime? LapsedDate { get; set; }
}

/// <summary>A covered person as read by genisis.Reinstatement_Covered.</summary>
public sealed class ReinstatementCovered
{
    public string CoverId { get; set; } = "";
    public string? AdultChild { get; set; }
    public string? AgeCode { get; set; }
    public string? Sex { get; set; }
    public decimal? McoFee { get; set; }
    public decimal? Premium { get; set; }
}

public sealed record ReinstatementView(ReinstatementMember Member, int CoveredCount);

/// <summary>The editable fields of the desktop form; the lapse and expiry dates default to the stored values on screen.</summary>
public sealed class ReinstatementRequest
{
    public DateTime? EndtBordxDate { get; set; }
    public string? BatchNo { get; set; }
    public DateTime? ReinstateDate { get; set; }
    public DateTime? LapsedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

/// <summary>The MCO fee and premium charged for one reinstated cover (00 = principal).</summary>
public sealed record ReinstatedCover(string CoverId, decimal McoFee, decimal Premium);

public sealed record ReinstatementResult(string Message, List<ReinstatedCover> Covers, List<string> Warnings);

/// <summary>The rate inputs of one person for the desktop MemberCallFees.</summary>
public sealed record ReinstatementFeeInput(string? AdultChild, string? AgeCode, string? Sex);
