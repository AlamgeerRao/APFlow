using APFlow.Application.DTOs;
using APFlow.Domain.Common;

namespace APFlow.Application.Interfaces;

/// <summary>
/// Read-optimized query capability for invoices: filtering, paging, and sorting,
/// returning lightweight DTOs. Deliberately separate from <see cref="IInvoiceService"/>
/// (which owns single-invoice reads/writes) - this interface exists purely to serve
/// list/search views efficiently, and has no create/update/delete responsibility.
/// </summary>
public interface IInvoiceQueryService
{
    /// <summary>
    /// Returns a filtered, sorted page of invoices visible to the current tenant.
    /// Validates <paramref name="parameters"/> (page/page size bounds, date range
    /// ordering) before querying and returns a <see cref="Result{TValue}"/> failure
    /// if invalid, rather than throwing.
    /// </summary>
    Task<Result<PagedResult<InvoiceListItemDto>>> SearchAsync(
        InvoiceQueryParameters parameters,
        CancellationToken cancellationToken = default);
}
