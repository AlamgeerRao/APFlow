using APFlow.Application.Interfaces;
using APFlow.Infrastructure.Configuration;
using APFlow.Infrastructure.Persistence;
using APFlow.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace APFlow.Infrastructure;

/// <summary>
/// Registers services owned by the Infrastructure layer (persistence, storage, messaging,
/// security). Called once from the composition root (APFlow.Api's Program.cs). Contains
/// no business logic itself — this is solution scaffolding; Blob Storage and Service Bus
/// registrations are added here as their work packages land.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Registers Infrastructure-layer services into the DI container and binds their options.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.Configure<KeyVaultOptions>(configuration.GetSection(KeyVaultOptions.SectionName));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddDatabase(configuration, environment);

        // Blob Storage and Service Bus registrations are added here as they are
        // implemented. Intentionally empty at solution-foundation stage.
        return services;
    }

    private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        var databaseOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
                               ?? new DatabaseOptions();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:DefaultConnection must be configured outside Development. " +
                    "Refusing to start with no database connection configured in a non-Development environment.");
            }

            // Development-only convenience default so `dotnet run` works without Key
            // Vault configured. Never used outside Development - see the throw above.
            connectionString = DesignTimeDefaults.LocalDbFallbackConnectionString;
        }

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(
                    databaseOptions.MaxRetryCount,
                    TimeSpan.FromSeconds(databaseOptions.MaxRetryDelaySeconds),
                    errorNumbersToAdd: null);
                sqlOptions.CommandTimeout(databaseOptions.CommandTimeoutSeconds);
            });
        });

        return services;
    }
}
