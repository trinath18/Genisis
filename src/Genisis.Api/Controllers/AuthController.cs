using Dapper;
using Genisis.Api.Auth;
using Genisis.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(DbConnections db, TokenService tokens) : ControllerBase
{
    public record LoginRequest(string UserCode, string Password);
    public record ChangePasswordRequest(string UserCode, string Password, string NewPassword);

    private record UsrRow(string USRCode, string? USRName, string? USRPassword, string? USRAccess, string? USRLogonCHGStatus);

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserCode))
            return BadRequest(new { message = "Please Enter User Name" });

        var usr = await FindActiveUser(request.UserCode);
        if (usr is null)
            return Unauthorized(new { message = "Invalid User Name!! Please enter again.." });
        if (!LegacyCrypt.Matches(request.Password ?? "", usr.USRPassword))
            return Unauthorized(new { message = "Invalid Password!! Please enter again.." });
        if (usr.USRLogonCHGStatus?.Trim() == "1")
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You are required to change your password", mustChangePassword = true });

        return Ok(CreateSession(usr));
    }

    [AllowAnonymous]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length > 10)
            return BadRequest(new { message = "New password must be 1 to 10 characters." });

        var usr = await FindActiveUser(request.UserCode);
        if (usr is null || !LegacyCrypt.Matches(request.Password ?? "", usr.USRPassword))
            return Unauthorized(new { message = "Invalid User Name or Password" });

        await using var conn = db.HisMaintenance();
        await conn.ExecuteAsync(
            "UPDATE USR SET USRPassword = @pwd, USRLogonCHGStatus = '0' WHERE USRCode = @code",
            new { pwd = LegacyCrypt.Encrypt(request.NewPassword).ToUpperInvariant(), code = usr.USRCode });

        return Ok(CreateSession(usr));
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me() => Ok(new
    {
        userCode = User.Identity?.Name,
        userName = User.FindFirst("name")?.Value,
        access = User.FindFirst(TokenService.AccessClaim)?.Value,
    });

    private async Task<UsrRow?> FindActiveUser(string userCode)
    {
        await using var conn = db.HisMaintenance();
        return await conn.QuerySingleOrDefaultAsync<UsrRow>(
            "SELECT USRCode, USRName, USRPassword, USRAccess, USRLogonCHGStatus FROM USR WHERE USRCode = @code AND USRStatus = 'A'",
            new { code = userCode.Trim() });
    }

    private object CreateSession(UsrRow usr)
    {
        var code = usr.USRCode.Trim().ToUpperInvariant();
        var name = usr.USRName?.Trim() ?? "";
        var access = usr.USRAccess?.Trim() ?? "";
        var (token, expires) = tokens.Create(code, name, access);
        return new { token, expiresUtc = expires, user = new { userCode = code, userName = name, access } };
    }
}
