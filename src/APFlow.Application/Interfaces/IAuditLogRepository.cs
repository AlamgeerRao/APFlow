using APFlow.Application.DTOs;
using APFlow.Domain.Entities;

namespace APFlow.Application.Interfaces;

/// <summary>
/// Data access abstraction for <see cref="AuditLog"/>. Same design as
/// <see cref="IInvoiceRepository"/>/<see cref="ISupplierRepository"/> - plain Domain
/// types only, tenant isolation enforced by the underlying EF Core query filter, not
/// by this interface. Deliberately exposes no <c>Update</c>/<c>Remove</c> - see
/// <see cref="AuditLog"/>'s doc comment for why an audit trail has no mutation
/// surface anywhere in this codebase.
/// </summary>
public interface IAuditLogRepository
{
    /// <summary>Returns the audit log entry with the given id, or null if not found.</summary>
    Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a filtered, sorted, paged slice of audit log entries visible to the
    /// current tenant, together with the total count of rows matching the filter
    /// (not just the page) - same shape and reasoning as WP-011's
    /// <c>IInvoiceRepository.QueryAsync</c>. Filtering, sorting, and paging are
    /// performed by the underlying data store, not in application memory.
    /// </summary>
    Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> QueryAsync(
        AuditLogQueryParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>Begins tracking a new audit log entry. Does not persist until <see cref="SaveChangesAsync"/> is called.</summary>
    Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists all pending changes made via this repository. See
    /// <see cref="IAuditService.LogAsync"/>'s doc comment: WP-013's own caller
    /// deliberately does NOT call this directly - it commits the staged entry via
    /// whatever other repository's <c>SaveChangesAsync</c> is already saving the
    /// change the entry describes, so both commit atomically together. Exposed here
    /// for interface symmetry with <see cref="IInvoiceRepository"/>/<see cref="ISupplierRepository"/>
    /// and for any future caller that genuinely needs a standalone audit-only write.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
