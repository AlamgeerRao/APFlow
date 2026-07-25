namespace APFlow.Application.DTOs;

/// <summary>Read shape for a single valid status within a workflow template (WP-050).</summary>
public sealed record WorkflowStatusDto(string Code, string Name, bool IsTerminal, int SortOrder);

/// <summary>
/// Read shape for a single allowed FromStatus -&gt; ToStatus edge within a workflow
/// template (WP-050's <c>WorkflowTransition</c>, exposed here starting WP-054 so
/// <see cref="APFlow.Application.Interfaces.IInvoiceWorkflowActionsService"/> can
/// enumerate them without reaching into Infrastructure/Domain repository types
/// directly).
/// </summary>
public sealed record WorkflowTransitionDto(string FromStatusCode, string ToStatusCode);

/// <summary>
/// Read shape for the active workflow template for a domain/tenant (WP-050) -
/// either the platform default or a tenant-specific override, never both at once.
/// </summary>
public sealed record WorkflowTemplateDto(
    Guid Id,
    string DomainName,
    string Name,
    bool IsTenantSpecific,
    IReadOnlyList<WorkflowStatusDto> Statuses,
    IReadOnlyList<WorkflowTransitionDto> Transitions);
