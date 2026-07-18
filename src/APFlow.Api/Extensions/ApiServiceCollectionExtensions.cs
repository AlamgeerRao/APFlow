using APFlow.Api.Configuration;
using APFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;

namespace APFlow.Api.Extensions;

/// <summary>
/// Registers services owned by the API layer itself: built-in OpenAPI document generation
/// and Health Checks. Kept separate from APFlow.Application/Infrastructure/Integrations/Workers
/// registrations so each layer's composition stays independently testable.
/// </summary>
public static class ApiServiceCollectionExtensions
{
    private const string BearerSecuritySchemeId = "Bearer";

    /// <summary>Registers API-owned services (OpenAPI, health checks) and binds their options.</summary>
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ApplicationOptions>(configuration.GetSection(ApplicationOptions.SectionName));

        // Built-in OpenAPI document generation (Microsoft.AspNetCore.OpenApi). This produces
        // the JSON document only; there is no built-in interactive Swagger-style UI page.
        // The document is annotated with the Bearer JWT security scheme (WP-002 "Secure
        // Swagger") so any client/UI reading it knows every endpoint requires a token -
        // the raw JSON endpoint itself stays open in Development for developer convenience,
        // since it carries no data, only the API shape. Tighten with .RequireAuthorization()
        // on the MapOpenApi() call in ApiServiceCollectionExtensions if that changes.
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes[BearerSecuritySchemeId] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Entra External ID JWT access token.",
                };

                document.SecurityRequirements.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = BearerSecuritySchemeId,
                        },
                    }] = [],
                });

                return Task.CompletedTask;
            });
        });

        // Liveness ("is the process up") intentionally runs zero checks - see
        // UseApiHealthChecks. Readiness ("can this instance serve traffic") includes
        // dependency checks, tagged "ready" so the /health/ready mapping can filter to
        // just these. AddDbContextCheck was flagged as pending since WP-001
        // ("Dependency-specific checks... added here as their infrastructure
        // registrations land") and is added now that AppDbContext exists (WP-003).
        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);
        // Blob Storage and Service Bus checks are added here (tagged "ready") as their
        // infrastructure registrations land.

        return services;
    }

    /// <summary>
    /// Maps the OpenAPI JSON document endpoint. Intended for Development only.
    /// Explicitly anonymous: the document itself carries no data, only the API shape,
    /// and the solution-wide fallback authorization policy would otherwise block
    /// access to it too. Individual operations in the document are still annotated
    /// with the Bearer security requirement (see AddApiServices).
    /// </summary>
    public static WebApplication UseApiOpenApi(this WebApplication app)
    {
        app.MapOpenApi().AllowAnonymous();
        return app;
    }

    /// <summary>
    /// Maps liveness and readiness health check endpoints. Enabled in every environment.
    /// Explicitly anonymous: these are probed by infrastructure (load balancers, Azure
    /// App Service health checks) that does not carry a bearer token, and the
    /// solution-wide fallback authorization policy (see <c>AddApiAuthorization</c>)
    /// would otherwise require authentication here too.
    /// Deliberately split: liveness runs no checks at all (just confirms the process is
    /// responding - a DB outage should not cause a load balancer to kill and restart a
    /// perfectly healthy process). Readiness runs every check tagged "ready" (currently
    /// just the database), so traffic can be routed away from an instance that can't
    /// actually serve requests without also treating that as a reason to restart it.
    /// </summary>
    public static WebApplication UseApiHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

        return app;
    }
}
