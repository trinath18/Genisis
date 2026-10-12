using Genisis.Api.Data;
using Genisis.Api.Plans;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Benefits;

public class BenefitCategoryRequest
{
    public string? Code { get; set; }
    public string? Description { get; set; }
}

public record BenefitCategoryRow(string Code, string? Description);

public class BenefitSubCategoryRequest
{
    public string? CategoryCode { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
}

public record BenefitSubCategoryRow(string? CategoryCode, string? CategoryDescription, string Code, string? Name);

/// <summary>Days, RM / day and RM per disability of one benefit row. RM / day and RM per disability are text in dbo.BNF
/// (RM per disability is often "FULL").</summary>
public class BenefitAmounts
{
    public string? SubCategoryCode { get; set; }
    public int? Days { get; set; }
    public string? RatePerDay { get; set; }
    public string? PerDisability { get; set; }
}

/// <summary>Benefit Entry tab: one save adds a row for every sub category line that has an RM per disability.</summary>
public class BenefitEntryRequest
{
    public string? PlanCode { get; set; }
    public string? InsuredCode { get; set; }
    public string? HealthCode { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public List<BenefitAmounts> Lines { get; set; } = [];
}

public record BenefitEntryLine(string Code, string? Name, short? Days, string? RatePerDay, string? PerDisability);

/// <summary>Benefits Listing tab: one row. Sub category, insured, plan and effective date identify it (the dbo.BNF key).</summary>
public class BenefitRequest : BenefitAmounts
{
    public string? HealthCode { get; set; }
    public string? InsuredCode { get; set; }
    public string? PlanCode { get; set; }
    public DateTime? EffectiveDate { get; set; }
}

public record BenefitRow(string SubCategoryCode, string? SubCategoryName, string HealthCode, string InsuredCode, string PlanCode,
    short? Days, string? RatePerDay, string? PerDisability, DateTime EffectiveDate, string? PayorCode, string? GroupCompany);

public class BenefitSearch
{
    public string? SubCategoryCode { get; set; }
    public string? HealthCode { get; set; }
    public string? InsuredCode { get; set; }
    public string? PlanCode { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public string? PayorCode { get; set; }
    public string? GroupCompany { get; set; }
}

/// <summary>Maintenance > Benefits (legacy frmMaintenanceBenefits).</summary>
public class BenefitService(DbConnections db)
{
    public const int SearchLimit = 500;
    public const string Duplicated = "Record Duplicated! Please Change The Corresponding Field";
    public const string SetExists = Duplicated + " (this plan, insured type and effective date already have benefits; change them on the Benefits Listing tab)";

    private static string? U(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToUpperInvariant();
    private static string? Text(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static string? Like(string? s) => U(s) is { } v ? SqlText.EscapeLike(v) : null;

    // ---------- Category tab ----------

    public static string? Validate(BenefitCategoryRequest r)
    {
        if (U(r.Code) is not { } code) return "Please Enter Category Code";
        if (Text(r.Description) is not { } description) return "Please Enter Category Description";
        if (code.Length > 10) return "Category Code cannot be longer than 10 characters.";
        if (description.Length > 255) return "Description cannot be longer than 255 characters.";
        return null;
    }

    public async Task<List<BenefitCategoryRow>> SearchCategoriesAsync(string? code, string? description)
    {
        await using var conn = db.MainDb();
        return await conn.ProcQueryAsync<BenefitCategoryRow>("genisis.BenefitCategory_Search", new { code = Like(code), description = Like(description) });
    }

    public Task SaveCategoryAsync(BenefitCategoryRequest r, bool update, string userCode) =>
        InTransaction((conn, tx) => SaveCategoryAsync(conn, tx, r, update, userCode));

    public Task DeleteCategoryAsync(string code) => InTransaction((conn, tx) => DeleteCategoryAsync(conn, tx, code));

    internal static async Task SaveCategoryAsync(SqlConnection conn, SqlTransaction tx, BenefitCategoryRequest r, bool update, string userCode)
    {
        if (Validate(r) is { } error) throw new PlanException(error);
        var result = await conn.ProcScalarAsync<int>("genisis.BenefitCategory_Save",
            new { code = U(r.Code), description = Text(r.Description), update, userCode }, tx);
        if (result == StoredProcedures.NotFound) throw new PlanException("Record not found");
        if (result == StoredProcedures.Conflict) throw new PlanException(Duplicated);
    }

    internal static async Task DeleteCategoryAsync(SqlConnection conn, SqlTransaction tx, string code)
    {
        var (result, usedIn) = await conn.ProcSingleAsync<(int Result, string? UsedIn)>("genisis.BenefitCategory_Delete", new { code = U(code) }, tx);
        ThrowIfNotDeleted(result, usedIn);
    }

    // ---------- Sub Category tab ----------

    public static string? Validate(BenefitSubCategoryRequest r)
    {
        if (U(r.CategoryCode) is null) return "Please Enter Category Code";
        if (U(r.Code) is not { } code) return "Please Enter Sub Category Code";
        if (Text(r.Name) is not { } name) return "Please Enter Sub Category Description";
        if (code.Length > 10) return "Sub Category Code cannot be longer than 10 characters.";
        if (name.Length > 100) return "Description cannot be longer than 100 characters.";
        return null;
    }

    public async Task<List<BenefitSubCategoryRow>> SearchSubCategoriesAsync(string? category, string? code, string? name)
    {
        await using var conn = db.MainDb();
        return await conn.ProcQueryAsync<BenefitSubCategoryRow>("genisis.BenefitSubCategory_Search",
            new { category = U(category), code = Like(code), name = Like(name) });
    }

    public Task SaveSubCategoryAsync(BenefitSubCategoryRequest r, bool update, string userCode) =>
        InTransaction((conn, tx) => SaveSubCategoryAsync(conn, tx, r, update, userCode));

    public Task DeleteSubCategoryAsync(string code) => InTransaction((conn, tx) => DeleteSubCategoryAsync(conn, tx, code));

    internal static async Task SaveSubCategoryAsync(SqlConnection conn, SqlTransaction tx, BenefitSubCategoryRequest r, bool update, string userCode)
    {
        if (Validate(r) is { } error) throw new PlanException(error);
        var (result, missing) = await conn.ProcSingleAsync<(int Result, string? Missing)>("genisis.BenefitSubCategory_Save",
            new { category = U(r.CategoryCode), code = U(r.Code), name = Text(r.Name), update, userCode }, tx);
        if (result == StoredProcedures.NotFound) throw new PlanException(missing == "Category" ? "Category Code was not found." : "Record not found");
        if (result == StoredProcedures.Conflict) throw new PlanException(Duplicated);
    }

    internal static async Task DeleteSubCategoryAsync(SqlConnection conn, SqlTransaction tx, string code)
    {
        var (result, usedIn) = await conn.ProcSingleAsync<(int Result, string? UsedIn)>("genisis.BenefitSubCategory_Delete", new { code = U(code) }, tx);
        ThrowIfNotDeleted(result, usedIn);
    }

    private static void ThrowIfNotDeleted(int result, string? usedIn)
    {
        if (result == StoredProcedures.NotFound) throw new PlanException("Record not found");
        if (result == StoredProcedures.Conflict) throw new PlanException($"This record cannot be deleted because it is used in {usedIn} table!");
    }

    // ---------- Benefit Entry and Benefits Listing tabs ----------

    private static string? ValidateKey(string? health, string? insured, string? plan, DateTime? effective)
    {
        if (U(health) is not { } h) return "Please Enter Health Type";
        if (U(insured) is not { } i) return "Please Enter Insured Type";
        if (U(plan) is not { } p) return "Please Enter Plan Code";
        if (effective is null) return "Please Enter a valid Effective Date";
        if (h.Length > 2 || i.Length > 2) return "Health and Insured Type cannot be longer than 2 characters.";
        if (p.Length > 4) return "Plan Code cannot be longer than 4 characters.";
        return null;
    }

    private static string? ValidateAmounts(BenefitAmounts a, string prefix = "")
    {
        if (Text(a.PerDisability) is null) return $"{prefix}Please Enter RM Per Disability";
        if (a.Days is < 0 or > short.MaxValue) return $"{prefix}Days/Months must be a whole number from 0 to {short.MaxValue}.";
        if (Text(a.RatePerDay)?.Length > 15) return $"{prefix}RM / Day cannot be longer than 15 characters.";
        if (Text(a.PerDisability)!.Length > 50) return $"{prefix}RM Per Disability cannot be longer than 50 characters.";
        return null;
    }

    /// <summary>A line counts when any amount is filled; like the desktop, it is saved only with an RM per disability.</summary>
    private static bool Filled(BenefitAmounts a) => a.Days is not null || Text(a.RatePerDay) is not null || Text(a.PerDisability) is not null;

    public static string? Validate(BenefitEntryRequest r)
    {
        if (ValidateKey(r.HealthCode, r.InsuredCode, r.PlanCode, r.EffectiveDate) is { } error) return error;
        var lines = (r.Lines ?? []).Where(Filled).ToList();
        if (lines.Count == 0) return "Please Enter RM Per Disability for at least one sub category.";
        if (lines.Any(l => U(l.SubCategoryCode) is null)) return "Please Enter Benefit Sub Category Code";
        if (lines.GroupBy(l => U(l.SubCategoryCode)).FirstOrDefault(g => g.Count() > 1) is { } dup) return $"Sub category {dup.Key} is entered more than once.";
        return lines.Select(l => ValidateAmounts(l, $"Sub category {U(l.SubCategoryCode)}: ")).FirstOrDefault(e => e is not null);
    }

    public static string? Validate(BenefitRequest r)
    {
        if (U(r.SubCategoryCode) is null) return "Please Enter Benefit Sub Category Code";
        return ValidateKey(r.HealthCode, r.InsuredCode, r.PlanCode, r.EffectiveDate) ?? ValidateAmounts(r);
    }

    public async Task<(List<BenefitEntryLine> Lines, bool SetExists)> LoadEntryAsync(string? plan, string? insured, string? health, DateTime? effective)
    {
        if (ValidateKey(health, insured, plan, effective) is { } error) throw new PlanException(error);
        await using var conn = db.MainDb();
        var p = new { plan = U(plan), insured = U(insured), health = U(health), effective = effective!.Value.Date };
        var lines = await conn.ProcQueryAsync<BenefitEntryLine>("genisis.BenefitEntry_Load", p);
        var exists = await conn.ProcScalarAsync<bool>("genisis.BenefitEntry_SetExists", new { p.plan, p.insured, p.effective });
        return (lines, exists);
    }

    public Task<int> SaveEntryAsync(BenefitEntryRequest r, string userCode) => InTransaction((conn, tx) => SaveEntryAsync(conn, tx, r, userCode));

    /// <summary>Adds the benefit set; returns the number of rows saved. Refused when the plan / insured / date already has benefits.</summary>
    internal static async Task<int> SaveEntryAsync(SqlConnection conn, SqlTransaction tx, BenefitEntryRequest r, string userCode)
    {
        if (Validate(r) is { } error) throw new PlanException(error);
        var effective = r.EffectiveDate!.Value.Date;
        if (await conn.ProcScalarAsync<bool>("genisis.BenefitEntry_SetExists", new { plan = U(r.PlanCode), insured = U(r.InsuredCode), effective }, tx))
            throw new PlanException(SetExists);
        var lines = r.Lines.Where(Filled).ToList();
        foreach (var line in lines)
            await InsertAsync(conn, tx, line, r.HealthCode, r.InsuredCode, r.PlanCode, effective, version: false, userCode);
        return lines.Count;
    }

    public async Task<(List<BenefitRow> Rows, bool Truncated)> SearchAsync(BenefitSearch s)
    {
        await using var conn = db.MainDb();
        var rows = await conn.ProcQueryAsync<BenefitRow>("genisis.Benefit_Search", new
        {
            top = SearchLimit + 1,
            subCategory = U(s.SubCategoryCode),
            health = U(s.HealthCode),
            insured = U(s.InsuredCode),
            plan = U(s.PlanCode),
            effective = s.EffectiveDate?.Date,
            payor = U(s.PayorCode),
            group = Like(s.GroupCompany),
        });
        var truncated = rows.Count > SearchLimit;
        return (truncated ? rows[..SearchLimit] : rows, truncated);
    }

    public Task CreateAsync(BenefitRequest r, string userCode) => InTransaction((conn, tx) => CreateAsync(conn, tx, r, userCode));

    public Task UpdateAsync(BenefitRequest r, string userCode) => InTransaction((conn, tx) => UpdateAsync(conn, tx, r, userCode));

    public Task DeleteAsync(string? subCategory, string? insured, string? plan, DateTime? effective) =>
        InTransaction((conn, tx) => DeleteAsync(conn, tx, subCategory, insured, plan, effective));

    internal static async Task CreateAsync(SqlConnection conn, SqlTransaction tx, BenefitRequest r, string userCode)
    {
        if (Validate(r) is { } error) throw new PlanException(error);
        await InsertAsync(conn, tx, r, r.HealthCode, r.InsuredCode, r.PlanCode, r.EffectiveDate!.Value.Date, version: true, userCode);
    }

    /// <summary>Changes days, RM / day and RM per disability; the key and health type stay fixed.</summary>
    internal static async Task UpdateAsync(SqlConnection conn, SqlTransaction tx, BenefitRequest r, string userCode)
    {
        if (Validate(r) is { } error) throw new PlanException(error);
        var result = await conn.ProcScalarAsync<int>("genisis.Benefit_Update", new
        {
            subCategory = U(r.SubCategoryCode), insured = U(r.InsuredCode), plan = U(r.PlanCode), effective = r.EffectiveDate!.Value.Date,
            days = (short?)r.Days, ratePerDay = Text(r.RatePerDay), perDisability = U(r.PerDisability), userCode,
        }, tx);
        if (result == StoredProcedures.NotFound) throw new PlanException("Record not found");
    }

    /// <summary>Deletes one row (the desktop deleted every row of the sub category, across all plans).</summary>
    internal static async Task DeleteAsync(SqlConnection conn, SqlTransaction tx, string? subCategory, string? insured, string? plan, DateTime? effective)
    {
        if (U(subCategory) is null || U(insured) is null || U(plan) is null || effective is null) throw new PlanException("Record not found");
        var result = await conn.ProcScalarAsync<int>("genisis.Benefit_Delete",
            new { subCategory = U(subCategory), insured = U(insured), plan = U(plan), effective = effective.Value.Date }, tx);
        if (result == StoredProcedures.NotFound) throw new PlanException("Record not found");
        if (result == StoredProcedures.Conflict) throw new PlanException("This record cannot be deleted because it is used in Member table!");
    }

    private static async Task InsertAsync(SqlConnection conn, SqlTransaction tx, BenefitAmounts a, string? health, string? insured, string? plan,
        DateTime effective, bool version, string userCode)
    {
        var code = U(a.SubCategoryCode);
        var (result, missing) = await conn.ProcSingleAsync<(int Result, string? Missing)>("genisis.Benefit_Insert", new
        {
            subCategory = code, health = U(health), insured = U(insured), plan = U(plan), effective,
            days = (short?)a.Days, ratePerDay = Text(a.RatePerDay), perDisability = U(a.PerDisability), version, userCode,
        }, tx);
        if (result == StoredProcedures.NotFound) throw new PlanException($"{missing} was not found.");
        if (result == StoredProcedures.Conflict) throw new PlanException($"{Duplicated} (sub category {code})");
    }

    private async Task InTransaction(Func<SqlConnection, SqlTransaction, Task> work) => await InTransaction(async (c, t) => { await work(c, t); return 0; });

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
