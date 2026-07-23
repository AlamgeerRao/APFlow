namespace APFlow.Domain.Common;

/// <summary>
/// Marks an entity whose tenant scoping is OPTIONAL rather than mandatory: a null
/// <see cref="TenantId"/> means "visible to every tenant" (a platform-wide
/// default), while a non-null value scopes the row to exactly one tenant, the same
/// as <see cref="Entities.TenantEntity.TenantId"/>. This is a genuinely different
/// shape from <see cref="Entities.TenantEntity"/> (which has no "global" concept -
/// every row belongs to exactly one tenant, and <c>AppDbContext</c>'s tenant filter
/// fails closed rather than falling back to some shared default) - WP-050
/// introduces this because a <c>WorkflowTemplate</c> genuinely needs both shapes at
/// once: one platform-default template visible to every tenant, plus zero or more
/// tenant-specific overrides. See <c>AppDbContext</c>'s
/// <c>ApplyOptionalTenantAndSoftDeleteFilter</c> for the corresponding query filter
/// (<c>TenantId == null || TenantId == currentTenantId</c>), and
/// docs/WP-050-Workflow-Engine-Decisions.md for the full reasoning.
/// Entities implementing this still derive from <see cref="Entities.AuditEntity"/>
/// (for created/modified/soft-delete), not <see cref="Entities.TenantEntity"/> -
/// the two are mutually exclusive tenant-scoping strategies for a given entity.
/// </summary>
public interface IOptionallyTenantScoped
{
    /// <summary>The owning tenant, or null if this row applies platform-wide.</summary>
    Guid? TenantId { get; set; }
}
