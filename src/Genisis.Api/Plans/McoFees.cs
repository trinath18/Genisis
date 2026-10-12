using Genisis.Api.Data;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Plans;

public class McoFeeAmounts
{
    public string? InsuredCode { get; set; }
    public decimal? Adult { get; set; }
    public decimal? Child { get; set; }
    public decimal? SuppAdult { get; set; }
    public decimal? SuppChild { get; set; }
}

public class McoFeeCreateRequest
{
    public string? PlanCode { get; set; }
    public string? HealthCode { get; set; }
    public string? SuppStatus { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public string? CoveredIdCharge { get; set; }
    public List<McoFeeAmounts> Rows { get; set; } = [];
}

public class McoFeeUpdateRequest
{
    public DateTime? EffectiveDate { get; set; }
    public decimal? Adult { get; set; }
    public decimal? Child { get; set; }
    public decimal? SuppAdult { get; set; }
    public decimal? SuppChild { get; set; }
    public string? CoveredIdCharge { get; set; }
}

public record McoFeeRow(int Index, string? PlanCode, string? HealthCode, string? InsuredCode, string? SuppStatus, decimal? Adult,
    decimal? Child, decimal? SuppAdult, decimal? SuppChild, DateTime? EffectiveDate, string? CoveredIdCharge);

/// <summary>MCO Fee tab. Like the desktop, one save adds a row per insured type (I, F, G, H) that has an adult fee;
/// supplementary fees apply to F and H for status B/C/D, and the covered ID charge to F and H for status D.</summary>
public class McoFeeService(DbConnections db)
{
    public const int SearchLimit = 500;
    public static readonly string[] InsuredCodes = ["I", "F", "G", "H"];
    private static bool Family(string? ins) => ins is "F" or "H";

    public static string? Validate(McoFeeCreateRequest r)
    {
        var status = PlanKey.U(r.SuppStatus) ?? "A";
        if (PlanKey.U(r.HealthCode) is null) return "Please Enter Health Code";
        if (PlanKey.U(r.PlanCode) is null) return "Please Enter Plan Code";
        if (status is not ("A" or "B" or "C" or "D")) return "Supp. MCO Status must be A, B, C or D.";
        if (status == "D" && PlanKey.U(r.CoveredIdCharge) is null) return "Please Enter Covered ID Charge";
        if (r.EffectiveDate is null) return "Please validate your MCO Effective date";
        var rows = r.Rows ?? [];
        if (rows.Any(x => !InsuredCodes.Contains(PlanKey.U(x.InsuredCode)))) return "Insured type must be I, F, G or H.";
        if (rows.GroupBy(x => PlanKey.U(x.InsuredCode)).Any(g => g.Count() > 1)) return "Each insured type can only be entered once.";
        if (!rows.Any(x => x.Adult is not null)) return "Please Enter MCO Fees ";
        if (rows.Any(x => x.Adult < 0 || x.Child < 0 || x.SuppAdult < 0 || x.SuppChild < 0)) return "MCO Fees cannot be negative.";
        return Lengths(r.PlanCode, r.HealthCode, r.CoveredIdCharge);
    }

    public static string? Validate(McoFeeUpdateRequest r)
    {
        if (r.EffectiveDate is null) return "Please validate your MCO Effective date";
        if (r.Adult is null) return "Please Enter MCO Fees ";
        if (r.Adult < 0 || r.Child < 0 || r.SuppAdult < 0 || r.SuppChild < 0) return "MCO Fees cannot be negative.";
        return PlanKey.U(r.CoveredIdCharge)?.Length > 2 ? "Covered ID Charge cannot be longer than 2 characters." : null;
    }

    private static string? Lengths(string? plan, string? health, string? covered)
    {
        if (PlanKey.U(plan)!.Length > 4) return "Plan Code cannot be longer than 4 characters.";
        if (PlanKey.U(health)!.Length > 2) return "Health Code cannot be longer than 2 characters.";
        if (PlanKey.U(covered)?.Length > 2) return "Covered ID Charge cannot be longer than 2 characters.";
        return null;
    }

    public async Task<(List<McoFeeRow> Rows, bool Truncated)> SearchAsync(string? plan, string? health, string? insured, DateTime? effective)
    {
        await using var conn = db.MainDb();
        var rows = await conn.ProcQueryAsync<McoFeeRow>("genisis.McoFee_Search", new
        {
            top = SearchLimit + 1, plan = PlanKey.U(plan), health = PlanKey.U(health), insured = PlanKey.U(insured), effective = effective?.Date,
        });
        var truncated = rows.Count > SearchLimit;
        return (truncated ? rows[..SearchLimit] : rows, truncated);
    }

    public Task<List<int>> CreateAsync(McoFeeCreateRequest r, string userCode) => InTransaction((conn, tx) => CreateAsync(conn, tx, r, userCode));

    public Task UpdateAsync(int index, McoFeeUpdateRequest r, string userCode) =>
        InTransaction(async (conn, tx) => { await UpdateAsync(conn, tx, index, r, userCode); return 0; });

    public Task DeleteAsync(int index) => InTransaction(async (conn, tx) => { await DeleteAsync(conn, tx, index); return 0; });

    /// <summary>Adds one MCO row per insured type with an adult fee; returns the new MCOCodes. The duplicate check uses the
    /// plan / health / insured / status / date (the desktop also required the same adult fee, so a changed fee slipped through).</summary>
    internal static async Task<List<int>> CreateAsync(SqlConnection conn, SqlTransaction tx, McoFeeCreateRequest r, string userCode)
    {
        if (Validate(r) is { } error) throw new PlanException(error);
        var status = PlanKey.U(r.SuppStatus) ?? "A";
        var codes = new List<int>();
        foreach (var row in r.Rows.Where(x => x.Adult is not null).OrderBy(x => Array.IndexOf(InsuredCodes, PlanKey.U(x.InsuredCode))))
        {
            var ins = PlanKey.U(row.InsuredCode)!;
            var supp = status != "A" && Family(ins);
            var (result, index) = await conn.ProcSingleAsync<(int Result, int? Index)>("genisis.McoFee_Insert", new
            {
                plan = PlanKey.U(r.PlanCode), health = PlanKey.U(r.HealthCode), insured = ins, status, effective = r.EffectiveDate!.Value.Date,
                adult = row.Adult, child = row.Child, suppAdult = supp ? row.SuppAdult : null, suppChild = supp ? row.SuppChild : null,
                coveredIdCharge = status == "D" && Family(ins) ? PlanKey.U(r.CoveredIdCharge) : null, userCode,
            }, tx);
            if (result == StoredProcedures.NotFound) throw new PlanException("Please validate your Plan Code");
            if (result == StoredProcedures.Conflict) throw new PlanException($"{PlanKey.Duplicated} (insured type {ins})");
            codes.Add(index!.Value);
        }
        return codes;
    }

    internal static async Task UpdateAsync(SqlConnection conn, SqlTransaction tx, int index, McoFeeUpdateRequest r, string userCode)
    {
        if (Validate(r) is { } error) throw new PlanException(error);
        var p = new
        {
            index, effective = r.EffectiveDate!.Value.Date, adult = r.Adult, child = r.Child, suppAdult = r.SuppAdult, suppChild = r.SuppChild,
            coveredIdCharge = PlanKey.U(r.CoveredIdCharge), userCode,
        };
        switch (await conn.ProcScalarAsync<int>("genisis.McoFee_Update", p, tx))
        {
            case StoredProcedures.NotFound: throw new PlanException("Record not found");
            case StoredProcedures.Conflict: throw new PlanException(PlanKey.Duplicated);
        }
    }

    /// <summary>Refused while a member is on the row's plan / health / insured (the desktop's check used the wrong columns).</summary>
    internal static async Task DeleteAsync(SqlConnection conn, SqlTransaction tx, int index)
    {
        switch (await conn.ProcScalarAsync<int>("genisis.McoFee_Delete", new { index }, tx))
        {
            case StoredProcedures.NotFound: throw new PlanException("Record not found");
            case StoredProcedures.Conflict: throw new PlanException("This record cannot be deleted because it is used in Member Table!");
        }
    }

    private async Task<T> InTransaction<T>(Func<SqlConnection, SqlTransaction, Task<T>> work)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        var result = await work(conn, tx);
        await tx.CommitAsync();
        return result;
    }
}
