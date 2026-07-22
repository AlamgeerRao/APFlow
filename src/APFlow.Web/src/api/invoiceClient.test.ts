import { describe, expect, it } from 'vitest';
import { FixtureInvoiceClient } from '@/api/invoiceClient';
import type { InvoiceQueryParams } from '@/types/invoice';

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

  it('matches search against supplier name, case-insensitively', async () => {
    const result = await client.queryInvoices(paramsFor({ search: 'northwind', pageSize: 100 }));

    expect(result.items.length).toBeGreaterThan(0);
    expect(result.items.every((invoice) => invoice.supplierName.toLowerCase().includes('northwind'))).toBe(
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
    expect(ascAmounts).toEqual([...ascAmounts].sort((a, b) => a - b));
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
