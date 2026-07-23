using APFlow.Domain.Entities;

namespace APFlow.Application.Interfaces;

/// <summary>
/// Data access abstraction for <see cref="WorkflowTemplate"/> (WP-050). Plain
/// Domain types only, same design as <see cref="IInvoiceRepository"/>/
/// <see cref="ISupplierRepository"/> - tenant resolution (platform-default vs
/// tenant-specific) is performed here, not left to callers.
/// </summary>
public interface IWorkflowTemplateRepository
{
    /// <summary>
    /// Returns the ACTIVE template for the given domain (e.g. "Invoice") and the
    /// current tenant: that tenant's own template if one exists, otherwise the
    /// platform-default template (<c>TenantId == null</c>). Returns null only if
    /// neither exists (a configuration gap - every domain WP-050 seeds always has
    /// at least a platform-default template). Includes <see cref="WorkflowTemplate.Statuses"/>
    /// and <see cref="WorkflowTemplate.Transitions"/> already loaded.
    /// </summary>
    Task<WorkflowTemplate?> GetActiveTemplateAsync(string domainName, CancellationToken cancellationToken = default);
}
