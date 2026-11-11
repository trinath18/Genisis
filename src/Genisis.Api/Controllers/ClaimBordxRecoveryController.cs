using Genisis.Api.Auth;
using Genisis.Api.ClaimBordx.Recovery;
using Genisis.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Claim Bordx > Print Recovery (legacy frmGenerateRecovery, "Generate Recovery Listing"): read only.</summary>
[Route("api/claim-bordx/recovery")]
[RequireAccess(AccessRight.ClaimBordx)]
public class ClaimBordxRecoveryController(PrintRecoveryService service) : ApiControllerBase
{
    /// <summary>combolist: the group companies that are BTBG clients.</summary>
    [HttpGet("companies")]
    public async Task<ActionResult<List<RecoveryCompany>>> Companies() => Ok(await service.CompaniesAsync());

    /// <summary>CmdSearch_Click: the recoveries of a group company.</summary>
    [HttpGet]
    public async Task<ActionResult<RecoveryResult>> Search([FromQuery] RecoveryFilter filter) => Ok(await service.SearchAsync(filter));
}
