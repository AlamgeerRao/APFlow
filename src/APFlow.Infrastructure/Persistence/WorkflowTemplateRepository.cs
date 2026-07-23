using APFlow.Application.Interfaces;
using APFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace APFlow.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IWorkflowTemplateRepository"/>. Tenant
/// visibility comes from AppDbContext's optional-tenant query filter (see
/// <see cref="APFlow.Domain.Common.IOptionallyTenantScoped"/>) - this class only
/// decides which of the (at most two) visible rows for a domain to prefer.
/// </summary>
public sealed class WorkflowTemplateRepository : IWorkflowTemplateRepository
{
    private readonly AppDbContext _context;

    /// <summary>Creates the repository over the given <see cref="AppDbContext"/>.</summary>
    public WorkflowTemplateRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<WorkflowTemplate?> GetActiveTemplateAsync(string domainName, CancellationToken cancellationToken = default)
    {
        // The query filter already restricts results to the platform-default
        // template (TenantId == null) plus the current tenant's own, if any - at
        // most two rows, enforced unique per (DomainName, TenantId) by
        // WorkflowTemplateConfiguration. Prefer the tenant-specific one.
        var templates = await _context.WorkflowTemplates
            .Include(t => t.Statuses)
            .Include(t => t.Transitions)
            .Where(t => t.DomainName == domainName)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return templates.FirstOrDefault(t => t.TenantId is not null) ?? templates.FirstOrDefault(t => t.TenantId is null);
    }
}
