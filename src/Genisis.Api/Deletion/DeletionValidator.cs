namespace Genisis.Api.Deletion;

/// <summary>Checks of cmdList_Click / cmdGenerate_Click, in the desktop's order. Pure functions.</summary>
public static class DeletionValidator
{
    public static string Code(string? value) => value?.Trim().ToUpperInvariant() ?? "";

    /// <summary>
    /// The desktop's Delete skipped the membership-number check that Go made; the web version applies it to both, so an empty range
    /// cannot be sent.
    /// </summary>
    public static string? Validate(DeletionCriteria c)
    {
        if (Code(c.HealthCode) == "") return DeletionMessages.EnterHealthPlan;
        if (Code(c.PayorCode) == "") return DeletionMessages.EnterPayorCode;
        var option = Code(c.Option);
        if (!DeleteOptions.All.Contains(option)) return DeletionMessages.EnterOption;
        return option switch
        {
            DeleteOptions.Batch when c.ReceiptDate is null || c.BatchNo is null => DeletionMessages.EnterReceiptDateOrBatch,
            DeleteOptions.BordxDate when c.BordxDateFrom is null || c.BordxDateTo is null => DeletionMessages.EnterBordxDate,
            DeleteOptions.Membership when string.IsNullOrWhiteSpace(c.NumberFrom) || string.IsNullOrWhiteSpace(c.NumberTo) => DeletionMessages.EnterMembershipNo,
            _ => null,
        };
    }
}
