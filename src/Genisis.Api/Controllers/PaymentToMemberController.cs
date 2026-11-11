using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Payment.ToMember;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Payment > To Member > Pay2MBM and Pay2MBM(Auto) (legacy FrmPayMember and frmPayMemberJul08; rebuilt: sources missing).</summary>
[Route("api/payment/to-member")]
[RequireAccess(AccessRight.PaymentToMember)]
public class PaymentToMemberController(PaymentToMemberService service) : ApiControllerBase
{
    /// <summary>The claims not yet paid to or voided for the member.</summary>
    [HttpGet]
    public async Task<ActionResult<PaymentToMemberResult>> Search([FromQuery] PaymentToMemberFilter filter) => Ok(await service.SearchAsync(filter));

    /// <summary>Generate PV: one voucher per claim ticked.</summary>
    [HttpPost("vouchers")]
    public async Task<ActionResult<PaymentToMemberDone>> Create(PaymentToMemberRequest request) => Ok(await service.CreateAsync(request, UserCode));

    /// <summary>Void: closes the claims ticked without paying the member.</summary>
    [HttpPost("void")]
    public async Task<ActionResult<PaymentToMemberVoided>> Void(PaymentToMemberVoidRequest request) => Ok(await service.VoidAsync(request, UserCode));
}
