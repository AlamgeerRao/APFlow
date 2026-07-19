using APFlow.Domain.Entities;

namespace APFlow.Application.Interfaces;

/// <summary>
/// Data access abstraction for <see cref="Supplier"/>. Same design as
/// <see cref="IInvoiceRepository"/> - plain Domain types only, tenant isolation
/// enforced by the underlying EF Core query filter, not by this interface.
/// </summary>
public interface ISupplierRepository
{
    /// <summary>Returns the supplier with the given id, or null if not found.</summary>
    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns every supplier visible to the current tenant.</summary>
    Task<IReadOnlyList<Supplier>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Begins tracking a new supplier. Does not persist until <see cref="SaveChangesAsync"/> is called.</summary>
    Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default);

    /// <summary>Marks a tracked supplier as modified. Does not persist until <see cref="SaveChangesAsync"/> is called.</summary>
    void Update(Supplier supplier);

    /// <summary>Marks a supplier for deletion (converted to a soft delete). Does not persist until <see cref="SaveChangesAsync"/> is called.</summary>
    void Remove(Supplier supplier);

    /// <summary>Persists all pending changes made via this repository.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
