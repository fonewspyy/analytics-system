using System.Diagnostics;
using System.Text.Json;
using Analytics.Application.Common;
using Analytics.Application.Logs;
using Analytics.Domain;
using Analytics.Domain.Entities;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Analytics.Api.Errors;

/// <summary>
/// The one place that turns an exception into an HTTP response (RFC 7807 ProblemDetails).
/// Controllers never catch to map status codes. Unexpected errors return a generic 500
/// without internals and are written to system_logs with the trace id for lookup.
/// </summary>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    ISystemLogRepository systemLogs,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var problem = Map(exception);
        var traceId = Activity.Current?.Id ?? http.TraceIdentifier;
        problem.Instance = http.Request.Path;
        problem.Extensions["traceId"] = traceId;

        if (problem.Status >= 500)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path} (trace {TraceId})",
                http.Request.Method, http.Request.Path, traceId);
            await AuditAsync(http, exception, traceId);
        }

        http.Response.StatusCode = problem.Status ?? 500;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails Map(Exception exception) => exception switch
    {
        RequestValidationException e => new ValidationProblemDetails(e.Errors)
        {
            Status = 400, Title = "Validation failed", Detail = e.Message,
        },
        NotFoundException e => Problem(404, "Not found", e.Message),
        ConflictException e => Problem(409, "Conflict", e.Message),
        DomainRuleException e => Problem(409, "Business rule violated", e.Message),
        PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } e =>
            Problem(409, "Duplicate value", UniqueMessage(e.ConstraintName)),
        PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } =>
            Problem(409, "Related data conflict", "The record is referenced by, or references, data that does not allow this change."),
        PostgresException { SqlState: PostgresErrorCodes.CheckViolation } e =>
            Problem(400, "Validation failed", $"The value breaks the rule '{e.ConstraintName}'."),
        BadHttpRequestException e => Problem(e.StatusCode, "Bad request", e.Message),
        OperationCanceledException => Problem(499, "Request cancelled", null),
        _ => Problem(500, "Unexpected error", "The error has been logged. Quote the traceId when reporting it."),
    };

    private static ProblemDetails Problem(int status, string title, string? detail) =>
        new() { Status = status, Title = title, Detail = detail };

    private static string UniqueMessage(string? constraint) => constraint switch
    {
        "ux_users_email_lower" => "A user with this email already exists.",
        "projects_code_key" => "A project with this code already exists.",
        _ => $"The value must be unique ({constraint}).",
    };

    private async Task AuditAsync(HttpContext http, Exception exception, string traceId)
    {
        try
        {
            await systemLogs.WriteAsync(new SystemLog
            {
                Level = LogSeverity.Error,
                Action = "request.failed",
                Message = exception.Message,
                Payload = JsonSerializer.Serialize(new
                {
                    method = http.Request.Method,
                    path = http.Request.Path.Value,
                    traceId,
                    exception = exception.GetType().FullName,
                }),
            }, CancellationToken.None);
        }
        catch (Exception logFailure)
        {
            // The database may be the thing that failed; never let logging hide the original error.
            logger.LogWarning(logFailure, "Could not write request.failed to system_logs");
        }
    }
}
