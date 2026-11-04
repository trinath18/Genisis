using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Payment.ContraAllocation;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Payment > Contra Allocation (legacy frmContraAllocation).</summary>
[Route("api/payment/contra-allocation")]
[RequireAccess(AccessRight.PaymentContraAllocation)]
public class PaymentContraAllocationController(ContraAllocationService service) : ApiControllerBase
{
    /// <summary>cmdSearch_Click: the contra rows that match.</summary>
    [HttpGet]
    public async Task<ActionResult<ContraAllocationResult>> Search([FromQuery] ContraAllocationFilter filter) => Ok(await service.SearchAsync(filter));

    /// <summary>cmdUpdate_Click: allocates the rows ticked.</summary>
    [HttpPost("allocate")]
    public async Task<ActionResult<ContraAllocationDone>> Allocate(ContraAllocationRequest request) => Ok(await service.AllocateAsync(request, UserCode));
}
