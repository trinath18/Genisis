using Genisis.Api.Auth;
using Genisis.Api.Benefits;
using Genisis.Api.Data;
using Genisis.Api.Plans;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Maintenance > Benefits (legacy frmMaintenanceBenefits): Category, Sub Category, Benefit Entry and Benefits Listing tabs.</summary>
[ApiController]
[Route("api/maintenance/benefits")]
[RequireAccess(AccessRight.BenefitsMaintenance)]
public class BenefitsMaintenanceController(DbConnections db, BenefitService benefits) : ControllerBase
{
    public record Option(string Code, string? Name);

    private string UserCode => User.Identity?.Name ?? "";

    [HttpGet("lookups")]
    public async Task<IActionResult> Lookups()
    {
        await using var conn = db.MainDb();
        using var grid = await conn.ProcMultipleAsync("genisis.Benefits_Lookups");
        return Ok(new
        {
            healthTypes = (await grid.ReadAsync<Option>()).ToList(),
            insuredTypes = (await grid.ReadAsync<Option>()).ToList(),
            categories = (await grid.ReadAsync<Option>()).ToList(),
            subCategories = (await grid.ReadAsync<Option>()).ToList(),
            payors = (await grid.ReadAsync<Option>()).ToList(),
            groupCompanies = (await grid.ReadAsync<string>()).ToList(),
        });
    }

    [HttpGet("categories")]
    public async Task<IActionResult> SearchCategories(string? code, string? description) =>
        Ok(await benefits.SearchCategoriesAsync(code, description));

    [HttpPost("categories")]
    public Task<IActionResult> CreateCategory(BenefitCategoryRequest request) => Run(() => benefits.SaveCategoryAsync(request, false, UserCode));

    [HttpPut("categories/{code}")]
    public Task<IActionResult> UpdateCategory(string code, BenefitCategoryRequest request)
    {
        request.Code = code;
        return Run(() => benefits.SaveCategoryAsync(request, true, UserCode));
    }

    [HttpDelete("categories/{code}")]
    public Task<IActionResult> DeleteCategory(string code) => Run(() => benefits.DeleteCategoryAsync(code));

    [HttpGet("sub-categories")]
    public async Task<IActionResult> SearchSubCategories(string? categoryCode, string? code, string? name) =>
        Ok(await benefits.SearchSubCategoriesAsync(categoryCode, code, name));

    [HttpPost("sub-categories")]
    public Task<IActionResult> CreateSubCategory(BenefitSubCategoryRequest request) => Run(() => benefits.SaveSubCategoryAsync(request, false, UserCode));

    [HttpPut("sub-categories/{code}")]
    public Task<IActionResult> UpdateSubCategory(string code, BenefitSubCategoryRequest request)
    {
        request.Code = code;
        return Run(() => benefits.SaveSubCategoryAsync(request, true, UserCode));
    }

    [HttpDelete("sub-categories/{code}")]
    public Task<IActionResult> DeleteSubCategory(string code) => Run(() => benefits.DeleteSubCategoryAsync(code));

    /// <summary>Benefit Entry grid: every sub category with what is saved for the key, and whether the plan / insured / date already has benefits.</summary>
    [HttpGet("entry")]
    public Task<IActionResult> LoadEntry(string? planCode, string? insuredCode, string? healthCode, DateTime? effectiveDate) =>
        Run(async () =>
        {
            var (lines, setExists) = await benefits.LoadEntryAsync(planCode, insuredCode, healthCode, effectiveDate);
            return Ok(new { lines, setExists });
        });

    [HttpPost("entry")]
    public Task<IActionResult> SaveEntry(BenefitEntryRequest request) =>
        Run(async () => Ok(new { saved = await benefits.SaveEntryAsync(request, UserCode) }));

    [HttpGet("rows")]
    public async Task<IActionResult> Search([FromQuery] BenefitSearch search)
    {
        var (rows, truncated) = await benefits.SearchAsync(search);
        return Ok(new { rows, truncated, limit = BenefitService.SearchLimit });
    }

    [HttpPost("rows")]
    public Task<IActionResult> Create(BenefitRequest request) => Run(() => benefits.CreateAsync(request, UserCode));

    /// <summary>The row is identified by the sub category, insured type, plan code and effective date in the body.</summary>
    [HttpPut("rows")]
    public Task<IActionResult> Update(BenefitRequest request) => Run(() => benefits.UpdateAsync(request, UserCode));

    [HttpDelete("rows")]
    public Task<IActionResult> Delete(string? subCategoryCode, string? insuredCode, string? planCode, DateTime? effectiveDate) =>
        Run(() => benefits.DeleteAsync(subCategoryCode, insuredCode, planCode, effectiveDate));

    private Task<IActionResult> Run(Func<Task> action) => Run(async () => { await action(); return NoContent(); });

    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (PlanException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
