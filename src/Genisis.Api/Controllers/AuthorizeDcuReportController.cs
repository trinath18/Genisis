using Genisis.Api.Auth;
using Genisis.Api.Authorize.Dcu;
using Genisis.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Authorize Claims > DCU Tracking > Claim Report (legacy FrmDCUReport1, "DCU Reports").</summary>
[Route("api/authorize/dcu-report")]
[RequireAccess(AccessRight.AuthorizeDcuReport)]
public class AuthorizeDcuReportController(DcuReportService service) : ApiControllerBase
{
    /// <summary>ComboListing: insured types, payors, policy statuses and the report types with their selections.</summary>
    [HttpGet("lookups")]
    public async Task<ActionResult<DcuLookups>> Lookups() => Ok(await service.LookupsAsync());

    /// <summary>cboPayorCode_Click: the group companies of a payor.</summary>
    [HttpGet("group-companies")]
    public async Task<ActionResult<List<string>>> GroupCompanies([FromQuery] string? payor) => Ok(await service.GroupCompaniesAsync(payor));

    /// <summary>cmdSearch_Click: the list of the report asked for.</summary>
    [HttpPost("run")]
    public async Task<ActionResult<DcuReportResult>> Run(DcuReportRequest request) => Ok(await service.RunAsync(request, UserCode));
}
