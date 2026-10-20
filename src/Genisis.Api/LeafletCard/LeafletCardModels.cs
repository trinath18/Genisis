namespace Genisis.Api.LeafletCard;

/// <summary>Messages of frmMainReport; the desktop wording is kept.</summary>
public static class LeafletCardMessages
{
    public const string EnterDateFrom = "Please Enter Bordereaux Date From !";
    public const string EnterDateTo = "Please Enter Bordereaux Date To !";
    public const string EnterInsurer = "Please Enter Insurer's Code !";
    public const string EnterBatchNo = "Please Enter Batch No !";
    public const string EnterHealthType = "Please Enter Health Type !";
    public const string NotFound = "Record not found !";
    public const string DateOrder = "Bordereaux Date To must not be before Bordereaux Date From.";
}

public static class LeafletCardLimits
{
    public const int SearchLimit = 5000;
}

/// <summary>The search criteria of frmMainReport; the desktop requires all five before searching or printing.</summary>
public sealed class LeafletCardCriteria
{
    public DateTime? BordxDateFrom { get; set; }
    public DateTime? BordxDateTo { get; set; }
    public string? PayorCode { get; set; }
    public int? BatchNo { get; set; }
    public string? HealthCode { get; set; }
}

public sealed class LeafletCardRow
{
    public string PayorCode { get; set; } = "";
    public string MembershipNo { get; set; } = "";
    public DateTime? BordxDate { get; set; }
    public string? PolicyNo { get; set; }
    public string? HealthCode { get; set; }
    public int? BatchNo { get; set; }
    public string? Name { get; set; }
}

public sealed record LeafletCardSearchResult(List<LeafletCardRow> Rows, bool Truncated);

/// <summary>One leaflet (VIEW_LEAFLET).</summary>
public sealed class Leaflet
{
    public string MembershipNo { get; set; } = "";
    public string? PolicyNo { get; set; }
    public string? AgentCode { get; set; }
    public string? Name { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? Address3 { get; set; }
    public string? PostCode { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PayorName { get; set; }
    public string? GroupCompany { get; set; }
    public string? EmployeeNo { get; set; }
    public string? Department { get; set; }
    public string? InsuredType { get; set; }
    public string? PlanDescription { get; set; }
    public List<LeafletCovered> Covered { get; set; } = [];
}

public sealed class LeafletCovered
{
    public string MembershipNo { get; set; } = "";
    public string CoverId { get; set; } = "";
    public string? Name { get; set; }
    public string? Relationship { get; set; }
}

/// <summary>One card (VIEW_CardPrinting): a covered person, the principal as cover 00.</summary>
public sealed class MembershipCard
{
    public string PayorCode { get; set; } = "";
    public DateTime? BordxDate { get; set; }
    public string? HealthCode { get; set; }
    public int? BatchNo { get; set; }
    public string MembershipNo { get; set; } = "";
    public string CoverId { get; set; } = "";
    public string? Name { get; set; }
    public string? IcBcPp { get; set; }
    public string? Relationship { get; set; }
    public string? PolicyNo { get; set; }
    public string? PlanCode { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? GroupCompany { get; set; }
    public string? EmployeeNo { get; set; }

    /// <summary>The number printed on the card and in the export: membership number and cover ID.</summary>
    public string CardNumber => $"{MembershipNo}-{CoverId}";
}

/// <summary>ExpMBMPrintingToExcel: payor name, bordx date and file name (payor + yymmdd) above the card rows.</summary>
public sealed record CardExport(string PayorName, DateTime BordxDate, string FileName, List<MembershipCard> Cards);
