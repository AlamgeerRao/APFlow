namespace APFlow.Infrastructure.Configuration;

/// <summary>
/// Binds the "EntraId" configuration section, used to configure JWT bearer
/// authentication against Microsoft Entra External ID. None of these values are
/// secrets (they are public identifiers embedded in every issued token/discovery
/// document), so unlike Key Vault this section is safe to keep in appsettings -
/// but the actual values are environment-specific and must be filled in per
/// deployment; they are intentionally blank here.
/// </summary>
public sealed class EntraIdOptions
{
    /// <summary>The configuration section name this class binds to.</summary>
    public const string SectionName = "EntraId";

    /// <summary>
    /// The Entra External ID (CIAM) tenant authority, e.g.
    /// "https://{tenantSubdomain}.ciamlogin.com/{tenantId}/v2.0". Used as the JWT
    /// bearer Authority for metadata/signing-key discovery.
    /// </summary>
    public string Authority { get; init; } = string.Empty;

    /// <summary>The Entra tenant id (GUID).</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>
    /// The Application (client) ID of this API's own App Registration. Used as the
    /// expected token audience.
    /// </summary>
    public string Audience { get; init; } = string.Empty;
}
