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
    public const int MembershipTracking = 14;

    /// <summary>Maintenance > Plan uses the same position as Registration in the desktop menu.</summary>
    public const int PlanMaintenance = 1;

    /// <summary>Maintenance > Benefits also checks position 1 in the desktop menu (mnuBenefits_Click).</summary>
    public const int BenefitsMaintenance = 1;

    /// <summary>Maintenance > Entity (mnuAgent_Click) and Maintenance > Accounts (MaintenanceAccounts_Click) check position 1 too.</summary>
    public const int EntityMaintenance = 1;
    public const int AccountsMaintenance = 1;
    /// <summary>Case > Inquiry (frmCaseInquiry) checks position 7 in mdiMain.</summary>
    public const int CaseEnquiry = 7;

    /// <summary>Case > Registration (frmCaseAdmission) checks position 11 in mdiMain.</summary>
    public const int CaseRegistration = 11;

    /// <summary>Case > Adjustment (frmCaseAdjustmentNew) checks position 12 in mdiMain.</summary>
    public const int CaseAdjustment = 12;

    /// <summary>Case > Tracking (FrmCaseTracking) checks position 15 in mdiMain.</summary>
    public const int CaseTracking = 15;

    public static bool Has(string? access, int position) =>
        access is not null && access.Length >= position && access[position - 1] == '1';

    public static bool Has(ClaimsPrincipal user, int position) =>
        Has(user.FindFirst(TokenService.AccessClaim)?.Value, position);
}

/// <summary>Requires one of the given menu positions (several when screens share read-only endpoints).
/// Class and method attributes stack: both must pass.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequireAccessAttribute(int position, params int[] alternatives) : Attribute, IAuthorizationFilter
{
    public int Position => position;
    public IReadOnlyList<int> Positions => [position, .. alternatives];

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true) return;
        if (!Positions.Any(p => AccessRight.Has(context.HttpContext.User, p)))
            context.Result = new ObjectResult(Genisis.Api.Common.Errors.Problems.Create(context.HttpContext, StatusCodes.Status403Forbidden, "Forbidden", "Access Denied!")) { StatusCode = StatusCodes.Status403Forbidden };
    }
}
