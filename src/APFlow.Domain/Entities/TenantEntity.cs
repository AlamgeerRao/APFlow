namespace APFlow.Domain.Entities;

/// <summary>
/// Base type for entities belonging to a specific tenant, per the "multi-tenant by
/// design, with clear tenant data isolation" architecture principle. Almost every
/// business entity in AP Flow (invoices, vendors, approvals, etc.) will derive from
/// this rather than <see cref="AuditEntity"/> directly.
/// <see cref="TenantId"/> is populated automatically by <c>AppDbContext.SaveChanges</c>
/// from the current authenticated user's tenant if not already set - do not rely on
/// manually setting it in application code.
/// IMPORTANT (WP-003 scope note): this entity carries the tenant identifier so it CAN
/// be persisted and stamped correctly on write. It does NOT yet enforce tenant
/// isolation on read (no automatic query filter limiting queries to the current
/// tenant) - that is a separate, deliberately deferred decision, tracked as a hard
/// gate in docs/WP-003-Tenant-Isolation-Decision.md. That checklist must be resolved
/// before any entity derives from this type and is queried in a real feature.
/// </summary>
public abstract class TenantEntity : AuditEntity
{
    /// <summary>The tenant this row belongs to.</summary>
    public Guid TenantId { get; set; }
}
