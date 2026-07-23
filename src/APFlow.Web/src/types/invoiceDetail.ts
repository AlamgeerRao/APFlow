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
  value: string;
  /** 0–1 confidence score for this specific field, as returned by Azure AI Document Intelligence. */
  confidenceScore: number;
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
  /** URL the browser can load directly to display the source PDF. */
  pdfUrl: string;
  /** Traceability field confirmed by the WP-012 report (Invoice.SourceDocumentBlobName). */
  sourceDocumentBlobName: string;
  /** ISO 8601 timestamp the invoice was first received. */
  receivedAt: string;
  extractedFields: ExtractedField[];
  /** 0–1 overall confidence score for the Document Intelligence extraction as a whole. */
  overallConfidenceScore: number;
  auditEntries: AuditEntry[];
}
