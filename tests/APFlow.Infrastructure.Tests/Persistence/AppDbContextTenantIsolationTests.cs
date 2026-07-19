using APFlow.Application.Interfaces;
using APFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace APFlow.Infrastructure.Tests.Persistence;

/// <summary>
/// Proves the tenant query filter added in WP-009 does NOT suffer the EF Core
/// model-caching pitfall documented in docs/WP-003-Tenant-Isolation-Decision.md:
/// that a filter capturing the tenant id as a local variable at model-build time
/// would freeze to whichever tenant happened to create the first DbContext instance
/// in the process, silently leaking data across every tenant after that.
/// This is exactly the scenario the decision doc asked to be proven before trusting
/// the fix: multiple DbContext instances, simulating different tenants, against the
/// same underlying store, in the same test process (so the SAME cached model is
/// necessarily reused across all of them - if the bug existed, this is where it
/// would show up).
/// </summary>
public class AppDbContextTenantIsolationTests
{
    [Fact]
    public void DifferentDbContextInstances_EachSeesOnlyOwnTenantData()
    {
        var databaseName = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        using (var contextA = CreateContext(databaseName, tenantA))
        {
            contextA.TestEntities.Add(new TestTenantEntity { Name = "Tenant A Item" });
            contextA.SaveChanges();
        }

        using (var contextB = CreateContext(databaseName, tenantB))
        {
            contextB.TestEntities.Add(new TestTenantEntity { Name = "Tenant B Item" });
            contextB.SaveChanges();
        }

        // Query as tenant A, in a THIRD context instance (not the one that wrote
        // tenant A's data) - if the model-caching bug existed, this would either see
        // both rows, or see tenant B's row instead of tenant A's, depending on
        // instance-creation order.
        using (var queryAsA = CreateContext(databaseName, tenantA))
        {
            var visible = queryAsA.TestEntities.ToList();

            Assert.Single(visible);
            Assert.Equal("Tenant A Item", visible[0].Name);
        }

        using (var queryAsB = CreateContext(databaseName, tenantB))
        {
            var visible = queryAsB.TestEntities.ToList();

            Assert.Single(visible);
            Assert.Equal("Tenant B Item", visible[0].Name);
        }
    }

    [Fact]
    public void NoResolvableTenant_SeesNoRows_FailsClosedNotOpen()
    {
        var databaseName = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();

        using (var contextA = CreateContext(databaseName, tenantA))
        {
            contextA.TestEntities.Add(new TestTenantEntity { Name = "Tenant A Item" });
            contextA.SaveChanges();
        }

        // No current user / no resolvable tenant - must see NOTHING, not everything.
        using var anonymousContext = CreateContext(databaseName, tenantId: null);
        var visible = anonymousContext.TestEntities.ToList();

        Assert.Empty(visible);
    }

    [Fact]
    public void ThirdTenant_WithNoData_SeesEmptyList_NotOtherTenantsData()
    {
        var databaseName = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var tenantC = Guid.NewGuid();

        using (var contextA = CreateContext(databaseName, tenantA))
        {
            contextA.TestEntities.Add(new TestTenantEntity { Name = "Tenant A Item" });
            contextA.SaveChanges();
        }

        using var contextC = CreateContext(databaseName, tenantC);
        var visible = contextC.TestEntities.ToList();

        Assert.Empty(visible);
    }

    [Fact]
    public void IgnoreQueryFilters_CanStillSeeAllTenantsData_ForAdminDiagnosticUseOnly()
    {
        // Documents an intentional escape hatch (standard EF Core mechanism, not
        // something WP-009 built): IgnoreQueryFilters() bypasses both soft-delete and
        // tenant filters. This must be used with extreme care and never exposed to
        // untrusted/tenant-scoped code paths - this test exists to make that
        // capability visible and deliberate, not to endorse using it casually.
        var databaseName = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        using (var contextA = CreateContext(databaseName, tenantA))
        {
            contextA.TestEntities.Add(new TestTenantEntity { Name = "Tenant A Item" });
            contextA.SaveChanges();
        }

        using (var contextB = CreateContext(databaseName, tenantB))
        {
            contextB.TestEntities.Add(new TestTenantEntity { Name = "Tenant B Item" });
            contextB.SaveChanges();
        }

        using var queryContext = CreateContext(databaseName, tenantA);
        var allRows = queryContext.TestEntities.IgnoreQueryFilters().ToList();

        Assert.Equal(2, allRows.Count);
    }

    private static TestAppDbContext CreateContext(string databaseName, Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        ICurrentUserService? currentUserService = tenantId is null
            ? null
            : new FakeCurrentUserService(userId: "test-user", tenantId: tenantId);

        return new TestAppDbContext(options, currentUserService);
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public FakeCurrentUserService(string? userId, Guid? tenantId)
        {
            UserId = userId;
            TenantId = tenantId?.ToString();
        }

        public bool IsAuthenticated => UserId is not null;
        public string? UserId { get; }
        public string? Email => null;
        public string? TenantId { get; }
        public IReadOnlyCollection<string> Roles => [];
        public bool IsInRole(string role) => false;
    }
}
