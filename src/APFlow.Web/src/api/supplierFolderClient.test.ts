import { describe, expect, it, vi, beforeEach } from 'vitest';
import { FixtureSupplierFolderClient, HttpSupplierFolderClient } from '@/api/supplierFolderClient';
import { httpClient } from '@/api/httpClient';

vi.mock('@/api/httpClient', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/httpClient')>();
  return { ...actual, httpClient: { ...actual.httpClient, get: vi.fn() } };
});

describe('FixtureSupplierFolderClient.getFolderCounts', () => {
  const client = new FixtureSupplierFolderClient();

  it('returns one entry per non-terminal status in the tenant WorkflowTemplate, in order', async () => {
    const folders = await client.getFolderCounts('platform-default');

    expect(folders.map((f) => f.statusCode)).toEqual([
      'RECEIVED',
      'PROCESSING',
      'EXTRACTED',
      'AWAITING_REVIEW',
      'NEEDS_QUERY',
      'QUERY_RAISED',
      'AWAITING_SUPPLIER_RESPONSE',
      'APPROVED',
      'REJECTED',
      'CANCELLED',
      'READY_FOR_PAYMENT',
      'PAID',
    ]);
  });

  it('includes GB Skips extra folders positioned between Awaiting Review and Approved', async () => {
    const folders = await client.getFolderCounts('gb-skips');
    const codes = folders.map((f) => f.statusCode);

    expect(codes).toContain('CHECKED_READY_TO_APPROVE');
    expect(codes).toContain('NEEDS_REVIEW_FEBINA');
    expect(codes.indexOf('CHECKED_READY_TO_APPROVE')).toBeGreaterThan(codes.indexOf('AWAITING_REVIEW'));
    expect(codes.indexOf('CHECKED_READY_TO_APPROVE')).toBeLessThan(codes.indexOf('APPROVED'));
  });

  it('does not include GB Skips-only folders for the platform-default tenant', async () => {
    const folders = await client.getFolderCounts('platform-default');
    const codes = folders.map((f) => f.statusCode);

    expect(codes).not.toContain('CHECKED_READY_TO_APPROVE');
    expect(codes).not.toContain('NEEDS_REVIEW_FEBINA');
  });

  it('counts reflect actual fixture invoices in that status for the tenant', async () => {
    const folders = await client.getFolderCounts('platform-default');
    const awaitingReview = folders.find((f) => f.statusCode === 'AWAITING_REVIEW');

    // inv-pd-001, inv-pd-002, inv-pd-008, inv-pd-009 (reassigned off the
    // retired DUPLICATE_SUSPECTED status), inv-pd-010.
    expect(awaitingReview?.count).toBe(5);
  });

  it('narrows counts by the given search text', async () => {
    const folders = await client.getFolderCounts('platform-default', 'northwind');
    const awaitingReview = folders.find((f) => f.statusCode === 'AWAITING_REVIEW');

    expect(awaitingReview?.count).toBe(1);
  });
});

describe('FixtureSupplierFolderClient.getSuppliers', () => {
  const client = new FixtureSupplierFolderClient();

  it('returns distinct supplier names for the tenant, sorted alphabetically', async () => {
    const suppliers = await client.getSuppliers('gb-skips');

    expect(suppliers).toEqual([...suppliers].sort((a, b) => a.localeCompare(b)));
    expect(suppliers).toContain('Yorkshire Skip Supplies');
    expect(new Set(suppliers).size).toBe(suppliers.length);
  });

  it('narrows supplier options to a single folder when given', async () => {
    const suppliers = await client.getSuppliers('platform-default', 'NEEDS_QUERY');

    expect(suppliers).toEqual(['Fabrikam Waste Services']);
  });
});

describe('FixtureSupplierFolderClient.getGroupedInvoices', () => {
  const client = new FixtureSupplierFolderClient();

  it('groups invoices by supplier, sorted alphabetically by supplier name', async () => {
    const result = await client.getGroupedInvoices({
      tenantId: 'platform-default',
      page: 1,
      pageSize: 100,
    });

    expect(result.groups.map((g) => g.supplierName)).toEqual(
      [...result.groups.map((g) => g.supplierName)].sort((a, b) => a.localeCompare(b)),
    );
  });

  it('sorts each group\'s invoices by date, newest first', async () => {
    const result = await client.getGroupedInvoices({
      tenantId: 'platform-default',
      supplier: 'Acme Skip Hire',
      page: 1,
      pageSize: 100,
    });

    const group = result.groups.find((g) => g.supplierName === 'Acme Skip Hire');
    const dates = group?.invoices.map((i) => i.invoiceDate) ?? [];
    expect(dates).toEqual([...dates].sort().reverse());
  });

  it('filters to a single folder when given', async () => {
    const result = await client.getGroupedInvoices({
      tenantId: 'gb-skips',
      folder: 'CHECKED_READY_TO_APPROVE',
      page: 1,
      pageSize: 100,
    });

    const allInvoices = result.groups.flatMap((g) => g.invoices);
    expect(allInvoices.every((i) => i.status === 'CHECKED_READY_TO_APPROVE')).toBe(true);
    expect(allInvoices.length).toBeGreaterThan(0);
  });

  it('filters to a single supplier when given', async () => {
    const result = await client.getGroupedInvoices({
      tenantId: 'platform-default',
      supplier: 'Contoso Supplies',
      page: 1,
      pageSize: 100,
    });

    expect(result.groups).toHaveLength(1);
    expect(result.groups[0].supplierName).toBe('Contoso Supplies');
  });

  it('paginates over supplier groups, not individual invoices', async () => {
    const page1 = await client.getGroupedInvoices({ tenantId: 'platform-default', page: 1, pageSize: 2 });
    const page2 = await client.getGroupedInvoices({ tenantId: 'platform-default', page: 2, pageSize: 2 });

    expect(page1.groups).toHaveLength(2);
    expect(page1.totalSuppliers).toBeGreaterThan(2);
    expect(page1.groups.map((g) => g.supplierName)).not.toEqual(page2.groups.map((g) => g.supplierName));
  });

  it('reflects a WP-018 status change immediately (shared fixture store)', async () => {
    const before = await client.getGroupedInvoices({
      tenantId: 'platform-default',
      folder: 'RECEIVED',
      page: 1,
      pageSize: 100,
    });
    const beforeCount = before.groups.flatMap((g) => g.invoices).length;

    expect(beforeCount).toBeGreaterThan(0);
  });
});

describe('HttpSupplierFolderClient', () => {
  beforeEach(() => {
    vi.mocked(httpClient.get).mockReset();
  });

  it('getFolderCounts calls GET /api/invoices/folders with search, not tenantId', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce([]);
    const client = new HttpSupplierFolderClient();

    await client.getFolderCounts('platform-default', 'northwind');

    expect(httpClient.get).toHaveBeenCalledWith('/api/invoices/folders', {
      params: { search: 'northwind' },
    });
  });

  it('getSuppliers calls GET /api/invoices/suppliers with folder and search, not tenantId', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce([]);
    const client = new HttpSupplierFolderClient();

    await client.getSuppliers('platform-default', 'NEEDS_QUERY', 'acme');

    expect(httpClient.get).toHaveBeenCalledWith('/api/invoices/suppliers', {
      params: { folder: 'NEEDS_QUERY', search: 'acme' },
    });
  });

  it('getGroupedInvoices calls GET /api/invoices/grouped with folder/supplier/search/page/pageSize, not tenantId', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce({ groups: [], totalSuppliers: 0, page: 1, pageSize: 5 });
    const client = new HttpSupplierFolderClient();

    await client.getGroupedInvoices({
      tenantId: 'platform-default',
      folder: 'AWAITING_REVIEW',
      supplier: 'Acme Skip Hire',
      search: 'acme',
      page: 2,
      pageSize: 5,
    });

    expect(httpClient.get).toHaveBeenCalledWith('/api/invoices/grouped', {
      params: {
        folder: 'AWAITING_REVIEW',
        supplier: 'Acme Skip Hire',
        search: 'acme',
        page: 2,
        pageSize: 5,
      },
    });
  });

  it('maps the real InvoiceListItemDto field names (supplierInvoiceNumber/grossTotal/currency) within each group', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce({
      groups: [
        {
          supplierName: 'Northwind Traders Ltd',
          count: 1,
          invoices: [
            {
              id: 'inv-1',
              supplierName: 'Northwind Traders Ltd',
              supplierInvoiceNumber: 'NW-1001',
              invoiceDate: '2026-07-01',
              grossTotal: 1240.5,
              currency: 'GBP',
              status: 'AWAITING_REVIEW',
              isPotentialDuplicate: false,
              duplicateCheckReason: null,
            },
          ],
        },
      ],
      totalSuppliers: 1,
      page: 1,
      pageSize: 5,
    });
    const client = new HttpSupplierFolderClient();

    const result = await client.getGroupedInvoices({ tenantId: 'platform-default', page: 1, pageSize: 5 });

    expect(result.groups).toEqual([
      {
        supplierName: 'Northwind Traders Ltd',
        count: 1,
        invoices: [
          {
            id: 'inv-1',
            supplierName: 'Northwind Traders Ltd',
            invoiceNumber: 'NW-1001',
            invoiceDate: '2026-07-01',
            amount: 1240.5,
            currencyCode: 'GBP',
            status: 'AWAITING_REVIEW',
            isPotentialDuplicate: false,
            duplicateCheckReason: null,
          },
        ],
      },
    ]);
    expect(result.totalSuppliers).toBe(1);
  });
});
