namespace APFlow.Infrastructure.Persistence;

/// <summary>
/// Local development fallback used only when no real connection string is configured
/// (Development environment / EF Core design-time tooling). Shared by
/// <c>DependencyInjection.AddDatabase</c> (runtime) and
/// <see cref="AppDbContextDesignTimeFactory"/> (design-time) so the two don't drift.
/// Never used outside Development - see <c>DependencyInjection.AddDatabase</c>'s
/// fail-fast check for every other environment.
/// </summary>
internal static class DesignTimeDefaults
{
    public const string LocalDbFallbackConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=APFlowDb;Trusted_Connection=True;TrustServerCertificate=True;";
}
