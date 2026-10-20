using Genisis.Api.Auth;
using Genisis.Api.Case.Ascii;
using Genisis.Api.Case.AuditReview;
using Genisis.Api.Case.Listing;
using Genisis.Api.Case.Mma;
using Genisis.Api.Case.Transfer;
using Genisis.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Case > Case Audit Review (legacy frmCaseAuditReview).</summary>
[Route("api/case/audit-review")]
[RequireAccess(AccessRight.CaseAuditReview)]
public class CaseAuditReviewController(AuditReviewService service) : ApiControllerBase
{
    /// <summary>cmdShow_Click: the case, its audit record, payments and member.</summary>
    [HttpGet("{cas}")]
    public async Task<ActionResult<AuditReviewView>> Get(string cas) => Ok(await service.LoadAsync(cas, UserCode));

    /// <summary>cmdUpdate1_Click: saves the audit record.</summary>
    [HttpPut("{cas}")]
    public async Task<IActionResult> Update(string cas, AuditReviewRequest request)
    {
        await service.SaveAsync(cas, request, UserCode);
        return Ok(new { message = "Record Updated Sucessfully" });
    }
}
