using APFlow.Domain.Common;

namespace APFlow.Domain.Entities;

/// <summary>
/// A role-gating rule for an approval-type action within one domain (SA-007 E-09) -
/// e.g. "who may approve an Invoice" or "who may create a Payment Batch". WP-051:
/// this entity did NOT exist before this work package - no "Payment Batch Approval"
/// feature or ApprovalPolicy mechanism existed anywhere in this codebase prior to
/// WP-051, despite the work package's own framing describing this as "extending"
/// an existing mechanism. Built here generically (domain-parameterized) so it can
/// plausibly serve a future Payment Batch domain per that framing, without
/// inventing actual Payment Batch entities/features that don't exist yet - see
/// docs/WP-051-Approval-Policy-Decisions.md.
/// Uses the same optional-tenant scoping as <see cref="WorkflowTemplate"/> (WP-050)
/// - a policy may be platform-wide (<see cref="TenantId"/> null) or tenant-specific
/// - reusing <see cref="IOptionallyTenantScoped"/>'s existing query-filter
/// mechanism rather than inventing a second one.
/// </summary>
public sealed class ApprovalPolicy : AuditEntity, IOptionallyTenantScoped
{
    /// <inheritdoc />
    public Guid? TenantId { get; set; }

    /// <summary>
    /// The domain this policy governs, e.g. "InvoiceApproval". See
    /// <see cref="APFlow.Domain.Common.Constants.ApprovalDomains"/> for named
    /// constants.
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// The role (see <see cref="APFlow.Domain.Common.Constants.Roles"/>) an acting
    /// user must hold to perform the action this policy governs.
    /// </summary>
    public string RequiredRole { get; set; } = string.Empty;

    /// <summary>
    /// Whether this action requires a second, distinct approver (dual control).
    /// WP-051 seeds this as false for GB Skips (single approver sufficient per the
    /// Requirements Addendum) - dual-control/multi-approver LOGIC is explicit
    /// WP-051 out-of-scope; this flag exists on the schema for a future work
    /// package to act on, not consumed by any enforcement code yet.
    /// </summary>
    public bool RequiresDualControl { get; set; }
}
