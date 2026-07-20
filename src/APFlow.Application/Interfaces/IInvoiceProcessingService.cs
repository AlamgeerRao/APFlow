using APFlow.Application.DTOs;
using APFlow.Domain.Common;

namespace APFlow.Application.Interfaces;

/// <summary>
/// Orchestrates the end-to-end invoice ingestion pipeline: Email Synchronisation
/// (WP-006) -> PDF Extraction (WP-007) -> Blob Storage (WP-005) -> Document
/// Intelligence (WP-008) -> Duplicate Detection (WP-010) -> Database Save (WP-009).
/// WP-012 scope: orchestration of the above six steps only. "Approval", "Query
/// workflow", and "Supplier emails" (outbound notifications to suppliers) are all
/// explicit WP-012 out-of-scope - this service never changes an invoice's status
/// beyond the initial save, never emails a supplier, and exposes no query
/// capability (see <c>IInvoiceQueryService</c> from WP-011 for that).
/// Deliberately not wired into a scheduled/background polling loop by this work
/// package - same reasoning as <c>IEmailSyncService</c> (WP-006): that is
/// APFlow.Workers' responsibility (see Solution Structure), and nothing in WP-012's
/// task list asked for a polling/scheduling mechanism. This interface is what a
/// future Workers job would call, on whatever cadence it decides.
/// See docs/WP-012-Invoice-Processing-Pipeline-Decisions.md for the judgment calls
/// this orchestration makes that go beyond simply chaining the six steps together
/// (supplier resolution strategy, idempotency mechanism, retry scope, and how an
/// unresolvable supplier name or a failed duplicate check are each handled).
/// </summary>
public interface IInvoiceProcessingService
{
    /// <summary>
    /// Runs one full pipeline pass: syncs unread emails, and for every PDF
    /// attachment found, extracts it, uploads it to Blob Storage, analyzes it via
    /// Document Intelligence, resolves its supplier, saves it as a new invoice, and
    /// checks it for potential duplicates. Idempotent - safe to call repeatedly
    /// (e.g. on a timer) without reprocessing attachments a prior run already saved.
    /// Only fails outright (a failed <see cref="Result{TValue}"/>) if the initial
    /// email sync itself fails; a failure processing an individual attachment is
    /// reported per-item in the returned <see cref="InvoiceProcessingResult"/>
    /// rather than aborting the whole run.
    /// </summary>
    Task<Result<InvoiceProcessingResult>> ProcessUnreadEmailsAsync(CancellationToken cancellationToken = default);
}
