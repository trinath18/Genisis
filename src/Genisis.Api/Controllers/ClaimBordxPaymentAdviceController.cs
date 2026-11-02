using Genisis.Api.Auth;
using Genisis.Api.Case.Listing;
using Genisis.Api.ClaimBordx.PaymentAdvice;
using Genisis.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Claim Bordx > Payment Advice > Generate Bordx No (legacy frmPayAdviceHosp, "Payment Advice - Generate Bordx").</summary>
[Route("api/claim-bordx/payment-advice-bordx")]
[RequireAccess(AccessRight.ClaimBordx)]
public class ClaimBordxPaymentAdviceController(PayAdviceBordxService service) : ApiControllerBase
{
    /// <summary>CmdSearch_Click: the worksheets waiting for a payment advice bordx.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<PayAdviceRow>>> Search([FromQuery] PayAdviceFilter filter) => Ok(await service.SearchAsync(filter));

    /// <summary>lstWorksheetTrackList_ItemCheck's "scanned documents in system" test for the ticked cases.</summary>
    [HttpPost("documents")]
    public IActionResult Documents(DocumentsRequest request)
    {
        var (available, cases) = service.Documents(request.Cases);
        return Ok(new { available, cases });
    }

    /// <summary>CmdBordx_Click: one bordx for the ticked worksheets.</summary>
    [HttpPost("bordx")]
    public async Task<ActionResult<PayAdviceBordxResult>> Bordx(PayAdviceBordxRequest request) => Ok(await service.GenerateAsync(request, UserCode));
}
