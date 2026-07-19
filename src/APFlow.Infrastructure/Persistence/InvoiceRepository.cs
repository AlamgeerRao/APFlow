using APFlow.Application.Interfaces;
using APFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace APFlow.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IInvoiceRepository"/>. Tenant isolation on
/// every read comes from AppDbContext's query filter (see AppDbContext), not from
/// any logic here - this class does not reference tenant/current-user state at all.
/// </summary>
public sealed class InvoiceRepository : IInvoiceRepository
{
    private readonly AppDbContext _context;

    /// <summary>Creates the repository over the given <see cref="AppDbContext"/>.</summary>
    public InvoiceRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Invoices
            .Include(i => i.Supplier)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    /// <inheritdoc/>
    public Task<Invoice?> GetByIdWithNotesAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Invoices
            .Include(i => i.Supplier)
            .Include(i => i.Notes)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.Invoices
            .Include(i => i.Supplier)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default) =>
        await _context.Invoices.AddAsync(invoice, cancellationToken);

    /// <inheritdoc/>
    public void Update(Invoice invoice) => _context.Invoices.Update(invoice);

    /// <inheritdoc/>
    public void Remove(Invoice invoice) => _context.Invoices.Remove(invoice);

    /// <inheritdoc/>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
