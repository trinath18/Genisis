using Genisis.Api.Auth;
using Genisis.Api.Common.Errors;
using Genisis.Api.Deletion;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>
/// Membership &gt; Deletion (legacy frmDeleteMembers). The desktop menu item has no access check of its own; it uses the Adjustment
/// position, as agreed for this migration.
/// </summary>
[ApiController]
[Route("api/membership/deletion")]
[RequireAccess(AccessRight.MembershipAdjustment)]
public sealed class DeletionController(IDeletionService deletion) : ControllerBase
{
    [HttpGet("lookups")]
    public async Task<ActionResult<DeletionLookups>> GetLookups(CancellationToken cancellationToken) => Ok(await deletion.GetLookupsAsync(cancellationToken));

    /// <summary>The desktop Go button: what Delete would remove.</summary>
    [HttpPost("list")]
    public async Task<ActionResult<DeletionListResult>> List(DeletionCriteria criteria, CancellationToken cancellationToken)
    {
        if (DeletionValidator.Validate(criteria) is { } error) return this.Invalid(error);
        return Ok(await deletion.ListAsync(criteria, cancellationToken));
    }

    [HttpPost("delete")]
    public async Task<ActionResult<DeletionResult>> Delete(DeletionCriteria criteria, CancellationToken cancellationToken)
    {
        if (DeletionValidator.Validate(criteria) is { } error) return this.Invalid(error);
        return Ok(await deletion.DeleteAsync(criteria, cancellationToken));
    }
}
