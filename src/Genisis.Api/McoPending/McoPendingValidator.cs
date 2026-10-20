namespace Genisis.Api.McoPending;

/// <summary>Request checks of frmMBMMCOPending, in the desktop's order. Pure functions.</summary>
public static class McoPendingValidator
{
    public static string? Code(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    public static string? ValidateSearch(McoPendingSearch search) =>
        Code(search.Status) is { } status && !McoPendingStatus.Searchable.Contains(status) ? McoPendingMessages.InvalidSearchStatus : null;

    public static string? ValidateUpdate(McoPendingUpdate update)
    {
        if (Code(update.Status) is not { } status || !McoPendingStatus.Settable.Contains(status)) return McoPendingMessages.SelectStatus;
        var numbers = update.MembershipNos.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        if (numbers.Count == 0) return McoPendingMessages.SelectRecord;
        if (numbers.Count > McoPendingLimits.MaxUpdateItems) return $"Select at most {McoPendingLimits.MaxUpdateItems} records at a time.";
        return null;
    }

    /// <summary>
    /// Desktop UpdateMCOPending: a membership without a pending row gets one with the chosen status; an existing row is removed on
    /// release and left as it is otherwise. Billed rows cannot be changed (lstMCOPending_ItemCheck).
    /// </summary>
    public static McoPendingAction Decide(bool pendingExists, string? currentStatus, string newStatus)
    {
        if (Code(currentStatus) == McoPendingStatus.Billed) return McoPendingAction.Refuse;
        if (!pendingExists) return McoPendingAction.Insert;
        return newStatus == McoPendingStatus.Release ? McoPendingAction.Delete : McoPendingAction.None;
    }
}

public enum McoPendingAction
{
    None,
    Insert,
    Delete,
    Refuse,
}
