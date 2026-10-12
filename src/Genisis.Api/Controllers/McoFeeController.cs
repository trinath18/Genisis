using Genisis.Api.Auth;
using Genisis.Api.Plans;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Maintenance > Plan, MCO Fee tab.</summary>
[ApiController]
[Route("api/maintenance/mco-fees")]
[RequireAccess(AccessRight.PlanMaintenance)]
public class McoFeeController(McoFeeService fees) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(string? planCode, string? healthCode, string? insuredCode, DateTime? effectiveDate)
    {
        var (rows, truncated) = await fees.SearchAsync(planCode, healthCode, insuredCode, effectiveDate);
        return Ok(new { rows, truncated, limit = McoFeeService.SearchLimit });
    }

    [HttpPost]
    public Task<IActionResult> Create(McoFeeCreateRequest request) =>
        Run(async () => Ok(new { codes = await fees.CreateAsync(request, User.Identity?.Name ?? "") }));

    [HttpPut("{index:int}")]
    public Task<IActionResult> Update(int index, McoFeeUpdateRequest request) =>
        Run(async () => { await fees.UpdateAsync(index, request, User.Identity?.Name ?? ""); return NoContent(); });

    [HttpDelete("{index:int}")]
    public Task<IActionResult> Delete(int index) => Run(async () => { await fees.DeleteAsync(index); return NoContent(); });

    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (PlanException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
