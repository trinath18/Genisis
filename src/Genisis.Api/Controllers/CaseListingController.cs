using Genisis.Api.Auth;
using Genisis.Api.Case.Ascii;
using Genisis.Api.Case.AuditReview;
using Genisis.Api.Case.Listing;
using Genisis.Api.Case.Mma;
using Genisis.Api.Case.Transfer;
using Genisis.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Case > Inpatient Case Listing (legacy mnuAppCase -> FrmWksBordx, "Select Worksheets for Bordx").</summary>
[Route("api/case/listing")]
[RequireAccess(AccessRight.CaseListing)]
public class CaseListingController(ListingService service) : ApiControllerBase
{
    /// <summary>CmdSearch_Click: the worksheets waiting for a bordx.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<ListingRow>>> Search([FromQuery] ListingFilter filter) => Ok(await service.SearchAsync(filter));

    /// <summary>lstWorksheetTrackList_ItemCheck's "scanned documents in system" test for the ticked cases.</summary>
    [HttpPost("documents")]
    public IActionResult Documents(DocumentsRequest request)
    {
        var (available, cases) = service.Documents(request.Cases);
        return Ok(new { available, cases });
    }

    /// <summary>CmdBordx_Click: generates the bordx for the ticked worksheets.</summary>
    [HttpPost("bordx")]
    public async Task<IActionResult> Bordx(BordxRequest request) =>
        Ok(new { message = "Record Saved!", lines = await service.GenerateAsync(request, UserCode) });
}
