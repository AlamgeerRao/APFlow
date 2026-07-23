using APFlow.Application.Interfaces;
using APFlow.Domain.Entities;

namespace APFlow.Application.Tests.Features;

/// <summary>Hand-written fake, same pattern as every repository fake elsewhere in this codebase.</summary>
internal sealed class FakeApprovalPolicyRepository : IApprovalPolicyRepository
{
    public List<ApprovalPolicy> Policies { get; } = [];

    /// <summary>
    /// Simulates the current caller's tenant, mirroring what AppDbContext's real
    /// optional-tenant query filter would already have restricted results to - see
    /// FakeWorkflowTemplateRepository's identical property for the full reasoning.
    /// </summary>
    public Guid? CurrentTenantId { get; set; }

    public Task<ApprovalPolicy?> GetActivePolicyAsync(string domain, CancellationToken cancellationToken = default)
    {
        var candidates = Policies
            .Where(p => p.Domain == domain && (p.TenantId is null || p.TenantId == CurrentTenantId))
            .ToList();
        var result = candidates.FirstOrDefault(p => p.TenantId is not null) ?? candidates.FirstOrDefault(p => p.TenantId is null);
        return Task.FromResult(result);
    }
}
