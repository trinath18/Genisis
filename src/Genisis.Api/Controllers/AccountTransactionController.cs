using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Maintenance.AccountTransaction;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Maintenance > Account Transaction (legacy frmMMASchedule, "MMA Schedule of Fees"; menu position 1).</summary>
[Route("api/maintenance/account-transaction")]
[RequireAccess(AccessRight.AccountTransaction)]
public class AccountTransactionController(AccountTransactionService service) : ApiControllerBase
{
    /// <summary>CmdSearch_Click: the schedule rows matching the criteria (all rows when none is given).</summary>
    [HttpGet]
    public async Task<ActionResult<AccountTransactionResult>> Search([FromQuery] AccountTransactionQuery query) => Ok(await service.SearchAsync(query));
}
