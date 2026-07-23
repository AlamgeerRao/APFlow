namespace APFlow.Domain.Common.Constants;

/// <summary>
/// Named constants for invoice status codes (WP-050). These values must exactly
/// match the corresponding <c>StatusReference.Code</c> rows - see
/// docs/06_Domain_Reference_Data.md §2 (SA-007 E-14 / SA-002 §5) for the canonical
/// list this mirrors. <see cref="APFlow.Domain.Entities.Invoice.Status"/> is a
/// plain string (not this class, and not an enum) precisely because status values
/// are tenant-configurable data (WP-050 retires the old <c>InvoiceStatus</c> enum
/// for exactly this reason - a shared enum cannot represent a tenant-specific
/// status like GB Skips' <see cref="CheckedReadyToApprove"/>). This class exists
/// only so code working with the known, platform-default codes can reference a
/// named constant instead of a raw string literal - it is not the source of truth
/// (the seeded <c>StatusReference</c> rows are), and does not enumerate
/// tenant-specific codes exhaustively for every tenant that might ever exist.
/// </summary>
public static class InvoiceStatusCodes
{
    // Platform default (docs/06_Domain_Reference_Data.md §2, unchanged from SA-002 §5).

    /// <summary>Synced from the mailbox but not yet processed further.</summary>
    public const string Received = "RECEIVED";

    /// <summary>Being actively processed by the AP pipeline or a reviewer.</summary>
    public const string Processing = "PROCESSING";

    /// <summary>Flagged by duplicate detection (WP-010) as a likely duplicate, pending resolution.</summary>
    public const string DuplicateSuspected = "DUPLICATE_SUSPECTED";

    /// <summary>Awaiting a reviewer's initial check.</summary>
    public const string AwaitingReview = "AWAITING_REVIEW";

    /// <summary>A query has been identified and needs to be raised with the supplier.</summary>
    public const string NeedsQuery = "NEEDS_QUERY";

    /// <summary>A query has been raised with the supplier and is awaiting their response.</summary>
    public const string QueryRaised = "QUERY_RAISED";

    /// <summary>Awaiting the supplier's response to a raised query.</summary>
    public const string AwaitingSupplierResponse = "AWAITING_SUPPLIER_RESPONSE";

    /// <summary>Approved for payment by whoever holds approval authority.</summary>
    public const string Approved = "APPROVED";

    /// <summary>Rejected - will not be paid as submitted.</summary>
    public const string Rejected = "REJECTED";

    /// <summary>Cancelled - withdrawn from the workflow entirely.</summary>
    public const string Cancelled = "CANCELLED";

    /// <summary>Approved and queued for payment.</summary>
    public const string ReadyForPayment = "READY_FOR_PAYMENT";

    /// <summary>Payment has been made.</summary>
    public const string Paid = "PAID";

    /// <summary>Terminal - the invoice's lifecycle is complete and it is retained for record-keeping only.</summary>
    public const string Archived = "ARCHIVED";

    /// <summary>
    /// WP-008/WP-012's pipeline-produced status once PDF extraction and Document
    /// Intelligence analysis are complete. Not part of SA-002 §5's baseline
    /// catalogue in docs/06_Domain_Reference_Data.md - that document only covers
    /// the review/approval lifecycle, not the ingestion stage before an invoice is
    /// first surfaced to a reviewer. Retained from the old <c>InvoiceStatus</c>
    /// enum (WP-009/WP-012) unchanged; flagged here for whoever reconciles this
    /// against SA-002/SA-007 formally, since it predates 06_Domain_Reference_Data.md
    /// and was never explicitly cross-checked against it until now.
    /// </summary>
    public const string Extracted = "EXTRACTED";

    // GB Skips tenant-specific additions (docs/06_Domain_Reference_Data.md §2) -
    // valid status VALUES, confirmed by that document. Their TRANSITIONS are NOT
    // yet confirmed - see docs/WP-050-Workflow-Engine-Decisions.md. Do not use
    // these as if they were reachable via any enforced transition yet.
    /// <summary>GB Skips tenant-specific: reviewed and checked, awaiting Full/Approver sign-off. Transitions not yet confirmed - see docs/WP-050-Workflow-Engine-Decisions.md.</summary>
    public const string CheckedReadyToApprove = "CHECKED_READY_TO_APPROVE";

    /// <summary>GB Skips tenant-specific: escalated for Febina's review. Transitions not yet confirmed - see docs/WP-050-Workflow-Engine-Decisions.md.</summary>
    public const string NeedsReviewFebina = "NEEDS_REVIEW_FEBINA";
}
