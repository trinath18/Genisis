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

    private const string SearchSelect = """
        SELECT TOP (@max) m.MBMNumber, m.MBMPolicyNo, m.MBMCOVID, m.MBMIcBcPp, m.MBMName,
               m.MBMPayorEffDate, m.MBMPayorExpDate, m.MBMValueDate, m.MBMBordxDate, m.MBMDataStatus,
               CASE WHEN m.MBMStatus = 'C' AND m.MBMPayorEffDate > CAST(GETDATE() AS date) AND t.MBMCancelDate IS NULL
                    THEN 'A' ELSE m.MBMStatus END AS MBMStatus,
               t.MBMRegUser, o.MBMGroupCompany, m.HSCLNInd
        FROM dbo.MBM m
        INNER JOIN dbo.MBMOthers o ON m.MBMNumber = o.MBMNumber
        INNER JOIN dbo.MBMTwo t ON m.MBMNumber = t.MBMNumber
        """;

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] string by = "number")
    {
        q = (q ?? "").Trim();
        if (q.Length < 3) return BadRequest(new { message = "Enter at least 3 characters." });

        var where = by.ToLowerInvariant() switch
        {
            "number" => "m.MBMNumber LIKE @prefix",
            "name" => "m.MBMName LIKE @prefix",
            "ic" => "(m.MBMIcBcPp = @q OR REPLACE(m.MBMIcBcPp, '-', '') = REPLACE(@q, '-', ''))",
            "policy" => "m.MBMPolicyNo LIKE @prefix",
            _ => null,
        };
        if (where is null) return BadRequest(new { message = "by must be one of: number, name, ic, policy." });

        await using var conn = db.MainDb();
        var rows = await conn.QueryAsync(
            $"{SearchSelect} WHERE {where} ORDER BY m.MBMNumber",
            new { max = MaxSearchRows, q, prefix = EscapeLike(q) + "%" });
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

        var plan = await conn.QueryFirstOrDefaultAsync(
            "SELECT PLNCode, PLNDescription, ProductName, PLNRBType, MessagePromt, PLNPolicyWording FROM PLN WHERE PLNCode = @plnCode",
            new { plnCode = Str(principal, "PLNCode") });

        var annualLimit = await conn.QueryFirstOrDefaultAsync(
            """
            SELECT TOP (1) AnnLimit, DisabilityLmt, SuppLimitStatus, MajorMedicalLmt, AnnualEffDate
            FROM AnnualLimit
            WHERE PLNCode = @plnCode AND INSCode = @insCode AND HLTCode = @hltCode AND AnnualEffDate <= @effDate
            ORDER BY AnnualEffDate DESC
            """,
            new
            {
                plnCode = Str(principal, "PLNCode"),
                insCode = Str(principal, "INSCode"),
                hltCode = Str(principal, "HLTCode"),
                effDate = principal.GetValueOrDefault("MBMPayorEffDate") as DateTime? ?? DateTime.Today,
            });

        var clinic = await conn.QueryAsync(
            "SELECT PLNType, INSType, MBMPolEffDate, MBMPolExpDate FROM MBMCLNOthers WHERE MBMNumber = @mbmNumber",
            new { mbmNumber });

        var benefitLimits = await conn.QueryAsync(
            """
            SELECT MBMCoveredID, MBMCancerAnnLmt, MBMCancerBalLmt, MBMKidneyAnnLmt, MBMKidneyBalLmt, MBMHomeNCAnnLmt, MBMHomeNCBalLmt
            FROM MBMBnfLmt WHERE MBMNumber = @mbmNumber
            """,
            new { mbmNumber });

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
        var rows = await conn.QueryAsync(
            """
            SELECT MBMAmendID, MBMNumber, MBMCoverID, MBMAmendEdtType, MBMAmendDate, MBMAENDtEffDate,
                   MBMAEndtBordxDate, MBMAEndtBatchNo, MBMAmendUser
            FROM MBMAmendHistory WHERE MBMNumber = @mbmNumber
            ORDER BY MBMAmendDate DESC, MBMAmendID DESC
            """,
            new { mbmNumber = mbmNumber.Trim() });
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

        var principal = await conn.QueryAsync(
            $"""
            SELECT DISTINCT TOP (200) x.MBMNumber, x.MBMPolicyNo, x.MBMIcBcPp, x.MBMName, x.MBMPayorEffDate, x.MBMPayorExpDate,
                   x.INSCode, x.PLNCode, x.PAYCode, x.MBMCOVID, x.MBMDOB, m.MBMDataStatus,
                   CASE WHEN m.MBMStatus = 'C' AND m.MBMPayorEffDate > CAST(GETDATE() AS date) AND t.MBMCancelDate IS NULL
                        THEN 'A' ELSE m.MBMStatus END AS MBMStatus,
                   x.MBMBordxDate, p.ProductName
            FROM {db.MaintenanceDbName}.dbo.MBMCrossReference x
            INNER JOIN dbo.MBM m ON x.MBMNumber = m.MBMNumber
            INNER JOIN dbo.MBMTwo t ON x.MBMNumber = t.MBMNumber
            LEFT JOIN dbo.PLN p ON p.PLNCode = x.PLNCode
            WHERE REPLACE(x.MBMIcBcPp, '-', '') = REPLACE(@ic, '-', '')
            ORDER BY x.MBMPayorEffDate DESC
            """,
            new { ic });

        var supplementary = await conn.QueryAsync(
            $"""
            SELECT DISTINCT TOP (100) c.MBMCName, c.MBMCICBCPPNo, c.MBMCCoverID AS MBMCOVID, c.MBMCDOB AS MBMDOB,
                   COALESCE(c.MBMCEffDate, x.MBMPayorEffDate) AS MBMPayorEffDate,
                   COALESCE(c.MBMCExpDate, x.MBMPayorExpDate) AS MBMPayorExpDate,
                   x.MBMNumber, x.MBMPolicyNo, x.MBMName AS PrincipalName,
                   CASE WHEN c.MBMCStatus = 'C' AND c.MBMCEffDate > CAST(GETDATE() AS date) AND c.MBMCCancelDate IS NULL
                        THEN 'A' ELSE c.MBMCStatus END AS MBMStatus
            FROM dbo.MBMCoveredPersons c
            INNER JOIN {db.MaintenanceDbName}.dbo.MBMCrossReference x ON x.MBMNumber = c.MBMCNumber
            WHERE REPLACE(c.MBMCICBCPPNo, '-', '') = REPLACE(@ic, '-', '')
            ORDER BY MBMPayorEffDate DESC
            """,
            new { ic });

        return Ok(new { principal = ToDictionaries(principal), supplementary = ToDictionaries(supplementary) });
    }

    [HttpGet("{mbmNumber}/account")]
    public async Task<IActionResult> Account(string mbmNumber)
    {
        await using var conn = db.MainDb();
        var rows = await conn.QueryAsync(
            "SELECT * FROM VIEW_MBMTotal_Summary WHERE MBMNumber = @mbmNumber ORDER BY MBMPatientCovID",
            new { mbmNumber = mbmNumber.Trim() });
        return Ok(ToDictionaries(rows));
    }

    [HttpGet("{mbmNumber}/notes")]
    public async Task<IActionResult> Notes(string mbmNumber)
    {
        await using var conn = db.MainDb();
        var ic = await GetIc(conn, mbmNumber);
        if (ic is null) return NotFound(new { message = "Membership not found." });
        var exclusions = await conn.QueryAsync(
            "SELECT MBMNumber, MBMName, MBMPolicyNo, INSCode, PAYCode, MBMExclusion FROM View_Get_MemberExclusion WHERE MBMIcBcPp = @ic",
            new { ic });
        var remarks = await conn.QueryAsync(
            "SELECT MBMNumber, MBMName, INSCode, PAYCode, MBMTakeover, TakeOvermessageInd, MBMRemarks FROM View_Get_MemberRemarks WHERE MBMIcBcPp = @ic",
            new { ic });
        return Ok(new { exclusions = ToDictionaries(exclusions), remarks = ToDictionaries(remarks) });
    }

    private static Task<string?> GetIc(IDbConnection conn, string mbmNumber) =>
        conn.QuerySingleOrDefaultAsync<string?>(
            "SELECT LTRIM(RTRIM(MBMIcBcPp)) FROM MBM WHERE MBMNumber = @mbmNumber", new { mbmNumber = mbmNumber.Trim() });

    private static string? Str(Dictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var v) ? v?.ToString() : null;

    private static string EscapeLike(string value) =>
        value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
}
