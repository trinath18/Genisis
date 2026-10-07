namespace Genisis.Api.Auth;

public class AuthOptions
{
    public string? JwtKey { get; set; }
    public string Issuer { get; set; } = "Genisis";
    public int TokenHours { get; set; } = 10;
}
