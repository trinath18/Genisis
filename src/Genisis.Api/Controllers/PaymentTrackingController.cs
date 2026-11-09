using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Payment.Tracking;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Payment > Payment Tracking (legacy FrmPaymentTracking, rebuilt on the PaymentTracking procedure): read only.</summary>
[Route("api/payment/tracking")]
[RequireAccess(AccessRight.PaymentTracking)]
public class PaymentTrackingController(PaymentTrackingService service) : ApiControllerBase
{
    /// <summary>The payments to hospitals or members that match, as a detail listing or a summary.</summary>
    [HttpGet]
    public async Task<ActionResult<PaymentTrackingResult>> Search([FromQuery] PaymentTrackingFilter filter) => Ok(await service.SearchAsync(filter));
}
