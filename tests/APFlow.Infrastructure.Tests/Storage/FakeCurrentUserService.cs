using APFlow.Application.Interfaces;

namespace APFlow.Infrastructure.Tests.Storage;

/// <summary>
/// Hand-written fake for <see cref="ICurrentUserService"/>, settable per test - lets
/// BlobStorageServiceTests exercise tenant-scoping without a real HttpContext/JWT.
/// </summary>
internal sealed class FakeCurrentUserService : ICurrentUserService
{
    public bool IsAuthenticated { get; set; } = true;

    public string? UserId { get; set; } = "fake-user";

    public string? Email { get; set; } = "fake-user@example.com";

    public string? TenantId { get; set; } = "11111111-1111-1111-1111-111111111111";

    public IReadOnlyCollection<string> Roles { get; set; } = [];

    public bool IsInRole(string role) => Roles.Contains(role);
}
