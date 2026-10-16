using Genisis.Api.Data;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Benefits;

public interface IBenefitService
{
    Task<BenefitLookups> GetLookupsAsync(CancellationToken cancellationToken);
    Task<List<BenefitCategoryRow>> SearchCategoriesAsync(string? code, string? description, CancellationToken cancellationToken);
    Task SaveCategoryAsync(BenefitCategoryRequest request, bool update, string userCode, CancellationToken cancellationToken);
    Task DeleteCategoryAsync(string code, CancellationToken cancellationToken);
    Task<List<BenefitSubCategoryRow>> SearchSubCategoriesAsync(string? category, string? code, string? name, CancellationToken cancellationToken);
    Task SaveSubCategoryAsync(BenefitSubCategoryRequest request, bool update, string userCode, CancellationToken cancellationToken);
    Task DeleteSubCategoryAsync(string code, CancellationToken cancellationToken);
    Task<BenefitEntry> LoadEntryAsync(string plan, string insured, string health, DateTime effective, CancellationToken cancellationToken);
    /// <returns>The number of rows saved.</returns>
    Task<int> SaveEntryAsync(BenefitEntryRequest request, string userCode, CancellationToken cancellationToken);
    Task<BenefitSearchResult> SearchAsync(BenefitSearch search, CancellationToken cancellationToken);
    Task CreateAsync(BenefitRequest request, string userCode, CancellationToken cancellationToken);
    Task UpdateAsync(BenefitRequest request, string userCode, CancellationToken cancellationToken);
    Task DeleteAsync(BenefitKey key, CancellationToken cancellationToken);
}

/// <summary>
/// Maintenance &gt; Benefits (legacy frmMaintenanceBenefits). Requests are validated by <see cref="BenefitValidator"/> in the controller;
/// rules that need the database (duplicates, references, rows in use) raise <see cref="BenefitException"/>.
/// </summary>
public sealed class BenefitService(DbConnections db) : IBenefitService
{
    private static string? Code(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? Like(string? value) => Code(value) is { } code ? SqlText.EscapeLike(code) : null;

    public async Task<BenefitLookups> GetLookupsAsync(CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        using var grid = await conn.ProcMultipleAsync("genisis.Benefits_Lookups");
        return new BenefitLookups(
            (await grid.ReadAsync<BenefitOption>()).ToList(),
            (await grid.ReadAsync<BenefitOption>()).ToList(),
            (await grid.ReadAsync<BenefitOption>()).ToList(),
            (await grid.ReadAsync<BenefitOption>()).ToList(),
            (await grid.ReadAsync<BenefitOption>()).ToList(),
            (await grid.ReadAsync<string>()).ToList());
    }

    // ---------- Category tab ----------

    public async Task<List<BenefitCategoryRow>> SearchCategoriesAsync(string? code, string? description, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        return await conn.ProcQueryAsync<BenefitCategoryRow>("genisis.BenefitCategory_Search", new { code = Like(code), description = Like(description) });
    }

    public Task SaveCategoryAsync(BenefitCategoryRequest request, bool update, string userCode, CancellationToken cancellationToken) =>
        InTransactionAsync((conn, tx) => SaveCategoryAsync(conn, tx, request, update, userCode), cancellationToken);

    public Task DeleteCategoryAsync(string code, CancellationToken cancellationToken) =>
        InTransactionAsync((conn, tx) => DeleteCategoryAsync(conn, tx, code), cancellationToken);

    internal static async Task SaveCategoryAsync(SqlConnection conn, SqlTransaction tx, BenefitCategoryRequest request, bool update, string userCode)
    {
        var result = await conn.ProcScalarAsync<int>("genisis.BenefitCategory_Save",
            new { code = Code(request.Code), description = Text(request.Description), update, userCode }, tx);
        if (result == StoredProcedures.NotFound) throw new BenefitException(BenefitMessages.NotFound);
        if (result == StoredProcedures.Conflict) throw new BenefitException(BenefitMessages.Duplicated);
    }

    internal static async Task DeleteCategoryAsync(SqlConnection conn, SqlTransaction tx, string code)
    {
        var (result, usedIn) = await conn.ProcSingleAsync<(int Result, string? UsedIn)>("genisis.BenefitCategory_Delete", new { code = Code(code) }, tx);
        ThrowIfNotDeleted(result, usedIn);
    }

    // ---------- Sub Category tab ----------

    public async Task<List<BenefitSubCategoryRow>> SearchSubCategoriesAsync(string? category, string? code, string? name, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        return await conn.ProcQueryAsync<BenefitSubCategoryRow>("genisis.BenefitSubCategory_Search",
            new { category = Code(category), code = Like(code), name = Like(name) });
    }

    public Task SaveSubCategoryAsync(BenefitSubCategoryRequest request, bool update, string userCode, CancellationToken cancellationToken) =>
        InTransactionAsync((conn, tx) => SaveSubCategoryAsync(conn, tx, request, update, userCode), cancellationToken);

    public Task DeleteSubCategoryAsync(string code, CancellationToken cancellationToken) =>
        InTransactionAsync((conn, tx) => DeleteSubCategoryAsync(conn, tx, code), cancellationToken);

    internal static async Task SaveSubCategoryAsync(SqlConnection conn, SqlTransaction tx, BenefitSubCategoryRequest request, bool update, string userCode)
    {
        var (result, missing) = await conn.ProcSingleAsync<(int Result, string? Missing)>("genisis.BenefitSubCategory_Save",
            new { category = Code(request.CategoryCode), code = Code(request.Code), name = Text(request.Name), update, userCode }, tx);
        if (result == StoredProcedures.NotFound)
            throw new BenefitException(missing == BenefitMissingReferences.Category ? BenefitMessages.CategoryNotFound : BenefitMessages.NotFound);
        if (result == StoredProcedures.Conflict) throw new BenefitException(BenefitMessages.Duplicated);
    }

    internal static async Task DeleteSubCategoryAsync(SqlConnection conn, SqlTransaction tx, string code)
    {
        var (result, usedIn) = await conn.ProcSingleAsync<(int Result, string? UsedIn)>("genisis.BenefitSubCategory_Delete", new { code = Code(code) }, tx);
        ThrowIfNotDeleted(result, usedIn);
    }

    private static void ThrowIfNotDeleted(int result, string? usedIn)
    {
        if (result == StoredProcedures.NotFound) throw new BenefitException(BenefitMessages.NotFound);
        if (result == StoredProcedures.Conflict) throw new BenefitException(BenefitMessages.UsedIn(usedIn));
    }

    // ---------- Benefit Entry and Benefits Listing tabs ----------

    public async Task<BenefitEntry> LoadEntryAsync(string plan, string insured, string health, DateTime effective, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        var key = new { plan = Code(plan), insured = Code(insured), health = Code(health), effective = effective.Date };
        var lines = await conn.ProcQueryAsync<BenefitEntryLine>("genisis.BenefitEntry_Load", key);
        var exists = await conn.ProcScalarAsync<bool>("genisis.BenefitEntry_SetExists", new { key.plan, key.insured, key.effective });
        return new BenefitEntry(lines, exists);
    }

    public Task<int> SaveEntryAsync(BenefitEntryRequest request, string userCode, CancellationToken cancellationToken) =>
        InTransactionAsync((conn, tx) => SaveEntryAsync(conn, tx, request, userCode), cancellationToken);

    /// <summary>Adds the benefit set; returns the number of rows saved. Refused when the plan / insured / date already has benefits.</summary>
    internal static async Task<int> SaveEntryAsync(SqlConnection conn, SqlTransaction tx, BenefitEntryRequest request, string userCode)
    {
        var effective = request.EffectiveDate!.Value.Date;
        if (await conn.ProcScalarAsync<bool>("genisis.BenefitEntry_SetExists", new { plan = Code(request.PlanCode), insured = Code(request.InsuredCode), effective }, tx))
            throw new BenefitException(BenefitMessages.SetExists);
        var lines = request.Lines.Where(BenefitValidator.IsFilled).ToList();
        foreach (var line in lines)
            await InsertAsync(conn, tx, line, request.HealthCode, request.InsuredCode, request.PlanCode, effective, version: false, userCode);
        return lines.Count;
    }

    public async Task<BenefitSearchResult> SearchAsync(BenefitSearch search, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        var rows = await conn.ProcQueryAsync<BenefitRow>("genisis.Benefit_Search", new
        {
            top = BenefitLimits.SearchRows + 1,
            subCategory = Code(search.SubCategoryCode),
            health = Code(search.HealthCode),
            insured = Code(search.InsuredCode),
            plan = Code(search.PlanCode),
            effective = search.EffectiveDate?.Date,
            payor = Code(search.PayorCode),
            group = Like(search.GroupCompany),
        });
        var truncated = rows.Count > BenefitLimits.SearchRows;
        return new BenefitSearchResult(truncated ? rows[..BenefitLimits.SearchRows] : rows, truncated, BenefitLimits.SearchRows);
    }

    public Task CreateAsync(BenefitRequest request, string userCode, CancellationToken cancellationToken) =>
        InTransactionAsync((conn, tx) => CreateAsync(conn, tx, request, userCode), cancellationToken);

    public Task UpdateAsync(BenefitRequest request, string userCode, CancellationToken cancellationToken) =>
        InTransactionAsync((conn, tx) => UpdateAsync(conn, tx, request, userCode), cancellationToken);

    public Task DeleteAsync(BenefitKey key, CancellationToken cancellationToken) =>
        InTransactionAsync((conn, tx) => DeleteAsync(conn, tx, key), cancellationToken);

    internal static Task CreateAsync(SqlConnection conn, SqlTransaction tx, BenefitRequest request, string userCode) =>
        InsertAsync(conn, tx, request, request.HealthCode, request.InsuredCode, request.PlanCode, request.EffectiveDate!.Value.Date, version: true, userCode);

    /// <summary>Changes days, RM / day and RM per disability; the key and health type stay fixed.</summary>
    internal static async Task UpdateAsync(SqlConnection conn, SqlTransaction tx, BenefitRequest request, string userCode)
    {
        var result = await conn.ProcScalarAsync<int>("genisis.Benefit_Update", new
        {
            subCategory = Code(request.SubCategoryCode), insured = Code(request.InsuredCode), plan = Code(request.PlanCode),
            effective = request.EffectiveDate!.Value.Date, days = (short?)request.Days, ratePerDay = Text(request.RatePerDay),
            perDisability = Code(request.PerDisability), userCode,
        }, tx);
        if (result == StoredProcedures.NotFound) throw new BenefitException(BenefitMessages.NotFound);
    }

    /// <summary>Deletes one row (the desktop deleted every row of the sub category, across all plans).</summary>
    internal static async Task DeleteAsync(SqlConnection conn, SqlTransaction tx, BenefitKey key)
    {
        var result = await conn.ProcScalarAsync<int>("genisis.Benefit_Delete", new
        {
            subCategory = Code(key.SubCategoryCode), insured = Code(key.InsuredCode), plan = Code(key.PlanCode), effective = key.EffectiveDate!.Value.Date,
        }, tx);
        if (result == StoredProcedures.NotFound) throw new BenefitException(BenefitMessages.NotFound);
        if (result == StoredProcedures.Conflict) throw new BenefitException(BenefitMessages.UsedByMembers);
    }

    private static async Task InsertAsync(SqlConnection conn, SqlTransaction tx, BenefitAmounts amounts, string? health, string? insured, string? plan,
        DateTime effective, bool version, string userCode)
    {
        var subCategory = Code(amounts.SubCategoryCode);
        var (result, missing) = await conn.ProcSingleAsync<(int Result, string? Missing)>("genisis.Benefit_Insert", new
        {
            subCategory, health = Code(health), insured = Code(insured), plan = Code(plan), effective,
            days = (short?)amounts.Days, ratePerDay = Text(amounts.RatePerDay), perDisability = Code(amounts.PerDisability), version, userCode,
        }, tx);
        if (result == StoredProcedures.NotFound) throw new BenefitException($"{missing} was not found.");
        if (result == StoredProcedures.Conflict) throw new BenefitException($"{BenefitMessages.Duplicated} (sub category {subCategory})");
    }

    private Task InTransactionAsync(Func<SqlConnection, SqlTransaction, Task> work, CancellationToken cancellationToken) =>
        InTransactionAsync(async (conn, tx) => { await work(conn, tx); return 0; }, cancellationToken);

    private async Task<T> InTransactionAsync<T>(Func<SqlConnection, SqlTransaction, Task<T>> work, CancellationToken cancellationToken)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync(cancellationToken);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(cancellationToken);
        var result = await work(conn, tx);
        await tx.CommitAsync(cancellationToken);
        return result;
    }
}
