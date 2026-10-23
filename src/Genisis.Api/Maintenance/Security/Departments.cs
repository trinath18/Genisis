using FluentValidation;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Maintenance.Security;

/// <summary>Messages of the Security screens; the desktop wording is kept where it had one.</summary>
public static class SecurityMessages
{
    /// <summary>Department code missing.</summary>
    public const string EnterDepartment = "Please Enter Department";

    /// <summary>Description missing.</summary>
    public const string EnterDescription = "Please Enter Description";

    /// <summary>Adding a code that exists.</summary>
    public const string Duplicated = "Record Duplicated! Please use edit button to commit the changes";

    /// <summary>The department or user is gone.</summary>
    public const string NotFound = "Record not found.";

    /// <summary>Deleting a department that still has users (the desktop deleted it and left the users pointing at nothing).</summary>
    public const string DepartmentInUse = "This department still has users. Move them to another department before deleting it.";

    /// <summary>User code missing.</summary>
    public const string EnterUserCode = "Please Enter User Code";

    /// <summary>User name missing.</summary>
    public const string EnterUserName = "Please Enter User Name";

    /// <summary>Password missing when adding a user.</summary>
    public const string EnterPassword = "Please Enter Password";

    /// <summary>Service provider missing.</summary>
    public const string EnterServiceProvider = "Please Enter Service Provider";

    /// <summary>Status missing or not A / S.</summary>
    public const string EnterStatus = "Please Enter Status";

    /// <summary>Authorised status missing or not Y / N.</summary>
    public const string EnterAuthorisedStatus = "Please Enter Authorised Status";

    /// <summary>Deleting your own account.</summary>
    public const string CannotDeleteSelf = "You cannot delete your own user.";

    /// <summary>Suspending your own account or removing your own User Permission right.</summary>
    public const string CannotLockOutSelf = "You cannot suspend your own user or remove your own User Permission right.";
}

/// <summary>Column sizes of GRP and USR (characters).</summary>
public static class SecurityLimits
{
    /// <summary>GRP.GRPCode; the user screens keep the first 3 characters of the department.</summary>
    public const int DepartmentCode = 3;

    /// <summary>GRP.GRPDescription.</summary>
    public const int DepartmentDescription = 255;

    /// <summary>USR.USRCode.</summary>
    public const int UserCode = 10;

    /// <summary>USR.USRName.</summary>
    public const int UserName = 60;

    /// <summary>USR.USRLGCode.</summary>
    public const int LgCode = 10;

    /// <summary>USR.SRVCode.</summary>
    public const int ServiceProvider = 2;

    /// <summary>The shortest password User Permission accepted.</summary>
    public const int PasswordMin = 7;

    /// <summary>USR.USRPassword holds 10 characters, and the scrambled password is as long as the password.</summary>
    public const int PasswordMax = 10;
}

/// <summary>One department (GRP).</summary>
public record Department
{
    /// <summary>GRPCode.</summary>
    public string Code { get; set; } = "";

    /// <summary>GRPDescription.</summary>
    public string? Description { get; set; }

    /// <summary>Users whose USR.GRPCode is this department.</summary>
    public int Users { get; set; }
}

/// <summary>Add or edit a department.</summary>
public class DepartmentRequest
{
    /// <summary>Department code (ignored on edit; the URL names the department).</summary>
    public string? Code { get; set; }

    /// <summary>Description.</summary>
    public string? Description { get; set; }
}

/// <summary>cmdSave_Click's checks, in its order.</summary>
public sealed class DepartmentRequestValidator : RequestValidator<DepartmentRequest>
{
    /// <summary>Declares the rules.</summary>
    public DepartmentRequestValidator()
    {
        RuleFor(r => r.Code).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage(SecurityMessages.EnterDepartment)
            .Must(v => v!.Trim().Length <= SecurityLimits.DepartmentCode).WithMessage($"Department code must be at most {SecurityLimits.DepartmentCode} characters.");
        RuleFor(r => r.Description).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage(SecurityMessages.EnterDescription)
            .Must(v => v!.Trim().Length <= SecurityLimits.DepartmentDescription).WithMessage($"Description must be at most {SecurityLimits.DepartmentDescription} characters.");
    }
}

/// <summary>Reads and writes GRP.</summary>
public interface IDepartmentRepository
{
    /// <summary>All departments with their user counts.</summary>
    Task<List<Department>> ListAsync(IDbSession session);

    /// <summary>Adds a department; false when the code exists.</summary>
    Task<bool> InsertAsync(IDbSession session, string code, string description);

    /// <summary>Changes the description; false when the department does not exist.</summary>
    Task<bool> UpdateAsync(IDbSession session, string code, string description);

    /// <summary>Deletes a department: -1 when users still belong to it, otherwise the rows deleted.</summary>
    Task<int> DeleteAsync(IDbSession session, string code);
}

/// <inheritdoc />
public sealed class DepartmentRepository : IDepartmentRepository
{
    /// <inheritdoc />
    public Task<List<Department>> ListAsync(IDbSession session) =>
        session.Connection.ProcQueryAsync<Department>("genisis.Security_Departments", null, session.Transaction);

    /// <inheritdoc />
    public async Task<bool> InsertAsync(IDbSession session, string code, string description) =>
        await session.Connection.ProcScalarAsync<int>("genisis.Security_DepartmentInsert", new { code, description }, session.Transaction) == 1;

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(IDbSession session, string code, string description) =>
        await session.Connection.ProcScalarAsync<int>("genisis.Security_DepartmentUpdate", new { code, description }, session.Transaction) > 0;

    /// <inheritdoc />
    public Task<int> DeleteAsync(IDbSession session, string code) =>
        session.Connection.ProcScalarAsync<int>("genisis.Security_DepartmentDelete", new { code }, session.Transaction);
}

/// <summary>Maintenance > Security > Department (frmGroupMaintenance, opened in place of the missing frmUserCreation).</summary>
public class DepartmentService(IDbSessions sessions, IDepartmentRepository repository, ILogger<DepartmentService> log)
{
    /// <summary>GRPListing.</summary>
    public async Task<List<Department>> ListAsync()
    {
        await using var session = await sessions.OpenAsync();
        return await repository.ListAsync(session);
    }

    /// <summary>cmdSave_Click (Add).</summary>
    public async Task CreateAsync(DepartmentRequest request, string by)
    {
        var code = request.Code!.Trim().ToUpperInvariant();
        await using var session = await sessions.BeginAsync();
        if (!await repository.InsertAsync(session, code, request.Description!.Trim())) throw new ConflictException(SecurityMessages.Duplicated);
        await session.CommitAsync();
        log.LogInformation("Department {Department} added by {User}", code, by);
    }

    /// <summary>cmdSave_Click (Edit).</summary>
    public async Task UpdateAsync(string code, DepartmentRequest request, string by)
    {
        await using var session = await sessions.BeginAsync();
        if (!await repository.UpdateAsync(session, code.Trim(), request.Description!.Trim())) throw new NotFoundException(SecurityMessages.NotFound);
        await session.CommitAsync();
        log.LogInformation("Department {Department} changed by {User}", code.Trim(), by);
    }

    /// <summary>cmdDelete_Click; refused while users belong to the department.</summary>
    public async Task DeleteAsync(string code, string by)
    {
        await using var session = await sessions.BeginAsync();
        var deleted = await repository.DeleteAsync(session, code.Trim());
        if (deleted < 0) throw new BusinessRuleException(SecurityMessages.DepartmentInUse);
        if (deleted == 0) throw new NotFoundException(SecurityMessages.NotFound);
        await session.CommitAsync();
        log.LogInformation("Department {Department} deleted by {User}", code.Trim(), by);
    }
}
