using Genisis.Api.Auth;
using Genisis.Api.ClaimBordx.Control;
using Genisis.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Claim Bordx > Monitoring > Outstanding Bordx (legacy FrmOSBordx): read only.</summary>
[Route("api/claim-bordx/outstanding")]
[RequireAccess(AccessRight.ClaimBordx)]
public class ClaimBordxOutstandingController(OutstandingBordxService service) : ApiControllerBase
{
    /// <summary>ComboListing: the payors with their layout.</summary>
    [HttpGet("payors")]
    public async Task<ActionResult<List<BordxControlPayor>>> Payors() => Ok(await service.PayorsAsync());

    /// <summary>cmdSearch_Click: the bordereaux that were sent and have not come back.</summary>
    [HttpGet]
    public async Task<ActionResult<OutstandingResult>> Search([FromQuery] OutstandingFilter filter) => Ok(await service.SearchAsync(filter));
}
