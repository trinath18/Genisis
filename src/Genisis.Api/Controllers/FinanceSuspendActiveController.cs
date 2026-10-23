using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Finance.SuspendActive;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Finance &gt; Suspend / Active (legacy frmPaymentMBMReissue): stop and re-issue member payment vouchers.</summary>
[Route("api/finance/suspend-active")]
[RequireAccess(AccessRight.FinanceSuspendActive)]
public class FinanceSuspendActiveController(SuspendActiveService service) : ApiControllerBase
{
    /// <summary>PV statuses, stop statuses and payment modes.</summary>
    [HttpGet("lookups")]
    public ActionResult<SuspendActiveLookups> Lookups() => Ok(SuspendActiveService.Lookups());

    /// <summary>The member payment vouchers that match.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<VoucherSearchResult>> Search([FromQuery] VoucherFilter filter) => Ok(await service.SearchAsync(filter));

    /// <summary>Cancels (stopped cheque) or expires a voucher.</summary>
    [HttpPost("{id:int}/stop")]
    public async Task<ActionResult<object>> Stop(int id, StopVoucherRequest request) => Ok(new { message = await service.StopAsync(id, request, UserCode) });

    /// <summary>Re-issues a stopped voucher under a new PV number.</summary>
    [HttpPost("{id:int}/reissue")]
    public async Task<ActionResult<object>> Reissue(int id, ReissueVoucherRequest request)
    {
        var (pvNo, message) = await service.ReissueAsync(id, request, UserCode);
        return Ok(new { pvNo, message });
    }
}
