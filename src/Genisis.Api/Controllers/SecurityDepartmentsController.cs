using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Maintenance.Security;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Maintenance > Security > Department (frmGroupMaintenance; access position 111).</summary>
[Route("api/maintenance/security/departments")]
[RequireAccess(AccessRight.SecurityDepartment)]
public class SecurityDepartmentsController(DepartmentService service) : ApiControllerBase
{
    /// <summary>GRPListing.</summary>
    [HttpGet]
    public async Task<ActionResult<List<Department>>> List() => Ok(await service.ListAsync());

    /// <summary>Add.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(DepartmentRequest request)
    {
        await service.CreateAsync(request, UserCode);
        return NoContent();
    }

    /// <summary>Edit (the description; the code cannot change).</summary>
    [HttpPut("{code}")]
    public async Task<IActionResult> Update(string code, DepartmentRequest request)
    {
        await service.UpdateAsync(code, request, UserCode);
        return NoContent();
    }

    /// <summary>Delete; refused while users belong to the department.</summary>
    [HttpDelete("{code}")]
    public async Task<IActionResult> Delete(string code)
    {
        await service.DeleteAsync(code, UserCode);
        return NoContent();
    }
}
