namespace Genisis.Api.Adjustment;

/// <summary>A membership as loaded for adjustment: principal, covered persons, clinical plan and annual limit.</summary>
public sealed record LoadedMembership(AdjustmentMember Member, List<AdjustmentCovered> Covered, ClinicalPlan? Clinic, PlanLimit? Limit);

// Rows read from the genisis.Adjustment_* / Registration_* procedures (column names as returned).
internal sealed record PlanRates(string? RoundUpStatus, string? ProRate, string? PremGenderStatus, string? PremiumInd);
internal sealed record PlanSettings(string? LifetimeStatus, decimal? AgeLimit, string? RoundUpStatus);
internal sealed record PlanDiscount(decimal? PolicyDisc, decimal? RenewalDisc);
internal sealed record McoRates(string? SuppMcoStatus, string? CovIDChrg, decimal? MCOAmountAdult, decimal? MCOAmountChild,
    decimal? MCOSuppAdultAmt, decimal? MCOSuppChildAmt);
internal sealed record PremiumRates(string? SuppPrmStatus, decimal? PRMAmount, decimal? PRMAmountFemale, decimal? SuppPrmAmt);
internal sealed record AnnualLimitRates(string? SuppLimitStatus, decimal? AnnLimit, decimal? SuppLimit, decimal? LifeTimeLimit, decimal? SuppLifeTimeLimit);
internal sealed record SupplementaryLimit(string? SuppLimitStatus, decimal? SuppLimitAmount);
internal sealed record SupplementaryPremium(string? SuppPrmStatus, decimal? SuppPrmAmt);
internal sealed record BenefitLimit(string? SuppBnfStatus, decimal? KidDialysisLmt, decimal? CancerLmt, decimal? HomeNursLmt);
internal sealed record PreviousLifetime(string MBMNumber, string? MBMTopupNumber, decimal? MBMAvailableLimit);
internal sealed record AmountRow(decimal? Value);
internal sealed record OpenCases(bool HisCase, bool ClinicalCase);
internal sealed record ClinicalPlanSettings(string? DcStatus, string? PayorCode, string? SuppDentalStatus, string? SuppSpecialistStatus,
    string? SuppMepStatus, string? RoundUpStatus, string? ProRate, string? McoProcFeeStatus);
internal sealed record ClinicalLimitRates(string? SuppLimitStatus, decimal? SuppLimit, decimal? DentalLimit, decimal? SpecialistLimit, decimal? MepLimit);
internal sealed record ClinicalMcoRates(string? SuppMcoStatus, decimal? SuppAdult, decimal? SuppChild);
