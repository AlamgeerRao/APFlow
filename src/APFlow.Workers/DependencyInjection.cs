using Microsoft.Extensions.DependencyInjection;

namespace APFlow.Workers;

/// <summary>
/// Registers services owned by the Workers layer (background and asynchronous processing:
/// polling, message-triggered processing, scheduled jobs). Contains no business logic
/// itself — this is solution scaffolding. Note: this project is currently a class library;
/// converting it into a runnable worker host (Program.cs, IHostedService/BackgroundService
/// registrations) is left to its dedicated work package rather than assumed here.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Registers Workers-layer services into the DI container.</summary>
    public static IServiceCollection AddWorkers(this IServiceCollection services)
    {
        // Hosted services / background job registrations are added here as they are
        // implemented. Intentionally empty at solution-foundation stage.
        return services;
    }
}
