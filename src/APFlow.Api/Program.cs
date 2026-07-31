using APFlow.Api.Extensions;
using APFlow.Api.Middleware;
using APFlow.Application;
using APFlow.Infrastructure;
using APFlow.Infrastructure.Configuration;
using APFlow.Integrations;
using APFlow.Workers;
using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration -----------------------------------------------------
// Optionally load secrets from Azure Key Vault. Disabled by default so local
// development and this solution's own build/test do not require an Azure
// resource; enable via "AzureKeyVault:Enabled" in appsettings per environment.
var keyVaultOptions = builder.Configuration
    .GetSection(KeyVaultOptions.SectionName)
    .Get<KeyVaultOptions>();

if (keyVaultOptions is { Enabled: true } && !string.IsNullOrWhiteSpace(keyVaultOptions.VaultUri))
{
    var keyVaultUri = new Uri(keyVaultOptions.VaultUri);
    var keyVaultCredential = new DefaultAzureCredential();

    builder.Configuration.AddAzureKeyVault(keyVaultUri, keyVaultCredential);

    // WP-068: the generic AddAzureKeyVault call above maps each secret's raw
    // NAME directly into configuration (translating "--" to ":"), which does
    // NOT help Graph:ClientSecret specifically - that secret is deliberately
    // stored under this project's own "graph-secret-{tenantId}" naming
    // convention (docs/Secret-Naming-Convention.md), not "Graph--ClientSecret",
    // because the future Per-Tenant Graph Configuration work
    // (docs/WP-004-Graph-Multitenancy-Decision.md) needs one differently-named
    // secret per tenant, not a single fixed config key. This was the actual
    // root cause of live Graph auth failing with a 401 the whole session
    // (confirmed via WP-067's EmailIngestionWorker logs, not guessed): even
    // with AzureKeyVault now enabled, nothing ever read this specific secret
    // and put it where GraphOptions.ClientSecret binds from, so it was always
    // blank, silently falling back to DefaultAzureCredential/Managed Identity
    // - which was never granted the Graph Mail.Read application permission.
    var graphTenantId = builder.Configuration["Graph:TenantId"];
    if (!string.IsNullOrWhiteSpace(graphTenantId))
    {
        var secretClient = new SecretClient(keyVaultUri, keyVaultCredential);
        try
        {
            var graphSecret = secretClient.GetSecret($"graph-secret-{graphTenantId}");
            builder.Configuration.AddInMemoryCollection(
                [new KeyValuePair<string, string?>("Graph:ClientSecret", graphSecret.Value.Value)]);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // No secret stored for this tenant yet - Graph:ClientSecret stays
            // blank, and GraphOptions' own documented fallback (Managed
            // Identity via DefaultAzureCredential) applies, exactly as before
            // this fix. Not a startup failure: a missing secret is a
            // legitimate, expected state for an environment that genuinely
            // intends to rely on Managed Identity for Graph instead.
        }
    }
}

// --- Logging -------------------------------------------------------------
// Built-in Microsoft.Extensions.Logging only, per Project Standards §2
// ("prefer built-in .NET and Azure capabilities"). Providers and levels are
// configured via the standard "Logging" section in appsettings per environment.
// The default host configuration already includes the Console provider; no
// additional wiring is required here.

// --- Dependency Injection --------------------------------------------------
builder.Services.AddControllers();
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration, builder.Environment)
    .AddIntegrations(builder.Configuration, builder.Environment)
    .AddWorkers(builder.Configuration)
    .AddApiServices(builder.Configuration, builder.Environment)
    .AddApiAuthentication(builder.Configuration, builder.Environment)
    .AddApiAuthorization();

var app = builder.Build();

// --- Middleware Pipeline -----------------------------------------------
// Ordering matters and follows Microsoft's canonical sequence: exception handling
// first (outermost, catches everything below it, including auth failures routed
// through it); then authentication/authorization; then all endpoint mappings.
// Deliberately placing UseAuthentication/UseAuthorization BEFORE the Map* calls
// below, rather than interleaved after them, to avoid relying on ASP.NET Core's
// implicit routing-insertion behavior in minimal hosting.
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();

// WP-059 Part B: named CORS policy (allowed origins from configuration - see
// AddApiServices/CorsOptions). Must run before UseAuthentication/UseAuthorization
// so a preflight OPTIONS request (which never carries an Authorization header)
// is answered without being rejected by auth middleware first.
app.UseApiCors();

// WP-002: Microsoft Entra External ID JWT bearer authentication, with a
// solution-wide fallback authorization policy requiring an authenticated caller
// on every endpoint unless explicitly marked [AllowAnonymous] (see
// AddApiAuthorization). Do not add [Authorize(Roles = "...")] using ad-hoc role
// strings - use the named policies in AuthorizationExtensions or the Roles
// constants in APFlow.Domain.Common.Constants.
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseApiOpenApi();
}

app.UseApiHealthChecks();

app.MapControllers();

app.Run();

/// <summary>
/// Exposed as a public partial class so integration tests can bootstrap this
/// application via <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program
{
}
