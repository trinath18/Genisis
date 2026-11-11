using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Receipt.Others;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Receipt > Others (legacy FrmReceiptHOS, "Receipt (Others)").</summary>
[Route("api/receipt/others")]
[RequireAccess(AccessRight.ReceiptOthers)]
public class ReceiptOthersController(ReceiptOthersService service) : ApiControllerBase
{
    /// <summary>ComboListing: the hospitals.</summary>
    [HttpGet("hospitals")]
    public async Task<ActionResult<List<ReceiptHospital>>> Hospitals() => Ok(await service.HospitalsAsync());

    /// <summary>cmdSearch_Click: the receipts that match.</summary>
    [HttpGet]
    public async Task<ActionResult<ReceiptOthersResult>> Search([FromQuery] ReceiptOthersFilter filter) => Ok(await service.SearchAsync(filter));

    /// <summary>cmdSave_Click: adds a receipt.</summary>
    [HttpPost]
    public async Task<IActionResult> Add(ReceiptOthersRequest request) => Ok(new { id = await service.AddAsync(request, UserCode), message = "Record Saved..." });

    /// <summary>cmdUpdate_Click: changes a receipt.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ReceiptOthersUpdateRequest request)
    {
        await service.UpdateAsync(id, request, UserCode);
        return Ok(new { message = "Record Updated..." });
    }

    /// <summary>CmdDelete_Click: deletes a receipt.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await service.DeleteAsync(id, UserCode);
        return Ok(new { message = "Record Deleted..." });
    }
}
