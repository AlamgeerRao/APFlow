namespace APFlow.Application.DTOs;

/// <summary>
/// Per-status invoice count for the current tenant's folder view (WP-059 Part A) -
/// backs <c>GET /api/invoices/folders</c>. Field names/casing match
/// docs/WP-019-Supplier-Folder-Views-Decisions.md §1's proposed contract exactly
/// (<c>statusCode</c>/<c>statusLabel</c>/<c>count</c> once serialized) - no
/// naming conflict to resolve here.
/// </summary>
public sealed record FolderSummaryDto(string StatusCode, string StatusLabel, int Count);

/// <summary>
/// One supplier's invoices within the current folder/search/supplier-filter
/// context (WP-059 Part A) - backs <c>GET /api/invoices/grouped</c>.
/// <see cref="Invoices"/> reuses the existing, already-established
/// <see cref="InvoiceListItemDto"/> as-is (same fields <c>GET /api/invoices</c>
/// returns: <c>SupplierInvoiceNumber</c>, <c>GrossTotal</c>, <c>Currency</c>, ...)
/// rather than the frontend fixture's renamed <c>InvoiceListItem</c> shape
/// (<c>invoiceNumber</c>, <c>amount</c>, <c>currencyCode</c>) - backend wins on
/// this naming conflict, per this task's own instruction and the same rule
/// already applied when WP-015/016 were reconciled (see docs/Backlog.md's closed
/// items).
/// </summary>
public sealed record SupplierGroupDto(string SupplierName, int Count, IReadOnlyList<InvoiceListItemDto> Invoices);

/// <summary>
/// A page of supplier-grouped invoices (WP-059 Part A) - backs
/// <c>GET /api/invoices/grouped</c>. Paginates over <see cref="Groups"/>
/// (supplier groups), not individual invoices - per
/// docs/WP-019-Supplier-Folder-Views-Decisions.md §3's decision that grouping is
/// the view's core structural unit, so a supplier with many invoices isn't
/// artificially cut off mid-list.
/// </summary>
public sealed record SupplierGroupedInvoicesDto(
    IReadOnlyList<SupplierGroupDto> Groups,
    int TotalSuppliers,
    int Page,
    int PageSize);

/// <summary>
/// Filter/paging parameters for <c>ISupplierFolderQueryService.GetGroupedInvoicesAsync</c>
/// (WP-059 Part A). Tenant scoping is never a parameter here, same reasoning as
/// <see cref="InvoiceQueryParameters"/> - it comes from the current caller's JWT via
/// the underlying EF Core query filter, not an explicit <c>tenantId</c> argument
/// (unlike the frontend fixture's client-side signatures, which must pass one
/// explicitly since there is no real auth context to derive it from yet).
/// </summary>
/// <param name="Folder">Optional single <c>StatusReference.Code</c> to narrow to one folder. Omitted = every non-terminal status.</param>
/// <param name="Supplier">Optional exact supplier name to narrow to.</param>
/// <param name="Search">Free-text match against supplier name and invoice number - reuses <see cref="InvoiceQueryParameters.Search"/>'s exact semantics.</param>
/// <param name="Page">1-based page number, paginating over supplier groups.</param>
/// <param name="PageSize">Groups per page. Must be between 1 and <see cref="MaxPageSize"/>.</param>
public sealed record SupplierFolderQueryParameters(
    string? Folder = null,
    string? Supplier = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 5) // Mirrors DefaultPageSize below - kept as a literal for the
                       // same reason InvoiceQueryParameters.PageSize's default is:
                       // a primary constructor parameter default cannot reference a
                       // const declared in the same record's body.
{
    /// <summary>Default page size (over supplier groups) - matches docs/WP-019-Supplier-Folder-Views-Decisions.md §3's decision exactly.</summary>
    public const int DefaultPageSize = 5;

    /// <summary>
    /// Upper bound on <see cref="PageSize"/>. Deliberately much smaller than
    /// <see cref="InvoiceQueryParameters.MaxPageSize"/> (100) - this paginates
    /// SUPPLIER GROUPS, not individual invoice rows, and each group can itself
    /// contain many invoices, so a much lower cap keeps a single response
    /// reasonably sized.
    /// </summary>
    public const int MaxPageSize = 50;
}
