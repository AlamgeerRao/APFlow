using APFlow.Api.Extensions;
using APFlow.Api.Middleware;
using APFlow.Application;
using APFlow.Infrastructure;
using APFlow.Infrastructure.Configuration;
using APFlow.Integrations;
using APFlow.Workers;
using Azure.Identity;

// TEMPORARY DIAGNOSTIC (2026-07-31) - remove once the live "IDX10214: Audience
// validation failed" investigation is closed. Microsoft.IdentityModel redacts
// actual claim values from its exception messages by default (the
// "See https://aka.ms/identitymodel/app-context-switches" suffix on that
// error is this redaction). IdentityModelEventSource.ShowPII alone did NOT
// unredact IDX10214 specifically - per the linked wiki
// (App-Context-Switches-in-IdentityModel), v8.8.0 moved that one behind its
// own dedicated switch instead.
AppContext.SetSwitch("Switch.DoNotScrubExceptions", true);
Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;

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
    .AddInfrastructure(builder.Configuration, builder.Environment)
    .AddIntegrations(builder.Configuration, builder.Environment)
    .AddWorkers()
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
