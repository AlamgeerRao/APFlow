using System.Security.Claims;
using APFlow.Domain.Common.Constants;
using APFlow.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace APFlow.Infrastructure.Tests.Security;

public class CurrentUserServiceTests
{
    [Fact]
    public void IsAuthenticated_NoHttpContext_ReturnsFalse()
    {
        var accessor = new HttpContextAccessor { HttpContext = null };
        var service = new CurrentUserService(accessor);

        Assert.False(service.IsAuthenticated);
        Assert.Null(service.UserId);
        Assert.Null(service.Email);
        Assert.Null(service.TenantId);
        Assert.Empty(service.Roles);
    }

    [Fact]
    public void IsAuthenticated_UnauthenticatedPrincipal_ReturnsFalse()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity()),
        };
        var service = new CurrentUserService(new HttpContextAccessor { HttpContext = context });

        Assert.False(service.IsAuthenticated);
    }

    [Fact]
    public void AuthenticatedUser_ExposesClaimsCorrectly()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim("oid", "user-123"),
                new Claim("tid", "tenant-456"),
                new Claim("preferred_username", "alice@example.com"),
                new Claim("roles", Roles.ApManager),
                new Claim("roles", Roles.ReadOnly),
            ],
            authenticationType: "Bearer",
            nameType: "preferred_username",
            roleType: "roles");

        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var service = new CurrentUserService(new HttpContextAccessor { HttpContext = context });

        Assert.True(service.IsAuthenticated);
        Assert.Equal("user-123", service.UserId);
        Assert.Equal("tenant-456", service.TenantId);
        Assert.Equal("alice@example.com", service.Email);
        Assert.Equal(2, service.Roles.Count);
        Assert.Contains(Roles.ApManager, service.Roles);
        Assert.Contains(Roles.ReadOnly, service.Roles);
    }

    [Fact]
    public void IsInRole_MatchingRole_ReturnsTrue()
    {
        var identity = new ClaimsIdentity(
            [new Claim("roles", Roles.Administrator)],
            authenticationType: "Bearer",
            nameType: ClaimTypes.Name,
            roleType: "roles");

        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var service = new CurrentUserService(new HttpContextAccessor { HttpContext = context });

        Assert.True(service.IsInRole(Roles.Administrator));
        Assert.False(service.IsInRole(Roles.Finance));
    }

    [Fact]
    public void Email_FallsBackToClaimTypesEmail_WhenPreferredUsernameAbsent()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Email, "bob@example.com")],
            authenticationType: "Bearer");

        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var service = new CurrentUserService(new HttpContextAccessor { HttpContext = context });

        Assert.Equal("bob@example.com", service.Email);
    }

    [Fact]
    public void UserId_FallsBackToClaimTypesNameIdentifier_WhenOidAbsent()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "fallback-user-id")],
            authenticationType: "Bearer");

        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var service = new CurrentUserService(new HttpContextAccessor { HttpContext = context });

        Assert.Equal("fallback-user-id", service.UserId);
    }
}
