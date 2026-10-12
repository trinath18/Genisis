using Genisis.Api.Data;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Plans;

public class BenefitLimitRequest
{
    public string? PlanCode { get; set; }
    public string? HealthCode { get; set; }
    public string? InsuredCode { get; set; }
    public string? SuppStatus { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public decimal? KidneyDialysisLimit { get; set; }
    public decimal? CancerLimit { get; set; }
    public decimal? HomeNursingLimit { get; set; }
}

public record BenefitLimitRow(int Index, string? PlanCode, string? HealthCode, string? InsuredCode, DateTime? EffectiveDate,
    string? SuppStatus, decimal? KidneyDialysisLimit, decimal? CancerLimit, decimal? HomeNursingLimit);

public class MgmtFeeRequest
{
    public string? PlanCode { get; set; }
    public string? HealthCode { get; set; }
    public string? InsuredCode { get; set; }
    public decimal? Fee { get; set; }
}

public record MgmtFeeRow(string PlanCode, string HealthCode, string InsuredCode, decimal? Fee);

/// <summary>Shared checks for the plan / health / insured key the Annual Limit (Benefit) and Case Mgmt. Fee tabs use.</summary>
public static class PlanKey
{
    public const string Duplicated = "Record Duplicated! Please Change The Corresponding Field";

    public static string? U(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToUpperInvariant();

    public static string? Validate(string? plan, string? health, string? insured)
    {
        if (U(plan) is not { } p) return "Please Enter Plan Code";
        if (U(health) is not { } h) return "Please Enter Health Code";
        if (U(insured) is not { } i) return "Please Enter Insured Code";
        if (p.Length > 4) return "Plan Code cannot be longer than 4 characters.";
        if (h.Length > 2 || i.Length > 2) return "Health and Insured Code cannot be longer than 2 characters.";
        return null;
    }
}

/// <summary>Annual Limit (Benefit) tab: dbo.BnfAnnLmt, one row per plan / health / insured.</summary>
public class BenefitLimitService(DbConnections db)
{
    public static string? Validate(BenefitLimitRequest r)
    {
        if (PlanKey.Validate(r.PlanCode, r.HealthCode, r.InsuredCode) is { } error) return error;
        if (r.EffectiveDate is null) return "Please Enter Annual Limit Effective Date";
        if (PlanKey.U(r.SuppStatus) is { } s && s is not ("A" or "B" or "C")) return "Supp. Status must be A, B or C.";
        if (r.KidneyDialysisLimit < 0 || r.CancerLimit < 0 || r.HomeNursingLimit < 0) return "Limits cannot be negative.";
        return null;
    }

    public async Task<List<BenefitLimitRow>> SearchAsync(string? plan, string? health, string? insured)
    {
        await using var conn = db.MainDb();
        return await conn.ProcQueryAsync<BenefitLimitRow>("genisis.BenefitLimit_Search",
            new { plan = PlanKey.U(plan), health = PlanKey.U(health), insured = PlanKey.U(insured) });
    }

    public async Task<int> SaveAsync(int? index, BenefitLimitRequest r, string userCode)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        var saved = await SaveAsync(conn, tx, index, r, userCode);
        await tx.CommitAsync();
        return saved;
    }

    public async Task DeleteAsync(int index)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        await DeleteAsync(conn, null, index);
    }

    internal static async Task<int> SaveAsync(SqlConnection conn, SqlTransaction tx, int? index, BenefitLimitRequest r, string userCode)
    {
        if (Validate(r) is { } error) throw new PlanException(error);
        var (result, saved) = await conn.ProcSingleAsync<(int Result, int? Index)>("genisis.BenefitLimit_Save", new
        {
            index,
            plan = PlanKey.U(r.PlanCode),
            health = PlanKey.U(r.HealthCode),
            insured = PlanKey.U(r.InsuredCode),
            status = PlanKey.U(r.SuppStatus),
            effective = r.EffectiveDate!.Value.Date,
            kidney = r.KidneyDialysisLimit,
            cancer = r.CancerLimit,
            homeNursing = r.HomeNursingLimit,
            userCode,
        }, tx);
        if (result == StoredProcedures.NotFound) throw new PlanException("Record not found");
        if (result == StoredProcedures.Conflict) throw new PlanException(PlanKey.Duplicated);
        return saved!.Value;
    }

    internal static async Task DeleteAsync(SqlConnection conn, SqlTransaction? tx, int index)
    {
        if (await conn.ProcScalarAsync<int>("genisis.BenefitLimit_Delete", new { index }, tx) == StoredProcedures.NotFound)
            throw new PlanException("Record not found");
    }
}

/// <summary>Case Mgmt. Fee tab: CaseMgmtFee in the maintenance database, keyed by plan / health / insured.</summary>
public class MgmtFeeService(DbConnections db)
{
    public static string? Validate(MgmtFeeRequest r)
    {
        if (PlanKey.Validate(r.PlanCode, r.HealthCode, r.InsuredCode) is { } error) return error;
        if (r.Fee is null) return "Please Enter Mgmt. Fee";
        if (r.Fee < 0) return "Mgmt. Fee cannot be negative.";
        return null;
    }

    public async Task<List<MgmtFeeRow>> SearchAsync(string? plan, string? health, string? insured)
    {
        await using var conn = db.MainDb();
        return await conn.ProcQueryAsync<MgmtFeeRow>("genisis.MgmtFee_Search",
            new { plan = PlanKey.U(plan), health = PlanKey.U(health), insured = PlanKey.U(insured) });
    }

    public async Task SaveAsync(MgmtFeeRequest r, bool update, string userCode)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        await SaveAsync(conn, tx, r, update, userCode);
        await tx.CommitAsync();
    }

    public async Task DeleteAsync(string plan, string health, string insured)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        await DeleteAsync(conn, null, plan, health, insured);
    }

    internal static async Task SaveAsync(SqlConnection conn, SqlTransaction tx, MgmtFeeRequest r, bool update, string userCode)
    {
        if (Validate(r) is { } error) throw new PlanException(error);
        var result = await conn.ProcScalarAsync<int>("genisis.MgmtFee_Save", new
        {
            plan = PlanKey.U(r.PlanCode),
            health = PlanKey.U(r.HealthCode),
            insured = PlanKey.U(r.InsuredCode),
            fee = r.Fee,
            update,
            userCode,
        }, tx);
        if (result == StoredProcedures.NotFound) throw new PlanException("Record not found");
        if (result == StoredProcedures.Conflict) throw new PlanException(PlanKey.Duplicated);
    }

    internal static async Task DeleteAsync(SqlConnection conn, SqlTransaction? tx, string plan, string health, string insured)
    {
        var result = await conn.ProcScalarAsync<int>("genisis.MgmtFee_Delete",
            new { plan = PlanKey.U(plan), health = PlanKey.U(health), insured = PlanKey.U(insured) }, tx);
        if (result == StoredProcedures.NotFound) throw new PlanException("Record not found");
    }
}
