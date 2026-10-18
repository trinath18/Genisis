using Genisis.Api.Registration;

namespace Genisis.Api.Membership;

/// <summary>Fee, refund and expiry formulas shared by Adjustment and Upload Bordx (legacy Medix.bas). Pure functions.</summary>
public static class FeeRules
{
    /// <summary>VB6 Round / CInt: banker's rounding to a whole number.</summary>
    public static decimal VbRound(decimal value) => Math.Round(value, 0, MidpointRounding.ToEven);

    /// <summary>Annual amount pro-rated over the inclusive cover period, rounded to cents.</summary>
    public static decimal ProRateMoney(DateTime effective, DateTime expiry, decimal annualAmount) =>
        LegacyRules.Money(LegacyRules.ProRate(effective, expiry, annualAmount));

    /// <summary>Legacy MCORefund: (days left incl. cancel day) / (policy days) x fee, never negative.</summary>
    public static decimal McoRefund(DateTime expiry, DateTime cancel, DateTime effective, decimal fee)
    {
        var refund = ((expiry.Date - cancel.Date).Days + 1) / (decimal)((expiry.Date - effective.Date).Days + 1) * fee;
        return refund < 0 ? 0 : refund;
    }

    /// <summary>Legacy MCOPremiumReduction: only when cancelled before expiry; the desktop omits the +1 on the days left.</summary>
    public static decimal PremiumReduction(DateTime expiry, DateTime cancel, DateTime effective, decimal premium) =>
        cancel.Date < expiry.Date ? (expiry.Date - cancel.Date).Days / (decimal)((expiry.Date - effective.Date).Days + 1) * premium : 0;

    /// <summary>
    /// Upload Bordx GetSuppExpDate (form version): a supplementary turning the plan's age limit in the expiry year expires on that
    /// birthday; otherwise the policy expiry applies.
    /// </summary>
    public static DateTime SupplementaryExpiry(DateTime dateOfBirth, DateTime expiry, decimal? ageLimit)
    {
        if (ageLimit is not { } limit || expiry.Year - dateOfBirth.Year != (int)limit) return expiry;
        var birthday = BirthdayIn(expiry.Year, dateOfBirth);
        return birthday <= expiry ? birthday : expiry;
    }

    /// <summary>Whole years between the birth year and the year of <paramref name="onDate"/> (the desktop's Year() - Year()).</summary>
    public static int CalendarYearAge(DateTime onDate, DateTime dateOfBirth) => onDate.Year - dateOfBirth.Year;

    /// <summary>The birthday in <paramref name="year"/>; 29 February becomes 28 February in non-leap years.</summary>
    public static DateTime BirthdayIn(int year, DateTime dateOfBirth) =>
        new(year, dateOfBirth.Month, Math.Min(dateOfBirth.Day, DateTime.DaysInMonth(year, dateOfBirth.Month)));

    /// <summary>
    /// SuppPrmStatus / SuppMCOStatus / SuppLimitStatus rule: is this supplementary charged? Principals are always charged.
    /// <paramref name="chargeFromCoverId"/> applies to status D (MCO.CovIDChrg).
    /// </summary>
    public static bool IsCharged(bool principal, string? status, string coverId, string? chargeFromCoverId = null)
    {
        if (principal) return true;
        return (status?.Trim().ToUpperInvariant() ?? "") switch
        {
            "" or SupplementaryCharge.PrincipalOnly => coverId == CoverIds.Principal,
            SupplementaryCharge.FirstSupplementaryOnly => coverId == CoverIds.FirstSupplementary,
            SupplementaryCharge.FromCoverId => string.CompareOrdinal(coverId, string.IsNullOrWhiteSpace(chargeFromCoverId) ? CoverIds.Principal : chargeFromCoverId.Trim()) >= 0,
            _ => true,
        };
    }
}
