using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Invoice;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Invoice > Invoice Processing (legacy frmInvoice): a case's invoices and one invoice's amounts, read-only for now.</summary>
[Route("api/invoice")]
[RequireAccess(AccessRight.InvoiceProcessing)]
public class InvoiceController(InvoiceService service) : ApiControllerBase
{
    /// <summary>cmdShow_Click: the case and its invoices.</summary>
    [HttpGet("{cas}")]
    public async Task<ActionResult<InvoiceCaseResult>> Case(string cas) => Ok(await service.GetCaseAsync(cas));

    /// <summary>lstInvoice_Click: one invoice with its amounts by worksheet row.</summary>
    [HttpGet("{cas}/{index:int}")]
    public async Task<ActionResult<InvoiceDetail>> Detail(string cas, int index) => Ok(await service.GetInvoiceAsync(cas, index));
}
