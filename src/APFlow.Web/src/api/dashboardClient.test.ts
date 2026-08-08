import { describe, expect, it, vi } from 'vitest';
import { FixtureDashboardClient, HttpDashboardClient } from '@/api/dashboardClient';
import { httpClient } from '@/api/httpClient';

vi.mock('@/api/httpClient', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/httpClient')>();
  return { ...actual, httpClient: { ...actual.httpClient, get: vi.fn() } };
});

describe('FixtureDashboardClient.getRecentActivity', () => {
  const client = new FixtureDashboardClient();

  it('returns only the given tenant\'s invoices', async () => {
    const items = await client.getRecentActivity('gb-skips', 100);

    expect(items.length).toBeGreaterThan(0);
    expect(items.every((item) => item.id.length > 0)).toBe(true);
  });

  it('respects the limit', async () => {
    const items = await client.getRecentActivity('platform-default', 2);

    expect(items.length).toBe(2);
  });

  it('orders newest invoiceDate first', async () => {
    const items = await client.getRecentActivity('platform-default', 100);
    const dates = items.map((item) => item.createdAtUtc);

    expect(dates).toEqual([...dates].sort().reverse());
  });

  it('returns an empty array for a tenant with no fixture invoices', async () => {
    const items = await client.getRecentActivity('unknown-tenant', 5);

    expect(items).toEqual([]);
  });
});

describe('HttpDashboardClient.getRecentActivity', () => {
  it('calls GET /api/invoices sorted by CreatedAtUtc descending, mapped to RecentActivityItem', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce({
      items: [
        {
          id: 'inv-1',
          supplierName: 'Acme Ltd',
          supplierInvoiceNumber: 'ACM-100',
          status: 'AWAITING_REVIEW',
          createdAtUtc: '2026-08-08T09:00:00Z',
        },
      ],
    });

    const client = new HttpDashboardClient();
    const items = await client.getRecentActivity('gb-skips', 8);

    expect(httpClient.get).toHaveBeenCalledWith('/api/invoices', {
      params: { sortBy: 'CreatedAtUtc', sortDescending: 'true', page: 1, pageSize: 8 },
    });
    expect(items).toEqual([
      {
        id: 'inv-1',
        supplierName: 'Acme Ltd',
        invoiceNumber: 'ACM-100',
        status: 'AWAITING_REVIEW',
        createdAtUtc: '2026-08-08T09:00:00Z',
      },
    ]);
  });
});
