using Genisis.Api.Data;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Bordx;

/// <summary>PAY row fields the upload needs.</summary>
public sealed record BordxPayor(string? BordxFormatType, string? ClinicalStatus, string? CancelStatus, string? RegStatus);

/// <summary>The layout of a file: ZURICH / TAK (from the header) or the payor's BordxFormatType.</summary>
public sealed record BordxFormat(bool IsZurichTakLayout, BordxPayor? Payor)
{
    public static bool IsZurichTak(BordxLine header)
    {
        var marker = header.Raw(HeaderLayout.PayorFormat);
        return marker.Trim() == BordxFormats.ZurichMarker || marker.StartsWith(BordxFormats.TakMarkerPrefix, StringComparison.Ordinal);
    }

    /// <exception cref="BordxException">The payor uses an AXA layout, which the web upload does not handle.</exception>
    public static async Task<BordxFormat> DetectAsync(SqlConnection conn, SqlTransaction? tx, BordxLine header, string payorCode)
    {
        var payor = payorCode == "" ? null : await conn.ProcFirstOrDefaultAsync<BordxPayor>("genisis.Bordx_Payor", new { pay = payorCode }, tx);
        if (IsZurichTak(header)) return new BordxFormat(true, payor);
        if (payor?.BordxFormatType?.Trim() is BordxFormats.Axa or BordxFormats.AxaLife)
            throw new BordxException("This payor uses an AXA bordereaux layout, which the web upload does not handle. Upload this file from the desktop.");
        return new BordxFormat(false, payor);
    }
}
