using APFlow.Application.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace APFlow.Integrations.Graph;

/// <summary>
/// Health check wrapping <see cref="IEmailService.VerifyMailboxConnectionAsync"/>.
/// Registered by APFlow.Api (see ApiServiceCollectionExtensions.AddApiServices),
/// tagged "ready" - same pattern as the database check added in WP-003.
/// SEVERITY DECISION (flagged for explicit CTA sign-off - tracked in
/// docs/WP-004-Health-Check-Severity-Decision.md): this reports
/// <see cref="HealthStatus.Degraded"/>, not <see cref="HealthStatus.Unhealthy"/>, when the mailbox is unreachable.
/// ASP.NET Core's default health check status-code mapping treats Degraded the same
/// as Healthy (HTTP 200) - only Unhealthy maps to 503. This means a Graph/mailbox
/// outage will NOT take /health/ready (and therefore general API traffic, if that
/// endpoint gates a load balancer) offline; it will still be visible in the health
/// response body for monitoring/alerting. Rationale: unlike the database, mailbox
/// connectivity is a dependency of a specific capability (email ingestion), not of
/// the whole API (login, viewing/approving already-ingested invoices don't need
/// Graph). Reconsider this if that assumption turns out to be wrong, or if the
/// intended design is a fully separate endpoint instead of a shared "ready" tag with
/// different severities.
/// </summary>
public sealed class GraphMailboxHealthCheck : IHealthCheck
{
    private readonly IEmailService _emailService;

    /// <summary>Creates a new <see cref="GraphMailboxHealthCheck"/>.</summary>
    public GraphMailboxHealthCheck(IEmailService emailService)
    {
        _emailService = emailService;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var isConnected = await _emailService.VerifyMailboxConnectionAsync(cancellationToken);

        return isConnected
            ? HealthCheckResult.Healthy("Graph mailbox connection verified.")
            : HealthCheckResult.Degraded("Graph mailbox connection could not be verified. This does not fail overall API readiness - see this check's type-level doc comment.");
    }
}
