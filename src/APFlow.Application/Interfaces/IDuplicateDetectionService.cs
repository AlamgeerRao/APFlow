using APFlow.Application.DTOs;
using APFlow.Domain.Common;

namespace APFlow.Application.Interfaces;

/// <summary>
/// Detects potential duplicate supplier invoices before approval, by comparing
/// Supplier and Invoice Number against every other invoice visible to the current
/// tenant (WP-047 - corrects WP-010's original four-field rule per the Product
/// Owner's confirmed requirement; see
/// docs/WP-047-Duplicate-Matching-Reconciliation.md).
/// WP-010 scope only: detection and reporting. This service never modifies an
/// invoice, never rejects it, and is not wired into any approval workflow, UI, or
/// supplier communication - all explicit WP-010 out-of-scope items. Callers decide
/// what to do with a flagged result.
/// </summary>
public interface IDuplicateDetectionService
{
    /// <summary>
    /// Checks the given invoice for potential duplicates among every other invoice
    /// visible to the current tenant (tenant isolation is enforced by
    /// <see cref="IInvoiceRepository"/>'s underlying query filter, not by this
    /// service). Returns a failure only if the invoice itself cannot be found; an
    /// invoice with no duplicates is still a *successful* result with
    /// <see cref="DuplicateCheckResult.IsPotentialDuplicate"/> set to false.
    /// </summary>
    Task<Result<DuplicateCheckResult>> CheckAsync(Guid invoiceId, CancellationToken cancellationToken = default);
}
