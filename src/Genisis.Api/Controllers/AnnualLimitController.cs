using Dapper;
using Genisis.Api.Auth;
using Genisis.Api.Data;
using Genisis.Api.Plans;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Maintenance > Plan, Annual Limit tab.</summary>
[ApiController]
[Route("api/maintenance/annual-limits")]
[RequireAccess(AccessRight.PlanMaintenance)]
public class AnnualLimitController(DbConnections db, AnnualLimitService limits) : ControllerBase
{
    [HttpGet("insured-types")]
    public async Task<IActionResult> InsuredTypes()
    {
        await using var conn = db.MainDb();
        return Ok(await conn.ProcQueryAsync<PlanMaintenanceController.Option>("genisis.Lookup_InsuredTypes"));
    }

    [HttpGet]
    public async Task<IActionResult> Search(string? planCode, string? healthCode, string? insuredCode, string? payorCode, string? groupCompany)
    {
        var (rows, truncated) = await limits.SearchAsync(planCode, healthCode, insuredCode, payorCode, groupCompany);
        return Ok(new { rows, truncated, limit = AnnualLimitService.SearchLimit });
    }

    [HttpPost]
    public Task<IActionResult> Create(AnnualLimitRequest request) => Run(async () => Ok(new { index = await limits.SaveAsync(request, null, User.Identity?.Name ?? "") }));

    [HttpPut("{index:int}")]
    public Task<IActionResult> Update(int index, AnnualLimitRequest request) => Run(async () => Ok(new { index = await limits.SaveAsync(request, index, User.Identity?.Name ?? "") }));

    [HttpDelete("{index:int}")]
    public Task<IActionResult> Delete(int index) => Run(async () => { await limits.DeleteAsync(index); return NoContent(); });

    private static async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (PlanException ex) { return new BadRequestObjectResult(new { message = ex.Message }); }
    }
}
