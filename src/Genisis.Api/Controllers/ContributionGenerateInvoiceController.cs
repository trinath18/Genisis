using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.ContributionBilling.GenerateInvoice;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Contribution Billing > Generate Invoice (legacy frmMCOFeesListingNew, "Member MCO Fees").</summary>
[Route("api/contribution-billing/generate-invoice")]
[RequireAccess(AccessRight.ContributionInvoicing)]
public class ContributionGenerateInvoiceController(McoInvoiceService service) : ApiControllerBase
{
    /// <summary>cmdMCOSearch_Click: the members of a batch, range or invoice.</summary>
    [HttpGet]
    public async Task<ActionResult<McoInvoiceSearchResult>> Search([FromQuery] McoInvoiceFilter filter) => Ok(await service.SearchAsync(filter));

    /// <summary>cboMCOPayor_Click: the group companies of a payor.</summary>
    [HttpGet("group-companies")]
    public async Task<ActionResult<List<string>>> GroupCompanies([FromQuery] string? payor) => Ok(await service.GroupCompaniesAsync(payor));

    /// <summary>cmdMCOPrint_Click / cmdMCOPrintPreview_Click: Print gives the members an invoice number; both return the document to write.</summary>
    [HttpPost("print")]
    public async Task<ActionResult<McoInvoiceDocument>> Print(McoInvoicePrintRequest request) => Ok(await service.PrintAsync(request, UserCode));
}
