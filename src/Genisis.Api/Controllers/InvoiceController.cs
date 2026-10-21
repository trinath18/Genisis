using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Invoice;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Invoice > Invoice Processing (legacy frmInvoice): a case's invoices and one invoice's amounts, read-only for now.</summary>
[Route("api/invoice")]
[RequireAccess(AccessRight.InvoiceProcessing)]
public class InvoiceController(InvoiceService service, InvoiceEntryService entry) : ApiControllerBase
{
    /// <summary>Form_Load: the form's drop-down lists.</summary>
    [HttpGet("lookups")]
    public async Task<ActionResult<InvoiceLookups>> Lookups() => Ok(await entry.LookupsAsync());

    /// <summary>The fields of the form: what to draw and what a request may carry.</summary>
    [HttpGet("fields")]
    public ActionResult<IReadOnlyList<InvoiceField>> Fields() => Ok(InvoiceFields.Form);

    /// <summary>The form's change handlers and getTotal: the totals of what is typed, without saving.</summary>
    [HttpPost("calculate")]
    public async Task<ActionResult<InvoiceCalculation>> Calculate(InvoiceForm form) => Ok(await entry.CalculateAsync(form));

    /// <summary>cmdUpdate_Click: adds an invoice, or changes the one named by Index. A question to ask comes back with nothing saved.</summary>
    [HttpPost]
    public async Task<ActionResult<InvoiceSaveResult>> Save(InvoiceForm form) => Ok(await entry.SaveAsync(form, UserCode));

    /// <summary>cmdEdit_Click: an invoice loaded to be changed.</summary>
    [HttpGet("{cas}/{index:int}/form")]
    public async Task<ActionResult<InvoiceFormData>> Form(string cas, int index) => Ok(await entry.FormAsync(cas, index));

    /// <summary>CmdRemove_Click: removes an invoice no worksheet has used.</summary>
    [HttpDelete("{cas}/{index:int}")]
    public async Task<IActionResult> Remove(string cas, int index)
    {
        await entry.RemoveAsync(cas, index);
        return NoContent();
    }

    /// <summary>cmdShow_Click: the case and its invoices.</summary>
    [HttpGet("{cas}")]
    public async Task<ActionResult<InvoiceCaseResult>> Case(string cas) => Ok(await service.GetCaseAsync(cas));

    /// <summary>lstInvoice_Click: one invoice with its amounts by worksheet row.</summary>
    [HttpGet("{cas}/{index:int}")]
    public async Task<ActionResult<InvoiceDetail>> Detail(string cas, int index) => Ok(await service.GetInvoiceAsync(cas, index));
}
