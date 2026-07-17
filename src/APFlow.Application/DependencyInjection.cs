using Microsoft.Extensions.DependencyInjection;

namespace APFlow.Application;

/// <summary>
/// Registers services owned by the Application layer. Called once from the composition
/// root (APFlow.Api's Program.cs). Contains no business logic itself — this is solution
/// scaffolding; feature registrations are added here as feature work packages land.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Registers Application-layer services into the DI container.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Feature use-case registrations, validators, and mapping profiles are added
        // here as they are implemented. Intentionally empty at solution-foundation stage.
        return services;
    }
}
