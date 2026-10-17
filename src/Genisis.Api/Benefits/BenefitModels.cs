using Genisis.Api.Common.Errors;

namespace Genisis.Api.Benefits;

public class BenefitCategoryRequest
{
    public string? Code { get; set; }
    public string? Description { get; set; }
}

public sealed record BenefitCategoryRow(string Code, string? Description);

public class BenefitSubCategoryRequest
{
    public string? CategoryCode { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
}

public sealed record BenefitSubCategoryRow(string? CategoryCode, string? CategoryDescription, string Code, string? Name);

/// <summary>Days, RM / day and RM per disability of one benefit row. RM / day and RM per disability are text in dbo.BNF
/// (RM per disability is often "FULL").</summary>
public class BenefitAmounts
{
    public string? SubCategoryCode { get; set; }
    public int? Days { get; set; }
    public string? RatePerDay { get; set; }
    public string? PerDisability { get; set; }
}

/// <summary>Benefit Entry tab: one save adds a row for every sub category line that has an RM per disability.</summary>
public class BenefitEntryRequest
{
    public string? PlanCode { get; set; }
    public string? InsuredCode { get; set; }
    public string? HealthCode { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public List<BenefitAmounts> Lines { get; set; } = [];
}

public sealed record BenefitEntryLine(string Code, string? Name, short? Days, string? RatePerDay, string? PerDisability);

public sealed record BenefitEntry(List<BenefitEntryLine> Lines, bool SetExists);

/// <summary>Benefits Listing tab: one row. Sub category, insured, plan and effective date identify it (the dbo.BNF key).</summary>
public class BenefitRequest : BenefitAmounts
{
    public string? HealthCode { get; set; }
    public string? InsuredCode { get; set; }
    public string? PlanCode { get; set; }
    public DateTime? EffectiveDate { get; set; }
}

public sealed record BenefitRow(string SubCategoryCode, string? SubCategoryName, string HealthCode, string InsuredCode, string PlanCode,
    short? Days, string? RatePerDay, string? PerDisability, DateTime EffectiveDate, string? PayorCode, string? GroupCompany);

public sealed record BenefitSearchResult(List<BenefitRow> Rows, bool Truncated, int Limit);

public class BenefitSearch
{
    public string? SubCategoryCode { get; set; }
    public string? HealthCode { get; set; }
    public string? InsuredCode { get; set; }
    public string? PlanCode { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public string? PayorCode { get; set; }
    public string? GroupCompany { get; set; }
}

/// <summary>Key of one Benefits Listing row (query string of DELETE rows).</summary>
public sealed record BenefitKey(string? SubCategoryCode, string? InsuredCode, string? PlanCode, DateTime? EffectiveDate);

public sealed record BenefitOption(string Code, string? Name);

public sealed record BenefitLookups(List<BenefitOption> HealthTypes, List<BenefitOption> InsuredTypes, List<BenefitOption> Categories,
    List<BenefitOption> SubCategories, List<BenefitOption> Payors, List<string> GroupCompanies);

/// <summary>A benefits rule stopped the change (duplicate, still in use, missing reference).</summary>
public class BenefitException(string message) : BusinessRuleException(message);
