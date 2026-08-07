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

// WP-077: every one of these invoice fields is genuinely nullable on the wire
// (InvoiceDto.cs's SupplierName/SupplierInvoiceNumber/GrossTotal/Currency/
// SourceDocumentBlobName are all string?/decimal?) - this DTO previously
// declared them all non-nullable, which mapInvoiceDetailResponse's straight
// pass-through would have silently forwarded regardless, but a type this
// inaccurate risks a future call site trusting it and calling a method that
// throws on null, the same way formatCurrency's old signature did.
function invoiceResponseWith(overrides: Partial<InvoiceDetailResponseDto['invoice']>): InvoiceDetailResponseDto {
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
      ...overrides,
    },
    recentAuditEntries: [],
    extractedFields: [],
  };
}

describe('mapInvoiceDetailResponse - null field mapping', () => {
  it('maps a null supplierName through without throwing', () => {
    const result = mapInvoiceDetailResponse(invoiceResponseWith({ supplierName: null }));
    expect(result.supplierName).toBeNull();
  });

  it('maps a null supplierInvoiceNumber through without throwing', () => {
    const result = mapInvoiceDetailResponse(invoiceResponseWith({ supplierInvoiceNumber: null }));
    expect(result.invoiceNumber).toBeNull();
  });

  it('maps a null grossTotal through without throwing', () => {
    const result = mapInvoiceDetailResponse(invoiceResponseWith({ grossTotal: null }));
    expect(result.amount).toBeNull();
  });

  it('maps a null currency through without throwing', () => {
    const result = mapInvoiceDetailResponse(invoiceResponseWith({ currency: null }));
    expect(result.currencyCode).toBeNull();
  });

  it('maps a null sourceDocumentBlobName through without throwing', () => {
    const result = mapInvoiceDetailResponse(invoiceResponseWith({ sourceDocumentBlobName: null }));
    expect(result.sourceDocumentBlobName).toBeNull();
  });
});

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
          performedByDisplayName: 'Priya Shah',
        },
      ]),
    );

    expect(result.auditEntries[0].timestamp).toBe('2026-08-01T14:45:38.663Z');
  });

  it('falls back to "System" when performedByUserId is null and no display name was captured', () => {
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
          performedByDisplayName: null,
        },
      ]),
    );

    expect(result.auditEntries[0].actor).toBe('System');
  });

  it('prefers performedByDisplayName over performedByUserId when both are present (WP-092)', () => {
    const result = mapInvoiceDetailResponse(
      responseWith([
        {
          id: 'audit-1',
          performedByUserId: 'a3f1c2d4-...',
          action: 'InvoiceCreated',
          entityName: 'Invoice',
          entityId: 'inv-1',
          previousValue: null,
          newValue: null,
          performedAtUtc: '2026-08-01T14:45:38.663Z',
          performedByDisplayName: 'Febina',
        },
      ]),
    );

    expect(result.auditEntries[0].actor).toBe('Febina');
  });

  it('falls back to "Unknown user" for a historical row with a real actor but no captured display name (WP-092)', () => {
    const result = mapInvoiceDetailResponse(
      responseWith([
        {
          id: 'audit-1',
          performedByUserId: 'a3f1c2d4-...',
          action: 'InvoiceCreated',
          entityName: 'Invoice',
          entityId: 'inv-1',
          previousValue: null,
          newValue: null,
          performedAtUtc: '2026-08-01T14:45:38.663Z',
          performedByDisplayName: null,
        },
      ]),
    );

    expect(result.auditEntries[0].actor).toBe('Unknown user');
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
          performedByDisplayName: 'Priya Shah',
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
          performedByDisplayName: 'Priya Shah',
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
          performedByDisplayName: 'Priya Shah',
        },
      ]),
    );

    expect(result.auditEntries[0].description).toBe('Document viewed');
  });
});
