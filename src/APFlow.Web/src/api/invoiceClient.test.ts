import { describe, expect, it, vi, beforeEach } from 'vitest';
import { FixtureInvoiceClient, HttpInvoiceClient, matchesInvoiceSearch, compareInvoices } from '@/api/invoiceClient';
import type { InvoiceListItem, InvoiceQueryParams } from '@/types/invoice';
import { httpClient } from '@/api/httpClient';

vi.mock('@/api/httpClient', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/httpClient')>();
  return { ...actual, httpClient: { ...actual.httpClient, get: vi.fn() } };
});

function paramsFor(overrides: Partial<InvoiceQueryParams>): InvoiceQueryParams {
  return {
    tenantId: 'platform-default',
    sortBy: 'invoiceDate',
    sortDirection: 'asc',
    page: 1,
    pageSize: 10,
    ...overrides,
  };
}

// WP-077: supplierName/invoiceNumber are both genuinely nullable
// (invoice.Supplier?.Name / string? SupplierInvoiceNumber) - matchesInvoiceSearch
// and compareInvoices previously called string methods on them unconditionally,
// which would throw the same way formatCurrency did on a null currencyCode.
function invoiceWith(overrides: Partial<InvoiceListItem>): InvoiceListItem {
  return {
    id: 'inv-x',
    supplierName: 'Northwind Traders Ltd',
    invoiceNumber: 'NW-1001',
    invoiceDate: '2026-07-01',
    amount: 1240.5,
    currencyCode: 'GBP',
    status: 'AWAITING_REVIEW',
    isPotentialDuplicate: false,
    duplicateCheckReason: null,
    duplicateMatchInvoiceId: null,
    ...overrides,
  };
}

describe('matchesInvoiceSearch', () => {
  it('returns a fallback (no match) instead of throwing for a null supplierName', () => {
    const invoice = invoiceWith({ supplierName: null });
    expect(() => matchesInvoiceSearch(invoice, 'northwind')).not.toThrow();
    expect(matchesInvoiceSearch(invoice, 'northwind')).toBe(false);
  });

  it('returns a fallback (no match) instead of throwing for a null invoiceNumber', () => {
    const invoice = invoiceWith({ invoiceNumber: null });
    expect(() => matchesInvoiceSearch(invoice, 'nw-1001')).not.toThrow();
    expect(matchesInvoiceSearch(invoice, 'nw-1001')).toBe(false);
  });

  it('still matches on whichever field is non-null when the other is null', () => {
    const invoice = invoiceWith({ supplierName: null, invoiceNumber: 'NW-1001' });
    expect(matchesInvoiceSearch(invoice, 'nw-1001')).toBe(true);
  });
});

describe('compareInvoices', () => {
  it('sorts a null invoiceNumber as if empty instead of throwing', () => {
    const withNumber = invoiceWith({ invoiceNumber: 'NW-1001' });
    const withoutNumber = invoiceWith({ id: 'inv-y', invoiceNumber: null });
    const params = paramsFor({ sortBy: 'invoiceNumber', sortDirection: 'asc' });

    expect(() => compareInvoices(withNumber, withoutNumber, params)).not.toThrow();
    expect(compareInvoices(withNumber, withoutNumber, params)).toBeGreaterThan(0);
  });

  it('sorts a null supplierName as if empty instead of throwing', () => {
    const withName = invoiceWith({ supplierName: 'Northwind Traders Ltd' });
    const withoutName = invoiceWith({ id: 'inv-y', supplierName: null });
    const params = paramsFor({ sortBy: 'supplierName', sortDirection: 'asc' });

    expect(() => compareInvoices(withName, withoutName, params)).not.toThrow();
    expect(compareInvoices(withName, withoutName, params)).toBeGreaterThan(0);
  });
});

describe('FixtureInvoiceClient', () => {
  const client = new FixtureInvoiceClient();

  it('only returns invoices belonging to the requested tenant', async () => {
    const result = await client.queryInvoices(paramsFor({ tenantId: 'gb-skips', pageSize: 100 }));

    expect(result.items.length).toBeGreaterThan(0);
    expect(result.items.every((invoice) => invoice.status !== undefined)).toBe(true);
    // gb-skips fixtures include a CHECKED_READY_TO_APPROVE row; platform-default never does.
    expect(result.items.some((invoice) => invoice.status === 'CHECKED_READY_TO_APPROVE')).toBe(true);
  });

  it('does not leak the internal tenantId field onto returned items', async () => {
    const result = await client.queryInvoices(paramsFor({ pageSize: 100 }));

    for (const item of result.items) {
      expect(item).not.toHaveProperty('tenantId');
    }
  });

  it('filters by status when provided', async () => {
    const result = await client.queryInvoices(
      paramsFor({ status: 'NEEDS_QUERY', pageSize: 100 }),
    );

    expect(result.items.length).toBeGreaterThan(0);
    expect(result.items.every((invoice) => invoice.status === 'NEEDS_QUERY')).toBe(true);
  });

  // WP-074: the Query Queue nav view combines these three statuses.
  it('filters by statuses (a set), returning invoices matching any of them', async () => {
    const result = await client.queryInvoices(
      paramsFor({ statuses: ['NEEDS_QUERY', 'QUERY_RAISED', 'AWAITING_SUPPLIER_RESPONSE'], pageSize: 100 }),
    );

    expect(result.items.length).toBeGreaterThan(0);
    expect(
      result.items.every((invoice) =>
        ['NEEDS_QUERY', 'QUERY_RAISED', 'AWAITING_SUPPLIER_RESPONSE'].includes(invoice.status),
      ),
    ).toBe(true);
    expect(new Set(result.items.map((i) => i.status)).size).toBeGreaterThan(1);
  });

  it('statuses takes precedence over status when both are given', async () => {
    const result = await client.queryInvoices(
      paramsFor({ status: 'RECEIVED', statuses: ['NEEDS_QUERY'], pageSize: 100 }),
    );

    expect(result.items.length).toBeGreaterThan(0);
    expect(result.items.every((invoice) => invoice.status === 'NEEDS_QUERY')).toBe(true);
  });

  it('matches search against supplier name, case-insensitively', async () => {
    const result = await client.queryInvoices(paramsFor({ search: 'northwind', pageSize: 100 }));

    expect(result.items.length).toBeGreaterThan(0);
    expect(result.items.every((invoice) => invoice.supplierName?.toLowerCase().includes('northwind'))).toBe(
      true,
    );
  });

  it('matches search against invoice number', async () => {
    const result = await client.queryInvoices(paramsFor({ search: 'NW-1001', pageSize: 100 }));

    expect(result.items).toHaveLength(1);
    expect(result.items[0].invoiceNumber).toBe('NW-1001');
  });

  it('returns no results when search matches nothing', async () => {
    const result = await client.queryInvoices(paramsFor({ search: 'no-such-supplier-xyz', pageSize: 100 }));

    expect(result.items).toEqual([]);
    expect(result.totalCount).toBe(0);
  });

  it('sorts ascending and descending by amount', async () => {
    const ascending = await client.queryInvoices(
      paramsFor({ sortBy: 'amount', sortDirection: 'asc', pageSize: 100 }),
    );
    const descending = await client.queryInvoices(
      paramsFor({ sortBy: 'amount', sortDirection: 'desc', pageSize: 100 }),
    );

    const ascAmounts = ascending.items.map((i) => i.amount);
    const descAmounts = descending.items.map((i) => i.amount);
    expect(ascAmounts).toEqual([...ascAmounts].sort((a, b) => (a ?? 0) - (b ?? 0)));
    expect(descAmounts).toEqual([...ascAmounts].reverse());
  });

  it('paginates results and reports totalCount independent of page size', async () => {
    const page1 = await client.queryInvoices(paramsFor({ pageSize: 3, page: 1 }));
    const page2 = await client.queryInvoices(paramsFor({ pageSize: 3, page: 2 }));

    expect(page1.items).toHaveLength(3);
    expect(page1.totalCount).toBeGreaterThan(3);
    expect(page1.items.map((i) => i.id)).not.toEqual(page2.items.map((i) => i.id));
  });

  it('correctly identifies fixture rows flagged as potential duplicates', async () => {
    const result = await client.queryInvoices(paramsFor({ pageSize: 100 }));
    const duplicate = result.items.find((i) => i.invoiceNumber === 'CS-2045');

    expect(duplicate?.isPotentialDuplicate).toBe(true);
    expect(duplicate?.duplicateCheckReason).toBeTruthy();
  });
});

describe('HttpInvoiceClient', () => {
  beforeEach(() => {
    vi.mocked(httpClient.get).mockReset();
  });

  it('sends invoiceNumber (not search) and sortDescending (not sortDirection) as query params, matching WP-058\'s real GetAll endpoint', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce({ items: [], totalCount: 0, page: 1, pageSize: 10 });
    const client = new HttpInvoiceClient();

    await client.queryInvoices(
      paramsFor({ search: 'northwind', sortBy: 'invoiceDate', sortDirection: 'desc', page: 2, pageSize: 10 }),
    );

    expect(httpClient.get).toHaveBeenCalledWith('/api/invoices', {
      params: {
        invoiceNumber: 'northwind',
        status: undefined,
        statuses: undefined,
        sortBy: 'invoiceDate',
        sortDescending: 'true',
        page: 2,
        pageSize: 10,
      },
    });
  });

  it('sends sortDescending: "false" for ascending sort', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce({ items: [], totalCount: 0, page: 1, pageSize: 10 });
    const client = new HttpInvoiceClient();

    await client.queryInvoices(paramsFor({ sortDirection: 'asc' }));

    expect(httpClient.get).toHaveBeenCalledWith(
      '/api/invoices',
      expect.objectContaining({ params: expect.objectContaining({ sortDescending: 'false' }) }),
    );
  });

  // WP-074: the Query Queue nav view sends this to combine
  // NEEDS_QUERY/QUERY_RAISED/AWAITING_SUPPLIER_RESPONSE in one request.
  it('sends statuses as an array param, for the Query Queue combined view', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce({ items: [], totalCount: 0, page: 1, pageSize: 10 });
    const client = new HttpInvoiceClient();

    await client.queryInvoices(paramsFor({ statuses: ['NEEDS_QUERY', 'QUERY_RAISED', 'AWAITING_SUPPLIER_RESPONSE'] }));

    expect(httpClient.get).toHaveBeenCalledWith(
      '/api/invoices',
      expect.objectContaining({
        params: expect.objectContaining({ statuses: ['NEEDS_QUERY', 'QUERY_RAISED', 'AWAITING_SUPPLIER_RESPONSE'] }),
      }),
    );
  });

  it('maps the real InvoiceDto field names (supplierInvoiceNumber/grossTotal/currency) to our InvoiceListItem shape', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce({
      items: [
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
      totalCount: 1,
      page: 1,
      pageSize: 10,
    });
    const client = new HttpInvoiceClient();

    const result = await client.queryInvoices(paramsFor({}));

    expect(result.items).toEqual([
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
    ]);
  });
});
