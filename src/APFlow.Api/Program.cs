using APFlow.Api.Extensions;
using APFlow.Api.Middleware;
using APFlow.Application;
using APFlow.Infrastructure;
using APFlow.Infrastructure.Configuration;
using APFlow.Integrations;
using APFlow.Workers;
using Azure.Identity;

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
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultOptions.VaultUri),
        new DefaultAzureCredential());
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
    .AddInfrastructure(builder.Configuration)
    .AddIntegrations()
    .AddWorkers()
    .AddApiServices(builder.Configuration)
    .AddApiAuthentication(builder.Configuration, builder.Environment)
    .AddApiAuthorization();

// NOTE: No CORS policy is configured. APFlow.Web (the React SPA) will need one
// to call this API cross-origin from a different host/port. Deferred to the
// work package that wires up APFlow.Web against this API - not implemented here.

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
