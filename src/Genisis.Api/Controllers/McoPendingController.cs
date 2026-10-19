using Genisis.Api.Auth;
using Genisis.Api.Common.Errors;
using Genisis.Api.McoPending;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>
/// Membership &gt; MCO Pending (legacy frmMBMMCOPending). The desktop menu item has no access check of its own, so it uses the
/// Adjustment position like the other membership maintenance screens.
/// </summary>
[ApiController]
[Route("api/membership/mco-pending")]
[RequireAccess(AccessRight.MembershipAdjustment)]
public sealed class McoPendingController(IMcoPendingService pending) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<McoPendingSearchResult>> Search([FromQuery] McoPendingSearch search, CancellationToken cancellationToken)
    {
        if (McoPendingValidator.ValidateSearch(search) is { } error) return this.Invalid(error);
        return Ok(await pending.SearchAsync(search, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<McoPendingUpdateResult>> Update(McoPendingUpdate update, CancellationToken cancellationToken)
    {
        if (McoPendingValidator.ValidateUpdate(update) is { } error) return this.Invalid(error);
        return Ok(await pending.UpdateAsync(update, User.Identity?.Name ?? "", cancellationToken));
    }
}
