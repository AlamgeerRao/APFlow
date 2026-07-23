using APFlow.Application.Interfaces;
using APFlow.Domain.Entities;

namespace APFlow.Application.Tests.Features.Workflow;

/// <summary>Hand-written fake, same pattern as every repository fake elsewhere in this codebase.</summary>
internal sealed class FakeWorkflowTemplateRepository : IWorkflowTemplateRepository
{
    public List<WorkflowTemplate> Templates { get; } = [];

    /// <summary>
    /// Simulates the current caller's tenant, mirroring what AppDbContext's real
    /// optional-tenant query filter would already have restricted results to
    /// before GetActiveTemplateAsync's own "prefer tenant-specific" logic runs -
    /// without this, a fake containing both a platform-default AND some OTHER
    /// tenant's template could incorrectly resolve to that other tenant's template
    /// regardless of which tenant is actually meant to be "current" in a given test.
    /// </summary>
    public Guid? CurrentTenantId { get; set; }

    public Task<WorkflowTemplate?> GetActiveTemplateAsync(string domainName, CancellationToken cancellationToken = default)
    {
        var candidates = Templates
            .Where(t => t.DomainName == domainName && (t.TenantId is null || t.TenantId == CurrentTenantId))
            .ToList();
        var result = candidates.FirstOrDefault(t => t.TenantId is not null) ?? candidates.FirstOrDefault(t => t.TenantId is null);
        return Task.FromResult(result);
    }
}
