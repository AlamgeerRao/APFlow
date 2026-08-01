using APFlow.Domain.Common.Constants;

namespace APFlow.Domain.Entities;

/// <summary>
/// A supplier invoice moving through AP Flow's ingestion pipeline. Fields mirror
/// WP-008's <c>InvoiceExtractionResult</c> - a natural persistence target for that
/// service's output - but this entity has no dependency on WP-008 and can be
/// created/edited independent of it (see <c>IInvoiceService</c>).
/// Per-field extraction confidence (WP-056) is deliberately NOT columns here -
/// see <see cref="ExtractedFields"/> - closing the gap this doc comment
/// originally flagged ("Add if/when that's a real requirement"; WP-052 Part D's
/// review of the Invoice Detail API was that requirement).
/// <see cref="SourceDocumentBlobName"/> is the one exception to the "no Blob Storage
/// linkage" stance WP-009 originally took: WP-009 excluded it because nothing yet
/// called for it. WP-012 (Invoice Processing Pipeline) now does - the pipeline
/// stores each extracted PDF in Blob Storage as an explicit orchestration step, and
/// needs a durable reference back to it (both for later retrieval and, together
/// with the source email, as this entity's idempotency key - see
/// docs/WP-012-Invoice-Processing-Pipeline-Decisions.md). No other storage-strategy
/// assumptions (per-attachment vs per-invoice blobs, retention, etc.) are implied
/// beyond what that document states.
/// No behavior methods (e.g. Approve()/Reject()) - "Approval workflow" is explicit
/// WP-009 out-of-scope. <see cref="Status"/> is a plain mutable property; a future
/// work package is responsible for enforcing which transitions are valid.
/// </summary>
public sealed class Invoice : TenantEntity
{
    /// <summary>The supplier this invoice was issued by.</summary>
    public Guid SupplierId { get; set; }

    /// <summary>Navigation property to the issuing supplier.</summary>
    public Supplier? Supplier { get; set; }

    /// <summary>The supplier's own invoice number/ID, if known.</summary>
    public string? SupplierInvoiceNumber { get; set; }

    /// <summary>The date the invoice was issued, if known.</summary>
    public DateOnly? InvoiceDate { get; set; }

    /// <summary>The payment due date, if known.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>The invoice's currency code (e.g. "GBP"), if known.</summary>
    public string? Currency { get; set; }

    /// <summary>The invoice subtotal before tax, if known.</summary>
    public decimal? NetAmount { get; set; }

    /// <summary>The tax amount, if known.</summary>
    public decimal? Vat { get; set; }

    /// <summary>The total amount including tax, if known.</summary>
    public decimal? GrossTotal { get; set; }

    /// <summary>
    /// Where this invoice currently stands - a status CODE (e.g. "RECEIVED",
    /// "APPROVED") that must match a <c>StatusReference.Code</c> row within the
    /// acting tenant's active <c>WorkflowTemplate</c> (WP-050). A plain string, not
    /// an enum: WP-050 retired the old <c>InvoiceStatus</c> enum specifically
    /// because status values are tenant-configurable data (a shared enum cannot
    /// represent a tenant-specific status like GB Skips'
    /// <see cref="APFlow.Domain.Common.Constants.InvoiceStatusCodes.CheckedReadyToApprove"/>).
    /// See <see cref="APFlow.Domain.Common.Constants.InvoiceStatusCodes"/> for named
    /// constants covering the platform-default codes.
    /// </summary>
    public string Status { get; set; } = InvoiceStatusCodes.Received;

    /// <summary>
    /// The Graph message id (see WP-006) this invoice was sourced from, if it
    /// arrived via email sync. Traceability only - not a foreign key to any table,
    /// and not validated against Graph. Null for invoices created by other means
    /// (e.g. manual entry, once that exists).
    /// </summary>
    public string? SourceEmailMessageId { get; set; }

    /// <summary>
    /// The logical Blob Storage name (see <c>IBlobStorageService</c>) of the source
    /// PDF this invoice was extracted from, if processed via the WP-012 pipeline.
    /// Traceability only, same shape and reasoning as <see cref="SourceEmailMessageId"/>
    /// (not a foreign key, not validated against Blob Storage) - purely a
    /// storage-path/traceability field as of WP-052. Previously (WP-012-WP-051)
    /// this also served as the pipeline's idempotency key; WP-052 moved that role
    /// to <see cref="SourceDocumentContentHash"/> specifically to close a
    /// same-filename data-loss risk - two distinct PDF attachments on the same
    /// email that happened to share a file name collided under the old
    /// blob-name-based key (a real, known gap - see
    /// docs/WP-012-Invoice-Processing-Pipeline-Decisions.md item 2) and would
    /// silently NOT both be processed. Null for invoices created by other means
    /// (e.g. manual entry, once that exists).
    /// </summary>
    public string? SourceDocumentBlobName { get; set; }

    /// <summary>
    /// The SHA-256 hash (lowercase hex, 64 characters) of the source PDF's raw
    /// bytes, if processed via the WP-012 pipeline - computed once during
    /// extraction (WP-007's output is already an in-memory byte array; no re-read
    /// of the attachment is needed). WP-052's pipeline idempotency key: unlike
    /// <see cref="SourceDocumentBlobName"/> (which is derived from the email
    /// message id and file name, and could collide for two distinct attachments
    /// that happen to share a file name), this key is derived from the actual
    /// document content, so two attachments are only ever treated as "the same
    /// document already processed" if their bytes are identical - regardless of
    /// what either is named. Null for invoices created by other means.
    /// </summary>
    public string? SourceDocumentContentHash { get; set; }

    /// <summary>
    /// Whether a duplicate-detection check (<c>IDuplicateDetectionService</c>, WP-010)
    /// most recently flagged this invoice as a potential duplicate of another invoice
    /// visible to the tenant. Computed, not user-editable - set only by whichever
    /// caller invokes the check and persists its result (currently
    /// <c>InvoiceProcessingService</c>, WP-012's ingestion orchestrator). Defaults to
    /// false for an invoice the check has never run against, which reads the same as
    /// "checked and found not to be a duplicate" - this field does not distinguish
    /// the two. See docs/WP-010-Duplicate-Flag-Persistence-Decision.md for why this
    /// is persisted here rather than staying an ephemeral, recompute-on-demand result.
    /// </summary>
    public bool IsPotentialDuplicate { get; set; }

    /// <summary>
    /// A human-readable summary of why <see cref="IsPotentialDuplicate"/> is true -
    /// which existing invoice(s) it matched and on which fields. Null whenever
    /// <see cref="IsPotentialDuplicate"/> is false, including "never checked".
    /// System-computed, same reasoning as <see cref="IsPotentialDuplicate"/> - see
    /// docs/WP-010-Duplicate-Flag-Persistence-Decision.md. WP-073: no longer embeds
    /// the matched invoice's raw id - see <see cref="DuplicateMatchInvoiceId"/> for
    /// that as structured data instead.
    /// </summary>
    public string? DuplicateCheckReason { get; set; }

    /// <summary>
    /// The id of the existing invoice the duplicate-detection check matched
    /// this one against, if any (WP-073) - structured data a UI can link to directly,
    /// rather than a GUID a user had to parse out of <see cref="DuplicateCheckReason"/>'s
    /// free text. Null whenever <see cref="IsPotentialDuplicate"/> is false, same
    /// reasoning as <see cref="DuplicateCheckReason"/>. When a candidate matches more
    /// than one existing invoice, this records only the first match
    /// (<see cref="DuplicateCheckReason"/> still summarizes all of them) - one
    /// navigable reference is what the UI needs, not a full list. No foreign-key
    /// constraint: this is a same-table self-reference purely for traceability/UI
    /// navigation, not a relationship EF needs to enforce referential integrity for
    /// (the matched invoice is never deleted through a path that would orphan this
    /// column in a way that matters - deleting invoices isn't supported at all yet).
    /// Deliberately NOT backfilled for invoices flagged before this field existed -
    /// the duplicate check only runs at ingestion time, not retroactively.
    /// </summary>
    public Guid? DuplicateMatchInvoiceId { get; set; }

    /// <summary>Notes/remarks recorded against this invoice.</summary>
    public ICollection<InvoiceNote> Notes { get; set; } = new List<InvoiceNote>();

    /// <summary>
    /// Document Intelligence's per-field extracted values and confidence scores
    /// for this invoice (WP-056) - one row per field WP-008's analysis reports,
    /// not columns on this entity (an open-ended, per-field set, not a fixed
    /// handful of scalars). See <see cref="InvoiceExtractedField"/>.
    /// </summary>
    public ICollection<InvoiceExtractedField> ExtractedFields { get; set; } = new List<InvoiceExtractedField>();
}
