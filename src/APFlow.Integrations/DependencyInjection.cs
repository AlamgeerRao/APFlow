using Microsoft.Extensions.DependencyInjection;

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
    /// <summary>Registers Integrations-layer services into the DI container.</summary>
    public static IServiceCollection AddIntegrations(this IServiceCollection services)
    {
        // Microsoft Graph, Sage 50, Document Intelligence, and Azure OpenAI client
        // registrations are added here as they are implemented. Intentionally empty
        // at solution-foundation stage.
        return services;
    }
}
