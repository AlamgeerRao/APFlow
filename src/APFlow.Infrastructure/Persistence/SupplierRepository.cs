using APFlow.Application.Interfaces;
using APFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace APFlow.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="ISupplierRepository"/>. Tenant isolation comes from AppDbContext's query filter.</summary>
public sealed class SupplierRepository : ISupplierRepository
{
    private readonly AppDbContext _context;

    /// <summary>Creates the repository over the given <see cref="AppDbContext"/>.</summary>
    public SupplierRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Supplier>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.Suppliers.AsNoTracking().ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default) =>
        await _context.Suppliers.AddAsync(supplier, cancellationToken);

    /// <inheritdoc/>
    public void Update(Supplier supplier) => _context.Suppliers.Update(supplier);

    /// <inheritdoc/>
    public void Remove(Supplier supplier) => _context.Suppliers.Remove(supplier);

    /// <inheritdoc/>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
