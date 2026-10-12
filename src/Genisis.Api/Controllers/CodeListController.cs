using Genisis.Api.Auth;
using Genisis.Api.Plans;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

/// <summary>Maintenance > Plan: Health Type, Insured Type, Product Category and Supp. MCO Status tabs.</summary>
[ApiController]
[Route("api/maintenance/code-lists/{list}")]
[RequireAccess(AccessRight.PlanMaintenance)]
public class CodeListController(CodeListService lists) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Search(string list, string? code) => Run(list, async l => Ok(await lists.SearchAsync(l, code)));

    [HttpPost]
    public Task<IActionResult> Create(string list, CodeRequest request) =>
        Run(list, async l => { await lists.SaveAsync(l, request, false, User.Identity?.Name ?? ""); return NoContent(); });

    [HttpPut("{code}")]
    public Task<IActionResult> Update(string list, string code, CodeRequest request)
    {
        request.Code = code;
        return Run(list, async l => { await lists.SaveAsync(l, request, true, User.Identity?.Name ?? ""); return NoContent(); });
    }

    [HttpDelete("{code}")]
    public Task<IActionResult> Delete(string list, string code) => Run(list, async l => { await lists.DeleteAsync(l, code); return NoContent(); });

    private async Task<IActionResult> Run(string list, Func<CodeList, Task<IActionResult>> action)
    {
        if (!CodeLists.All.TryGetValue(list, out var l)) return NotFound();
        try { return await action(l); }
        catch (PlanException ex) { return BadRequest(new { message = ex.Message }); }
    }
}

/// <summary>Maintenance > Plan, Special Note tab.</summary>
[ApiController]
[Route("api/maintenance/special-notes")]
[RequireAccess(AccessRight.PlanMaintenance)]
public class SpecialNoteController(SpecialNoteService notes) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(string? planCode, string? groupCompany)
    {
        var (rows, truncated) = await notes.SearchAsync(planCode, groupCompany);
        return Ok(new { rows, truncated, limit = SpecialNoteService.SearchLimit });
    }

    [HttpPut("{index:int}")]
    public Task<IActionResult> Update(int index, SpecialNoteRequest request) => Run(() => notes.SaveAsync(index, request.Note));

    /// <summary>Clears the note. The plan itself is kept (the desktop's Delete here removed the plan rows).</summary>
    [HttpDelete("{index:int}")]
    public Task<IActionResult> Delete(int index) => Run(() => notes.SaveAsync(index, null));

    private async Task<IActionResult> Run(Func<Task> action)
    {
        try { await action(); return NoContent(); }
        catch (PlanException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
