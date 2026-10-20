using Dapper;
using Genisis.Api.Auth;
using Genisis.Api.Common;
using Genisis.Api.Data;
using Genisis.Api.Maintenance.Security;
using Microsoft.Extensions.Configuration;

namespace Genisis.Api.Tests;

public class AccessMatrixTests
{
    [Fact]
    public void The_matrix_has_one_entry_per_desktop_checkbox_in_position_order()
    {
        Assert.Equal(178, AccessMatrix.Size);
        Assert.Equal(Enumerable.Range(1, 178), AccessMatrix.Entries.Select(e => e.Position));
        Assert.All(AccessMatrix.Entries, e => Assert.False(string.IsNullOrWhiteSpace(e.Tab) || string.IsNullOrWhiteSpace(e.Group) || string.IsNullOrWhiteSpace(e.Caption)));
        // Positions the API already checks must carry the right captions.
        Assert.Equal("Registration", AccessMatrix.Entries[AccessRight.MembershipRegistration - 1].Caption);
        Assert.Equal("Adjustment", AccessMatrix.Entries[AccessRight.MembershipAdjustment - 1].Caption);
        Assert.Equal("Enquiry", AccessMatrix.Entries[AccessRight.MembershipEnquiry - 1].Caption);
        Assert.Equal("Department", AccessMatrix.Entries[AccessRight.SecurityDepartment - 1].Caption);
        Assert.Equal("User Permission", AccessMatrix.Entries[AccessRight.SecurityUsers - 1].Caption);
    }

    [Fact]
    public void The_access_string_is_rebuilt_from_the_ticks_and_keeps_what_follows_the_matrix()
    {
        var built = AccessMatrix.Build([1, 3, 178, 0, 500], new string('1', 178) + "101");
        Assert.Equal(181, built.Length);
        Assert.Equal('1', built[0]);
        Assert.Equal('0', built[1]);
        Assert.Equal('1', built[2]);
        Assert.Equal('1', built[177]);
        Assert.EndsWith("101", built);
        Assert.Equal([1, 3, 178, 179, 181], AccessMatrix.GrantedPositions(built));
        Assert.Equal(178, AccessMatrix.Build([], null).Length);
        Assert.True(AccessMatrix.IsGranted(built, 3));
        Assert.False(AccessMatrix.IsGranted(built, 2));
        Assert.False(AccessMatrix.IsGranted("1", 5));
    }
}

public class SecurityRuleTests
{
    [Fact]
    public void A_password_set_here_passes_the_sign_in_comparison()
    {
        var stored = SecurityUserService.Scramble("Secret12")!;
        Assert.NotEqual("Secret12", stored);
        Assert.True(LegacyCrypt.Matches("Secret12", stored));
        Assert.Null(SecurityUserService.Scramble(""));
        Assert.Null(SecurityUserService.Scramble(null));
    }

    [Fact]
    public void Passwords_are_7_to_10_plain_characters()
    {
        Assert.True(UserRules.PasswordIsValid("abc1234"));
        Assert.True(UserRules.PasswordIsValid("abcdefghi0"));
        Assert.False(UserRules.PasswordIsValid("abc123"));
        Assert.False(UserRules.PasswordIsValid("abcdefghi01"));
        Assert.False(UserRules.PasswordIsValid("abc 1234"));
        Assert.False(UserRules.PasswordIsValid("pässwort1"));
    }

    [Fact]
    public void Nobody_can_lock_themselves_out()
    {
        var withRight = AccessMatrix.Build([AccessMatrix.UserPermissionPosition], null);
        var withoutRight = AccessMatrix.Build([1], null);
        SecurityUserService.GuardSelf("ADMIN", "admin", UserCodes.Active, withRight);
        SecurityUserService.GuardSelf("OTHER", "ADMIN", UserCodes.Suspended, withoutRight);
        Assert.Throws<BusinessRuleException>(() => SecurityUserService.GuardSelf("ADMIN", "ADMIN", UserCodes.Suspended, withRight));
        Assert.Throws<BusinessRuleException>(() => SecurityUserService.GuardSelf("ADMIN", "ADMIN", UserCodes.Active, withoutRight));
    }

    [Fact]
    public void Validators_follow_the_desktop_order_and_messages()
    {
        var permission = new PermissionUserRequestValidator();
        Assert.Equal(SecurityMessages.EnterUserCode, permission.Validate(new PermissionUserRequest()).Errors.Single().ErrorMessage);
        var request = new PermissionUserRequest { Code = "U1", Name = "USER", Department = "IT", Status = "A", AuthorisedStatus = "N" };
        Assert.Equal(SecurityMessages.EnterServiceProvider, permission.Validate(request).Errors.Single().ErrorMessage);
        request.ServiceProvider = "01";
        Assert.True(permission.Validate(request).IsValid);
        request.Granted = [179];
        Assert.False(permission.Validate(request).IsValid);
        Assert.Equal(SecurityMessages.EnterDescription, new DepartmentRequestValidator().Validate(new DepartmentRequest { Code = "IT" }).Errors.Single().ErrorMessage);
        Assert.False(new DepartmentRequestValidator().Validate(new DepartmentRequest { Code = "ABCD", Description = "X" }).IsValid);
    }

    [Fact]
    public void Role_management_keeps_the_first_3_characters_of_the_department()
    {
        var write = SecurityUserService.Write(new RoleUserRequest { Name = " Ali ", Department = "itx - IT", Status = "a", Password = "Secret12" }, " u1 ");
        Assert.Equal(("U1", "Ali", "ITX", "A"), (write.Code, write.Name, write.Department, write.Status));
        Assert.True(LegacyCrypt.Matches("Secret12", write.Password));
        Assert.Null(write.Access);
    }
}

public class SecurityDatabaseTests
{
    private static readonly string? MainDb = Environment.GetEnvironmentVariable("GENISIS_TEST_MAINDB");
    private const string TestDepartment = "ZZT";
    private const string TestUser = "ZZTESTUSR";

    private static DbSessions Sessions() => new(new DbConnections(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:MainDb"] = MainDb }).Build()));

    [Fact]
    public async Task Departments_and_users_round_trip_without_exposing_the_password()
    {
        if (MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(MainDb);
        var departments = new DepartmentRepository();
        var users = new SecurityUserRepository();
        await using var session = await Sessions().BeginAsync();   // never committed: rolled back on dispose

        Assert.True(await departments.InsertAsync(session, TestDepartment, "Test department"));
        Assert.False(await departments.InsertAsync(session, TestDepartment, "Again"));
        Assert.True(await departments.UpdateAsync(session, TestDepartment, "Renamed"));
        Assert.Equal("Renamed", (await departments.ListAsync(session)).Single(d => d.Code == TestDepartment).Description);

        var request = new PermissionUserRequest
        {
            Name = "Test User", Password = "Secret12", Department = TestDepartment, Status = "A", ServiceProvider = "01", AuthorisedStatus = "N",
            Granted = [1, AccessMatrix.UserPermissionPosition],
        };
        Assert.True(await users.InsertAsync(session, SecurityUserService.WritePermission(request, TestUser, null)));
        Assert.False(await users.InsertAsync(session, SecurityUserService.WritePermission(request, TestUser, null)));

        var stored = await session.Connection.QuerySingleAsync<(string Password, string ChangeFlag)>(
            "SELECT USRPassword, USRLogonCHGStatus FROM genisis.Maint_USR WHERE USRCode = @code", new { code = TestUser }, session.Transaction);
        Assert.True(LegacyCrypt.Matches("Secret12", stored.Password));
        Assert.Equal("1", stored.ChangeFlag);

        var detail = (await users.GetAsync(session, TestUser))!;
        Assert.Equal([1, AccessMatrix.UserPermissionPosition], detail.Granted);
        Assert.Contains(await users.ListAsync(session, "ZZTEST"), u => u.Code == TestUser);

        // Editing without a password keeps the current one.
        request.Password = null;
        request.Name = "Renamed User";
        Assert.True(await users.UpdatePermissionAsync(session, SecurityUserService.WritePermission(request, TestUser, detail.Access)));
        var kept = await session.Connection.QuerySingleAsync<string>("SELECT USRPassword FROM genisis.Maint_USR WHERE USRCode = @code", new { code = TestUser }, session.Transaction);
        Assert.True(LegacyCrypt.Matches("Secret12", kept));

        Assert.Equal(-1, await departments.DeleteAsync(session, TestDepartment));
        Assert.True(await users.DeleteAsync(session, TestUser, "TEST"));
        Assert.Null(await users.GetAsync(session, TestUser));

        // A code that was deleted before can be created and deleted again (USRArchive is keyed on the code).
        Assert.True(await users.InsertAsync(session, SecurityUserService.WritePermission(request, TestUser, null) with { Password = "X" }));
        Assert.True(await users.DeleteAsync(session, TestUser, "TEST"));
        Assert.False(await users.DeleteAsync(session, TestUser, "TEST"));
        Assert.Equal(1, await departments.DeleteAsync(session, TestDepartment));
    }
}
