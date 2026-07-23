using APFlow.Application.DTOs;
using APFlow.Domain.Common;

namespace APFlow.Application.Interfaces;

/// <summary>
/// Read access to the current tenant's active workflow (WP-050) - which statuses
/// are valid for a given domain, e.g. for a future review/approval UI (WP-018,
/// WP-019, WP-030) to render the correct set of statuses for whichever tenant is
/// logged in, without hardcoding GB Skips' extra statuses or the platform default
/// into UI code.
/// </summary>
public interface IWorkflowQueryService
{
    /// <summary>Returns the active template (tenant-specific if one exists, otherwise platform-default) for the given domain.</summary>
    Task<Result<WorkflowTemplateDto>> GetActiveTemplateAsync(string domainName, CancellationToken cancellationToken = default);
}
