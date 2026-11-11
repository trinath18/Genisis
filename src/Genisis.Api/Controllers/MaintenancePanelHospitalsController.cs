using Genisis.Api.Auth;
using Genisis.Api.Case.PanelHospitals;
using Genisis.Api.Common;
using Genisis.Api.Maintenance.PanelHospitals;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Maintenance > Panel Hospital Maintenance (legacy FrmMaintenancePanelHos).</summary>
[Route("api/maintenance/panel-hospitals")]
[RequireAccess(AccessRight.PanelHospitalMaintenance)]
public class MaintenancePanelHospitalsController(PanelHospitalMaintenanceService service) : ApiControllerBase
{
    /// <summary>GetPanelHosCode: the hospital panel codes.</summary>
    [HttpGet("codes")]
    public async Task<ActionResult<List<PanelCode>>> Codes() => Ok(await service.GetCodesAsync());

    /// <summary>PanelListing: the hospitals of a panel code.</summary>
    [HttpGet]
    public async Task<ActionResult<List<PanelHospital>>> List([FromQuery] PanelHospitalMaintenanceQuery query) => Ok(await service.GetHospitalsAsync(query.Code!));

    /// <summary>HOSListing: the hospitals not yet in the panel.</summary>
    [HttpGet("available")]
    public async Task<ActionResult<List<AvailableHospital>>> Available([FromQuery] PanelHospitalMaintenanceQuery query) => Ok(await service.GetAvailableAsync(query.Code!));

    /// <summary>cmdUpdate_Click: adds and removes the hospitals of the panel.</summary>
    [HttpPut]
    public async Task<ActionResult<PanelHospitalMaintenanceResult>> Update(PanelHospitalMaintenanceRequest request) => Ok(await service.UpdateAsync(request, UserCode));
}
