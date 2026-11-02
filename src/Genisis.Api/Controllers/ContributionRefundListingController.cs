using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.ContributionBilling.RefundListing;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Contribution Billing > Refund Listing (legacy frmMCORefundListing2, "MCO Refund Listing"): read only.</summary>
[Route("api/contribution-billing/refund-listing")]
[RequireAccess(AccessRight.ContributionRefundListing)]
public class ContributionRefundListingController(RefundListingService service) : ApiControllerBase
{
    /// <summary>cmdSearch_Click: the MCO fees refunded to the members of a payor.</summary>
    [HttpGet]
    public async Task<ActionResult<RefundResult>> Search([FromQuery] RefundFilter filter) => Ok(await service.SearchAsync(filter));
}
