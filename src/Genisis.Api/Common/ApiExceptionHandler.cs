using Genisis.Api.Data;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Common;

/// <summary>
/// The one place that turns exceptions into HTTP answers, so controllers carry no try/catch. Answers are always
/// <c>{ "message": "..." }</c> with text the user can act on; failures the user cannot fix are logged with their stack trace
/// and answered with a generic message.
/// </summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> log) : IExceptionHandler
{
    /// <summary>SQL Server error numbers this handler recognises.</summary>
    private const int StringTruncated = 8152, DuplicateKeyTruncation = 2628, Deadlock = 1205, Timeout = -2;

    /// <summary>
    /// The exceptions this handler answers: the Case / Worksheet screens' <see cref="BusinessRuleException"/> family and deadlocks. Everything else
    /// (the membership screens' <see cref="Errors.BusinessRuleException"/>, other SQL errors, unexpected errors) is left to
    /// <see cref="Errors.ProblemDetailsExceptionHandler"/>, which is registered after this handler.
    /// </summary>
    public static bool Owns(Exception exception) => exception is BusinessRuleException or SqlException { Number: Deadlock };

    /// <summary>The status and message an exception is answered with.</summary>
    public static (int Status, string Message) Map(Exception exception) => exception switch
    {
        NotFoundException => (StatusCodes.Status404NotFound, exception.Message),
        ConflictException => (StatusCodes.Status409Conflict, exception.Message),
        BusinessRuleException => (StatusCodes.Status400BadRequest, exception.Message),
        SqlException { Number: StringTruncated or DuplicateKeyTruncation } =>
            (StatusCodes.Status400BadRequest, "One of the values is longer than the database allows. Shorten it and try again."),
        SqlException { Number: Deadlock } =>
            (StatusCodes.Status409Conflict, "Someone else is changing the same records. Wait a moment and try again."),
        SqlException { Number: Timeout } =>
            (StatusCodes.Status503ServiceUnavailable, "The database took too long to answer. Try again in a moment."),
        SqlException { Number: StoredProcedures.MissingProcedure } =>
            (StatusCodes.Status500InternalServerError,
                "The database is missing the stored procedures this version needs. Run sql/genisis-main.sql and sql/genisis-maintenance.sql (see sql/README.md)."),
        _ => (StatusCodes.Status500InternalServerError, "Server error. Check the API log for details."),
    };

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (!Owns(exception)) return false;
        var (status, message) = Map(exception);
        if (status >= StatusCodes.Status500InternalServerError)
            log.LogError(exception, "Unhandled error processing {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        else
            log.LogWarning("{Method} {Path} refused with {Status}: {Message}", httpContext.Request.Method, httpContext.Request.Path, status, message);

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new { message }, cancellationToken);
        return true;
    }
}
