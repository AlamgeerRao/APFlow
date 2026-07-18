namespace APFlow.Infrastructure.Configuration;

/// <summary>
/// Binds the "Database" configuration section: EF Core / Azure SQL resiliency
/// settings. The connection string itself is bound separately via the standard
/// ASP.NET Core "ConnectionStrings:DefaultConnection" convention, not through this
/// class, so it can be sourced from Key Vault the same way as other secrets.
/// </summary>
public sealed class DatabaseOptions
{
    /// <summary>The configuration section name this class binds to.</summary>
    public const string SectionName = "Database";

    /// <summary>Command timeout in seconds before a query is abandoned.</summary>
    public int CommandTimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Maximum number of automatic retries EF Core performs on transient Azure SQL
    /// failures (throttling, failover) before giving up.
    /// </summary>
    public int MaxRetryCount { get; init; } = 3;

    /// <summary>Maximum delay in seconds between retry attempts.</summary>
    public int MaxRetryDelaySeconds { get; init; } = 5;
}
