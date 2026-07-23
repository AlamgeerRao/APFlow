using APFlow.Application.Interfaces;
using APFlow.Domain.Common.Constants;
using APFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace APFlow.Infrastructure.Tests.Persistence;

/// <summary>
/// Exercises ApprovalPolicyRepository against a real AppDbContext (InMemory
/// provider), using the REAL seeded data (ApprovalPolicySeedData), not
/// test-constructed fakes - same approach as WorkflowTemplateRepositoryTests.
/// </summary>
public class ApprovalPolicyRepositoryTests
{
    [Fact]
    public async Task GetActivePolicyAsync_GbSkipsTenant_ReturnsSeededFinanceManagerPolicy()
    {
        using var context = CreateContext(WorkflowSeedData.GbSkipsPlaceholderTenantId);
        var repository = new ApprovalPolicyRepository(context);

        var policy = await repository.GetActivePolicyAsync(ApprovalDomains.InvoiceApproval);

        Assert.NotNull(policy);
        Assert.Equal(WorkflowSeedData.GbSkipsPlaceholderTenantId, policy!.TenantId);
        Assert.Equal(Roles.FinanceManager, policy.RequiredRole);
        Assert.False(policy.RequiresDualControl);
    }

    [Fact]
    public async Task GetActivePolicyAsync_OtherTenant_NoPolicyConfigured_ReturnsNull()
    {
        // No platform-default InvoiceApproval policy is seeded (task 3 only seeds
        // GB Skips') - a tenant with no policy of its own correctly sees none at
        // all, not GB Skips' policy.
        using var context = CreateContext(Guid.NewGuid());
        var repository = new ApprovalPolicyRepository(context);

        var policy = await repository.GetActivePolicyAsync(ApprovalDomains.InvoiceApproval);

        Assert.Null(policy);
    }

    [Fact]
    public async Task GetActivePolicyAsync_PaymentBatchApprovalDomain_NoPolicySeeded_ReturnsNull()
    {
        // Task 5: the domain is defined but no policy is seeded against it yet.
        using var context = CreateContext(WorkflowSeedData.GbSkipsPlaceholderTenantId);
        var repository = new ApprovalPolicyRepository(context);

        var policy = await repository.GetActivePolicyAsync(ApprovalDomains.PaymentBatchApproval);

        Assert.Null(policy);
    }

    private static AppDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options, new FakeCurrentUserService(tenantId));
        context.Database.EnsureCreated();
        return context;
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public FakeCurrentUserService(Guid tenantId)
        {
            TenantId = tenantId.ToString();
        }

        public bool IsAuthenticated => true;
        public string? UserId => "test-user";
        public string? Email => null;
        public string? TenantId { get; }
        public IReadOnlyCollection<string> Roles => [];
        public bool IsInRole(string role) => false;
    }
}
