namespace Genisis.Api.Plans;

/// <summary>One plan line on the Plan tab: a typed code (health type S) and a description.</summary>
public class PlanLine
{
    public string? Code { get; set; }
    public string? Description { get; set; }
}

/// <summary>Maintenance > Plan > Plan tab "Add" (legacy frmMaintenencePlan.SavePLN). Indicators are Y or N.</summary>
public class PlanCreateRequest
{
    public string? HealthCode { get; set; }
    public string? PayorCode { get; set; }
    /// <summary>Health type N: how many plans to create (1-6); codes come from the PLNSeq counter.</summary>
    public int? NumberOfPlans { get; set; }
    public List<PlanLine> Plans { get; set; } = [];
    public string? GroupCompany { get; set; }
    public string? AnnualLimitInd { get; set; }
    public string? LifetimeStatus { get; set; }
    public string? PremiumInd { get; set; }
    public string? McoInd { get; set; }
    public string? TopUpStatus { get; set; }
    public string? SpecialGracePeriod { get; set; }
    public int? SpecialGracePeriodDays { get; set; }
    public string? CoPayment { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public string? ProductCategory { get; set; }
    public string? Meal { get; set; }
    public string? Nursing { get; set; }
    public string? Tax { get; set; }
    public string? Mri { get; set; }
    public string? Sof { get; set; }
    public string? SmPlan { get; set; }
    public string? ManagementFee { get; set; }
    public string? DisIndicator { get; set; }
    public int? StartAge { get; set; }
    public int? EndAge { get; set; }
    public string? PolicyWording { get; set; }
    public string? ClientPlan { get; set; }
    public string? ClientPolicyNo { get; set; }
    public int? CoPayPercent { get; set; }
}

public record CreatedPlan(int Index, string Code, string? Description);

public record PlanRow(
    int Index, string HealthCode, string Code, string? NewCode, string? Description, string? PayorCode, string? GroupCompany,
    string? TopUpStatus, string? AnnualLimitInd, string? PremiumInd, string? McoInd, string? CoPayment, DateTime? EffectiveDate,
    string? SpecialGracePeriod, decimal? SpecialGracePeriodDays, string? SmPlan, string? LifetimeStatus, string? Sof,
    string? ProductCategory, string? Meal, string? Nursing, string? Tax, string? Mri, string? ManagementFee, string? DisIndicator,
    int? StartAge, int? EndAge, string? PolicyWording, string? ClientPlan, string? ClientPolicyNo, int? CoPayPercent);

public class PlanSearch
{
    public string? HealthCode { get; set; }
    public string? PayorCode { get; set; }
    public string? GroupCompany { get; set; }
    public string? PlanCode { get; set; }
    public string? ProductCategory { get; set; }
    public string? TopUpStatus { get; set; }
    public string? CoPayment { get; set; }
    public string? Sof { get; set; }
    public string? SpecialGracePeriod { get; set; }
    public string? Meal { get; set; }
    public string? Nursing { get; set; }
    public string? Tax { get; set; }
    public string? Mri { get; set; }
    public string? DisIndicator { get; set; }
}

public class PlanException(string message) : Exception(message);
