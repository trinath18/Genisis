using Genisis.Api.Auth;
using Genisis.Api.Case.Lookups;
using Genisis.Api.Case.NonDisclosure;
using Genisis.Api.Case.PanelHospitals;
using Genisis.Api.Case.PayClaim;
using Genisis.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>The payor drop-down of Inpatient Case Listing, Claim ASCII and Case ASCII.</summary>
[Route("api/case/payors")]
[RequireAccess(AccessRight.CaseListing, AccessRight.ClaimAscii)]
public class PayorsController(PayorLookupService service) : ApiControllerBase
{
    /// <summary>Every payor (PAY: code and company name).</summary>
    [HttpGet]
    public async Task<ActionResult<List<PayorOption>>> Payors() => Ok(await service.GetPayorsAsync());
}
