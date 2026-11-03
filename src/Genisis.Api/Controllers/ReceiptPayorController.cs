using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Receipt.Payor;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Receipt > Payor (legacy FrmReceiptPayor, "Receipt From Payor").</summary>
[Route("api/receipt/from-payor")]
[RequireAccess(AccessRight.ReceiptPayor)]
public class ReceiptPayorController(ReceiptPayorService service) : ApiControllerBase
{
    /// <summary>cmdSearch_Click: the bordx or the claims and what has been received.</summary>
    [HttpGet]
    public async Task<ActionResult<PayorSearchResult>> Search([FromQuery] ReceiptPayorFilter filter) => Ok(await service.SearchAsync(filter, UserCode));

    /// <summary>cboPayor_Click: the group companies of a payor.</summary>
    [HttpGet("group-companies")]
    public async Task<ActionResult<List<string>>> GroupCompanies([FromQuery] string? payor) => Ok(await service.GroupCompaniesAsync(payor));

    /// <summary>cmdSave_Click: one receipt for the ticked bordx or claims.</summary>
    [HttpPost]
    public async Task<ActionResult> Save(ReceiptPayorSaveRequest request)
    {
        var (receiptNo, saved) = await service.SaveAsync(request, UserCode);
        return Ok(new { receiptNo, saved });
    }

    /// <summary>CmdUpdate_Click: changes a receipt.</summary>
    [HttpPut]
    public async Task<ActionResult> Update(ReceiptPayorUpdateRequest request)
    {
        await service.UpdateAsync(request, UserCode);
        return NoContent();
    }

    /// <summary>cmdRemove_Click: removes the receipt rows of a bordx or of a claim.</summary>
    [HttpPost("remove")]
    public async Task<ActionResult> Remove(ReceiptPayorDeleteRequest request) => Ok(new { deleted = await service.DeleteAsync(request, UserCode) });

    /// <summary>cmdPrintPV_Click: the lines of the official receipt.</summary>
    [HttpGet("official-receipt")]
    public async Task<ActionResult<List<ReceiptPayorOfficialLine>>> OfficialReceipt([FromQuery] string? receiptNo) => Ok(await service.OfficialReceiptAsync(receiptNo));
}
