using System.Data;
using Dapper;
using Genisis.Api.Auth;
using Genisis.Api.Case;
using Genisis.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Controllers;

/// <summary>Case > Adjustment (legacy frmCaseAdjustmentNew + frmGLMenu, menu position 12). Lookups and code
/// searches are shared with /api/case/registration.</summary>
[ApiController]
[Route("api/case/adjustment")]
[RequireAccess(AccessRight.CaseAdjustment)]
public class CaseAdjustmentController(CaseAdjustmentService adjustment, CaseDocumentsService documents, CaseFilesService files, DbConnections db)
    : ControllerBase
{
    private string UserCode => User.Identity?.Name ?? "";
    private string UserName => User.FindFirst("name")?.Value ?? "";

    /// <summary>frmCASList (cmdSearch): find a case to load.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> CaseSearch([FromQuery] string q, [FromQuery] string by = "case", [FromQuery] string? plan = null)
    {
        q = (q ?? "").Trim();
        if (q.Length < 3) return BadRequest(new { message = "Enter at least 3 characters." });
        by = by.ToLowerInvariant();
        if (by is not ("case" or "member" or "policy" or "name" or "ic" or "employee" or "polsub"))
            return BadRequest(new { message = "by must be one of: case, member, policy, name, ic, employee, polsub." });
        await using var conn = db.MainDb();
        var rows = await conn.ProcQueryAsync<dynamic>("genisis.CaseAdj_CaseSearch",
            new { by, q = SqlText.EscapeLike(q), plan = string.IsNullOrWhiteSpace(plan) ? null : plan.Trim(), max = 200 });
        return Ok(RowMapper.ToDictionaries(rows));
    }

    /// <summary>Form_Load's cboPendingReason fill.</summary>
    [HttpGet("pending-reasons")]
    public async Task<IActionResult> PendingReasons()
    {
        await using var conn = db.MainDb();
        return Ok(await conn.QueryAsync<string>("genisis.CaseAdj_PendingReasons", commandType: CommandType.StoredProcedure));
    }

    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (CaseRegistrationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (SqlException ex) when (ex.Number is 8152 or 2628)
        {
            return BadRequest(new { message = "One of the values is longer than the database allows. Shorten it and try again." });
        }
    }

    [HttpGet("gl-options")]
    public IActionResult GLOptions() => Ok(new
    {
        purposes = CaseGLRules.Purposes, requestModes = CaseGLRules.RequestModes,
        requestBy = CaseGLRules.RequestBy, delayReasons = CaseGLRules.DelayReasons,
    });

    /// <summary>cmdShow_Click.</summary>
    [HttpGet("{cas}")]
    public async Task<IActionResult> Get(string cas) =>
        await adjustment.LoadAsync(cas, UserCode) is { } view ? Ok(view) : NotFound(new { message = "No record found!" });

    /// <summary>cmdPrev_Click / CmdNext_Click.</summary>
    [HttpGet("{cas}/neighbour")]
    public async Task<IActionResult> Neighbour(string cas, [FromQuery] bool next = true) =>
        await adjustment.NavigateAsync(cas, next) is { } found
            ? Ok(new { caseNumber = found })
            : NotFound(new { message = next ? " Last Record Reached! " : "First Record Reached! " });

    /// <summary>cmdUpdate1_Click.</summary>
    [HttpPut("{cas}")]
    public Task<IActionResult> Update(string cas, CaseAdjustmentRequest request) => Run(async () =>
    {
        request.CaseNumber = cas;
        if (CaseAdjustmentValidator.Validate(request) is { } error) return BadRequest(new { message = error });
        await adjustment.UpdateAsync(request, UserCode);
        return Ok(new { message = "Record Updated Sucessfully" });
    });

    /// <summary>showCAS "Do you want to change Claimability Status?" → NN.</summary>
    [HttpPost("{cas}/reset-claimability")]
    public Task<IActionResult> ResetClaimability(string cas) => Run(async () =>
    {
        await adjustment.ResetClaimabilityAsync(cas, UserCode);
        return Ok(new { message = "Status has been updated sucessfully" });
    });

    /// <summary>cmdRepudiate_Click: save the repudiation, mark the case declined and return the letter.</summary>
    [HttpPost("{cas}/repudiate")]
    public Task<IActionResult> Repudiate(string cas, CaseAdjustmentRequest request) => Run(async () =>
    {
        request.CaseNumber = cas;
        if (CaseAdjustmentValidator.ValidateRepudiation(request) is { } error) return BadRequest(new { message = error });
        await adjustment.RepudiateAsync(request, UserCode);
        return Ok(await documents.LetterAsync(new("R", cas, null, request), UserCode, UserName));
    });

    /// <summary>The letter buttons: D (Doc Disch), I (GP/QA), S (Reim Req), M (Mgmt Note), AUTO (Notify), R.</summary>
    [HttpPost("{cas}/letter/{letter}")]
    public Task<IActionResult> Letter(string cas, string letter, CaseAdjustmentRequest request) => Run(async () =>
    {
        request.CaseNumber = cas;
        var result = await documents.LetterAsync(new(letter, cas, null, request), UserCode, UserName);
        // cmdDocReqDis_Click also moves the case to "pending document from hospital".
        if (result.Letter == "D") await adjustment.SetProgressAsync(cas, "doc-discharge", UserCode);
        return Ok(result);
    });

    /// <summary>C. Ref AA / C. Ref RR / Clear Doc Record.</summary>
    [HttpPost("{cas}/progress/{action}")]
    public Task<IActionResult> Progress(string cas, string action) => Run(async () =>
        Ok(new { message = await adjustment.SetProgressAsync(cas, action, UserCode) }));

    [HttpGet("{cas}/lg")]
    public async Task<IActionResult> LgList(string cas) => Ok(await adjustment.LgListAsync(cas));

    /// <summary>cmdDocReqAdm_Click ("Delete" on the LG tab).</summary>
    [HttpDelete("{cas}/lg/{lgNo:int}")]
    public Task<IActionResult> DeleteLg(string cas, int lgNo) => Run(async () =>
    {
        await adjustment.DeleteLgAsync(cas, lgNo, UserCode);
        return Ok(new { message = "GL Deleted Successful..." });
    });

    /// <summary>frmGLMenu defaults: the request date / time already on record and the last GL purpose.</summary>
    [HttpGet("{cas}/gl")]
    public async Task<IActionResult> GLDefaults(string cas)
    {
        var (requestDate, requestTime, lastPurpose) = await documents.GLDefaultsAsync(cas);
        return Ok(new { requestDate, requestTime, lastPurpose });
    }

    /// <summary>CheckLimit before the GL is issued (the desktop then asks "Are you sure?" or "Please check LG Amount").</summary>
    [HttpPost("{cas}/gl/check")]
    public Task<IActionResult> CheckGL(string cas, CaseGLRequest request) => Run(async () =>
    {
        var (_, _, lastPurpose) = await documents.GLDefaultsAsync(cas);
        if (CaseGLRules.Validate(request, lastPurpose) is { } error) return BadRequest(new { message = error });
        return Ok(await documents.CheckGLAsync(cas, request.LgAmount));
    });

    [HttpPost("{cas}/gl")]
    public Task<IActionResult> IssueGL(string cas, CaseGLRequest request) => Run(async () =>
        Ok(await documents.IssueGLAsync(cas, request, UserCode, UserName)));

    [HttpGet("{cas}/worksheet")]
    public async Task<IActionResult> Worksheet(string cas) => Ok(await adjustment.WorksheetAsync(cas));

    /// <summary>SSTab1_Click on Case Mgmt Notes with an empty page 1.</summary>
    [HttpPost("{cas}/notes-template")]
    public Task<IActionResult> NotesTemplate(string cas, CaseAdjustmentRequest request) => Run(async () =>
    {
        request.CaseNumber = cas;
        return Ok(await adjustment.NotesTemplateAsync(request, UserCode));
    });

    // ---- Forms tab / policy wording (DocPath folders) ----

    [HttpGet("{cas}/documents")]
    public IActionResult Documents(string cas) =>
        files.Available ? Ok(files.ListCase(cas)) : Ok(new { unavailable = true });

    [HttpGet("{cas}/documents/{folder}/{name}")]
    public IActionResult Document(string cas, string folder, string name) =>
        files.CasePath(folder, cas, name) is { } path ? Download(path) : NotFound(new { message = "File not found." });

    /// <summary>cmdDelGL_Click: delete one document of the case.</summary>
    [HttpDelete("{cas}/documents/{folder}/{name}")]
    public Task<IActionResult> DeleteDocument(string cas, string folder, string name) => Run(() =>
        Task.FromResult<IActionResult>(files.DeleteCaseFile(folder, cas, name, UserCode)
            ? Ok(new { message = "File Deleted Successful..." })
            : NotFound(new { message = "File not found." })));

    /// <summary>Browse + Copy: store a document in the case's scanned documents.</summary>
    [HttpPost("{cas}/documents")]
    [RequestSizeLimit(CaseFilesService.MaxUploadBytes + 1024 * 1024)]
    public Task<IActionResult> Upload(string cas, IFormFile file) => Run(async () =>
        Ok(await files.SaveScannedAsync(cas, file, UserCode)));

    [HttpGet("policy-wording/{wording}")]
    public IActionResult Wording(string wording) => Ok(files.ListWording(wording));

    [HttpGet("policy-wording/{wording}/{name}")]
    public IActionResult WordingFile(string wording, string name) =>
        files.WordingPath(wording, name) is { } path ? Download(path) : NotFound(new { message = "File not found." });

    private PhysicalFileResult Download(string path)
    {
        if (!new FileExtensionContentTypeProvider().TryGetContentType(path, out var type)) type = "application/octet-stream";
        return PhysicalFile(path, type, Path.GetFileName(path), enableRangeProcessing: true);
    }
}
