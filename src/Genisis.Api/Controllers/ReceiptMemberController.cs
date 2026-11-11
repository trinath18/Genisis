using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Receipt.Member;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Receipt > From Member (legacy FrmReceiptMember, "Receipt From Member").</summary>
[Route("api/receipt/from-member")]
[RequireAccess(AccessRight.ReceiptMember)]
public class ReceiptMemberController(ReceiptMemberService service) : ApiControllerBase
{
    /// <summary>cmdSearch_Click: the debit notes of members and their receipts.</summary>
    [HttpGet]
    public async Task<ActionResult<ReceiptMemberSearchResult>> Search([FromQuery] ReceiptMemberFilter filter) => Ok(await service.SearchAsync(filter, UserCode));

    /// <summary>cmdSave_Click: the receipt of the ticked claims.</summary>
    [HttpPost]
    public async Task<ActionResult> Save(ReceiptMemberSaveRequest request) => Ok(new { saved = await service.SaveAsync(request, UserCode) });

    /// <summary>lstReceiptMBM_Click (Edit): one receipt row for the entry boxes.</summary>
    [HttpGet("{receiptIndex:int}")]
    public async Task<ActionResult<ReceiptMemberReceipt>> Receipt(int receiptIndex) => Ok(await service.ReceiptAsync(receiptIndex));

    /// <summary>CmdUpdate_Click: changes one receipt row.</summary>
    [HttpPut]
    public async Task<ActionResult> Update(ReceiptMemberUpdateRequest request)
    {
        await service.UpdateAsync(request, UserCode);
        return NoContent();
    }

    /// <summary>cmdPrintPV_Click: the lines of the official receipt.</summary>
    [HttpGet("official-receipt")]
    public async Task<ActionResult<List<ReceiptMemberOfficialLine>>> OfficialReceipt([FromQuery] string? receiptNo) => Ok(await service.OfficialReceiptAsync(receiptNo));
}
