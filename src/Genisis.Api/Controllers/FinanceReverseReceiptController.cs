using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Finance.ReverseReceipt;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Finance &gt; Reverse Receipt (legacy frmReverseReceipt; receipts are found with Receipt Enquiry's search).</summary>
[Route("api/finance/reverse-receipt")]
[RequireAccess(AccessRight.FinanceReverseReceipt)]
public class FinanceReverseReceiptController(ReverseReceiptService service) : ApiControllerBase
{
    /// <summary>Reverses a receipt with a new negative receipt.</summary>
    [HttpPost("{id:int}")]
    public async Task<ActionResult<ReverseReceiptResult>> Reverse(int id, ReverseReceiptRequest request) => Ok(await service.ReverseAsync(id, request, UserCode));
}
