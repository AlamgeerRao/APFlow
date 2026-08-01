using APFlow.Api.Configuration;
using APFlow.Infrastructure.Persistence;
using APFlow.Infrastructure.Storage;
using APFlow.Integrations.Graph;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

namespace APFlow.Api.Extensions;

/// <summary>
/// Registers services owned by the API layer itself: Application Insights telemetry
/// (WP-066), built-in OpenAPI document generation, and Health Checks. Kept separate from
/// APFlow.Application/Infrastructure/Integrations/Workers registrations so each layer's
/// composition stays independently testable.
/// </summary>
public static class ApiServiceCollectionExtensions
{
    private const string BearerSecuritySchemeId = "Bearer";

    /// <summary>Named CORS policy applied to every endpoint (WP-059 Part B) - see <see cref="UseApiCors"/>.</summary>
    public const string CorsPolicyName = "ApFlowWebClient";

    /// <summary>Registers API-owned services (OpenAPI, health checks, CORS) and binds their options.</summary>
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.Configure<ApplicationOptions>(configuration.GetSection(ApplicationOptions.SectionName));

        // WP-066: the connection string has been sitting in App Service configuration as
        // APPLICATIONINSIGHTS_CONNECTION_STRING since WP-021, but nothing ever called this -
        // that (not the connection string, not App Service config) was the entire reason zero
        // telemetry had ever been ingested. AddApplicationInsightsTelemetry() reads that exact
        // environment-variable name from IConfiguration by convention, with no explicit
        // connection-string argument needed; it also auto-registers dependency tracking for
        // outgoing HTTP calls (Graph) and SQL calls (EF Core/ADO.NET), so no per-call-site
        // instrumentation is required for either.
        services.AddApplicationInsightsTelemetry();

        // WP-059 Part B: named CORS policy, allowed origins read from configuration
        // ("Cors:AllowedOrigins"), not hardcoded - see appsettings.Development.json
        // for the actual Development-environment origins (the deployed dev Web App
        // Service URL plus the local Vite dev server). The decision logic is
        // extracted to ConfigureCorsPolicy (public, not inline in this lambda) so
        // it can be unit-tested directly against a real CorsPolicyBuilder/CorsPolicy,
        // without needing a full DI container or WebApplicationFactory.
        var corsOptions = configuration.GetSection(CorsPolicyOptions.SectionName).Get<CorsPolicyOptions>() ?? new CorsPolicyOptions();

        services.AddCors(options => options.AddPolicy(
            CorsPolicyName, policy => ConfigureCorsPolicy(policy, corsOptions, environment)));

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
        // registrations land") and was added in WP-003. GraphMailboxHealthCheck
        // (WP-004) and BlobStorageHealthCheck (WP-005) follow the same pattern.
        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database", tags: ["ready"])
            .AddCheck<GraphMailboxHealthCheck>("graph-mailbox", tags: ["ready"])
            .AddCheck<BlobStorageHealthCheck>("blob-storage", tags: ["ready"]);
        // Service Bus checks are added here (tagged "ready") as its infrastructure
        // registration lands.

        return services;
    }

    /// <summary>
    /// The CORS policy decision logic (WP-059 Part B), extracted from
    /// <see cref="AddApiServices"/>'s <c>AddCors</c> call so it can be unit-tested
    /// directly against a real <see cref="CorsPolicyBuilder"/>/built
    /// <c>CorsPolicy</c>, without needing a full DI container or
    /// <c>WebApplicationFactory</c>.
    /// </summary>
    /// <param name="policy">The builder to configure.</param>
    /// <param name="corsOptions">The bound "Cors" configuration section.</param>
    /// <param name="environment">The current hosting environment.</param>
    public static void ConfigureCorsPolicy(CorsPolicyBuilder policy, CorsPolicyOptions corsOptions, IHostEnvironment environment)
    {
        if (corsOptions.AllowedOrigins.Length > 0)
        {
            // No AllowCredentials(): this API authenticates via a Bearer token in
            // the Authorization header (Entra External ID JWT, WP-002), not
            // cookies/TLS client certs - AllowCredentials() governs the latter and
            // would be an unnecessary permission grant to add
            // (02_Project_Standards.md §4, least privilege).
            policy.WithOrigins(corsOptions.AllowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
        else if (environment.IsDevelopment())
        {
            // No explicit origins configured, and this is local Development -
            // permissive fallback so a developer's frontend on any port can call
            // this API without needing to edit config first. Never applies
            // outside Development: a deployed environment with no configured
            // origins gets a CORS policy that allows nothing, not a silent
            // permissive fallback - fail closed, not open, matching this
            // codebase's established philosophy elsewhere (WP-003's tenant
            // filter, WP-012's idempotency check, WP-051/053's approval-policy
            // fail-closed behaviour).
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }

        // Implicit else (deployed environment, no configured origins): no
        // WithOrigins/AllowAnyOrigin call at all - the policy permits no
        // cross-origin requests until Cors:AllowedOrigins is set for that
        // environment.
    }

    /// <summary>
    /// Applies the named CORS policy registered by <see cref="AddApiServices"/>
    /// (WP-059 Part B). Must run before <c>UseAuthentication</c>/<c>UseAuthorization</c>
    /// in the middleware pipeline (see Program.cs) - the standard ASP.NET Core
    /// ordering, so a preflight <c>OPTIONS</c> request is answered before auth
    /// middleware ever sees it (preflight requests never carry an Authorization
    /// header).
    /// </summary>
    public static WebApplication UseApiCors(this WebApplication app)
    {
        app.UseCors(CorsPolicyName);
        return app;
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
    /// perfectly healthy process). Readiness runs every check tagged "ready" (database,
    /// Graph mailbox, Blob Storage), so traffic can be routed away from an instance that can't
    /// actually serve requests without also treating that as a reason to restart it.
    /// </summary>
    public static WebApplication UseApiHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

        return app;
    }
}
