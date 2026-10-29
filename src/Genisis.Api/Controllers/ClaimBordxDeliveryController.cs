using Genisis.Api.Auth;
using Genisis.Api.ClaimBordx.Delivery;
using Genisis.Api.Common;
using Genisis.Api.Worksheet;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Claim Bordx > Bor Delivery > Send to Payor and Send to A/C (legacy FrmSendtoPayor and FrmSendtoAC): <c>kind</c> is <c>to-payor</c> or <c>to-accounts</c>.</summary>
[Route("api/claim-bordx/delivery/" + BordxDeliveryKinds.RoutePattern)]
[RequireAccess(AccessRight.ClaimBordx)]
public class ClaimBordxDeliveryController(BordxDeliveryService service) : ApiControllerBase
{
    /// <summary>CmdSearch_Click: the bordx that match the criteria.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<SearchResult<BordxDeliveryRow>>> Search(string kind, [FromQuery] BordxDeliveryFilter filter) => Ok(await service.SearchAsync(kind, filter));

    /// <summary>CmdToPayor_Click / CmdToAC_Click: records the date on the ticked bordx.</summary>
    [HttpPost("send")]
    public async Task<IActionResult> Send(string kind, BordxDeliveryRequest request) =>
        Ok(new { message = "Record updated...", updated = await service.SendAsync(kind, request, UserCode) });
}
