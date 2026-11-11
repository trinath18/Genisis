using Genisis.Api.Auth;
using Genisis.Api.ClaimBordx.ReturnCase;
using Genisis.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Claim Bordx > Bor Delivery > Returned Fr Payor (Not App) (legacy FrmReturnPayorCase).</summary>
[Route("api/claim-bordx/return-case")]
[RequireAccess(AccessRight.ClaimBordx)]
public class ClaimBordxReturnCaseController(BordxReturnCaseService service) : ApiControllerBase
{
    /// <summary>TxtCaseNo_LostFocus: the versions of a case.</summary>
    [HttpGet("versions")]
    public async Task<ActionResult<List<int>>> Versions([FromQuery] string caseNo) => Ok(await service.VersionsAsync(caseNo));

    /// <summary>CmdSearch_Click: the worksheet and its bordx.</summary>
    [HttpGet]
    public async Task<ActionResult<BordxReturnCaseRow>> Find([FromQuery] BordxReturnCaseFilter filter) => Ok(await service.FindAsync(filter));

    /// <summary>CmdToPayor_Click: sets the worksheet's approved status.</summary>
    [HttpPost]
    public async Task<IActionResult> Set(BordxReturnCaseRequest request)
    {
        await service.SetAsync(request, UserCode);
        return Ok(new { message = "Record updated..." });
    }
}
