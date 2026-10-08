using Dapper;
using Genisis.Api.Auth;
using Genisis.Api.Data;
using Genisis.Api.Registration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Controllers;

/// <summary>Membership > Registration (legacy frmMemberEntry + frmNewMBMReg05), standard non-clinical plans.</summary>
[ApiController]
[Route("api/membership/registration")]
[RequireAccess(AccessRight.MembershipRegistration)]
public class RegistrationController(DbConnections db, RegistrationService registration, ILogger<RegistrationController> log) : ControllerBase
{
    public record Option(string Code, string Name);
    public record PayorOption(string Code, string Name, string? HealthCode);

    [HttpGet("lookups")]
    public async Task<IActionResult> Lookups()
    {
        await using var conn = db.MainDb();
        var maintenanceDb = await db.MaintenanceDbNameAsync();
        async Task<List<Option>> Options(string sql) => (await conn.QueryAsync<Option>(sql)).ToList();

        return Ok(new
        {
            healthCodes = await Options("SELECT RTRIM(HLTCode) AS Code, RTRIM(HLTDescription) AS Name FROM dbo.HLT ORDER BY HLTCode"),
            payors = (await conn.QueryAsync<PayorOption>(
                "SELECT RTRIM(PAYCode) AS Code, RTRIM(PAYCompanyName) AS Name, RTRIM(HLTCode) AS HealthCode FROM dbo.PAY ORDER BY PAYCode")).ToList(),
            insuredTypes = await Options("SELECT RTRIM(INSCode) AS Code, RTRIM(INSDescription) AS Name FROM dbo.INS ORDER BY INSCode"),
            races = await Options("SELECT RTRIM(RACCode) AS Code, RTRIM(RACName) AS Name FROM dbo.RAC WHERE RACCode IS NOT NULL ORDER BY RACCode"),
            nationalities = await Options("SELECT RTRIM(NATCode) AS Code, RTRIM(NATName) AS Name FROM dbo.NAT WHERE NATCode IS NOT NULL ORDER BY NATName"),
            relationships = await Options("SELECT RTRIM(RELCode) AS Code, RTRIM(RELDescription) AS Name FROM dbo.REL WHERE RELCode IS NOT NULL ORDER BY RELCode"),
            states = await Options("SELECT RTRIM(STACode) AS Code, RTRIM(STADescription) AS Name FROM dbo.STA WHERE STACode IS NOT NULL ORDER BY STADescription"),
            cities = await Options($"SELECT RTRIM(CTYCode) AS Code, RTRIM(CTYDescription) AS Name FROM {maintenanceDb}.dbo.CTY WHERE CTYCode IS NOT NULL ORDER BY CTYDescription"),
        });
    }

    [HttpGet("plans")]
    public async Task<IActionResult> Plans([FromQuery] string healthCode, [FromQuery] string? payorCode)
    {
        healthCode = (healthCode ?? "").Trim();
        payorCode = (payorCode ?? "").Trim();
        await using var conn = db.MainDb();
        var plans = await conn.QueryAsync<Option>(
            """
            SELECT RTRIM(PLNCode) AS Code, MAX(RTRIM(PLNDescription)) AS Name
            FROM dbo.PLN
            WHERE HLTCode = @healthCode AND (@healthCode = 'S' OR PAYCode = @payorCode)
            GROUP BY PLNCode
            ORDER BY PLNCode
            """,
            new { healthCode, payorCode });
        return Ok(plans);
    }

    [HttpPost("quote")]
    public async Task<IActionResult> Quote(RegistrationRequest request) =>
        await Run(request, () => registration.QuoteAsync(request));

    [HttpPost]
    public async Task<IActionResult> Register(RegistrationRequest request) =>
        await Run(request, () => registration.RegisterAsync(request, User.Identity?.Name ?? ""));

    private async Task<IActionResult> Run<T>(RegistrationRequest request, Func<Task<T>> action)
    {
        if (RegistrationValidator.Validate(request) is { } error) return BadRequest(new { message = error });
        try
        {
            return Ok(await action());
        }
        catch (RegistrationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (SqlException ex) when (ex.Number is 8152 or 2628)
        {
            return BadRequest(new { message = "One of the values is longer than the database allows. Shorten it and try again." });
        }
        catch (SqlException ex) when (ex.Number is 15581 or 15466)
        {
            log.LogError(ex, "Registration failed: database encryption key is not available");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "The database encryption key is not open on this SQL Server, so member details cannot be saved. Ask your DBA to open the database master key.",
            });
        }
    }
}
