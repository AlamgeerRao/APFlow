using APFlow.Application.DTOs;
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

    /// <summary>
    /// In-memory re-implementation of InvoiceRepository.QueryAsync's filter/sort/
    /// paging semantics, so InvoiceQueryServiceTests can assert on behavior without
    /// a real EF Core provider. Kept deliberately in step with the real
    /// implementation - see InvoiceRepositoryQueryTests (APFlow.Infrastructure.Tests)
    /// for the equivalent assertions against a real (InMemory-provider) DbContext.
    /// </summary>
    public Task<(IReadOnlyList<Invoice> Items, int TotalCount)> QueryAsync(
        InvoiceQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<Invoice> query = Invoices;

        if (parameters.Status is not null)
        {
            query = query.Where(i => i.Status == parameters.Status);
        }

        if (parameters.SupplierId is not null)
        {
            query = query.Where(i => i.SupplierId == parameters.SupplierId);
        }

        if (parameters.InvoiceDateFrom is not null)
        {
            query = query.Where(i => i.InvoiceDate is not null && i.InvoiceDate >= parameters.InvoiceDateFrom);
        }

        if (parameters.InvoiceDateTo is not null)
        {
            query = query.Where(i => i.InvoiceDate is not null && i.InvoiceDate <= parameters.InvoiceDateTo);
        }

        if (!string.IsNullOrWhiteSpace(parameters.InvoiceNumber))
        {
            // OrdinalIgnoreCase here approximates SQL Server's typical default
            // collation (case-insensitive) for this fake's purposes. The real
            // InvoiceRepository uses the plain single-argument Contains(string) - EF
            // Core cannot translate the StringComparison overload to SQL - so actual
            // case sensitivity there is whatever the database collation says (see
            // InvoiceQueryParameters.InvoiceNumber's doc comment).
            query = query.Where(i => i.SupplierInvoiceNumber is not null
                                      && i.SupplierInvoiceNumber.Contains(parameters.InvoiceNumber, StringComparison.OrdinalIgnoreCase));
        }

        var totalCount = query.Count();

        query = ApplySort(query, parameters.SortBy, parameters.SortDescending);

        var page = Math.Max(parameters.Page, 1);
        var pageSize = Math.Clamp(parameters.PageSize, 1, InvoiceQueryParameters.MaxPageSize);

        var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult<(IReadOnlyList<Invoice> Items, int TotalCount)>((items, totalCount));
    }

    private static IEnumerable<Invoice> ApplySort(IEnumerable<Invoice> query, InvoiceSortField sortBy, bool descending) =>
        sortBy switch
        {
            InvoiceSortField.InvoiceDate => descending ? query.OrderByDescending(i => i.InvoiceDate) : query.OrderBy(i => i.InvoiceDate),
            InvoiceSortField.DueDate => descending ? query.OrderByDescending(i => i.DueDate) : query.OrderBy(i => i.DueDate),
            InvoiceSortField.GrossTotal => descending ? query.OrderByDescending(i => i.GrossTotal) : query.OrderBy(i => i.GrossTotal),
            InvoiceSortField.SupplierName => descending ? query.OrderByDescending(i => i.Supplier?.Name) : query.OrderBy(i => i.Supplier?.Name),
            InvoiceSortField.SupplierInvoiceNumber => descending ? query.OrderByDescending(i => i.SupplierInvoiceNumber) : query.OrderBy(i => i.SupplierInvoiceNumber),
            InvoiceSortField.Status => descending ? query.OrderByDescending(i => i.Status) : query.OrderBy(i => i.Status),
            _ => descending ? query.OrderByDescending(i => i.CreatedAtUtc) : query.OrderBy(i => i.CreatedAtUtc),
        };

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
