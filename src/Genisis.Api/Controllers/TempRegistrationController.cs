using Genisis.Api.Auth;
using Genisis.Api.Common.Errors;
using Genisis.Api.Registration;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>
/// Membership &gt; Temp MBM Reg (legacy FrmTempMBMReg). Loads a previous membership as a pre-filled renewal; the new membership is
/// saved with POST /api/membership/registration. The desktop menu item has no access check of its own, so it uses Registration's.
/// </summary>
[ApiController]
[Route("api/membership/temp-registration")]
[RequireAccess(AccessRight.MembershipRegistration)]
public sealed class TempRegistrationController(ITempRegistrationService renewals) : ControllerBase
{
    public const string NotFoundMessage = "No record found!";

    [HttpGet("{previousMembershipNo}")]
    public async Task<ActionResult<RenewalDraft>> GetDraft(string previousMembershipNo, CancellationToken cancellationToken)
    {
        var draft = await renewals.GetDraftAsync(previousMembershipNo, cancellationToken);
        return draft is null
            ? new ObjectResult(Problems.Create(HttpContext, StatusCodes.Status404NotFound, "Not found", NotFoundMessage)) { StatusCode = StatusCodes.Status404NotFound }
            : Ok(draft);
    }
}
