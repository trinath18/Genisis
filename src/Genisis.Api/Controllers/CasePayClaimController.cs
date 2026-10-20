using Genisis.Api.Auth;
using Genisis.Api.Case.Lookups;
using Genisis.Api.Case.NonDisclosure;
using Genisis.Api.Case.PanelHospitals;
using Genisis.Api.Case.PayClaim;
using Genisis.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Case > Pay &amp; Claim (legacy frmCASPayClaim).</summary>
[Route("api/case/pay-claim")]
[RequireAccess(AccessRight.CasePayClaim)]
public class CasePayClaimController(PayClaimService service) : ApiControllerBase
{
    /// <summary>cmdShow_Click: the members, the case header and the existing Pay &amp; Claim record.</summary>
    [HttpGet("{cas}")]
    public async Task<ActionResult<PayClaimView>> Get(string cas) => Ok(await service.LoadAsync(cas));

    /// <summary>cmdSave_Click: Add (<c>isNew</c>) or Edit.</summary>
    [HttpPut("{cas}")]
    public async Task<IActionResult> Save(string cas, PayClaimRequest request)
    {
        await service.SaveAsync(cas, request, UserCode);
        return Ok(new { message = request.IsNew ? "Record Saved..." : "Record Updated..." });
    }
}
