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
        using var grid = await conn.ProcMultipleAsync("genisis.Registration_Lookups");
        async Task<List<Option>> Options() => (await grid.ReadAsync<Option>()).ToList();

        return Ok(new
        {
            healthCodes = await Options(),
            payors = (await grid.ReadAsync<PayorOption>()).ToList(),
            insuredTypes = await Options(),
            races = await Options(),
            nationalities = await Options(),
            relationships = await Options(),
            states = await Options(),
            cities = await Options(),
        });
    }

    [HttpGet("plans")]
    public async Task<IActionResult> Plans([FromQuery] string healthCode, [FromQuery] string? payorCode)
    {
        healthCode = (healthCode ?? "").Trim();
        payorCode = (payorCode ?? "").Trim();
        await using var conn = db.MainDb();
        var plans = await conn.ProcQueryAsync<Option>("genisis.Registration_Plans", new { healthCode, payorCode });
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
