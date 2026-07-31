using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace APFlow.Workers;

/// <summary>
/// Registers services owned by the Workers layer (background and asynchronous processing:
/// polling, message-triggered processing, scheduled jobs). Contains no business logic
/// itself — this is solution scaffolding.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers Workers-layer services into the DI container: <see cref="WorkersOptions"/>
    /// (bound from the "Workers" config section) and <see cref="EmailIngestionWorker"/>
    /// (WP-067) as a hosted service, so it starts automatically with the rest of the
    /// API app on host startup.
    /// </summary>
    public static IServiceCollection AddWorkers(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WorkersOptions>(configuration.GetSection(WorkersOptions.SectionName));
        services.AddHostedService<EmailIngestionWorker>();

        return services;
    }
}
