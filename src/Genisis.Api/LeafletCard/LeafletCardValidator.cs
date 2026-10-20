namespace Genisis.Api.LeafletCard;

/// <summary>cmdSearch_Click checks, in the desktop's order. Pure function.</summary>
public static class LeafletCardValidator
{
    public static string? Validate(LeafletCardCriteria c)
    {
        if (c.BordxDateFrom is null) return LeafletCardMessages.EnterDateFrom;
        if (c.BordxDateTo is null) return LeafletCardMessages.EnterDateTo;
        if (string.IsNullOrWhiteSpace(c.PayorCode)) return LeafletCardMessages.EnterInsurer;
        if (c.BatchNo is null) return LeafletCardMessages.EnterBatchNo;
        if (string.IsNullOrWhiteSpace(c.HealthCode)) return LeafletCardMessages.EnterHealthType;
        if (c.BordxDateTo.Value.Date < c.BordxDateFrom.Value.Date) return LeafletCardMessages.DateOrder;
        return null;
    }
}
