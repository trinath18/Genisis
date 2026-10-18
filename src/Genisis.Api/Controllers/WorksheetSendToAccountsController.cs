using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Worksheet;
using Genisis.Api.Worksheet.SendToAccounts;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Worksheet > Worksheet Send to A/C (legacy FrmWksSendToAC, "Claims Send To A/C").</summary>
[Route("api/worksheet/send-to-accounts")]
[RequireAccess(AccessRight.WorksheetSendToAccounts)]
public class WorksheetSendToAccountsController(SendToAccountsService service) : ApiControllerBase
{
    /// <summary>CmdSearch_Click: the worksheets that match the criteria.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<SearchResult<SendToAccountsRow>>> Search([FromQuery] SendToAccountsFilter filter) => Ok(await service.SearchAsync(filter));

    /// <summary>CmdToAC_Click: sends the ticked worksheets to accounts.</summary>
    [HttpPost("send")]
    public async Task<IActionResult> Send(SendToAccountsRequest request) =>
        Ok(new { message = "Record updated...", updated = await service.SendAsync(request, UserCode) });
}
