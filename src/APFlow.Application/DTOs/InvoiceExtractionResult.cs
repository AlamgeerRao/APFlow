namespace APFlow.Application.DTOs;

/// <summary>
/// Pairs an extracted value with its Document Intelligence confidence score
/// (0.0-1.0). <typeparamref name="T"/> should itself be a nullable type at the call
/// site (e.g. <c>string?</c>, <c>DateOnly?</c>, <c>decimal?</c>) - both a null value
/// and a null confidence are normal, expected outcomes (a field may legitimately not
/// be found on a given invoice), not errors.
/// NOTE: this does NOT use the "T? Value" pattern on an unconstrained T. Without a
/// `where T : struct` constraint, "T?" on an unconstrained generic parameter is a
/// nullable-reference-type annotation only - it does not become Nullable&lt;T&gt; for
/// value types the way a concrete "DateOnly?" does. Confirmed by a real compile
/// failure during WP-008 verification. Making T itself the nullable type at each
/// usage site (see InvoiceExtractionResult below) is the correct, uniform fix for
/// both reference and value types.
/// </summary>
public sealed record ExtractedField<T>(T Value, double? Confidence);

/// <summary>
/// Strongly typed result of analyzing one invoice PDF via Azure AI Document
/// Intelligence (see APFlow.Integrations.DocumentIntelligence.DocumentAnalysisService).
/// Deliberately raw extraction only - no validation of the extracted values, no
/// database persistence, no approval workflow, no UI concerns. What a caller does
/// with a low-confidence or partially-null result is a future work package's decision.
/// </summary>
/// <param name="SupplierName">The invoice's vendor/supplier name.</param>
/// <param name="SupplierInvoiceNumber">The supplier's own invoice number/ID.</param>
/// <param name="InvoiceDate">The date the invoice was issued.</param>
/// <param name="DueDate">The payment due date.</param>
/// <param name="Currency">
/// The invoice's currency code (e.g. "GBP", "USD"), derived from whichever monetary
/// field reported one first (gross total, then net amount, then VAT) - Document
/// Intelligence attaches a currency code per monetary field, not as one standalone
/// field, so this is a best-effort reconciliation, not a separately-confidence-scored
/// extraction in its own right.
/// </param>
/// <param name="NetAmount">The invoice subtotal before tax.</param>
/// <param name="Vat">The tax amount.</param>
/// <param name="GrossTotal">The total amount including tax.</param>
public sealed record InvoiceExtractionResult(
    ExtractedField<string?> SupplierName,
    ExtractedField<string?> SupplierInvoiceNumber,
    ExtractedField<DateOnly?> InvoiceDate,
    ExtractedField<DateOnly?> DueDate,
    string? Currency,
    ExtractedField<decimal?> NetAmount,
    ExtractedField<decimal?> Vat,
    ExtractedField<decimal?> GrossTotal);
