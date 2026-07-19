using APFlow.Application.Interfaces;
using APFlow.Domain.Entities;
using APFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace APFlow.Infrastructure.Tests.Persistence;

/// <summary>
/// Test-only concrete entity so <see cref="TenantEntity"/>'s behavior (via
/// <see cref="AppDbContext"/>'s audit-stamping, soft-delete, and tenant-filtering
/// conventions) can be exercised in isolation from the real Invoice/Supplier/
/// InvoiceNote entities added in WP-009 - useful for testing base AppDbContext
/// mechanics without real-entity complexity (navigation properties, EF
/// configurations) getting in the way.
/// </summary>
internal sealed class TestTenantEntity : TenantEntity
{
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Test-only subclass adding a single test DbSet. Deliberately does NOT override
/// OnModelCreating/SaveChanges - it inherits AppDbContext's real implementations
/// unchanged, so these tests exercise the exact production audit-stamping,
/// soft-delete, and query-filter logic. EF Core discovers <see cref="TestEntities"/>
/// via the normal DbSet-property convention regardless of which class's
/// OnModelCreating body runs.
/// </summary>
internal sealed class TestAppDbContext : AppDbContext
{
    public TestAppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService? currentUserService, ILogger<AppDbContext>? logger = null)
        : base(options, currentUserService, logger)
    {
    }

    public DbSet<TestTenantEntity> TestEntities => Set<TestTenantEntity>();
}
