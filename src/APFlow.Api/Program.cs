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
    .AddApiServices(builder.Configuration);

// NOTE: No CORS policy is configured. APFlow.Web (the React SPA) will need one
// to call this API cross-origin from a different host/port. Deferred to the
// work package that wires up APFlow.Web against this API - not implemented here.

var app = builder.Build();

// --- Middleware Pipeline -----------------------------------------------
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseApiOpenApi();
}

app.UseApiHealthChecks();

app.UseHttpsRedirection();

// NOTE: No UseAuthentication()/UseAuthorization() pipeline is wired up yet.
// No Microsoft Entra External ID scheme is registered anywhere in this solution.
// Adding UseAuthorization() against an unauthenticated pipeline would be
// misleading, so it is deliberately omitted rather than added as a no-op.
// This is intentionally deferred to the authentication work package - see
// WP-001 review notes. Do not add [Authorize] attributes to controllers until
// that work package lands.

app.MapControllers();

app.Run();

/// <summary>
/// Exposed as a public partial class so integration tests can bootstrap this
/// application via <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program
{
}
