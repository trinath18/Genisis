namespace Genisis.Api.Registration;

/// <summary>New membership registration (legacy Membership > Registration screen).</summary>
public class RegistrationRequest
{
    public string? HealthCode { get; set; }
    public string? PayorCode { get; set; }
    public DateTime? BordxDate { get; set; }
    public int? BatchNo { get; set; }

    public string? Renewal { get; set; } = "N";
    public string? PreviousMembershipNo { get; set; }
    public string? PreviousPolicyNo { get; set; }

    public string? Salutation { get; set; }
    public string? Name { get; set; }
    public string? IcBcPp { get; set; }
    public string? OtherIc { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Sex { get; set; }
    public string? RaceCode { get; set; }
    public string? NationalityCode { get; set; }
    public string? MaritalStatus { get; set; }

    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? Address3 { get; set; }
    public string? City { get; set; }
    public string? PostCode { get; set; }
    public string? State { get; set; }
    public string? TelHome { get; set; }
    public string? TelMobile { get; set; }
    public string? TelOffice { get; set; }
    public string? Email { get; set; }

    public string? InsuredType { get; set; }
    public string? PlanCode { get; set; }
    public string? PolicyNo { get; set; }
    public DateTime? PayorEffectiveDate { get; set; }
    public DateTime? PayorExpiryDate { get; set; }
    public DateTime? DateJoined { get; set; }
    public string? TakeOver { get; set; } = "N";
    public string? GuaranteeRenewal { get; set; }
    public string? InstallmentMode { get; set; }

    public string? GroupCompany { get; set; }
    public string? EmployeeNo { get; set; }
    public string? Department { get; set; }
    public string? Branch { get; set; }
    public string? AgentCode { get; set; }
    public string? Exclusion { get; set; }
    public string? Allergic { get; set; }
    public string? Remarks { get; set; }

    public List<CoveredPersonRequest> CoveredPersons { get; set; } = [];
}

public class CoveredPersonRequest
{
    public string? Relationship { get; set; }
    public string? Salutation { get; set; }
    public string? Name { get; set; }
    public string? IcBcPp { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Sex { get; set; }
    public string? Occupation { get; set; }
    public string? Exclusion { get; set; }
    public string? Allergic { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

public record CoveredPersonQuote(
    string CoverId, string Name, string AgeCode, string AdultChild,
    decimal BasicPremium, decimal Premium, decimal BasicMco, decimal Mco, decimal AnnualLimit);

public record RegistrationQuote(
    string AgeCode, string AdultChild, string MemberType, bool LifetimePlan,
    decimal BasicPremium, decimal Premium, decimal BasicMco, decimal Mco, decimal AnnualLimit,
    List<CoveredPersonQuote> CoveredPersons, List<string> Warnings);

public record RegistrationResult(string MembershipNo, string SecurityCode, RegistrationQuote Quote);

public class RegistrationException(string message) : Exception(message);
