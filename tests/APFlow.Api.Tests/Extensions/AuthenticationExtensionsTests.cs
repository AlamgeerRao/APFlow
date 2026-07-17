using APFlow.Api.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace APFlow.Api.Tests.Extensions;

public class AuthenticationExtensionsTests
{
    [Fact]
    public void AddApiAuthentication_MissingConfig_OutsideDevelopment_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(authority: null, audience: null);
        var environment = new FakeHostEnvironment("Production");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddApiAuthentication(configuration, environment));

        Assert.Contains("EntraId:Authority", exception.Message);
    }

    [Fact]
    public void AddApiAuthentication_MissingConfig_InDevelopment_DoesNotThrow()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(authority: null, audience: null);
        var environment = new FakeHostEnvironment("Development");

        var exception = Record.Exception(() =>
            services.AddApiAuthentication(configuration, environment));

        Assert.Null(exception);
    }

    [Fact]
    public async Task AddApiAuthentication_MissingConfig_InDevelopment_StillRegistersJwtBearerScheme()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(authority: null, audience: null);
        var environment = new FakeHostEnvironment("Development");

        services.AddApiAuthentication(configuration, environment);
        var provider = services.BuildServiceProvider();
        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var scheme = await schemeProvider.GetSchemeAsync(JwtBearerDefaults.AuthenticationScheme);

        Assert.NotNull(scheme);
    }

    [Fact]
    public async Task AddApiAuthentication_FullyConfigured_OutsideDevelopment_DoesNotThrow_AndRegistersScheme()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            authority: "https://tenant.ciamlogin.com/tenant-id/v2.0",
            audience: "api-client-id");
        var environment = new FakeHostEnvironment("Production");

        var exception = Record.Exception(() =>
            services.AddApiAuthentication(configuration, environment));

        Assert.Null(exception);

        var provider = services.BuildServiceProvider();
        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var scheme = await schemeProvider.GetSchemeAsync(JwtBearerDefaults.AuthenticationScheme);

        Assert.NotNull(scheme);
    }

    [Fact]
    public void AddApiAuthentication_OnlyAuthorityMissing_OutsideDevelopment_Throws()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(authority: null, audience: "api-client-id");
        var environment = new FakeHostEnvironment("Production");

        Assert.Throws<InvalidOperationException>(() =>
            services.AddApiAuthentication(configuration, environment));
    }

    [Fact]
    public void AddApiAuthentication_OnlyAudienceMissing_OutsideDevelopment_Throws()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(authority: "https://tenant.ciamlogin.com/tenant-id/v2.0", audience: null);
        var environment = new FakeHostEnvironment("Production");

        Assert.Throws<InvalidOperationException>(() =>
            services.AddApiAuthentication(configuration, environment));
    }

    private static IConfiguration BuildConfiguration(string? authority, string? audience)
    {
        var values = new Dictionary<string, string?>
        {
            ["EntraId:Authority"] = authority,
            ["EntraId:Audience"] = audience,
            ["EntraId:TenantId"] = "tenant-id",
        };

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    /// <summary>Minimal <see cref="IHostEnvironment"/> test double - avoids depending on internal Hosting types.</summary>
    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "APFlow.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
