using APFlow.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace APFlow.Api.Extensions;

/// <summary>
/// Registers JWT bearer authentication against Microsoft Entra External ID.
/// Uses only <c>Microsoft.AspNetCore.Authentication.JwtBearer</c>, which ships as
/// part of the ASP.NET Core shared framework - no additional NuGet package, per
/// Project Standards §2 ("prefer built-in .NET and Azure capabilities").
/// </summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// Registers JWT bearer authentication. Authentication is always wired (there is
    /// no config switch to disable it - unlike the Key Vault "Enabled" flag, an
    /// authentication on/off toggle is a security risk). If "EntraId:Authority" or
    /// "EntraId:Audience" are missing outside Development, startup fails fast rather
    /// than silently accepting unvalidated tokens.
    /// </summary>
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var entraIdOptions = configuration.GetSection(EntraIdOptions.SectionName).Get<EntraIdOptions>()
                              ?? new EntraIdOptions();

        var isConfigured = !string.IsNullOrWhiteSpace(entraIdOptions.Authority)
                            && !string.IsNullOrWhiteSpace(entraIdOptions.Audience);

        if (!isConfigured && !environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "EntraId:Authority and EntraId:Audience must be configured outside Development. " +
                "Refusing to start with authentication unconfigured in a non-Development environment.");
        }

        services.Configure<EntraIdOptions>(configuration.GetSection(EntraIdOptions.SectionName));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                if (!isConfigured)
                {
                    // Development-only convenience path: authentication is still wired
                    // (so [Authorize] behaves consistently everywhere) but there is no
                    // real tenant to validate against yet. Requests will simply fail
                    // authentication until EntraId:Authority/Audience are set.
                    return;
                }

                // ASP.NET Core's JwtBearer handler applies a legacy inbound claim-type
                // map by default (MapInboundClaims = true), silently rewriting short
                // Entra v2.0 claim names to old WS-Federation-era XML schema URIs -
                // e.g. "tid" becomes "http://schemas.microsoft.com/identity/claims/
                // tenantid", "oid" becomes ".../objectidentifier". Live-diagnosed
                // 2026-07-31: CurrentUserService.TenantId reads the literal "tid" claim
                // type with no fallback, so it was always null for every real
                // authenticated user (not something specific to any one test) - the
                // tenant query filter (AppDbContext.ApplyTenantAndSoftDeleteFilter)
                // then fails closed and every TenantEntity query returns zero rows.
                // Disabling the remap is the standard fix for exactly this class of
                // surprise with modern Entra v2.0 APIs - claims now come through under
                // their real JWT names, matching CurrentUserService's existing
                // "oid"/"tid"/"roles" assumptions.
                options.MapInboundClaims = false;

                options.Authority = entraIdOptions.Authority;
                options.Audience = entraIdOptions.Audience;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    // Entra ID v2.0 App Roles are emitted in the "roles" claim; map it
                    // explicitly so ClaimsPrincipal.IsInRole()/[Authorize(Roles=...)]
                    // work against it. ASSUMPTION - confirm against the actual tenant's
                    // token shape once the App Registration exists (see CurrentUserService).
                    RoleClaimType = "roles",
                    NameClaimType = "preferred_username",
                };
            });

        return services;
    }
}
