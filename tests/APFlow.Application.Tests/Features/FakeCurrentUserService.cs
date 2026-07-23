using APFlow.Application.Interfaces;

namespace APFlow.Application.Tests.Features;

/// <summary>Hand-written fake, same pattern as every fake elsewhere in this codebase. Defaults to an authenticated caller with no roles.</summary>
internal sealed class FakeCurrentUserService : ICurrentUserService
{
    public bool IsAuthenticated { get; set; } = true;
    public string? UserId { get; set; } = "test-user";
    public string? Email { get; set; }
    public string? TenantId { get; set; }
    public List<string> RolesList { get; } = [];

    public IReadOnlyCollection<string> Roles => RolesList;

    public bool IsInRole(string role) => RolesList.Contains(role);
}
