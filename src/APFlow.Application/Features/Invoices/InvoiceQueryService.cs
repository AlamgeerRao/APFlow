using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using APFlow.Domain.Entities;

namespace APFlow.Application.Features.Invoices;

/// <summary>
/// Default implementation of <see cref="IInvoiceQueryService"/>. Depends only on
/// <see cref="IInvoiceRepository"/> (a plain, EF-Core-free interface), so this class
/// is fully unit-testable with a fake repository - no database, no EF Core provider
/// required. Validates paging/date-range inputs itself; delegates all filtering,
/// sorting, and paging execution to <see cref="IInvoiceRepository.QueryAsync"/> so
/// the actual work happens at the data store, not in application memory.
/// </summary>
public sealed class InvoiceQueryService : IInvoiceQueryService
{
    private readonly IInvoiceRepository _invoiceRepository;

    /// <summary>Creates a new <see cref="InvoiceQueryService"/>.</summary>
    public InvoiceQueryService(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<InvoiceListItemDto>>> SearchAsync(
        InvoiceQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var validationError = Validate(parameters);
        if (validationError is not null)
        {
            return Result.Failure<PagedResult<InvoiceListItemDto>>(validationError);
        }

        var (items, totalCount) = await _invoiceRepository.QueryAsync(parameters, cancellationToken);

        var dtoItems = items.Select(ToListItemDto).ToList();

        return Result.Success(new PagedResult<InvoiceListItemDto>(dtoItems, totalCount, parameters.Page, parameters.PageSize));
    }

    /// <summary>
    /// Validates paging bounds and date-range ordering before anything reaches the
    /// repository, so bad input comes back as a clean <see cref="Result"/> failure
    /// with a specific error code rather than an out-of-range query or a
    /// silently-empty/incorrect page. Mirrors the validate-before-repository
    /// pattern used by <c>InvoiceService.ValidateFields</c>.
    /// </summary>
    private static Error? Validate(InvoiceQueryParameters parameters)
    {
        if (parameters.Page < 1)
        {
            return new Error("InvoiceQuery.InvalidPage", "Page must be 1 or greater.");
        }

        if (parameters.PageSize < 1 || parameters.PageSize > InvoiceQueryParameters.MaxPageSize)
        {
            return new Error(
                "InvoiceQuery.InvalidPageSize",
                $"PageSize must be between 1 and {InvoiceQueryParameters.MaxPageSize}.");
        }

        if (parameters.InvoiceDateFrom is not null
            && parameters.InvoiceDateTo is not null
            && parameters.InvoiceDateFrom > parameters.InvoiceDateTo)
        {
            return new Error(
                "InvoiceQuery.InvalidDateRange",
                "InvoiceDateFrom must not be later than InvoiceDateTo.");
        }

        return null;
    }

    private static InvoiceListItemDto ToListItemDto(Invoice invoice) => new(
        Id: invoice.Id,
        SupplierId: invoice.SupplierId,
        SupplierName: invoice.Supplier?.Name,
        SupplierInvoiceNumber: invoice.SupplierInvoiceNumber,
        InvoiceDate: invoice.InvoiceDate,
        DueDate: invoice.DueDate,
        Currency: invoice.Currency,
        GrossTotal: invoice.GrossTotal,
        Status: invoice.Status,
        CreatedAtUtc: invoice.CreatedAtUtc);
}
