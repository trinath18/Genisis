using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Worksheet;
using Genisis.Api.Worksheet.SendToAr;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Worksheet > Worksheet Send to A/R (legacy FrmARRecFrCLM, "Date Send to Audit Review").</summary>
[Route("api/worksheet/send-to-ar")]
[RequireAccess(AccessRight.WorksheetSendToAr)]
public class WorksheetSendToArController(SendToArService service) : ApiControllerBase
{
    /// <summary>CmdSearch_Click: the cases that match the criteria.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<SearchResult<SendToArRow>>> Search([FromQuery] SendToArFilter filter) => Ok(await service.SearchAsync(filter));

    /// <summary>CmdToAC_Click: sends the ticked cases to audit review.</summary>
    [HttpPost("send")]
    public async Task<IActionResult> Send(SendToArRequest request) =>
        Ok(new { message = "Record updated...", updated = await service.SendAsync(request, UserCode) });
}
