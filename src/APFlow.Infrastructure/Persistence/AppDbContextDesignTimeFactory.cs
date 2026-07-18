using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace APFlow.Infrastructure.Persistence;

/// <summary>
/// Lets EF Core design-time tooling (<c>dotnet ef migrations add</c>, <c>dotnet ef
/// database update</c>) construct an <see cref="AppDbContext"/> without running the
/// full APFlow.Api host. Required because AppDbContext lives in APFlow.Infrastructure,
/// not the startup project, and its constructor takes a service (
/// <see cref="APFlow.Application.Interfaces.ICurrentUserService"/>) that only exists
/// in a running DI container - this factory passes null for it instead, which is safe
/// for schema-only design-time operations that never call SaveChanges.
/// Reads the connection string from environment variable
/// "APFLOW_DESIGNTIME_CONNECTIONSTRING" first (for CI/non-interactive use), falling
/// back to a local SQL Server LocalDB instance for developer convenience. Neither of
/// these is used at runtime - see <c>DependencyInjection.AddInfrastructure</c> for the
/// real, Key-Vault-sourced connection string.
/// </summary>
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <inheritdoc />
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("APFLOW_DESIGNTIME_CONNECTIONSTRING")
            ?? new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build()
                .GetConnectionString("DefaultConnection")
            ?? DesignTimeDefaults.LocalDbFallbackConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sql =>
            sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));

        return new AppDbContext(optionsBuilder.Options, currentUserService: null);
    }
}
