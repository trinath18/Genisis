using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Worksheet.Calculation;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>
/// Worksheet > Worksheet Registration, calculation step (legacy frmWorksheetRegGST2015): a worksheet worked out from its invoices and the patient's plan, shown beside what the desktop
/// saved. Read-only. Worksheet Adjustment may use it too, to recalculate.
/// </summary>
[Route("api/worksheet/calculation")]
[RequireAccess(AccessRight.WorksheetRegistration, AccessRight.WorksheetAdjustment)]
public class WorksheetCalculationController(CalculationService service) : ApiControllerBase
{
    /// <summary>The calculation of a case's worksheet at a version (the latest when none is given).</summary>
    [HttpGet("{cas}")]
    public async Task<ActionResult<CalculationResult>> Get(string cas, [FromQuery] int? version) => Ok(await service.GetAsync(cas, version));
}
