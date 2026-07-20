using APFlow.Application.Interfaces;

namespace APFlow.Application.DTOs;

/// <summary>
/// The outcome of processing a single PDF attachment during an
/// <see cref="IInvoiceProcessingService"/> run.
/// </summary>
public enum InvoiceProcessingOutcome
{
    /// <summary>The attachment was successfully processed and saved as a new invoice.</summary>
    Processed,

    /// <summary>
    /// The attachment had already been processed by a prior pipeline run (matched by
    /// <see cref="APFlow.Domain.Entities.Invoice.SourceDocumentBlobName"/> - see
    /// docs/WP-012-Invoice-Processing-Pipeline-Decisions.md) and was skipped rather
    /// than reprocessed. This is the pipeline's idempotency guarantee showing up in
    /// its own result, not an error.
    /// </summary>
    AlreadyProcessed,

    /// <summary>
    /// Processing this attachment failed after any applicable retries. See
    /// <see cref="InvoiceProcessingItemResult.ErrorCode"/> and
    /// <see cref="InvoiceProcessingItemResult.ErrorMessage"/> for why. A failure here
    /// does not stop other attachments (in the same or other emails) from being
    /// processed, and the source email is not marked as processed, so this
    /// attachment is retried on the next pipeline run.
    /// </summary>
    Failed,
}

/// <summary>
/// The result of attempting to process a single PDF attachment (one candidate
/// invoice) found on a synced email.
/// </summary>
/// <param name="MessageId">The source email's Graph message id.</param>
/// <param name="FileName">The attachment's file name, or null if this result represents a failure that occurred before attachments could even be listed (e.g. PDF extraction itself failed).</param>
/// <param name="Outcome">What happened - see <see cref="InvoiceProcessingOutcome"/>.</param>
/// <param name="InvoiceId">The resulting (or, for <see cref="InvoiceProcessingOutcome.AlreadyProcessed"/>, the pre-existing) invoice's id. Null only when <see cref="Outcome"/> is <see cref="InvoiceProcessingOutcome.Failed"/>.</param>
/// <param name="IsPotentialDuplicate">
/// The outcome of the pipeline's duplicate-detection step (see
/// <see cref="IDuplicateDetectionService"/>), if it ran and succeeded. Null when the
/// invoice was not saved (<see cref="Outcome"/> is not <see cref="InvoiceProcessingOutcome.Processed"/>)
/// or when the duplicate check itself failed - a failed duplicate check does not
/// fail the item overall (the invoice is still saved); see
/// docs/WP-012-Invoice-Processing-Pipeline-Decisions.md. This flag is advisory only,
/// exactly as WP-010 defined it - it never triggers an automatic status change or
/// rejection ("Approval" is explicit WP-012 out-of-scope). Unlike this per-run report
/// field, the underlying result IS now persisted directly onto the invoice (see
/// <see cref="APFlow.Domain.Entities.Invoice.IsPotentialDuplicate"/>) per WP-010's
/// persistence ruling in docs/WP-010-Duplicate-Flag-Persistence-Decision.md.
/// </param>
/// <param name="ErrorCode">The failing step's <c>Error.Code</c>, if <see cref="Outcome"/> is <see cref="InvoiceProcessingOutcome.Failed"/>.</param>
/// <param name="ErrorMessage">The failing step's <c>Error.Message</c>, if <see cref="Outcome"/> is <see cref="InvoiceProcessingOutcome.Failed"/>.</param>
public sealed record InvoiceProcessingItemResult(
    string MessageId,
    string? FileName,
    InvoiceProcessingOutcome Outcome,
    Guid? InvoiceId,
    bool? IsPotentialDuplicate,
    string? ErrorCode,
    string? ErrorMessage);

/// <summary>
/// The overall result of one <see cref="IInvoiceProcessingService.ProcessUnreadEmailsAsync"/>
/// run: how many emails were synced, how many were fully handled and marked
/// processed, and the per-attachment breakdown.
/// </summary>
/// <param name="EmailsSynced">Total unread emails returned by this run's email sync step.</param>
/// <param name="EmailsMarkedProcessed">
/// How many of those emails were fully handled (every attachment either processed
/// or already-processed, none failed) and successfully marked as processed in the
/// mailbox. An email is deliberately left unmarked - and so re-synced next run - if
/// any of its attachments failed, or if marking itself failed; per-attachment
/// idempotency (see <see cref="InvoiceProcessingOutcome.AlreadyProcessed"/>) makes
/// that safe to retry without reprocessing what already succeeded.
/// </param>
/// <param name="Items">Every attachment this run attempted, in processing order.</param>
public sealed record InvoiceProcessingResult(
    int EmailsSynced,
    int EmailsMarkedProcessed,
    IReadOnlyList<InvoiceProcessingItemResult> Items);
