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
    public const int MembershipUploadBordx = 4;

    /// <summary>Maintenance > Plan uses the same position as Registration in the desktop menu.</summary>
    public const int PlanMaintenance = 1;

    /// <summary>Maintenance > Benefits also checks position 1 in the desktop menu (mnuBenefits_Click).</summary>
    public const int BenefitsMaintenance = 1;

    /// <summary>Maintenance > Entity (mnuAgent_Click) and Maintenance > Accounts (MaintenanceAccounts_Click) check position 1 too.</summary>
    public const int EntityMaintenance = 1;
    public const int AccountsMaintenance = 1;

    public static bool Has(string? access, int position) =>
        access is not null && access.Length >= position && access[position - 1] == '1';

    public static bool Has(ClaimsPrincipal user, int position) =>
        Has(user.FindFirst(TokenService.AccessClaim)?.Value, position);
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireAccessAttribute(int position) : Attribute, IAuthorizationFilter
{
    public int Position => position;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true) return;
        if (!AccessRight.Has(context.HttpContext.User, position))
            context.Result = new ObjectResult(Genisis.Api.Common.Errors.Problems.Create(context.HttpContext, StatusCodes.Status403Forbidden, "Forbidden", "Access Denied!")) { StatusCode = StatusCodes.Status403Forbidden };
    }
}
