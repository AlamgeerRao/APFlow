namespace APFlow.Domain.Enums;

/// <summary>
/// The current state of an invoice through AP Flow's ingestion pipeline. This is a
/// data-modeling concern only - the states an invoice CAN be in - not workflow logic.
/// No transition rules, guards, or approval logic are implemented here or anywhere
/// in WP-009 ("Approval workflow" and "Query workflow" are explicit WP-009
/// out-of-scope items); a future work package defines who can move an invoice
/// between which states and under what conditions.
/// Deliberately excludes a "Paid"/payment-related state: automatic payment
/// execution is out of scope for the MVP (01_Project_Context.md §8), and adding a
/// status for it now would presume a payment-tracking design that hasn't been made.
/// Deliberately excludes a "Queried"/under-query state: "Query workflow" is
/// explicit WP-009 out-of-scope, and that state only makes sense once the
/// corresponding workflow exists to use it.
/// </summary>
public enum InvoiceStatus
{
    /// <summary>Synced from the mailbox (WP-006) but not yet processed further.</summary>
    Received,

    /// <summary>PDF attachment extracted (WP-007) and analyzed (WP-008); structured data is available.</summary>
    Extracted,

    /// <summary>Awaiting human review. No review/approval UI or workflow exists yet - this is where an invoice sits until one does.</summary>
    PendingReview,

    /// <summary>Reviewed and approved.</summary>
    Approved,

    /// <summary>Reviewed and rejected.</summary>
    Rejected,
}
