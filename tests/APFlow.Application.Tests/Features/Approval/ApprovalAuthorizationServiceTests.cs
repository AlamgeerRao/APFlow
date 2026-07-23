using APFlow.Application.Features.Approval;
using APFlow.Application.Tests.Features;
using APFlow.Domain.Common.Constants;
using APFlow.Domain.Entities;
using Xunit;

namespace APFlow.Application.Tests.Features.Approval;

public class ApprovalAuthorizationServiceTests
{
    [Fact]
    public async Task AuthorizeAsync_ActingUserHasRequiredRole_Succeeds()
    {
        var repository = new FakeApprovalPolicyRepository();
        repository.Policies.Add(new ApprovalPolicy { Domain = ApprovalDomains.InvoiceApproval, RequiredRole = Roles.FinanceManager });
        var service = new ApprovalAuthorizationService(repository);

        var result = await service.AuthorizeAsync(ApprovalDomains.InvoiceApproval, [Roles.FinanceManager]);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task AuthorizeAsync_ActingUserLacksRequiredRole_Rejected()
    {
        var repository = new FakeApprovalPolicyRepository();
        repository.Policies.Add(new ApprovalPolicy { Domain = ApprovalDomains.InvoiceApproval, RequiredRole = Roles.FinanceManager });
        var service = new ApprovalAuthorizationService(repository);

        var result = await service.AuthorizeAsync(ApprovalDomains.InvoiceApproval, [Roles.ApReviewer]);

        Assert.True(result.IsFailure);
        Assert.Equal("Approval.Unauthorized", result.Error.Code);
    }

    [Fact]
    public async Task AuthorizeAsync_ActingUserHasRequiredRoleAmongOthers_Succeeds()
    {
        var repository = new FakeApprovalPolicyRepository();
        repository.Policies.Add(new ApprovalPolicy { Domain = ApprovalDomains.InvoiceApproval, RequiredRole = Roles.FinanceManager });
        var service = new ApprovalAuthorizationService(repository);

        var result = await service.AuthorizeAsync(ApprovalDomains.InvoiceApproval, [Roles.ReadOnly, Roles.FinanceManager]);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task AuthorizeAsync_NoPolicyConfiguredForDomain_FailsClosed()
    {
        var repository = new FakeApprovalPolicyRepository();
        var service = new ApprovalAuthorizationService(repository);

        var result = await service.AuthorizeAsync(ApprovalDomains.InvoiceApproval, [Roles.FinanceManager]);

        Assert.True(result.IsFailure);
        Assert.Equal("Approval.PolicyNotConfigured", result.Error.Code);
    }

    [Fact]
    public async Task AuthorizeAsync_NoRoles_Rejected()
    {
        var repository = new FakeApprovalPolicyRepository();
        repository.Policies.Add(new ApprovalPolicy { Domain = ApprovalDomains.InvoiceApproval, RequiredRole = Roles.FinanceManager });
        var service = new ApprovalAuthorizationService(repository);

        var result = await service.AuthorizeAsync(ApprovalDomains.InvoiceApproval, []);

        Assert.True(result.IsFailure);
        Assert.Equal("Approval.Unauthorized", result.Error.Code);
    }

    [Fact]
    public async Task AuthorizeAsync_TenantSpecificPolicyPreferredOverPlatformDefault()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeApprovalPolicyRepository { CurrentTenantId = tenantId };
        repository.Policies.Add(new ApprovalPolicy { Domain = ApprovalDomains.InvoiceApproval, TenantId = null, RequiredRole = Roles.PlatformAdmin });
        repository.Policies.Add(new ApprovalPolicy { Domain = ApprovalDomains.InvoiceApproval, TenantId = tenantId, RequiredRole = Roles.FinanceManager });
        var service = new ApprovalAuthorizationService(repository);

        var result = await service.AuthorizeAsync(ApprovalDomains.InvoiceApproval, [Roles.FinanceManager]);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task AuthorizeAsync_BlankDomain_ReturnsFailure()
    {
        var repository = new FakeApprovalPolicyRepository();
        var service = new ApprovalAuthorizationService(repository);

        var result = await service.AuthorizeAsync("", [Roles.FinanceManager]);

        Assert.True(result.IsFailure);
        Assert.Equal("Approval.InvalidDomain", result.Error.Code);
    }
}
