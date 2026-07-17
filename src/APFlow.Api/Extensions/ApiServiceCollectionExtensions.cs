using APFlow.Api.Configuration;

namespace APFlow.Api.Extensions;

/// <summary>
/// Registers services owned by the API layer itself: built-in OpenAPI document generation
/// and Health Checks. Kept separate from APFlow.Application/Infrastructure/Integrations/Workers
/// registrations so each layer's composition stays independently testable.
/// </summary>
public static class ApiServiceCollectionExtensions
{
    /// <summary>Registers API-owned services (OpenAPI, health checks) and binds their options.</summary>
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ApplicationOptions>(configuration.GetSection(ApplicationOptions.SectionName));

        // Built-in OpenAPI document generation (Microsoft.AspNetCore.OpenApi). This produces
        // the JSON document only; there is no built-in interactive Swagger-style UI page.
        services.AddOpenApi();

        services.AddHealthChecks();
        // Dependency-specific checks (Azure SQL, Blob Storage, Service Bus) are added
        // here as their infrastructure registrations land. Intentionally minimal
        // (process-liveness only) at solution-foundation stage.

        return services;
    }

    /// <summary>Maps the OpenAPI JSON document endpoint. Intended for Development only.</summary>
    public static WebApplication UseApiOpenApi(this WebApplication app)
    {
        app.MapOpenApi();
        return app;
    }

    /// <summary>Maps liveness and readiness health check endpoints. Enabled in every environment.</summary>
    public static WebApplication UseApiHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live");
        app.MapHealthChecks("/health/ready");

        return app;
    }
}
