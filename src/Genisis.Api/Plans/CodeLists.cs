using Genisis.Api.Data;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Plans;

/// <summary>A Plan Maintenance tab that keeps a code and a description (Health Type, Insured Type, Product Category,
/// Supp. MCO Status). Procedure is the prefix of its genisis.*_Search / _Save / _Delete procedures.</summary>
public record CodeList(string Name, string Procedure, int CodeLength, int DescriptionLength,
    string CodeRequired, string DescriptionRequired, string Duplicate);

public class CodeRequest
{
    public string? Code { get; set; }
    public string? Description { get; set; }
}

public record CodeRow(string Code, string? Description);

public static class CodeLists
{
    private const string Duplicated = "Record Duplicated! Please Change The Corresponding Field";

    public static readonly IReadOnlyDictionary<string, CodeList> All = new CodeList[]
    {
        new("health-types", "HealthType", 2, 100, "Please Enter Health Type", "Please Enter Health Description", Duplicated),
        new("insured-types", "InsuredType", 2, 100, "Please Enter Insured Type", "Please Enter Insured Type Description", Duplicated),
        new("product-categories", "ProductCategory", 2, 50, "Please Enter Product Code", "Please Enter Product Name",
            "Duplicate Data! Please select another Product Code."),
        new("supp-mco-statuses", "SuppMcoStatus", 1, 200, "Please Enter MCO Code", "Please enter MCO Status.",
            "Duplicate Data! Please select another MCO Code."),
    }.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);

    public static string? Validate(CodeList list, CodeRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Code)) return list.CodeRequired;
        if (string.IsNullOrWhiteSpace(r.Description)) return list.DescriptionRequired;
        if (r.Code.Trim().Length > list.CodeLength) return $"Code cannot be longer than {list.CodeLength} characters.";
        if (r.Description.Trim().Length > list.DescriptionLength) return $"Description cannot be longer than {list.DescriptionLength} characters.";
        return null;
    }
}

public class CodeListService(DbConnections db)
{
    private static string? U(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToUpperInvariant();

    public async Task<List<CodeRow>> SearchAsync(CodeList list, string? code)
    {
        await using var conn = db.MainDb();
        return await conn.ProcQueryAsync<CodeRow>($"genisis.{list.Procedure}_Search", new { code = U(code) });
    }

    public Task SaveAsync(CodeList list, CodeRequest r, bool update, string userCode) =>
        InTransaction((conn, tx) => SaveAsync(conn, tx, list, r, update, userCode));

    public Task DeleteAsync(CodeList list, string code) => InTransaction((conn, tx) => DeleteAsync(conn, tx, list, code));

    internal static async Task SaveAsync(SqlConnection conn, SqlTransaction tx, CodeList list, CodeRequest r, bool update, string userCode)
    {
        if (CodeLists.Validate(list, r) is { } error) throw new PlanException(error);
        var result = await conn.ProcScalarAsync<int>($"genisis.{list.Procedure}_Save",
            new { code = U(r.Code), description = r.Description!.Trim(), update, userCode }, tx);
        if (result == StoredProcedures.NotFound) throw new PlanException("Record not found");
        if (result == StoredProcedures.Conflict) throw new PlanException(list.Duplicate);
    }

    internal static async Task DeleteAsync(SqlConnection conn, SqlTransaction tx, CodeList list, string code)
    {
        var (result, usedIn) = await conn.ProcSingleAsync<(int Result, string? UsedIn)>($"genisis.{list.Procedure}_Delete", new { code = U(code) }, tx);
        if (result == StoredProcedures.NotFound) throw new PlanException("Record not found");
        if (result == StoredProcedures.Conflict) throw new PlanException($"This record cannot be deleted because it is used in {usedIn} table!");
    }

    private async Task InTransaction(Func<SqlConnection, SqlTransaction, Task> action)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        await action(conn, tx);
        await tx.CommitAsync();
    }
}

public record SpecialNoteRow(int Index, string PlanCode, string? Description, string? GroupCompany, string? Note);

public class SpecialNoteRequest
{
    public string? Note { get; set; }
}

/// <summary>Special Note tab: the note lives on the plan row. Saving with no text clears it.</summary>
public class SpecialNoteService(DbConnections db)
{
    public const int SearchLimit = 500;

    public async Task<(List<SpecialNoteRow> Rows, bool Truncated)> SearchAsync(string? plan, string? group)
    {
        await using var conn = db.MainDb();
        var rows = await conn.ProcQueryAsync<SpecialNoteRow>("genisis.SpecialNote_Search", new
        {
            top = SearchLimit + 1,
            plan = string.IsNullOrWhiteSpace(plan) ? null : plan.Trim().ToUpperInvariant(),
            group = string.IsNullOrWhiteSpace(group) ? null : SqlText.EscapeLike(group.Trim().ToUpperInvariant()),
        });
        var truncated = rows.Count > SearchLimit;
        return (truncated ? rows[..SearchLimit] : rows, truncated);
    }

    public async Task SaveAsync(int index, string? note)
    {
        await using var conn = db.MainDb();
        await conn.OpenAsync();
        await SaveAsync(conn, null, index, note);
    }

    internal static async Task SaveAsync(SqlConnection conn, SqlTransaction? tx, int index, string? note)
    {
        var value = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (await conn.ProcScalarAsync<int>("genisis.SpecialNote_Save", new { index, note = value }, tx) == StoredProcedures.NotFound)
            throw new PlanException("Record not found");
    }
}
