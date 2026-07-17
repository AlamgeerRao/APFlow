namespace APFlow.Application.Interfaces;

/// <summary>
/// Exposes identity information about the caller of the current request, derived from
/// the validated JWT. Application and Infrastructure code should depend on this
/// abstraction rather than reading <c>HttpContext</c>/<c>ClaimsPrincipal</c> directly,
/// keeping identity access testable in isolation from ASP.NET Core.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>Whether the current request has a validated, authenticated identity.</summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// The caller's stable unique identifier (Entra object id / "oid" claim), or
    /// <c>null</c> if unauthenticated.
    /// </summary>
    string? UserId { get; }

    /// <summary>The caller's email/username, or <c>null</c> if unauthenticated or not present.</summary>
    string? Email { get; }

    /// <summary>
    /// The caller's tenant identifier (Entra "tid" claim), for tenant-isolation checks,
    /// or <c>null</c> if unauthenticated.
    /// </summary>
    string? TenantId { get; }

    /// <summary>The set of application roles (see <c>Roles</c>) assigned to the caller.</summary>
    IReadOnlyCollection<string> Roles { get; }

    /// <summary>Whether the caller has been assigned the given role.</summary>
    bool IsInRole(string role);
}
