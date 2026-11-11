using Genisis.Api.Auth;
using Genisis.Api.Case.Lookups;
using Genisis.Api.Common;
using Genisis.Api.Invoice;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Invoice > Invoice Tracking (legacy FrmInvoiceTracking2).</summary>
[Route("api/invoice/tracking")]
[RequireAccess(AccessRight.InvoiceTracking)]
public class InvoiceTrackingController(InvoiceTrackingService service, PayorLookupService payors) : ApiControllerBase
{
    /// <summary>CmdSearch_Click: the rows of the chosen report.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<InvoiceTrackingResult>> Search([FromQuery] InvoiceTrackingFilter filter) => Ok(await service.SearchAsync(filter));

    /// <summary>ComboListing: every payor.</summary>
    [HttpGet("payors")]
    public async Task<ActionResult<List<PayorOption>>> Payors() => Ok(await payors.GetPayorsAsync());

    /// <summary>ComboListing: the hospitals of the drop-down.</summary>
    [HttpGet("hospitals")]
    public async Task<ActionResult<List<InvoiceOption>>> Hospitals() => Ok(await service.HospitalsAsync());

    /// <summary>ChkGrpCom_Click: the group companies that have worksheets for a payor.</summary>
    [HttpGet("group-companies")]
    public async Task<ActionResult<List<string>>> GroupCompanies([FromQuery] string? payor) => Ok(await service.GroupCompaniesAsync(payor));
}
