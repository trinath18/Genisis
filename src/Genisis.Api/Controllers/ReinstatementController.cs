using Genisis.Api.Auth;
using Genisis.Api.Common.Errors;
using Genisis.Api.Reinstatement;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>
/// Membership &gt; Member Reinstatement (legacy frmMBMReintstatement). The desktop menu item has no access check of its own, so it
/// uses the Adjustment position: reinstating is an endorsement of an existing membership.
/// </summary>
[ApiController]
[Route("api/membership/reinstatement")]
[RequireAccess(AccessRight.MembershipAdjustment)]
public sealed class ReinstatementController(IReinstatementService reinstatements) : ControllerBase
{
    private ObjectResult MembershipNotFound() => new(Problems.Create(HttpContext, StatusCodes.Status404NotFound, "Not found", ReinstatementMessages.NotFound))
    {
        StatusCode = StatusCodes.Status404NotFound,
    };

    [HttpGet("{number}")]
    public async Task<ActionResult<ReinstatementView>> Get(string number, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(number)) return this.Invalid(ReinstatementMessages.EnterMembershipNumber);
        var view = await reinstatements.LoadAsync(number, cancellationToken);
        return view is null ? MembershipNotFound() : Ok(view);
    }

    [HttpPost("{number}")]
    public async Task<ActionResult<ReinstatementResult>> Reinstate(string number, ReinstatementRequest request, CancellationToken cancellationToken)
    {
        if (ReinstatementValidator.ValidateRequest(request) is { } error) return this.Invalid(error);
        var result = await reinstatements.ReinstateAsync(number, request, User.Identity?.Name ?? "", cancellationToken);
        return result is null ? MembershipNotFound() : Ok(result);
    }
}
