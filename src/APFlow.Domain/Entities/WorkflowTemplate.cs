using APFlow.Domain.Common;

namespace APFlow.Domain.Entities;

/// <summary>
/// A named set of valid statuses and allowed transitions for one business domain
/// (currently only <c>"Invoice"</c> - see <see cref="DomainName"/>), realising
/// SA-004 LM-19's "Future Expansion" (WP-050): platform-default and tenant-specific
/// workflows are DATA (rows in this table and its children), not hardcoded
/// application logic. Every tenant gets the platform-default template
/// (<see cref="TenantId"/> null) unless they have their own
/// (<see cref="TenantId"/> set to that tenant) - see
/// <see cref="IOptionallyTenantScoped"/>'s doc comment for the query-filter
/// mechanics, and <c>IWorkflowTemplateRepository.GetActiveTemplateAsync</c> for the
/// "tenant-specific overrides platform-default" resolution logic.
/// </summary>
public sealed class WorkflowTemplate : AuditEntity, IOptionallyTenantScoped
{
    /// <inheritdoc />
    public Guid? TenantId { get; set; }

    /// <summary>The business domain this template applies to, e.g. "Invoice". See <see cref="APFlow.Domain.Common.Constants.WorkflowDomains"/>.</summary>
    public string DomainName { get; set; } = string.Empty;

    /// <summary>A human-readable name, e.g. "Platform Default" or "GB Skips Invoice Workflow".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The valid statuses for this template.</summary>
    public ICollection<StatusReference> Statuses { get; set; } = new List<StatusReference>();

    /// <summary>
    /// The allowed transitions for this template. Deliberately empty for both the
    /// platform-default and GB Skips templates as delivered by WP-050 - see
    /// docs/WP-050-Workflow-Engine-Decisions.md: neither the platform-default
    /// transition graph nor GB Skips' proposed additions have been confirmed by
    /// the Chief Technical Architect, and WP-050's own task list explicitly
    /// prohibits finalising an unconfirmed transition set.
    /// </summary>
    public ICollection<WorkflowTransition> Transitions { get; set; } = new List<WorkflowTransition>();
}
