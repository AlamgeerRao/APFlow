using APFlow.Application.Interfaces;
using APFlow.Domain.Entities;

namespace APFlow.Application.Tests.Features;

/// <summary>Hand-written fake, same pattern as every Graph/Blob fake elsewhere in this codebase.</summary>
internal sealed class FakeInvoiceRepository : IInvoiceRepository
{
    public List<Invoice> Invoices { get; } = [];
    public bool SaveChangesCalled { get; private set; }

    public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Invoices.FirstOrDefault(i => i.Id == id));

    public Task<Invoice?> GetByIdWithNotesAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Invoices.FirstOrDefault(i => i.Id == id));

    public Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Invoice>>(Invoices);

    public Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        Invoices.Add(invoice);
        return Task.CompletedTask;
    }

    public void Update(Invoice invoice)
    {
    }

    public void Remove(Invoice invoice) => Invoices.Remove(invoice);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCalled = true;
        return Task.FromResult(1);
    }
}

/// <summary>Hand-written fake, same pattern as every Graph/Blob fake elsewhere in this codebase.</summary>
internal sealed class FakeSupplierRepository : ISupplierRepository
{
    public List<Supplier> Suppliers { get; } = [];

    public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Suppliers.FirstOrDefault(s => s.Id == id));

    public Task<IReadOnlyList<Supplier>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Supplier>>(Suppliers);

    public Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        Suppliers.Add(supplier);
        return Task.CompletedTask;
    }

    public void Update(Supplier supplier)
    {
    }

    public void Remove(Supplier supplier) => Suppliers.Remove(supplier);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
}
