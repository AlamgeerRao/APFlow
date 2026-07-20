using APFlow.Application.Interfaces;
using APFlow.Domain.Enums;

namespace APFlow.Application.DTOs;

/// <summary>
/// The field an invoice query is sorted by. Sorting is an application-level query
/// concept, not a domain concept - deliberately kept out of APFlow.Domain (compare
/// to <see cref="APFlow.Domain.Enums.InvoiceStatus"/>, which represents a real
/// business state).
/// </summary>
public enum InvoiceSortField
{
    /// <summary>Default. Most-recently-ingested invoices are usually what an AP user wants to see first.</summary>
    CreatedAtUtc,

    /// <summary>Sort by <see cref="APFlow.Domain.Entities.Invoice.InvoiceDate"/>.</summary>
    InvoiceDate,

    /// <summary>Sort by <see cref="APFlow.Domain.Entities.Invoice.DueDate"/>.</summary>
    DueDate,

    /// <summary>Sort by <see cref="APFlow.Domain.Entities.Invoice.GrossTotal"/>.</summary>
    GrossTotal,

    /// <summary>Sort by the issuing supplier's <see cref="APFlow.Domain.Entities.Supplier.Name"/>.</summary>
    SupplierName,

    /// <summary>Sort by <see cref="APFlow.Domain.Entities.Invoice.SupplierInvoiceNumber"/>.</summary>
    SupplierInvoiceNumber,

    /// <summary>Sort by <see cref="APFlow.Domain.Enums.InvoiceStatus"/>.</summary>
    Status,
}

/// <summary>
/// Filter, paging, and sort parameters for <see cref="IInvoiceQueryService"/> /
/// <see cref="APFlow.Application.Interfaces.IInvoiceRepository.QueryAsync"/>.
/// All filter properties are optional (null = not filtered on that field).
/// Tenant scoping is never a parameter here - it comes from the current caller's
/// JWT via the underlying EF Core query filter (see AppDbContext), the same as
/// every other invoice read in this codebase.
/// </summary>
/// <param name="Status">Restrict to invoices in exactly this status, if set.</param>
/// <param name="SupplierId">Restrict to invoices issued by this supplier, if set.</param>
/// <param name="InvoiceDateFrom">Restrict to invoices with <c>InvoiceDate</c> on or after this date, if set. Invoices with a null InvoiceDate never match when this is set.</param>
/// <param name="InvoiceDateTo">Restrict to invoices with <c>InvoiceDate</c> on or before this date, if set. Invoices with a null InvoiceDate never match when this is set.</param>
/// <param name="InvoiceNumber">
/// Restrict to invoices whose <c>SupplierInvoiceNumber</c> contains this text
/// (case sensitivity depends on the database collation), if set. A substring/
/// "search" match was chosen over an exact match because invoice numbers are
/// free-text values supplied by suppliers (see <see cref="APFlow.Domain.Entities.Invoice.SupplierInvoiceNumber"/>'s
/// doc comment) and users are more likely to remember a fragment than the exact
/// string. This is an implementation default, not a business rule - trivial to
/// change to exact/prefix matching if that turns out to be wrong.
/// </param>
/// <param name="Page">1-based page number. Must be 1 or greater.</param>
/// <param name="PageSize">Rows per page. Must be between 1 and <see cref="MaxPageSize"/>.</param>
/// <param name="SortBy">Field to sort by. Defaults to <see cref="InvoiceSortField.CreatedAtUtc"/>.</param>
/// <param name="SortDescending">Sort direction. Defaults to descending (newest/highest first).</param>
public sealed record InvoiceQueryParameters(
    InvoiceStatus? Status = null,
    Guid? SupplierId = null,
    DateOnly? InvoiceDateFrom = null,
    DateOnly? InvoiceDateTo = null,
    string? InvoiceNumber = null,
    int Page = 1,
    int PageSize = 25, // Mirrors DefaultPageSize below - kept as a literal because a
                        // primary constructor parameter default cannot reference a
                        // const declared in the same record's body.
    InvoiceSortField SortBy = InvoiceSortField.CreatedAtUtc,
    bool SortDescending = true)
{
    /// <summary>Default page size when a caller doesn't specify one.</summary>
    public const int DefaultPageSize = 25;

    /// <summary>
    /// Upper bound on <see cref="PageSize"/>. Enforced (returning a validation
    /// <c>Error</c>) by <see cref="APFlow.Application.Features.Invoices.InvoiceQueryService"/>,
    /// and enforced again defensively (by clamping, not throwing) inside
    /// <c>APFlow.Infrastructure.Persistence.InvoiceRepository.QueryAsync</c> so a
    /// future caller that bypasses the service cannot request an unbounded result
    /// set from the database.
    /// </summary>
    public const int MaxPageSize = 100;
}
