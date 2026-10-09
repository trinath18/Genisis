using Genisis.Api.Data;
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

    public static async Task<string> GenerateAsync(SqlConnection conn, SqlTransaction tx,
        string payorCode, string insuredType, DateTime dateOfBirth, string name, string policyNo, string renewal, bool byPolicy)
    {
        var key = byPolicy
            ? $"{payorCode}*{policyNo}"
            : $"{payorCode}*{insuredType}*{dateOfBirth.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}*{name}";
        var isNew = renewal == "N";

        var sequence = "";
        if (isNew)
        {
            var check = await NextCheckDigitsAsync(conn, tx);
            sequence = await NextPayorSequenceAsync(conn, tx, payorCode) + check;
        }

        var row = (await conn.ProcQueryAsync<SequenceRow>("genisis.Seq_RenewGet", new { byPolicy, key }, tx)).SingleOrDefault();

        var id = 1;
        string insCode;
        if (row is null)
        {
            insCode = insuredType;
            if (!isNew)
                sequence = await NextPayorSequenceAsync(conn, tx, payorCode) + await NextCheckDigitsAsync(conn, tx);
            await conn.ProcExecAsync("genisis.Seq_RenewInsert", new { byPolicy, key, insCode, sequence }, tx);
        }
        else if (isNew)
        {
            await conn.ProcExecAsync("genisis.Seq_RenewHistoryAppend", new { key, entry = $"{sequence}*{row.RewSeqID?.Trim()}" }, tx);
            insCode = insuredType;
            await conn.ProcExecAsync("genisis.Seq_RenewUpdate", new { byPolicy, key, insCode, sequence, seqId = "01" }, tx);
        }
        else
        {
            insCode = row.INSCode?.Trim() ?? "";
            sequence = row.RewSeqNo?.Trim() ?? "";
            id = (int.TryParse(row.RewSeqID?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var last) ? last : 0) + 1;
            await conn.ProcExecAsync("genisis.Seq_RenewUpdate",
                new { byPolicy, key, insCode = (string?)null, sequence, seqId = id.ToString("00", CultureInfo.InvariantCulture) }, tx);
        }

        return payorCode + (insCode + sequence).Trim() + "*" + id.ToString("00", CultureInfo.InvariantCulture);
    }

    public static async Task<string> NextPayorSequenceAsync(SqlConnection conn, SqlTransaction tx, string payorCode)
    {
        var current = (await conn.ProcQueryAsync<Value>("genisis.Seq_PayorGet", new { payorCode }, tx)).SingleOrDefault();
        var next = LegacyRules.NextPayorSequence(current?.Text);
        await conn.ProcExecAsync("genisis.Seq_PayorSet", new { payorCode, next }, tx);
        return next;
    }

    public static async Task<string> NextCheckDigitsAsync(SqlConnection conn, SqlTransaction tx)
    {
        var current = (await conn.ProcQueryAsync<Value>("genisis.Seq_CheckDigitsGet", tx: tx)).FirstOrDefault()
            ?? throw new RegistrationException("Check digit counter (MBMCheckDigits) is empty. Ask your administrator to set it up.");
        var next = LegacyRules.NextCheckDigits(current.Text ?? "00");
        await conn.ProcExecAsync("genisis.Seq_CheckDigitsSet", new { next }, tx);
        return next;
    }

    public static async Task<string> NextSecurityCodeAsync(SqlConnection conn, SqlTransaction tx)
    {
        var current = (await conn.ProcQueryAsync<Value>("genisis.Seq_SecurityGet", tx: tx)).FirstOrDefault()
            ?? throw new RegistrationException("Security code counter (MBMSecurityDigits) is empty. Ask your administrator to set it up.");
        var next = LegacyRules.NextSecurityCode(current.Text ?? "1000");
        await conn.ProcExecAsync("genisis.Seq_SecuritySet", new { next }, tx);
        return next;
    }
}
