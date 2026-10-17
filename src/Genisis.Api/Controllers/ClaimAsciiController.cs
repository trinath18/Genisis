using Genisis.Api.Auth;
using Genisis.Api.Case.Ascii;
using Genisis.Api.Case.AuditReview;
using Genisis.Api.Case.Listing;
using Genisis.Api.Case.Mma;
using Genisis.Api.Case.Transfer;
using Genisis.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Case > Claim ASCII (legacy frmNewClaimAscii).</summary>
[Route("api/case/claim-ascii")]
[RequireAccess(AccessRight.ClaimAscii)]
public class ClaimAsciiController(ClaimAsciiService service) : ApiControllerBase
{
    /// <summary>CmdGenerate_Click: builds the claim file on the server.</summary>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate(ClaimAsciiRequest request)
    {
        var result = await service.GenerateAsync(request, UserCode);
        return Ok(new { message = "File Generated Sucessfully...", result.FileName, result.Records, result.BatchNo });
    }

    /// <summary>Downloads a generated claim file.</summary>
    [HttpGet("files/{name}")]
    public IActionResult File(string name) =>
        service.FindFile(name) is { } path ? PhysicalFile(path, "application/octet-stream", name) : NotFound(new { message = "File not found." });
}
