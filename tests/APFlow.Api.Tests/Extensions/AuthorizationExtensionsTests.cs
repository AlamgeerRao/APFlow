using APFlow.Api.Extensions;
using APFlow.Domain.Common.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace APFlow.Api.Tests.Extensions;

public class AuthorizationExtensionsTests
{
    [Theory]
    [InlineData(AuthorizationExtensions.RequirePlatformAdmin, Roles.PlatformAdmin)]
    [InlineData(AuthorizationExtensions.RequireApReviewer, Roles.ApReviewer)]
    [InlineData(AuthorizationExtensions.RequireFinanceManager, Roles.FinanceManager)]
    [InlineData(AuthorizationExtensions.RequireCreditController, Roles.CreditController)]
    [InlineData(AuthorizationExtensions.RequireAccountsAdmin, Roles.AccountsAdmin)]
    [InlineData(AuthorizationExtensions.RequireReadOnly, Roles.ReadOnly)]
    public async Task NamedPolicy_RequiresExpectedRole(string policyName, string expectedRole)
    {
        var provider = BuildAuthorizationPolicyProvider();

        var policy = await provider.GetPolicyAsync(policyName);

        Assert.NotNull(policy);
        var roleRequirement = Assert.Single(policy.Requirements.OfType<RolesAuthorizationRequirement>());
        Assert.Contains(expectedRole, roleRequirement.AllowedRoles);
    }

    [Fact]
    public async Task FallbackPolicy_RequiresAuthenticatedUser()
    {
        var provider = BuildAuthorizationPolicyProvider();

        var fallbackPolicy = await provider.GetFallbackPolicyAsync();

        Assert.NotNull(fallbackPolicy);
        Assert.Contains(fallbackPolicy.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
    }

    private static IAuthorizationPolicyProvider BuildAuthorizationPolicyProvider()
    {
        var services = new ServiceCollection();
        services.AddApiAuthorization();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IAuthorizationPolicyProvider>();
    }
}
