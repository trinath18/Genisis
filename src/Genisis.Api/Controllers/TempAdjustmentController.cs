using Genisis.Api.Adjustment;
using Genisis.Api.Auth;
using Genisis.Api.Common.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>
/// Membership &gt; Temp MBM Adj (legacy FrmTempMBMAdj): whether a membership belongs to a temporary payor and may be opened. Changes
/// are saved through /api/membership/adjustment. The desktop menu item has no access check of its own, so it uses Adjustment's.
/// </summary>
[ApiController]
[Route("api/membership/temp-adjustment")]
[RequireAccess(AccessRight.MembershipAdjustment)]
public sealed class TempAdjustmentController(ITempAdjustmentService temp) : ControllerBase
{
    [HttpGet("{number}")]
    public async Task<ActionResult<TempMembershipCheck>> Check(string number, CancellationToken cancellationToken)
    {
        var check = await temp.CheckAsync(number, cancellationToken);
        if (check is null)
            return new ObjectResult(Problems.Create(HttpContext, StatusCodes.Status404NotFound, "Not found", AdjustmentMessages.NotFound))
            {
                StatusCode = StatusCodes.Status404NotFound,
            };
        return check.Temporary ? Ok(check) : this.Invalid(TempAdjustmentService.NotTemporary);
    }
}
