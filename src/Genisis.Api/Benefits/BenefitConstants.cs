namespace Genisis.Api.Benefits;

/// <summary>Column sizes of BNFCategory, BNFSubCat and BNF, and the listing limit.</summary>
public static class BenefitLimits
{
    public const int SearchRows = 500;
    public const int CategoryCodeLength = 10;
    public const int CategoryDescriptionLength = 255;
    public const int SubCategoryCodeLength = 10;
    public const int SubCategoryNameLength = 100;
    public const int HealthOrInsuredCodeLength = 2;
    public const int PlanCodeLength = 4;
    public const int RatePerDayLength = 15;
    public const int PerDisabilityLength = 50;
}

/// <summary>Messages of the Benefits screen (desktop texts where it had one).</summary>
public static class BenefitMessages
{
    public const string NotFound = "Record not found";
    public const string Duplicated = "Record Duplicated! Please Change The Corresponding Field";
    public const string SetExists = Duplicated + " (this plan, insured type and effective date already have benefits; change them on the Benefits Listing tab)";
    public const string CategoryNotFound = "Category Code was not found.";
    public const string UsedByMembers = "This record cannot be deleted because it is used in Member table!";

    public static string UsedIn(string? table) => $"This record cannot be deleted because it is used in {table} table!";
}

/// <summary>Value of the procedures' Missing column when a sub category's category does not exist.</summary>
internal static class BenefitMissingReferences
{
    public const string Category = "Category";
}
