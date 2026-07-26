import type { InvoiceDetail, ExtractedField, AuditEntry } from '@/types/invoiceDetail';

/**
 * Real response DTO shared by `GET /api/invoices/{id}` and WP-054's
 * `PATCH /api/invoices/{id}/status` (both return the same "WP-052 Part D"
 * InvoiceDetail shape — see status-postwb-057.md §2.2 and
 * docs/Backlog.md's WP-018 row).
 *
 * Field names confirmed 2026-07-27 by QA against the real
 * `APFlow.Application/DTOs/InvoiceDto.cs` directly: `SupplierInvoiceNumber`
 * (not `InvoiceNumber`), `GrossTotal` (not `Amount`), `Currency` (not
 * `CurrencyCode`) — camelCased on the wire per standard ASP.NET Core
 * serialization. The WP-020 delivery had these three wrong (pre-ruling
 * guesses that were never reconciled, unlike `workflowActionClient.ts`'s
 * WP-018a contract, which had already gone through this exact
 * reconciliation). Fixed here 2026-07-27 — see
 * docs/WP-020-Real-Auth-And-Api-Integration-Decisions.md §7.
 */
export interface InvoiceDetailResponseDto {
  invoice: {
    id: string;
    supplierName: string;
    supplierInvoiceNumber: string;
    invoiceDate: string;
    grossTotal: number;
    currency: string;
    status: string;
    isPotentialDuplicate: boolean;
    duplicateCheckReason: string | null;
    sourceDocumentBlobName: string;
    createdAtUtc: string;
  };
  recentAuditEntries: AuditEntry[];
  extractedFields: ExtractedField[];
}

function averageConfidence(fields: ExtractedField[]): number {
  const scored = fields.map((f) => f.confidenceScore).filter((score): score is number => score !== null);
  if (scored.length === 0) return 0;
  return scored.reduce((sum, score) => sum + score, 0) / scored.length;
}

/**
 * Maps the shared response DTO to our `InvoiceDetail` shape, minus
 * `pdfUrl` — deliberately excluded here rather than defaulted to an empty
 * string, so every caller is structurally forced to decide where the PDF
 * URL comes from rather than accidentally shipping a broken one:
 * `invoiceDetailClient.ts` fetches+blobs it (the PDF may have changed —
 * well, actually never does for an existing invoice, but the initial
 * load always needs one); `workflowActionClient.ts`'s caller reuses the
 * already-resolved `blob:` URL from the invoice already on screen, since
 * a status change never changes the underlying document and re-fetching
 * it would leak another object URL for no reason.
 */
export function mapInvoiceDetailResponse(response: InvoiceDetailResponseDto): Omit<InvoiceDetail, 'pdfUrl'> {
  return {
    id: response.invoice.id,
    supplierName: response.invoice.supplierName,
    invoiceNumber: response.invoice.supplierInvoiceNumber,
    invoiceDate: response.invoice.invoiceDate,
    amount: response.invoice.grossTotal,
    currencyCode: response.invoice.currency,
    status: response.invoice.status,
    isPotentialDuplicate: response.invoice.isPotentialDuplicate,
    duplicateCheckReason: response.invoice.duplicateCheckReason,
    sourceDocumentBlobName: response.invoice.sourceDocumentBlobName,
    receivedAt: response.invoice.createdAtUtc,
    extractedFields: response.extractedFields,
    overallConfidenceScore: averageConfidence(response.extractedFields),
    auditEntries: response.recentAuditEntries,
  };
}
