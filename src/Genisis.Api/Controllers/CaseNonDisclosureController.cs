using Genisis.Api.Auth;
using Genisis.Api.Case.Lookups;
using Genisis.Api.Case.NonDisclosure;
using Genisis.Api.Case.PanelHospitals;
using Genisis.Api.Case.PayClaim;
using Genisis.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Case > Non-Disclosure Info (legacy frmCASNonDisclosure, opened from Case Adjustment).</summary>
[Route("api/case/non-disclosure")]
[RequireAccess(AccessRight.CaseNonDisclosure)]
public class CaseNonDisclosureController(NonDisclosureService service) : ApiControllerBase
{
    /// <summary>CasDetails: the case's non-disclosure record.</summary>
    [HttpGet("{cas}")]
    public async Task<ActionResult<NonDisclosureView>> Get(string cas) => Ok(await service.GetAsync(cas));

    /// <summary>CmdUpdate_Click: saves the record.</summary>
    [HttpPut("{cas}")]
    public async Task<IActionResult> Save(string cas, NonDisclosureRequest request)
    {
        await service.SaveAsync(cas, request, UserCode);
        return Ok(new { message = "Record Updated Successfully" });
    }
}
