using APFlow.Domain.Common.Constants;

namespace APFlow.Domain.Entities;

/// <summary>
/// An immutable record of a single tracked action against another entity - "who did
/// what, to which record, and when" (WP-013). Derives from <see cref="TenantEntity"/>
/// like almost every other business entity (audit data needs the same tenant
/// isolation as everything else - Architecture Principles §6), which also means it
/// already provides two of WP-013's six required fields for free, rather than
/// duplicating them:
/// <list type="bullet">
///   <item><description><b>User</b> - <see cref="AuditEntity.CreatedBy"/>. Populated
///   automatically by <c>AppDbContext.SaveChanges</c> from the current
///   <c>ICurrentUserService.UserId</c>, falling back to the literal string
///   <c>"system"</c> for callers with no authenticated context (e.g. a background
///   pipeline run) - this is an existing, established convention (see
///   <c>AppDbContext.ApplyAuditAndSoftDeleteConventions</c>), not a new one invented
///   for this entity.</description></item>
///   <item><description><b>Date/Time</b> - <see cref="AuditEntity.CreatedAtUtc"/>,
///   populated the same way.</description></item>
/// </list>
/// The remaining four required fields (Action, Entity, Entity Id, Previous/New
/// Value) have no existing equivalent and are added below. See
/// docs/WP-013-Audit-Logging-Decisions.md for the reasoning behind this design,
/// including why no attempt is made to have <c>IAuditService</c> (APFlow.Application
/// - not referenced here, Domain has no dependency on Application per Solution
/// Structure §2) persist a row itself (it stages one; the caller describing the
/// underlying change commits both together).
/// No update/delete surface is exposed anywhere in this codebase for this entity -
/// an audit trail that could be edited or removed through the application would
/// defeat its own purpose. <see cref="AuditEntity"/>'s inherited soft-delete fields
/// remain present (for type-hierarchy consistency, per Solution Structure) but are
/// never expected to be set for a row of this type.
/// </summary>
public sealed class AuditLog : TenantEntity
{
    /// <summary>
    /// What happened, e.g. <see cref="AuditActions.InvoiceStatusChanged"/>.
    /// A plain string, not a closed enum: this entity is deliberately generic
    /// (usable for any future entity/action, not only invoices), and a fixed enum
    /// would need editing for every new kind of audited action across the whole
    /// application. <see cref="AuditActions"/> supplies named constants for known
    /// values so callers aren't writing raw string literals.
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// The name of the entity type this action was performed against, e.g.
    /// <c>"Invoice"</c>. Free string, not a foreign key or closed type reference -
    /// this table is not scoped to invoices specifically, even though WP-013's only
    /// concrete producer is invoice status changes.
    /// </summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>The affected entity's id. Not a foreign key - the referenced row may later be modified or (in principle) soft-deleted independently of this audit trail.</summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// A plain-text representation of the value before this action, or null if not
    /// applicable (e.g. there is no "previous" state for a creation event). No
    /// structured/JSON diff format is imposed - a caller decides how to represent
    /// the value it is describing (WP-013's own caller uses
    /// the status code string directly).
    /// </summary>
    public string? PreviousValue { get; set; }

    /// <summary>A plain-text representation of the value after this action, or null if not applicable. See <see cref="PreviousValue"/>.</summary>
    public string? NewValue { get; set; }
}
