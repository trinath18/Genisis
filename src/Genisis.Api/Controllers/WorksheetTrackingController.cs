using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Worksheet;
using Genisis.Api.Worksheet.Tracking;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Worksheet > Worksheet Tracking (legacy FrmWorksheetTracking).</summary>
[Route("api/worksheet/tracking")]
[RequireAccess(AccessRight.WorksheetTracking)]
public class WorksheetTrackingController(TrackingService service) : ApiControllerBase
{
    /// <summary>CmdSearch_Click: the worksheets that match the criteria.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<SearchResult<TrackingRow>>> Search([FromQuery] TrackingFilter filter) => Ok(await service.SearchAsync(filter));

    /// <summary>ChkGrpCom_Click: the group companies that have worksheets for a payor.</summary>
    [HttpGet("group-companies")]
    public async Task<ActionResult<List<string>>> GroupCompanies([FromQuery] string payor) => Ok(await service.GroupCompaniesAsync(payor));

    /// <summary>CmdUpdate_Click: gives the ticked worksheets a pending status.</summary>
    [HttpPost("update")]
    public async Task<IActionResult> Update(TrackingUpdateRequest request) =>
        Ok(new { message = "Record updated...", updated = await service.UpdateAsync(request, UserCode) });
}
