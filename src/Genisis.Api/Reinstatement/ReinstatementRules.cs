using Genisis.Api.Membership;
using Genisis.Api.Registration;

namespace Genisis.Api.Reinstatement;

/// <summary>Fee formulas of the desktop MemberCallFees on frmMBMReintstatement. Pure functions.</summary>
public static class ReinstatementRules
{
    /// <summary>GetMCO amount for the lapse date, pro-rated from the lapse date to the expiry date unless PLNProRate = N (InsertMCOFees).</summary>
    public static decimal McoFee(decimal annualMco, bool proRate, DateTime lapsed, DateTime expiry)
    {
        var fee = LegacyRules.Money(annualMco);
        return proRate ? FeeRules.ProRateMoney(lapsed, expiry, fee) : fee;
    }

    /// <summary>
    /// The MCO fee is rounded to a whole amount unless PLNRoundUpStatus = N. The desktop rounds even when it kept the previous fee
    /// (a person without an adult/child code).
    /// </summary>
    public static decimal RoundMcoFee(decimal fee, bool roundUp) => roundUp ? FeeRules.VbRound(fee) : fee;

    /// <summary>GetPremium amount for the lapse date, always pro-rated from the lapse date to the expiry date (InsertPremium).</summary>
    public static decimal Premium(decimal annualPremium, DateTime lapsed, DateTime expiry) =>
        FeeRules.ProRateMoney(lapsed, expiry, LegacyRules.Money(annualPremium));

    /// <summary>GetMCO returns the adult amount for A, the child amount for C, and nothing for any other value.</summary>
    public static decimal McoRate(string? adultChild, decimal? adultAmount, decimal? childAmount) => adultChild?.Trim().ToUpperInvariant() switch
    {
        AdultChild.Adult => adultAmount ?? 0,
        AdultChild.Child => childAmount ?? 0,
        _ => 0,
    };

    /// <summary>GetPremium: the female rate applies when the plan charges by gender (PLNPremGenderStatus = Y) and the person is female.</summary>
    public static decimal PremiumRate(bool chargesByGender, string? sex, decimal? amount, decimal? femaleAmount) =>
        (chargesByGender && sex?.Trim().ToUpperInvariant() == Sex.Female ? femaleAmount : amount) ?? 0;

    /// <summary>The desktop passes only the first characters of these codes to GetMCO / GetPremium.</summary>
    public static string Prefix(string? code, int length)
    {
        var value = code?.Trim() ?? "";
        return value.Length > length ? value[..length] : value;
    }
}
