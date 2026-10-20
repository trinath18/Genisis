namespace Genisis.Api.McoPending;

/// <summary>The filters of the desktop search frame; empty values are ignored.</summary>
public sealed class McoPendingSearch
{
    public string? NumberFrom { get; set; }
    public string? NumberTo { get; set; }
    public DateTime? BordxDateFrom { get; set; }
    public DateTime? BordxDateTo { get; set; }
    public string? Status { get; set; }
}

public sealed class McoPendingRow
{
    public string MembershipNo { get; set; } = "";
    public DateTime? BordxDate { get; set; }
    public DateTime? NewBordxDate { get; set; }
    public string? Status { get; set; }
}

public sealed record McoPendingSearchResult(List<McoPendingRow> Rows, bool Truncated);

/// <summary>The checked memberships and the status chosen in cboStatus.</summary>
public sealed class McoPendingUpdate
{
    public string? Status { get; set; }
    public List<string> MembershipNos { get; set; } = [];
}

public sealed record McoPendingUpdateResult(string Message, int Updated);

internal sealed class McoPendingCurrent
{
    public string MembershipNo { get; set; } = "";
    public DateTime? BordxDate { get; set; }
    public string? Status { get; set; }
    public bool PendingExists { get; set; }
}
