import type { InvoiceDetail } from '@/types/invoiceDetail';
import { invoiceFixtures } from '@/api/fixtures/invoices.fixture';

/**
 * Detail-only fields layered onto each WP-015 list fixture to build a full
 * InvoiceDetail. Only a handful of invoices have detail fixtures — enough
 * to exercise every screen state (high/medium/low confidence, duplicate
 * warning, multi-entry audit trail) without duplicating all fixture data.
 */
const detailExtrasById: Record<
  string,
  Pick<InvoiceDetail, 'pdfUrl' | 'sourceDocumentBlobName' | 'receivedAt' | 'extractedFields' | 'overallConfidenceScore' | 'auditEntries'>
> = {
  'inv-pd-001': {
    pdfUrl: '/sample-invoices/inv-pd-001.pdf',
    sourceDocumentBlobName: 'platform-default/2026/07/inv-pd-001.pdf',
    receivedAt: '2026-07-01T08:12:00Z',
    overallConfidenceScore: 0.96,
    extractedFields: [
      { fieldKey: 'supplierName', label: 'Supplier Name', value: 'Northwind Traders Ltd', confidenceScore: 0.98 },
      { fieldKey: 'invoiceNumber', label: 'Invoice Number', value: 'NW-1001', confidenceScore: 0.99 },
      { fieldKey: 'invoiceDate', label: 'Invoice Date', value: '01 Jul 2026', confidenceScore: 0.95 },
      { fieldKey: 'totalAmount', label: 'Total Amount', value: '£1,240.50', confidenceScore: 0.97 },
      { fieldKey: 'vatNumber', label: 'VAT Number', value: 'GB123456789', confidenceScore: 0.91 },
    ],
    auditEntries: [
      { id: 'audit-1', timestamp: '2026-07-01T08:12:00Z', actor: 'System', action: 'Received', description: 'Invoice received via email ingestion.' },
      { id: 'audit-2', timestamp: '2026-07-01T08:13:10Z', actor: 'System', action: 'Extracted', description: 'Document Intelligence extraction completed.' },
      { id: 'audit-3', timestamp: '2026-07-01T08:13:15Z', actor: 'System', action: 'Duplicate check', description: 'No duplicate found.' },
    ],
  },
  'inv-pd-002': {
    pdfUrl: '/sample-invoices/inv-pd-002.pdf',
    sourceDocumentBlobName: 'platform-default/2026/07/inv-pd-002.pdf',
    receivedAt: '2026-07-03T09:40:00Z',
    overallConfidenceScore: 0.71,
    extractedFields: [
      { fieldKey: 'supplierName', label: 'Supplier Name', value: 'Contoso Supplies', confidenceScore: 0.88 },
      { fieldKey: 'invoiceNumber', label: 'Invoice Number', value: 'CS-2045', confidenceScore: 0.9 },
      { fieldKey: 'invoiceDate', label: 'Invoice Date', value: '03 Jul 2026', confidenceScore: 0.62 },
      { fieldKey: 'totalAmount', label: 'Total Amount', value: '£875.00', confidenceScore: 0.65 },
      { fieldKey: 'vatNumber', label: 'VAT Number', value: 'Not detected', confidenceScore: 0.31 },
    ],
    auditEntries: [
      { id: 'audit-1', timestamp: '2026-07-03T09:40:00Z', actor: 'System', action: 'Received', description: 'Invoice received via email ingestion.' },
      { id: 'audit-2', timestamp: '2026-07-03T09:41:22Z', actor: 'System', action: 'Extracted', description: 'Document Intelligence extraction completed with low confidence on scanned fields.' },
      { id: 'audit-3', timestamp: '2026-07-03T09:41:30Z', actor: 'System', action: 'Duplicate check', description: 'Flagged as a potential duplicate — all fields matched an existing invoice from the same supplier.' },
    ],
  },
  'inv-gb-001': {
    pdfUrl: '/sample-invoices/inv-gb-001.pdf',
    sourceDocumentBlobName: 'gb-skips/2026/07/inv-gb-001.pdf',
    receivedAt: '2026-07-02T07:55:00Z',
    overallConfidenceScore: 0.99,
    extractedFields: [
      { fieldKey: 'supplierName', label: 'Supplier Name', value: 'Yorkshire Skip Supplies', confidenceScore: 0.99 },
      { fieldKey: 'invoiceNumber', label: 'Invoice Number', value: 'YSS-2201', confidenceScore: 0.99 },
      { fieldKey: 'invoiceDate', label: 'Invoice Date', value: '02 Jul 2026', confidenceScore: 0.98 },
      { fieldKey: 'totalAmount', label: 'Total Amount', value: '£640.00', confidenceScore: 0.99 },
    ],
    auditEntries: [
      { id: 'audit-1', timestamp: '2026-07-02T07:55:00Z', actor: 'System', action: 'Received', description: 'Invoice received via email ingestion.' },
      { id: 'audit-2', timestamp: '2026-07-02T07:56:02Z', actor: 'System', action: 'Extracted', description: 'Document Intelligence extraction completed.' },
    ],
  },
  'inv-gb-002': {
    pdfUrl: '/sample-invoices/inv-gb-002.pdf',
    sourceDocumentBlobName: 'gb-skips/2026/07/inv-gb-002.pdf',
    receivedAt: '2026-07-05T10:05:00Z',
    overallConfidenceScore: 0.84,
    extractedFields: [
      { fieldKey: 'supplierName', label: 'Supplier Name', value: 'Yorkshire Skip Supplies', confidenceScore: 0.93 },
      { fieldKey: 'invoiceNumber', label: 'Invoice Number', value: 'YSS-2202', confidenceScore: 0.95 },
      { fieldKey: 'invoiceDate', label: 'Invoice Date', value: '05 Jul 2026', confidenceScore: 0.9 },
      { fieldKey: 'totalAmount', label: 'Total Amount', value: '£640.00', confidenceScore: 0.6 },
    ],
    auditEntries: [
      { id: 'audit-1', timestamp: '2026-07-05T10:05:00Z', actor: 'System', action: 'Received', description: 'Invoice received via email ingestion.' },
      { id: 'audit-2', timestamp: '2026-07-05T10:06:40Z', actor: 'System', action: 'Extracted', description: 'Document Intelligence extraction completed.' },
      { id: 'audit-3', timestamp: '2026-07-05T10:06:45Z', actor: 'System', action: 'Duplicate check', description: 'Flagged as a potential duplicate — amount and supplier matched an existing invoice within the same week.' },
    ],
  },
};

/** Full InvoiceDetail fixtures: WP-015 list fixture fields + this file's detail-only additions. */
export const invoiceDetailFixtures: (InvoiceDetail & { tenantId: string })[] = invoiceFixtures
  .filter((invoice) => invoice.id in detailExtrasById)
  .map((invoice) => ({ ...invoice, ...detailExtrasById[invoice.id] }));
