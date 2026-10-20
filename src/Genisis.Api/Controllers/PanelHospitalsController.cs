using Genisis.Api.Auth;
using Genisis.Api.Case.Lookups;
using Genisis.Api.Case.NonDisclosure;
using Genisis.Api.Case.PanelHospitals;
using Genisis.Api.Case.PayClaim;
using Genisis.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Case > Panel Hospitals (legacy frmPanelHospital).</summary>
[Route("api/case/panel-hospitals")]
[RequireAccess(AccessRight.PanelHospitals)]
public class PanelHospitalsController(PanelHospitalsService service) : ApiControllerBase
{
    /// <summary>GetPanelHosCode: the hospital panel codes.</summary>
    [HttpGet("codes")]
    public async Task<ActionResult<List<PanelCode>>> Codes() => Ok(await service.GetCodesAsync());

    /// <summary>PanelListing: the hospitals of a panel code (an empty list when the panel has none).</summary>
    [HttpGet]
    public async Task<ActionResult<List<PanelHospital>>> List([FromQuery] PanelHospitalsQuery query) => Ok(await service.GetHospitalsAsync(query.Code!));
}
