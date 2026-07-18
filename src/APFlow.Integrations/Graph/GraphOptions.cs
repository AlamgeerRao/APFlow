namespace APFlow.Integrations.Graph;

/// <summary>
/// Binds the "Graph" configuration section: Microsoft Graph app-only authentication
/// and mailbox configuration.
/// IMPORTANT: <see cref="TenantId"/> here is UNRELATED to "EntraId:TenantId" (WP-002).
/// EntraId:TenantId is APFlow's own Entra External ID (CIAM) tenant, used to
/// authenticate end users logging into AP Flow. Graph:TenantId is the Microsoft 365 /
/// Entra ID tenant that owns the mailbox being read - in the current MVP scope, this
/// is the initial customer's (GB Skips') own tenant, not APFlow's. Do not merge or
/// reuse these two tenant configurations.
/// See docs/WP-004-Graph-Multitenancy-Decision.md for why this is a single, app-wide
/// configuration rather than per-tenant, and what has to change before a second
/// customer with their own mailbox is onboarded.
/// </summary>
public sealed class GraphOptions
{
    /// <summary>The configuration section name this class binds to.</summary>
    public const string SectionName = "Graph";

    /// <summary>
    /// The Microsoft 365 / Entra ID tenant hosting the mailbox. NOT the same tenant as
    /// EntraId:TenantId - see the type-level remarks.
    /// </summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>
    /// The Application (client) ID of the Graph App Registration, granted application
    /// permissions (e.g. Mail.Read) with admin consent in the tenant identified by
    /// <see cref="TenantId"/>.
    /// </summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>
    /// Client secret for the App Registration. OPTIONAL: if left blank, authentication
    /// falls back to <c>DefaultAzureCredential</c> (Managed Identity in Azure, or local
    /// developer tool credentials) instead - the more secure, secret-less path, where
    /// the hosting identity itself has been granted the Graph application permissions.
    /// If set, this must come from Key Vault - never a literal value in appsettings.
    /// </summary>
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>
    /// The mailbox this service operates against - a user mailbox or shared mailbox
    /// UPN/address, e.g. "ap-invoices@clienttenant.onmicrosoft.com".
    /// </summary>
    public string MailboxUserPrincipalName { get; init; } = string.Empty;
}
