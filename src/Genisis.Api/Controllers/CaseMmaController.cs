using Genisis.Api.Auth;
using Genisis.Api.Case.Ascii;
using Genisis.Api.Case.AuditReview;
using Genisis.Api.Case.Listing;
using Genisis.Api.Case.Mma;
using Genisis.Api.Case.Transfer;
using Genisis.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Case > MMA/CPT (legacy frmMMA): the MMA procedure schedule and the fee look-up table.</summary>
[Route("api/case/mma-cpt")]
[RequireAccess(AccessRight.MmaCpt)]
public class CaseMmaController(MmaService service) : ApiControllerBase
{
    /// <summary>ProcListing: procedures matching the criteria.</summary>
    [HttpGet("procedures")]
    public async Task<ActionResult<List<MmaProcedure>>> Procedures([FromQuery] MmaProcedureQuery query) => Ok(await service.SearchProceduresAsync(query));

    /// <summary>SaveProc (add).</summary>
    [HttpPost("procedures")]
    public async Task<IActionResult> AddProcedure(MmaProcedureRequest request) =>
        Ok(new { index = await service.SaveProcedureAsync(null, request, UserCode), message = "Record Saved..." });

    /// <summary>SaveProc (edit).</summary>
    [HttpPut("procedures/{index:int}")]
    public async Task<IActionResult> UpdateProcedure(int index, MmaProcedureRequest request)
    {
        await service.SaveProcedureAsync(index, request, UserCode);
        return Ok(new { index, message = "Record Updated..." });
    }

    /// <summary>DeleteProc.</summary>
    [HttpDelete("procedures/{index:int}")]
    public async Task<IActionResult> DeleteProcedure(int index)
    {
        await service.DeleteProcedureAsync(index, UserCode);
        return Ok(new { message = "Record Deleted..." });
    }

    /// <summary>LookListing: fee rows matching the criteria.</summary>
    [HttpGet("fees")]
    public async Task<ActionResult<List<MmaFee>>> Fees([FromQuery] MmaFeeQuery query) => Ok(await service.SearchFeesAsync(query));

    /// <summary>SaveLook (add).</summary>
    [HttpPost("fees")]
    public async Task<IActionResult> AddFee(MmaFeeRequest request) =>
        Ok(new { index = await service.SaveFeeAsync(null, request, UserCode), message = "Record Saved..." });

    /// <summary>SaveLook (edit).</summary>
    [HttpPut("fees/{index:int}")]
    public async Task<IActionResult> UpdateFee(int index, MmaFeeRequest request)
    {
        await service.SaveFeeAsync(index, request, UserCode);
        return Ok(new { index, message = "Record Updated..." });
    }

    /// <summary>DeleteLook.</summary>
    [HttpDelete("fees/{index:int}")]
    public async Task<IActionResult> DeleteFee(int index)
    {
        await service.DeleteFeeAsync(index, UserCode);
        return Ok(new { message = "Record Deleted..." });
    }
}
