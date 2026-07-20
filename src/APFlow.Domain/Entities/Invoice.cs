using APFlow.Domain.Enums;

namespace APFlow.Domain.Entities;

/// <summary>
/// A supplier invoice moving through AP Flow's ingestion pipeline. Fields mirror
/// WP-008's <c>InvoiceExtractionResult</c> - a natural persistence target for that
/// service's output - but this entity has no dependency on WP-008 and can be
/// created/edited independent of it (see <c>IInvoiceService</c>).
/// Deliberately excludes per-field confidence scores: storing WP-008's extraction
/// confidence per field would meaningfully grow this entity's shape around a
/// specific future consumption pattern (e.g. a review UI flagging low-confidence
/// fields) that hasn't been designed yet. Add if/when that's a real requirement.
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

    /// <summary>Where this invoice currently stands - see <see cref="InvoiceStatus"/>.</summary>
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Received;

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
    /// (not a foreign key, not validated against Blob Storage) - with one added
    /// role: WP-012's pipeline also uses this as its idempotency key (a given
    /// email+attachment combination maps to a deterministic logical blob name, so a
    /// re-run that finds an existing invoice with the same value skips reprocessing
    /// it rather than creating a duplicate). Null for invoices created by other
    /// means (e.g. manual entry, once that exists).
    /// </summary>
    public string? SourceDocumentBlobName { get; set; }

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
    /// docs/WP-010-Duplicate-Flag-Persistence-Decision.md.
    /// </summary>
    public string? DuplicateCheckReason { get; set; }

    /// <summary>Notes/remarks recorded against this invoice.</summary>
    public ICollection<InvoiceNote> Notes { get; set; } = new List<InvoiceNote>();
}
