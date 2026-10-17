using Genisis.Api.Auth;
using Genisis.Api.Benefits;
using Genisis.Api.Common.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Maintenance &gt; Benefits (legacy frmMaintenanceBenefits): Category, Sub Category, Benefit Entry and Benefits Listing tabs.</summary>
[ApiController]
[Route("api/maintenance/benefits")]
[RequireAccess(AccessRight.BenefitsMaintenance)]
public sealed class BenefitsMaintenanceController(IBenefitService benefits) : ControllerBase
{
    private string UserCode => User.Identity?.Name ?? "";

    [HttpGet("lookups")]
    public async Task<ActionResult<BenefitLookups>> GetLookups(CancellationToken cancellationToken) => Ok(await benefits.GetLookupsAsync(cancellationToken));

    [HttpGet("categories")]
    public async Task<ActionResult<List<BenefitCategoryRow>>> SearchCategories(string? code, string? description, CancellationToken cancellationToken) =>
        Ok(await benefits.SearchCategoriesAsync(code, description, cancellationToken));

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory(BenefitCategoryRequest request, CancellationToken cancellationToken)
    {
        if (BenefitValidator.Validate(request) is { } error) return this.Invalid(error);
        await benefits.SaveCategoryAsync(request, update: false, UserCode, cancellationToken);
        return StatusCode(StatusCodes.Status201Created);
    }

    [HttpPut("categories/{code}")]
    public async Task<IActionResult> UpdateCategory(string code, BenefitCategoryRequest request, CancellationToken cancellationToken)
    {
        request.Code = code;
        if (BenefitValidator.Validate(request) is { } error) return this.Invalid(error);
        await benefits.SaveCategoryAsync(request, update: true, UserCode, cancellationToken);
        return NoContent();
    }

    [HttpDelete("categories/{code}")]
    public async Task<IActionResult> DeleteCategory(string code, CancellationToken cancellationToken)
    {
        await benefits.DeleteCategoryAsync(code, cancellationToken);
        return NoContent();
    }

    [HttpGet("sub-categories")]
    public async Task<ActionResult<List<BenefitSubCategoryRow>>> SearchSubCategories(string? categoryCode, string? code, string? name,
        CancellationToken cancellationToken) =>
        Ok(await benefits.SearchSubCategoriesAsync(categoryCode, code, name, cancellationToken));

    [HttpPost("sub-categories")]
    public async Task<IActionResult> CreateSubCategory(BenefitSubCategoryRequest request, CancellationToken cancellationToken)
    {
        if (BenefitValidator.Validate(request) is { } error) return this.Invalid(error);
        await benefits.SaveSubCategoryAsync(request, update: false, UserCode, cancellationToken);
        return StatusCode(StatusCodes.Status201Created);
    }

    [HttpPut("sub-categories/{code}")]
    public async Task<IActionResult> UpdateSubCategory(string code, BenefitSubCategoryRequest request, CancellationToken cancellationToken)
    {
        request.Code = code;
        if (BenefitValidator.Validate(request) is { } error) return this.Invalid(error);
        await benefits.SaveSubCategoryAsync(request, update: true, UserCode, cancellationToken);
        return NoContent();
    }

    [HttpDelete("sub-categories/{code}")]
    public async Task<IActionResult> DeleteSubCategory(string code, CancellationToken cancellationToken)
    {
        await benefits.DeleteSubCategoryAsync(code, cancellationToken);
        return NoContent();
    }

    /// <summary>Benefit Entry grid: every sub category with what is saved for the key, and whether the plan / insured / date already has benefits.</summary>
    [HttpGet("entry")]
    public async Task<ActionResult<BenefitEntry>> LoadEntry(string? planCode, string? insuredCode, string? healthCode, DateTime? effectiveDate,
        CancellationToken cancellationToken)
    {
        if (BenefitValidator.ValidateKey(healthCode, insuredCode, planCode, effectiveDate) is { } error) return this.Invalid(error);
        return Ok(await benefits.LoadEntryAsync(planCode!, insuredCode!, healthCode!, effectiveDate!.Value, cancellationToken));
    }

    [HttpPost("entry")]
    public async Task<IActionResult> SaveEntry(BenefitEntryRequest request, CancellationToken cancellationToken)
    {
        if (BenefitValidator.Validate(request) is { } error) return this.Invalid(error);
        return StatusCode(StatusCodes.Status201Created, new { saved = await benefits.SaveEntryAsync(request, UserCode, cancellationToken) });
    }

    [HttpGet("rows")]
    public async Task<ActionResult<BenefitSearchResult>> Search([FromQuery] BenefitSearch search, CancellationToken cancellationToken) =>
        Ok(await benefits.SearchAsync(search, cancellationToken));

    [HttpPost("rows")]
    public async Task<IActionResult> Create(BenefitRequest request, CancellationToken cancellationToken)
    {
        if (BenefitValidator.Validate(request) is { } error) return this.Invalid(error);
        await benefits.CreateAsync(request, UserCode, cancellationToken);
        return StatusCode(StatusCodes.Status201Created);
    }

    /// <summary>The row is identified by the sub category, insured type, plan code and effective date in the body.</summary>
    [HttpPut("rows")]
    public async Task<IActionResult> Update(BenefitRequest request, CancellationToken cancellationToken)
    {
        if (BenefitValidator.Validate(request) is { } error) return this.Invalid(error);
        await benefits.UpdateAsync(request, UserCode, cancellationToken);
        return NoContent();
    }

    [HttpDelete("rows")]
    public async Task<IActionResult> Delete(string? subCategoryCode, string? insuredCode, string? planCode, DateTime? effectiveDate,
        CancellationToken cancellationToken)
    {
        var key = new BenefitKey(subCategoryCode, insuredCode, planCode, effectiveDate);
        if (BenefitValidator.Validate(key) is { } error) return this.Invalid(error);
        await benefits.DeleteAsync(key, cancellationToken);
        return NoContent();
    }
}
