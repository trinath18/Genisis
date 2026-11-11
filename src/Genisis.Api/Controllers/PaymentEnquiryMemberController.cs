using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Payment.EnquiryMember;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Payment > Enquiry > Payment To Member (legacy FrmEnquiryPayMember).</summary>
[Route("api/payment/enquiry-member")]
[RequireAccess(AccessRight.PaymentEnquiry)]
public class PaymentEnquiryMemberController(PaymentEnquiryMemberService service) : ApiControllerBase
{
    /// <summary>ComboListing: the payors and health types.</summary>
    [HttpGet("lookups")]
    public async Task<ActionResult<PaymentMemberLookups>> Lookups() => Ok(await service.LookupsAsync());

    /// <summary>cmdSearch_Click: the payments, the reissued payments and the receipts that match.</summary>
    [HttpGet]
    public async Task<ActionResult<PaymentEnquiryMemberResult>> Search([FromQuery] PaymentEnquiryMemberFilter filter) => Ok(await service.SearchAsync(filter));
}
