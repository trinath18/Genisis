using Genisis.Api.Auth;
using Genisis.Api.Case;
using Genisis.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using static Genisis.Api.Data.RowMapper;

namespace Genisis.Api.Controllers;

/// <summary>Case > Registration (legacy frmCaseAdmission, menu position 11).</summary>
[ApiController]
[Route("api/case/registration")]
// Lookups and searches are shared with Case > Adjustment; the writes below need Case Registration itself.
[RequireAccess(AccessRight.CaseRegistration, AccessRight.CaseAdjustment)]
public class CaseRegistrationController(DbConnections db, CaseRegistrationService registration, CaseDocumentsService documents) : ControllerBase
{
    private const int MaxSearchRows = 200;
    public record Option(string Code, string Name);

    [HttpGet("lookups")]
    public async Task<IActionResult> Lookups()
    {
        await using var conn = db.MainDb();
        using var grid = await conn.ProcMultipleAsync("genisis.CaseReg_Lookups");
        async Task<List<Option>> Read() => (await grid.ReadAsync<Option>()).ToList();
        var specialApprovals = await Read();
        var claimNatures = await Read();
        var payTypes = await Read();
        var stayTypes = await Read();
        var treatmentModes = await Read();
        var hospitals = await Read();
        var selections = await Read();
        return Ok(new
        {
            specialApprovals, claimNatures, payTypes, stayTypes, treatmentModes, hospitals,
            documents = selections.Where(s => s.Code.StartsWith('D')).ToList(),
            personnel = selections.Where(s => s.Code.StartsWith('P')).ToList(),
            attention = selections.Where(s => s.Code.StartsWith('A')).ToList(),
        });
    }

    /// <summary>The Search button (frmSearchMBM).</summary>
    [HttpGet("member-search")]
    public async Task<IActionResult> MemberSearch([FromQuery] string q, [FromQuery] string by = "number")
    {
        q = (q ?? "").Trim();
        if (q.Length < 3) return BadRequest(new { message = "Enter at least 3 characters." });
        by = by.ToLowerInvariant();
        if (by is not ("number" or "name" or "ic" or "policy"))
            return BadRequest(new { message = "by must be one of: number, name, ic, policy." });
        await using var conn = db.MainDb();
        var rows = await conn.ProcQueryAsync<dynamic>("genisis.Membership_Search",
            new { max = MaxSearchRows, by, q, prefix = SqlText.EscapeLike(q) + "%" });
        return Ok(ToDictionaries(rows));
    }

    /// <summary>The Go button: member header, patients, alerts and whether Register is allowed.</summary>
    [HttpGet("member/{mbmNumber}")]
    public async Task<IActionResult> Member(string mbmNumber) =>
        await registration.LoadMemberAsync(mbmNumber) is { } view ? Ok(view) : NotFound(new { message = "No record found!" });

    [HttpGet("member/{mbmNumber}/patient/{coverId}")]
    public async Task<IActionResult> Patient(string mbmNumber, string coverId)
    {
        await using var conn = db.MainDb();
        using var grid = await conn.ProcMultipleAsync("genisis.CaseReg_Patient", new { mbm = mbmNumber.Trim(), coverId = coverId.Trim() });
        var principal = ToDictionaries(await grid.ReadAsync()).FirstOrDefault();
        var covered = ToDictionaries(await grid.ReadAsync()).FirstOrDefault();
        var row = covered ?? principal;
        return row is null ? NotFound(new { message = "Sorry, No Record Found" }) : Ok(row);
    }

    [HttpGet("benefits")]
    public Task<IActionResult> Benefits([FromQuery] string plan, [FromQuery] string hlt, [FromQuery] string ins) =>
        Rows("genisis.CaseReg_Benefits", new { plan = T(plan), hlt = T(hlt), ins = T(ins) });

    [HttpGet("member-history")]
    public Task<IActionResult> MemberHistory([FromQuery] string ic) => Rows("genisis.CaseReg_MemberHistory", new { ic = T(ic) });

    [HttpGet("non-disclosure")]
    public Task<IActionResult> NonDisclosure([FromQuery] string ic) => Rows("genisis.CaseReg_NonDisclosure", new { ic = T(ic) });

    [HttpGet("policy-terms")]
    public async Task<IActionResult> PolicyTerms([FromQuery] string plan, [FromQuery] string pay, [FromQuery] string hlt)
    {
        await using var conn = db.MainDb();
        var row = await conn.ProcFirstOrDefaultAsync<dynamic>("genisis.CaseReg_PolicyTerms", new { plan = T(plan), pay = T(pay), hlt = T(hlt) });
        return Ok(row is null ? null : ToDictionary(row));
    }

    [HttpGet("surgical-schedule")]
    public Task<IActionResult> SurgicalSchedule([FromQuery] string pay, [FromQuery] string plan) =>
        Rows("genisis.CaseReg_SurgicalSchedule", new { pay = T(pay), plan = T(plan) });

    /// <summary>frmSearchICD.</summary>
    [HttpGet("icd")]
    public Task<IActionResult> Icd([FromQuery] string? code, [FromQuery] string? desc, [FromQuery] string? desc2, [FromQuery] bool any = false) =>
        Rows("genisis.CaseReg_IcdSearch", new { max = 500, code = N(code), desc = Like(desc), desc2 = Like(desc2), matchAny = any });

    /// <summary>FrmSearchRepudiation.</summary>
    [HttpGet("repudiation-codes")]
    public Task<IActionResult> RepudiationCodes([FromQuery] string? code, [FromQuery] string? desc, [FromQuery] string? desc2, [FromQuery] bool any = false) =>
        Rows("genisis.CaseReg_RepCodeSearch", new { max = 500, code = Like(code), desc = Like(desc), desc2 = Like(desc2), matchAny = any });

    /// <summary>frmSearchCPT / frmSearchInv: OPCS codes (investigations are ProcPart 'A').</summary>
    [HttpGet("procedures")]
    public Task<IActionResult> Procedures([FromQuery] string? code, [FromQuery] string? desc, [FromQuery] bool inv = false) =>
        Rows("genisis.CaseReg_OpcsSearch", new { max = 500, code = N(code), desc = Like(desc), part = inv ? "A" : null });

    /// <summary>SearchMMARate: fees shown next to an OPCS code.</summary>
    [HttpGet("mma/{code}")]
    public async Task<IActionResult> Mma(string code)
    {
        await using var conn = db.MainDb();
        var row = await conn.ProcFirstOrDefaultAsync<dynamic>("genisis.CaseReg_MmaRate", new { code = code.Trim() });
        return Ok(row is null ? new { SurgeonsFees = 0m, AnaesthetistsFees = 0m } : ToDictionary(row));
    }

    /// <summary>txtHOSCode_LostFocus: does the hospital need a typed name, and is it on the payor's panel.</summary>
    [HttpGet("hospital-check")]
    public async Task<IActionResult> HospitalCheck([FromQuery] string hospital, [FromQuery] string pay, [FromQuery] string? policy, [FromQuery] string? payType)
    {
        await using var conn = db.MainDb();
        var h = await registration.HospitalAsync(conn, hospital, pay, policy);
        if (h is null) return NotFound(new { message = "Please Enter Hospital Information" });
        return Ok(new { needsName = CaseRegistrationService.IsNamedHospital(h.HOSName), notOnPanel = CaseRegistrationService.NotOnPanel(h, payType) });
    }

    /// <summary>Every Register check without saving. Returns the flags for the desktop's confirmation questions
    /// (activated within 30 days, same-day admission).</summary>
    [RequireAccess(AccessRight.CaseRegistration)]
    [HttpPost("validate")]
    public Task<IActionResult> Validate(CaseRegistrationRequest request) =>
        Run(request, () => registration.ValidateAsync(request));

    /// <summary>The Register button.</summary>
    [RequireAccess(AccessRight.CaseRegistration)]
    [HttpPost]
    public Task<IActionResult> Register(CaseRegistrationRequest request) =>
        Run(request, () => registration.RegisterAsync(request, User.Identity?.Name ?? ""));

    /// <summary>funcword's letters: A/D = document requests, T = take-home drugs, AUTO = prior/expiry
    /// notification (cmdPrior), R = repudiation letter.</summary>
    [RequireAccess(AccessRight.CaseRegistration)]
    [HttpPost("letter")]
    public async Task<IActionResult> Letter(CaseLetterRequest request)
    {
        try
        {
            return Ok(await documents.LetterAsync(request, User.Identity?.Name ?? "", User.FindFirst("name")?.Value ?? ""));
        }
        catch (CaseRegistrationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>cmdLG_Click: balance check, LG sequence, CASLG insert and the letter fields.</summary>
    [RequireAccess(AccessRight.CaseRegistration)]
    [HttpPost("lg")]
    public async Task<IActionResult> Lg(CaseLgRequest request)
    {
        try
        {
            return Ok(await documents.IssueLGAsync(request, User.Identity?.Name ?? "", User.FindFirst("name")?.Value ?? ""));
        }
        catch (CaseRegistrationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<IActionResult> Run<T>(CaseRegistrationRequest request, Func<Task<T>> action)
    {
        if (CaseRegistrationValidator.Validate(request) is { } error) return BadRequest(new { message = error });
        try
        {
            return Ok(await action());
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

    private static string T(string? s) => (s ?? "").Trim();
    private static string? N(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static string? Like(string? s) => string.IsNullOrWhiteSpace(s) ? null : SqlText.EscapeLike(s.Trim());

    private async Task<IActionResult> Rows(string proc, object p)
    {
        await using var conn = db.MainDb();
        return Ok(ToDictionaries(await conn.ProcQueryAsync<dynamic>(proc, p)));
    }
}
