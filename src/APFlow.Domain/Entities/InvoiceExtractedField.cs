namespace APFlow.Domain.Entities;

/// <summary>
/// One Document Intelligence-extracted field's value and confidence score,
/// persisted per invoice (WP-056) - closes the gap flagged in WP-052 Part D:
/// <c>InvoiceExtractionResult</c>'s per-field <c>ExtractedField&lt;T&gt;.Confidence</c>
/// (WP-008) was computed at ingestion time but discarded once the raw values
/// were copied onto <see cref="Invoice"/>'s own scalar columns - the API could
/// only ever return a placeholder note, never real confidence data.
/// A one-to-many child entity, not columns on <see cref="Invoice"/> itself (per
/// this task's explicit instruction) - Document Intelligence extracts an
/// open-ended, per-field set of (value, confidence) pairs, not a fixed handful
/// of scalars, so this shape can represent however many fields a given analysis
/// reports without growing <see cref="Invoice"/>'s own column list.
/// TenantEntity-derived (not just linked via <see cref="InvoiceId"/>)
/// deliberately - same defense-in-depth reasoning as <see cref="InvoiceNote"/>.
/// </summary>
public sealed class InvoiceExtractedField : TenantEntity
{
    /// <summary>The invoice this extracted field belongs to.</summary>
    public Guid InvoiceId { get; set; }

    /// <summary>Navigation property to the parent invoice.</summary>
    public Invoice? Invoice { get; set; }

    /// <summary>
    /// Machine-readable field identifier, e.g. "SUPPLIER_NAME" - see
    /// <see cref="APFlow.Domain.Common.Constants.InvoiceExtractedFieldKeys"/> for
    /// the fixed set this pipeline currently writes.
    /// </summary>
    public string FieldKey { get; set; } = string.Empty;

    /// <summary>Human-readable label, e.g. "Supplier Name".</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// The extracted value, formatted as display text (the underlying
    /// <c>ExtractedField&lt;T&gt;</c> is typed per field - string, date, or
    /// decimal - but this column is intentionally one uniform text
    /// representation, since nothing here needs to compute on the value, only
    /// display it). Null if Document Intelligence did not extract this field for
    /// this invoice - a normal, expected outcome
    /// (<c>ExtractedField&lt;T&gt;</c>'s own doc comment), not an error.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Document Intelligence's confidence score for this field (0.0-1.0), or
    /// null if not reported - independent of whether <see cref="Value"/> itself
    /// is null (see <c>ExtractedField&lt;T&gt;.Confidence</c>'s own doc comment).
    /// </summary>
    public double? ConfidenceScore { get; set; }
}
