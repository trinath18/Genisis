using Genisis.Api.Auth;
using Genisis.Api.ClaimBordx.Update;
using Genisis.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Claim Bordx > Update Bordx (legacy frmUpdateWS2Bordx, "Update Bordx").</summary>
[Route("api/claim-bordx/update-bordx")]
[RequireAccess(AccessRight.ClaimBordx)]
public class ClaimBordxUpdateController(UpdateBordxService service) : ApiControllerBase
{
    /// <summary>CmdSearch_Click: the pending worksheets that match.</summary>
    [HttpGet]
    public async Task<ActionResult<UpdateBordxSearchResult>> Search([FromQuery] UpdateBordxFilter filter) => Ok(await service.SearchAsync(filter));

    /// <summary>CmdBordx_Click: puts the checked worksheets into the bordx and saves the bordx.</summary>
    [HttpPost]
    public async Task<ActionResult<UpdateBordxResult>> Update(UpdateBordxRequest request) => Ok(await service.UpdateAsync(request, UserCode));
}
