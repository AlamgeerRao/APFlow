using System.Net;
using APFlow.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace APFlow.Api.Middleware;

/// <summary>
/// Translates known <see cref="AppFlowException"/> types into RFC 7807 ProblemDetails
/// responses with the appropriate HTTP status code, and logs unhandled exceptions
/// without leaking internal details to the client. Purely mechanical mapping — no
/// business logic.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    /// <summary>Creates a new <see cref="ExceptionHandlingMiddleware"/>.</summary>
    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>Invokes the middleware, catching and translating any exception thrown further down the pipeline.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title) = MapException(exception);

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        else
        {
            _logger.LogWarning(exception, "Handled exception ({StatusCode}) processing {Method} {Path}", (int)statusCode, context.Request.Method, context.Request.Path);
        }

        // Known AppFlowException subtypes carry client-safe messages by design (they are
        // authored by our own code specifically to be shown to callers). Anything that
        // isn't one of those - i.e. anything mapped to 500 - is an unhandled/unexpected
        // exception and its message must NOT be echoed to the client, since it can
        // contain internal detail (connection strings, stack info, third-party error
        // text). The real message is logged above (server-side only).
        var clientSafeDetail = exception is AppFlowException
            ? exception.Message
            : "An unexpected error occurred. Please contact support if the problem persists.";

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = clientSafeDetail,
            Instance = context.Request.Path,
        };

        if (exception is ValidationException { Errors.Count: > 0 } validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors;
        }

        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsJsonAsync(problemDetails, options: null, contentType: "application/problem+json");
    }

    private static (HttpStatusCode StatusCode, string Title) MapException(Exception exception) => exception switch
    {
        NotFoundException => (HttpStatusCode.NotFound, "Resource not found"),
        ValidationException => (HttpStatusCode.BadRequest, "Validation failed"),
        ConflictException => (HttpStatusCode.Conflict, "Conflict"),
        ForbiddenException => (HttpStatusCode.Forbidden, "Forbidden"),
        _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred"),
    };
}
