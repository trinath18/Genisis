using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Receipt.Tracking;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Receipt > Receipt Tracking (legacy FrmReceiptTracking): read only.</summary>
[Route("api/receipt/tracking")]
[RequireAccess(AccessRight.ReceiptTracking)]
public class ReceiptTrackingController(ReceiptTrackingService service) : ApiControllerBase
{
    /// <summary>SearchRecord: the receipts that match, as a detail listing or a summary.</summary>
    [HttpGet]
    public async Task<ActionResult<ReceiptTrackingResult>> Search([FromQuery] ReceiptTrackingFilter filter) => Ok(await service.SearchAsync(filter));
}
