using APFlow.Domain.Common.Constants;
using Microsoft.AspNetCore.Authorization;

namespace APFlow.Api.Extensions;

/// <summary>
/// Registers role-based authorization policies, one per role defined in
/// <see cref="Roles"/>, plus a fallback policy that requires authentication on every
/// endpoint unless explicitly marked <c>[AllowAnonymous]</c> (secure by default).
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>Policy name requiring the <see cref="Roles.Administrator"/> role.</summary>
    public const string RequireAdministrator = "RequireAdministrator";

    /// <summary>Policy name requiring the <see cref="Roles.ApManager"/> role.</summary>
    public const string RequireApManager = "RequireApManager";

    /// <summary>Policy name requiring the <see cref="Roles.ApClerk"/> role.</summary>
    public const string RequireApClerk = "RequireApClerk";

    /// <summary>Policy name requiring the <see cref="Roles.Finance"/> role.</summary>
    public const string RequireFinance = "RequireFinance";

    /// <summary>Policy name requiring the <see cref="Roles.ReadOnly"/> role.</summary>
    public const string RequireReadOnly = "RequireReadOnly";

    /// <summary>Registers authorization services, per-role policies, and the fallback policy.</summary>
    public static IServiceCollection AddApiAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(RequireAdministrator, policy => policy.RequireRole(Roles.Administrator))
            .AddPolicy(RequireApManager, policy => policy.RequireRole(Roles.ApManager))
            .AddPolicy(RequireApClerk, policy => policy.RequireRole(Roles.ApClerk))
            .AddPolicy(RequireFinance, policy => policy.RequireRole(Roles.Finance))
            .AddPolicy(RequireReadOnly, policy => policy.RequireRole(Roles.ReadOnly))
            // Secure by default: every endpoint requires an authenticated caller
            // unless it explicitly opts out with [AllowAnonymous] (e.g. health checks).
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        return services;
    }
}
