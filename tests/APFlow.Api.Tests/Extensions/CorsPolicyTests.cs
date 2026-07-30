using APFlow.Api.Configuration;
using APFlow.Api.Extensions;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace APFlow.Api.Tests.Extensions;

public class CorsPolicyTests
{
    [Fact]
    public void ConfigureCorsPolicy_OriginsConfigured_AllowsExactlyThoseOrigins()
    {
        var options = new CorsPolicyOptions
        {
            AllowedOrigins = ["https://app-apflow-dev-web-ryd3y6fyfloxu.azurewebsites.net", "http://localhost:5173"],
        };
        var builder = new CorsPolicyBuilder();

        ApiServiceCollectionExtensions.ConfigureCorsPolicy(builder, options, new FakeHostEnvironment("Production"));
        var policy = builder.Build();

        Assert.False(policy.AllowAnyOrigin);
        Assert.Equal(
            ["https://app-apflow-dev-web-ryd3y6fyfloxu.azurewebsites.net", "http://localhost:5173"],
            policy.Origins);
        Assert.True(policy.AllowAnyHeader);
        Assert.True(policy.AllowAnyMethod);
        Assert.False(policy.SupportsCredentials); // Bearer token, not cookies - see ConfigureCorsPolicy's own doc comment
    }

    [Fact]
    public void ConfigureCorsPolicy_OriginsConfigured_AppliesRegardlessOfEnvironment()
    {
        // Explicit configuration wins in EVERY environment, including Development -
        // this is not a "only matters outside dev" branch.
        var options = new CorsPolicyOptions { AllowedOrigins = ["https://example.com"] };
        var builder = new CorsPolicyBuilder();

        ApiServiceCollectionExtensions.ConfigureCorsPolicy(builder, options, new FakeHostEnvironment("Development"));
        var policy = builder.Build();

        Assert.False(policy.AllowAnyOrigin);
        Assert.Equal(["https://example.com"], policy.Origins);
    }

    [Fact]
    public void ConfigureCorsPolicy_NoOriginsConfigured_DevelopmentEnvironment_PermissiveFallback()
    {
        var options = new CorsPolicyOptions(); // AllowedOrigins defaults to empty
        var builder = new CorsPolicyBuilder();

        ApiServiceCollectionExtensions.ConfigureCorsPolicy(builder, options, new FakeHostEnvironment("Development"));
        var policy = builder.Build();

        Assert.True(policy.AllowAnyOrigin);
        Assert.True(policy.AllowAnyHeader);
        Assert.True(policy.AllowAnyMethod);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("SomeOtherEnvironmentName")]
    public void ConfigureCorsPolicy_NoOriginsConfigured_NonDevelopmentEnvironment_FailsClosed_NotPermissive(string environmentName)
    {
        // WP-059 required behavior: a deployed environment with no explicit
        // allow-list must NOT silently fall back to permissive - it must allow
        // nothing until Cors:AllowedOrigins is actually configured.
        var options = new CorsPolicyOptions();
        var builder = new CorsPolicyBuilder();

        ApiServiceCollectionExtensions.ConfigureCorsPolicy(builder, options, new FakeHostEnvironment(environmentName));
        var policy = builder.Build();

        Assert.False(policy.AllowAnyOrigin);
        Assert.Empty(policy.Origins);
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "APFlow.Api.Tests";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
