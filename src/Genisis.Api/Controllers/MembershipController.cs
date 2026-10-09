using System.Data;
using Dapper;
using Genisis.Api.Auth;
using Genisis.Api.Data;
using Microsoft.AspNetCore.Mvc;
using static Genisis.Api.Data.RowMapper;

namespace Genisis.Api.Controllers;

/// <summary>Read-only port of VB6 frmNewMBMEnquiry / frmSearchMBM05 (Membership > Enquiry).</summary>
[ApiController]
[Route("api/membership")]
[RequireAccess(AccessRight.MembershipEnquiry)]
public class MembershipController(DbConnections db) : ControllerBase
{
    private const int MaxSearchRows = 200;

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] string by = "number")
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

    [HttpGet("{mbmNumber}")]
    public async Task<IActionResult> Get(string mbmNumber)
    {
        mbmNumber = mbmNumber.Trim();
        await using var conn = db.MainDb();
        var rows = ToDictionaries(await conn.QueryAsync(
            "SearchMBMCovPersonsProc", new { MBMNumber = mbmNumber }, commandType: CommandType.StoredProcedure));
        if (rows.Count == 0) return NotFound(new { message = "Membership not found." });

        var principal = rows[0];
        var covered = rows
            .Where(r => r.TryGetValue("MBMCCoverID", out var id) && id is string s && s.Length > 0)
            .GroupBy(r => (string)r["MBMCCoverID"]!)
            .Select(g => g.First())
            .ToList();

        using var grid = await conn.ProcMultipleAsync("genisis.Membership_Details", new
        {
            mbmNumber,
            plnCode = Str(principal, "PLNCode"),
            insCode = Str(principal, "INSCode"),
            hltCode = Str(principal, "HLTCode"),
            effDate = principal.GetValueOrDefault("MBMPayorEffDate") as DateTime? ?? DateTime.Today,
        });
        var plan = await grid.ReadFirstOrDefaultAsync();
        var annualLimit = await grid.ReadFirstOrDefaultAsync();
        var clinic = await grid.ReadAsync();
        var benefitLimits = await grid.ReadAsync();

        return Ok(new
        {
            principal,
            coveredPersons = covered,
            plan = plan is null ? null : ToDictionary(plan),
            annualLimit = annualLimit is null ? null : ToDictionary(annualLimit),
            clinic = ToDictionaries(clinic),
            benefitLimits = ToDictionaries(benefitLimits),
        });
    }

    [HttpGet("{mbmNumber}/adjustments")]
    public async Task<IActionResult> Adjustments(string mbmNumber)
    {
        await using var conn = db.MainDb();
        var rows = await conn.ProcQueryAsync<dynamic>("genisis.Membership_Adjustments", new { mbmNumber = mbmNumber.Trim() });
        return Ok(ToDictionaries(rows));
    }

    [HttpGet("{mbmNumber}/cases")]
    public async Task<IActionResult> Cases(string mbmNumber)
    {
        await using var conn = db.MainDb();
        var rows = await conn.QueryAsync(
            "SearchCas", new { MBMNumber = mbmNumber.Trim() }, commandType: CommandType.StoredProcedure);
        return Ok(ToDictionaries(rows));
    }

    [HttpGet("{mbmNumber}/case-history")]
    public async Task<IActionResult> CaseHistory(string mbmNumber)
    {
        await using var conn = db.MainDb();
        var ic = await GetIc(conn, mbmNumber);
        if (ic is null) return NotFound(new { message = "Membership not found." });
        var rows = await conn.QueryAsync(
            "SearchCasHistory", new { ICNumber = ic }, commandType: CommandType.StoredProcedure);
        return Ok(ToDictionaries(rows));
    }

    [HttpGet("{mbmNumber}/member-history")]
    public async Task<IActionResult> MemberHistory(string mbmNumber)
    {
        await using var conn = db.MainDb();
        var ic = await GetIc(conn, mbmNumber);
        if (ic is null) return NotFound(new { message = "Membership not found." });
        using var grid = await conn.ProcMultipleAsync("genisis.Membership_History", new { ic });
        var principal = await grid.ReadAsync();
        var supplementary = await grid.ReadAsync();

        return Ok(new { principal = ToDictionaries(principal), supplementary = ToDictionaries(supplementary) });
    }

    [HttpGet("{mbmNumber}/account")]
    public async Task<IActionResult> Account(string mbmNumber)
    {
        await using var conn = db.MainDb();
        var rows = await conn.ProcQueryAsync<dynamic>("genisis.Membership_Account", new { mbmNumber = mbmNumber.Trim() });
        return Ok(ToDictionaries(rows));
    }

    [HttpGet("{mbmNumber}/notes")]
    public async Task<IActionResult> Notes(string mbmNumber)
    {
        await using var conn = db.MainDb();
        var ic = await GetIc(conn, mbmNumber);
        if (ic is null) return NotFound(new { message = "Membership not found." });
        using var grid = await conn.ProcMultipleAsync("genisis.Membership_Notes", new { ic });
        var exclusions = await grid.ReadAsync();
        var remarks = await grid.ReadAsync();
        return Ok(new { exclusions = ToDictionaries(exclusions), remarks = ToDictionaries(remarks) });
    }

    private static Task<string?> GetIc(Microsoft.Data.SqlClient.SqlConnection conn, string mbmNumber) =>
        conn.ProcScalarAsync<string?>("genisis.Membership_GetIc", new { mbmNumber = mbmNumber.Trim() });

    private static string? Str(Dictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var v) ? v?.ToString() : null;

}
