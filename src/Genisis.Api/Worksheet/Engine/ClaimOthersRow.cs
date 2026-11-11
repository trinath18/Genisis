using System.Globalization;

namespace Genisis.Api.Worksheet.Engine;

/// <summary>What <c>SaveClaimRemarks</c> reads from the screen: the remarks, the payee's address, the payment mode and the attention line.</summary>
public sealed record ClaimOthersInput
{
    /// <summary>Case number.</summary>
    public required string CaseNo { get; init; }

    /// <summary>The worksheet version.</summary>
    public required int Version { get; init; }

    /// <summary>The worksheet remarks.</summary>
    public string? WorksheetRemarks { get; init; }

    /// <summary>The credit/debit note remarks.</summary>
    public string? NoteRemarks { get; init; }

    /// <summary>The note.</summary>
    public string? Note { get; init; }

    /// <summary>Address line 1.</summary>
    public string? Address1 { get; init; }

    /// <summary>Address line 2.</summary>
    public string? Address2 { get; init; }

    /// <summary>Address line 3.</summary>
    public string? Address3 { get; init; }

    /// <summary>Post code.</summary>
    public string? PostCode { get; init; }

    /// <summary>City.</summary>
    public string? City { get; init; }

    /// <summary>State.</summary>
    public string? State { get; init; }

    /// <summary>Payment mode.</summary>
    public string? PaymentMode { get; init; }

    /// <summary>Attention.</summary>
    public string? Attention { get; init; }

    /// <summary>The gross room and board rate of the plan; above zero it is added to the worksheet remarks.</summary>
    public decimal GrossRoomRate { get; init; }
}

/// <summary>The row of <c>ClaimOthers</c> as <c>SaveClaimRemarks</c> writes it through <c>Insert_WS_ClaimOthers_Jul08</c>, keyed by the procedure's parameter names.</summary>
public static class ClaimOthersRow
{
    /// <summary>The parameters of <c>Insert_WS_ClaimOthers_Jul08</c>, in its order.</summary>
    public static readonly IReadOnlyList<string> Parameters =
    [
        "CasNumber", "claimversion", "CLMWSRemarks", "CLMDCNoteRemarks", "CLMNote", "MBMAdd1", "MBMAdd2", "MBMAdd3", "MBMPostCode", "MBMCity", "MBMState", "CLMPayMode", "CLMAttention",
    ];

    /// <summary>The words the desktop adds to the worksheet remarks when the plan has a gross room and board rate (none of the worksheets saved since July 2020 has them).</summary>
    public const string GrossRateText = " GROSS RB RATE : RM";

    /// <summary>The worksheet remarks with the gross room and board rate, as <c>SaveClaimRemarks</c> writes it: a rate above zero is added in VB's number format.</summary>
    public static string Remarks(string? remarks, decimal grossRoomRate) =>
        (remarks ?? "") + (grossRoomRate > 0 ? GrossRateText + grossRoomRate.ToString("0.####", CultureInfo.InvariantCulture) : "");

    /// <summary>The parameters of the remarks row.</summary>
    public static Dictionary<string, object?> Build(ClaimOthersInput i) => new()
    {
        ["CasNumber"] = i.CaseNo.Trim(),
        ["claimversion"] = i.Version,
        ["CLMWSRemarks"] = Remarks(i.WorksheetRemarks, i.GrossRoomRate),
        ["CLMDCNoteRemarks"] = i.NoteRemarks ?? "",
        ["CLMNote"] = i.Note ?? "",
        ["MBMAdd1"] = i.Address1 ?? "",
        ["MBMAdd2"] = i.Address2 ?? "",
        ["MBMAdd3"] = i.Address3 ?? "",
        ["MBMPostCode"] = i.PostCode ?? "",
        ["MBMCity"] = i.City ?? "",
        ["MBMState"] = i.State ?? "",
        ["CLMPayMode"] = i.PaymentMode ?? "",
        ["CLMAttention"] = i.Attention ?? "",
    };
}
