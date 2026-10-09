using Dapper;
using Genisis.Api.Data;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Plans;

/// <summary>One age band of a Premium entry (legacy txt01-07 / Text1-7). A band without an amount is not saved.</summary>
public class PremiumBand
{
    public string? AgeCode { get; set; }
    public decimal? Amount { get; set; }
    public decimal? SuppAmount { get; set; }
}

/// <summary>Maintenance > Plan > Premium tab (legacy SavePRM): one PRM row per age band.
/// Supp status A = no supplementary premium; B/C = a supplementary premium per band.</summary>
public class PremiumCreateRequest
{
    public string? InsuredCode { get; set; }
    public string? HealthCode { get; set; }
    public string? PayorCode { get; set; }
    public string? PlanCode { get; set; }
    public string? SuppStatus { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public int? Version { get; set; }
    public List<PremiumBand> Bands { get; set; } = [];
}

public class PremiumUpdateRequest
{
    public decimal? Amount { get; set; }
    public decimal? SuppAmount { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public int? Version { get; set; }
}

public record PremiumRow(int Code, string? InsuredCode, string? PayorCode, string? HealthCode, string? AgeCode, string? PlanCode,
    decimal? Amount, decimal? SuppAmount, DateTime? EffectiveDate, int? Version, string? SuppStatus);

public static class PremiumValidator
{
    public static readonly string[] AgeCodes = ["01", "02", "03", "04", "05", "06", "07"];
    private static bool Blank(string? s) => string.IsNullOrWhiteSpace(s);

    public static string? Validate(PremiumCreateRequest r)
    {
        if (Blank(r.InsuredCode)) return "Please Enter Insured Code";
        if (Blank(r.HealthCode)) return "Please Enter Health Code";
        if (Blank(r.PlanCode)) return "Please Enter Plan Code";
        if (r.EffectiveDate is null) return "Please validate your Premium Effective date";
        if (r.Version is null) return "Please Enter Premium Version";
        var status = r.SuppStatus?.Trim().ToUpperInvariant();
        if (status is not (null or "" or "A" or "B" or "C")) return "Supp Premium Status must be A, B or C.";
        var bands = r.Bands ?? [];
        if (bands.Any(b => !AgeCodes.Contains(b.AgeCode?.Trim()))) return "Age band must be 01 to 07.";
        if (bands.GroupBy(b => b.AgeCode!.Trim()).Any(g => g.Count() > 1)) return "Each age band can only be entered once.";
        if (!bands.Any(b => b.Amount is not null)) return "Please enter the premium for at least one age band.";
        if (bands.Any(b => b.Amount < 0 || b.SuppAmount < 0)) return "Premiums cannot be negative.";
        if (new[] { r.InsuredCode, r.HealthCode, r.PayorCode, r.PlanCode }.Any(c => c?.Trim().Length > 4))
            return "Codes cannot be longer than 4 characters.";
        return null;
    }

    public static string? Validate(PremiumUpdateRequest r)
    {
        if (r.EffectiveDate is null) return "Please validate your Premium Effective date";
        if (r.Version is null) return "Please Enter Premium Version";
        if (r.Amount is null) return "Please enter the premium amount.";
        if (r.Amount < 0 || r.SuppAmount < 0) return "Premiums cannot be negative.";
        return null;
    }
}

public class PremiumService(DbConnections db)
{
    public const int SearchLimit = 500;
    public const string Duplicated = "Record Duplicated! Please Change The Corresponding Field";
    private static string? U(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToUpperInvariant();

    public async Task<List<int>> CreateAsync(PremiumCreateRequest r, string userCode) =>
        await InTransaction(async (conn, tx) => await CreateAsync(conn, tx, r, userCode));

    public Task UpdateAsync(int code, PremiumUpdateRequest r, string userCode) =>
        InTransaction(async (conn, tx) => { await UpdateAsync(conn, tx, code, r, userCode); return 0; });

    public Task DeleteAsync(int code) => InTransaction(async (conn, tx) => { await DeleteAsync(conn, tx, code); return 0; });

    private async Task<T> InTransaction<T>(Func<SqlConnection, SqlTransaction, Task<T>> work)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        var result = await work(conn, tx);
        await tx.CommitAsync();
        return result;
    }

    /// <summary>Saves one PRM row per band that has an amount; returns the new PRMCodes.
    /// The duplicate check uses the real effective date, per band (the desktop compared it with the age-01 amount).</summary>
    internal static async Task<List<int>> CreateAsync(SqlConnection conn, SqlTransaction tx, PremiumCreateRequest r, string userCode)
    {
        if (PremiumValidator.Validate(r) is { } error) throw new PlanException(error);
        var status = U(r.SuppStatus) ?? "A";
        var codes = new List<int>();
        foreach (var band in r.Bands.Where(b => b.Amount is not null).OrderBy(b => b.AgeCode!.Trim()))
        {
            var p = new
            {
                ins = U(r.InsuredCode), hlt = U(r.HealthCode), pay = U(r.PayorCode), plan = U(r.PlanCode), age = band.AgeCode!.Trim(),
                amount = band.Amount, supp = status is "B" or "C" ? band.SuppAmount : null, status,
                effective = r.EffectiveDate!.Value.Date, version = r.Version, userCode, today = DateTime.Today,
            };
            if (await conn.ExecuteScalarAsync<int>(
                    """
                    SELECT COUNT(*) FROM dbo.PRM WITH (UPDLOCK, HOLDLOCK)
                    WHERE INSCode = @ins AND HLTCode = @hlt AND PLNCode = @plan AND AGECode = @age AND PRMEffDate = @effective
                      AND (@hlt = 'S' OR ISNULL(PAYCode, '') = ISNULL(@pay, ''))
                    """, p, tx) > 0)
                throw new PlanException($"{Duplicated} (age band {p.age})");
            codes.Add(await conn.ExecuteScalarAsync<int>(
                """
                INSERT INTO dbo.PRM (INSCode, PAYCode, HLTCode, AGECode, PLNCode, SuppPrmStatus, PRMAmount, SuppPrmAmt, PRMEffDate, PRMVersion,
                    PRMLastUpdateUser, PRMLastUpdateDate)
                VALUES (@ins, @pay, @hlt, @age, @plan, @status, @amount, @supp, @effective, @version, @userCode, @today);
                SELECT CAST(SCOPE_IDENTITY() AS int);
                """, p, tx));
        }
        return codes;
    }

    /// <summary>Edits one row's amounts, date and version; its codes and age band stay fixed.
    /// A row that already shares its key and date with another row can still be edited as long as the date is kept.</summary>
    internal static async Task UpdateAsync(SqlConnection conn, SqlTransaction tx, int code, PremiumUpdateRequest r, string userCode)
    {
        if (PremiumValidator.Validate(r) is { } error) throw new PlanException(error);
        var p = new { code, amount = r.Amount, supp = r.SuppAmount, effective = r.EffectiveDate!.Value.Date, version = r.Version, userCode, today = DateTime.Today };
        if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.PRM WITH (UPDLOCK, HOLDLOCK) WHERE PRMCode = @code", p, tx) == 0)
            throw new PlanException("Record not found");
        if (await conn.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(*) FROM dbo.PRM o WITH (UPDLOCK, HOLDLOCK) JOIN dbo.PRM t ON t.PRMCode = @code
                WHERE o.PRMCode <> @code AND o.INSCode = t.INSCode AND o.HLTCode = t.HLTCode AND o.PLNCode = t.PLNCode AND o.AGECode = t.AGECode
                  AND o.PRMEffDate = @effective AND (t.HLTCode = 'S' OR ISNULL(o.PAYCode, '') = ISNULL(t.PAYCode, ''))
                  AND (t.PRMEffDate IS NULL OR t.PRMEffDate <> @effective)
                """, p, tx) > 0)
            throw new PlanException(Duplicated);
        await conn.ExecuteAsync(
            """
            UPDATE dbo.PRM SET PRMAmount = @amount, SuppPrmAmt = CASE WHEN SuppPrmStatus IN ('B','C') THEN @supp ELSE SuppPrmAmt END,
                PRMEffDate = @effective, PRMVersion = @version, PRMLastUpdateUser = @userCode, PRMLastUpdateDate = @today
            WHERE PRMCode = @code
            """, p, tx);
    }

    /// <summary>Desktop DeletePRM: refused while a member is on the row's insured/health/plan/age band (and payor unless health type S).</summary>
    internal static async Task DeleteAsync(SqlConnection conn, SqlTransaction tx, int code)
    {
        if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.PRM WITH (UPDLOCK, HOLDLOCK) WHERE PRMCode = @code", new { code }, tx) == 0)
            throw new PlanException("Record not found");
        if (await conn.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(*) FROM (SELECT TOP 1 1 AS x FROM dbo.MBM m JOIN dbo.PRM t ON t.PRMCode = @code
                    WHERE m.INSCode = t.INSCode AND m.HLTCode = t.HLTCode AND m.PLNCode = t.PLNCode AND m.AGECode = t.AGECode
                      AND (t.HLTCode = 'S' OR m.PAYCode = t.PAYCode)) u
                """, new { code }, tx) > 0)
            throw new PlanException("This record cannot be deleted because it is used in Membership table!");
        await conn.ExecuteAsync("DELETE FROM dbo.PRM WHERE PRMCode = @code", new { code }, tx);
    }

    public async Task<(List<PremiumRow> Rows, bool Truncated)> SearchAsync(string? insured, string? payor, string? health, string? age, string? plan)
    {
        await using var conn = db.MainDb();
        var rows = (await conn.QueryAsync<PremiumRow>(
            $"""
            SELECT TOP ({SearchLimit + 1}) PRMCode AS Code, RTRIM(INSCode) AS InsuredCode, RTRIM(PAYCode) AS PayorCode, RTRIM(HLTCode) AS HealthCode,
                RTRIM(AGECode) AS AgeCode, RTRIM(PLNCode) AS PlanCode, PRMAmount AS Amount, SuppPrmAmt AS SuppAmount, PRMEffDate AS EffectiveDate,
                PRMVersion AS Version, SuppPrmStatus AS SuppStatus
            FROM dbo.PRM
            WHERE (@insured IS NULL OR INSCode = @insured) AND (@payor IS NULL OR PAYCode = @payor) AND (@health IS NULL OR HLTCode = @health)
              AND (@age IS NULL OR AGECode = @age) AND (@plan IS NULL OR PLNCode = @plan)
            ORDER BY PRMCode DESC
            """,
            new { insured = U(insured), payor = U(payor), health = U(health), age = U(age), plan = U(plan) })).ToList();
        var truncated = rows.Count > SearchLimit;
        return (truncated ? rows[..SearchLimit] : rows, truncated);
    }
}
