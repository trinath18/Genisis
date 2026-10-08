using Dapper;
using Genisis.Api.Auth;
using Genisis.Api.Data;
using Genisis.Api.Plans;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Controllers;

/// <summary>Maintenance > Plan, Plan tab (legacy frmMaintenencePlan).</summary>
[ApiController]
[Route("api/maintenance/plans")]
[RequireAccess(AccessRight.PlanMaintenance)]
public class PlanMaintenanceController(DbConnections db, PlanService plans) : ControllerBase
{
    public record Option(string Code, string Name);

    [HttpGet("lookups")]
    public async Task<IActionResult> Lookups()
    {
        await using var conn = db.MainDb();
        var maintenanceDb = await db.MaintenanceDbNameAsync();
        async Task<List<Option>> Options(string sql) => (await conn.QueryAsync<Option>(sql)).ToList();
        return Ok(new
        {
            healthCodes = await Options("SELECT RTRIM(HLTCode) AS Code, RTRIM(HLTDescription) AS Name FROM dbo.HLT ORDER BY HLTCode"),
            payors = await Options("SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name FROM dbo.PAY WHERE PAYCode IS NOT NULL ORDER BY PAYCode"),
            productCategories = await Options($"SELECT RTRIM(ProductCode) AS Code, RTRIM(ProductName) AS Name FROM {maintenanceDb}.dbo.ProductCategory ORDER BY ProductCode"),
            policyWordings = (await conn.QueryAsync<string>($"SELECT RTRIM(PolicyWording) FROM {maintenanceDb}.dbo.PolicyCategory ORDER BY PolicyWording")).ToList(),
            maxPlans = PlanValidator.MaxPlans,
        });
    }

    [HttpGet("client-plans")]
    public IActionResult ClientPlans([FromQuery] string? payorCode) => Ok(PlanValidator.ClientPlans(payorCode));

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] PlanSearch search)
    {
        var (rows, truncated) = await plans.SearchAsync(search);
        return Ok(new { rows, truncated, limit = PlanService.SearchLimit });
    }

    [HttpPost]
    public async Task<IActionResult> Create(PlanCreateRequest request)
    {
        if (PlanValidator.Validate(request) is { } error) return BadRequest(new { message = error });
        return await Run(async () => Ok(await plans.CreateAsync(request, User.Identity?.Name ?? "")));
    }

    [HttpDelete("{index:int}")]
    public Task<IActionResult> Delete(int index) => Run(async () =>
    {
        await plans.DeleteAsync(index);
        return NoContent();
    });

    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (PlanException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (SqlException ex) when (ex.Number is 8152 or 2628)
        {
            return BadRequest(new { message = "One of the values is longer than the database allows. Shorten it and try again." });
        }
    }
}
