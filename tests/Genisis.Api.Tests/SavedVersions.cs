using System.Globalization;
using Dapper;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;
using Genisis.Api.Worksheet.Calculation;
using Genisis.Api.Worksheet.Engine;

namespace Genisis.Api.Tests;

/// <summary>A saved worksheet version read back: its tables as saved, the grid they make, the summary rows and the inputs the desktop read from the screen.</summary>
internal sealed record SavedVersion(
    string CaseNo,
    int Version,
    bool Flat,
    Dictionary<string, IDictionary<string, object?>> Tables,
    Dictionary<string, object?> Claim,
    WorksheetEvaluation Grid,
    WorksheetFinal Final,
    ClaimVersionInput Input,
    ClaimOthersInput? Others)
{
    /// <summary>A saved number as the desktop wrote it: money, or the grid's text (a dash for nothing, brackets for a negative, thousands separators).</summary>
    public static decimal Number(object? value)
    {
        if (value is decimal money) return money;
        var text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
        if (string.IsNullOrEmpty(text) || text == "-") return 0m;
        var negative = text.StartsWith('(') && text.EndsWith(')');
        text = text.Trim('(', ')').Replace(",", "");
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? (negative ? -n : n) : 0m;
    }

    /// <summary>A saved text, trimmed; empty for NULL.</summary>
    public static string Text(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? "";

    /// <summary>The closed versions saved since July 2020 that have all their tables, about one in <paramref name="every"/>, with the plan layout and BTBG flag of their member.</summary>
    public static async Task<List<(string Cas, int Version, string? Family, string? Payor, string? Btbg)>> SampleAsync(IDbSession session, int every) =>
        (await session.Connection.QueryAsync<(string Cas, int Version, string? Family, string? Payor, string? Btbg)>(
            $"""
            SELECT RTRIM(a.CASNumber), a.CLMVersion, LEFT(RTRIM(CASE WHEN v.MBMPatientCovID <> N'00' AND ISNULL(RTRIM(v.MBMCPLNCode), N'') <> N'' THEN v.MBMCPLNCode ELSE v.PLNCode END), 1), LEFT(RTRIM(v.PAYCode), 2), RTRIM(o.MBMBTBG)
            FROM genisis.WS_ClaimActual a
            JOIN dbo.VIEW_Claim_MBM_Case_worksheetAdjustment v ON v.CASNumber = a.CASNumber AND v.ClaimVersion = a.CLMVersion
            JOIN genisis.WS_Claim w ON w.CASNumber = a.CASNumber AND w.ClaimVersion = a.CLMVersion
            LEFT JOIN dbo.MBMOthersTwo o ON o.MBMNumber = v.MBMNumber
            WHERE a.CASNumber NOT LIKE 'ZZ%' AND v.MBMNumber IS NOT NULL AND w.ClaimOpenDate >= '2020-07-01' AND w.ClaimStatus = 'C' AND w.CLMPreviousBalance IS NOT NULL AND ABS(CHECKSUM(a.CASNumber, a.CLMVersion)) % {every} = 0
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimApprove p WHERE p.CASNumber = a.CASNumber AND p.CLMVersion = a.CLMVersion)
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimHos h WHERE h.CASNumber = a.CASNumber AND h.CLMVersion = a.CLMVersion)
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimMember m WHERE m.CASNumber = a.CASNumber AND m.CLMVersion = a.CLMVersion)
              AND EXISTS (SELECT 1 FROM genisis.WS_ClaimLimit l WHERE l.CASNumber = a.CASNumber AND l.CLMVersion = a.CLMVersion)
            """, transaction: session.Transaction, commandTimeout: 300)).ToList();

    /// <summary>Reads one version back; <c>null</c> when a table is missing.</summary>
    public static async Task<SavedVersion?> ReadAsync(IDbSession session, string cas, int version, string? family, string? payor, string? btbg)
    {
        var flat = WorksheetRows.Flat(family, payor);
        var tables = new Dictionary<string, IDictionary<string, object?>>();
        foreach (var table in new[] { "ClaimActual", "ClaimApprove", "ClaimHos", "ClaimMember", "ClaimLimit" })
            if (await session.Connection.QueryFirstOrDefaultAsync($"SELECT * FROM genisis.WS_{table} WHERE CASNumber = @cas AND CLMVersion = @version", new { cas, version }, session.Transaction) is IDictionary<string, object?> row) tables[table] = row;
        if (tables.Count < 5 || await session.Connection.QueryFirstOrDefaultAsync("SELECT * FROM genisis.WS_Claim WHERE CASNumber = @cas AND ClaimVersion = @version", new { cas, version }, session.Transaction) is not IDictionary<string, object?> raw) return null;
        var claim = new Dictionary<string, object?>(raw, StringComparer.OrdinalIgnoreCase);

        var lines = WorksheetRows.Columns(flat).Select((c) => new WorksheetLine(c.Slot, Number(tables["ClaimActual"][c.Column]), Text(tables["ClaimLimit"][c.Column]) is { Length: > 0 } limit && limit != "-" ? limit : null,
            Number(tables["ClaimApprove"][c.Column]), Number(tables["ClaimHos"][c.Column]), Number(tables["ClaimMember"][c.Column]))).ToList();
        var grid = new WorksheetEvaluation(lines, new WorksheetTotals(lines.Sum((l) => l.Actual), lines.Sum((l) => l.Approved), lines.Sum((l) => l.ToHospital), lines.Sum((l) => l.ToMember)));
        var final = WorksheetRules.Final(grid, Number(claim["CLMCoPayRB"]), Number(claim["CLMPreviousBalance"]));
        var perDisability = Number(claim["CLMPrevPerDisLmt"]);
        var cash = Text(claim["CLMCashType"]);
        var input = new ClaimVersionInput
        {
            CaseNo = cas,
            Version = version,
            UserCode = Text(claim["ClaimOpenBy"]),
            Today = Convert.ToDateTime(claim["ClaimOpendate"], CultureInfo.InvariantCulture),
            WorksheetStatus = Text(claim["CLMWSStatus"]),
            PayToCode = cash == "C" ? "H" : cash == "R" ? "M" : null,
            StayType = Text(claim["CLMStayType"]),
            PayTo = Text(claim["CLMPayto"]),
            Bank = Text(claim["CLMBank"]),
            AccountNo = Text(claim["CLMAccountNo"]),
            GpName = Text(claim["CLMGPName"]),
            LastDocumentDate = claim["CLMLastDocDate"] as DateTime?,
            ExcessCollectedBy = Text(claim["ExcessCollectedBy"]) is { Length: 1 } e ? e[0] : 'M',
            LimitCollectedBy = Text(claim["LimitCollectedBy"]) is { Length: 1 } l ? l[0] : 'M',
            ExGratia = Text(claim["CLMExGratia"]),
            ExGratiaAmount = Number(claim["CLMExGratiaAmt"]),
            CaseMgmtFees = Number(claim["CLMCaseMgmtFees"]),
            MemberBtbg = btbg == "Y",
            Limit = perDisability != 0 ? LimitKind.PerDisability : LimitKind.Annual,
            AvailableLimit = Number(claim["CLMPreviousBalance"]),
            PerDisabilityLimit = perDisability,
            LifetimeBalance = Number(claim["CLMLifetimeBal"]),
            TakeOverLimit = Number(claim["CLMPrevTOLmt"]),
            LinkCase = Text(claim["CLMLinkCase"]),
            LinkVersion = claim["CLMLinkVersion"] is null ? null : Convert.ToInt32(claim["CLMLinkVersion"], CultureInfo.InvariantCulture),
        };

        ClaimOthersInput? others = null;
        if (await session.Connection.QueryFirstOrDefaultAsync("SELECT * FROM genisis.WS_ClaimOthers WHERE CASNumber = @cas AND ClaimVersion = @version", new { cas, version }, session.Transaction) is IDictionary<string, object?> o)
        {
            var row = new Dictionary<string, object?>(o, StringComparer.OrdinalIgnoreCase);
            others = new ClaimOthersInput
            {
                CaseNo = cas,
                Version = version,
                WorksheetRemarks = Convert.ToString(row["CLMWSRemarks"]),
                NoteRemarks = Convert.ToString(row["CLMDCNoteRemarks"]),
                Note = Convert.ToString(row["CLMNote"]),
                Address1 = Convert.ToString(row["MBMAdd1"]),
                Address2 = Convert.ToString(row["MBMAdd2"]),
                Address3 = Convert.ToString(row["MBMAdd3"]),
                PostCode = Convert.ToString(row["MBMPostCode"]),
                City = Convert.ToString(row["MBMCity"]),
                State = Convert.ToString(row["MBMState"]),
                PaymentMode = Convert.ToString(row["CLMPayMode"]),
                Attention = Convert.ToString(row["CLMAttention"]),
            };
        }
        return new SavedVersion(cas, version, flat, tables, claim, grid, final, input, others);
    }
}
