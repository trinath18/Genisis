using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Worksheet.Calculation;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Worksheet > Worksheet Registration (legacy frmWorksheetRegGST2015): the form for the next worksheet of a case, and its save.</summary>
[Route("api/worksheet/registration")]
[RequireAccess(AccessRight.WorksheetRegistration)]
public class WorksheetRegistrationController(RegistrationService service) : ApiControllerBase
{
    /// <summary>The case, its invoices, the worksheet worked out for its next version and what the save needs.</summary>
    [HttpGet("{cas}")]
    public async Task<ActionResult<RegistrationForm>> Prepare(string cas) => Ok(await service.PrepareAsync(cas));

    /// <summary>cmdApprove_Click: the checks, "Are you sure?" (nothing written), then the worksheet written in one transaction.</summary>
    [HttpPost("{cas}")]
    public async Task<ActionResult<RegistrationResult>> Save(string cas, RegistrationRequest request) => Ok(await service.SaveAsync(cas, request, UserCode));
}
