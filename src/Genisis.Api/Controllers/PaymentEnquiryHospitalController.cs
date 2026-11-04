using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Payment.EnquiryHospital;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Payment > Enquiry > Payment To Hospital (legacy FrmEnquiryPaymentHospital).</summary>
[Route("api/payment/enquiry-hospital")]
[RequireAccess(AccessRight.PaymentEnquiry)]
public class PaymentEnquiryHospitalController(PaymentEnquiryHospitalService service) : ApiControllerBase
{
    /// <summary>ComboListing: the hospitals, payors and health types.</summary>
    [HttpGet("lookups")]
    public async Task<ActionResult<PaymentEnquiryLookups>> Lookups() => Ok(await service.LookupsAsync());

    /// <summary>cmdSearch_Click: the payments that match.</summary>
    [HttpGet]
    public async Task<ActionResult<PaymentEnquiryHospitalResult>> Search([FromQuery] PaymentEnquiryHospitalFilter filter) => Ok(await service.SearchAsync(filter));
}
