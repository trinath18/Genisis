using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Maintenance.Suspension;

/// <summary>A suspension category (a row of <c>SuspensionCat</c>): 01 Membership, 02 Agent.</summary>
public record SuspensionCategory
{
    /// <summary>Category code.</summary>
    public string Code { get; set; } = "";

    /// <summary>Category description.</summary>
    public string? Description { get; set; }
}

/// <summary>A category to add or change (<c>SaveCAT</c>).</summary>
public class SuspensionCategoryRequest
{
    /// <summary>Category code.</summary>
    public string? Code { get; set; }

    /// <summary>Category description.</summary>
    public string? Description { get; set; }
}

/// <summary>A suspended member or agent (a row of <c>SuspensionDetail</c>, a line of <c>lstAgentList</c>).</summary>
public record SuspensionDetailRow
{
    /// <summary>Suspension code: 01 member, 02 agent.</summary>
    public string Selection { get; set; } = "";

    /// <summary>The member number (01) or agent code (02).</summary>
    public string? KeyCode { get; set; }

    /// <summary>Description (remarks).</summary>
    public string? Description { get; set; }

    /// <summary>Payor code.</summary>
    public string? PayorCode { get; set; }

    /// <summary>Payor company name.</summary>
    public string? PayorName { get; set; }

    /// <summary>The payor as the list shows it: code - name.</summary>
    public string Payor => $"{PayorCode} - {PayorName}".Trim(' ', '-');
}

/// <summary>A suspension to add or change (<c>SaveDetail</c>).</summary>
public class SuspensionDetailRequest
{
    /// <summary>01 member or 02 agent.</summary>
    public string? Selection { get; set; }

    /// <summary>The member number or agent code.</summary>
    public string? Key { get; set; }

    /// <summary>Description (remarks).</summary>
    public string? Description { get; set; }

    /// <summary>Payor code (the first two letters of the "CODE - name" the drop-down shows).</summary>
    public string? Payor { get; set; }
}

/// <summary>The rules of the Suspension screens: the checks of <c>SaveCAT</c> and <c>SaveDetail</c>, in the desktop's order, and the clean-up ChkStr did (trimmed, upper case).</summary>
public static class SuspensionRules
{
    /// <summary>The selections the drop-down offers.</summary>
    public static readonly string[] Selections = ["01", "02"];

    /// <summary>ChkStr: trimmed and upper-cased.</summary>
    public static string Clean(string? text) => (text ?? "").Trim().ToUpperInvariant();

    /// <summary>The first two characters of "CODE - name" (what <c>Mid(x, 1, 2)</c> took).</summary>
    public static string Code2(string? text) => Clean(text) is { Length: > 1 } t ? t[..2] : Clean(text);
}

/// <summary>What <c>SuspensionCat_Save</c> and <c>SuspensionDetail_Save</c> answer: 0 saved, 1 the key already exists (add), 2 there is no such row (edit).</summary>
public static class SuspensionSaveResult
{
    /// <summary>The key already has a row.</summary>
    public const int Duplicate = 1;

    /// <summary>There is no row to change.</summary>
    public const int Missing = 2;

    /// <summary>Throws the desktop's message for a save that did not happen.</summary>
    public static void ThrowUnless(int result)
    {
        if (result == Duplicate) throw new ConflictException("Record Duplicated! Please Change The Corresponding Field");
        if (result == Missing) throw new NotFoundException("Record not found");
    }
}

/// <summary>SaveCAT's checks.</summary>
public sealed class SuspensionCategoryRequestValidator : RequestValidator<SuspensionCategoryRequest>
{
    /// <summary>Declares the rules.</summary>
    public SuspensionCategoryRequestValidator()
    {
        RuleFor(r => r.Code).Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Please Enter Code");
        RuleFor(r => r.Code).Must(c => c!.Trim().Length <= 4).WithMessage("Code can be at most 4 characters.");
        RuleFor(r => r.Description).Must(d => !string.IsNullOrWhiteSpace(d)).WithMessage("Please Enter Description");
        RuleFor(r => r.Description).Must(d => d!.Trim().Length <= 100).WithMessage("Description can be at most 100 characters.");
    }
}

/// <summary>SaveDetail's checks, in the desktop's order.</summary>
public sealed class SuspensionDetailRequestValidator : RequestValidator<SuspensionDetailRequest>
{
    /// <summary>Declares the rules.</summary>
    public SuspensionDetailRequestValidator()
    {
        RuleFor(r => r.Selection).Must(s => !string.IsNullOrWhiteSpace(s)).WithMessage("Please Enter Selection");
        RuleFor(r => r.Selection).Must(s => SuspensionRules.Selections.Contains(SuspensionRules.Code2(s))).WithMessage("Select 01 - Member or 02 - Agent.");
        RuleFor(r => r.Key).Must(k => !string.IsNullOrWhiteSpace(k)).WithMessage("Please Enter Member Number/Agent Code");
        RuleFor(r => r.Key).Must(k => k!.Trim().Length <= 30).WithMessage("Member Number/Agent Code can be at most 30 characters.");
        RuleFor(r => r.Description).Must(d => !string.IsNullOrWhiteSpace(d)).WithMessage("Please Enter Description");
        RuleFor(r => r.Description).Must(d => d!.Trim().Length <= 200).WithMessage("Description can be at most 200 characters.");
        RuleFor(r => r.Payor).Must(p => !string.IsNullOrWhiteSpace(p)).WithMessage("Please Enter Payor");
        RuleFor(r => r).Must(r => SuspensionRules.Code2(r.Selection) != "01" || SuspensionRules.Code2(r.Key) == SuspensionRules.Code2(r.Payor)).WithMessage("Payor Code Not Match");
    }
}

/// <summary>Reads and writes the suspension categories and details.</summary>
public interface ISuspensionRepository
{
    /// <summary>The categories (all, or the one the code starts).</summary>
    Task<List<SuspensionCategory>> SearchCategoriesAsync(IDbSession session, string? code);

    /// <summary>Adds or changes a category; returns 0, 1 (duplicate) or 2 (not found).</summary>
    Task<int> SaveCategoryAsync(IDbSession session, string code, string description, bool edit, string userCode, DateTime today);

    /// <summary>Deletes a category; returns 0, 1 (not found) or 2 (used by a suspension).</summary>
    Task<int> DeleteCategoryAsync(IDbSession session, string code);

    /// <summary>The suspended members and agents.</summary>
    Task<List<SuspensionDetailRow>> SearchDetailsAsync(IDbSession session, string? selection, string? key, string? payor);

    /// <summary>Adds or changes a suspension; returns 0, 1 (duplicate) or 2 (not found).</summary>
    Task<int> SaveDetailAsync(IDbSession session, string selection, string key, string remarks, string payor, bool edit, string userCode, DateTime today);

    /// <summary>Deletes a suspension; returns 0 or 1 (not found).</summary>
    Task<int> DeleteDetailAsync(IDbSession session, string selection, string key);
}

/// <inheritdoc />
public sealed class SuspensionRepository : ISuspensionRepository
{
    /// <inheritdoc />
    public async Task<List<SuspensionCategory>> SearchCategoriesAsync(IDbSession session, string? code) =>
        await session.Connection.ProcQueryAsync<SuspensionCategory>("genisis.SuspensionCat_Search", new { code = string.IsNullOrWhiteSpace(code) ? null : SuspensionRules.Clean(code) }, session.Transaction);

    /// <inheritdoc />
    public async Task<int> SaveCategoryAsync(IDbSession session, string code, string description, bool edit, string userCode, DateTime today) =>
        await session.Connection.ProcScalarAsync<int>("genisis.SuspensionCat_Save", new { code, description, edit, userCode, today = today.Date }, session.Transaction);

    /// <inheritdoc />
    public async Task<int> DeleteCategoryAsync(IDbSession session, string code) =>
        await session.Connection.ProcScalarAsync<int>("genisis.SuspensionCat_Delete", new { code }, session.Transaction);

    /// <inheritdoc />
    public async Task<List<SuspensionDetailRow>> SearchDetailsAsync(IDbSession session, string? selection, string? key, string? payor) =>
        await session.Connection.ProcQueryAsync<SuspensionDetailRow>("genisis.SuspensionDetail_Search", new
        {
            selection = string.IsNullOrWhiteSpace(selection) ? null : SuspensionRules.Code2(selection), key = string.IsNullOrWhiteSpace(key) ? null : SuspensionRules.Clean(key),
            payor = string.IsNullOrWhiteSpace(payor) ? null : SuspensionRules.Code2(payor),
        }, session.Transaction);

    /// <inheritdoc />
    public async Task<int> SaveDetailAsync(IDbSession session, string selection, string key, string remarks, string payor, bool edit, string userCode, DateTime today) =>
        await session.Connection.ProcScalarAsync<int>("genisis.SuspensionDetail_Save", new { selection, key, remarks, payor, edit, userCode, today = today.Date }, session.Transaction);

    /// <inheritdoc />
    public async Task<int> DeleteDetailAsync(IDbSession session, string selection, string key) =>
        await session.Connection.ProcScalarAsync<int>("genisis.SuspensionDetail_Delete", new { selection, key }, session.Transaction);
}

/// <summary>Maintenance > Suspension (legacy <c>frmSuspensionDetail</c>, "Suspension Detail": the Suspension Category and Suspension Details tabs).</summary>
public class SuspensionService(IDbSessions sessions, ISuspensionRepository repository, ILogger<SuspensionService> log)
{
    /// <summary>SearchCAT: the categories.</summary>
    public async Task<List<SuspensionCategory>> SearchCategoriesAsync(string? code)
    {
        await using var session = await sessions.OpenAsync();
        return await repository.SearchCategoriesAsync(session, code);
    }

    /// <summary>SaveCAT: adds a category (a duplicate code is a <see cref="ConflictException"/>) or, with <paramref name="edit"/>, changes its description.</summary>
    public async Task SaveCategoryAsync(SuspensionCategoryRequest request, bool edit, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        var result = await repository.SaveCategoryAsync(session, SuspensionRules.Clean(request.Code), SuspensionRules.Clean(request.Description), edit, userCode, DateTime.Today);
        SuspensionSaveResult.ThrowUnless(result);
        await session.CommitAsync();
        log.LogInformation("Suspension category {Code} {Action} by {User}", SuspensionRules.Clean(request.Code), edit ? "changed" : "added", userCode);
    }

    /// <summary>DeleteCat: deletes a category that no suspension uses.</summary>
    public async Task DeleteCategoryAsync(string code, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        var result = await repository.DeleteCategoryAsync(session, SuspensionRules.Clean(code));
        if (result == StoredProcedures.NotFound) throw new NotFoundException("Record not found");
        if (result == StoredProcedures.Conflict) throw new ConflictException("This record cannot be deleted because it is used in Suspension Detail table!");
        await session.CommitAsync();
        log.LogInformation("Suspension category {Code} deleted by {User}", SuspensionRules.Clean(code), userCode);
    }

    /// <summary>SearchDetail: the suspended members and agents.</summary>
    public async Task<List<SuspensionDetailRow>> SearchDetailsAsync(string? selection, string? key, string? payor)
    {
        await using var session = await sessions.OpenAsync();
        return await repository.SearchDetailsAsync(session, selection, key, payor);
    }

    /// <summary>SaveDetail: adds a suspension (a member number or agent code that already has one is a <see cref="ConflictException"/>) or, with <paramref name="edit"/>, changes it.</summary>
    public async Task SaveDetailAsync(SuspensionDetailRequest request, bool edit, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        var selection = SuspensionRules.Code2(request.Selection);
        var key = SuspensionRules.Clean(request.Key);
        var result = await repository.SaveDetailAsync(session, selection, key, SuspensionRules.Clean(request.Description), SuspensionRules.Code2(request.Payor), edit, userCode, DateTime.Today);
        SuspensionSaveResult.ThrowUnless(result);
        await session.CommitAsync();
        log.LogInformation("Suspension of {Selection} {Key} {Action} by {User}", selection, key, edit ? "changed" : "added", userCode);
    }

    /// <summary>DeleteDETAIL: removes a suspension.</summary>
    public async Task DeleteDetailAsync(string selection, string key, string userCode)
    {
        await using var session = await sessions.BeginAsync();
        if (await repository.DeleteDetailAsync(session, SuspensionRules.Code2(selection), SuspensionRules.Clean(key)) != StoredProcedures.Ok) throw new NotFoundException("Record not found");
        await session.CommitAsync();
        log.LogInformation("Suspension of {Selection} {Key} deleted by {User}", SuspensionRules.Code2(selection), SuspensionRules.Clean(key), userCode);
    }
}
