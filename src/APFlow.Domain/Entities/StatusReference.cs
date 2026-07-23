using APFlow.Domain.Common;

namespace APFlow.Domain.Entities;

/// <summary>
/// A single valid status within a <see cref="WorkflowTemplate"/> (SA-007 E-14).
/// <see cref="TenantId"/> is denormalized from the owning template (not
/// independently meaningful) purely so this entity can be queried and
/// tenant-filtered directly without always joining through
/// <see cref="WorkflowTemplate"/> first - see
/// <see cref="IOptionallyTenantScoped"/>'s doc comment.
/// </summary>
public sealed class StatusReference : AuditEntity, IOptionallyTenantScoped
{
    /// <inheritdoc />
    public Guid? TenantId { get; set; }

    /// <summary>The owning template.</summary>
    public Guid WorkflowTemplateId { get; set; }

    /// <summary>Navigation property to the owning template.</summary>
    public WorkflowTemplate? WorkflowTemplate { get; set; }

    /// <summary>
    /// The machine-readable status code, e.g. "RECEIVED" or (GB Skips only)
    /// "CHECKED_READY_TO_APPROVE". See
    /// <see cref="APFlow.Domain.Common.Constants.InvoiceStatusCodes"/> for named
    /// constants covering the known codes.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>The human-readable name, e.g. "Received" or "Checked &amp; Ready to Approve".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether an invoice in this status can never leave it (e.g. "Archived").</summary>
    public bool IsTerminal { get; set; }

    /// <summary>
    /// Relative display/lifecycle ordering within the template - lets a
    /// tenant-specific addition (e.g. GB Skips' two new statuses) be positioned
    /// between two platform-default statuses (task 3: "positioned between
    /// AWAITING_REVIEW and APPROVED") without requiring the platform-default rows
    /// to be renumbered. Not a strict transition graph - see
    /// <see cref="WorkflowTransition"/> for that.
    /// </summary>
    public int SortOrder { get; set; }
}
