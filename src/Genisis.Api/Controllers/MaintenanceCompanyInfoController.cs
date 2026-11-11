using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Maintenance.CompanyInfo;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Maintenance > Company Info (legacy FrmMaintenanceComInfo, "Company Information").</summary>
[Route("api/maintenance/company-info")]
[RequireAccess(AccessRight.CompanyInfo)]
public class MaintenanceCompanyInfoController(CompanyInfoService service) : ApiControllerBase
{
    /// <summary>LoadCOMInfo: the saved details.</summary>
    [HttpGet]
    public async Task<ActionResult<CompanyInfoRow>> Get() => Ok(await service.GetAsync());

    /// <summary>cmdUpdate_Click: saves the details.</summary>
    [HttpPut]
    public async Task<IActionResult> Save(CompanyInfoRequest request)
    {
        await service.SaveAsync(request, UserCode);
        return Ok(new { message = "Record Saved..." });
    }
}
