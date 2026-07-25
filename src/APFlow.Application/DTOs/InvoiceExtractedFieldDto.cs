namespace APFlow.Application.DTOs;

/// <summary>
/// Read shape for one Document Intelligence-extracted field and its confidence
/// score (WP-056). See <see cref="APFlow.Domain.Entities.InvoiceExtractedField"/>
/// for the persisted shape this mirrors.
/// </summary>
public sealed record InvoiceExtractedFieldDto(
    string FieldKey,
    string Label,
    string? Value,
    double? ConfidenceScore);
