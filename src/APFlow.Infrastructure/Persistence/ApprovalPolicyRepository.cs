using APFlow.Application.Interfaces;
using APFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace APFlow.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IApprovalPolicyRepository"/>. Same
/// "prefer tenant-specific over platform-default" resolution as
/// <see cref="WorkflowTemplateRepository"/> - tenant visibility comes from
/// AppDbContext's optional-tenant query filter.
/// </summary>
public sealed class ApprovalPolicyRepository : IApprovalPolicyRepository
{
    private readonly AppDbContext _context;

    /// <summary>Creates the repository over the given <see cref="AppDbContext"/>.</summary>
    public ApprovalPolicyRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<ApprovalPolicy?> GetActivePolicyAsync(string domain, CancellationToken cancellationToken = default)
    {
        var policies = await _context.ApprovalPolicies
            .Where(p => p.Domain == domain)
            .ToListAsync(cancellationToken);

        return policies.FirstOrDefault(p => p.TenantId is not null) ?? policies.FirstOrDefault(p => p.TenantId is null);
    }
}
