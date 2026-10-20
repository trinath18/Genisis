using System.Data;
using Dapper;
using Genisis.Api.Data;
using Genisis.Api.Membership;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.CardControl;

public interface ICardControlService
{
    Task<CardControlSearchResult> SearchAsync(CardControlSearch search, CancellationToken cancellationToken);
    Task<CardControlUpdateResult> UpdateAsync(CardControlUpdate update, CancellationToken cancellationToken);
}

/// <summary>Membership &gt; Card Sending Mode (legacy frmMembershipCardControl: MBMInquiryList, CmdUpdate_Click).</summary>
public sealed class CardControlService(DbConnections db) : ICardControlService
{
    public async Task<CardControlSearchResult> SearchAsync(CardControlSearch search, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync(cancellationToken);
        using var grid = await conn.ProcMultipleAsync("genisis.CardControl_Search", new
        {
            payor = Text(search.PayorCode), numberFrom = Text(search.NumberFrom), numberTo = Text(search.NumberTo),
            bordxFrom = search.BordxDateFrom?.Date, bordxTo = search.BordxDateTo?.Date, batchFrom = search.BatchFrom, batchTo = search.BatchTo,
            sentFrom = search.SentDateFrom?.Date, sentTo = search.SentDateTo?.Date, mode = SendingModes.Normalize(search.SendingMode),
            limit = CardControlLimits.SearchLimit + 1,
        });
        var principals = (await grid.ReadAsync<PrincipalRow>()).ToList();
        var covered = (await grid.ReadAsync<CoveredRow>()).ToList();
        var truncated = principals.Count > CardControlLimits.SearchLimit;
        if (truncated) principals.RemoveAt(principals.Count - 1);
        return Listing(principals, covered, truncated);
    }

    /// <summary>Each principal (cover 00) followed by its covered persons, which show the principal's bordx date, batch and policy.</summary>
    internal static CardControlSearchResult Listing(List<PrincipalRow> principals, List<CoveredRow> covered, bool truncated)
    {
        var byMember = covered.ToLookup(c => c.MembershipNo, StringComparer.OrdinalIgnoreCase);
        var rows = new List<CardControlRow>();
        var supplementaries = 0;
        foreach (var p in principals)
        {
            rows.Add(new CardControlRow
            {
                MembershipNo = p.MembershipNo, CoverId = CoverIds.Principal, Name = p.Name, BordxDate = p.BordxDate, BatchNo = p.BatchNo,
                SendingMode = p.SendingMode, SentDate = p.SentDate, PolicyNo = p.PolicyNo, DocReference = p.DocReference,
                GroupCompany = p.GroupCompany, CostCenter = p.CostCenter, Branch = p.Branch, Department = p.Department,
            });
            if (!InsuredTypes.IsFamily(p.InsuredType)) continue;
            foreach (var c in byMember[p.MembershipNo])
            {
                supplementaries++;
                rows.Add(new CardControlRow
                {
                    MembershipNo = p.MembershipNo, CoverId = c.CoverId, Name = c.Name, BordxDate = p.BordxDate, BatchNo = p.BatchNo,
                    SendingMode = c.SendingMode, SentDate = c.SentDate, PolicyNo = p.PolicyNo, DocReference = c.DocReference,
                    GroupCompany = p.GroupCompany, CostCenter = p.CostCenter, Branch = p.Branch, Department = p.Department,
                });
            }
        }
        return new CardControlSearchResult(rows, principals.Count, supplementaries, truncated);
    }

    public async Task<CardControlUpdateResult> UpdateAsync(CardControlUpdate update, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync(cancellationToken);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var result = await UpdateAsync(conn, tx, update);
        await tx.CommitAsync(cancellationToken);
        return result;
    }

    /// <summary>All checked rows in the caller's transaction (the tests roll it back), as the desktop did.</summary>
    internal static async Task<CardControlUpdateResult> UpdateAsync(SqlConnection conn, SqlTransaction tx, CardControlUpdate update)
    {
        var mode = SendingModes.Normalize(update.SendingMode)!;
        var sentDate = update.SentDate!.Value.Date;
        var docRef = update.DocReference?.Trim() ?? "";
        var updated = 0;
        var items = update.Items.Where(i => !string.IsNullOrWhiteSpace(i.MembershipNo) && !string.IsNullOrWhiteSpace(i.CoverId))
            .Select(i => (Number: i.MembershipNo.Trim(), CoverId: i.CoverId.Trim())).Distinct();
        foreach (var (number, coverId) in items)
            updated += await conn.ProcSingleAsync<int>("genisis.CardControl_Update", new { number, coverId, mode, sentDate, docRef }, tx);
        return new CardControlUpdateResult(CardControlMessages.Updated, updated);
    }

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
