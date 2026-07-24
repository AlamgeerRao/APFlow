using APFlow.Application.DTOs;

namespace APFlow.Api.Contracts;

/// <summary>
/// Response shape for <c>GET /api/invoices/{id}</c> (WP-052 Part D). Deliberately
/// composes the existing <see cref="InvoiceDto"/> (WP-009) and
/// <see cref="AuditLogDto"/> (WP-013) as-is, rather than inventing new field
/// names for data those DTOs already shape - per this task's own "do not
/// introduce a third, incompatible naming scheme" instruction. Field name/casing
/// was NOT cross-checked against a WP-015 fixture: no such fixture was available
/// in this delivery's working context - see docs/WP-052-Pipeline-And-Api-Hardening-Decisions.md.
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
/// <param name="ExtractionConfidenceNote">
/// NOT a real confidence dataset - a fixed, documented placeholder. WP-009's own
/// entity doc comment explicitly excluded persisting WP-008's per-field
/// extraction confidence scores, reasoning that doing so should wait for "a real
/// requirement" for it - this task is precisely that requirement arriving, but
/// implementing it means extending what the ingestion pipeline PERSISTS (a
/// schema change to <c>Invoice</c> capturing confidence per field), which is
/// outside Part D's own scope (an API endpoint over EXISTING data). Confidence
/// data is therefore not available to return here for any invoice, past or
/// future, until that persistence gap is closed by a work package scoped to
/// close it. See docs/WP-052-Pipeline-And-Api-Hardening-Decisions.md.
/// </param>
public sealed record InvoiceDetailResponse(
    InvoiceDto Invoice,
    IReadOnlyList<AuditLogDto> RecentAuditEntries,
    string ExtractionConfidenceNote);
