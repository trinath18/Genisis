using FluentValidation;
using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Data;

namespace Genisis.Api.Maintenance.Security;

/// <summary>Code values of USR.</summary>
public static class UserCodes
{
    /// <summary>USRStatus: active (can sign in).</summary>
    public const string Active = "A";

    /// <summary>USRStatus: suspended.</summary>
    public const string Suspended = "S";

    /// <summary>AuthorisedStatus: yes.</summary>
    public const string Authorised = "Y";

    /// <summary>AuthorisedStatus: no (the default of a new user).</summary>
    public const string NotAuthorised = "N";

    /// <summary>The accepted USRStatus values.</summary>
    public static readonly IReadOnlySet<string> Statuses = new HashSet<string> { Active, Suspended };

    /// <summary>The accepted AuthorisedStatus values.</summary>
    public static readonly IReadOnlySet<string> AuthorisedStatuses = new HashSet<string> { Authorised, NotAuthorised };
}

/// <summary>One row of the users list.</summary>
public record UserSummary
{
    /// <summary>USRCode.</summary>
    public string Code { get; set; } = "";

    /// <summary>USRName.</summary>
    public string? Name { get; set; }

    /// <summary>USRLGCode (User LG ID).</summary>
    public string? LgCode { get; set; }

    /// <summary>GRPCode.</summary>
    public string? Department { get; set; }

    /// <summary>SRVCode.</summary>
    public string? ServiceProvider { get; set; }

    /// <summary>USRStatus (A / S).</summary>
    public string? Status { get; set; }

    /// <summary>AuthorisedStatus (Y / N).</summary>
    public string? AuthorisedStatus { get; set; }
}

/// <summary>One user as the screens show it. The password is never read.</summary>
public record UserDetail : UserSummary
{
    /// <summary>USRDateCreated.</summary>
    public DateTime? DateCreated { get; set; }

    /// <summary>USRAccess as stored.</summary>
    public string? Access { get; set; }

    /// <summary>The granted positions of <see cref="Access"/>.</summary>
    public List<int> Granted => AccessMatrix.GrantedPositions(Access);
}

/// <summary>A code and its description for the drop-down lists.</summary>
public record CodeName
{
    /// <summary>The code.</summary>
    public string Code { get; set; } = "";

    /// <summary>The description.</summary>
    public string? Name { get; set; }
}

/// <summary>ComboListing: departments, service providers and the access matrix.</summary>
public record SecurityLookups(List<CodeName> Departments, List<CodeName> ServiceProviders, IReadOnlyList<AccessEntry> Matrix);

/// <summary>Role Management (frmUserMaintenance): add or edit a user's code, name, password, department, status and date.</summary>
public class RoleUserRequest
{
    /// <summary>User code (ignored on edit; the URL names the user).</summary>
    public string? Code { get; set; }

    /// <summary>User name.</summary>
    public string? Name { get; set; }

    /// <summary>New password: required when adding, optional when editing (blank keeps the current one).</summary>
    public string? Password { get; set; }

    /// <summary>Department (GRP code).</summary>
    public string? Department { get; set; }

    /// <summary>A (active) or S (suspended).</summary>
    public string? Status { get; set; }

    /// <summary>Date created (today when not given).</summary>
    public DateTime? DateCreated { get; set; }
}

/// <summary>User Permission (frmUserPermissionMaintenance): everything Role Management has plus LG ID, service provider, authorised status and rights.</summary>
public class PermissionUserRequest : RoleUserRequest
{
    /// <summary>User LG ID.</summary>
    public string? LgCode { get; set; }

    /// <summary>Service provider (SRV code).</summary>
    public string? ServiceProvider { get; set; }

    /// <summary>Y or N.</summary>
    public string? AuthorisedStatus { get; set; }

    /// <summary>The ticked positions of the access matrix.</summary>
    public List<int> Granted { get; set; } = [];
}

/// <summary>The checks both user screens share, in frmUserMaintenance's order.</summary>
public static class UserRules
{
    /// <summary>Adds the code, name, password, department and status rules to <paramref name="v"/>.</summary>
    public static void AddCommon<T>(AbstractValidator<T> v) where T : RoleUserRequest
    {
        v.RuleFor(r => r.Code).Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage(SecurityMessages.EnterUserCode)
            .Must(c => c!.Trim().Length <= SecurityLimits.UserCode).WithMessage($"User code must be at most {SecurityLimits.UserCode} characters.");
        v.RuleFor(r => r.Name).Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage(SecurityMessages.EnterUserName)
            .Must(n => n!.Trim().Length <= SecurityLimits.UserName).WithMessage($"User name must be at most {SecurityLimits.UserName} characters.");
        v.RuleFor(r => r.Password).Must(p => string.IsNullOrEmpty(p) || PasswordIsValid(p))
            .WithMessage($"Password must be {SecurityLimits.PasswordMin} to {SecurityLimits.PasswordMax} characters, without spaces.");
        v.RuleFor(r => r.Department).Must(d => !string.IsNullOrWhiteSpace(d)).WithMessage(SecurityMessages.EnterDepartment)
            .Must(d => d!.Trim().Length <= SecurityLimits.DepartmentCode).WithMessage($"Department code must be at most {SecurityLimits.DepartmentCode} characters.");
        v.RuleFor(r => r.Status).Must(s => s is not null && UserCodes.Statuses.Contains(s.Trim().ToUpperInvariant())).WithMessage(SecurityMessages.EnterStatus);
    }

    /// <summary>
    /// 7 to 10 printable characters without spaces: USRPassword holds 10, and the desktop's Crypt maps characters outside
    /// Windows-1252 to '?', so only plain ASCII is accepted.
    /// </summary>
    public static bool PasswordIsValid(string password) =>
        password.Length is >= SecurityLimits.PasswordMin and <= SecurityLimits.PasswordMax && password.All(c => c is > ' ' and <= '~');
}

/// <summary>Role Management's checks.</summary>
public sealed class RoleUserRequestValidator : RequestValidator<RoleUserRequest>
{
    /// <summary>Declares the rules.</summary>
    public RoleUserRequestValidator() => UserRules.AddCommon(this);
}

/// <summary>User Permission's checks, in cmdSave_Click's order.</summary>
public sealed class PermissionUserRequestValidator : RequestValidator<PermissionUserRequest>
{
    /// <summary>Declares the rules.</summary>
    public PermissionUserRequestValidator()
    {
        UserRules.AddCommon(this);
        RuleFor(r => r.ServiceProvider).Must(s => !string.IsNullOrWhiteSpace(s)).WithMessage(SecurityMessages.EnterServiceProvider)
            .Must(s => s!.Trim().Length <= SecurityLimits.ServiceProvider).WithMessage($"Service provider must be at most {SecurityLimits.ServiceProvider} characters.");
        RuleFor(r => r.AuthorisedStatus).Must(s => s is not null && UserCodes.AuthorisedStatuses.Contains(s.Trim().ToUpperInvariant()))
            .WithMessage(SecurityMessages.EnterAuthorisedStatus);
        RuleFor(r => r.LgCode).Must(c => (c?.Trim().Length ?? 0) <= SecurityLimits.LgCode).WithMessage($"User LG ID must be at most {SecurityLimits.LgCode} characters.");
        RuleFor(r => r.Granted).Must(g => g.All(p => p >= 1 && p <= AccessMatrix.Size)).WithMessage("Unknown access position.");
    }
}

/// <summary>The values written to USR; the password is already scrambled.</summary>
public sealed record UserWrite(string Code, string Name, string? Password, string Department, string Status, DateTime DateCreated,
    string? LgCode = null, string? ServiceProvider = null, string? AuthorisedStatus = null, string? Access = null);

/// <summary>Reads and writes USR.</summary>
public interface ISecurityUserRepository
{
    /// <summary>The users, optionally those whose code contains <paramref name="search"/>.</summary>
    Task<List<UserSummary>> ListAsync(IDbSession session, string? search);

    /// <summary>One user, or null.</summary>
    Task<UserDetail?> GetAsync(IDbSession session, string code);

    /// <summary>Departments and service providers.</summary>
    Task<(List<CodeName> Departments, List<CodeName> ServiceProviders)> LookupsAsync(IDbSession session);

    /// <summary>Adds a user; false when the code exists.</summary>
    Task<bool> InsertAsync(IDbSession session, UserWrite user);

    /// <summary>Role Management's update; false when the user does not exist.</summary>
    Task<bool> UpdateRoleAsync(IDbSession session, UserWrite user);

    /// <summary>User Permission's update; false when the user does not exist.</summary>
    Task<bool> UpdatePermissionAsync(IDbSession session, UserWrite user);

    /// <summary>Archives and deletes a user; false when the user does not exist.</summary>
    Task<bool> DeleteAsync(IDbSession session, string code, string by);
}

/// <inheritdoc />
public sealed class SecurityUserRepository : ISecurityUserRepository
{
    /// <inheritdoc />
    public Task<List<UserSummary>> ListAsync(IDbSession session, string? search) =>
        session.Connection.ProcQueryAsync<UserSummary>("genisis.Security_Users", new { search }, session.Transaction);

    /// <inheritdoc />
    public Task<UserDetail?> GetAsync(IDbSession session, string code) =>
        session.Connection.ProcFirstOrDefaultAsync<UserDetail>("genisis.Security_User", new { code }, session.Transaction);

    /// <inheritdoc />
    public async Task<(List<CodeName> Departments, List<CodeName> ServiceProviders)> LookupsAsync(IDbSession session)
    {
        using var grid = await session.Connection.ProcMultipleAsync("genisis.Security_Lookups", null, session.Transaction);
        return ((await grid.ReadAsync<CodeName>()).ToList(), (await grid.ReadAsync<CodeName>()).ToList());
    }

    /// <inheritdoc />
    public async Task<bool> InsertAsync(IDbSession session, UserWrite u) =>
        await session.Connection.ProcScalarAsync<int>("genisis.Security_UserInsert", new
        {
            code = u.Code, name = u.Name, password = u.Password, department = u.Department, status = u.Status, dateCreated = u.DateCreated,
            lgCode = u.LgCode, serviceProvider = u.ServiceProvider, authorisedStatus = u.AuthorisedStatus, access = u.Access,
        }, session.Transaction) == 1;

    /// <inheritdoc />
    public async Task<bool> UpdateRoleAsync(IDbSession session, UserWrite u) =>
        await session.Connection.ProcScalarAsync<int>("genisis.Security_UserUpdateRole", new
        {
            code = u.Code, name = u.Name, department = u.Department, status = u.Status, dateCreated = u.DateCreated, password = u.Password,
        }, session.Transaction) > 0;

    /// <inheritdoc />
    public async Task<bool> UpdatePermissionAsync(IDbSession session, UserWrite u) =>
        await session.Connection.ProcScalarAsync<int>("genisis.Security_UserUpdatePermission", new
        {
            code = u.Code, name = u.Name, department = u.Department, status = u.Status, dateCreated = u.DateCreated, lgCode = u.LgCode,
            serviceProvider = u.ServiceProvider, authorisedStatus = u.AuthorisedStatus, access = u.Access, password = u.Password,
        }, session.Transaction) > 0;

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(IDbSession session, string code, string by) =>
        await session.Connection.ProcScalarAsync<int>("genisis.Security_UserDelete", new { code, by }, session.Transaction) > 0;
}

/// <summary>
/// Maintenance > Security > Role Management (frmUserMaintenance) and User Permission (frmUserPermissionMaintenance), both on USR.
/// Passwords are scrambled with the desktop's Crypt (so sign-in keeps working) and never returned; a password set here must be
/// changed at the next sign-in. Nobody can delete or suspend their own user or remove their own User Permission right.
/// </summary>
public class SecurityUserService(IDbSessions sessions, ISecurityUserRepository repository, ILogger<SecurityUserService> log)
{
    /// <summary>USRListing / cmdSearch_Click.</summary>
    public async Task<List<UserSummary>> ListAsync(string? search)
    {
        await using var session = await sessions.OpenAsync();
        return await repository.ListAsync(session, string.IsNullOrWhiteSpace(search) ? null : search.Trim());
    }

    /// <summary>lstUsersList_Click, without the password.</summary>
    public async Task<UserDetail> GetAsync(string code)
    {
        await using var session = await sessions.OpenAsync();
        return await repository.GetAsync(session, code.Trim()) ?? throw new NotFoundException(SecurityMessages.NotFound);
    }

    /// <summary>ComboListing and the access matrix.</summary>
    public async Task<SecurityLookups> LookupsAsync()
    {
        await using var session = await sessions.OpenAsync();
        var (departments, providers) = await repository.LookupsAsync(session);
        return new SecurityLookups(departments, providers, AccessMatrix.Entries);
    }

    /// <summary>Role Management, Add. The new user has no rights until they are granted in User Permission (as in the desktop).</summary>
    public Task CreateRoleAsync(RoleUserRequest r, string by) => CreateAsync(Write(r, r.Code!), by);

    /// <summary>User Permission, Add.</summary>
    public Task CreatePermissionAsync(PermissionUserRequest r, string by) => CreateAsync(WritePermission(r, r.Code!, null), by);

    /// <summary>Role Management, Edit.</summary>
    public async Task UpdateRoleAsync(string code, RoleUserRequest r, string by)
    {
        var user = Write(r, code);
        await using var session = await sessions.BeginAsync();
        if (await repository.GetAsync(session, user.Code) is not { } current) throw new NotFoundException(SecurityMessages.NotFound);
        GuardSelf(user.Code, by, user.Status, current.Access);
        await repository.UpdateRoleAsync(session, user);
        await session.CommitAsync();
        Audit("changed (Role Management)", user, by);
    }

    /// <summary>User Permission, Edit: rights are rebuilt from the ticked positions; positions after the matrix are kept.</summary>
    public async Task UpdatePermissionAsync(string code, PermissionUserRequest r, string by)
    {
        await using var session = await sessions.BeginAsync();
        if (await repository.GetAsync(session, code.Trim()) is not { } current) throw new NotFoundException(SecurityMessages.NotFound);
        var user = WritePermission(r, code, current.Access);
        GuardSelf(user.Code, by, user.Status, user.Access);
        await repository.UpdatePermissionAsync(session, user);
        await session.CommitAsync();
        Audit("changed (User Permission)", user, by);
    }

    /// <summary>cmdDelete_Click: recorded in USRArchive, then deleted.</summary>
    public async Task DeleteAsync(string code, string by)
    {
        code = code.Trim();
        if (string.Equals(code, by, StringComparison.OrdinalIgnoreCase)) throw new BusinessRuleException(SecurityMessages.CannotDeleteSelf);
        await using var session = await sessions.BeginAsync();
        if (!await repository.DeleteAsync(session, code, by)) throw new NotFoundException(SecurityMessages.NotFound);
        await session.CommitAsync();
        log.LogInformation("User {Code} deleted by {User}", code, by);
    }

    private async Task CreateAsync(UserWrite user, string by)
    {
        if (user.Password is null) throw new BusinessRuleException(SecurityMessages.EnterPassword);
        await using var session = await sessions.BeginAsync();
        if (!await repository.InsertAsync(session, user)) throw new ConflictException(SecurityMessages.Duplicated);
        await session.CommitAsync();
        Audit("added", user, by);
    }

    /// <summary>Stops an administrator suspending themselves or removing their own User Permission right.</summary>
    public static void GuardSelf(string code, string by, string status, string? accessAfter)
    {
        if (!string.Equals(code, by, StringComparison.OrdinalIgnoreCase)) return;
        if (status != UserCodes.Active || !AccessMatrix.IsGranted(accessAfter, AccessMatrix.UserPermissionPosition))
            throw new BusinessRuleException(SecurityMessages.CannotLockOutSelf);
    }

    /// <summary>The USR values of Role Management. The department keeps its first 3 characters, as the desktop did.</summary>
    public static UserWrite Write(RoleUserRequest r, string code) => new(
        code.Trim().ToUpperInvariant(), r.Name!.Trim(), Scramble(r.Password), Department(r.Department), r.Status!.Trim().ToUpperInvariant(),
        (r.DateCreated ?? DateTime.Today).Date);

    /// <summary>The USR values of User Permission, with the access string built from the ticked positions.</summary>
    public static UserWrite WritePermission(PermissionUserRequest r, string code, string? existingAccess) => Write(r, code) with
    {
        LgCode = string.IsNullOrWhiteSpace(r.LgCode) ? null : r.LgCode.Trim(),
        ServiceProvider = r.ServiceProvider!.Trim().ToUpperInvariant(),
        AuthorisedStatus = r.AuthorisedStatus!.Trim().ToUpperInvariant(),
        Access = AccessMatrix.Build(r.Granted, existingAccess),
    };

    /// <summary>The desktop's Crypt of a new password; null keeps the current one.</summary>
    public static string? Scramble(string? password) => string.IsNullOrEmpty(password) ? null : LegacyCrypt.Encrypt(password);

    private static string Department(string? value)
    {
        var code = value!.Trim().ToUpperInvariant();
        return code.Length > SecurityLimits.DepartmentCode ? code[..SecurityLimits.DepartmentCode] : code;
    }

    private void Audit(string action, UserWrite user, string by) =>
        log.LogInformation("User {Code} {Action} by {User}: status {Status}, department {Department}, password {PasswordChange}, {Rights} rights",
            user.Code, action, by, user.Status, user.Department, user.Password is null ? "kept" : "set",
            user.Access is null ? "unchanged" : AccessMatrix.GrantedPositions(user.Access).Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
