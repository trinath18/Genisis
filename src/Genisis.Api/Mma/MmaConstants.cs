namespace Genisis.Api.Mma;

/// <summary>Column sizes of MMAProcedures / MMAFees and the search limit.</summary>
public static class MmaLimits
{
    public const int SearchRows = 500;
    public const decimal MaximumFee = 99_999_999m;
    public const int BodyPartLength = 100;
    public const int CodeLength = 10;
    /// <summary>The table's limit; the desktop box allowed 3000, which the database rejected.</summary>
    public const int NarrativeLength = 400;
    public const int ProcedureCategoryLength = 30;
    public const int PageNoLength = 10;
    public const int PartACostLength = 50;
    public const int FeeSurgeonCategoryLength = 10;
    public const int FeeAnaesthetistCategoryLength = 50;

    /// <summary>Parts of the MMA schedule.</summary>
    public static readonly IReadOnlySet<string> Parts = new HashSet<string> { "A", "B", "C" };
}

public static class MmaMessages
{
    public const string NotFound = "Record not found";
    public const string FeeInUse = "This record cannot be deleted because it is used in Procedures table!";
    public const string FirstRecord = "This is first record!";
    public const string LastRecord = "This is last record!";
    public const string NoCode = "No Code Available !";
}
