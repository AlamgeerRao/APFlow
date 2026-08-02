import type { InvoiceListItem } from '@/types/invoice';

/**
 * Client-side shape for the full Invoice Review Screen (WP-016).
 *
 * PROVISIONAL CONTRACT: this extends WP-015's InvoiceListItem with fields
 * this screen additionally needs. None of these additive fields have a
 * confirmed backend contract yet (WP-008 Document Intelligence extraction
 * shape, WP-013 audit log shape, and the PDF retrieval mechanism for
 * Invoice.SourceDocumentBlobName are all backend work already built, but
 * their contracts weren't available when WP-016 was built). See
 * docs/WP-016-Invoice-Review-Decisions.md for the proposed contract,
 * flagged there for sign-off.
 */

/** A single field Document Intelligence extracted from the source PDF, with its own confidence score. */
export interface ExtractedField {
  fieldKey: string;
  label: string;
  /**
   * WP-077: or null if Document Intelligence did not extract this field for
   * this invoice (backend `string? Value` on `InvoiceExtractedField` - a
   * normal, expected outcome per that entity's own doc comment, independent
   * of whether `confidenceScore` below is also null).
   */
  value: string | null;
  /**
   * 0–1 confidence score for this specific field, as returned by Azure AI
   * Document Intelligence — or null. Confirmed null for the `Currency`
   * field specifically, per WP-056's Chief Technical Architect ruling
   * (Currency is included as its own extracted-field row, with an
   * always-null ConfidenceScore, not excluded) — see
   * status-postwb-057.md §2.4.
   */
  confidenceScore: number | null;
}

/** A single entry in the invoice's audit/activity history (WP-013). */
export interface AuditEntry {
  id: string;
  /** ISO 8601 timestamp. */
  timestamp: string;
  actor: string;
  action: string;
  description: string;
}

export interface InvoiceDetail extends InvoiceListItem {
  /**
   * URL the browser can load directly to display the source PDF, or null if
   * it couldn't be retrieved (e.g. no document was ever attached to this
   * invoice, or Blob Storage returned an error) — a real, expected state,
   * not just a loading placeholder. `InvoicePdfViewer` renders its own
   * "unavailable" message for this case rather than the page failing to
   * load entirely.
   */
  pdfUrl: string | null;
  /**
   * Traceability field confirmed by the WP-012 report (Invoice.SourceDocumentBlobName).
   * WP-077: or null for an invoice not created via the email pipeline (backend
   * `string? SourceDocumentBlobName`) - not rendered directly anywhere today
   * (the PDF itself is fetched via a fixed `/download` endpoint, not this
   * field), but typed accurately regardless.
   */
  sourceDocumentBlobName: string | null;
  /** ISO 8601 timestamp the invoice was first received. */
  receivedAt: string;
  extractedFields: ExtractedField[];
  /**
   * 0–1 overall confidence score for the Document Intelligence extraction
   * as a whole. NOT a confirmed server field — status-postwb-057.md's
   * live API surface only documents per-field `extractedFields` data
   * (WP-056), no aggregate score. The real client computes this
   * client-side (mean of non-null field scores) rather than expecting the
   * server to supply it; the fixture client supplies it directly. See
   * docs/WP-020-Real-Auth-And-Api-Integration-Decisions.md §3.
   */
  overallConfidenceScore: number;
  auditEntries: AuditEntry[];
}
