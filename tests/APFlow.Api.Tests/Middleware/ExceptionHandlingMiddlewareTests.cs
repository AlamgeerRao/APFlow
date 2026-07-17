using System.Text.Json;
using APFlow.Api.Middleware;
using APFlow.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace APFlow.Api.Tests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(typeof(NotFoundException), 404, "Resource not found")]
    [InlineData(typeof(ConflictException), 409, "Conflict")]
    [InlineData(typeof(ForbiddenException), 403, "Forbidden")]
    public async Task InvokeAsync_KnownAppFlowException_MapsToExpectedStatusAndTitle(Type exceptionType, int expectedStatus, string expectedTitle)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, "test message")!;
        var (context, body) = await InvokeWithException(exception);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);

        var problem = Deserialize(body);
        Assert.Equal(expectedTitle, problem.GetProperty("title").GetString());
        Assert.Equal("test message", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvokeAsync_ValidationExceptionWithFieldErrors_PopulatesErrorsExtension()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["Amount"] = ["Amount must be greater than zero."],
        };
        var exception = new ValidationException(errors);

        var (context, body) = await InvokeWithException(exception);

        Assert.Equal(400, context.Response.StatusCode);

        var problem = Deserialize(body);
        Assert.Equal("Validation failed", problem.GetProperty("title").GetString());
        Assert.True(problem.TryGetProperty("errors", out var errorsElement));
        Assert.True(errorsElement.TryGetProperty("Amount", out _));
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_MapsTo500WithGenericDetail_AndDoesNotLeakMessage()
    {
        // Regression test for the information-disclosure issue identified in the WP-001
        // review: an unhandled exception's real message must never reach the client.
        const string internalSecret = "Connection string: Server=internal-db;Password=hunter2";
        var exception = new InvalidOperationException(internalSecret);

        var (context, body) = await InvokeWithException(exception);

        Assert.Equal(500, context.Response.StatusCode);

        var problem = Deserialize(body);
        var detail = problem.GetProperty("detail").GetString();

        Assert.NotNull(detail);
        Assert.DoesNotContain(internalSecret, detail);
        Assert.DoesNotContain("hunter2", detail);
        Assert.Equal("An unexpected error occurred. Please contact support if the problem persists.", detail);
    }

    [Fact]
    public async Task InvokeAsync_NoExceptionThrown_PassesThroughWithoutModifyingResponse()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(_ => Task.CompletedTask, NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
    }

    private static async Task<(HttpContext Context, string Body)> InvokeWithException(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw exception,
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();

        return (context, body);
    }

    private static JsonElement Deserialize(string body) => JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
}
