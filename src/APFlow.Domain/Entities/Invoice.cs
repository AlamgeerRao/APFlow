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
/// Deliberately excludes any Blob Storage reference (e.g. a source PDF URI):
/// WP-005 was explicit that Blob Storage is "not connected to invoice processing",
/// and WP-009's own task list doesn't call for it either - inventing a storage
/// linkage now would presume a strategy (one blob per invoice? per attachment?)
/// nobody has decided.
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

    /// <summary>Notes/remarks recorded against this invoice.</summary>
    public ICollection<InvoiceNote> Notes { get; set; } = new List<InvoiceNote>();
}
