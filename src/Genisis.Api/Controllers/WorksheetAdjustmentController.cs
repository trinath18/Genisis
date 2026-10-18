using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Worksheet.Adjustment;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Worksheet > Worksheet Adjustment (legacy frmWorksheetAdj2013). A worksheet is shown through the Enquiry endpoints; this controller saves.</summary>
[Route("api/worksheet/adjustment")]
[RequireAccess(AccessRight.WorksheetAdjustment)]
public class WorksheetAdjustmentController(WorksheetAdjustmentService service, WorksheetDocumentsService documents) : ApiControllerBase
{
    /// <summary>The drop-down lists: banks, states and payment modes.</summary>
    [HttpGet("lookups")]
    public async Task<ActionResult<AdjustmentLookups>> Lookups() => Ok(await service.LookupsAsync());

    /// <summary>The credit / debit note of a version (cmdCredit / cmdDebit): the kind follows the sign of the amount due. Nothing is written.</summary>
    [HttpGet("{cas}/{version:int}/note")]
    public async Task<ActionResult<WorksheetNote>> Note(string cas, int version) => Ok(await documents.NoteAsync(cas, version));

    /// <summary>cmdDebit_Click: the debit note of a version; a worksheet without a debit note number is given the next one of its payor.</summary>
    [HttpPost("{cas}/{version:int}/debit-note")]
    public async Task<ActionResult<WorksheetNote>> DebitNote(string cas, int version) => Ok(await documents.DebitNoteAsync(cas, version, UserCode));

    /// <summary>cmdExcLetter_Click: the Excess letter of a case; the case is marked as having had it.</summary>
    [HttpPost("{cas}/{version:int}/excess-letter")]
    public async Task<ActionResult<ExcessLetter>> ExcessLetterOf(string cas, int version) => Ok(await documents.ExcessLetterAsync(cas, version, UserCode));

    /// <summary>cmdUpdate_Click: saves the payment details and remarks of one worksheet version.</summary>
    [HttpPut("{cas}/{version:int}/payment")]
    public async Task<IActionResult> UpdatePayment(string cas, int version, UpdatePaymentRequest request)
    {
        await service.UpdatePaymentAsync(cas, version, request, UserCode);
        return Ok(new { message = "Record Save" });
    }
}
