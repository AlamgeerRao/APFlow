namespace APFlow.Application.DTOs;

/// <summary>Read shape for a single valid status within a workflow template (WP-050).</summary>
public sealed record WorkflowStatusDto(string Code, string Name, bool IsTerminal, int SortOrder);

/// <summary>
/// Read shape for the active workflow template for a domain/tenant (WP-050) -
/// either the platform default or a tenant-specific override, never both at once.
/// </summary>
public sealed record WorkflowTemplateDto(
    Guid Id,
    string DomainName,
    string Name,
    bool IsTenantSpecific,
    IReadOnlyList<WorkflowStatusDto> Statuses);
