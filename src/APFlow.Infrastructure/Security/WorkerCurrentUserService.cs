using APFlow.Application.Interfaces;
using APFlow.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace APFlow.Infrastructure.Security;

/// <summary>
/// <see cref="ICurrentUserService"/> for DI scopes with no HTTP request -
/// specifically, <c>APFlow.Workers</c>' <c>EmailIngestionWorker</c> polling
/// cycles, which create their own scope via <c>IServiceScopeFactory</c> on a
/// timer, not from an inbound request. <see cref="CurrentUserService"/> (the
/// real, HTTP-path implementation) reads <c>IHttpContextAccessor.HttpContext</c>,
/// which is always null there, so every tenant-scoped call downstream
/// (<c>BlobStorageService</c>'s <c>ScopeBlobName</c>, <c>AppDbContext</c>'s tenant
/// query filter and audit stamping) failed or silently produced orphaned,
/// unreachable rows (<c>TenantId == Guid.Empty</c>) - see WP-069.
/// Selected instead of <see cref="CurrentUserService"/> purely by the DI
/// registration in <c>DependencyInjection.AddInfrastructure</c> (a factory that
/// checks for a live <c>HttpContext</c>), never by changing
/// <see cref="CurrentUserService"/> itself, which remains untouched and behaves
/// identically for every real HTTP request.
/// </summary>
public sealed class WorkerCurrentUserService : ICurrentUserService
{
    private readonly WorkerIdentityOptions _options;

    /// <summary>Creates a new <see cref="WorkerCurrentUserService"/>.</summary>
    public WorkerCurrentUserService(IOptions<WorkerIdentityOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public bool IsAuthenticated => true;

    /// <inheritdoc />
    public string? UserId => "system:email-ingestion-worker";

    /// <inheritdoc />
    public string? Email => null;

    /// <inheritdoc />
    public string? DisplayName => "Email Ingestion Worker";

    /// <inheritdoc />
    public string? TenantId => _options.TenantId;

    /// <inheritdoc />
    public IReadOnlyCollection<string> Roles => [];

    /// <inheritdoc />
    public bool IsInRole(string role) => false;
}
