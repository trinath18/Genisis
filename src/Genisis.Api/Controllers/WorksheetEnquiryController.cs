using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Worksheet.Enquiry;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Worksheet > Worksheet Enquiry (legacy frmWorksheetEnq): a read-only view of a saved worksheet.</summary>
[Route("api/worksheet/enquiry")]
[RequireAccess(AccessRight.WorksheetEnquiry)]
public class WorksheetEnquiryController(EnquiryService service) : ApiControllerBase
{
    /// <summary>cmdSearchCase_Click: the cases that match the text.</summary>
    [HttpGet("cases")]
    public async Task<ActionResult<List<EnquiryCaseRow>>> Cases([FromQuery] EnquiryCaseSearch search) => Ok(await service.SearchCasesAsync(search));

    /// <summary>cmdDisplay_Click: the worksheet of a case at a version selection.</summary>
    [HttpGet("{cas}")]
    public async Task<ActionResult<EnquiryResult>> Get(string cas, [FromQuery] EnquiryQuery query) => Ok(await service.GetAsync(cas, query));
}
