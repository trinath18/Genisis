using Genisis.Api.Membership;

namespace Genisis.Api.Adjustment;

/// <summary>Adjustment calculations that need no database access.</summary>
public static class AdjustmentRules
{
    /// <summary>Cover IDs as stored ("1" and "01" both mean the first supplementary).</summary>
    public static string CoverId(string? id) => int.TryParse(id?.Trim(), out var number) ? CoverIds.Format(number) : id?.Trim() ?? "";

    /// <summary>
    /// Legacy GetSuppExpDate (Adjustment version): a supplementary reaching the plan's age limit in the policy year expires on that
    /// birthday, and is over age when the birthday has already passed on the effective date.
    /// </summary>
    public static SupplementaryExpiry SupplementaryExpiry(DateTime dateOfBirth, int ageLimit, DateTime effective, DateTime expiry)
    {
        if (ageLimit == 0) return new(expiry, false, 0);
        var years = FeeRules.CalendarYearAge(expiry, dateOfBirth);
        if (years != ageLimit) return new(expiry, years > ageLimit, ageLimit);
        var birthdayInExpiryYear = FeeRules.BirthdayIn(expiry.Year, dateOfBirth);
        var birthdayInEffectiveYear = FeeRules.BirthdayIn(effective.Year, dateOfBirth);
        return new(birthdayInExpiryYear <= expiry ? birthdayInExpiryYear : expiry, birthdayInEffectiveYear <= effective, ageLimit);
    }

    /// <summary>The disability limit applies to the members charged an annual limit (SuppLimitStatus A / B / C).</summary>
    public static bool DisabilityLimitApplies(string? suppLimitStatus, string coverId) => (suppLimitStatus?.Trim().ToUpperInvariant() ?? "") switch
    {
        "" or SupplementaryCharge.PrincipalOnly => coverId == CoverIds.Principal,
        SupplementaryCharge.FirstSupplementaryOnly => coverId == CoverIds.FirstSupplementary,
        SupplementaryCharge.Everyone => true,
        _ => false,
    };

    /// <summary>A supplementary's annual limit, premium or clinical fee is charged for status C, or B on the first covered person.</summary>
    public static bool SupplementaryCharged(string? status, string coverId) =>
        (status?.Trim().ToUpperInvariant() ?? "") is var s && (s == SupplementaryCharge.Everyone || (s == SupplementaryCharge.FirstSupplementaryOnly && coverId == CoverIds.FirstSupplementary));

    /// <summary>Policy-length warning of MemberCallFees.</summary>
    public static string? PolicyLengthWarning(DateTime effective, DateTime expiry) => (expiry.Date - effective.Date).Days switch
    {
        > AdjustmentLimits.MaximumPolicyDays => "Policy more than 365 days.",
        <= AdjustmentLimits.MinimumPolicyDays => "Policy less than 30 days.",
        _ => null,
    };

    /// <summary>
    /// Renewal key of a renewal membership number (payor*insured*dob*name); a name or birth date change moves the renewals to the new key.
    /// </summary>
    public static string RenewalKey(string? payor, string? insured, DateTime? dateOfBirth, string? name) =>
        $"{(payor ?? "").Trim().ToUpperInvariant()}*{(insured ?? "").Trim().ToUpperInvariant()}*{dateOfBirth:yyyyMMdd}*{(name ?? "").Trim()}";
}
