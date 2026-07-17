using System.Security.Claims;
using APFlow.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace APFlow.Infrastructure.Security;

/// <summary>
/// Reads caller identity from the current <see cref="HttpContext"/>'s validated
/// <see cref="ClaimsPrincipal"/>. Registered per-request (scoped) since it depends on
/// <see cref="IHttpContextAccessor"/>.
/// ASSUMPTION (flag for confirmation once a real Entra External ID App Registration
/// exists): claim names below follow standard Entra ID v2.0 token conventions -
/// "oid" for the user's object id, "tid" for tenant id, and "roles" for App Roles
/// (configured via <c>RoleClaimType = "roles"</c> on JWT bearer options). Email is
/// read from the standard "preferred_username" claim, falling back to
/// <see cref="ClaimTypes.Email"/>. If the actual tenant's token shape differs, update
/// this class - do not change the <see cref="ICurrentUserService"/> contract.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private const string ObjectIdClaimType = "oid";
    private const string TenantIdClaimType = "tid";
    private const string PreferredUsernameClaimType = "preferred_username";
    private const string RoleClaimType = "roles";

    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>Creates a new <see cref="CurrentUserService"/>.</summary>
    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    /// <inheritdoc />
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc />
    public string? UserId => FindClaim(ObjectIdClaimType) ?? FindClaim(ClaimTypes.NameIdentifier);

    /// <inheritdoc />
    public string? Email => FindClaim(PreferredUsernameClaimType) ?? FindClaim(ClaimTypes.Email);

    /// <inheritdoc />
    public string? TenantId => FindClaim(TenantIdClaimType);

    /// <inheritdoc />
    public IReadOnlyCollection<string> Roles =>
        User?.FindAll(RoleClaimType).Select(c => c.Value).ToArray()
        ?? [];

    /// <inheritdoc />
    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;

    private string? FindClaim(string claimType) => User?.FindFirst(claimType)?.Value;
}
