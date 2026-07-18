using System.Reflection;
using APFlow.Application.Interfaces;
using APFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace APFlow.Infrastructure.Persistence;

/// <summary>
/// The solution's single EF Core database context. Contains no entity DbSets yet -
/// this work package (WP-003) establishes the persistence foundation (base entity
/// types, audit stamping, soft delete, migrations pipeline) only. Concrete DbSets are
/// added as their owning entities are implemented in future work packages.
/// Deliberately not sealed: APFlow.Infrastructure.Tests subclasses this with a
/// test-only DbSet to exercise the real OnModelCreating/SaveChanges conventions
/// without adding test-only entities to production code.
/// </summary>
public class AppDbContext : DbContext
{
    private static readonly MethodInfo ApplySoftDeleteFilterMethod =
        typeof(AppDbContext).GetMethod(nameof(ApplySoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly ICurrentUserService? _currentUserService;
    private readonly ILogger<AppDbContext>? _logger;

    /// <summary>
    /// Creates a new <see cref="AppDbContext"/>. <paramref name="currentUserService"/>
    /// and <paramref name="logger"/> are optional (default to null) specifically so
    /// this context can be constructed at EF Core design time (migrations tooling)
    /// without a full DI/HTTP host - see <see cref="AppDbContextDesignTimeFactory"/>.
    /// At runtime both are always supplied via DI.
    /// </summary>
    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService? currentUserService = null, ILogger<AppDbContext>? logger = null)
        : base(options)
    {
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            if (typeof(BaseEntity).IsAssignableFrom(clrType))
            {
                // Id is generated in C# (see BaseEntity.Id), not by the database.
                modelBuilder.Entity(clrType).Property(nameof(BaseEntity.Id)).ValueGeneratedNever();
            }

            if (typeof(AuditEntity).IsAssignableFrom(clrType))
            {
                // Soft-delete query filter: excludes IsDeleted rows from every normal
                // query automatically. This is a pure boolean-column comparison with
                // no external/per-instance state, so it is safe under EF Core's
                // per-DbContext-type model caching (unlike a tenant-based filter would
                // be - see docs/WP-003-Tenant-Isolation-Decision.md for why tenant-based
                // query filtering was deliberately NOT added here yet).
                ApplySoftDeleteFilterMethod.MakeGenericMethod(clrType).Invoke(this, [modelBuilder]);
            }
        }
    }

    private void ApplySoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : AuditEntity
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }

    /// <inheritdoc />
    public override int SaveChanges()
    {
        ApplyAuditAndSoftDeleteConventions();
        return base.SaveChanges();
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditAndSoftDeleteConventions();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Stamps audit fields (created/modified) and tenant id on save, and converts
    /// hard deletes of <see cref="AuditEntity"/>-derived entities into soft deletes.
    /// This is the single place these conventions are applied - do not duplicate this
    /// logic in application/feature code.
    /// </summary>
    private void ApplyAuditAndSoftDeleteConventions()
    {
        var now = DateTimeOffset.UtcNow;
        var currentUser = _currentUserService?.UserId ?? "system";

        foreach (var entry in ChangeTracker.Entries<AuditEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = now;
                    entry.Entity.CreatedBy = currentUser;

                    if (entry.Entity is TenantEntity { TenantId: var tenantId } tenantEntity && tenantId == Guid.Empty)
                    {
                        if (Guid.TryParse(_currentUserService?.TenantId, out var currentTenantId))
                        {
                            tenantEntity.TenantId = currentTenantId;
                        }
                        else
                        {
                            // No resolvable tenant (e.g. a background job with no HTTP
                            // context). WP-003 deliberately does not reject this outright
                            // - "no business logic yet" - but it must not fail silently
                            // in production. Logged at Warning so this is visible before
                            // any real TenantEntity-derived data exists; whether to
                            // reject outright is a decision for whoever adds the first
                            // real tenant-scoped entity, once Workers' actual usage
                            // patterns are known.
                            _logger?.LogWarning(
                                "Entity {EntityType} ({EntityId}) was saved with no resolvable tenant - TenantId left as Guid.Empty.",
                                tenantEntity.GetType().Name,
                                tenantEntity.Id);
                        }
                    }

                    break;

                case EntityState.Modified:
                    entry.Entity.ModifiedAtUtc = now;
                    entry.Entity.ModifiedBy = currentUser;
                    break;

                case EntityState.Deleted:
                    // Convert the hard delete into a soft delete.
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAtUtc = now;
                    entry.Entity.DeletedBy = currentUser;
                    break;
            }
        }
    }
}
