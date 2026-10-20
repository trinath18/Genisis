using Genisis.Api.Auth;
using Genisis.Api.Case.Ascii;
using Genisis.Api.Case.AuditReview;
using Genisis.Api.Case.Listing;
using Genisis.Api.Case.Mma;
using Genisis.Api.Case.Transfer;
using Genisis.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Case > Case Transfer (legacy frmCaseTransferMBMNo, "Case - Transfer of Membership").</summary>
[Route("api/case/transfer")]
[RequireAccess(AccessRight.CaseTransfer)]
public class CaseTransferController(TransferService service) : ApiControllerBase
{
    /// <summary>DisplayTransMBMInfo: the member and patient to transfer to.</summary>
    [HttpGet("member")]
    public async Task<ActionResult<TransferMember>> Member([FromQuery] TransferMemberQuery query) => Ok(await service.LookupAsync(query.MemberNo!, query.CoverId!));

    /// <summary>CmdGo_Click: the case and the member it is on now.</summary>
    [HttpGet("{cas}")]
    public async Task<ActionResult<TransferView>> Get(string cas) => Ok(await service.LoadAsync(cas));

    /// <summary>cmdSave_Click ("Transfer").</summary>
    [HttpPost("{cas}")]
    public async Task<IActionResult> Transfer(string cas, TransferRequest request)
    {
        var result = await service.TransferAsync(cas, request, UserCode);
        return Ok(new { message = "Transferred successfully.", moved = result.Moved });
    }
}
