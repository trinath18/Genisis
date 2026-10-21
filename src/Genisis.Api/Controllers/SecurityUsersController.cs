using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Maintenance.Security;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>
/// Maintenance > Security > Role Management (frmUserMaintenance) and the user list, details, lookups and delete that User Permission
/// shares (access position 112). Passwords are never returned.
/// </summary>
[Route("api/maintenance/security/users")]
[RequireAccess(AccessRight.SecurityUsers)]
public class SecurityUsersController(SecurityUserService service) : ApiControllerBase
{
    /// <summary>USRListing / cmdSearch_Click (code contains <paramref name="search"/>).</summary>
    [HttpGet]
    public async Task<ActionResult<List<UserSummary>>> List([FromQuery] string? search) => Ok(await service.ListAsync(search));

    /// <summary>Departments, service providers and the access matrix.</summary>
    [HttpGet("lookups")]
    public async Task<ActionResult<SecurityLookups>> Lookups() => Ok(await service.LookupsAsync());

    /// <summary>lstUsersList_Click.</summary>
    [HttpGet("{code}")]
    public async Task<ActionResult<UserDetail>> Get(string code) => Ok(await service.GetAsync(code));

    /// <summary>Role Management, Add.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(RoleUserRequest request)
    {
        await service.CreateRoleAsync(request, UserCode);
        return NoContent();
    }

    /// <summary>Role Management, Edit.</summary>
    [HttpPut("{code}")]
    public async Task<IActionResult> Update(string code, RoleUserRequest request)
    {
        await service.UpdateRoleAsync(code, request, UserCode);
        return NoContent();
    }

    /// <summary>Delete (recorded in USRArchive first).</summary>
    [HttpDelete("{code}")]
    public async Task<IActionResult> Delete(string code)
    {
        await service.DeleteAsync(code, UserCode);
        return NoContent();
    }
}
