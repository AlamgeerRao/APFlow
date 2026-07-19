using APFlow.Application.Interfaces;
using APFlow.Integrations.Graph;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;

namespace APFlow.Integrations;

/// <summary>
/// Registers services owned by the Integrations layer (Microsoft Graph, accounting
/// system connectors, Azure AI Document Intelligence, Azure OpenAI). Called once from
/// the composition root (APFlow.Api's Program.cs). Contains no business logic itself —
/// this is solution scaffolding; individual integration clients are added here as their
/// work packages land.
/// </summary>
public static class DependencyInjection
{
    private static readonly string[] GraphDefaultScope = ["https://graph.microsoft.com/.default"];

    /// <summary>Registers Integrations-layer services into the DI container.</summary>
    public static IServiceCollection AddIntegrations(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddGraph(configuration, environment);

        // Sage 50, Document Intelligence, and Azure OpenAI client registrations are
        // added here as they are implemented. Intentionally empty at
        // solution-foundation stage.
        return services;
    }

    private static IServiceCollection AddGraph(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.Configure<GraphOptions>(configuration.GetSection(GraphOptions.SectionName));
        var graphOptions = configuration.GetSection(GraphOptions.SectionName).Get<GraphOptions>() ?? new GraphOptions();

        var isConfigured = !string.IsNullOrWhiteSpace(graphOptions.TenantId)
                            && !string.IsNullOrWhiteSpace(graphOptions.ClientId)
                            && !string.IsNullOrWhiteSpace(graphOptions.MailboxUserPrincipalName);

        if (!isConfigured && !environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Graph:TenantId, Graph:ClientId, and Graph:MailboxUserPrincipalName must be configured outside Development. " +
                "Refusing to start with Graph unconfigured in a non-Development environment.");
        }

        // GraphServiceClient/credential construction does not itself make a network
        // call, so this is safe to register even when unconfigured in Development -
        // it will simply fail (and be logged) on the first real call, consistent with
        // the Development-convenience pattern used for EntraId (WP-002).
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<GraphOptions>>().Value;

            TokenCredential credential = string.IsNullOrWhiteSpace(options.ClientSecret)
                ? new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    TenantId = string.IsNullOrWhiteSpace(options.TenantId) ? null : options.TenantId,
                })
                : new ClientSecretCredential(options.TenantId, options.ClientId, options.ClientSecret);

            return new GraphServiceClient(credential, GraphDefaultScope);
        });

        // Registered via an explicit factory, not services.AddSingleton<IEmailService,
        // EmailService>(): EmailService's constructor is internal (it takes the
        // internal IGraphInboxReader - see GraphInboxReader.cs), and the default DI
        // container's reflection-based activation only finds PUBLIC constructors.
        // Calling `new EmailService(...)` directly here works because this code runs
        // within APFlow.Integrations itself, where the internal constructor is
        // accessible at compile time.
        services.AddSingleton<IGraphInboxReader, GraphInboxReader>();
        services.AddSingleton<IEmailService>(sp => new EmailService(
            sp.GetRequiredService<IGraphInboxReader>(),
            sp.GetRequiredService<IOptions<GraphOptions>>(),
            sp.GetRequiredService<ILogger<EmailService>>()));

        // WP-006: same internal-constructor-needs-a-factory reasoning as EmailService
        // above. GraphMessageOperations' own constructor is public (only its class is
        // internal), registered via the generic form directly since nothing prevents it.
        services.AddSingleton<IGraphMessageOperations, GraphMessageOperations>();
        services.AddSingleton<IEmailSyncService>(sp => new EmailSyncService(
            sp.GetRequiredService<IGraphMessageOperations>(),
            sp.GetRequiredService<IOptions<GraphOptions>>(),
            sp.GetRequiredService<ILogger<EmailSyncService>>()));

        return services;
    }
}
