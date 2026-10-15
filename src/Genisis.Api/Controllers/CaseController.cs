using System.Data;
using Dapper;
using Genisis.Api.Auth;
using Genisis.Api.Case;
using Genisis.Api.Data;
using Microsoft.AspNetCore.Mvc;
using static Genisis.Api.Data.RowMapper;

namespace Genisis.Api.Controllers;

/// <summary>Read-only port of VB6 frmCaseInquiry (Case > Enquiry) and FrmCaseTracking (Case > Tracking).</summary>
[ApiController]
[Route("api/case")]
public class CaseController(DbConnections db) : ControllerBase
{
    private const int MaxSearchRows = 200;
    private const int MaxTrackingRows = 2000;

    [RequireAccess(AccessRight.CaseEnquiry)]
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] string by = "number")
    {
        q = (q ?? "").Trim();
        if (q.Length < 2) return BadRequest(new { message = "Enter at least 2 characters." });

        by = by.ToLowerInvariant();
        if (by is not ("number" or "member" or "patient" or "ic" or "policy"))
            return BadRequest(new { message = "by must be one of: number, member, patient, ic, policy." });

        await using var conn = db.MainDb();
        var rows = await conn.ProcQueryAsync<dynamic>("genisis.CaseEnquiry_Search", new
        {
            max = MaxSearchRows,
            by,
            q,
            prefix = SqlText.EscapeLike(q) + "%",
            contains = "%" + SqlText.EscapeLike(q) + "%",
        });
        return Ok(ToDictionaries(rows));
    }

    /// <summary>Full case detail via the existing parameterized SearchCasAdjustment procedure.</summary>
    [RequireAccess(AccessRight.CaseEnquiry)]
    [HttpGet("{casNumber}")]
    public async Task<IActionResult> Get(string casNumber)
    {
        await using var conn = db.MainDb();
        var row = await conn.QueryFirstOrDefaultAsync(
            "SearchCasAdjustment", new { CASNumber = casNumber.Trim() }, commandType: CommandType.StoredProcedure);
        return row is null
            ? NotFound(new { message = "Case not found." })
            : Ok(ToDictionary(row));
    }

    /// <summary>Worksheet claim versions for the case (CaseEnquiryNotes over VIEW_WS_Listing, HISWorkSheet).</summary>
    [RequireAccess(AccessRight.CaseEnquiry)]
    [HttpGet("{casNumber}/claims")]
    public async Task<IActionResult> Claims(string casNumber)
    {
        await using var conn = db.WorkSheet();
        var rows = await conn.QueryAsync(
            "CaseEnquiryNotes", new { CasNumber = casNumber.Trim() }, commandType: CommandType.StoredProcedure);
        return Ok(ToDictionaries(rows));
    }

    /// <summary>Other cases registered for the same member.</summary>
    [RequireAccess(AccessRight.CaseEnquiry)]
    [HttpGet("{casNumber}/member-cases")]
    public async Task<IActionResult> MemberCases(string casNumber)
    {
        await using var conn = db.MainDb();
        var mbmNumber = await conn.ExecuteScalarAsync<string>(
            "SELECT MBMNumber FROM dbo.CAS WHERE CASNumber = @casNumber", new { casNumber = casNumber.Trim() });
        if (mbmNumber is null) return NotFound(new { message = "Case not found." });
        var rows = await conn.QueryAsync(
            "SearchCas", new { MBMNumber = mbmNumber.Trim() }, commandType: CommandType.StoredProcedure);
        return Ok(ToDictionaries(rows));
    }

    /// <summary>Case history for the patient's IC across memberships (existing SearchCasHistory).</summary>
    [RequireAccess(AccessRight.CaseEnquiry)]
    [HttpGet("{casNumber}/history")]
    public async Task<IActionResult> History(string casNumber)
    {
        await using var conn = db.MainDb();
        var ic = await conn.ExecuteScalarAsync<string>(
            "SELECT MBMIcBcPp FROM genisis.Maint_MBMCrossReference WHERE MBMNumber = " +
            "(SELECT MBMNumber FROM dbo.CAS WHERE CASNumber = @casNumber)",
            new { casNumber = casNumber.Trim() });
        if (ic is null) return NotFound(new { message = "Case not found." });
        var rows = await conn.QueryAsync(
            "SearchCasHistory", new { ICNumber = ic.Trim() }, commandType: CommandType.StoredProcedure);
        return Ok(ToDictionaries(rows));
    }

    /// <summary>First/final diagnoses and repudiation details (legacy Case_Inquiry modes 2, 5, 6).</summary>
    [RequireAccess(AccessRight.CaseEnquiry)]
    [HttpGet("{casNumber}/diagnoses")]
    public async Task<IActionResult> Diagnoses(string casNumber)
    {
        await using var conn = db.MainDb();
        using var grid = await conn.ProcMultipleAsync("genisis.CaseEnquiry_Diagnoses", new { casNumber = casNumber.Trim() });
        var first = await grid.ReadAsync();
        var final = await grid.ReadAsync();
        var repudiation = await grid.ReadAsync();
        return Ok(new
        {
            first = ToDictionaries(first),
            final = ToDictionaries(final),
            repudiation = ToDictionaries(repudiation),
        });
    }

    /// <summary>Dropdown lists for the enquiry / tracking filter panels.</summary>
    [RequireAccess(AccessRight.CaseEnquiry)]
    [HttpGet("lookups")]
    public async Task<IActionResult> Lookups()
    {
        await using var conn = db.MainDb();
        using var grid = await conn.ProcMultipleAsync("genisis.Case_Lookups");
        var hospitals = await grid.ReadAsync();
        var payors = await grid.ReadAsync();
        var repCodes = await grid.ReadAsync();
        var claimNatures = await grid.ReadAsync();
        var healthTypes = await grid.ReadAsync();
        var insuredTypes = await grid.ReadAsync();
        return Ok(new
        {
            hospitals = ToDictionaries(hospitals),
            payors = ToDictionaries(payors),
            repudiationCodes = ToDictionaries(repCodes),
            claimNatures = ToDictionaries(claimNatures),
            healthTypes = ToDictionaries(healthTypes),
            insuredTypes = ToDictionaries(insuredTypes),
        });
    }

    /// <summary>Case Tracking main list (legacy CaseTracking mode 1 over VIEW_CAS_TRACKING2).</summary>
    [RequireAccess(AccessRight.CaseTracking)]
    [HttpGet("tracking")]
    public async Task<IActionResult> Tracking([FromQuery] CaseTrackingFilter f)
    {
        await using var conn = db.MainDb();
        var rows = await conn.ProcQueryAsync<dynamic>("genisis.CaseTracking_List", Parameters(f, includeMax: true));
        return Ok(ToDictionaries(rows));
    }

    /// <summary>Case Tracking totals (legacy CaseTracking summary modes over VIEW_CAS_TRACKINGSUM).</summary>
    [RequireAccess(AccessRight.CaseTracking)]
    [HttpGet("tracking/summary")]
    public async Task<IActionResult> Summary([FromQuery] CaseTrackingFilter f)
    {
        await using var conn = db.MainDb();
        var row = await conn.ProcFirstOrDefaultAsync<dynamic>("genisis.CaseTracking_Summary", Parameters(f, includeMax: false));
        return Ok(row is null ? null : ToDictionary(row));
    }

    private static DynamicParameters Parameters(CaseTrackingFilter f, bool includeMax)
    {
        var p = new DynamicParameters();
        if (includeMax) p.Add("max", MaxTrackingRows);
        else p.Add("claimPending", Trim(f.ClaimPending)?.ToUpperInvariant());
        p.Add("payor", Trim(f.Payor));
        p.Add("caseFrom", Trim(f.CaseFrom)); p.Add("caseTo", Trim(f.CaseTo));
        p.Add("mbmFrom", Trim(f.MbmFrom)); p.Add("mbmTo", Trim(f.MbmTo));
        p.Add("regFrom", f.RegFrom); p.Add("regTo", f.RegTo);
        p.Add("batch", Trim(f.Batch));
        p.Add("patient", Like(f.Patient));
        p.Add("grp", Trim(f.Grp));
        p.Add("status", Trim(f.Status)?.ToUpperInvariant());
        p.Add("claimability", Trim(f.Claimability)?.ToUpperInvariant());
        p.Add("pending", Trim(f.Pending)?.ToUpperInvariant());
        p.Add("regBy", Trim(f.RegBy));
        p.Add("payType", Trim(f.PayType));
        p.Add("userToken", Trim(f.UserToken));
        p.Add("sex", Trim(f.Sex));
        p.Add("memberType", Trim(f.MemberType)?.ToLowerInvariant());
        p.Add("hltCode", Trim(f.HltCode));
        p.Add("insCode", Trim(f.InsCode));
        p.Add("renewFlag", Trim(f.RenewFlag)?.ToUpperInvariant());
        p.Add("takeover", Trim(f.Takeover));
        p.Add("agentCode", Like(f.AgentCode));
        p.Add("policyNo", Like(f.PolicyNo));
        p.Add("doctor", Like(f.Doctor));
        p.Add("repFrom", Trim(f.RepFrom)); p.Add("repTo", Trim(f.RepTo));
        p.Add("claimNatureFrom", Trim(f.ClaimNatureFrom)); p.Add("claimNatureTo", Trim(f.ClaimNatureTo));
        p.Add("hosName", Trim(f.HosName));
        p.Add("stayType", Trim(f.StayType));
        return p;
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Like(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : SqlText.EscapeLike(value.Trim());
}
