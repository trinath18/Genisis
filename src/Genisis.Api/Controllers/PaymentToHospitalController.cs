using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Payment.ToHospital;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Payment > To Hospital (legacy FrmPaymentHospital; rebuilt: source missing).</summary>
[Route("api/payment/to-hospital")]
[RequireAccess(AccessRight.PaymentToHospital)]
public class PaymentToHospitalController(PaymentToHospitalService service) : ApiControllerBase
{
    /// <summary>The claims not yet paid to their hospital.</summary>
    [HttpGet]
    public async Task<ActionResult<PaymentToHospitalResult>> Search([FromQuery] PaymentToHospitalFilter filter) => Ok(await service.SearchAsync(filter));

    /// <summary>Generate PV: one voucher per hospital for the claims ticked.</summary>
    [HttpPost("vouchers")]
    public async Task<ActionResult<PaymentToHospitalDone>> Create(PaymentToHospitalRequest request) => Ok(await service.CreateAsync(request, UserCode));
}
