using APFlow.Application.DTOs;

namespace APFlow.Api.Contracts;

/// <summary>
/// Response shape for <c>GET /api/invoices/{id}</c> (WP-052 Part D; real
/// extraction confidence added WP-056). Deliberately composes the existing
/// <see cref="InvoiceDto"/> (WP-009), <see cref="AuditLogDto"/> (WP-013), and
/// <see cref="InvoiceExtractedFieldDto"/> (WP-056) as-is, rather than inventing
/// new field names for data those DTOs already shape - per WP-052 Part D's own
/// "do not introduce a third, incompatible naming scheme" instruction, which
/// this later addition follows too. Field name/casing was NOT cross-checked
/// against a WP-015 fixture: no such fixture was available in this delivery's
/// working context - see docs/WP-052-Pipeline-And-Api-Hardening-Decisions.md.
/// JSON serialization uses ASP.NET Core's default camelCase policy for
/// controller-based APIs (no custom <c>JsonSerializerOptions</c> configured), so
/// e.g. <see cref="InvoiceDto.SupplierInvoiceNumber"/> serializes as
/// <c>supplierInvoiceNumber</c>.
/// </summary>
/// <param name="Invoice">
/// Canonical invoice/supplier fields (WP-009), including
/// <see cref="InvoiceDto.IsPotentialDuplicate"/>/<see cref="InvoiceDto.DuplicateCheckReason"/>
/// (WP-048) and <see cref="InvoiceDto.SourceDocumentBlobName"/>.
/// </param>
/// <param name="RecentAuditEntries">
/// The most recent audit log entries for this invoice (WP-013, extended by
/// WP-052 Part C to also cover creation, deletion, and note additions) - up to
/// <see cref="APFlow.Application.DTOs.AuditLogQueryParameters"/>'s default page
/// size (25) entries, most recent first.
/// </param>
/// <param name="ExtractedFields">
/// Document Intelligence's per-field extraction confidence data (WP-056) -
/// replaces the WP-052 Part D placeholder note (<c>ExtractionConfidenceNote</c>)
/// now that the ingestion pipeline actually persists it (see
/// <c>InvoiceExtractedField</c>). Empty, not missing/null, for an invoice
/// created before WP-056 or not processed via the WP-012 pipeline at all (e.g.
/// created manually) - the absence of extraction data is a normal, expected
/// state for such an invoice, not an error condition for this endpoint to
/// report.
/// </param>
public sealed record InvoiceDetailResponse(
    InvoiceDto Invoice,
    IReadOnlyList<AuditLogDto> RecentAuditEntries,
    IReadOnlyList<InvoiceExtractedFieldDto> ExtractedFields);
