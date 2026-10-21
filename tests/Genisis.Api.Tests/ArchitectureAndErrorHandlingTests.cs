using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;
using Genisis.Api.Case.Listing;
using Genisis.Api.Common;
using Genisis.Api.Controllers;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>Keeps the layering of the Case screens honest: controllers orchestrate, services hold the rules, repositories hold the SQL.</summary>
public partial class ArchitectureTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly Type[] ScreenControllers =
    [
        typeof(PayorsController), typeof(PanelHospitalsController), typeof(CaseNonDisclosureController), typeof(CasePayClaimController),
        typeof(CaseMmaController), typeof(CaseAuditReviewController), typeof(CaseTransferController), typeof(CaseListingController),
        typeof(ClaimAsciiController), typeof(CaseAsciiController),
        typeof(WorksheetLookupsController), typeof(WorksheetTrackingController), typeof(WorksheetSendToAccountsController), typeof(WorksheetSendToArController), typeof(WorksheetEnquiryController),
        typeof(AccountTransactionController), typeof(SecurityDepartmentsController), typeof(SecurityUsersController), typeof(SecurityPermissionsController), typeof(MmaEntryController),
        typeof(FinanceSunPostingController), typeof(FinanceReceiptEnquiryController), typeof(FinanceInvoiceReceiptController), typeof(FinanceReverseReceiptController), typeof(FinanceSuspendActiveController), typeof(FinanceElectronicBankingController),
    ];

    private static readonly string[] ScreenNamespaces =
    [
        "Genisis.Api.Case.Ascii", "Genisis.Api.Case.AuditReview", "Genisis.Api.Case.Listing", "Genisis.Api.Case.Lookups", "Genisis.Api.Case.Mma",
        "Genisis.Api.Case.NonDisclosure", "Genisis.Api.Case.PanelHospitals", "Genisis.Api.Case.PayClaim", "Genisis.Api.Case.Transfer",
        "Genisis.Api.Worksheet.Lookups", "Genisis.Api.Worksheet.SendToAccounts", "Genisis.Api.Worksheet.SendToAr", "Genisis.Api.Worksheet.Tracking", "Genisis.Api.Worksheet.Enquiry", "Genisis.Api.Worksheet.Engine", "Genisis.Api.Worksheet.Adjustment",
        "Genisis.Api.Maintenance.AccountTransaction", "Genisis.Api.Maintenance.Security", "Genisis.Api.Maintenance.MmaEntry",
        "Genisis.Api.Finance.SunPosting", "Genisis.Api.Finance.ReceiptEnquiry", "Genisis.Api.Finance.InvoiceReceipt", "Genisis.Api.Finance.ReverseReceipt", "Genisis.Api.Finance.SuspendActive", "Genisis.Api.Finance.ElectronicBanking",
    ];

    private static bool IsScreenNamespace(string? ns) =>
        ns is not null && (ns.StartsWith("Genisis.Api.Case", StringComparison.Ordinal) || ns.StartsWith("Genisis.Api.Worksheet", StringComparison.Ordinal)
            || ns.StartsWith("Genisis.Api.Maintenance", StringComparison.Ordinal)
            || ns.StartsWith("Genisis.Api.Finance", StringComparison.Ordinal));

    private static string ApiSourceRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "src", "Genisis.Api", "Genisis.Api.csproj"))) return Path.Combine(dir.FullName, "src", "Genisis.Api");
        throw new DirectoryNotFoundException("Cannot find src/Genisis.Api from the test output folder.");
    }

    [GeneratedRegex(@"\b(try|catch)\b")]
    private static partial Regex TryCatch();

    [Fact]
    public void Screen_controllers_hold_no_data_access_and_no_exception_handling()
    {
        foreach (var controller in ScreenControllers)
        {
            var text = File.ReadAllText(Path.Combine(ApiSourceRoot(), "Controllers", controller.Name + ".cs"));
            foreach (var forbidden in new[] { "using Dapper", "SqlClient", "DbConnections", "IDbSession", "Repository" })
                Assert.False(text.Contains(forbidden, StringComparison.Ordinal), $"{controller.Name} must not reference {forbidden}");
            Assert.False(TryCatch().IsMatch(text), $"{controller.Name} must leave exception handling to ApiExceptionHandler");
        }
    }

    [Fact]
    public void Screen_controllers_depend_on_services_only()
    {
        foreach (var controller in ScreenControllers)
            foreach (var parameter in controller.GetConstructors().Single().GetParameters())
                Assert.True(parameter.ParameterType.Name.EndsWith("Service", StringComparison.Ordinal) && IsScreenNamespace(parameter.ParameterType.Namespace),
                    $"{controller.Name} takes {parameter.ParameterType.Name}");
    }

    [Fact]
    public void Screen_services_depend_on_abstractions_not_on_concrete_repositories()
    {
        var services = typeof(Program).Assembly.GetTypes().Where(t => t.Name.EndsWith("Service", StringComparison.Ordinal) && ScreenNamespaces.Contains(t.Namespace)).ToList();
        Assert.True(services.Count >= 15, "expected the services of the Case and Worksheet screens");
        foreach (var service in services)
            foreach (var parameter in service.GetConstructors().Single().GetParameters().Select(p => p.ParameterType))
                Assert.True(parameter.IsInterface || parameter.Name.EndsWith("Service", StringComparison.Ordinal), $"{service.Name} takes the concrete {parameter.Name}");
    }

    [Fact]
    public void Every_screen_repository_is_behind_an_interface_and_registered()
    {
        var repositories = typeof(Program).Assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && t.Name.EndsWith("Repository", StringComparison.Ordinal) && ScreenNamespaces.Contains(t.Namespace)).ToList();
        Assert.True(repositories.Count >= 14);
        foreach (var repository in repositories)
        {
            var contract = repository.GetInterfaces().Single(i => i.Name == "I" + repository.Name);
            Assert.NotNull(factory.Services.GetService(contract));
        }
    }

    [Fact]
    public void Every_request_model_of_the_screens_has_a_validator()
    {
        var exempt = new[] { "MmaProcedureQuery", "MmaFeeQuery" };
        using var scope = factory.Services.CreateScope();
        foreach (var controller in ScreenControllers)
            foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                foreach (var parameter in method.GetParameters().Where(p => p.ParameterType.IsClass && IsScreenNamespace(p.ParameterType.Namespace)))
                    if (!exempt.Contains(parameter.ParameterType.Name))
                        Assert.True(scope.ServiceProvider.GetService(typeof(IValidator<>).MakeGenericType(parameter.ParameterType)) is not null,
                            $"{controller.Name}.{method.Name} takes {parameter.ParameterType.Name} which has no validator");
    }
}

/// <summary>The exception handler is the single place that decides status codes and user-facing messages.</summary>
public class ApiExceptionHandlerTests
{
    private static SqlException SqlError(int number)
    {
        var error = (SqlError)RuntimeHelpers.GetUninitializedObject(typeof(SqlError));
        typeof(SqlError).GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .First(f => f.FieldType == typeof(int) && f.Name.Contains("number", StringComparison.OrdinalIgnoreCase)).SetValue(error, number);
        var errors = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), true)!;
        typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(errors, [error]);
        var exception = (SqlException)RuntimeHelpers.GetUninitializedObject(typeof(SqlException));
        typeof(SqlException).GetFields(BindingFlags.NonPublic | BindingFlags.Instance).First(f => f.FieldType == typeof(SqlErrorCollection)).SetValue(exception, errors);
        return exception;
    }

    [Fact]
    public void Business_exceptions_keep_their_message_and_pick_the_status()
    {
        Assert.Equal((404, "gone"), ApiExceptionHandler.Map(new NotFoundException("gone")));
        Assert.Equal((409, "clash"), ApiExceptionHandler.Map(new ConflictException("clash")));
        Assert.Equal((400, "fix it"), ApiExceptionHandler.Map(new BusinessRuleException("fix it")));
        Assert.Equal((400, "w"), ApiExceptionHandler.Map(new CaseListingException("w")));
        Assert.Equal((400, "x"), ApiExceptionHandler.Map(new WorksheetException("x")));
    }

    [Theory]
    [InlineData(8152, 400, "longer than the database allows")]
    [InlineData(2628, 400, "longer than the database allows")]
    [InlineData(1205, 409, "Someone else is changing")]
    [InlineData(-2, 503, "took too long")]
    [InlineData(StoredProcedures.MissingProcedure, 500, "missing the stored procedures")]
    [InlineData(547, 500, "Server error")]
    public void Database_errors_are_translated_into_something_the_user_can_act_on(int number, int status, string text)
    {
        var (actualStatus, message) = ApiExceptionHandler.Map(SqlError(number));
        Assert.Equal(status, actualStatus);
        Assert.Contains(text, message);
    }

    [Fact]
    public void Unexpected_errors_are_not_leaked()
    {
        var (status, message) = ApiExceptionHandler.Map(new InvalidOperationException("secret connection detail"));
        Assert.Equal(500, status);
        Assert.DoesNotContain("secret", message);
    }

    [Fact]
    public async Task The_handler_writes_a_json_message_and_marks_the_exception_handled()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handled = await new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance).TryHandleAsync(context, new NotFoundException("No record found!"), CancellationToken.None);
        Assert.True(handled);
        Assert.Equal(404, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        Assert.Equal("No record found!", (await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body)).GetProperty("message").GetString());
    }
}

/// <summary>The unit of work that services and repositories share.</summary>
public class DbSessionTests
{
    [Fact]
    public async Task A_fake_session_that_is_not_committed_counts_as_rolled_back()
    {
        var sessions = new FakeSessions();
        await using (await sessions.BeginAsync()) { }
        Assert.True(sessions.Last.RolledBack);
        await using (var committed = await sessions.BeginAsync()) await committed.CommitAsync();
        Assert.True(sessions.Last.Committed);
        Assert.False(sessions.Last.RolledBack);
    }

    [Fact]
    public async Task A_real_session_rolls_back_on_dispose_unless_committed()
    {
        if (TestDb.MainDb is null) return;
        var sessions = TestDb.Sessions();
        await using (var session = await sessions.BeginAsync())
        {
            Assert.NotNull(session.Transaction);
            await session.ExecuteScalarAsync("SELECT 1");
        }
        await using var read = await sessions.OpenAsync();
        Assert.Null(read.Transaction);
        await read.CommitAsync();
        await read.RollbackAsync();
    }
}

file static class SessionExtensions
{
    public static async Task<object?> ExecuteScalarAsync(this IDbSession session, string sql)
    {
        await using var command = new SqlCommand(sql, session.Connection, session.Transaction);
        return await command.ExecuteScalarAsync();
    }
}
