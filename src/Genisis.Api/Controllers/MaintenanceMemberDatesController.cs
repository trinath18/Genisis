using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Maintenance.MemberDates;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Maintenance > Member (legacy frmMaintenanceMembers, "Members Maintenance": the Valid Date Range tab).</summary>
[Route("api/maintenance/member-dates")]
[RequireAccess(AccessRight.MemberMaintenance)]
public class MaintenanceMemberDatesController(MemberDatesService service) : ApiControllerBase
{
    /// <summary>LoadDate: the saved date ranges.</summary>
    [HttpGet]
    public async Task<ActionResult<MemberDatesRow>> Get() => Ok(await service.GetAsync());

    /// <summary>cmdUpdate_Click: saves the six dates.</summary>
    [HttpPut]
    public async Task<IActionResult> Save(MemberDatesRequest request)
    {
        await service.SaveAsync(request, UserCode);
        return Ok(new { message = "Record Saved..." });
    }
}
