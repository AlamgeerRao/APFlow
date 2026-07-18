using APFlow.Application.Interfaces;
using APFlow.Infrastructure.Configuration;
using APFlow.Infrastructure.Persistence;
using APFlow.Infrastructure.Security;
using APFlow.Infrastructure.Storage;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        services.AddBlobStorage(configuration, environment);

        // Service Bus registrations are added here as they are implemented.
        // Intentionally empty at solution-foundation stage.
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

    private static IServiceCollection AddBlobStorage(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.Configure<BlobStorageOptions>(configuration.GetSection(BlobStorageOptions.SectionName));
        var blobOptions = configuration.GetSection(BlobStorageOptions.SectionName).Get<BlobStorageOptions>()
                           ?? new BlobStorageOptions();

        var isConfigured = !string.IsNullOrWhiteSpace(blobOptions.ContainerName)
                            && (!string.IsNullOrWhiteSpace(blobOptions.AccountUrl) || !string.IsNullOrWhiteSpace(blobOptions.ConnectionString));

        if (!isConfigured && !environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "BlobStorage:ContainerName and either BlobStorage:AccountUrl or BlobStorage:ConnectionString " +
                "must be configured outside Development. Refusing to start with Blob Storage unconfigured in a " +
                "non-Development environment.");
        }

        // BlobServiceClient construction does not itself make a network call, so this
        // is safe to register even when unconfigured in Development - it will simply
        // fail (and be logged) on the first real call, consistent with the
        // Development-convenience pattern used for EntraId (WP-002) and Graph (WP-004).
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<BlobStorageOptions>>().Value;

            if (!string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                return new BlobServiceClient(options.ConnectionString);
            }

            if (!string.IsNullOrWhiteSpace(options.AccountUrl))
            {
                return new BlobServiceClient(new Uri(options.AccountUrl), new DefaultAzureCredential());
            }

            // Development-unconfigured placeholder - fails on first real call, not at
            // startup. Never reached outside Development (see the throw above).
            return new BlobServiceClient(new Uri("https://placeholder.blob.core.windows.net/"), new DefaultAzureCredential());
        });

        // BlobStorageService is registered via an explicit factory lambda, not
        // services.AddScoped<TInterface, TImpl>(): its constructor is internal (it
        // takes the internal IBlobContainerOperations - see BlobContainerOperations.cs),
        // and the default DI container's reflection-based activation only finds
        // PUBLIC constructors. Same pattern as EmailService (WP-004).
        // BlobContainerOperations' own constructor is public (only its class is
        // internal, which reflection-based activation tolerates - verified in WP-004b);
        // it's registered via factory here too only for consistency with the line below.
        // BlobContainerOperations itself stays Singleton (thin, stateless wrapper over
        // the thread-safe BlobServiceClient - no request-scoped state).
        services.AddSingleton<IBlobContainerOperations>(sp => new BlobContainerOperations(
            sp.GetRequiredService<BlobServiceClient>(),
            sp.GetRequiredService<IOptions<BlobStorageOptions>>()));
        // Scoped, not Singleton: BlobStorageService now depends on the Scoped
        // ICurrentUserService to enforce tenant-scoped blob names (see
        // docs/WP-005-Blob-Storage-Tenant-Isolation-Decision.md). A Singleton would
        // capture whichever tenant's request constructed it first and freeze to that
        // tenant forever - the same bug class flagged for the EF Core query filter in
        // docs/WP-003-Tenant-Isolation-Decision.md.
        services.AddScoped<IBlobStorageService>(sp => new BlobStorageService(
            sp.GetRequiredService<IBlobContainerOperations>(),
            sp.GetRequiredService<ICurrentUserService>(),
            sp.GetRequiredService<ILogger<BlobStorageService>>()));

        return services;
    }
}
