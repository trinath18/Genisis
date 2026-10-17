using Genisis.Api.Auth;
using Genisis.Api.Common.Errors;
using Genisis.Api.Mma;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Maintenance &gt; Entity (legacy frmMMA): Procedures and Look Up Table tabs.</summary>
[ApiController]
[Route("api/maintenance/entity")]
[RequireAccess(AccessRight.EntityMaintenance)]
public sealed class EntityMaintenanceController(IMmaService mma) : ControllerBase
{
    private string UserCode => User.Identity?.Name ?? "";

    [HttpGet("procedures")]
    public async Task<ActionResult<MmaProcedureSearchResult>> SearchProcedures(string? bodyPart, string? code, CancellationToken cancellationToken) =>
        Ok(await mma.SearchProceduresAsync(bodyPart, code, cancellationToken));

    [HttpPost("procedures")]
    public async Task<IActionResult> CreateProcedure(MmaProcedureRequest request, CancellationToken cancellationToken)
    {
        if (MmaValidator.Validate(request) is { } error) return this.Invalid(error);
        return StatusCode(StatusCodes.Status201Created, new { index = await mma.SaveProcedureAsync(null, request, UserCode, cancellationToken) });
    }

    [HttpPut("procedures/{index:int}")]
    public async Task<IActionResult> UpdateProcedure(int index, MmaProcedureRequest request, CancellationToken cancellationToken)
    {
        if (MmaValidator.Validate(request) is { } error) return this.Invalid(error);
        await mma.SaveProcedureAsync(index, request, UserCode, cancellationToken);
        return NoContent();
    }

    [HttpDelete("procedures/{index:int}")]
    public async Task<IActionResult> DeleteProcedure(int index, CancellationToken cancellationToken)
    {
        await mma.DeleteProcedureAsync(index, cancellationToken);
        return NoContent();
    }

    [HttpGet("fees")]
    public async Task<ActionResult<List<MmaFeeRow>>> SearchFees(string? surgeonCategory, string? anaesthetistCategory, CancellationToken cancellationToken) =>
        Ok(await mma.SearchFeesAsync(surgeonCategory, anaesthetistCategory, cancellationToken));

    [HttpPost("fees")]
    public async Task<IActionResult> CreateFee(MmaFeeRequest request, CancellationToken cancellationToken)
    {
        if (MmaValidator.Validate(request) is { } error) return this.Invalid(error);
        return StatusCode(StatusCodes.Status201Created, new { index = await mma.SaveFeeAsync(null, request, UserCode, cancellationToken) });
    }

    [HttpPut("fees/{index:int}")]
    public async Task<IActionResult> UpdateFee(int index, MmaFeeRequest request, CancellationToken cancellationToken)
    {
        if (MmaValidator.Validate(request) is { } error) return this.Invalid(error);
        await mma.SaveFeeAsync(index, request, UserCode, cancellationToken);
        return NoContent();
    }

    [HttpDelete("fees/{index:int}")]
    public async Task<IActionResult> DeleteFee(int index, CancellationToken cancellationToken)
    {
        await mma.DeleteFeeAsync(index, cancellationToken);
        return NoContent();
    }
}
