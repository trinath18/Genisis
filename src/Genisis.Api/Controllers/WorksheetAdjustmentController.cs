using Genisis.Api.Auth;
using Genisis.Api.Case;
using Genisis.Api.Common;
using Genisis.Api.Worksheet.Adjustment;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Worksheet > Worksheet Adjustment (legacy frmWorksheetAdj2013). A worksheet is shown through the Enquiry endpoints; this controller saves.</summary>
[Route("api/worksheet/adjustment")]
[RequireAccess(AccessRight.WorksheetAdjustment)]
public class WorksheetAdjustmentController(WorksheetAdjustmentService service, WorksheetDocumentsService documents, FinalLgService finalLg) : ApiControllerBase
{
    private string UserName => User.FindFirst("name")?.Value ?? "";

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

    /// <summary>frmGLMenu's drop-down lists, for Final LG.</summary>
    [HttpGet("gl-options")]
    public IActionResult GlOptions() => Ok(new
    {
        purposes = CaseGLRules.Purposes, requestModes = CaseGLRules.RequestModes, requestBy = CaseGLRules.RequestBy, delayReasons = CaseGLRules.DelayReasons,
    });

    /// <summary>frmGLMenu defaults for Final LG: the request date and time on record and the last GL purpose.</summary>
    [HttpGet("{cas}/gl")]
    public async Task<IActionResult> GlDefaults(string cas) => Ok(await finalLg.DefaultsAsync(cas));

    /// <summary>cmdFinalLG_Click's checks before the letter: take-over clause, member, patient, LG amount against the worksheet and the limit.</summary>
    [HttpPost("{cas}/{version:int}/final-lg/check")]
    public async Task<ActionResult<FinalLgCheck>> FinalLgCheck(string cas, int version, FinalLgRequest request) => Ok(await finalLg.CheckAsync(cas, version, request));

    /// <summary>cmdFinalLG_Click: issues the Final LG of the worksheet's case.</summary>
    [HttpPost("{cas}/{version:int}/final-lg")]
    public async Task<ActionResult<CaseGLResult>> FinalLg(string cas, int version, FinalLgRequest request) => Ok(await finalLg.IssueAsync(cas, version, request, UserCode, UserName));

    /// <summary>cmdUpdate_Click: saves the payment details and remarks of one worksheet version.</summary>
    [HttpPut("{cas}/{version:int}/payment")]
    public async Task<IActionResult> UpdatePayment(string cas, int version, UpdatePaymentRequest request)
    {
        await service.UpdatePaymentAsync(cas, version, request, UserCode);
        return Ok(new { message = "Record Save" });
    }
}
