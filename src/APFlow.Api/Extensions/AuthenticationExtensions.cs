using System.IdentityModel.Tokens.Jwt;
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

                // TEMPORARY DIAGNOSTIC (2026-07-31) - remove once the live "IDX10214:
                // Audience validation failed" investigation is closed. IdentityModel's
                // own exception message stays redacted even with ShowPII/
                // Switch.DoNotScrubExceptions set (see Program.cs) - decode the raw
                // token directly instead of continuing to fight that redaction.
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("TempJwtDiagnostics");

                        var authHeader = context.Request.Headers.Authorization.ToString();
                        const string bearerPrefix = "Bearer ";
                        if (authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                var jwt = new JwtSecurityTokenHandler().ReadJwtToken(authHeader[bearerPrefix.Length..]);
                                logger.LogWarning(
                                    "TEMP DIAGNOSTIC: token aud=[{Audiences}] iss={Issuer} appid={AppId} scp={Scp}",
                                    string.Join(", ", jwt.Audiences),
                                    jwt.Issuer,
                                    jwt.Claims.FirstOrDefault(c => c.Type == "azp" || c.Type == "appid")?.Value,
                                    jwt.Claims.FirstOrDefault(c => c.Type == "scp")?.Value);
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning(ex, "TEMP DIAGNOSTIC: failed to decode raw token for logging");
                            }
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        return services;
    }
}
