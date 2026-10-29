using Genisis.Api.Auth;
using Genisis.Api.ClaimBordx.Delivery;
using Genisis.Api.Common;
using Genisis.Api.Worksheet;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Claim Bordx > Bor Delivery > Send to Payor, Send to A/C and Returned From Payor (legacy FrmSendtoPayor, FrmSendtoAC and FrmReturnPayor): <c>kind</c> is <c>to-payor</c>, <c>to-accounts</c> or <c>from-payor</c>.</summary>
[Route("api/claim-bordx/delivery/" + BordxDeliveryKinds.RoutePattern)]
[RequireAccess(AccessRight.ClaimBordx)]
public class ClaimBordxDeliveryController(BordxDeliveryService service) : ApiControllerBase
{
    /// <summary>CmdSearch_Click: the bordx that match the criteria.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<SearchResult<BordxDeliveryRow>>> Search(string kind, [FromQuery] BordxDeliveryFilter filter) => Ok(await service.SearchAsync(kind, filter));

    /// <summary>CmdToPayor_Click / CmdToAC_Click: records the date on the ticked bordx (and, for a return, approves their open claims).</summary>
    [HttpPost("send")]
    public async Task<IActionResult> Send(string kind, BordxDeliveryRequest request) =>
        Ok(new { message = "Record updated...", updated = await service.SendAsync(kind, request, UserCode) });
}
