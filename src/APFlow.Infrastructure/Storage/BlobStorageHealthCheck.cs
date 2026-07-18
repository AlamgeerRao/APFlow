using APFlow.Application.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace APFlow.Infrastructure.Storage;

/// <summary>
/// Health check wrapping <see cref="IBlobStorageService.VerifyContainerAccessAsync"/>.
/// Registered by APFlow.Api (see ApiServiceCollectionExtensions.AddApiServices),
/// tagged "ready" - same pattern as the database (WP-003) and Graph (WP-004) checks.
/// SEVERITY: reports <see cref="HealthStatus.Degraded"/>, not
/// <see cref="HealthStatus.Unhealthy"/>, for the same reason as
/// GraphMailboxHealthCheck - see docs/WP-004-Health-Check-Severity-Decision.md, which
/// now also covers this check. Blob Storage is a dependency of specific capabilities
/// (document upload/download/SAS links), not of the whole API, so an outage here
/// should not fail general API readiness the way a database outage does.
/// </summary>
public sealed class BlobStorageHealthCheck : IHealthCheck
{
    private readonly IBlobStorageService _blobStorageService;

    /// <summary>Creates a new <see cref="BlobStorageHealthCheck"/>.</summary>
    public BlobStorageHealthCheck(IBlobStorageService blobStorageService)
    {
        _blobStorageService = blobStorageService;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var isAccessible = await _blobStorageService.VerifyContainerAccessAsync(cancellationToken);

        return isAccessible
            ? HealthCheckResult.Healthy("Blob Storage container access verified.")
            : HealthCheckResult.Degraded("Blob Storage container could not be verified. This does not fail overall API readiness - see docs/WP-004-Health-Check-Severity-Decision.md.");
    }
}
