using APFlow.Application.DTOs;
using APFlow.Domain.Common;

namespace APFlow.Application.Interfaces;

/// <summary>
/// Read-optimized query capability backing WP-059 Part A's three endpoints -
/// folder-count summaries, supplier-name listing, and supplier-grouped invoice
/// listing - the backend contract
/// docs/WP-019-Supplier-Folder-Views-Decisions.md §1 proposed and
/// docs/Backlog.md's "WP-019 — real backend endpoints" item tracked as blocking.
/// Deliberately a composition over the existing, already-tested
/// <see cref="IWorkflowQueryService"/> (folder list) and
/// <see cref="IInvoiceQueryService"/> (invoice search/counts) - no new
/// repository method or business rule is introduced; this interface only
/// aggregates data those two already expose.
/// </summary>
public interface ISupplierFolderQueryService
{
    /// <summary>
    /// Returns one entry per non-terminal status in the current tenant's active
    /// Invoice workflow template, with the count of invoices currently in that
    /// status (honouring <paramref name="search"/>, same semantics as
    /// <see cref="InvoiceQueryParameters.Search"/>), ordered by the template's own
    /// <c>SortOrder</c>.
    /// </summary>
    Task<Result<IReadOnlyList<FolderSummaryDto>>> GetFolderCountsAsync(
        string? search, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the distinct supplier names appearing among invoices matching
    /// <paramref name="folder"/>/<paramref name="search"/> (both optional), sorted
    /// alphabetically - for populating a supplier filter dropdown scoped to what's
    /// actually in the current folder/search context, not every supplier the
    /// tenant has ever recorded.
    /// </summary>
    Task<Result<IReadOnlyList<string>>> GetSupplierNamesAsync(
        string? folder, string? search, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a page of supplier-grouped invoices matching
    /// <paramref name="parameters"/>. Validates <paramref name="parameters"/>
    /// (page/page size bounds) before querying and returns a failure rather than
    /// throwing, same convention as <see cref="IInvoiceQueryService.SearchAsync"/>.
    /// </summary>
    Task<Result<SupplierGroupedInvoicesDto>> GetGroupedInvoicesAsync(
        SupplierFolderQueryParameters parameters, CancellationToken cancellationToken = default);
}
