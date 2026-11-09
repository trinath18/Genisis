using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Payment.Others;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Payment > Others (legacy FrmPaymentOthers; rebuilt: source missing).</summary>
[Route("api/payment/others")]
[RequireAccess(AccessRight.PaymentOthers)]
public class PaymentOthersController(PaymentOthersService service) : ApiControllerBase
{
    /// <summary>The vouchers of PaymentOthers that match.</summary>
    [HttpGet]
    public async Task<ActionResult<PaymentOthersResult>> Search([FromQuery] PaymentOthersFilter filter) => Ok(await service.SearchAsync(filter));

    /// <summary>Save: writes a voucher with the next PV number.</summary>
    [HttpPost]
    public async Task<ActionResult<PaymentOthersDone>> Create(PaymentOthersRequest request) => Ok(await service.CreateAsync(request, UserCode));
}
