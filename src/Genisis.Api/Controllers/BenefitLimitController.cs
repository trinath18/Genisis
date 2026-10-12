using Genisis.Api.Auth;
using Genisis.Api.Plans;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Maintenance > Plan, Annual Limit (Benefit) tab.</summary>
[ApiController]
[Route("api/maintenance/benefit-limits")]
[RequireAccess(AccessRight.PlanMaintenance)]
public class BenefitLimitController(BenefitLimitService limits) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(string? planCode, string? healthCode, string? insuredCode) =>
        Ok(await limits.SearchAsync(planCode, healthCode, insuredCode));

    [HttpPost]
    public Task<IActionResult> Create(BenefitLimitRequest request) =>
        Run(async () => Ok(new { index = await limits.SaveAsync(null, request, User.Identity?.Name ?? "") }));

    [HttpPut("{index:int}")]
    public Task<IActionResult> Update(int index, BenefitLimitRequest request) =>
        Run(async () => { await limits.SaveAsync(index, request, User.Identity?.Name ?? ""); return NoContent(); });

    [HttpDelete("{index:int}")]
    public Task<IActionResult> Delete(int index) => Run(async () => { await limits.DeleteAsync(index); return NoContent(); });

    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (PlanException ex) { return BadRequest(new { message = ex.Message }); }
    }
}

/// <summary>Maintenance > Plan, Case Mgmt. Fee tab. A row is identified by its plan / health / insured codes.</summary>
[ApiController]
[Route("api/maintenance/mgmt-fees")]
[RequireAccess(AccessRight.PlanMaintenance)]
public class MgmtFeeController(MgmtFeeService fees) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(string? planCode, string? healthCode, string? insuredCode) =>
        Ok(await fees.SearchAsync(planCode, healthCode, insuredCode));

    [HttpPost]
    public Task<IActionResult> Create(MgmtFeeRequest request) => Run(() => fees.SaveAsync(request, false, User.Identity?.Name ?? ""));

    [HttpPut("{planCode}/{healthCode}/{insuredCode}")]
    public Task<IActionResult> Update(string planCode, string healthCode, string insuredCode, MgmtFeeRequest request)
    {
        request.PlanCode = planCode;
        request.HealthCode = healthCode;
        request.InsuredCode = insuredCode;
        return Run(() => fees.SaveAsync(request, true, User.Identity?.Name ?? ""));
    }

    [HttpDelete("{planCode}/{healthCode}/{insuredCode}")]
    public Task<IActionResult> Delete(string planCode, string healthCode, string insuredCode) =>
        Run(() => fees.DeleteAsync(planCode, healthCode, insuredCode));

    private async Task<IActionResult> Run(Func<Task> action)
    {
        try { await action(); return NoContent(); }
        catch (PlanException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
