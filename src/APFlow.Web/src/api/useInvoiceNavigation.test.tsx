import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { useInvoiceNavigation } from '@/api/useInvoiceNavigation';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';
import { httpClient } from '@/api/httpClient';
import type { InvoiceListItem } from '@/types/invoice';

// WP-020: invoiceClient is now HTTP-backed; mock httpClient.get directly
// with a fixed, deterministic ordered page so the navigation math can be
// asserted precisely.
vi.mock('@/api/httpClient', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/httpClient')>();
  return { ...actual, httpClient: { ...actual.httpClient, get: vi.fn() } };
});

const orderedItems: InvoiceListItem[] = Array.from({ length: 5 }, (_, i) => ({
  id: `inv-${i + 1}`,
  supplierName: `Supplier ${i + 1}`,
  invoiceNumber: `INV-${i + 1}`,
  invoiceDate: `2026-07-0${5 - i}`,
  amount: 100 * (i + 1),
  currencyCode: 'GBP',
  status: 'AWAITING_REVIEW',
  isPotentialDuplicate: false,
  duplicateCheckReason: null,
}));

const authValue: AuthContextValue = {
  user: { tenantId: 'platform-default', tenantName: 'Platform Default Tenant', displayName: 'Test User', roles: ['AP_REVIEWER'] },
  isAuthenticated: true,
  signIn: () => {},
  signOut: () => {},
};

function wrapper({ children }: { children: ReactNode }) {
  return <AuthContext.Provider value={authValue}>{children}</AuthContext.Provider>;
}

beforeEach(() => {
  vi.mocked(httpClient.get).mockReset();
  vi.mocked(httpClient.get).mockResolvedValue({
    items: orderedItems,
    totalCount: orderedItems.length,
    page: 1,
    pageSize: 100,
  });
});

describe('useInvoiceNavigation', () => {
  it('returns previous/next ids and position matching the mocked order', async () => {
    const middleId = orderedItems[2].id;

    const { result } = renderHook(() => useInvoiceNavigation(middleId), { wrapper });

    await waitFor(() => expect(result.current.total).toBe(orderedItems.length));

    expect(result.current.position).toBe(3);
    expect(result.current.previousId).toBe(orderedItems[1].id);
    expect(result.current.nextId).toBe(orderedItems[3].id);
  });

  it('returns a null previousId for the first invoice in the order', async () => {
    const { result } = renderHook(() => useInvoiceNavigation(orderedItems[0].id), { wrapper });

    await waitFor(() => expect(result.current.total).toBe(orderedItems.length));
    expect(result.current.previousId).toBeNull();
    expect(result.current.nextId).not.toBeNull();
  });

  it('returns a null nextId for the last invoice in the order', async () => {
    const lastId = orderedItems[orderedItems.length - 1].id;

    const { result } = renderHook(() => useInvoiceNavigation(lastId), { wrapper });

    await waitFor(() => expect(result.current.total).toBe(orderedItems.length));
    expect(result.current.nextId).toBeNull();
    expect(result.current.previousId).not.toBeNull();
  });

  it('returns nulls when there is no current invoice id', async () => {
    const { result } = renderHook(() => useInvoiceNavigation(undefined), { wrapper });
    await act(async () => {
      await Promise.resolve();
    });

    expect(result.current.previousId).toBeNull();
    expect(result.current.nextId).toBeNull();
    expect(result.current.position).toBeNull();
  });

  // WP-072 follow-up: real live traffic showed this hook's old single-request
  // pageSize: 1000 failing with a 400 - the real backend caps pageSize at 100.
  it('requests pageSize 100 (the real backend cap), not 1000', async () => {
    const { result } = renderHook(() => useInvoiceNavigation(orderedItems[0].id), { wrapper });
    await waitFor(() => expect(result.current.total).toBe(orderedItems.length));

    expect(httpClient.get).toHaveBeenCalledWith(
      '/api/invoices',
      expect.objectContaining({ params: expect.objectContaining({ pageSize: 100 }) }),
    );
  });

  it('loops across pages when a tenant has more invoices than one page', async () => {
    const manyItems: InvoiceListItem[] = Array.from({ length: 120 }, (_, i) => ({
      id: `many-${i + 1}`,
      supplierName: `Supplier ${i + 1}`,
      invoiceNumber: `INV-${i + 1}`,
      invoiceDate: '2026-07-01',
      amount: 100,
      currencyCode: 'GBP',
      status: 'AWAITING_REVIEW',
      isPotentialDuplicate: false,
      duplicateCheckReason: null,
    }));

    vi.mocked(httpClient.get).mockReset();
    vi.mocked(httpClient.get).mockImplementation(async (_path, options) => {
      const page = ((options as { params?: { page?: number } } | undefined)?.params?.page ?? 1) as number;
      const start = (page - 1) * 100;
      return {
        items: manyItems.slice(start, start + 100),
        totalCount: manyItems.length,
        page,
        pageSize: 100,
      };
    });

    const { result } = renderHook(() => useInvoiceNavigation(manyItems[110].id), { wrapper });
    await waitFor(() => expect(result.current.total).toBe(manyItems.length));

    expect(httpClient.get).toHaveBeenCalledTimes(2);
    expect(result.current.position).toBe(111);
  });
});
