using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Maintenance.MmaEntry;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Maintenance > MMA (legacy frmMMAEntry, "Procedures/OPCS"; access position 147, the matrix's Maintenance MMA right).</summary>
[Route("api/maintenance/mma-entry")]
[RequireAccess(AccessRight.MmaEntry)]
public class MmaEntryController(MmaEntryService service) : ApiControllerBase
{
    /// <summary>The schedule rates of up to six OPCS codes and the fee totals.</summary>
    [HttpPost("calculate")]
    public async Task<ActionResult<MmaEntryResult>> Calculate(MmaEntryRequest request) => Ok(await service.CalculateAsync(request));
}
