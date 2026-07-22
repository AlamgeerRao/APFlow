using APFlow.Application.DTOs;
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

    /// <summary>
    /// Returns a filtered, sorted, paged slice of invoices visible to the current
    /// tenant, together with the total count of rows matching the filter (not just
    /// the page). Filtering, sorting, and paging are performed by the underlying
    /// data store - unlike <see cref="GetAllAsync"/>, this method is safe to call
    /// against a large invoice table without loading every row into memory.
    /// Callers are expected to have already validated <paramref name="parameters"/>
    /// (see <c>IInvoiceQueryService</c>); this method does not return validation
    /// errors, only results.
    /// </summary>
    Task<(IReadOnlyList<Invoice> Items, int TotalCount)> QueryAsync(
        InvoiceQueryParameters parameters,
        CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Fetches the invoice with the given id, sets its
    /// <see cref="Invoice.IsPotentialDuplicate"/>/<see cref="Invoice.DuplicateCheckReason"/>
    /// fields, and persists the change immediately - this method calls
    /// <see cref="SaveChangesAsync"/> itself, unlike every other mutating method on
    /// this interface (<see cref="AddAsync"/>/<see cref="Update"/>/<see cref="Remove"/>
    /// all stage only). See docs/WP-048-Persist-Duplicate-Detection-Result.md for
    /// why: unlike those methods, which participate in whatever save the caller was
    /// already about to make as part of the same unit of work, a duplicate-check
    /// result is always computed and persisted as its own, separate step, after the
    /// invoice it describes has already been created/updated and committed by an
    /// earlier, unrelated call - there is nothing in-flight to batch it with.
    /// Returns false (and persists nothing) if no invoice with the given id exists.
    /// </summary>
    Task<bool> PersistDuplicateCheckResultAsync(
        Guid invoiceId,
        bool isPotentialDuplicate,
        string? duplicateCheckReason,
        CancellationToken cancellationToken = default);
}
