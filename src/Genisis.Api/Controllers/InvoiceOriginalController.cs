using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Invoice;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Invoice > Original Invoice Update (legacy frmUpdateInvoice, "Update Invoice").</summary>
[Route("api/invoice/original")]
[RequireAccess(AccessRight.InvoiceOriginalUpdate)]
public class InvoiceOriginalController(InvoiceOriginalService service) : ApiControllerBase
{
    /// <summary>Form_Load: the hospitals the drop-down offers.</summary>
    [HttpGet("hospitals")]
    public async Task<ActionResult<List<InvoiceOption>>> Hospitals() => Ok(await service.HospitalsAsync());

    /// <summary>cmdShow_Click: the invoices of a case and worksheet version.</summary>
    [HttpGet]
    public async Task<ActionResult<List<OriginalInvoice>>> Find([FromQuery(Name = "case")] string? caseNo, [FromQuery] string? version) => Ok(await service.FindAsync(caseNo, version));

    /// <summary>cmdUpdate_Click: updates one invoice.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, OriginalInvoiceUpdate update)
    {
        await service.UpdateAsync(id, update, UserCode);
        return Ok(new { message = "Record Updated Sucessfully" });
    }
}
