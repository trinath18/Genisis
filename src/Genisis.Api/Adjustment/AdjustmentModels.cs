namespace Genisis.Api.Adjustment;

/// <summary>Principal as shown and edited on frmNewMBMAdjustment (including the frmNewMBMOthers "Others Details" fields).
/// Property names match the genisis.Adjustment_Load columns.</summary>
public class AdjustmentMember
{
    public string? MembershipNo { get; set; }
    public string? HealthCode { get; set; }
    public string? PayorCode { get; set; }
    public string? InsuredType { get; set; }
    public string? PlanCode { get; set; }
    public string? PolicyNo { get; set; }
    public string? Name { get; set; }
    public string? IcBcPp { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Sex { get; set; }
    public string? RaceCode { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? Address3 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostCode { get; set; }
    public DateTime? PayorEffectiveDate { get; set; }
    public DateTime? PayorExpiryDate { get; set; }
    public DateTime? BordxDate { get; set; }
    public int? BatchNo { get; set; }
    public string? AgeCode { get; set; }
    public string? AdultChild { get; set; }
    public string? TakeOver { get; set; }
    public string? Renewal { get; set; }
    public string? DataStatus { get; set; }
    public string? Status { get; set; }
    public string? ExpiredStatus { get; set; }
    public decimal? AvailableLimit { get; set; }
    public DateTime? ValueDate { get; set; }
    public string? PolicyYear { get; set; }
    public DateTime? FinalExpiryDate { get; set; }
    public DateTime? NextDueDate { get; set; }
    public string? TopupInd { get; set; }
    public string? TopupNumber { get; set; }
    public string? Salutation { get; set; }
    public string? Email { get; set; }
    public string? LgnCode { get; set; }
    public string? NationalityCode { get; set; }
    public string? TelHome { get; set; }
    public string? TelMobile { get; set; }
    public string? TelOffice { get; set; }
    public string? MgmtNotes { get; set; }
    public string? OtherIc { get; set; }
    public string? SendingMode { get; set; }
    public DateTime? CardSentDate { get; set; }
    public DateTime? CancelDate { get; set; }
    public string? Department { get; set; }
    public string? GroupCompany { get; set; }
    public string? PolicySubNo { get; set; }
    public bool LabelStatus { get; set; }
    public string? EmployeeNo { get; set; }
    public string? AgentCode { get; set; }
    public string? InstallmentMode { get; set; }
    public decimal? McoFee { get; set; }
    public decimal? BasicMco { get; set; }
    public string? Branch { get; set; }
    public decimal? BasicPremium { get; set; }
    public decimal? Premium { get; set; }
    public DateTime? DateJoined { get; set; }
    public DateTime? ClaimNonPayFrom { get; set; }
    public DateTime? ClaimNonPayTo { get; set; }
    public string? NewPlanCode { get; set; }
    public DateTime? NewPolicyEffectiveDate { get; set; }
    public decimal? PolicyDisc { get; set; }
    public decimal? RenewalDisc { get; set; }
    public string? PolicyCondition { get; set; }
    public decimal? UnderwritingExcess { get; set; }
    public string? EndtRefNo { get; set; }
    public string? CostCenter { get; set; }
    public string? Position { get; set; }
    public string? GroupCategory { get; set; }
    public string? Division { get; set; }
    public string? SubDivision { get; set; }
    public string? Subsidiary { get; set; }
    public string? GuaranteeRenewal { get; set; }
    public string? InvoiceNo { get; set; }
    public DateTime? InvoiceDate { get; set; }
    public string? CreditNote { get; set; }
    public DateTime? CreditNoteDate { get; set; }
    public string? Allergic { get; set; }
    public string? Remarks { get; set; }
    public string? Weight { get; set; }
    public string? Height { get; set; }
    public string? WorkNature { get; set; }
    public string? Blood { get; set; }
    public string? Smoking { get; set; }
    public string? Alcohol { get; set; }
    public string? MaritalStatus { get; set; }
    public string? PreviousPlanCode { get; set; }
    public decimal? RbAmount { get; set; }
    public string? PreviousInsurer { get; set; }
    public string? Occupation { get; set; }
    public string? CoPayRb { get; set; }
    public decimal? GeneralWaitingDays { get; set; }
    public DateTime? GeneralWaitingEnd { get; set; }
    public decimal? PreExistingWaitingDays { get; set; }
    public DateTime? PreExistingWaitingEnd { get; set; }
    public decimal? SpecificIllnessWaitingDays { get; set; }
    public DateTime? SpecificIllnessWaitingEnd { get; set; }
    public string? StaffDiscount { get; set; }
    public string? Btbg { get; set; }
    public decimal? PayorPremiumGross { get; set; }
    public decimal? PayorPremiumNet { get; set; }
    public decimal? PayorMcoGross { get; set; }
    public decimal? PayorMcoNet { get; set; }
    public decimal? FamilyDiscountAmount { get; set; }
    public decimal? RenewalDiscountAmount { get; set; }
    public decimal? LoadingPremium { get; set; }
    public string? Exclusion { get; set; }
    public string? BankAccountNo { get; set; }
    public DateTime? TopupJoinDate { get; set; }
    public string? PayorGroupCode { get; set; }
}

/// <summary>One covered (supplementary) person. On save, <see cref="Action"/> says what to do with it:
/// "add" (Add Supplementary), "remove" (Delete Supplementary), "cancel" (Cancel Supplementary); anything else keeps / updates it.</summary>
public class AdjustmentCovered
{
    public string? Action { get; set; }
    public string? CoverId { get; set; }
    public string? Salutation { get; set; }
    public string? Name { get; set; }
    public string? IcBcPp { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Sex { get; set; }
    public string? AdultChild { get; set; }
    public string? Relationship { get; set; }
    public string? PlanCode { get; set; }
    public string? Status { get; set; }
    public string? Exclusion { get; set; }
    public string? Allergic { get; set; }
    public decimal? AnnualLimit { get; set; }
    public decimal? AvailableLimit { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? CancelDate { get; set; }
    public decimal? UnderwritingExcess { get; set; }
    public decimal? Mco { get; set; }
    public decimal? BasicMco { get; set; }
    public decimal? Premium { get; set; }
    public decimal? BasicPremium { get; set; }
    public string? InvoiceNo { get; set; }
    public DateTime? InvoiceDate { get; set; }
    public string? CreditNote { get; set; }
    public DateTime? CreditNoteDate { get; set; }
    public string? PreviousPlanCode { get; set; }
    public decimal? RbAmount { get; set; }
    public string? PayorRemarks { get; set; }
    public string? PreviousInsurer { get; set; }
    public decimal? GeneralWaitingDays { get; set; }
    public DateTime? GeneralWaitingEnd { get; set; }
    public decimal? PreExistingWaitingDays { get; set; }
    public DateTime? PreExistingWaitingEnd { get; set; }
    public decimal? SpecificIllnessWaitingDays { get; set; }
    public DateTime? SpecificIllnessWaitingEnd { get; set; }
    public string? StaffDiscount { get; set; }
    public DateTime? ClaimNonPayFrom { get; set; }
    public DateTime? ClaimNonPayTo { get; set; }
    public decimal? PayorPremiumGross { get; set; }
    public decimal? PayorPremiumNet { get; set; }
    public decimal? PayorMcoGross { get; set; }
    public decimal? PayorMcoNet { get; set; }
    public decimal? RenewalDisc { get; set; }
    public decimal? PolicyDisc { get; set; }
    public decimal? FamilyDiscountAmount { get; set; }
    public decimal? RenewalDiscountAmount { get; set; }
    public decimal? LoadingPremium { get; set; }
    public string? PayorMemberNo { get; set; }
    public string? PayorSeqNo { get; set; }
    public string? ClientNo { get; set; }
    public string? ClientId { get; set; }
    public string? OtherIc { get; set; }
}

public record ClinicalPlan(string? PlanCode, string? InsuredType, DateTime? EffectiveDate, DateTime? ExpiryDate);

public record PlanLimit(decimal? AnnualLimit, decimal? DisabilityLimit, string? SuppLimitStatus, string? SuppLimitDescription);

public class AdjustmentRequest
{
    public string? EndorsementType { get; set; }
    public DateTime? EndtBordxDate { get; set; }
    public DateTime? EndtEffectiveDate { get; set; }
    public AdjustmentMember Member { get; set; } = new();
    public List<AdjustmentCovered> Covered { get; set; } = [];
    /// <summary>Amendment / Plan Change: store <see cref="AnnualLimit"/> as the available limit (desktop "Do you want to change Annual Limit?").</summary>
    public bool ApplyAnnualLimit { get; set; }
    public decimal? AnnualLimit { get; set; }
}

public record AdjustmentResult(string Message, List<string> Warnings);

public class RecalculateRequest
{
    public string? PlanCode { get; set; }
    public DateTime? PayorEffectiveDate { get; set; }
    public DateTime? PayorExpiryDate { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Sex { get; set; }
}

public record Recalculation(string AgeCode, string AdultChild, decimal BasicPremium, decimal Premium, decimal Mco, decimal? AnnualLimit,
    List<string> Warnings);

public class SupplementaryExpiryRequest
{
    public DateTime? DateOfBirth { get; set; }
    public string? PlanCode { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

public record SupplementaryExpiry(DateTime? ExpiryDate, bool OverAge, int AgeLimit);

public record FeeRow(string Name, string? Relationship, string CoverId, string? Status, string? PlanCode, DateTime? EffectiveDate,
    DateTime? ExpiryDate, DateTime? CancelDate, decimal? AnnualLimit, decimal? AvailableLimit, decimal? DisabilityLimit,
    decimal? BasicPremium, decimal? Premium, decimal? BasicMco, decimal? Mco, string? InvoiceNo, DateTime? InvoiceDate,
    string? CreditNote, DateTime? CreditNoteDate, decimal? McoEffective, decimal? McoRefund, decimal? PremiumEffective,
    decimal? PremiumReduction, bool Reinstatement);

/// <summary>An adjustment rule stopped the save (the transaction is rolled back).</summary>
public class AdjustmentException(string message) : Genisis.Api.Common.Errors.BusinessRuleException(message);
