using Genisis.Api.Auth;
using Genisis.Api.ClaimBordx.Revise;
using Genisis.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Claim Bordx > Worksheet > Remove Worksheet (legacy FrmBordxRevise, "Bordereaux Revise").</summary>
[Route("api/claim-bordx/revise")]
[RequireAccess(AccessRight.ClaimBordx)]
public class ClaimBordxReviseController(BordxReviseService service) : ApiControllerBase
{
    /// <summary>CmdGo_Click: the worksheets of a bordx.</summary>
    [HttpGet]
    public async Task<ActionResult<BordxReviseResult>> Search([FromQuery] BordxReviseFilter filter) => Ok(await service.SearchAsync(filter));

    /// <summary>CmdReject_Click: takes the ticked worksheets out of the bordx.</summary>
    [HttpPost("remove")]
    public async Task<IActionResult> Remove(BordxReviseRequest request) => Ok(new { message = "Record updated...", removed = await service.RemoveAsync(request, UserCode) });
}
