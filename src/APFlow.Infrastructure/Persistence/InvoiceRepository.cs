using APFlow.Application.DTOs;
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

    /// <inheritdoc />
    public async Task<(IReadOnlyList<Invoice> Items, int TotalCount)> QueryAsync(
        InvoiceQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Invoices
            .Include(i => i.Supplier)
            .AsNoTracking()
            .AsQueryable();

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
            query = query.Where(i => i.InvoiceDate != null && i.InvoiceDate >= parameters.InvoiceDateFrom);
        }

        if (parameters.InvoiceDateTo is not null)
        {
            query = query.Where(i => i.InvoiceDate != null && i.InvoiceDate <= parameters.InvoiceDateTo);
        }

        if (!string.IsNullOrWhiteSpace(parameters.InvoiceNumber))
        {
            query = query.Where(i => i.SupplierInvoiceNumber != null && i.SupplierInvoiceNumber.Contains(parameters.InvoiceNumber));
        }

        // Count against the filtered-but-unsorted, unpaged query - this is the total
        // across all pages, not the page size. Executed as a single SQL COUNT(*),
        // not a client-side count of a materialized list.
        var totalCount = await query.CountAsync(cancellationToken);

        query = ApplySort(query, parameters.SortBy, parameters.SortDescending);

        // Defensive clamp, not the primary validation - see InvoiceQueryParameters.MaxPageSize's
        // doc comment. IInvoiceQueryService.SearchAsync validates and rejects invalid
        // paging before this is ever called; this exists so a future caller that
        // bypasses the service still cannot request an unbounded result set.
        var page = Math.Max(parameters.Page, 1);
        var pageSize = Math.Clamp(parameters.PageSize, 1, InvoiceQueryParameters.MaxPageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    private static IQueryable<Invoice> ApplySort(IQueryable<Invoice> query, InvoiceSortField sortBy, bool descending) =>
        sortBy switch
        {
            InvoiceSortField.InvoiceDate => descending ? query.OrderByDescending(i => i.InvoiceDate) : query.OrderBy(i => i.InvoiceDate),
            InvoiceSortField.DueDate => descending ? query.OrderByDescending(i => i.DueDate) : query.OrderBy(i => i.DueDate),
            InvoiceSortField.GrossTotal => descending ? query.OrderByDescending(i => i.GrossTotal) : query.OrderBy(i => i.GrossTotal),
            InvoiceSortField.SupplierName => descending ? query.OrderByDescending(i => i.Supplier!.Name) : query.OrderBy(i => i.Supplier!.Name),
            InvoiceSortField.SupplierInvoiceNumber => descending ? query.OrderByDescending(i => i.SupplierInvoiceNumber) : query.OrderBy(i => i.SupplierInvoiceNumber),
            InvoiceSortField.Status => descending ? query.OrderByDescending(i => i.Status) : query.OrderBy(i => i.Status),
            _ => descending ? query.OrderByDescending(i => i.CreatedAtUtc) : query.OrderBy(i => i.CreatedAtUtc),
        };

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

    /// <inheritdoc/>
    public async Task<bool> PersistDuplicateCheckResultAsync(
        Guid invoiceId, bool isPotentialDuplicate, string? duplicateCheckReason, CancellationToken cancellationToken = default)
    {
        var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);
        if (invoice is null)
        {
            return false;
        }

        invoice.IsPotentialDuplicate = isPotentialDuplicate;
        invoice.DuplicateCheckReason = duplicateCheckReason;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
