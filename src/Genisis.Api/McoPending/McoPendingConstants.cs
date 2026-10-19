using Genisis.Api.Common.Errors;

namespace Genisis.Api.McoPending;

/// <summary>MCOPending.Status.</summary>
public static class McoPendingStatus
{
    public const string Billed = "B";
    public const string Pending = "P";
    /// <summary>Release: the pending row is removed.</summary>
    public const string Release = "R";

    /// <summary>The statuses the Update button can set (desktop cboStatus).</summary>
    public static readonly IReadOnlySet<string> Settable = new HashSet<string>([Pending, Release]);

    /// <summary>The statuses the search can filter on (desktop cboSearchStatus).</summary>
    public static readonly IReadOnlySet<string> Searchable = new HashSet<string>([Billed, Pending, Release]);
}

/// <summary>Messages of frmMBMMCOPending; the desktop wording is kept.</summary>
public static class McoPendingMessages
{
    public const string SelectStatus = "Please select in status";
    public const string SelectRecord = "Please select at least one record.";
    public const string AlreadyBilled = "This Bordx has been billed";
    public const string Updated = "Record updated...";
    public const string InvalidSearchStatus = "Status must be B, P or R.";
    public const string MembershipNotFound = "Membership not found!";
}

public static class McoPendingLimits
{
    /// <summary>The desktop lists every membership when no filter is set; the web list stops here and says so.</summary>
    public const int SearchLimit = 2000;
    public const int MaxUpdateItems = 2000;
}

public sealed class McoPendingException(string message) : BusinessRuleException(message);
