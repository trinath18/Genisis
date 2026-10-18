using Genisis.Api.Auth;
using Genisis.Api.Case.Lookups;
using Genisis.Api.Common;
using Genisis.Api.Worksheet.Lookups;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>The drop-downs of the Worksheet screens: payors and insured types.</summary>
[Route("api/worksheet/lookups")]
[RequireAccess(AccessRight.WorksheetTracking, AccessRight.WorksheetEnquiry, AccessRight.WorksheetRegistration, AccessRight.WorksheetAdjustment)]
public class WorksheetLookupsController(PayorLookupService payors, WorksheetLookupService lookups) : ApiControllerBase
{
    /// <summary>Every payor (PAY: code and company name).</summary>
    [HttpGet("payors")]
    public async Task<ActionResult<List<PayorOption>>> Payors() => Ok(await payors.GetPayorsAsync());

    /// <summary>Every insured type (INS: code and description).</summary>
    [HttpGet("insured-types")]
    public async Task<ActionResult<List<InsuredTypeOption>>> InsuredTypes() => Ok(await lookups.GetInsuredTypesAsync());
}
