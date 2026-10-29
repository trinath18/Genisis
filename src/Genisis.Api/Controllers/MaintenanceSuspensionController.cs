using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Maintenance.Suspension;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Maintenance > Suspension (legacy frmSuspensionDetail, "Suspension Detail"; menu position 27): the Suspension Category and Suspension Details tabs.</summary>
[Route("api/maintenance/suspension")]
[RequireAccess(AccessRight.Suspension)]
public class MaintenanceSuspensionController(SuspensionService service) : ApiControllerBase
{
    /// <summary>SearchCAT: the categories (all, or the one the code starts).</summary>
    [HttpGet("categories")]
    public async Task<ActionResult<List<SuspensionCategory>>> Categories([FromQuery] string? code) => Ok(await service.SearchCategoriesAsync(code));

    /// <summary>SaveCAT, adding: a new category.</summary>
    [HttpPost("categories")]
    public async Task<IActionResult> AddCategory(SuspensionCategoryRequest request)
    {
        await service.SaveCategoryAsync(request, false, UserCode);
        return Ok(new { message = "Record Saved..." });
    }

    /// <summary>SaveCAT, editing: the description of a category.</summary>
    [HttpPut("categories")]
    public async Task<IActionResult> EditCategory(SuspensionCategoryRequest request)
    {
        await service.SaveCategoryAsync(request, true, UserCode);
        return Ok(new { message = "Record Updated..." });
    }

    /// <summary>DeleteCat: deletes a category no suspension uses.</summary>
    [HttpDelete("categories/{code}")]
    public async Task<IActionResult> DeleteCategory(string code)
    {
        await service.DeleteCategoryAsync(code, UserCode);
        return Ok(new { message = "Record Deleted... " });
    }

    /// <summary>SearchDetail: the suspended members and agents.</summary>
    [HttpGet("details")]
    public async Task<ActionResult<List<SuspensionDetailRow>>> Details([FromQuery] string? selection, [FromQuery] string? key, [FromQuery] string? payor) =>
        Ok(await service.SearchDetailsAsync(selection, key, payor));

    /// <summary>SaveDetail, adding: a new suspension.</summary>
    [HttpPost("details")]
    public async Task<IActionResult> AddDetail(SuspensionDetailRequest request)
    {
        await service.SaveDetailAsync(request, false, UserCode);
        return Ok(new { message = "Record Saved..." });
    }

    /// <summary>SaveDetail, editing: the description and payor of a suspension.</summary>
    [HttpPut("details")]
    public async Task<IActionResult> EditDetail(SuspensionDetailRequest request)
    {
        await service.SaveDetailAsync(request, true, UserCode);
        return Ok(new { message = "Record Updated..." });
    }

    /// <summary>DeleteDETAIL: removes a suspension.</summary>
    [HttpDelete("details")]
    public async Task<IActionResult> DeleteDetail([FromQuery] string selection, [FromQuery] string key)
    {
        await service.DeleteDetailAsync(selection, key, UserCode);
        return Ok(new { message = "Record Deleted..." });
    }
}
