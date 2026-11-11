using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Payment.PvUpdate;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Payment > PV Update (Manual) (legacy FrmPaymentManual; rebuilt: source missing).</summary>
[Route("api/payment/pv-update")]
[RequireAccess(AccessRight.PaymentPvUpdate)]
public class PaymentPvUpdateController(PvUpdateService service) : ApiControllerBase
{
    /// <summary>The PVs that match.</summary>
    [HttpGet]
    public async Task<ActionResult<PvUpdateListResult>> Find([FromQuery] PvUpdateFilter filter) => Ok(await service.FindAsync(filter));

    /// <summary>The rows of a PV.</summary>
    [HttpGet("{source}/{pvNo}")]
    public async Task<ActionResult<PvUpdateDetail>> Rows(string source, string pvNo) => Ok(await service.RowsAsync(source, pvNo));

    /// <summary>Save: the details on every row of the PV.</summary>
    [HttpPut]
    public async Task<ActionResult<PvUpdateDone>> Save(PvUpdateRequest request) => Ok(await service.SaveAsync(request, UserCode));
}
