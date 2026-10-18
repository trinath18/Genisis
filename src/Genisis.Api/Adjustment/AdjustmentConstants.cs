namespace Genisis.Api.Adjustment;

/// <summary>Endorsement types offered by the desktop (Y Re-Activate and I Insert New Member are commented out there).</summary>
public static class EndorsementTypes
{
    public const string Amendment = "A";
    public const string Cancellation = "C";
    public const string AddSupplementary = "S";
    public const string CancelSupplementary = "K";
    public const string DeleteSupplementary = "T";
    public const string Suspend = "X";
    public const string DeletePolicy = "D";
    public const string PlanChange = "P";

    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
    {
        [Amendment] = "Amendment",
        [Cancellation] = "Cancellation",
        [AddSupplementary] = "Add Supplementary",
        [CancelSupplementary] = "Cancel Supplementary",
        [DeleteSupplementary] = "Delete Supplementary",
        [Suspend] = "Suspend Membership",
        [DeletePolicy] = "Delete Policy",
        [PlanChange] = "Plan Change",
    };

    /// <summary>Types that edit the principal's details (the others save the membership as loaded).</summary>
    public static bool EditsPrincipal(string type) => type is Amendment or PlanChange;

    /// <summary>Types that end the membership and change every covered person's status.</summary>
    public static bool EndsMembership(string type) => type is Cancellation or Suspend or DeletePolicy;
}

/// <summary>What the screen asks for a covered person in <see cref="AdjustmentCovered.Action"/>.</summary>
public static class CoveredActions
{
    public const string Add = "ADD";
    public const string Remove = "REMOVE";
    public const string Cancel = "CANCEL";

    public static bool IsChange(string? action) => action?.Trim().ToUpperInvariant() is Add or Remove or Cancel;

    public static bool Is(string? action, string expected) => action?.Trim().ToUpperInvariant() == expected;
}

/// <summary>Membership status written by Adjustment besides the standard ones (MBM.MBMStatus).</summary>
public static class AdjustmentStatuses
{
    /// <summary>Plan change in progress.</summary>
    public const string PlanChanged = "P";
}

/// <summary>Search and policy rules of the Adjustment screen.</summary>
public static class AdjustmentLimits
{
    public const int SearchMinimumLength = 3;
    public const int SearchMaximumRows = 200;
    public const int MinimumPolicyDays = 30;
    public const int MaximumPolicyDays = 365;
    /// <summary>MBM.AGECode holds two characters.</summary>
    public const int AgeCodeLength = 2;
}

/// <summary>Messages shown after a successful save, per endorsement type (desktop MsgBox texts).</summary>
public static class AdjustmentMessages
{
    public static string Saved(string type) => type switch
    {
        EndorsementTypes.Suspend => "Record is Suspended",
        EndorsementTypes.DeletePolicy => "Policy Deleted Sucessfully",
        EndorsementTypes.Amendment or EndorsementTypes.PlanChange => "Updated Sucessfully",
        EndorsementTypes.Cancellation => "This Membership has been cancelled successfully",
        EndorsementTypes.DeleteSupplementary => "Supplementary Deleted Sucessfully",
        EndorsementTypes.CancelSupplementary => "Supplementary cancelled Sucessfully",
        _ => "Supplementary Added Sucessfully",
    };

    public const string NotFound = "No record found!";
    public const string InvalidDateOrAge = "Please Enter A Valid Date OR Age Out of Acceptable Range";
}
