using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Genisis.Api.Auth;

/// <summary>Menu access positions in USR.USRAccess (legacy VB6 CheckAccess).</summary>
public static class AccessRight
{
    public const int MembershipRegistration = 1;
    public const int MembershipAdjustment = 2;
    public const int MembershipEnquiry = 3;

    public static bool Has(string? access, int position) =>
        access is not null && access.Length >= position && access[position - 1] == '1';

    public static bool Has(ClaimsPrincipal user, int position) =>
        Has(user.FindFirst(TokenService.AccessClaim)?.Value, position);
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireAccessAttribute(int position) : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true) return;
        if (!AccessRight.Has(context.HttpContext.User, position))
            context.Result = new ObjectResult(new { message = "Access Denied!" }) { StatusCode = StatusCodes.Status403Forbidden };
    }
}
