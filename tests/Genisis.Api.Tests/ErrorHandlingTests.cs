using Genisis.Api.Common.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

public class ErrorHandlingTests
{
    private sealed class SampleRuleException(string message) : BusinessRuleException(message);

    private sealed class CapturingProblemDetailsService : IProblemDetailsService
    {
        public ProblemDetails? Written { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Written = context.ProblemDetails;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            Written = context.ProblemDetails;
            return ValueTask.FromResult(true);
        }
    }

    private static async Task<(int Status, ProblemDetails Problem)> HandleAsync(Exception exception)
    {
        var service = new CapturingProblemDetailsService();
        var handler = new ProblemDetailsExceptionHandler(service, NullLogger<ProblemDetailsExceptionHandler>.Instance);
        var context = new DefaultHttpContext { TraceIdentifier = "trace-1" };
        Assert.True(await handler.TryHandleAsync(context, exception, default));
        return (context.Response.StatusCode, service.Written!);
    }

    [Fact]
    public async Task Business_rules_become_400_with_the_user_message()
    {
        var (status, problem) = await HandleAsync(new SampleRuleException("Payor Selected Is Not Match The File Payor"));
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal("Payor Selected Is Not Match The File Payor", problem.Detail);
        Assert.Equal("Payor Selected Is Not Match The File Payor", problem.Extensions["message"]);
        Assert.Equal("trace-1", problem.Extensions["traceId"]);
    }

    [Fact]
    public async Task Unexpected_errors_do_not_leak_details()
    {
        var (status, problem) = await HandleAsync(new InvalidOperationException("Connection string Server=prod;Password=secret"));
        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.DoesNotContain("secret", problem.Detail);
        Assert.Equal("Server error. Check the API log for details.", problem.Detail);
    }

    [Fact]
    public void Sql_error_groups()
    {
        Assert.True(SqlErrorNumbers.IsTruncation(SqlErrorNumbers.StringTruncated));
        Assert.True(SqlErrorNumbers.IsTruncation(SqlErrorNumbers.StringTruncatedInColumn));
        Assert.True(SqlErrorNumbers.IsEncryptionKeyMissing(SqlErrorNumbers.MasterKeyNotOpen));
        Assert.False(SqlErrorNumbers.IsEncryptionKeyMissing(SqlErrorNumbers.Timeout));
    }
}
