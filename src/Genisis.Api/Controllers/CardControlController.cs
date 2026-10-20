using Genisis.Api.Auth;
using Genisis.Api.CardControl;
using Genisis.Api.Common.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>
/// Membership &gt; Card Sending Mode (legacy frmMembershipCardControl). The desktop menu item has no access check of its own, so it
/// uses the Adjustment position like the other membership maintenance screens.
/// </summary>
[ApiController]
[Route("api/membership/card-control")]
[RequireAccess(AccessRight.MembershipAdjustment)]
public sealed class CardControlController(ICardControlService cards) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CardControlSearchResult>> Search([FromQuery] CardControlSearch search, CancellationToken cancellationToken)
    {
        if (CardControlValidator.ValidateSearch(search) is { } error) return this.Invalid(error);
        return Ok(await cards.SearchAsync(search, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<CardControlUpdateResult>> Update(CardControlUpdate update, CancellationToken cancellationToken)
    {
        if (CardControlValidator.ValidateUpdate(update) is { } error) return this.Invalid(error);
        return Ok(await cards.UpdateAsync(update, cancellationToken));
    }
}
