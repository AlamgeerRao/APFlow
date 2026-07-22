using APFlow.Application.DTOs;
using APFlow.Domain.Entities;

namespace APFlow.Application.Interfaces;

/// <summary>
/// Detects potential duplicate supplier invoices before approval, by comparing
/// Supplier and Invoice Number (WP-047's confirmed two-field rule) between one
/// candidate invoice and a set of other invoices to compare it against.
/// A pure compute service (WP-048): takes plain <see cref="Invoice"/> data in and
/// returns a plain <see cref="DuplicateCheckResult"/> out, with NO repository,
/// DbContext, or any other I/O dependency at all - not even a read-only one. The
/// caller (see WP-012's <c>InvoiceProcessingService</c>) owns fetching both the
/// candidate and the comparison set (typically via <see cref="IInvoiceRepository"/>),
/// and owns persisting the result if it chooses to (see
/// <see cref="IInvoiceRepository.PersistDuplicateCheckResultAsync"/> and
/// docs/WP-048-Persist-Duplicate-Detection-Result.md). This is a deliberate,
/// literal reading of "no IInvoiceRepository dependency" - WP-010's original
/// ruling only required no <c>SaveChangesAsync</c> access, but WP-048 asks for
/// zero persistence dependency of any kind, which is only achievable by having the
/// caller supply data rather than this service fetching it.
/// </summary>
public interface IDuplicateDetectionService
{
    /// <summary>
    /// Checks <paramref name="candidate"/> for potential duplicates among
    /// <paramref name="otherInvoices"/> (<paramref name="candidate"/> itself does
    /// not need to be excluded from <paramref name="otherInvoices"/> - it is
    /// skipped automatically by id if present). Synchronous and side-effect-free:
    /// no I/O, and no failure mode - given a non-null <paramref name="candidate"/>,
    /// this always produces a result (an invoice with no duplicates is still a
    /// successful result with <see cref="DuplicateCheckResult.IsPotentialDuplicate"/>
    /// false).
    /// </summary>
    DuplicateCheckResult Check(Invoice candidate, IReadOnlyList<Invoice> otherInvoices);
}
