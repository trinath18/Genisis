using Genisis.Api.Auth;
using Genisis.Api.ClaimBordx.Control;
using Genisis.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Claim Bordx > Monitoring > Bordx Control (legacy FrmBordxControl): read only.</summary>
[Route("api/claim-bordx/control")]
[RequireAccess(AccessRight.ClaimBordx)]
public class ClaimBordxControlController(BordxControlService service) : ApiControllerBase
{
    /// <summary>PayorComboListing: the payors with their layout.</summary>
    [HttpGet("payors")]
    public async Task<ActionResult<List<BordxControlPayor>>> Payors() => Ok(await service.PayorsAsync());

    /// <summary>cmdSearch_Click: the bordereaux and when they moved.</summary>
    [HttpGet]
    public async Task<ActionResult<BordxControlResult>> Search([FromQuery] BordxControlFilter filter) => Ok(await service.SearchAsync(filter));
}
