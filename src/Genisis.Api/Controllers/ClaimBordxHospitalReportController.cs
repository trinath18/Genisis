using Genisis.Api.Auth;
using Genisis.Api.ClaimBordx.PaymentAdvice;
using Genisis.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Claim Bordx > Payment Advice > Hospital Report (legacy frmPayAdviceHosRpt, "Payment Advice- By Hospital"): read only.</summary>
[Route("api/claim-bordx/hospital-report")]
[RequireAccess(AccessRight.ClaimBordx)]
public class ClaimBordxHospitalReportController(HospitalReportService service) : ApiControllerBase
{
    /// <summary>CmdSearch_Click: the claims of the bordx in the range.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<HospitalReportSearchResult>> Search([FromQuery] HospitalReportFilter filter) => Ok(await service.SearchAsync(filter));

    /// <summary>CmdPrintSummary_Click: the payment advice sets of the selected bordx.</summary>
    [HttpGet("payment-advice")]
    public async Task<ActionResult<HospitalReportDocument>> PaymentAdvice([FromQuery] string? bordxNo) => Ok(await service.PaymentAdviceAsync(bordxNo));

    /// <summary>cmdCCRpt_Click: the cost allocation of the bordx in the range.</summary>
    [HttpGet("cost-centre")]
    public async Task<ActionResult<HospitalReportDocument>> CostCentre([FromQuery] HospitalReportFilter filter) => Ok(await service.CostCentreReportAsync(filter));
}
