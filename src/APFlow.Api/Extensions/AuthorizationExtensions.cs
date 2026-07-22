using APFlow.Domain.Common.Constants;
using Microsoft.AspNetCore.Authorization;

namespace APFlow.Api.Extensions;

/// <summary>
/// Registers role-based authorization policies, one per role defined in
/// <see cref="Roles"/> (WP-046: docs/06_Domain_Reference_Data.md §1 / SA-007 E-05),
/// plus a fallback policy that requires authentication on every endpoint unless
/// explicitly marked <c>[AllowAnonymous]</c> (secure by default).
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>Policy name requiring the <see cref="Roles.PlatformAdmin"/> role.</summary>
    public const string RequirePlatformAdmin = "RequirePlatformAdmin";

    /// <summary>Policy name requiring the <see cref="Roles.ApReviewer"/> role.</summary>
    public const string RequireApReviewer = "RequireApReviewer";

    /// <summary>Policy name requiring the <see cref="Roles.FinanceManager"/> role.</summary>
    public const string RequireFinanceManager = "RequireFinanceManager";

    /// <summary>Policy name requiring the <see cref="Roles.CreditController"/> role.</summary>
    public const string RequireCreditController = "RequireCreditController";

    /// <summary>Policy name requiring the <see cref="Roles.AccountsAdmin"/> role.</summary>
    public const string RequireAccountsAdmin = "RequireAccountsAdmin";

    /// <summary>Policy name requiring the <see cref="Roles.ReadOnly"/> role.</summary>
    public const string RequireReadOnly = "RequireReadOnly";

    /// <summary>Registers authorization services, per-role policies, and the fallback policy.</summary>
    public static IServiceCollection AddApiAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(RequirePlatformAdmin, policy => policy.RequireRole(Roles.PlatformAdmin))
            .AddPolicy(RequireApReviewer, policy => policy.RequireRole(Roles.ApReviewer))
            .AddPolicy(RequireFinanceManager, policy => policy.RequireRole(Roles.FinanceManager))
            .AddPolicy(RequireCreditController, policy => policy.RequireRole(Roles.CreditController))
            .AddPolicy(RequireAccountsAdmin, policy => policy.RequireRole(Roles.AccountsAdmin))
            .AddPolicy(RequireReadOnly, policy => policy.RequireRole(Roles.ReadOnly))
            // Secure by default: every endpoint requires an authenticated caller
            // unless it explicitly opts out with [AllowAnonymous] (e.g. health checks).
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        return services;
    }
}
