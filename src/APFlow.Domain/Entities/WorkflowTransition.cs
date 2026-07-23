using APFlow.Domain.Common;

namespace APFlow.Domain.Entities;

/// <summary>
/// A single allowed FromStatus -> ToStatus edge within a
/// <see cref="WorkflowTemplate"/>. WP-050 defines this entity/table but
/// deliberately seeds NO rows into either the platform-default or GB Skips
/// template - see docs/WP-050-Workflow-Engine-Decisions.md for why (task 4's own
/// wording explicitly prohibits finalising GB Skips' proposed set without Chief
/// Technical Architect sign-off, and the platform-default transition graph turns
/// out not to be documented anywhere in this project's reference material either -
/// only its status LIST is confirmed, not its edges). Deliberately does not carry
/// a "required role" field - role-gating the APPROVED transition is explicit
/// WP-051 scope, not WP-050's; adding that column here now would be inventing a
/// detail WP-051 hasn't specified yet.
/// </summary>
public sealed class WorkflowTransition : AuditEntity, IOptionallyTenantScoped
{
    /// <inheritdoc />
    public Guid? TenantId { get; set; }

    /// <summary>The owning template.</summary>
    public Guid WorkflowTemplateId { get; set; }

    /// <summary>Navigation property to the owning template.</summary>
    public WorkflowTemplate? WorkflowTemplate { get; set; }

    /// <summary>The status code this transition starts from. Must match a <see cref="StatusReference.Code"/> within the same template.</summary>
    public string FromStatusCode { get; set; } = string.Empty;

    /// <summary>The status code this transition ends at. Must match a <see cref="StatusReference.Code"/> within the same template.</summary>
    public string ToStatusCode { get; set; } = string.Empty;
}
