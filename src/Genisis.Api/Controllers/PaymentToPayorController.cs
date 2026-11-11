using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Payment.ToPayor;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Payment > Payor (legacy FrmPaymentPayor; rebuilt: source missing).</summary>
[Route("api/payment/to-payor")]
[RequireAccess(AccessRight.PaymentPayor)]
public class PaymentToPayorController(PaymentToPayorService service) : ApiControllerBase
{
    /// <summary>The reversed claims not yet paid back to their payor.</summary>
    [HttpGet]
    public async Task<ActionResult<PaymentToPayorResult>> Search([FromQuery] PaymentToPayorFilter filter) => Ok(await service.SearchAsync(filter));

    /// <summary>Generate PV: one voucher per payor for the claims ticked.</summary>
    [HttpPost("vouchers")]
    public async Task<ActionResult<PaymentToPayorDone>> Create(PaymentToPayorRequest request) => Ok(await service.CreateAsync(request, UserCode));
}
