using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Maintenance.SurgicalSchedule;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Maintenance > Surgical Schedule (rebuilt on dbo.PLNSurgOperation: the desktop has no form for it).</summary>
[Route("api/maintenance/surgical-schedule")]
[RequireAccess(AccessRight.SurgicalSchedule)]
public class MaintenanceSurgicalScheduleController(SurgicalScheduleService service) : ApiControllerBase
{
    /// <summary>The payors of the drop-down.</summary>
    [HttpGet("payors")]
    public async Task<ActionResult<List<SurgicalSchedulePayor>>> Payors() => Ok(await service.GetPayorsAsync());

    /// <summary>A payor's plan and its surgical schedule.</summary>
    [HttpGet]
    public async Task<ActionResult<SurgicalScheduleResult>> Get([FromQuery] SurgicalScheduleQuery query) => Ok(await service.GetAsync(query));

    /// <summary>Adds a row to the plan's schedule.</summary>
    [HttpPost]
    public async Task<IActionResult> Add(SurgicalScheduleRequest request) => Ok(new { index = await service.SaveAsync(null, request, UserCode), message = "Record saved." });

    /// <summary>Changes a row of the plan's schedule.</summary>
    [HttpPut("{index:int}")]
    public async Task<IActionResult> Change(int index, SurgicalScheduleRequest request) => Ok(new { index = await service.SaveAsync(index, request, UserCode), message = "Record saved." });

    /// <summary>Deletes a row of the plan's schedule.</summary>
    [HttpDelete("{index:int}")]
    public async Task<IActionResult> Delete(int index, [FromQuery] SurgicalScheduleQuery query)
    {
        await service.DeleteAsync(index, query, UserCode);
        return Ok(new { message = "Record deleted." });
    }
}
