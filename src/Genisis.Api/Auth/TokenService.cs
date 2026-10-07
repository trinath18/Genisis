using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Genisis.Api.Auth;

public class TokenService
{
    public const string AccessClaim = "usr_access";
    private readonly AuthOptions _options;

    public TokenService(IOptions<AuthOptions> options)
    {
        _options = options.Value;
        SigningKey = new SymmetricSecurityKey(string.IsNullOrWhiteSpace(_options.JwtKey)
            ? RandomNumberGenerator.GetBytes(64)
            : Encoding.UTF8.GetBytes(_options.JwtKey));
    }

    public SymmetricSecurityKey SigningKey { get; }
    public string Issuer => _options.Issuer;

    public (string Token, DateTime ExpiresUtc) Create(string userCode, string userName, string access)
    {
        var expires = DateTime.UtcNow.AddHours(_options.TokenHours);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userCode),
            new Claim(ClaimTypes.Name, userCode),
            new Claim("name", userName),
            new Claim(AccessClaim, access),
        };
        var token = new JwtSecurityToken(_options.Issuer, _options.Issuer, claims, expires: expires,
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
