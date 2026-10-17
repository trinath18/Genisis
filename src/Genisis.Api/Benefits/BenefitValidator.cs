namespace Genisis.Api.Benefits;

/// <summary>Required-field and length checks of the Benefits screen tabs, in the desktop's order.</summary>
public static class BenefitValidator
{
    private static string? Code(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string? Validate(BenefitCategoryRequest request)
    {
        if (Code(request.Code) is not { } code) return "Please Enter Category Code";
        if (Text(request.Description) is not { } description) return "Please Enter Category Description";
        if (code.Length > BenefitLimits.CategoryCodeLength) return $"Category Code cannot be longer than {BenefitLimits.CategoryCodeLength} characters.";
        if (description.Length > BenefitLimits.CategoryDescriptionLength) return $"Description cannot be longer than {BenefitLimits.CategoryDescriptionLength} characters.";
        return null;
    }

    public static string? Validate(BenefitSubCategoryRequest request)
    {
        if (Code(request.CategoryCode) is null) return "Please Enter Category Code";
        if (Code(request.Code) is not { } code) return "Please Enter Sub Category Code";
        if (Text(request.Name) is not { } name) return "Please Enter Sub Category Description";
        if (code.Length > BenefitLimits.SubCategoryCodeLength) return $"Sub Category Code cannot be longer than {BenefitLimits.SubCategoryCodeLength} characters.";
        if (name.Length > BenefitLimits.SubCategoryNameLength) return $"Description cannot be longer than {BenefitLimits.SubCategoryNameLength} characters.";
        return null;
    }

    /// <summary>Health type, insured type, plan and effective date of the Benefit Entry grid and of a listing row.</summary>
    public static string? ValidateKey(string? health, string? insured, string? plan, DateTime? effective)
    {
        if (Code(health) is not { } healthCode) return "Please Enter Health Type";
        if (Code(insured) is not { } insuredCode) return "Please Enter Insured Type";
        if (Code(plan) is not { } planCode) return "Please Enter Plan Code";
        if (effective is null) return "Please Enter a valid Effective Date";
        if (healthCode.Length > BenefitLimits.HealthOrInsuredCodeLength || insuredCode.Length > BenefitLimits.HealthOrInsuredCodeLength)
            return $"Health and Insured Type cannot be longer than {BenefitLimits.HealthOrInsuredCodeLength} characters.";
        if (planCode.Length > BenefitLimits.PlanCodeLength) return $"Plan Code cannot be longer than {BenefitLimits.PlanCodeLength} characters.";
        return null;
    }

    private static string? ValidateAmounts(BenefitAmounts amounts, string prefix = "")
    {
        if (Text(amounts.PerDisability) is null) return $"{prefix}Please Enter RM Per Disability";
        if (amounts.Days is < 0 or > short.MaxValue) return $"{prefix}Days/Months must be a whole number from 0 to {short.MaxValue}.";
        if (Text(amounts.RatePerDay)?.Length > BenefitLimits.RatePerDayLength) return $"{prefix}RM / Day cannot be longer than {BenefitLimits.RatePerDayLength} characters.";
        if (Text(amounts.PerDisability)!.Length > BenefitLimits.PerDisabilityLength)
            return $"{prefix}RM Per Disability cannot be longer than {BenefitLimits.PerDisabilityLength} characters.";
        return null;
    }

    /// <summary>A line counts when any amount is filled; like the desktop, it is saved only with an RM per disability.</summary>
    public static bool IsFilled(BenefitAmounts amounts) =>
        amounts.Days is not null || Text(amounts.RatePerDay) is not null || Text(amounts.PerDisability) is not null;

    public static string? Validate(BenefitEntryRequest request)
    {
        if (ValidateKey(request.HealthCode, request.InsuredCode, request.PlanCode, request.EffectiveDate) is { } error) return error;
        var lines = (request.Lines ?? []).Where(IsFilled).ToList();
        if (lines.Count == 0) return "Please Enter RM Per Disability for at least one sub category.";
        if (lines.Any(l => Code(l.SubCategoryCode) is null)) return "Please Enter Benefit Sub Category Code";
        if (lines.GroupBy(l => Code(l.SubCategoryCode)).FirstOrDefault(g => g.Count() > 1) is { } duplicate)
            return $"Sub category {duplicate.Key} is entered more than once.";
        return lines.Select(l => ValidateAmounts(l, $"Sub category {Code(l.SubCategoryCode)}: ")).FirstOrDefault(e => e is not null);
    }

    public static string? Validate(BenefitRequest request)
    {
        if (Code(request.SubCategoryCode) is null) return "Please Enter Benefit Sub Category Code";
        return ValidateKey(request.HealthCode, request.InsuredCode, request.PlanCode, request.EffectiveDate) ?? ValidateAmounts(request);
    }

    public static string? Validate(BenefitKey key) =>
        Code(key.SubCategoryCode) is null || Code(key.InsuredCode) is null || Code(key.PlanCode) is null || key.EffectiveDate is null
            ? BenefitMessages.NotFound
            : null;
}
