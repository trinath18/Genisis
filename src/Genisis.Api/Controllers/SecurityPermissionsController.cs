using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Maintenance.Security;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>
/// Maintenance > Security > User Permission (frmUserPermissionMaintenance; access position 112): add or edit a user with their
/// LG ID, service provider, authorised status and menu rights. List, details, lookups and delete are under /api/maintenance/security/users.
/// </summary>
[Route("api/maintenance/security/permissions")]
[RequireAccess(AccessRight.SecurityUsers)]
public class SecurityPermissionsController(SecurityUserService service) : ApiControllerBase
{
    /// <summary>Add.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(PermissionUserRequest request)
    {
        await service.CreatePermissionAsync(request, UserCode);
        return NoContent();
    }

    /// <summary>Edit.</summary>
    [HttpPut("{code}")]
    public async Task<IActionResult> Update(string code, PermissionUserRequest request)
    {
        await service.UpdatePermissionAsync(code, request, UserCode);
        return NoContent();
    }
}
