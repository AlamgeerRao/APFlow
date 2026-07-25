namespace APFlow.Domain.Common.Constants;

/// <summary>
/// Named constants for <see cref="Entities.InvoiceExtractedField.FieldKey"/>
/// values (WP-056) - the fixed set of fields
/// <c>APFlow.Application.DTOs.InvoiceExtractionResult</c> (WP-008) reports.
/// Not tenant-configurable data (unlike <see cref="InvoiceStatusCodes"/>/workflow
/// statuses) - this mirrors WP-008's own fixed record shape, so a new key here
/// only ever follows a change to that record, not a per-tenant configuration
/// decision.
/// </summary>
public static class InvoiceExtractedFieldKeys
{
    /// <summary>The invoice's vendor/supplier name.</summary>
    public const string SupplierName = "SUPPLIER_NAME";

    /// <summary>The supplier's own invoice number/ID.</summary>
    public const string SupplierInvoiceNumber = "SUPPLIER_INVOICE_NUMBER";

    /// <summary>The date the invoice was issued.</summary>
    public const string InvoiceDate = "INVOICE_DATE";

    /// <summary>The payment due date.</summary>
    public const string DueDate = "DUE_DATE";

    /// <summary>The invoice subtotal before tax.</summary>
    public const string NetAmount = "NET_AMOUNT";

    /// <summary>The tax amount.</summary>
    public const string Vat = "VAT";

    /// <summary>The total amount including tax.</summary>
    public const string GrossTotal = "GROSS_TOTAL";

    /// <summary>
    /// The invoice currency code. Reconciled from several monetary fields
    /// rather than separately extracted by Document Intelligence (see
    /// <c>InvoiceExtractionResult.Currency</c>'s own doc comment), so this row's
    /// <see cref="Entities.InvoiceExtractedField.ConfidenceScore"/> is always
    /// null - included per Chief Technical Architect ruling (2026-07-25) for
    /// consistency: one row per <c>InvoiceExtractionResult</c> field, always.
    /// </summary>
    public const string Currency = "CURRENCY";

    /// <summary>
    /// The canonical display order - the same order
    /// <c>InvoiceExtractionResult</c>'s own fields are declared in.
    /// <c>InvoiceService.GetExtractedFieldsAsync</c> sorts by this, since EF
    /// Core/SQL row order is not otherwise guaranteed.
    /// </summary>
    public static readonly string[] CanonicalOrder =
    [
        SupplierName, SupplierInvoiceNumber, InvoiceDate, DueDate, NetAmount, Vat, GrossTotal, Currency,
    ];
}
