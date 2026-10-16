namespace Genisis.Api.Membership;

/// <summary>Code values stored in the membership tables (MBM, MBMCoveredPersons, MBMCrossReference, PLN, PRM, MCO, AnnualLimit).</summary>
public static class MemberStatus
{
    public const string Active = "A";
    public const string Cancelled = "C";
    public const string Suspended = "S";
    public const string Deleted = "D";
}

/// <summary>MBM.MBMDataStatus.</summary>
public static class DataStatus
{
    public const string Active = "A";
    public const string Deleted = "D";
}

/// <summary>INS.INSCode.</summary>
public static class InsuredTypes
{
    public const string Individual = "I";
    public const string Family = "F";
    public const string GroupIndividual = "G";
    public const string GroupFamily = "H";

    /// <summary>Insured types that can have supplementary (covered) persons.</summary>
    public static bool IsFamily(string? code) => code?.Trim().ToUpperInvariant() is Family or GroupFamily;
}

/// <summary>HLT.HLTCode.</summary>
public static class HealthTypes
{
    public const string NonSihat = "N";
    public const string Sihat = "S";
}

/// <summary>MBM.MBMMemberType / MBMCoveredPersons.MBMCMemberType.</summary>
public static class MemberTypes
{
    /// <summary>Principal sharing one combined annual limit with the family.</summary>
    public const string PrincipalCombined = "P";
    /// <summary>Principal with a separate annual limit per person.</summary>
    public const string PrincipalSeparate = "Q";
    public const string SupplementaryCombined = "C";
    public const string SupplementarySeparate = "S";
}

/// <summary>Cover IDs: the principal is 00, supplementaries are numbered from 01.</summary>
public static class CoverIds
{
    public const string Principal = "00";
    public const string FirstSupplementary = "01";

    public static string Format(int coverNumber) => coverNumber.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// SuppPrmStatus / SuppMCOStatus / SuppLimitStatus: which family members are charged a premium, MCO fee or annual limit.
/// </summary>
public static class SupplementaryCharge
{
    /// <summary>Only the principal (cover 00) is charged.</summary>
    public const string PrincipalOnly = "A";
    /// <summary>Only the first supplementary (cover 01) is charged.</summary>
    public const string FirstSupplementaryOnly = "B";
    /// <summary>Every member is charged.</summary>
    public const string Everyone = "C";
    /// <summary>Members from MCO.CovIDChrg onwards are charged.</summary>
    public const string FromCoverId = "D";
}

/// <summary>Age band 01 is the child band; every other band is adult (MBMAdultChild).</summary>
public static class AdultChild
{
    public const string Adult = "A";
    public const string Child = "C";
    public const string ChildAgeBand = "01";

    public static string ForAgeBand(string? ageCode) => ageCode == ChildAgeBand ? Child : Adult;
}

/// <summary>Y / N indicator columns (PRMInd, MCOInd, AnnLmtIND, PLNProRate, PLNRoundUpStatus, ...).</summary>
public static class YesNo
{
    public const string Yes = "Y";
    public const string No = "N";

    public static bool IsYes(string? value) => value?.Trim().ToUpperInvariant() == Yes;
    public static bool IsNotNo(string? value) => value?.Trim().ToUpperInvariant() != No;
}

/// <summary>PLN.PLNGuaranteeStatus / MBMGuaRenewal: guarantee renewal codes of lifetime-limit plans.</summary>
public static class GuaranteeRenewal
{
    public const string Guaranteed = "1";
    /// <summary>Annual limit renews; the lifetime limit is tracked in MBMLifeTime.</summary>
    public const string LifetimeTracked = "3";
}

/// <summary>Sex codes (MBMSex).</summary>
public static class Sex
{
    public const string Male = "M";
    public const string Female = "F";
}
