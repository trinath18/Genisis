using Genisis.Api.Auth;
using Genisis.Api.Plans;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Maintenance > Plan, Premium tab.</summary>
[ApiController]
[Route("api/maintenance/premiums")]
[RequireAccess(AccessRight.PlanMaintenance)]
public class PremiumController(PremiumService premiums) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(string? insuredCode, string? payorCode, string? healthCode, string? ageCode, string? planCode)
    {
        var (rows, truncated) = await premiums.SearchAsync(insuredCode, payorCode, healthCode, ageCode, planCode);
        return Ok(new { rows, truncated, limit = PremiumService.SearchLimit });
    }

    [HttpPost]
    public Task<IActionResult> Create(PremiumCreateRequest request) => Run(async () => Ok(new { codes = await premiums.CreateAsync(request, User.Identity?.Name ?? "") }));

    [HttpPut("{code:int}")]
    public Task<IActionResult> Update(int code, PremiumUpdateRequest request) => Run(async () => { await premiums.UpdateAsync(code, request, User.Identity?.Name ?? ""); return NoContent(); });

    [HttpDelete("{code:int}")]
    public Task<IActionResult> Delete(int code) => Run(async () => { await premiums.DeleteAsync(code); return NoContent(); });

    private static async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (PlanException ex) { return new BadRequestObjectResult(new { message = ex.Message }); }
    }
}
