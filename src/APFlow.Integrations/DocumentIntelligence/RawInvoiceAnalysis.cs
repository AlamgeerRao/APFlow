namespace APFlow.Integrations.DocumentIntelligence;

/// <summary>Raw text field as read directly from Document Intelligence, before any domain interpretation.</summary>
internal sealed record RawTextField(string? Value, double? Confidence);

/// <summary>Raw date field as read directly from Document Intelligence.</summary>
internal sealed record RawDateField(DateOnly? Value, double? Confidence);

/// <summary>
/// Raw monetary field as read directly from Document Intelligence. Carries its own
/// currency code because Document Intelligence attaches currency per monetary field,
/// not as a single document-level field - see
/// <see cref="APFlow.Application.DTOs.InvoiceExtractionResult"/>'s remarks on how
/// these get reconciled into one overall currency.
/// </summary>
internal sealed record RawMoneyField(decimal? Amount, string? CurrencyCode, double? Confidence);

/// <summary>
/// Plain-C# projection of everything WP-008 needs from a Document Intelligence
/// prebuilt-invoice analysis, with zero Document Intelligence SDK types. This is the
/// seam between the mechanical, unverified Graph-SDK-equivalent call
/// (<see cref="DocumentIntelligenceOperations"/>) and the tested domain logic
/// (<see cref="DocumentAnalysisService"/>) - same pattern as every prior WP's
/// IGraph*Operations seams.
/// </summary>
internal sealed record RawInvoiceAnalysis(
    RawTextField SupplierName,
    RawTextField SupplierInvoiceNumber,
    RawDateField InvoiceDate,
    RawDateField DueDate,
    RawMoneyField NetAmount,
    RawMoneyField Vat,
    RawMoneyField GrossTotal);
