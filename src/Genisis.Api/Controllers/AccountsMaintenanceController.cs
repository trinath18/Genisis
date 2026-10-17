using Genisis.Api.Auth;
using Genisis.Api.Mma;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Maintenance &gt; Accounts (legacy frmMMA13thSchDesc): read-only MMA and 13th Schedule description per OPCS code.</summary>
[ApiController]
[Route("api/maintenance/accounts")]
[RequireAccess(AccessRight.AccountsMaintenance)]
public sealed class AccountsMaintenanceController(IMmaService mma) : ControllerBase
{
    /// <summary>direction: next, prev or find (first code at or after code). Returns row null with the desktop message at either end.</summary>
    [HttpGet("schedule")]
    public async Task<ActionResult<ScheduleMove>> Move(string? code, string direction = "next", CancellationToken cancellationToken = default) =>
        Ok(await mma.MoveScheduleAsync(code, MmaValidator.ParseDirection(direction), cancellationToken));
}
