using APFlow.Application.DTOs;
using APFlow.Domain.Common;

namespace APFlow.Application.Interfaces;

/// <summary>
/// Read-optimized query capability for audit log entries: filtering, paging, and
/// sorting, returning read-shaped DTOs. Deliberately separate from
/// <see cref="IAuditService"/> (which owns writes) - same split as WP-011's
/// <c>IInvoiceQueryService</c> versus <c>IInvoiceService</c>. "Reporting" is
/// explicit WP-013 out-of-scope: this is a query capability for retrieving raw
/// entries (e.g. "this invoice's history"), not aggregation, export, or a
/// reporting surface.
/// </summary>
public interface IAuditQueryService
{
    /// <summary>
    /// Returns a filtered, sorted page of audit log entries visible to the current
    /// tenant. Validates <paramref name="parameters"/> (page/page size bounds, date
    /// range ordering) before querying and returns a <see cref="Result{TValue}"/>
    /// failure if invalid, rather than throwing.
    /// </summary>
    Task<Result<PagedResult<AuditLogDto>>> SearchAsync(
        AuditLogQueryParameters parameters, CancellationToken cancellationToken = default);
}
