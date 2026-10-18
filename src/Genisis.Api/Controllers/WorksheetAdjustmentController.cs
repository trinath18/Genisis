using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Worksheet.Adjustment;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Worksheet > Worksheet Adjustment (legacy frmWorksheetAdj2013). A worksheet is shown through the Enquiry endpoints; this controller saves.</summary>
[Route("api/worksheet/adjustment")]
[RequireAccess(AccessRight.WorksheetAdjustment)]
public class WorksheetAdjustmentController(WorksheetAdjustmentService service) : ApiControllerBase
{
    /// <summary>The drop-down lists: banks, states and payment modes.</summary>
    [HttpGet("lookups")]
    public async Task<ActionResult<AdjustmentLookups>> Lookups() => Ok(await service.LookupsAsync());

    /// <summary>cmdUpdate_Click: saves the payment details and remarks of one worksheet version.</summary>
    [HttpPut("{cas}/{version:int}/payment")]
    public async Task<IActionResult> UpdatePayment(string cas, int version, UpdatePaymentRequest request)
    {
        await service.UpdatePaymentAsync(cas, version, request, UserCode);
        return Ok(new { message = "Record Save" });
    }
}
