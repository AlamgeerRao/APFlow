using APFlow.Application.Features.Invoices;
using APFlow.Domain.Common.Constants;
using Xunit;

namespace APFlow.Application.Tests.Features.Invoices;

public class DuplicateOverrideAuthorizationServiceTests
{
    [Fact]
    public void CanOverrideDuplicateWarning_FinanceManagerRole_ReturnsTrue()
    {
        var service = new DuplicateOverrideAuthorizationService();

        var result = service.CanOverrideDuplicateWarning([Roles.FinanceManager]);

        Assert.True(result);
    }

    [Fact]
    public void CanOverrideDuplicateWarning_FinanceManagerAmongOtherRoles_ReturnsTrue()
    {
        var service = new DuplicateOverrideAuthorizationService();

        var result = service.CanOverrideDuplicateWarning([Roles.ReadOnly, Roles.FinanceManager, Roles.ApReviewer]);

        Assert.True(result);
    }

    [Theory]
    [InlineData(Roles.PlatformAdmin)]
    [InlineData(Roles.ApReviewer)]
    [InlineData(Roles.CreditController)]
    [InlineData(Roles.AccountsAdmin)]
    [InlineData(Roles.ReadOnly)]
    public void CanOverrideDuplicateWarning_AnyOtherSingleRole_ReturnsFalse(string role)
    {
        var service = new DuplicateOverrideAuthorizationService();

        var result = service.CanOverrideDuplicateWarning([role]);

        Assert.False(result);
    }

    [Fact]
    public void CanOverrideDuplicateWarning_NoRoles_ReturnsFalse()
    {
        var service = new DuplicateOverrideAuthorizationService();

        var result = service.CanOverrideDuplicateWarning([]);

        Assert.False(result);
    }
}
