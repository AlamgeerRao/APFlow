using System.Reflection;
using APFlow.Application.Interfaces;
using APFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace APFlow.Infrastructure.Persistence;

/// <summary>
/// The solution's single EF Core database context. WP-009 adds the first concrete
/// entities (Invoice, Supplier, InvoiceNote) and, with them, resolves the
/// tenant-isolation-on-read gap tracked in docs/WP-003-Tenant-Isolation-Decision.md -
/// see that document (now marked Resolved) for the full history of why this wasn't
/// done earlier and what the risk was.
/// Deliberately not sealed: APFlow.Infrastructure.Tests subclasses this with a
/// test-only DbSet to exercise the real OnModelCreating/SaveChanges conventions
/// without adding test-only entities to production code.
/// </summary>
public class AppDbContext : DbContext
{
    private static readonly MethodInfo ApplySoftDeleteFilterMethod =
        typeof(AppDbContext).GetMethod(nameof(ApplySoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly MethodInfo ApplyTenantAndSoftDeleteFilterMethod =
        typeof(AppDbContext).GetMethod(nameof(ApplyTenantAndSoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly ICurrentUserService? _currentUserService;
    private readonly ILogger<AppDbContext>? _logger;

    /// <summary>
    /// The current request's tenant id, resolved ONCE per DbContext instance (in the
    /// constructor) and referenced directly (not copied into a local variable) by the
    /// query filter lambda in <see cref="ApplyTenantAndSoftDeleteFilter{TEntity}"/>.
    /// THIS FIELD IS THE ENTIRE FIX for the tenant-isolation gap: EF Core caches the
    /// compiled model once per DbContext TYPE, not per instance. A filter lambda that
    /// captured a local `tenantId` variable would bake in whichever tenant happened
    /// to build the model first, for the lifetime of the process - silently leaking
    /// data across every tenant after that. Referencing this instance field directly
    /// makes EF Core re-parameterize the filter per DbContext instance instead.
    /// VERIFIED via web search against Microsoft's own EF Core documentation
    /// (learn.microsoft.com/en-us/ef/core/querying/filters), which
    /// states this exact pattern explicitly: "Model-level filters will use the value
    /// from the correct context instance (that is, the instance that is executing
    /// the query)." That is the documented, intended behavior this field relies on.
    /// A contradictory-looking third-party source was also found (a blog describing
    /// a custom IModelCacheKeyFactory as necessary to avoid stale filters) - on
    /// inspection, that source's scenario is different in kind: it conditionally
    /// adds/omits the filter itself based on runtime state (super-admin users get no
    /// filter at all), which genuinely changes the MODEL'S STRUCTURE per scenario and
    /// does need special handling, since OnModelCreating only runs once. This
    /// codebase's filter is always present, with only its VALUE varying per
    /// instance - the case Microsoft's docs describe as safe. Not conditionally
    /// adding/removing the filter itself is a real constraint worth remembering if
    /// this is ever extended (e.g. a future "super-admin sees all tenants" feature
    /// should NOT be implemented by conditionally omitting this HasQueryFilter call).
    /// STILL NOT EXECUTED against a real EF Core provider in this sandbox (no NuGet
    /// access, ever, in this entire project) - AppDbContextTenantIsolationTests is
    /// written and, based on the above, expected to pass, but "expected based on
    /// documented behavior" is not the same as "observed to pass". Treat this as the
    /// single highest-priority test to actually run in your environment.
    /// </summary>
    private readonly Guid? _currentTenantId;

    /// <summary>Invoices. Tenant-isolated - see <see cref="_currentTenantId"/>.</summary>
    public DbSet<Invoice> Invoices => Set<Invoice>();

    /// <summary>Suppliers. Tenant-isolated - see <see cref="_currentTenantId"/>.</summary>
    public DbSet<Supplier> Suppliers => Set<Supplier>();

    /// <summary>Invoice notes. Tenant-isolated - see <see cref="_currentTenantId"/>.</summary>
    public DbSet<InvoiceNote> InvoiceNotes => Set<InvoiceNote>();

    /// <summary>
    /// Audit trail entries (WP-013). Tenant-isolated - see <see cref="_currentTenantId"/>.
    /// No corresponding <c>Update</c>/<c>Remove</c> anywhere in this codebase - see
    /// <see cref="AuditLog"/>'s doc comment.
    /// </summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

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
        _currentTenantId = Guid.TryParse(currentUserService?.TenantId, out var tenantId) ? tenantId : null;
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            if (typeof(BaseEntity).IsAssignableFrom(clrType))
            {
                // Id is generated in C# (see BaseEntity.Id), not by the database.
                modelBuilder.Entity(clrType).Property(nameof(BaseEntity.Id)).ValueGeneratedNever();
            }

            // TenantEntity also derives from AuditEntity, so this must be an
            // if/else-if: TenantEntity-derived types get ONE combined filter
            // (tenant + soft-delete) - EF Core allows only one HasQueryFilter per
            // entity type, and a second call would overwrite the first, not combine.
            if (typeof(TenantEntity).IsAssignableFrom(clrType))
            {
                ApplyTenantAndSoftDeleteFilterMethod.MakeGenericMethod(clrType).Invoke(this, [modelBuilder]);
            }
            else if (typeof(AuditEntity).IsAssignableFrom(clrType))
            {
                ApplySoftDeleteFilterMethod.MakeGenericMethod(clrType).Invoke(this, [modelBuilder]);
            }
        }
    }

    private void ApplySoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : AuditEntity
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }

    /// <summary>
    /// Combined tenant + soft-delete filter. A caller with no resolvable tenant
    /// (<see cref="_currentTenantId"/> is null) sees zero rows of any
    /// TenantEntity-derived type - fail-closed, not fail-open. See
    /// <see cref="_currentTenantId"/>'s doc comment for why this references the
    /// instance field directly rather than a captured local variable.
    /// </summary>
    private void ApplyTenantAndSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : TenantEntity
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == _currentTenantId);
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
                            // context). Not rejected outright - logged at Warning so
                            // it's visible rather than silent. With the tenant query
                            // filter now in place (WP-009), an entity saved this way
                            // is also now UNREACHABLE via any normal query (the filter
                            // is e.TenantId == _currentTenantId, and Guid.Empty will
                            // never match a real resolved tenant) - effectively an
                            // orphaned row, not just an audit gap. Still not rejected
                            // outright here; whether to reject at save time is a
                            // decision for whoever owns Workers' actual usage patterns.
                            _logger?.LogWarning(
                                "Entity {EntityType} ({EntityId}) was saved with no resolvable tenant - TenantId left as Guid.Empty and the row will be unreachable via any tenant-scoped query.",
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
