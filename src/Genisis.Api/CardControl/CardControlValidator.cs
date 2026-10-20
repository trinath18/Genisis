namespace Genisis.Api.CardControl;

/// <summary>Request checks of frmMembershipCardControl, in the desktop's order. Pure functions.</summary>
public static class CardControlValidator
{
    public static string? ValidateSearch(CardControlSearch search) =>
        !string.IsNullOrWhiteSpace(search.SendingMode) && SendingModes.Normalize(search.SendingMode) is null ? CardControlMessages.InvalidFilterMode : null;

    public static string? ValidateUpdate(CardControlUpdate update)
    {
        if (SendingModes.Normalize(update.SendingMode) is null) return CardControlMessages.EnterSendingMode;
        if (update.SentDate is null) return CardControlMessages.EnterSentDate;
        if ((update.DocReference?.Trim().Length ?? 0) > CardControlLimits.DocReferenceMaxLength)
            return $"Doc Reference must be at most {CardControlLimits.DocReferenceMaxLength} characters.";
        var items = update.Items.Where(i => !string.IsNullOrWhiteSpace(i.MembershipNo) && !string.IsNullOrWhiteSpace(i.CoverId)).ToList();
        if (items.Count == 0) return CardControlMessages.SelectRecord;
        if (items.Count > CardControlLimits.MaxUpdateItems) return $"Select at most {CardControlLimits.MaxUpdateItems} records at a time.";
        return null;
    }
}
