using APFlow.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace APFlow.Infrastructure;

/// <summary>
/// Registers services owned by the Infrastructure layer (persistence, storage, messaging,
/// security). Called once from the composition root (APFlow.Api's Program.cs). Contains
/// no business logic itself — this is solution scaffolding; persistence (EF Core DbContext),
/// Blob Storage, and Service Bus registrations are added here as their work packages land.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Registers Infrastructure-layer services into the DI container and binds their options.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KeyVaultOptions>(configuration.GetSection(KeyVaultOptions.SectionName));

        // Persistence (Azure SQL / EF Core), Blob Storage, and Service Bus registrations
        // are added here as they are implemented. Intentionally empty at solution-foundation stage.
        return services;
    }
}
