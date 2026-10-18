using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Genisis.Api.Common.Errors;

/// <summary>
/// Central mapping of exceptions to Problem Details responses. Users see a short message (also in the <c>message</c> extension the
/// UI reads); SQL text, stack traces and file paths only go to the log.
/// </summary>
public sealed class ProblemDetailsExceptionHandler(IProblemDetailsService problemDetails, ILogger<ProblemDetailsExceptionHandler> logger)
    : IExceptionHandler
{
    /// <summary>Non-standard status nginx and ASP.NET use for "client closed the request".</summary>
    public const int ClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, message) = exception switch
        {
            BusinessRuleException e => (StatusCodes.Status400BadRequest, "Request could not be completed", e.Message),
            SqlException e when SqlErrorNumbers.IsTruncation(e.Number) =>
                (StatusCodes.Status400BadRequest, "Value too long", "One of the values is longer than the database allows. Nothing was saved."),
            SqlException e when SqlErrorNumbers.IsEncryptionKeyMissing(e.Number) =>
                (StatusCodes.Status500InternalServerError, "Database encryption key not open",
                    "The database encryption key is not open on this SQL Server, so member details cannot be saved. Ask your DBA to open the database master key."),
            SqlException { Number: SqlErrorNumbers.Timeout } =>
                (StatusCodes.Status504GatewayTimeout, "Database timeout", "The request took too long. Narrow the filters and try again."),
            SqlException { Number: SqlErrorNumbers.MissingProcedure } =>
                (StatusCodes.Status500InternalServerError, "Database not up to date",
                    "The database is missing the stored procedures this version needs. Run sql/genisis-main.sql and sql/genisis-maintenance.sql (see sql/README.md)."),
            OperationCanceledException when context.RequestAborted.IsCancellationRequested =>
                (ClientClosedRequest, "Request cancelled", "Stopped. Nothing was saved."),
            _ => (StatusCodes.Status500InternalServerError, "Server error", "Server error. Check the API log for details."),
        };

        // Server errors are already logged with the exception by ASP.NET Core's ExceptionHandlerMiddleware.
        if (status < StatusCodes.Status500InternalServerError) logger.LogInformation("Request {Method} {Path} refused: {Reason}", context.Request.Method, context.Request.Path, message);

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = Problems.Create(context, status, title, message),
        });
    }
}

/// <summary>Builds the project's Problem Details shape: RFC 7807 fields plus <c>message</c> (shown by the UI) and <c>traceId</c>.</summary>
public static class Problems
{
    public static ProblemDetails Create(HttpContext context, int status, string title, string message) => new()
    {
        Status = status,
        Title = title,
        Detail = message,
        Extensions = { ["message"] = message, ["traceId"] = context.TraceIdentifier },
    };

    /// <summary>400 response for a request that failed its validator.</summary>
    public static ObjectResult Invalid(this ControllerBase controller, string message) =>
        new(Create(controller.HttpContext, StatusCodes.Status400BadRequest, "Request validation failed", message))
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" },
        };
}
