using Genisis.Api.Common.Errors;

namespace Genisis.Api.CardControl;

/// <summary>Desktop cbosendingmode / cboSendCat.</summary>
public static class SendingModes
{
    public const string ByHand = "By Hand";
    public const string ByPost = "By Post";
    public const string Courier = "Courier";

    public static readonly IReadOnlyList<string> All = [ByHand, ByPost, Courier];

    /// <summary>The stored spelling of a mode, matched case-insensitively; <c>null</c> when it is not one of them.</summary>
    public static string? Normalize(string? mode) => All.FirstOrDefault(m => string.Equals(m, mode?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>Messages of frmMembershipCardControl; the desktop wording is kept.</summary>
public static class CardControlMessages
{
    public const string EnterSendingMode = "Please Enter Card Sending Mode!";
    public const string EnterSentDate = "Please Enter Card Sent Date!";
    public const string SelectRecord = "Please select at least one record.";
    public const string InvalidFilterMode = "Sending Mode must be By Hand, By Post or Courier.";
    public const string Updated = "Record updated !";
    public const string NotFound = "Record Not Found!";
}

public static class CardControlLimits
{
    /// <summary>The desktop lists every membership when no filter is set (with a Stop button); the web list stops here.</summary>
    public const int SearchLimit = 2000;
    public const int MaxUpdateItems = 5000;
    /// <summary>MBMTwo.MBMDOCRef / MBMCoveredPersonsTwo.MBMDOCRef are nvarchar(100): 50 characters.</summary>
    public const int DocReferenceMaxLength = 50;
}

public sealed class CardControlSearch
{
    public string? PayorCode { get; set; }
    public string? NumberFrom { get; set; }
    public string? NumberTo { get; set; }
    public DateTime? BordxDateFrom { get; set; }
    public DateTime? BordxDateTo { get; set; }
    public int? BatchFrom { get; set; }
    public int? BatchTo { get; set; }
    public DateTime? SentDateFrom { get; set; }
    public DateTime? SentDateTo { get; set; }
    public string? SendingMode { get; set; }
}

/// <summary>A principal (cover 00) or covered person row of the list; covered persons carry the principal's bordx date and batch.</summary>
public sealed class CardControlRow
{
    public string MembershipNo { get; set; } = "";
    public string CoverId { get; set; } = "";
    public string? Name { get; set; }
    public DateTime? BordxDate { get; set; }
    public int? BatchNo { get; set; }
    public string? SendingMode { get; set; }
    public DateTime? SentDate { get; set; }
    public string? PolicyNo { get; set; }
    public string? DocReference { get; set; }
    public string? GroupCompany { get; set; }
    public string? CostCenter { get; set; }
    public string? Branch { get; set; }
    public string? Department { get; set; }
}

public sealed record CardControlSearchResult(List<CardControlRow> Rows, int TotalPrincipal, int TotalSupplementary, bool Truncated);

public sealed class CardControlItem
{
    public string MembershipNo { get; set; } = "";
    public string CoverId { get; set; } = "";
}

public sealed class CardControlUpdate
{
    public string? SendingMode { get; set; }
    public DateTime? SentDate { get; set; }
    public string? DocReference { get; set; }
    public List<CardControlItem> Items { get; set; } = [];
}

public sealed record CardControlUpdateResult(string Message, int Updated);

public sealed class CardControlException(string message) : BusinessRuleException(message);

internal sealed class PrincipalRow
{
    public string MembershipNo { get; set; } = "";
    public string? Name { get; set; }
    public DateTime? BordxDate { get; set; }
    public int? BatchNo { get; set; }
    public string? SendingMode { get; set; }
    public DateTime? SentDate { get; set; }
    public string? PolicyNo { get; set; }
    public string? DocReference { get; set; }
    public string? GroupCompany { get; set; }
    public string? CostCenter { get; set; }
    public string? Branch { get; set; }
    public string? Department { get; set; }
    public string? InsuredType { get; set; }
}

internal sealed class CoveredRow
{
    public string MembershipNo { get; set; } = "";
    public string CoverId { get; set; } = "";
    public string? Name { get; set; }
    public string? SendingMode { get; set; }
    public DateTime? SentDate { get; set; }
    public string? DocReference { get; set; }
}
