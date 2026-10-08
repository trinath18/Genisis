using Dapper;
using Genisis.Api.Data;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Plans;

/// <summary>Maintenance > Plan > Annual Limit tab (legacy SaveAnnualLimit). Status: A share limit for family,
/// B share limit for supplementary, C separate limit for supplementary.</summary>
public class AnnualLimitRequest
{
    public string? PlanCode { get; set; }
    public string? HealthCode { get; set; }
    public string? InsuredCode { get; set; }
    public string? PayorCode { get; set; }
    public string? GroupCompany { get; set; }
    public decimal? AnnualLimit { get; set; }
    public decimal? LifetimeLimit { get; set; }
    public string? SuppLimitStatus { get; set; }
    public decimal? SuppLimit { get; set; }
    public decimal? SuppLifetimeLimit { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public int? Version { get; set; }
    public decimal? DisabilityLimit { get; set; }
}

public record AnnualLimitRow(int Index, string? PlanCode, string? InsuredCode, string? HealthCode, string? PayorCode, string? GroupCompany,
    decimal? AnnualLimit, decimal? SuppLimit, DateTime? EffectiveDate, string? SuppLimitStatus, int? Version, decimal? LifetimeLimit,
    decimal? SuppLifetimeLimit, decimal DisabilityLimit);

public static class AnnualLimitValidator
{
    public static string? Validate(AnnualLimitRequest r, bool lifetimePlan)
    {
        static bool Blank(string? s) => string.IsNullOrWhiteSpace(s);
        var status = r.SuppLimitStatus?.Trim().ToUpperInvariant();
        if (Blank(r.PlanCode)) return "Please Enter Plan Code";
        if (Blank(r.HealthCode)) return "Please Enter Health Code";
        if (Blank(r.InsuredCode)) return "Please Enter Insured Code";
        if (r.AnnualLimit is null) return "Please Enter Annual Limit";
        if (lifetimePlan && r.LifetimeLimit is null) return "Please Enter Life Time Limit";
        if (r.EffectiveDate is null) return "Please Enter Annual Limit Effective Date";
        if (r.Version is null) return "Please Enter Annual Limit Version";
        if (status is "B" or "C" && r.SuppLimit is null) return "Please Enter Supp Premium Limit";
        if (status is "B" or "C" && r.SuppLifetimeLimit is null) return "Please Enter Supp Life Time Limit";
        if (status is not (null or "" or "A" or "B" or "C")) return "Supp Limit Status must be A, B or C.";
        if (new[] { r.AnnualLimit, r.LifetimeLimit, r.SuppLimit, r.SuppLifetimeLimit, r.DisabilityLimit }.Any(v => v < 0))
            return "Limits cannot be negative.";
        if (r.PlanCode!.Trim().Length > 4) return "Plan Code cannot be longer than 4 characters.";
        if (r.GroupCompany?.Trim().Length > 200) return "Group Company cannot be longer than 200 characters.";
        return null;
    }
}

public class AnnualLimitService(DbConnections db)
{
    public const int SearchLimit = 500;
    private static string? U(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToUpperInvariant();

    /// <summary>Validates and saves; index null = new row. Returns the AnnualIndex.</summary>
    public async Task<int> SaveAsync(AnnualLimitRequest r, int? index, string userCode)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        var saved = await SaveAsync(conn, tx, r, index, userCode);
        await tx.CommitAsync();
        return saved;
    }

    internal static async Task<int> SaveAsync(SqlConnection conn, SqlTransaction tx, AnnualLimitRequest r, int? index, string userCode)
    {
        var plan = U(r.PlanCode);
        var lifetimePlan = plan is not null && await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.PLN WHERE PLNCode = @plan AND PLNLifeTimeStatus = 'Y'", new { plan }, tx) > 0;
        if (AnnualLimitValidator.Validate(r, lifetimePlan) is { } error) throw new PlanException(error);

        var status = U(r.SuppLimitStatus);
        var p = new
        {
            index, plan, ins = U(r.InsuredCode), hlt = U(r.HealthCode), pay = U(r.PayorCode),
            grp = string.IsNullOrWhiteSpace(r.GroupCompany) ? null : r.GroupCompany.Trim(),
            annual = r.AnnualLimit, lifetime = r.LifetimeLimit, status, supp = r.SuppLimit, suppLifetime = r.SuppLifetimeLimit,
            effective = r.EffectiveDate!.Value.Date, version = r.Version, userCode, today = DateTime.Today, disability = r.DisabilityLimit ?? 0,
        };
        if (index is null)
        {
            if (await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM dbo.AnnualLimit WITH (UPDLOCK, HOLDLOCK) WHERE INSCode = @ins AND HLTCode = @hlt AND PLNCode = @plan", p, tx) > 0)
                throw new PlanException("Record Duplicated! Please Change The Corresponding Field");
            return await conn.ExecuteScalarAsync<int>(
                """
                INSERT INTO dbo.AnnualLimit (PLNCode, INSCode, HLTCode, PAYCode, GRPCompany, AnnLimit, LifeTimeLimit, SuppLimitStatus,
                    SuppLimit, SuppLifeTimeLimit, AnnualEffDate, AnnualLimitVersion, AnnualLastUpdatedUser, AnnualLastUpdatedDate, DisabilityLmt)
                VALUES (@plan, @ins, @hlt, @pay, @grp, @annual, @lifetime, @status,
                    CASE WHEN @status IN ('B','C') THEN @supp END, CASE WHEN @status IN ('B','C') THEN @suppLifetime END,
                    @effective, @version, @userCode, @today, @disability);
                SELECT CAST(SCOPE_IDENTITY() AS int);
                """, p, tx);
        }
        // Like the desktop, status A leaves any existing supplementary limits untouched.
        var updated = await conn.ExecuteAsync(
            """
            UPDATE dbo.AnnualLimit SET PLNCode = @plan, INSCode = @ins, HLTCode = @hlt, PAYCode = @pay, GRPCompany = @grp, AnnLimit = @annual,
                LifeTimeLimit = @lifetime, SuppLimitStatus = @status,
                SuppLimit = CASE WHEN @status IN ('B','C') THEN @supp ELSE SuppLimit END,
                SuppLifeTimeLimit = CASE WHEN @status IN ('B','C') THEN @suppLifetime ELSE SuppLifeTimeLimit END,
                AnnualEffDate = @effective, AnnualLimitVersion = @version, AnnualLastUpdatedUser = @userCode,
                AnnualLastUpdatedDate = @today, DisabilityLmt = @disability
            WHERE AnnualIndex = @index
            """, p, tx);
        return updated == 0 ? throw new PlanException("Record not found") : index.Value;
    }

    public async Task<(List<AnnualLimitRow> Rows, bool Truncated)> SearchAsync(string? plan, string? health, string? insured, string? payor, string? group)
    {
        await using var conn = db.MainDb();
        var rows = (await conn.QueryAsync<AnnualLimitRow>(
            $"""
            SELECT TOP ({SearchLimit + 1}) AnnualIndex AS [Index], RTRIM(PLNCode) AS PlanCode, RTRIM(INSCode) AS InsuredCode, RTRIM(HLTCode) AS HealthCode,
                RTRIM(PAYCode) AS PayorCode, GRPCompany AS GroupCompany, AnnLimit AS AnnualLimit, SuppLimit, AnnualEffDate AS EffectiveDate,
                SuppLimitStatus, AnnualLimitVersion AS Version, LifeTimeLimit AS LifetimeLimit, SuppLifeTimeLimit AS SuppLifetimeLimit,
                DisabilityLmt AS DisabilityLimit
            FROM dbo.AnnualLimit
            WHERE (@plan IS NULL OR PLNCode = @plan) AND (@health IS NULL OR HLTCode = @health) AND (@insured IS NULL OR INSCode = @insured)
              AND (@payor IS NULL OR PAYCode = @payor) AND (@group IS NULL OR GRPCompany LIKE @group + '%')
            ORDER BY AnnualIndex DESC
            """,
            new { plan = U(plan), health = U(health), insured = U(insured), payor = U(payor), group = U(group) is { } g ? SqlText.EscapeLike(g) : null })).ToList();
        var truncated = rows.Count > SearchLimit;
        return (truncated ? rows[..SearchLimit] : rows, truncated);
    }

    /// <summary>Desktop DeleteAnnual: refused while any member is on the row's plan code.</summary>
    public async Task DeleteAsync(int index)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        var plan = await conn.ExecuteScalarAsync<string?>("SELECT PLNCode FROM dbo.AnnualLimit WITH (UPDLOCK) WHERE AnnualIndex = @index", new { index }, tx)
            ?? throw new PlanException("Record not found");
        if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM (SELECT TOP 1 1 AS x FROM dbo.MBM WHERE PLNCode = @plan) m", new { plan }, tx) > 0)
            throw new PlanException("This record cannot be deleted because it is used in Member Table!");
        await conn.ExecuteAsync("DELETE FROM dbo.AnnualLimit WHERE AnnualIndex = @index", new { index }, tx);
        await tx.CommitAsync();
    }
}
