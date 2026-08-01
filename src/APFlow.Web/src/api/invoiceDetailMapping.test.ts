import { describe, expect, it } from 'vitest';
import { mapInvoiceDetailResponse, type InvoiceDetailResponseDto } from '@/api/invoiceDetailMapping';

function responseWith(recentAuditEntries: InvoiceDetailResponseDto['recentAuditEntries']): InvoiceDetailResponseDto {
  return {
    invoice: {
      id: 'inv-1',
      supplierName: 'Northwind Traders Ltd',
      supplierInvoiceNumber: 'NW-1001',
      invoiceDate: '2026-07-01',
      grossTotal: 1240.5,
      currency: 'GBP',
      status: 'AWAITING_REVIEW',
      isPotentialDuplicate: false,
      duplicateCheckReason: null,
      duplicateMatchInvoiceId: null,
      sourceDocumentBlobName: 'blob-1',
      createdAtUtc: '2026-07-01T08:00:00Z',
    },
    recentAuditEntries,
    extractedFields: [],
  };
}

// WP-072 follow-up: recentAuditEntries was, until now, cast directly to the
// frontend's AuditEntry shape (timestamp/actor/description) with no mapping at
// all - the real wire shape (AuditLogDto.cs) has performedAtUtc/
// performedByUserId/previousValue/newValue instead, none of which exist under
// those names on the real object. Every real entry's timestamp/actor/
// description were `undefined`, which crashed AuditSummaryPanel's date
// formatter (RangeError: Invalid time value) on any invoice with real audit
// history - confirmed live via GET /api/invoices/{id} against the deployed API.
describe('mapInvoiceDetailResponse - audit entry mapping', () => {
  it('maps performedAtUtc to timestamp', () => {
    const result = mapInvoiceDetailResponse(
      responseWith([
        {
          id: 'audit-1',
          performedByUserId: 'user-1',
          action: 'DocumentViewed',
          entityName: 'Invoice',
          entityId: 'inv-1',
          previousValue: null,
          newValue: null,
          performedAtUtc: '2026-08-01T14:45:38.663Z',
        },
      ]),
    );

    expect(result.auditEntries[0].timestamp).toBe('2026-08-01T14:45:38.663Z');
  });

  it('maps performedByUserId to actor, falling back to "system" when null', () => {
    const result = mapInvoiceDetailResponse(
      responseWith([
        {
          id: 'audit-1',
          performedByUserId: null,
          action: 'InvoiceCreated',
          entityName: 'Invoice',
          entityId: 'inv-1',
          previousValue: null,
          newValue: null,
          performedAtUtc: '2026-08-01T14:45:38.663Z',
        },
      ]),
    );

    expect(result.auditEntries[0].actor).toBe('system');
  });

  it('describes an InvoiceStatusChanged entry as "from → to"', () => {
    const result = mapInvoiceDetailResponse(
      responseWith([
        {
          id: 'audit-1',
          performedByUserId: 'user-1',
          action: 'InvoiceStatusChanged',
          entityName: 'Invoice',
          entityId: 'inv-1',
          previousValue: 'AWAITING_REVIEW',
          newValue: 'CHECKED_READY_TO_APPROVE',
          performedAtUtc: '2026-08-01T00:19:58.79Z',
        },
      ]),
    );

    expect(result.auditEntries[0].description).toBe('AWAITING_REVIEW → CHECKED_READY_TO_APPROVE');
  });

  it('describes a NoteAdded entry using its newValue as the note content', () => {
    const result = mapInvoiceDetailResponse(
      responseWith([
        {
          id: 'audit-1',
          performedByUserId: 'user-1',
          action: 'NoteAdded',
          entityName: 'Invoice',
          entityId: 'inv-1',
          previousValue: null,
          newValue: 'Approved by test-approver',
          performedAtUtc: '2026-08-01T00:20:58.78Z',
        },
      ]),
    );

    expect(result.auditEntries[0].description).toBe('Approved by test-approver');
  });

  it('describes a DocumentViewed entry with a fixed message', () => {
    const result = mapInvoiceDetailResponse(
      responseWith([
        {
          id: 'audit-1',
          performedByUserId: 'user-1',
          action: 'DocumentViewed',
          entityName: 'Invoice',
          entityId: 'inv-1',
          previousValue: null,
          newValue: null,
          performedAtUtc: '2026-08-01T14:45:38.663Z',
        },
      ]),
    );

    expect(result.auditEntries[0].description).toBe('Document viewed');
  });
});
