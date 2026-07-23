using APFlow.Application.Interfaces;

namespace APFlow.Application.DTOs;

/// <summary>
/// Lightweight read shape for an invoice returned by <see cref="IInvoiceQueryService"/>
/// list/search results. Deliberately narrower than <see cref="InvoiceDto"/> (used by
/// single-invoice reads): omits <c>NetAmount</c>, <c>Vat</c>, and
/// <c>SourceEmailMessageId</c>, which are detail-view fields not needed to render a
/// filtered results grid. Callers that need the full breakdown for one invoice use
/// <c>IInvoiceService.GetByIdAsync</c>, which still returns the complete
/// <see cref="InvoiceDto"/>. This is a presentation-shaping choice, not a business
/// rule - trivial to widen later if a list view turns out to need more fields.
/// </summary>
public sealed record InvoiceListItemDto(
    Guid Id,
    Guid SupplierId,
    string? SupplierName,
    string? SupplierInvoiceNumber,
    DateOnly? InvoiceDate,
    DateOnly? DueDate,
    string? Currency,
    decimal? GrossTotal,
    string Status,
    DateTimeOffset CreatedAtUtc);
