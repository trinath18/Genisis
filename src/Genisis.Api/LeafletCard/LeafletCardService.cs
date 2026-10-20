using System.Globalization;
using Genisis.Api.Data;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.LeafletCard;

public interface ILeafletCardService
{
    Task<LeafletCardSearchResult> SearchAsync(LeafletCardCriteria criteria, CancellationToken cancellationToken);
    Task<List<Leaflet>> GetLeafletsAsync(LeafletCardCriteria criteria, CancellationToken cancellationToken);
    Task<CardExport> GetCardsAsync(LeafletCardCriteria criteria, CancellationToken cancellationToken);
}

/// <summary>Membership &gt; Leaflet &amp; Card (legacy frmMainReport: RecordSearching, LeafLetPrint, ExpMBMPrintingToExcel).</summary>
public sealed class LeafletCardService(DbConnections db) : ILeafletCardService
{
    private const string FileNameDateFormat = "yyMMdd";

    public async Task<LeafletCardSearchResult> SearchAsync(LeafletCardCriteria c, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync(cancellationToken);
        var rows = await conn.ProcQueryAsync<LeafletCardRow>("genisis.LeafletCard_Search", new
        {
            from = c.BordxDateFrom!.Value.Date, to = c.BordxDateTo!.Value.Date, pay = Code(c.PayorCode), hlt = Code(c.HealthCode), batch = c.BatchNo,
            limit = LeafletCardLimits.SearchLimit + 1,
        });
        var truncated = rows.Count > LeafletCardLimits.SearchLimit;
        if (truncated) rows.RemoveAt(rows.Count - 1);
        return new LeafletCardSearchResult(rows, truncated);
    }

    public async Task<List<Leaflet>> GetLeafletsAsync(LeafletCardCriteria c, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync(cancellationToken);
        return await GetLeafletsAsync(conn, c);
    }

    internal static async Task<List<Leaflet>> GetLeafletsAsync(SqlConnection conn, LeafletCardCriteria c)
    {
        using var grid = await conn.ProcMultipleAsync("genisis.LeafletCard_Leaflets", new
        {
            from = c.BordxDateFrom!.Value.Date, to = c.BordxDateTo!.Value.Date, pay = Code(c.PayorCode), hlt = Code(c.HealthCode), batch = c.BatchNo,
        });
        var leaflets = (await grid.ReadAsync<Leaflet>()).ToList();
        var covered = (await grid.ReadAsync<LeafletCovered>()).ToLookup(p => p.MembershipNo, StringComparer.OrdinalIgnoreCase);
        foreach (var leaflet in leaflets) leaflet.Covered = [.. covered[leaflet.MembershipNo]];
        return leaflets;
    }

    public async Task<CardExport> GetCardsAsync(LeafletCardCriteria c, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync(cancellationToken);
        return await GetCardsAsync(conn, c);
    }

    internal static async Task<CardExport> GetCardsAsync(SqlConnection conn, LeafletCardCriteria c)
    {
        var payor = Code(c.PayorCode);
        var from = c.BordxDateFrom!.Value.Date;
        var cards = await conn.ProcQueryAsync<MembershipCard>("genisis.LeafletCard_Cards", new { from, to = c.BordxDateTo!.Value.Date, pay = payor, batch = c.BatchNo });
        var payorName = await conn.ProcScalarAsync<string?>("genisis.LeafletCard_Payor", new { pay = payor }) ?? payor;
        return new CardExport(payorName, from, FileName(payor, from), cards);
    }

    /// <summary>ExpMBMPrintingToExcel: payor code followed by the bordx date from as yymmdd.</summary>
    public static string FileName(string payorCode, DateTime bordxDateFrom) =>
        payorCode + bordxDateFrom.ToString(FileNameDateFormat, CultureInfo.InvariantCulture);

    private static string Code(string? value) => value?.Trim().ToUpperInvariant() ?? "";
}
