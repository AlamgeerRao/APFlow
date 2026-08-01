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
    /** Genuinely nullable on the wire - see InvoiceListItem.invoiceDate (WP-072). */
    invoiceDate: string | null;
    grossTotal: number;
    currency: string;
    status: string;
    isPotentialDuplicate: boolean;
    duplicateCheckReason: string | null;
    /** The matched existing invoice's id, if any (WP-073). */
    duplicateMatchInvoiceId: string | null;
    sourceDocumentBlobName: string;
    createdAtUtc: string;
  };
  recentAuditEntries: AuditLogResponseDto[];
  extractedFields: ExtractedField[];
}

/**
 * Real wire shape of one entry in `recentAuditEntries` -
 * `APFlow.Application.DTOs.AuditLogDto` (`AuditLogDto.cs`). WP-072: this was never
 * reconciled against real data before now - `recentAuditEntries` was typed as
 * directly `AuditEntry[]` (this file's own frontend-only shape: `timestamp`/
 * `actor`/`description`), and every real entry actually has `performedAtUtc`/
 * `performedByUserId`/`previousValue`/`newValue` instead, none of which exist on
 * the real object under those names. `entry.timestamp` was always `undefined` for
 * real (non-fixture) data, and `AuditSummaryPanel`'s local `formatTimestamp` threw
 * `RangeError: Invalid time value` on `new Date(undefined)` - crashing the whole
 * Review screen for any invoice with real audit history (i.e. almost all of them).
 * `mapAuditEntry` below is the reconciliation this contract never got.
 */
export interface AuditLogResponseDto {
  id: string;
  performedByUserId: string | null;
  action: string;
  entityName: string;
  entityId: string;
  previousValue: string | null;
  newValue: string | null;
  performedAtUtc: string;
}

/**
 * Synthesizes a human-readable description from the real audit fields - there is
 * no single "description" field on the wire, only `action` plus the
 * before/after `previousValue`/`newValue` pair. Covers every real
 * `AuditActions` constant (`AuditActions.cs`); an unrecognized future action
 * falls back to whichever of `newValue`/`previousValue` is present, rather than
 * an empty string.
 */
function describeAuditEntry(entry: AuditLogResponseDto): string {
  switch (entry.action) {
    case 'InvoiceStatusChanged':
      return `${entry.previousValue ?? '—'} → ${entry.newValue ?? '—'}`;
    case 'NoteAdded':
      return entry.newValue ?? '';
    case 'DocumentViewed':
      return 'Document viewed';
    case 'InvoiceCreated':
      return 'Invoice created';
    case 'InvoiceDeleted':
      return 'Invoice deleted';
    default:
      return entry.newValue ?? entry.previousValue ?? '';
  }
}

function mapAuditEntry(entry: AuditLogResponseDto): AuditEntry {
  return {
    id: entry.id,
    timestamp: entry.performedAtUtc,
    actor: entry.performedByUserId ?? 'system',
    action: entry.action,
    description: describeAuditEntry(entry),
  };
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
    duplicateMatchInvoiceId: response.invoice.duplicateMatchInvoiceId,
    sourceDocumentBlobName: response.invoice.sourceDocumentBlobName,
    receivedAt: response.invoice.createdAtUtc,
    extractedFields: response.extractedFields,
    overallConfidenceScore: averageConfidence(response.extractedFields),
    auditEntries: response.recentAuditEntries.map(mapAuditEntry),
  };
}
