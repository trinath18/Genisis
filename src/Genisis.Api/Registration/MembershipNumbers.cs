using System.Globalization;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Registration;

/// <summary>
/// Legacy membership-number generation (GenerateMBMNo05 / GenerateMBMNoPol, GenerateChkDigits, GenerateOPSecurity).
/// Every counter row is read WITH (UPDLOCK, HOLDLOCK) inside the caller's transaction so concurrent saves
/// (web or desktop) cannot hand out the same number.
/// </summary>
public static class MembershipNumbers
{
    private sealed record SequenceRow(string? INSCode, string? RewSeqNo, string? RewSeqID);
    private sealed record Value(string? Text);

    public static async Task<string> GenerateAsync(SqlConnection conn, SqlTransaction tx, string maintenanceDb,
        string payorCode, string insuredType, DateTime dateOfBirth, string name, string policyNo, string renewal, bool byPolicy)
    {
        var table = $"{maintenanceDb}.dbo.{(byPolicy ? "MBMSEQRenewPol" : "MBMSEQRenew05")}";
        var key = byPolicy
            ? $"{payorCode}*{policyNo}"
            : $"{payorCode}*{insuredType}*{dateOfBirth.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}*{name}";
        var isNew = renewal == "N";

        var sequence = "";
        if (isNew) sequence = await NextCheckDigitsAsync(conn, tx) is var check
            ? await NextPayorSequenceAsync(conn, tx, maintenanceDb, payorCode) + check
            : "";

        var row = await conn.QuerySingleOrDefaultAsync<SequenceRow>(
            $"SELECT INSCode, RewSeqNo, RewSeqID FROM {table} WITH (UPDLOCK, HOLDLOCK) WHERE RewMBMSeqKey = @key",
            new { key }, tx);

        var id = 1;
        string insCode;
        if (row is null)
        {
            insCode = insuredType;
            if (!isNew)
                sequence = await NextPayorSequenceAsync(conn, tx, maintenanceDb, payorCode) + await NextCheckDigitsAsync(conn, tx);
            await conn.ExecuteAsync(
                $"INSERT INTO {table} (RewMBMSeqKey, INSCode, RewSeqNo, RewSeqID) VALUES (@key, @insCode, @sequence, '01')",
                new { key, insCode, sequence }, tx);
        }
        else if (isNew)
        {
            await AppendHistoryAsync(conn, tx, maintenanceDb, key, $"{sequence}*{row.RewSeqID?.Trim()}");
            insCode = insuredType;
            await conn.ExecuteAsync(
                $"UPDATE {table} SET RewSeqID = '01', INSCode = @insCode, RewSeqNo = @sequence WHERE RewMBMSeqKey = @key",
                new { key, insCode, sequence }, tx);
        }
        else
        {
            insCode = row.INSCode?.Trim() ?? "";
            sequence = row.RewSeqNo?.Trim() ?? "";
            id = (int.TryParse(row.RewSeqID?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var last) ? last : 0) + 1;
            await conn.ExecuteAsync(
                $"UPDATE {table} SET RewSeqID = @seqId, RewSeqNo = @sequence WHERE RewMBMSeqKey = @key",
                new { key, sequence, seqId = id.ToString("00", CultureInfo.InvariantCulture) }, tx);
        }

        return payorCode + (insCode + sequence).Trim() + "*" + id.ToString("00", CultureInfo.InvariantCulture);
    }

    public static async Task<string> NextPayorSequenceAsync(SqlConnection conn, SqlTransaction tx, string maintenanceDb, string payorCode)
    {
        var current = await conn.QuerySingleOrDefaultAsync<Value>(
            $"SELECT SEQNumber AS Text FROM {maintenanceDb}.dbo.MBMSEQ05 WITH (UPDLOCK, HOLDLOCK) WHERE PAYCode = @payorCode",
            new { payorCode }, tx);
        var next = LegacyRules.NextPayorSequence(current?.Text);
        await conn.ExecuteAsync(current is null
                ? $"INSERT INTO {maintenanceDb}.dbo.MBMSEQ05 (PAYCode, SEQNumber) VALUES (@payorCode, @next)"
                : $"UPDATE {maintenanceDb}.dbo.MBMSEQ05 SET SEQNumber = @next WHERE PAYCode = @payorCode",
            new { payorCode, next }, tx);
        return next;
    }

    public static async Task<string> NextCheckDigitsAsync(SqlConnection conn, SqlTransaction tx)
    {
        var current = await conn.QuerySingleOrDefaultAsync<Value>(
            "SELECT TOP 1 MBMChkDigits AS Text FROM dbo.MBMCheckDigits WITH (UPDLOCK, HOLDLOCK)", transaction: tx)
            ?? throw new RegistrationException("Check digit counter (MBMCheckDigits) is empty. Ask your administrator to set it up.");
        var next = LegacyRules.NextCheckDigits(current.Text ?? "00");
        await conn.ExecuteAsync("UPDATE dbo.MBMCheckDigits SET MBMChkDigits = @next", new { next }, tx);
        return next;
    }

    public static async Task<string> NextSecurityCodeAsync(SqlConnection conn, SqlTransaction tx)
    {
        var current = await conn.QuerySingleOrDefaultAsync<Value>(
            "SELECT TOP 1 MBMSecurityCode AS Text FROM dbo.MBMSecurityDigits WITH (UPDLOCK, HOLDLOCK)", transaction: tx)
            ?? throw new RegistrationException("Security code counter (MBMSecurityDigits) is empty. Ask your administrator to set it up.");
        var next = LegacyRules.NextSecurityCode(current.Text ?? "1000");
        await conn.ExecuteAsync("UPDATE dbo.MBMSecurityDigits SET MBMSecurityCode = @next", new { next }, tx);
        return next;
    }

    private static Task AppendHistoryAsync(SqlConnection conn, SqlTransaction tx, string maintenanceDb, string key, string entry) =>
        conn.ExecuteAsync(
            $"""
            UPDATE {maintenanceDb}.dbo.MBMSEQRenewHistory WITH (UPDLOCK, HOLDLOCK) SET SEQNo = SEQNo + '~' + @entry WHERE MBMKey = @key;
            IF @@ROWCOUNT = 0 INSERT INTO {maintenanceDb}.dbo.MBMSEQRenewHistory (MBMKey, SEQNo) VALUES (@key, @entry);
            """,
            new { key, entry }, tx);
}
