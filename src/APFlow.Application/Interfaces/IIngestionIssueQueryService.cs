using APFlow.Application.DTOs;
using APFlow.Domain.Common;

namespace APFlow.Application.Interfaces;

/// <summary>
/// Read-optimized query capability for ingestion issues: paging and sorting,
/// returning read-shaped DTOs (WP-076). Deliberately separate from the pipeline's
/// own write path in <c>InvoiceProcessingService</c> - same split as
/// <see cref="IAuditQueryService"/> versus <see cref="IAuditService"/>.
/// </summary>
public interface IIngestionIssueQueryService
{
    /// <summary>
    /// Returns a sorted page of ingestion issues visible to the current tenant.
    /// Validates <paramref name="parameters"/> (page/page size bounds) before
    /// querying and returns a <see cref="Result{TValue}"/> failure if invalid,
    /// rather than throwing.
    /// </summary>
    Task<Result<PagedResult<IngestionIssueDto>>> SearchAsync(
        IngestionIssueQueryParameters parameters, CancellationToken cancellationToken = default);
}
