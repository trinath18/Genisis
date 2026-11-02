using Genisis.Api.Auth;
using Genisis.Api.ClaimBordx.Print;
using Genisis.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Claim Bordx > Print Bordx (legacy FrmPrintBordx, "Print Bordx").</summary>
[Route("api/claim-bordx/print-bordx")]
[RequireAccess(AccessRight.ClaimBordxPrint)]
public class ClaimBordxPrintController(PrintBordxService service) : ApiControllerBase
{
    /// <summary>CmdSearch_Click: the claims of the bordx in the range.</summary>
    [HttpGet]
    public async Task<ActionResult<PrintBordxSearchResult>> Search([FromQuery] PrintBordxFilter filter) => Ok(await service.SearchAsync(filter));

    /// <summary>The Print and Preview buttons: the detail or summary report; printing the detail report marks the bordx as printed.</summary>
    [HttpPost("print")]
    public async Task<ActionResult<PrintBordxDocument>> Print(PrintBordxRequest request) => Ok(await service.PrintAsync(request, UserCode));
}
