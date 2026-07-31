import { describe, expect, it, vi, beforeEach } from 'vitest';
import { FixtureInvoiceDetailClient, HttpInvoiceDetailClient } from '@/api/invoiceDetailClient';
import { httpClient, ApiError } from '@/api/httpClient';

vi.mock('@/api/httpClient', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/httpClient')>();
  return { ...actual, httpClient: { ...actual.httpClient, get: vi.fn(), getBlob: vi.fn() } };
});

describe('FixtureInvoiceDetailClient', () => {
  const client = new FixtureInvoiceDetailClient();

  it('returns full detail for a known invoice in the correct tenant', async () => {
    const result = await client.getInvoiceDetail('platform-default', 'inv-pd-001');

    expect(result).not.toBeNull();
    expect(result?.invoiceNumber).toBe('NW-1001');
    expect(result?.extractedFields.length).toBeGreaterThan(0);
    expect(result?.auditEntries.length).toBeGreaterThan(0);
    expect(result?.pdfUrl).toMatch(/\.pdf$/);
  });

  it('does not leak the internal tenantId field onto the returned detail', async () => {
    const result = await client.getInvoiceDetail('platform-default', 'inv-pd-001');

    expect(result).not.toHaveProperty('tenantId');
  });

  it('returns null when the invoice id exists but belongs to a different tenant', async () => {
    const result = await client.getInvoiceDetail('gb-skips', 'inv-pd-001');

    expect(result).toBeNull();
  });

  it('returns null for an unknown invoice id', async () => {
    const result = await client.getInvoiceDetail('platform-default', 'does-not-exist');

    expect(result).toBeNull();
  });

  it('carries the isPotentialDuplicate/duplicateCheckReason fields through unchanged', async () => {
    const result = await client.getInvoiceDetail('platform-default', 'inv-pd-002');

    expect(result?.isPotentialDuplicate).toBe(true);
    expect(result?.duplicateCheckReason).toBeTruthy();
  });
});

describe('HttpInvoiceDetailClient', () => {
  beforeEach(() => {
    vi.mocked(httpClient.get).mockReset();
    vi.mocked(httpClient.getBlob).mockReset();
  });

  it('maps the real InvoiceDto field names (supplierInvoiceNumber/grossTotal/currency) to our InvoiceDetail shape', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce({
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
        sourceDocumentBlobName: 'platform-default/2026/07/inv-1.pdf',
        createdAtUtc: '2026-07-01T08:12:00Z',
      },
      recentAuditEntries: [],
      extractedFields: [],
    });
    vi.mocked(httpClient.getBlob).mockResolvedValueOnce(new Blob(['pdf-bytes']));
    const client = new HttpInvoiceDetailClient();

    const result = await client.getInvoiceDetail('platform-default', 'inv-1');

    expect(result?.invoiceNumber).toBe('NW-1001');
    expect(result?.amount).toBe(1240.5);
    expect(result?.currencyCode).toBe('GBP');
    expect(httpClient.get).toHaveBeenCalledWith('/api/invoices/inv-1');
    expect(httpClient.getBlob).toHaveBeenCalledWith('/api/invoices/inv-1/download');
  });

  it('returns a blob: URL for pdfUrl, not the raw download endpoint URL', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce({
      invoice: {
        id: 'inv-1',
        supplierName: 'X',
        supplierInvoiceNumber: 'X-1',
        invoiceDate: '2026-07-01',
        grossTotal: 1,
        currency: 'GBP',
        status: 'RECEIVED',
        isPotentialDuplicate: false,
        duplicateCheckReason: null,
        sourceDocumentBlobName: 'x',
        createdAtUtc: '2026-07-01T00:00:00Z',
      },
      recentAuditEntries: [],
      extractedFields: [],
    });
    vi.mocked(httpClient.getBlob).mockResolvedValueOnce(new Blob(['pdf-bytes']));
    const client = new HttpInvoiceDetailClient();

    const result = await client.getInvoiceDetail('platform-default', 'inv-1');

    expect(result?.pdfUrl?.startsWith('blob:')).toBe(true);
  });

  it('returns null on a 404 rather than throwing', async () => {
    vi.mocked(httpClient.get).mockRejectedValueOnce(new ApiError(404, 'Not found'));
    const client = new HttpInvoiceDetailClient();

    const result = await client.getInvoiceDetail('platform-default', 'does-not-exist');

    expect(result).toBeNull();
    expect(httpClient.getBlob).not.toHaveBeenCalled();
  });

  it('re-throws a non-404 error', async () => {
    vi.mocked(httpClient.get).mockRejectedValueOnce(new ApiError(500, 'Server error'));
    const client = new HttpInvoiceDetailClient();

    await expect(client.getInvoiceDetail('platform-default', 'inv-1')).rejects.toThrow('Server error');
  });

  it('still returns the invoice with pdfUrl null when the PDF download fails, instead of failing the whole page', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce({
      invoice: {
        id: 'inv-1',
        supplierName: 'X',
        supplierInvoiceNumber: 'X-1',
        invoiceDate: '2026-07-01',
        grossTotal: 1,
        currency: 'GBP',
        status: 'RECEIVED',
        isPotentialDuplicate: false,
        duplicateCheckReason: null,
        sourceDocumentBlobName: 'x',
        createdAtUtc: '2026-07-01T00:00:00Z',
      },
      recentAuditEntries: [],
      extractedFields: [],
    });
    vi.mocked(httpClient.getBlob).mockRejectedValueOnce(new ApiError(404, 'Not found'));
    const client = new HttpInvoiceDetailClient();

    const result = await client.getInvoiceDetail('platform-default', 'inv-1');

    expect(result).not.toBeNull();
    expect(result?.invoiceNumber).toBe('X-1');
    expect(result?.pdfUrl).toBeNull();
  });

  it('computes overallConfidenceScore as the mean of non-null field scores', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce({
      invoice: {
        id: 'inv-1',
        supplierName: 'X',
        supplierInvoiceNumber: 'X-1',
        invoiceDate: '2026-07-01',
        grossTotal: 1,
        currency: 'GBP',
        status: 'RECEIVED',
        isPotentialDuplicate: false,
        duplicateCheckReason: null,
        sourceDocumentBlobName: 'x',
        createdAtUtc: '2026-07-01T00:00:00Z',
      },
      recentAuditEntries: [],
      extractedFields: [
        { fieldKey: 'a', label: 'A', value: '1', confidenceScore: 0.8 },
        { fieldKey: 'b', label: 'B', value: '2', confidenceScore: 0.6 },
        { fieldKey: 'currency', label: 'Currency', value: 'GBP', confidenceScore: null },
      ],
    });
    vi.mocked(httpClient.getBlob).mockResolvedValueOnce(new Blob(['pdf-bytes']));
    const client = new HttpInvoiceDetailClient();

    const result = await client.getInvoiceDetail('platform-default', 'inv-1');

    expect(result?.overallConfidenceScore).toBeCloseTo(0.7);
  });
});
