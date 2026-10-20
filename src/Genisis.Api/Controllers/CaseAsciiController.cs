using Genisis.Api.Auth;
using Genisis.Api.Case.Ascii;
using Genisis.Api.Case.AuditReview;
using Genisis.Api.Case.Listing;
using Genisis.Api.Case.Mma;
using Genisis.Api.Case.Transfer;
using Genisis.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Case > Case ASCII (legacy frmCaseASCII).</summary>
[Route("api/case/case-ascii")]
[RequireAccess(AccessRight.CaseAscii)]
public class CaseAsciiController(CaseAsciiService service) : ApiControllerBase
{
    /// <summary>CmdGenerate_Click: builds the case file on the server.</summary>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate(CaseAsciiRequest request)
    {
        var result = await service.GenerateAsync(request, UserCode);
        return Ok(new { message = "File Generated Sucessfully...", result.FileName, result.Records, result.BatchNo });
    }

    /// <summary>Downloads a generated case file.</summary>
    [HttpGet("files/{name}")]
    public IActionResult File(string name) =>
        service.FindFile(name) is { } path ? PhysicalFile(path, "application/octet-stream", name) : NotFound(new { message = "File not found." });
}
