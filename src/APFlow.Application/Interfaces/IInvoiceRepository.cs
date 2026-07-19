using APFlow.Domain.Entities;

namespace APFlow.Application.Interfaces;

/// <summary>
/// Data access abstraction for <see cref="Invoice"/>. Deliberately uses only plain
/// Domain types (no <c>IQueryable</c>, no EF Core types) in its signature so
/// consumers (e.g. <c>IInvoiceService</c>) can be unit-tested against a fake
/// implementation without needing a real database or EF Core provider.
/// Tenant isolation on every read is enforced by the underlying EF Core query filter
/// (see AppDbContext), not by this interface - callers do not need to (and must not
/// try to) pass a tenant id to these methods.
/// </summary>
public interface IInvoiceRepository
{
    /// <summary>Returns the invoice with the given id, or null if not found (or not visible to the current tenant).</summary>
    Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns the invoice with the given id including its <see cref="Invoice.Notes"/>, or null if not found.</summary>
    Task<Invoice?> GetByIdWithNotesAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns every invoice visible to the current tenant.</summary>
    Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Begins tracking a new invoice. Does not persist until <see cref="SaveChangesAsync"/> is called.</summary>
    Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default);

    /// <summary>Marks a tracked invoice as modified. Does not persist until <see cref="SaveChangesAsync"/> is called.</summary>
    void Update(Invoice invoice);

    /// <summary>
    /// Marks an invoice for deletion (converted to a soft delete by AppDbContext -
    /// see AuditEntity.IsDeleted). Does not persist until <see cref="SaveChangesAsync"/> is called.
    /// </summary>
    void Remove(Invoice invoice);

    /// <summary>Persists all pending changes made via this repository.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
