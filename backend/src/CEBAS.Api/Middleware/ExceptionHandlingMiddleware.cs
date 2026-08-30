using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Common;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Api.Middleware;

/// <summary>
/// Global exception handling middleware implementing RFC 7807 (Problem Details for HTTP APIs).
/// Guarantees consistent error responses and shields internal implementation details.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        string traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        var (statusCode, problemDetails) = exception switch
        {
            ValidationException valEx => (
                StatusCodes.Status400BadRequest,
                new ProblemDetailsResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Validation Error",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = valEx.Message,
                    Instance = context.Request.Path,
                    TraceId = traceId,
                    Errors = valEx.Errors
                }
            ),

            NotFoundException notFoundEx => (
                StatusCodes.Status404NotFound,
                new ProblemDetailsResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                    Title = "Resource Not Found",
                    Status = StatusCodes.Status404NotFound,
                    Detail = notFoundEx.Message,
                    Instance = context.Request.Path,
                    TraceId = traceId
                }
            ),

            ConflictException conflictEx => (
                StatusCodes.Status409Conflict,
                new ProblemDetailsResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                    Title = "Conflict",
                    Status = StatusCodes.Status409Conflict,
                    Detail = conflictEx.Message,
                    Instance = context.Request.Path,
                    TraceId = traceId
                }
            ),

            UnauthorizedException unauthEx => (
                StatusCodes.Status401Unauthorized,
                new ProblemDetailsResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
                    Title = "Unauthorized",
                    Status = StatusCodes.Status401Unauthorized,
                    Detail = unauthEx.Message,
                    Instance = context.Request.Path,
                    TraceId = traceId
                }
            ),

            ForbiddenException forbiddenEx => (
                StatusCodes.Status403Forbidden,
                new ProblemDetailsResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                    Title = "Forbidden",
                    Status = StatusCodes.Status403Forbidden,
                    Detail = forbiddenEx.Message,
                    Instance = context.Request.Path,
                    TraceId = traceId
                }
            ),

            DomainException domainEx => (
                StatusCodes.Status400BadRequest,
                new ProblemDetailsResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Domain Rule Violation",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = domainEx.Message,
                    Instance = context.Request.Path,
                    TraceId = traceId
                }
            ),

            _ => (
                StatusCodes.Status500InternalServerError,
                new ProblemDetailsResponse
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                    Title = "Internal Server Error",
                    Status = StatusCodes.Status500InternalServerError,
                    Detail = _env.IsDevelopment() ? exception.Message : "An unexpected server error occurred. Please contact support with the trace identifier.",
                    Instance = context.Request.Path,
                    TraceId = traceId
                }
            )
        };

        if (statusCode >= 500)
        {
            _logger.LogError(exception, "Unhandled server error processing HTTP {Method} {Path} [TraceId: {TraceId}]",
                context.Request.Method, context.Request.Path, traceId);
        }
        else
        {
            _logger.LogWarning("Handled HTTP {StatusCode} error processing {Method} {Path} [TraceId: {TraceId}]: {Detail}",
                statusCode, context.Request.Method, context.Request.Path, traceId, problemDetails.Detail);
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, JsonOptions));
    }
}
